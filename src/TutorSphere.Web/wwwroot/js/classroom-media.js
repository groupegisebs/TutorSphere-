window.classroomMedia = (function () {
    let stream = null;
    let screenStream = null;
    let canvasStream = null;
    let endedCallback = null;
    let deviceChangeBound = false;
    let privacyBound = false;
    let privacyPaused = false;
    let privacyResumeInFlight = false;
    let lastVideoEl = null;
    /** Intent avant verrouillage / onglet masqué — pour soft-resume. */
    let privacySaved = { audio: false, video: false };

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

        // Miroirs galerie / scène (même flux partout).
        document.querySelectorAll("video[data-rtc-local], video[data-rtc-local-stage]").forEach(function (el) {
            if (el === videoEl) return;
            el.srcObject = mediaStream;
            el.muted = true;
            el.playsInline = true;
            var p = el.play();
            if (p && typeof p.catch === "function") p.catch(function () { });
        });
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

    function notifyDevicesChanged(info) {
        if (endedCallback && typeof endedCallback.invokeMethodAsync === "function") {
            endedCallback.invokeMethodAsync("OnMediaDevicesChanged", info).catch(function () { });
        }
    }

    function notifyPrivacyPause(paused) {
        if (endedCallback && typeof endedCallback.invokeMethodAsync === "function") {
            endedCallback.invokeMethodAsync("OnMediaPrivacyPause", !!paused).catch(function () { });
        }
    }

    function setAllLocalMediaEnabled(audioOn, videoOn) {
        if (stream) {
            stream.getAudioTracks().forEach(function (t) { t.enabled = !!audioOn; });
            stream.getVideoTracks().forEach(function (t) { t.enabled = !!videoOn; });
        }
        // Partage écran / tableau : couper aussi pour ne pas diffuser l'écran de verrouillage.
        [screenStream, canvasStream].forEach(function (s) {
            if (!s) return;
            s.getVideoTracks().forEach(function (t) { t.enabled = !!videoOn; });
            s.getAudioTracks().forEach(function (t) { t.enabled = !!audioOn; });
        });
        document.querySelectorAll("video[data-rtc-local], video[data-rtc-local-stage]").forEach(function (el) {
            var thumb = el.closest(".cr-pro-thumb");
            if (videoOn && ((stream && stream.getVideoTracks().length) || screenStream || canvasStream)) {
                el.classList.remove("is-hidden");
                if (thumb) thumb.classList.add("has-video");
            } else if (!videoOn) {
                el.classList.add("is-hidden");
                if (thumb) thumb.classList.remove("has-video");
            }
        });
    }

    /**
     * Verrouillage PC / onglet en arrière-plan :
     * coupe micro + caméra (et pause le flux partagé), libère le matériel.
     * Soft-resume au retour (déverrouillage / onglet visible).
     */
    async function applyPrivacyPause(pause) {
        if (pause) {
            if (privacyPaused) return;
            privacyPaused = true;
            privacySaved = {
                audio: !!(stream && stream.getAudioTracks().some(function (t) {
                    return t.readyState === "live" && t.enabled;
                })),
                video: !!(stream && stream.getVideoTracks().some(function (t) {
                    return t.readyState === "live" && t.enabled;
                }))
            };
            // Couper immédiatement la diffusion WebRTC.
            setAllLocalMediaEnabled(false, false);
            // Libérer la caméra / le micro (voyant matériel).
            if (stream) {
                stream.getTracks().forEach(function (t) {
                    try { t.stop(); } catch (_) { }
                });
                stream = null;
            }
            notifyPrivacyPause(true);
            return;
        }

        if (!privacyPaused || privacyResumeInFlight) return;
        privacyPaused = false;
        privacyResumeInFlight = true;
        try {
            var wantAudio = privacySaved.audio;
            var wantVideo = privacySaved.video;
            if ((wantAudio || wantVideo) && navigator.mediaDevices && navigator.mediaDevices.getUserMedia) {
                try {
                    var next = await getUserMediaWithFallback(wantVideo, wantAudio);
                    stream = next;
                    if (!screenStream && !canvasStream) {
                        if (bgMode !== "none" && stream.getVideoTracks().length > 0)
                            await applyBackground(lastVideoEl);
                        else
                            attach(lastVideoEl, stream, true);
                    }
                    setAllLocalMediaEnabled(wantAudio, wantVideo);
                    // Réactiver les pistes de partage si elles existent encore.
                    if (screenStream) {
                        screenStream.getVideoTracks().forEach(function (t) {
                            if (t.readyState === "live") t.enabled = true;
                        });
                    }
                    if (canvasStream) {
                        canvasStream.getVideoTracks().forEach(function (t) {
                            if (t.readyState === "live") t.enabled = true;
                        });
                    }
                } catch (_) {
                    // Permissions / appareil — rester muet ; l'utilisateur pourra rallumer.
                    privacySaved = { audio: false, video: false };
                }
            }
            notifyPrivacyPause(false);
        } finally {
            privacyResumeInFlight = false;
        }
    }

    function onVisibilityChange() {
        if (document.hidden || document.visibilityState === "hidden")
            applyPrivacyPause(true);
        else
            applyPrivacyPause(false);
    }

    function bindPrivacyPause() {
        if (privacyBound || typeof document === "undefined") return;
        privacyBound = true;
        document.addEventListener("visibilitychange", onVisibilityChange);
        // pagehide : certains navigateurs au verrouillage / mise en veille.
        window.addEventListener("pagehide", function () { applyPrivacyPause(true); });
        window.addEventListener("pageshow", function () {
            if (!document.hidden) applyPrivacyPause(false);
        });
        window.addEventListener("freeze", function () { applyPrivacyPause(true); });
        // Retour focus après verrouillage Windows (complément à visibilitychange).
        window.addEventListener("focus", function () {
            if (!document.hidden && privacyPaused) applyPrivacyPause(false);
        });
    }

    function restoreCamera(videoEl) {
        if (!videoEl) return;
        if (stream && window.classroomVirtualBg && classroomVirtualBg.isRunning()) {
            classroomVirtualBg.reattach(videoEl);
            return;
        }
        if (stream) attach(videoEl, stream, true);
        else videoEl.srcObject = null;
    }

    async function applyBackground(videoEl) {
        if (!videoEl || !stream || stream.getVideoTracks().length === 0) return { ok: true };
        if (!window.classroomVirtualBg) return { ok: false, error: "Module arrière-plan indisponible." };
        if (bgMode === "none") {
            classroomVirtualBg.stop();
            if (!screenStream && !canvasStream)
                attach(videoEl, stream, true);
            return { ok: true, mode: "none" };
        }
        if (screenStream || canvasStream)
            return { ok: true, mode: bgMode, deferred: true };
        return await classroomVirtualBg.start(videoEl, stream, {
            mode: bgMode,
            imageUrl: bgImageUrl
        });
    }

    let bgMode = "none";
    let bgImageUrl = null;

    function mapMediaError(ex) {
        var msg = "Impossible d'accéder à la caméra ou au micro.";
        if (!ex) return msg;
        if (ex.name === "NotAllowedError" || ex.name === "PermissionDeniedError")
            return "Permission refusée. Autorisez la caméra et le micro dans le navigateur.";
        if (ex.name === "NotFoundError" || ex.name === "DevicesNotFoundError")
            return "Aucune caméra ou micro détecté sur cet appareil.";
        if (ex.name === "NotReadableError")
            return "La caméra est déjà utilisée par une autre application.";
        if (ex.message) return ex.message;
        return msg;
    }

    async function listDevices() {
        if (!navigator.mediaDevices || !navigator.mediaDevices.enumerateDevices) {
            return { cameras: 0, mics: 0, cameraLabels: [], micLabels: [] };
        }
        try {
            var devices = await navigator.mediaDevices.enumerateDevices();
            var cams = devices.filter(function (d) { return d.kind === "videoinput"; });
            var mics = devices.filter(function (d) { return d.kind === "audioinput"; });
            return {
                cameras: cams.length,
                mics: mics.length,
                cameraLabels: cams.map(function (c) { return c.label || "Caméra"; }),
                micLabels: mics.map(function (m) { return m.label || "Micro"; })
            };
        } catch (_) {
            return { cameras: 0, mics: 0, cameraLabels: [], micLabels: [] };
        }
    }

    function bindDeviceChange() {
        if (deviceChangeBound || !navigator.mediaDevices || !navigator.mediaDevices.addEventListener)
            return;
        deviceChangeBound = true;
        navigator.mediaDevices.addEventListener("devicechange", function () {
            listDevices().then(function (info) {
                notifyDevicesChanged(info);
            });
        });
    }

    async function getUserMediaWithFallback(wantVideo, wantAudio) {
        // Essais rapides d'abord (évite 2–5s de timeouts sur contraintes 1080p).
        var attempts = [];

        if (wantVideo && wantAudio) {
            attempts.push({ video: true, audio: true });
            attempts.push({
                video: { facingMode: "user", width: { ideal: 1280 }, height: { ideal: 720 } },
                audio: { echoCancellation: true, noiseSuppression: true }
            });
            attempts.push({
                video: {
                    facingMode: "user",
                    width: { ideal: 1920 },
                    height: { ideal: 1080 },
                    frameRate: { ideal: 30 }
                },
                audio: { echoCancellation: true, noiseSuppression: true }
            });
        }

        if (wantVideo && !wantAudio) {
            attempts.push({ video: true, audio: false });
        }

        if (wantAudio) {
            attempts.push({
                video: false,
                audio: { echoCancellation: true, noiseSuppression: true }
            });
            attempts.push({ video: false, audio: true });
        }

        if (wantVideo) {
            attempts.push({ video: true, audio: false });
        }

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
                            advanced.width = Math.min(1280, caps.width.max || 1280);
                            advanced.height = Math.min(720, caps.height.max || 720);
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
        throw lastErr || new Error("Périphériques média indisponibles");
    }

    return {
        listDevices: listDevices,

        setEndedCallback: function (dotNetRef) {
            endedCallback = dotNetRef;
            bindDeviceChange();
            bindPrivacyPause();
        },

        start: async function (videoEl, opts) {
            opts = opts || {};
            var wantVideo = opts.video !== false;
            var wantAudio = opts.audio !== false;
            lastVideoEl = videoEl || lastVideoEl;

            if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
                return {
                    ok: false,
                    video: false,
                    audio: false,
                    cameras: 0,
                    mics: 0,
                    error: "Ce navigateur ne prend pas en charge la caméra / le micro."
                };
            }

            bindDeviceChange();
            bindPrivacyPause();

            // Ne pas relancer le média pendant un verrouillage.
            if (document.hidden || privacyPaused) {
                privacySaved = { audio: wantAudio, video: wantVideo };
                if (!privacyPaused) {
                    privacyPaused = true;
                    notifyPrivacyPause(true);
                }
                return {
                    ok: true,
                    video: false,
                    audio: false,
                    cameras: 0,
                    mics: 0,
                    warning: "Média en pause (écran verrouillé ou onglet masqué).",
                    error: null,
                    privacyPaused: true
                };
            }

            try {
                var before = await listDevices();
                // Si on sait déjà qu'il n'y a aucune caméra, démarrer en audio uniquement.
                if (wantVideo && before.cameras === 0)
                    wantVideo = false;

                if (stream) {
                    stopTracks(stream);
                    stream = null;
                }

                stream = await getUserMediaWithFallback(wantVideo, wantAudio);

                if (!screenStream && !canvasStream) {
                    if (bgMode !== "none" && stream.getVideoTracks().length > 0)
                        await applyBackground(videoEl);
                    else
                        attach(videoEl, stream, true);
                }

                var after = await listDevices();
                var st = trackStates(stream);
                var warning = null;
                if (!st.video && opts.video !== false) {
                    warning = after.cameras === 0
                        ? "Aucune caméra détectée. Session en audio uniquement."
                        : "Caméra indisponible. Session en audio uniquement.";
                }

                return {
                    ok: true,
                    video: st.video,
                    audio: st.audio,
                    cameras: after.cameras,
                    mics: after.mics,
                    warning: warning,
                    error: null
                };
            } catch (ex) {
                var devices = await listDevices();
                return {
                    ok: false,
                    video: false,
                    audio: false,
                    cameras: devices.cameras,
                    mics: devices.mics,
                    error: mapMediaError(ex)
                };
            }
        },

        /** Active la caméra si elle vient d'être branchée (ajoute une piste vidéo au flux). */
        enableCamera: async function (videoEl) {
            lastVideoEl = videoEl || lastVideoEl;
            if (privacyPaused) {
                privacySaved.video = true;
                return { ok: false, error: "Caméra en pause (écran verrouillé). Elle se réactivera au déverrouillage." };
            }
            if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
                return { ok: false, error: "Caméra non supportée." };
            }

            var info = await listDevices();
            if (info.cameras === 0) {
                return {
                    ok: false,
                    cameras: 0,
                    error: "Aucune caméra détectée. Branchez une webcam puis réessayez."
                };
            }

            try {
                var camStream = await getUserMediaWithFallback(true, false);
                var videoTrack = camStream.getVideoTracks()[0];
                if (!videoTrack)
                    return { ok: false, cameras: info.cameras, error: "Aucune piste vidéo disponible." };

                if (stream) {
                    stream.getVideoTracks().forEach(function (t) {
                        stream.removeTrack(t);
                        try { t.stop(); } catch (_) { }
                    });
                    stream.addTrack(videoTrack);
                    // Stop leftover audio from camStream if any
                    camStream.getAudioTracks().forEach(function (t) { try { t.stop(); } catch (_) { } });
                } else {
                    stream = camStream;
                }

                if (!screenStream && !canvasStream)
                    attach(videoEl, stream, true);

                var st = trackStates(stream);
                var after = await listDevices();
                return {
                    ok: true,
                    video: st.video,
                    audio: st.audio,
                    cameras: after.cameras,
                    mics: after.mics,
                    error: null
                };
            } catch (ex) {
                return { ok: false, cameras: info.cameras, error: mapMediaError(ex) };
            }
        },

        setVideoEnabled: function (enabled) {
            if (privacyPaused) {
                privacySaved.video = !!enabled;
                return false;
            }
            if (!stream) return false;
            var tracks = stream.getVideoTracks();
            if (tracks.length === 0) return false;
            tracks.forEach(function (t) { t.enabled = !!enabled; });
            document.querySelectorAll("video[data-rtc-local], video[data-rtc-local-stage]").forEach(function (el) {
                var thumb = el.closest(".cr-pro-thumb");
                if (enabled) {
                    el.classList.remove("is-hidden");
                    if (thumb) thumb.classList.add("has-video");
                } else {
                    el.classList.add("is-hidden");
                    if (thumb) thumb.classList.remove("has-video");
                }
            });
            return true;
        },

        setAudioEnabled: function (enabled) {
            if (privacyPaused) {
                privacySaved.audio = !!enabled;
                return false;
            }
            if (!stream) return false;
            stream.getAudioTracks().forEach(function (t) { t.enabled = !!enabled; });
            return true;
        },

        isPrivacyPaused: function () {
            return !!privacyPaused;
        },

        hasStream: function () {
            return !!stream;
        },

        getLocalStream: function () {
            return stream;
        },

        /** Fusionne les pistes audio du micro local dans un flux vidéo (écran / tableau). */
        mixWithLocalAudio: function (videoStream) {
            if (!videoStream) return null;
            var mixed = new MediaStream();
            videoStream.getVideoTracks().forEach(function (t) { mixed.addTrack(t); });
            videoStream.getAudioTracks().forEach(function (t) { mixed.addTrack(t); });
            if (stream) {
                stream.getAudioTracks().forEach(function (t) {
                    if (!mixed.getAudioTracks().some(function (a) { return a.id === t.id; }))
                        mixed.addTrack(t);
                });
            }
            return mixed;
        },

        /** Flux à publier en WebRTC : partage d'écran/tableau + micro, sinon caméra/micro. */
        getPublishStream: function () {
            if (screenStream && screenStream.getVideoTracks().length > 0)
                return this.mixWithLocalAudio(screenStream);
            if (canvasStream && canvasStream.getVideoTracks().length > 0)
                return this.mixWithLocalAudio(canvasStream);
            return stream;
        },

        hasVideoTrack: function () {
            return !!(stream && stream.getVideoTracks().length > 0);
        },

        hasAudioTrack: function () {
            return !!(stream && stream.getAudioTracks().some(function (t) {
                return t.readyState === "live";
            }));
        },

        /** Active le micro s'il n'y a pas encore de piste audio (comme enableCamera). */
        enableMicrophone: async function () {
            if (privacyPaused) {
                privacySaved.audio = true;
                return { ok: false, error: "Micro en pause (écran verrouillé). Il se réactivera au déverrouillage." };
            }
            if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
                return { ok: false, error: "Micro non supporté." };
            }

            var info = await listDevices();
            if (info.mics === 0) {
                return {
                    ok: false,
                    mics: 0,
                    error: "Aucun microphone détecté. Branchez un micro puis réessayez."
                };
            }

            try {
                var micStream = await getUserMediaWithFallback(false, true);
                var audioTrack = micStream.getAudioTracks()[0];
                if (!audioTrack)
                    return { ok: false, mics: info.mics, error: "Aucune piste audio disponible." };

                if (stream) {
                    stream.getAudioTracks().forEach(function (t) {
                        stream.removeTrack(t);
                        try { t.stop(); } catch (_) { }
                    });
                    stream.addTrack(audioTrack);
                    micStream.getVideoTracks().forEach(function (t) { try { t.stop(); } catch (_) { } });
                } else {
                    stream = micStream;
                }

                audioTrack.enabled = true;
                var st = trackStates(stream);
                var after = await listDevices();
                return {
                    ok: true,
                    video: st.video,
                    audio: st.audio,
                    cameras: after.cameras,
                    mics: after.mics,
                    error: null
                };
            } catch (ex) {
                return { ok: false, mics: info.mics, error: mapMediaError(ex) };
            }
        },

        stop: function (videoEl) {
            if (window.classroomVirtualBg) classroomVirtualBg.stop();
            bgMode = "none";
            bgImageUrl = null;
            stopTracks(stream);
            stream = null;
            stopTracks(screenStream);
            screenStream = null;
            stopTracks(canvasStream);
            canvasStream = null;
            if (videoEl) videoEl.srcObject = null;
        },

        setBackgroundEffect: async function (videoEl, opts) {
            opts = opts || {};
            bgMode = opts.mode || "none";
            if (opts.imageUrl !== undefined)
                bgImageUrl = opts.imageUrl;
            if (bgMode === "image" && !bgImageUrl && opts.preset && window.classroomVirtualBg) {
                bgImageUrl = classroomVirtualBg.presetDataUrl(opts.preset);
            }
            if (!stream || stream.getVideoTracks().length === 0) {
                return { ok: false, error: "Activez d'abord la caméra." };
            }
            return await applyBackground(videoEl);
        },

        getBackgroundEffect: function () {
            return { mode: bgMode, imageUrl: bgImageUrl };
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
                        frameRate: { ideal: 15, max: 30 },
                        displaySurface: "monitor"
                    },
                    audio: false,
                    preferCurrentTab: false
                });
                var screenTrack = screenStream.getVideoTracks()[0];
                if (screenTrack) {
                    try { screenTrack.contentHint = "detail"; } catch (_) { }
                    try { screenTrack.applyConstraints({ frameRate: { ideal: 15, max: 30 } }); } catch (_) { }
                }
                attach(videoEl, screenStream, false);
                videoEl.classList.add("cr-tile-video--contain");
                if (window.classroomVirtualBg) classroomVirtualBg.stop();
                if (screenTrack) {
                    screenTrack.addEventListener("ended", function () {
                        screenStream = null;
                        videoEl.classList.remove("cr-tile-video--contain");
                        if (bgMode !== "none")
                            applyBackground(videoEl);
                        else
                            restoreCamera(videoEl);
                        notifyEnded("screen");
                    });
                }
                return { ok: true, kind: "screen" };
            } catch (ex) {
                return { ok: false, error: ex && ex.message ? ex.message : "Partage d'écran annulé." };
            }
        },

        startWhiteboardShare: async function (videoEl, canvasEl) {
            if (!canvasEl || typeof canvasEl.captureStream !== "function") {
                return { ok: false, error: "Impossible de partager le tableau blanc sur cet appareil." };
            }
            try {
                stopTracks(screenStream);
                screenStream = null;
                stopTracks(canvasStream);
                canvasStream = canvasEl.captureStream(15);
                var boardTrack = canvasStream.getVideoTracks()[0];
                if (boardTrack) {
                    try { boardTrack.contentHint = "detail"; } catch (_) { }
                }
                attach(videoEl, canvasStream, false);
                videoEl.classList.add("cr-tile-video--contain");
                if (window.classroomVirtualBg) classroomVirtualBg.stop();
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
            if (bgMode !== "none")
                applyBackground(videoEl);
            else
                restoreCamera(videoEl);
            return true;
        },

        isSharing: function () {
            return !!(screenStream || canvasStream);
        }
    };
})();
