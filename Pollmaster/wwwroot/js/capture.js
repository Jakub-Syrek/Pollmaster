// Pollmaster screenshot + screen-recording interop. Exposed via window.pollmasterCapture
// and called from the Blazor MapView component. Both paths are html2canvas-based because
// MAUI WebView (especially on Android) does not expose getDisplayMedia, and Leaflet tile
// layers render to <img> elements that captureStream cannot pick up directly.
//
// Quality is governed by three knobs:
//   - scale: html2canvas oversampling factor. 1.0 = CSS pixels, 2.0 = retina-grade.
//   - fps: how often html2canvas redraws into the recording canvas.
//   - bitsPerSecond: MediaRecorder bitrate — higher = sharper, larger files.
//
// All three are exposed as parameters so the Razor caller can decide the trade-off.
(function () {
    'use strict';

    const sessions = new Map();
    let sessionCounter = 0;

    const DEFAULTS = Object.freeze({
        screenshotScale: 2,
        recordingScale: 1.5,
        recordingFps: 8,
        recordingBitsPerSecond: 6_000_000
    });

    function resolveTarget(elementId) {
        const target = document.getElementById(elementId);
        if (!target) {
            throw new Error('Capture target not found: ' + elementId);
        }
        return target;
    }

    function clampScale(value, fallback) {
        const numeric = Number(value);
        if (!isFinite(numeric) || numeric <= 0) {
            return fallback;
        }
        // Hard ceiling — html2canvas at scale > 3 starts to lag badly on phones.
        return Math.min(numeric, 3);
    }

    function clampFps(value, fallback) {
        const numeric = Number(value);
        if (!isFinite(numeric) || numeric <= 0) {
            return fallback;
        }
        return Math.min(Math.max(numeric, 1), 30);
    }

    async function captureScreenshot(elementId, scale) {
        const target = resolveTarget(elementId);
        const effectiveScale = clampScale(scale, DEFAULTS.screenshotScale);
        const canvas = await html2canvas(target, {
            useCORS: true,
            allowTaint: false,
            backgroundColor: '#ffffff',
            logging: false,
            scale: effectiveScale
        });
        return canvas.toDataURL('image/png');
    }

    function createOffscreenCanvas(width, height) {
        const offscreen = document.createElement('canvas');
        offscreen.width = width;
        offscreen.height = height;
        return offscreen;
    }

    async function drawTargetOnto(session) {
        try {
            const snapshot = await html2canvas(session.target, {
                useCORS: true,
                allowTaint: false,
                backgroundColor: '#ffffff',
                logging: false,
                scale: session.scale
            });
            session.ctx.drawImage(snapshot, 0, 0, session.width, session.height);
        } catch (err) {
            console.warn('Pollmaster recording frame failed', err);
        }
    }

    function pickMimeType() {
        const candidates = [
            'video/webm;codecs=vp9',
            'video/webm;codecs=vp8',
            'video/webm'
        ];
        return candidates.find(m => MediaRecorder.isTypeSupported(m));
    }

    async function startRecording(elementId, fps, scale, bitsPerSecond) {
        const target = resolveTarget(elementId);
        const rect = target.getBoundingClientRect();
        const effectiveScale = clampScale(scale, DEFAULTS.recordingScale);
        const effectiveFps = clampFps(fps, DEFAULTS.recordingFps);
        const effectiveBitrate = (typeof bitsPerSecond === 'number' && bitsPerSecond > 0)
            ? bitsPerSecond
            : DEFAULTS.recordingBitsPerSecond;

        const width = Math.max(1, Math.floor(rect.width * effectiveScale));
        const height = Math.max(1, Math.floor(rect.height * effectiveScale));

        const offscreen = createOffscreenCanvas(width, height);
        const ctx = offscreen.getContext('2d');
        if (!ctx) {
            throw new Error('Failed to acquire 2D context for offscreen capture canvas.');
        }

        const stream = offscreen.captureStream(effectiveFps);
        const mimeType = pickMimeType();
        if (!mimeType) {
            throw new Error('MediaRecorder does not support WebM on this platform.');
        }

        const recorder = new MediaRecorder(stream, {
            mimeType,
            videoBitsPerSecond: effectiveBitrate
        });
        const chunks = [];
        recorder.ondataavailable = (e) => {
            if (e.data && e.data.size > 0) {
                chunks.push(e.data);
            }
        };

        const sessionId = String(++sessionCounter);
        const session = {
            stream: stream,
            recorder: recorder,
            chunks: chunks,
            offscreen: offscreen,
            ctx: ctx,
            target: target,
            width: width,
            height: height,
            scale: effectiveScale,
            intervalMs: Math.round(1000 / effectiveFps),
            timer: null,
            drawing: false,
            mimeType: mimeType
        };
        sessions.set(sessionId, session);

        // Draw the first frame synchronously so the recording is not blank at the start.
        await drawTargetOnto(session);
        recorder.start();

        session.timer = setInterval(async () => {
            // Coalesce: if the previous html2canvas pass is still running, skip this tick
            // rather than queue up overlapping CPU-heavy redraws.
            if (session.drawing) {
                return;
            }
            session.drawing = true;
            try {
                await drawTargetOnto(session);
            } finally {
                session.drawing = false;
            }
        }, session.intervalMs);

        return sessionId;
    }

    function blobToBase64(blob) {
        return new Promise(function (resolve, reject) {
            const reader = new FileReader();
            reader.onloadend = function () { resolve(reader.result); };
            reader.onerror = function () { reject(reader.error); };
            reader.readAsDataURL(blob);
        });
    }

    async function stopRecording(sessionId) {
        const session = sessions.get(sessionId);
        if (!session) {
            throw new Error('Recording session not found: ' + sessionId);
        }
        clearInterval(session.timer);
        session.timer = null;

        const stopped = new Promise(function (resolve) {
            session.recorder.onstop = function () { resolve(); };
        });
        if (session.recorder.state !== 'inactive') {
            session.recorder.stop();
        }
        await stopped;

        for (const track of session.stream.getTracks()) {
            track.stop();
        }

        const blob = new Blob(session.chunks, { type: session.mimeType });
        sessions.delete(sessionId);
        return await blobToBase64(blob);
    }

    function abortRecording(sessionId) {
        const session = sessions.get(sessionId);
        if (!session) {
            return;
        }
        clearInterval(session.timer);
        if (session.recorder.state !== 'inactive') {
            session.recorder.onstop = null;
            session.recorder.stop();
        }
        for (const track of session.stream.getTracks()) {
            track.stop();
        }
        sessions.delete(sessionId);
    }

    window.pollmasterCapture = {
        captureScreenshot: captureScreenshot,
        startRecording: startRecording,
        stopRecording: stopRecording,
        abortRecording: abortRecording,
        defaults: DEFAULTS
    };
})();
