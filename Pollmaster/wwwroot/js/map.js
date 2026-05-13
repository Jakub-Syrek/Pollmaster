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
        activeLayer: 'markers'
    };

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

    function popupSkeleton(station) {
        const indexHtml = '<span class="aq-popup__index ' + indexClass(station.severity) +
            '">' + escapeHtml(indexLabel(station.severity)) + '</span>';
        return '<div class="aq-popup">' +
            '<div class="aq-popup__title">' + escapeHtml(station.name) + '</div>' +
            '<div class="aq-popup__city">' + escapeHtml(station.city || '') + '</div>' +
            indexHtml +
            '<div class="aq-popup__body" data-station-id="' + station.id + '">' +
            '<div class="aq-popup__loading">Loading sensors...</div>' +
            '</div></div>';
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
            attribution: '&copy; OpenStreetMap contributors | Data: GIO&Sacute;'
        }).addTo(state.map);

        state.markerLayer = L.layerGroup().addTo(state.map);
        buildLayerControl().addTo(state.map);
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
            marker.bindPopup(popupSkeleton(station));
            marker.on('popupopen', function () {
                if (state.dotnetRef) {
                    state.dotnetRef.invokeMethodAsync('OnStationPopupOpenedAsync', station.id)
                        .catch(function (err) { console.error('OnStationPopupOpenedAsync failed', err); });
                }
            });
            marker.addTo(state.markerLayer);
            state.markers.set(station.id, marker);
        });

        // If a heatmap layer is currently active, rebuild it now that data is loaded.
        if (state.activeLayer !== 'markers') {
            setLayer(state.activeLayer);
        }
    }

    function updateStationSensors(stationId, sensors) {
        const marker = state.markers.get(stationId);
        if (!marker) {
            return;
        }
        const popup = marker.getPopup();
        if (!popup) {
            return;
        }
        const element = popup.getElement();
        if (!element) {
            return;
        }
        const body = element.querySelector('.aq-popup__body[data-station-id="' + stationId + '"]');
        if (body) {
            body.innerHTML = sensorsHtml(sensors);
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
    }

    window.pollmasterMap = {
        initMap: initMap,
        addStations: addStations,
        updateStationSensors: updateStationSensors,
        dispose: dispose
    };
})();
