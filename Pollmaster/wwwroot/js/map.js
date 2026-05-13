// Pollmaster Leaflet interop. Exposed via window.pollmasterMap and called from Blazor JSRuntime.
(function () {
    'use strict';

    const state = {
        map: null,
        markerLayer: null,
        markers: new Map(),
        dotnetRef: null
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
        const indexHtml = '<span class="aq-popup__index ' + indexClass(station.indexLevel) +
            '">' + escapeHtml(indexLabel(station.indexLevel)) + '</span>';
        return '<div class="aq-popup">' +
            '<div class="aq-popup__title">' + escapeHtml(station.name) + '</div>' +
            '<div class="aq-popup__city">' + escapeHtml(station.city || '') + '</div>' +
            indexHtml +
            '<div class="aq-popup__body" data-station-id="' + station.id + '">' +
            '<div class="aq-popup__loading">Loading sensors...</div>' +
            '</div></div>';
    }

    function sensorsHtml(sensors) {
        if (!sensors || sensors.length === 0) {
            return '<div class="aq-popup__loading">No sensor readings.</div>';
        }
        const items = sensors.map(function (s) {
            const value = (s.value === null || s.value === undefined)
                ? '&mdash;'
                : Number(s.value).toFixed(1) + ' ' + escapeHtml(s.unit || '');
            return '<li><span class="aq-popup__sensor-name">' + escapeHtml(s.code) +
                '</span><span class="aq-popup__sensor-value">' + value + '</span></li>';
        }).join('');
        return '<ul class="aq-popup__sensors">' + items + '</ul>';
    }

    function buildIcon(level) {
        return L.divIcon({
            className: '',
            html: '<div class="aq-marker ' + indexClass(level) + '"></div>',
            iconSize: [22, 22],
            iconAnchor: [11, 11]
        });
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
    }

    function addStations(stations) {
        if (!state.map || !state.markerLayer) {
            return;
        }
        state.markerLayer.clearLayers();
        state.markers.clear();
        stations.forEach(function (station) {
            if (station.latitude === null || station.longitude === null) {
                return;
            }
            const marker = L.marker([station.latitude, station.longitude], {
                icon: buildIcon(station.indexLevel)
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
        state.markers.clear();
        state.dotnetRef = null;
    }

    window.pollmasterMap = {
        initMap: initMap,
        addStations: addStations,
        updateStationSensors: updateStationSensors,
        dispose: dispose
    };
})();
