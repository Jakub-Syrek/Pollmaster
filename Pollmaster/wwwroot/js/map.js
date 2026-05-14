// Pollmaster Leaflet interop. Exposed via window.pollmasterMap and called from Blazor JSRuntime.
(function () {
    'use strict';

    const HEATMAP_POLLUTANTS = ['PM10', 'PM2.5', 'NO2', 'SO2', 'O3'];

    const state = {
        map: null,
        markerLayer: null,
        heatLayer: null,
        markers: new Map(),
        stations: [],
        dotnetRef: null,
        activeLayer: 'markers',
        satelliteLayer: null,
        satelliteLayerKey: null,
        satelliteMarker: null
    };

    // NASA GIBS WMTS endpoints — public, no API key, served via CloudFront. Each layer is a
    // daily mosaic; we pin to "default" so GIBS picks the most recent available date.
    // GoogleMapsCompatible_Level6 means tiles exist up to zoom 6 (continent-scale view);
    // Leaflet falls back to interpolation when the user zooms in further.
    const SATELLITE_LAYERS = {
        'aod': {
            label: 'Aerosols',
            title: 'MODIS Aqua — Aerosol Optical Depth (PM proxy)',
            url: 'https://gibs.earthdata.nasa.gov/wmts/epsg3857/best/MODIS_Aqua_Aerosol/default/{time}/GoogleMapsCompatible_Level6/{z}/{y}/{x}.png',
            maxZoom: 6,
            attribution: '&copy; NASA EOSDIS GIBS — MODIS Aqua AOD'
        },
        'no2': {
            label: 'NO₂',
            title: 'OMI — Tropospheric NO₂ column',
            url: 'https://gibs.earthdata.nasa.gov/wmts/epsg3857/best/OMI_Nitrogen_Dioxide_Tropo_Column/default/{time}/GoogleMapsCompatible_Level6/{z}/{y}/{x}.png',
            maxZoom: 6,
            attribution: '&copy; NASA EOSDIS GIBS — OMI NO₂'
        }
    };

    // GIBS publishes daily mosaics with a ~1-day latency. Use yesterday's date so the
    // very-recent-day-not-yet-published case never returns 404 tiles.
    function gibsTimeParam() {
        const d = new Date(Date.now() - 24 * 3600 * 1000);
        const yyyy = d.getUTCFullYear();
        const mm = String(d.getUTCMonth() + 1).padStart(2, '0');
        const dd = String(d.getUTCDate()).padStart(2, '0');
        return yyyy + '-' + mm + '-' + dd;
    }

    const AQ_LABELS = {
        '-1': 'No data',
        '0': 'Very good',
        '1': 'Good',
        '2': 'Moderate',
        '3': 'Sufficient',
        '4': 'Bad',
        '5': 'Very bad'
    };

    // WHO 2021 short-term air-quality guideline values (μg/m³, 24h or 8h). Used as the
    // 100%-fill point on the per-pollutant bar. Values above the guideline render red.
    const POLLUTANT_LIMITS = {
        'PM10': 45,
        'PM2.5': 15,
        'NO2': 25,
        'SO2': 40,
        'O3': 100,
        'CO': 4000,
        'C6H6': 5,
        'BaP(PM10)': 0.001
    };

    const HEAT_GRADIENT = {
        0.0: '#57b108',
        0.25: '#b0dd10',
        0.5: '#ffd911',
        0.75: '#e58100',
        1.0: '#b21f00'
    };

    function indexClass(value) {
        if (value === null || value === undefined || value < 0) {
            return 'aq-color--unknown';
        }
        return 'aq-color-' + value;
    }

    function indexLabel(value) {
        if (value === null || value === undefined) {
            return AQ_LABELS['-1'];
        }
        const key = String(value);
        return AQ_LABELS[key] || AQ_LABELS['-1'];
    }

    function escapeHtml(input) {
        if (input === null || input === undefined) {
            return '';
        }
        return String(input)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    // GIOŚ publishes every pollutant concentration in micrograms per cubic metre.
    // We bake the unit in here so the popup can render straight from the overview payload
    // without round-tripping to /api/stations/{id}/snapshot just to learn the unit string.
    const POLLUTANT_UNIT = 'μg/m³';

    function popupSkeleton(station) {
        const indexHtml = '<span class="aq-popup__index ' + indexClass(station.severity) +
            '">' + escapeHtml(indexLabel(station.severity)) + '</span>';
        return '<div class="aq-popup">' +
            '<div class="aq-popup__title">' + escapeHtml(station.name) + '</div>' +
            '<div class="aq-popup__city">' + escapeHtml(station.city || '') + '</div>' +
            indexHtml +
            '<div class="aq-popup__body">' +
            pollutantListHtml(station.pollutants) +
            '</div></div>';
    }

    function pollutantListHtml(pollutants) {
        if (!pollutants || pollutants.length === 0) {
            return '<div class="aq-popup__loading">No sensor readings.</div>';
        }
        const sensors = pollutants
            .slice()
            // Show the worst offender first (highest WHO ratio), then alphabetical fallback.
            .sort(function (a, b) {
                const aRatio = (a.ratio === null || a.ratio === undefined) ? -1 : a.ratio;
                const bRatio = (b.ratio === null || b.ratio === undefined) ? -1 : b.ratio;
                if (bRatio !== aRatio) {
                    return bRatio - aRatio;
                }
                return String(a.code).localeCompare(String(b.code));
            })
            .map(function (p) {
                return { code: p.code, value: p.value, unit: POLLUTANT_UNIT };
            });
        return sensorsHtml(sensors);
    }

    function formatValue(value, unit) {
        if (value === null || value === undefined) {
            return '&mdash;';
        }
        const fixed = Math.abs(value) < 1 ? value.toFixed(3) : value.toFixed(1);
        return fixed + ' ' + escapeHtml(unit || '');
    }

    function barFillPercent(code, value) {
        if (value === null || value === undefined) {
            return 0;
        }
        const limit = POLLUTANT_LIMITS[code];
        if (!limit || limit <= 0) {
            return 0;
        }
        const pct = (value / limit) * 100;
        if (!isFinite(pct) || pct < 0) {
            return 0;
        }
        return Math.min(pct, 200);
    }

    function barClass(percent) {
        if (percent === 0) {
            return 'aq-popup__bar--empty';
        }
        if (percent <= 50) {
            return 'aq-popup__bar--good';
        }
        if (percent <= 100) {
            return 'aq-popup__bar--warn';
        }
        return 'aq-popup__bar--bad';
    }

    function sensorRow(sensor) {
        const limit = POLLUTANT_LIMITS[sensor.code];
        const hasValue = sensor.value !== null && sensor.value !== undefined;
        const fillPercent = barFillPercent(sensor.code, sensor.value);
        const trackClass = hasValue ? 'aq-popup__bar' : 'aq-popup__bar aq-popup__bar--idle';
        const fillClass = 'aq-popup__bar-fill ' + barClass(fillPercent);
        const widthStyle = 'width: ' + Math.min(fillPercent, 100).toFixed(1) + '%;';
        const limitLabel = limit ? ' / ' + limit + ' WHO' : '';
        const rowClass = hasValue ? 'aq-popup__sensor' : 'aq-popup__sensor aq-popup__sensor--empty';

        return '<li class="' + rowClass + '">' +
            '<div class="aq-popup__sensor-head">' +
            '<span class="aq-popup__sensor-name">' + escapeHtml(sensor.code) + '</span>' +
            '<span class="aq-popup__sensor-value">' + formatValue(sensor.value, sensor.unit) +
            (hasValue && limit ? '<small>' + limitLabel + '</small>' : '') +
            '</span></div>' +
            '<div class="' + trackClass + '"><div class="' + fillClass + '" style="' + widthStyle + '"></div></div>' +
            '</li>';
    }

    function sensorsHtml(sensors) {
        if (!sensors || sensors.length === 0) {
            return '<div class="aq-popup__loading">No sensor readings.</div>';
        }
        const items = sensors.map(sensorRow).join('');
        return '<ul class="aq-popup__sensors">' + items + '</ul>';
    }

    function buildIcon(station) {
        const severityClass = indexClass(station.severity);
        const pulse = station.severity >= 4 ? ' aq-marker--pulse' : '';
        return L.divIcon({
            className: '',
            html: '<div class="aq-marker ' + severityClass + pulse + '"></div>',
            iconSize: [22, 22],
            iconAnchor: [11, 11]
        });
    }

    function findPollutantRatio(station, code) {
        if (!station.pollutants) {
            return null;
        }
        for (const p of station.pollutants) {
            if (p.code === code && p.ratio !== null && p.ratio !== undefined) {
                return p.ratio;
            }
        }
        return null;
    }

    function buildHeatPoints(pollutantCode) {
        const points = [];
        for (const station of state.stations) {
            const ratio = findPollutantRatio(station, pollutantCode);
            if (ratio === null) {
                continue;
            }
            // Clamp intensity to the [0, 2] range so Leaflet.heat normalizes against the gradient.
            const intensity = Math.min(Math.max(ratio, 0), 2);
            points.push([station.latitude, station.longitude, intensity]);
        }
        return points;
    }

    function buildHeatLayer(pollutantCode) {
        const points = buildHeatPoints(pollutantCode);
        if (points.length === 0) {
            return null;
        }
        return L.heatLayer(points, {
            radius: 38,
            blur: 28,
            maxZoom: 11,
            max: 1.5,
            minOpacity: 0.4,
            gradient: HEAT_GRADIENT
        });
    }

    function setLayer(layerKey) {
        if (!state.map) {
            return;
        }
        state.activeLayer = layerKey;
        if (state.heatLayer) {
            state.map.removeLayer(state.heatLayer);
            state.heatLayer = null;
        }
        if (layerKey === 'markers') {
            state.map.addLayer(state.markerLayer);
            return;
        }
        state.map.removeLayer(state.markerLayer);
        const heat = buildHeatLayer(layerKey);
        if (heat) {
            heat.addTo(state.map);
            state.heatLayer = heat;
        }
    }

    function buildLayerControl() {
        const control = L.control({ position: 'topright' });
        control.onAdd = function () {
            const wrapper = L.DomUtil.create('div', 'aq-layer-switch leaflet-bar');
            const buttons = [{ key: 'markers', label: 'Markers' }]
                .concat(HEATMAP_POLLUTANTS.map(code => ({ key: code, label: code })));

            buttons.forEach(function (btn) {
                const node = L.DomUtil.create('button', 'aq-layer-switch__btn', wrapper);
                node.type = 'button';
                node.textContent = btn.label;
                node.dataset.key = btn.key;
                if (btn.key === state.activeLayer) {
                    node.classList.add('aq-layer-switch__btn--active');
                }
                L.DomEvent.disableClickPropagation(node);
                L.DomEvent.on(node, 'click', function () {
                    wrapper.querySelectorAll('.aq-layer-switch__btn').forEach(function (b) {
                        b.classList.remove('aq-layer-switch__btn--active');
                    });
                    node.classList.add('aq-layer-switch__btn--active');
                    setLayer(btn.key);
                });
            });
            return wrapper;
        };
        return control;
    }

    function initMap(elementId, dotnetRef) {
        if (state.map) {
            state.map.remove();
        }
        state.dotnetRef = dotnetRef;
        state.map = L.map(elementId, {
            center: [52.0, 19.4],
            zoom: 6,
            preferCanvas: true
        });

        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 18,
            attribution: '&copy; OpenStreetMap contributors | Data: GIO&Sacute;',
            // crossOrigin lets html2canvas-based screenshots / recordings read the tile pixels
            // (OSM serves Access-Control-Allow-Origin: * on the tile endpoint).
            crossOrigin: true
        }).addTo(state.map);

        state.markerLayer = L.layerGroup().addTo(state.map);
        buildLayerControl().addTo(state.map);
        buildSatelliteControl().addTo(state.map);

        // Map-background click (i.e. not on a marker) asks the backend for a satellite-
        // assimilated reading at the clicked coordinate. Marker clicks bubble through their
        // own popup binding and don't fire this — Leaflet stops propagation for layers.
        state.map.on('click', function (ev) {
            if (!state.dotnetRef) {
                return;
            }
            const lat = ev.latlng.lat;
            const lon = ev.latlng.lng;
            placeSatelliteProbe(lat, lon, 'Loading satellite reading…');
            state.dotnetRef.invokeMethodAsync('OnMapClickedAsync', lat, lon)
                .catch(function () { /* Blazor side reports via setSatelliteReading */ });
        });
    }

    function placeSatelliteProbe(lat, lon, message) {
        if (!state.map) {
            return;
        }
        if (state.satelliteMarker) {
            state.satelliteMarker.setLatLng([lat, lon]);
        } else {
            state.satelliteMarker = L.marker([lat, lon], {
                icon: L.divIcon({
                    className: '',
                    html: '<div class="aq-probe"></div>',
                    iconSize: [18, 18],
                    iconAnchor: [9, 9]
                })
            }).addTo(state.map);
        }
        state.satelliteMarker.bindPopup(
            '<div class="aq-popup aq-popup--probe">' +
            '<div class="aq-popup__title">Satellite (' + lat.toFixed(3) + ', ' + lon.toFixed(3) + ')</div>' +
            '<div class="aq-popup__loading">' + escapeHtml(message) + '</div></div>'
        ).openPopup();
    }

    // Called from Blazor once the /api/satellite/point round-trip completes.
    function setSatelliteReading(reading) {
        if (!state.satelliteMarker) {
            return;
        }
        const html = reading
            ? satellitePopupHtml(reading)
            : '<div class="aq-popup aq-popup--probe"><div class="aq-popup__loading">Satellite data unavailable.</div></div>';
        state.satelliteMarker.bindPopup(html).openPopup();
    }

    function satellitePopupHtml(reading) {
        const aqiLabels = ['Unknown', 'Good', 'Fair', 'Moderate', 'Poor', 'Very Poor'];
        const aqi = reading.aqi || 0;
        const label = aqiLabels[Math.min(Math.max(aqi, 0), aqiLabels.length - 1)];
        const components = reading.components || {};
        const sensors = Object.keys(components)
            .sort()
            .map(function (code) {
                return { code: code, value: components[code], unit: POLLUTANT_UNIT };
            });
        const subtitle = reading.observedAt
            ? new Date(reading.observedAt).toLocaleString()
            : '';
        return '<div class="aq-popup aq-popup--probe">' +
            '<div class="aq-popup__title">Satellite (' +
            reading.latitude.toFixed(3) + ', ' + reading.longitude.toFixed(3) + ')</div>' +
            '<div class="aq-popup__city">' + escapeHtml(subtitle) + '</div>' +
            '<span class="aq-popup__index aq-color-' + Math.max(aqi - 1, 0) + '">' +
            escapeHtml(label) + ' (AQI ' + aqi + ')</span>' +
            '<div class="aq-popup__body">' + sensorsHtml(sensors) + '</div>' +
            '<div class="aq-popup__attribution">Source: OpenWeatherMap (Sentinel-5P assimilated)</div>' +
            '</div>';
    }

    function buildSatelliteControl() {
        const control = L.control({ position: 'topleft' });
        control.onAdd = function () {
            const wrapper = L.DomUtil.create('div', 'aq-sat-switch leaflet-bar');
            const buttons = [{ key: null, label: 'No sat' }]
                .concat(Object.keys(SATELLITE_LAYERS).map(function (k) {
                    return { key: k, label: SATELLITE_LAYERS[k].label };
                }));
            buttons.forEach(function (btn) {
                const node = L.DomUtil.create('button', 'aq-sat-switch__btn', wrapper);
                node.type = 'button';
                node.textContent = btn.label;
                node.title = btn.key ? SATELLITE_LAYERS[btn.key].title : 'Hide satellite overlay';
                if (btn.key === state.satelliteLayerKey) {
                    node.classList.add('aq-sat-switch__btn--active');
                }
                L.DomEvent.disableClickPropagation(node);
                L.DomEvent.on(node, 'click', function () {
                    wrapper.querySelectorAll('.aq-sat-switch__btn').forEach(function (b) {
                        b.classList.remove('aq-sat-switch__btn--active');
                    });
                    node.classList.add('aq-sat-switch__btn--active');
                    setSatelliteLayer(btn.key);
                });
            });
            return wrapper;
        };
        return control;
    }

    function setSatelliteLayer(layerKey) {
        if (!state.map) {
            return;
        }
        if (state.satelliteLayer) {
            state.map.removeLayer(state.satelliteLayer);
            state.satelliteLayer = null;
        }
        state.satelliteLayerKey = layerKey;
        if (!layerKey || !SATELLITE_LAYERS[layerKey]) {
            return;
        }
        const def = SATELLITE_LAYERS[layerKey];
        const url = def.url.replace('{time}', gibsTimeParam());
        state.satelliteLayer = L.tileLayer(url, {
            maxZoom: 18,
            maxNativeZoom: def.maxZoom,
            opacity: 0.55,
            attribution: def.attribution,
            crossOrigin: true
        });
        state.satelliteLayer.addTo(state.map);
    }

    function addStations(stations) {
        if (!state.map || !state.markerLayer) {
            return;
        }
        state.markerLayer.clearLayers();
        state.markers.clear();
        state.stations = Array.isArray(stations) ? stations : [];

        state.stations.forEach(function (station) {
            if (station.latitude === null || station.longitude === null) {
                return;
            }
            const marker = L.marker([station.latitude, station.longitude], {
                icon: buildIcon(station)
            });
            // Popup renders fully from the overview payload that came in with addStations,
            // so opening it never blocks on /api/stations/{id}/snapshot — the data is
            // already on the JS side.
            marker.bindPopup(popupSkeleton(station));
            marker.addTo(state.markerLayer);
            state.markers.set(station.id, marker);
        });

        // If a heatmap layer is currently active, rebuild it now that data is loaded.
        if (state.activeLayer !== 'markers') {
            setLayer(state.activeLayer);
        }
    }

    function dispose() {
        if (state.map) {
            state.map.remove();
        }
        state.map = null;
        state.markerLayer = null;
        state.heatLayer = null;
        state.markers.clear();
        state.stations = [];
        state.dotnetRef = null;
        state.activeLayer = 'markers';
        state.satelliteLayer = null;
        state.satelliteLayerKey = null;
        state.satelliteMarker = null;
    }

    window.pollmasterMap = {
        initMap: initMap,
        addStations: addStations,
        setSatelliteReading: setSatelliteReading,
        // updateStationSensors removed — popups now serve straight from the overview payload.
        dispose: dispose
    };
})();
