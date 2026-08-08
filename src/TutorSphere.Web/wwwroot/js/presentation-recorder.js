/**
 * Enregistrement courte vidéo de présentation (caméra + micro) pour les offres.
 * MaxDurationSec par défaut : 90 s.
 */
window.tsPresentationRecorder = (function () {
    var stream = null;
    var recorder = null;
    var chunks = [];
    var previewEl = null;
    var recordedBlob = null;
    var recordedUrl = null;
    var maxMs = 90 * 1000;
    var startedAt = 0;
    var tickTimer = null;
    var dotnetRef = null;

    function pickMimeType() {
        var candidates = [
            "video/webm;codecs=vp9,opus",
            "video/webm;codecs=vp8,opus",
            "video/webm",
            "video/mp4"
        ];
        for (var i = 0; i < candidates.length; i++) {
            if (window.MediaRecorder && MediaRecorder.isTypeSupported(candidates[i]))
                return candidates[i];
        }
        return "";
    }

    function clearTick() {
        if (tickTimer) {
            clearInterval(tickTimer);
            tickTimer = null;
        }
    }

    function notifyState(state, elapsedSec) {
        if (!dotnetRef) return;
        try {
            dotnetRef.invokeMethodAsync("OnRecorderState", state, elapsedSec || 0);
        } catch (_) { /* ignore */ }
    }

    async function init(videoElement, maxDurationSec, dotNetObjectReference) {
        dispose();
        previewEl = videoElement;
        maxMs = Math.max(15, (maxDurationSec || 90)) * 1000;
        dotnetRef = dotNetObjectReference;

        if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia)
            throw new Error("CameraNotSupported");
        if (!window.MediaRecorder)
            throw new Error("RecorderNotSupported");

        stream = await navigator.mediaDevices.getUserMedia({
            audio: true,
            video: {
                facingMode: "user",
                width: { ideal: 1280 },
                height: { ideal: 720 }
            }
        });

        if (previewEl) {
            previewEl.srcObject = stream;
            previewEl.muted = true;
            previewEl.playsInline = true;
            try { await previewEl.play(); } catch (_) { /* autoplay */ }
        }

        notifyState("ready", 0);
        return true;
    }

    function start() {
        if (!stream) throw new Error("NotInitialized");
        if (recorder && recorder.state === "recording") return false;

        chunks = [];
        recordedBlob = null;
        if (recordedUrl) {
            URL.revokeObjectURL(recordedUrl);
            recordedUrl = null;
        }

        var mime = pickMimeType();
        recorder = mime
            ? new MediaRecorder(stream, { mimeType: mime, videoBitsPerSecond: 1_500_000 })
            : new MediaRecorder(stream);

        recorder.ondataavailable = function (e) {
            if (e.data && e.data.size > 0) chunks.push(e.data);
        };

        recorder.onstop = function () {
            clearTick();
            var type = (recorder && recorder.mimeType) || "video/webm";
            recordedBlob = new Blob(chunks, { type: type });
            recordedUrl = URL.createObjectURL(recordedBlob);
            if (previewEl) {
                previewEl.srcObject = null;
                previewEl.src = recordedUrl;
                previewEl.muted = false;
                previewEl.controls = true;
                previewEl.play().catch(function () { });
            }
            notifyState("stopped", Math.round((Date.now() - startedAt) / 1000));
        };

        startedAt = Date.now();
        recorder.start(250);
        clearTick();
        tickTimer = setInterval(function () {
            var elapsed = Date.now() - startedAt;
            notifyState("recording", Math.round(elapsed / 1000));
            if (elapsed >= maxMs) stop();
        }, 250);

        notifyState("recording", 0);
        return true;
    }

    function stop() {
        clearTick();
        if (recorder && recorder.state === "recording") {
            recorder.stop();
            return true;
        }
        return false;
    }

    function retake() {
        recordedBlob = null;
        if (recordedUrl) {
            URL.revokeObjectURL(recordedUrl);
            recordedUrl = null;
        }
        if (previewEl && stream) {
            previewEl.controls = false;
            previewEl.muted = true;
            previewEl.src = "";
            previewEl.srcObject = stream;
            previewEl.play().catch(function () { });
        }
        notifyState("ready", 0);
    }

    async function getBytes() {
        if (!recordedBlob) return null;
        var buf = await recordedBlob.arrayBuffer();
        return new Uint8Array(buf);
    }

    function getMimeType() {
        if (!recordedBlob) return "";
        return recordedBlob.type || "video/webm";
    }

    function getExtension() {
        var t = getMimeType();
        if (t.indexOf("mp4") >= 0) return "mp4";
        return "webm";
    }

    function dispose() {
        clearTick();
        try {
            if (recorder && recorder.state === "recording") recorder.stop();
        } catch (_) { /* ignore */ }
        recorder = null;
        chunks = [];
        if (stream) {
            stream.getTracks().forEach(function (t) { t.stop(); });
            stream = null;
        }
        if (previewEl) {
            previewEl.srcObject = null;
            previewEl.removeAttribute("src");
            previewEl.load();
        }
        if (recordedUrl) {
            URL.revokeObjectURL(recordedUrl);
            recordedUrl = null;
        }
        recordedBlob = null;
        previewEl = null;
        dotnetRef = null;
    }

    return {
        init: init,
        start: start,
        stop: stop,
        retake: retake,
        getBytes: getBytes,
        getMimeType: getMimeType,
        getExtension: getExtension,
        dispose: dispose
    };
})();
