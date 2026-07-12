window.classroomMedia = (function () {
    let stream = null;
    let screenStream = null;

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

    return {
        /** Request camera + mic and bind to a <video> element. */
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
            if (videoEl) videoEl.srcObject = null;
        },

        startScreenShare: async function (videoEl) {
            if (!navigator.mediaDevices || !navigator.mediaDevices.getDisplayMedia) {
                return { ok: false, error: "Le partage d'écran n'est pas supporté." };
            }
            try {
                screenStream = await navigator.mediaDevices.getDisplayMedia({ video: true, audio: false });
                attach(videoEl, screenStream);
                screenStream.getVideoTracks()[0].addEventListener("ended", function () {
                    if (stream) attach(videoEl, stream);
                    else if (videoEl) videoEl.srcObject = null;
                    screenStream = null;
                });
                return { ok: true };
            } catch (ex) {
                return { ok: false, error: ex && ex.message ? ex.message : "Partage d'écran annulé." };
            }
        },

        stopScreenShare: function (videoEl) {
            stopTracks(screenStream);
            screenStream = null;
            if (stream) attach(videoEl, stream);
            else if (videoEl) videoEl.srcObject = null;
            return true;
        }
    };
})();
