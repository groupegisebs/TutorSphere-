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

    function attach(videoEl, mediaStream, mirror) {
        if (!videoEl) return;
        videoEl.srcObject = mediaStream;
        videoEl.muted = true;
        videoEl.playsInline = true;
        if (mirror === false)
            videoEl.classList.add("cr-tile-video--no-mirror");
        else
            videoEl.classList.remove("cr-tile-video--no-mirror");
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
        if (stream) attach(videoEl, stream, true);
        else if (videoEl) videoEl.srcObject = null;
    }

    async function getHdCameraStream(wantVideo, wantAudio) {
        var attempts = [
            {
                video: wantVideo ? {
                    facingMode: "user",
                    width: { min: 1280, ideal: 1920, max: 1920 },
                    height: { min: 720, ideal: 1080, max: 1080 },
                    frameRate: { ideal: 30, max: 30 },
                    aspectRatio: { ideal: 16 / 9 }
                } : false,
                audio: wantAudio ? { echoCancellation: true, noiseSuppression: true } : false
            },
            {
                video: wantVideo ? {
                    facingMode: "user",
                    width: { ideal: 1280 },
                    height: { ideal: 720 },
                    frameRate: { ideal: 30 }
                } : false,
                audio: wantAudio ? { echoCancellation: true, noiseSuppression: true } : false
            },
            {
                video: wantVideo,
                audio: wantAudio
            }
        ];

        var lastErr = null;
        for (var i = 0; i < attempts.length; i++) {
            try {
                var s = await navigator.mediaDevices.getUserMedia(attempts[i]);
                var track = s.getVideoTracks()[0];
                if (track && track.getCapabilities) {
                    try {
                        var caps = track.getCapabilities();
                        var advanced = {};
                        if (caps.width && caps.height) {
                            advanced.width = Math.min(1920, caps.width.max || 1920);
                            advanced.height = Math.min(1080, caps.height.max || 1080);
                        }
                        if (caps.frameRate)
                            advanced.frameRate = Math.min(30, caps.frameRate.max || 30);
                        if (Object.keys(advanced).length)
                            await track.applyConstraints(advanced);
                    } catch (_) { /* keep stream as-is */ }
                }
                return s;
            } catch (ex) {
                lastErr = ex;
            }
        }
        throw lastErr || new Error("Caméra indisponible");
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

                stream = await getHdCameraStream(wantVideo, wantAudio);

                if (!screenStream && !canvasStream)
                    attach(videoEl, stream, true);
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
                screenStream = await navigator.mediaDevices.getDisplayMedia({
                    video: {
                        width: { ideal: 1920 },
                        height: { ideal: 1080 },
                        frameRate: { ideal: 30 }
                    },
                    audio: false
                });
                attach(videoEl, screenStream, false);
                videoEl.classList.add("cr-tile-video--contain");
                screenStream.getVideoTracks()[0].addEventListener("ended", function () {
                    screenStream = null;
                    videoEl.classList.remove("cr-tile-video--contain");
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
                canvasStream = canvasEl.captureStream(30);
                attach(videoEl, canvasStream, false);
                videoEl.classList.add("cr-tile-video--contain");
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
            if (videoEl) videoEl.classList.remove("cr-tile-video--contain");
            restoreCamera(videoEl);
            return true;
        },

        isSharing: function () {
            return !!(screenStream || canvasStream);
        }
    };
})();
