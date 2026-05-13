// Pollmaster screenshot + screen-recording interop. Exposed via window.pollmasterCapture
// and called from the Blazor MapView component. Both paths are html2canvas-based because
// MAUI WebView (especially on Android) does not expose getDisplayMedia, and Leaflet tile
// layers render to <img> elements that captureStream cannot pick up directly.
(function () {
    'use strict';

    const sessions = new Map();
    let sessionCounter = 0;

    function resolveTarget(elementId) {
        const target = document.getElementById(elementId);
        if (!target) {
            throw new Error('Capture target not found: ' + elementId);
        }
        return target;
    }

    async function captureScreenshot(elementId) {
        const target = resolveTarget(elementId);
        const canvas = await html2canvas(target, {
            useCORS: true,
            allowTaint: false,
            backgroundColor: '#ffffff',
            logging: false
        });
        return canvas.toDataURL('image/png');
    }

    function createOffscreenCanvas(width, height) {
        const offscreen = document.createElement('canvas');
        offscreen.width = width;
        offscreen.height = height;
        return offscreen;
    }

    async function drawTargetOnto(target, ctx, width, height) {
        try {
            const snapshot = await html2canvas(target, {
                useCORS: true,
                allowTaint: false,
                backgroundColor: '#ffffff',
                logging: false
            });
            ctx.drawImage(snapshot, 0, 0, width, height);
        } catch (err) {
            console.warn('Pollmaster recording frame failed', err);
        }
    }

    async function startRecording(elementId, fps) {
        const target = resolveTarget(elementId);
        const rect = target.getBoundingClientRect();
        const width = Math.max(1, Math.floor(rect.width));
        const height = Math.max(1, Math.floor(rect.height));
        const targetFps = (typeof fps === 'number' && fps > 0) ? fps : 4;

        const offscreen = createOffscreenCanvas(width, height);
        const ctx = offscreen.getContext('2d');
        if (!ctx) {
            throw new Error('Failed to acquire 2D context for offscreen capture canvas.');
        }

        const stream = offscreen.captureStream(targetFps);
        const mimeCandidates = ['video/webm;codecs=vp9', 'video/webm;codecs=vp8', 'video/webm'];
        const mimeType = mimeCandidates.find(m => MediaRecorder.isTypeSupported(m));
        if (!mimeType) {
            throw new Error('MediaRecorder does not support WebM on this platform.');
        }

        const recorder = new MediaRecorder(stream, { mimeType });
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
            intervalMs: Math.round(1000 / targetFps),
            timer: null,
            drawing: false,
            mimeType: mimeType
        };
        sessions.set(sessionId, session);

        // Draw the first frame synchronously so the recording isn't blank at the start.
        await drawTargetOnto(target, ctx, width, height);
        recorder.start();

        session.timer = setInterval(async () => {
            if (session.drawing) {
                return;
            }
            session.drawing = true;
            try {
                await drawTargetOnto(target, ctx, width, height);
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
        abortRecording: abortRecording
    };
})();
