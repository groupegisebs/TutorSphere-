window.classroomMedia = (function () {
    let stream = null;
    let screenStream = null;
    let canvasStream = null;
    let endedCallback = null;

    function stopTracks(mediaStream) {
        if (!mediaStream) return;
        mediaStream.getTracks().forEach(function (t) {
            try { t.stop(); } catch (_) { }
        });
    }

    function attach(videoEl, mediaStream) {
        if (!videoEl) return;
        videoEl.srcObject = mediaStream;
        videoEl.muted = true;
        videoEl.playsInline = true;
        var play = videoEl.play();
        if (play && typeof play.catch === "function")
            play.catch(function () { });
    }

    function trackStates(mediaStream) {
        if (!mediaStream) return { video: false, audio: false };
        var v = mediaStream.getVideoTracks();
        var a = mediaStream.getAudioTracks();
        return {
            video: v.length > 0 && v[0].readyState === "live" && v[0].enabled,
            audio: a.length > 0 && a[0].readyState === "live" && a[0].enabled
        };
    }

    function notifyEnded(kind) {
        if (endedCallback && typeof endedCallback.invokeMethodAsync === "function") {
            endedCallback.invokeMethodAsync("OnScreenShareEnded", kind || "screen").catch(function () { });
        }
    }

    function restoreCamera(videoEl) {
        if (stream) attach(videoEl, stream);
        else if (videoEl) videoEl.srcObject = null;
    }

    return {
        setEndedCallback: function (dotNetRef) {
            endedCallback = dotNetRef;
        },

        start: async function (videoEl, opts) {
            opts = opts || {};
            var wantVideo = opts.video !== false;
            var wantAudio = opts.audio !== false;

            if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
                return { ok: false, error: "Ce navigateur ne prend pas en charge la caméra / le micro." };
            }

            try {
                if (stream) {
                    stopTracks(stream);
                    stream = null;
                }

                stream = await navigator.mediaDevices.getUserMedia({
                    video: wantVideo ? { facingMode: "user", width: { ideal: 1280 }, height: { ideal: 720 } } : false,
                    audio: wantAudio ? { echoCancellation: true, noiseSuppression: true } : false
                });

                if (!screenStream && !canvasStream)
                    attach(videoEl, stream);
                var st = trackStates(stream);
                return { ok: true, video: st.video, audio: st.audio };
            } catch (ex) {
                var msg = "Impossible d'accéder à la caméra ou au micro.";
                if (ex && (ex.name === "NotAllowedError" || ex.name === "PermissionDeniedError"))
                    msg = "Permission refusée. Autorisez la caméra et le micro dans le navigateur.";
                else if (ex && (ex.name === "NotFoundError" || ex.name === "DevicesNotFoundError"))
                    msg = "Aucune caméra ou micro détecté sur cet appareil.";
                else if (ex && ex.name === "NotReadableError")
                    msg = "La caméra est déjà utilisée par une autre application.";
                else if (ex && ex.message)
                    msg = ex.message;
                return { ok: false, error: msg };
            }
        },

        setVideoEnabled: function (enabled) {
            if (!stream) return false;
            stream.getVideoTracks().forEach(function (t) { t.enabled = !!enabled; });
            return true;
        },

        setAudioEnabled: function (enabled) {
            if (!stream) return false;
            stream.getAudioTracks().forEach(function (t) { t.enabled = !!enabled; });
            return true;
        },

        hasStream: function () {
            return !!stream;
        },

        stop: function (videoEl) {
            stopTracks(stream);
            stream = null;
            stopTracks(screenStream);
            screenStream = null;
            stopTracks(canvasStream);
            canvasStream = null;
            if (videoEl) videoEl.srcObject = null;
        },

        startScreenShare: async function (videoEl) {
            if (!navigator.mediaDevices || !navigator.mediaDevices.getDisplayMedia) {
                return { ok: false, error: "Le partage d'écran n'est pas supporté." };
            }
            try {
                stopTracks(canvasStream);
                canvasStream = null;
                stopTracks(screenStream);
                screenStream = await navigator.mediaDevices.getDisplayMedia({ video: true, audio: false });
                attach(videoEl, screenStream);
                screenStream.getVideoTracks()[0].addEventListener("ended", function () {
                    screenStream = null;
                    restoreCamera(videoEl);
                    notifyEnded("screen");
                });
                return { ok: true, kind: "screen" };
            } catch (ex) {
                return { ok: false, error: ex && ex.message ? ex.message : "Partage d'écran annulé." };
            }
        },

        /** Affiche le tableau blanc dans la vignette vidéo (captureStream). */
        startWhiteboardShare: async function (videoEl, canvasEl) {
            if (!canvasEl || typeof canvasEl.captureStream !== "function") {
                return { ok: false, error: "Impossible de partager le tableau blanc sur cet appareil." };
            }
            try {
                stopTracks(screenStream);
                screenStream = null;
                stopTracks(canvasStream);
                canvasStream = canvasEl.captureStream(15);
                attach(videoEl, canvasStream);
                return { ok: true, kind: "whiteboard" };
            } catch (ex) {
                return { ok: false, error: ex && ex.message ? ex.message : "Partage du tableau impossible." };
            }
        },

        stopScreenShare: function (videoEl) {
            stopTracks(screenStream);
            screenStream = null;
            stopTracks(canvasStream);
            canvasStream = null;
            restoreCamera(videoEl);
            return true;
        },

        isSharing: function () {
            return !!(screenStream || canvasStream);
        }
    };
})();
