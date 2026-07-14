window.classroomRtc = (function () {
    var peers = {}; // connectionId -> { pc, makingOffer, ignoreOffer, polite }
    var remoteStreams = {}; // connectionId -> MediaStream
    var pendingConnect = {}; // connectionId -> createOffer bool
    var dotNetRef = null;
    var iceServers = [
        { urls: "stun:stun.l.google.com:19302" },
        { urls: "stun:stun1.l.google.com:19302" }
    ];
    var selfPolite = true;
    var remountTimer = null;

    function getPublishStream() {
        if (window.classroomMedia && typeof classroomMedia.getPublishStream === "function")
            return classroomMedia.getPublishStream();
        return null;
    }

    function scheduleRemount(remoteId) {
        if (remountTimer) clearTimeout(remountTimer);
        remountTimer = setTimeout(function () {
            if (remoteId)
                attachToDom(remoteId, remoteStreams[remoteId]);
            else
                Object.keys(remoteStreams).forEach(function (id) {
                    attachToDom(id, remoteStreams[id]);
                });
            remountMainIfFocused();
        }, 60);
    }

    function remountMainIfFocused() {
        var main = document.querySelector("video[data-rtc-main]");
        if (!main || !main.dataset.focusedPeer) return;
        var stream = remoteStreams[main.dataset.focusedPeer];
        if (stream) attachMain(stream, main.dataset.focusedPeer);
    }

    function ensurePc(remoteId) {
        if (peers[remoteId])
            return peers[remoteId];

        var pc = new RTCPeerConnection({ iceServers: iceServers });
        var state = {
            pc: pc,
            makingOffer: false,
            ignoreOffer: false,
            polite: selfPolite
        };
        peers[remoteId] = state;

        // Toujours préparer audio+vidéo pour que chaque participant puisse envoyer/recevoir.
        var local = getPublishStream();
        try {
            if (local && local.getAudioTracks().length)
                pc.addTrack(local.getAudioTracks()[0], local);
            else
                pc.addTransceiver("audio", { direction: local ? "sendrecv" : "recvonly" });
        } catch (_) { }

        try {
            if (local && local.getVideoTracks().length)
                pc.addTrack(local.getVideoTracks()[0], local);
            else
                pc.addTransceiver("video", { direction: local ? "sendrecv" : "recvonly" });
        } catch (_) { }

        pc.onicecandidate = function (ev) {
            if (!ev.candidate) return;
            sendSignal(remoteId, "ice", JSON.stringify(ev.candidate));
        };

        pc.ontrack = function (ev) {
            var stream = remoteStreams[remoteId] || new MediaStream();
            if (ev.streams && ev.streams[0]) {
                stream = ev.streams[0];
            } else if (ev.track && !stream.getTracks().some(function (t) { return t.id === ev.track.id; })) {
                stream.addTrack(ev.track);
            }
            remoteStreams[remoteId] = stream;
            attachToDom(remoteId, stream);
            scheduleRemount(remoteId);
            notifyRemoteMedia(remoteId, stream);

            ev.track.onunmute = function () {
                notifyRemoteMedia(remoteId, stream);
                scheduleRemount(remoteId);
            };
            ev.track.onended = function () {
                notifyRemoteMedia(remoteId, stream);
            };
        };

        pc.onnegotiationneeded = async function () {
            try {
                if (state.makingOffer) return;
                state.makingOffer = true;
                await pc.setLocalDescription();
                if (pc.localDescription)
                    sendSignal(remoteId, "sdp", JSON.stringify(pc.localDescription));
            } catch (ex) {
                console.warn("classroomRtc negotiationneeded", ex);
            } finally {
                state.makingOffer = false;
            }
        };

        pc.onconnectionstatechange = function () {
            if (pc.connectionState === "failed") {
                try { pc.restartIce(); } catch (_) { }
            }
            if (dotNetRef && typeof dotNetRef.invokeMethodAsync === "function") {
                dotNetRef.invokeMethodAsync("OnRtcConnectionState", remoteId, pc.connectionState).catch(function () { });
            }
        };

        return state;
    }

    function sendSignal(targetId, type, payload) {
        if (!dotNetRef || typeof dotNetRef.invokeMethodAsync !== "function") return;
        dotNetRef.invokeMethodAsync("OnRtcOutgoingSignal", targetId, type, payload).catch(function () { });
    }

    function notifyRemoteMedia(remoteId, stream) {
        if (!dotNetRef || typeof dotNetRef.invokeMethodAsync !== "function") return;
        if (!stream) {
            dotNetRef.invokeMethodAsync("OnRtcRemoteMedia", remoteId, false, false).catch(function () { });
            return;
        }
        var hasVideo = stream.getVideoTracks().some(function (t) {
            return t.readyState !== "ended" && t.enabled;
        });
        var hasAudio = stream.getAudioTracks().some(function (t) {
            return t.readyState !== "ended" && t.enabled;
        });
        dotNetRef.invokeMethodAsync("OnRtcRemoteMedia", remoteId, hasVideo, hasAudio).catch(function () { });
    }

    function isGalleryLayout() {
        var root = document.querySelector(".cr-pro-video");
        return !!(root && (root.classList.contains("is-gallery") || root.classList.contains("is-mosaic")));
    }

    function tryPlay(el) {
        if (!el) return Promise.resolve();
        var play = el.play();
        if (play && typeof play.catch === "function")
            return play.catch(function () { return false; });
        return Promise.resolve();
    }

    /** Évite le double son : en galerie le son vient des vignettes ; en grand écran, de la scène. */
    function syncPlaybackRouting() {
        var gallery = isGalleryLayout();
        var main = document.querySelector("video[data-rtc-main]");
        var focused = main && main.dataset.focusedPeer ? main.dataset.focusedPeer : null;
        var mainActive = !!(main && !gallery && focused && main.srcObject
            && !main.classList.contains("is-hidden"));

        document.querySelectorAll("video[data-rtc-peer]").forEach(function (el) {
            var id = el.getAttribute("data-rtc-peer");
            if (el.srcObject) {
                // Si la scène diffuse déjà ce pair, mute la vignette pour éviter l'écho local.
                el.muted = !!(mainActive && id === focused);
                el.volume = 1;
                el.playsInline = true;
                el.autoplay = true;
                tryPlay(el);
            }
        });

        if (main) {
            if (gallery || !main.srcObject || main.classList.contains("is-hidden"))
                main.muted = true;
            else
                main.muted = false;
            main.volume = 1;
            tryPlay(main);
        }
    }

    function attachToDom(remoteId, stream) {
        if (!remoteId || !stream) return;
        var nodes = document.querySelectorAll('video[data-rtc-peer="' + remoteId + '"]');
        if (!nodes.length) {
            // DOM pas encore rendu (Blazor) — réessayer.
            scheduleRemount(remoteId);
            return;
        }
        var liveVideo = stream.getVideoTracks().some(function (t) {
            return t.readyState === "live" && t.enabled;
        });
        nodes.forEach(function (el) {
            if (el.srcObject !== stream)
                el.srcObject = stream;
            el.playsInline = true;
            el.autoplay = true;
            var thumb = el.closest(".cr-pro-thumb");
            if (thumb) {
                if (liveVideo) thumb.classList.add("has-video");
                else thumb.classList.remove("has-video");
            }
            if (liveVideo)
                el.classList.remove("is-hidden");
            else
                el.classList.add("is-hidden");
        });
        syncPlaybackRouting();
    }

    function attachMain(stream, peerId) {
        var el = document.querySelector("video[data-rtc-main]");
        if (!el || !stream) return;
        if (peerId) el.dataset.focusedPeer = peerId;
        if (el.srcObject !== stream)
            el.srcObject = stream;
        el.playsInline = true;
        el.autoplay = true;
        el.classList.remove("is-hidden");
        syncPlaybackRouting();
    }

    async function handleSdp(remoteId, description) {
        var state = ensurePc(remoteId);
        var pc = state.pc;
        var offerCollision = description.type === "offer"
            && (state.makingOffer || pc.signalingState !== "stable");
        state.ignoreOffer = !state.polite && offerCollision;
        if (state.ignoreOffer)
            return;

        await pc.setRemoteDescription(description);
        if (description.type === "offer") {
            await pc.setLocalDescription();
            if (pc.localDescription)
                sendSignal(remoteId, "sdp", JSON.stringify(pc.localDescription));
        }
        if (remoteStreams[remoteId])
            scheduleRemount(remoteId);
    }

    async function handleIce(remoteId, candidate) {
        var state = peers[remoteId] || ensurePc(remoteId);
        try {
            await state.pc.addIceCandidate(candidate);
        } catch (ex) {
            if (!state.ignoreOffer)
                console.warn("classroomRtc addIceCandidate", ex);
        }
    }

    async function makeOffer(remoteId) {
        var state = ensurePc(remoteId);
        try {
            state.makingOffer = true;
            await state.pc.setLocalDescription();
            if (state.pc.localDescription)
                sendSignal(remoteId, "sdp", JSON.stringify(state.pc.localDescription));
            return { ok: true };
        } catch (ex) {
            console.warn("classroomRtc makeOffer", ex);
            return { ok: false, error: ex && ex.message ? ex.message : "offer failed" };
        } finally {
            state.makingOffer = false;
        }
    }

    return {
        init: function (ref, opts) {
            dotNetRef = ref;
            opts = opts || {};
            if (opts.polite === false)
                selfPolite = false;
            if (opts.iceServers && opts.iceServers.length)
                iceServers = opts.iceServers;
        },

        connect: async function (remoteId, createOffer) {
            if (!remoteId) return { ok: false };
            pendingConnect[remoteId] = !!createOffer;
            var state = ensurePc(remoteId);
            if (createOffer)
                await makeOffer(remoteId);
            if (remoteStreams[remoteId])
                scheduleRemount(remoteId);
            return { ok: true };
        },

        handleSignal: async function (fromId, type, payload) {
            if (!fromId || !type || !payload) return;
            try {
                var data = typeof payload === "string" ? JSON.parse(payload) : payload;
                if (type === "sdp")
                    await handleSdp(fromId, data);
                else if (type === "ice")
                    await handleIce(fromId, data);
            } catch (ex) {
                console.warn("classroomRtc handleSignal", ex);
            }
        },

        /** Publie (ou remplace) audio+vidéo locaux vers TOUS les pairs, puis renégocie. */
        refreshLocalTracks: async function () {
            var local = getPublishStream();
            var ids = Object.keys(peers);
            for (var i = 0; i < ids.length; i++) {
                var pc = peers[ids[i]].pc;
                if (!local) continue;

                var kinds = ["audio", "video"];
                for (var k = 0; k < kinds.length; k++) {
                    var kind = kinds[k];
                    var track = local.getTracks().find(function (t) {
                        return t.kind === kind && t.readyState !== "ended";
                    }) || null;

                    var sender = null;
                    var trMatch = pc.getTransceivers().find(function (t) {
                        var senderKind = t.sender && t.sender.track && t.sender.track.kind;
                        var recvKind = t.receiver && t.receiver.track && t.receiver.track.kind;
                        return senderKind === kind || recvKind === kind
                            || (t.mid == null && !t.sender.track && kind === "audio" && t.direction !== "inactive");
                    });
                    if (trMatch) sender = trMatch.sender;
                    if (!sender) {
                        sender = pc.getSenders().find(function (s) {
                            return s.track && s.track.kind === kind;
                        });
                    }
                    if (!sender) {
                        sender = pc.getSenders().find(function (s) { return !s.track; });
                    }

                    if (track) {
                        if (sender)
                            await sender.replaceTrack(track);
                        else
                            pc.addTrack(track, local);
                    }
                }
            }

            // Forcer une nouvelle offre pour propager micro/caméra à tous.
            for (var j = 0; j < ids.length; j++) {
                if (peers[ids[j]].pc.signalingState === "stable")
                    await makeOffer(ids[j]);
            }
            syncPlaybackRouting();
            return { ok: true, peers: ids.length, hasLocal: !!local };
        },

        /** Débloque la lecture audio distante (politique autoplay du navigateur). */
        unlockAudio: async function () {
            syncPlaybackRouting();
            var blocked = false;
            var nodes = document.querySelectorAll("video[data-rtc-peer], video[data-rtc-main]");
            for (var i = 0; i < nodes.length; i++) {
                var el = nodes[i];
                if (!el.srcObject) continue;
                try {
                    if (!el.muted)
                        await el.play();
                } catch (_) {
                    blocked = true;
                }
            }
            return { ok: !blocked, blocked: blocked };
        },

        focusRemote: function (remoteId) {
            var stream = remoteStreams[remoteId];
            if (stream) attachMain(stream, remoteId);
            return !!stream;
        },

        clearMain: function () {
            var el = document.querySelector("video[data-rtc-main]");
            if (el) {
                el.srcObject = null;
                delete el.dataset.focusedPeer;
            }
        },

        remount: function (remoteId) {
            scheduleRemount(remoteId || null);
        },

        remountAll: function () {
            scheduleRemount(null);
            // Miroir local pour la galerie (toujours muet — pas d'écho).
            var local = getPublishStream();
            var hasLiveVideo = !!(local && local.getVideoTracks().some(function (t) {
                return t.readyState === "live" && t.enabled;
            }));
            document.querySelectorAll("video[data-rtc-local]").forEach(function (el) {
                if (!local) {
                    el.srcObject = null;
                    el.classList.add("is-hidden");
                    return;
                }
                if (el.srcObject !== local)
                    el.srcObject = local;
                el.muted = true;
                el.volume = 0;
                el.playsInline = true;
                // Ne pas forcer l'affichage si la caméra est coupée (évite le rectangle noir).
                if (hasLiveVideo)
                    el.classList.remove("is-hidden");
                else
                    el.classList.add("is-hidden");
                tryPlay(el);
            });
            // Marquer les vignettes distantes avec/sans vidéo active.
            document.querySelectorAll("video[data-rtc-peer]").forEach(function (el) {
                var id = el.getAttribute("data-rtc-peer");
                var stream = id ? remoteStreams[id] : null;
                var live = !!(stream && stream.getVideoTracks().some(function (t) {
                    return t.readyState === "live" && t.enabled;
                }));
                var thumb = el.closest(".cr-pro-thumb");
                if (thumb) {
                    if (live) thumb.classList.add("has-video");
                    else thumb.classList.remove("has-video");
                }
                if (stream && el.srcObject !== stream)
                    el.srcObject = stream;
                if (live)
                    el.classList.remove("is-hidden");
                tryPlay(el);
            });
            syncPlaybackRouting();
        },

        hasRemote: function (remoteId) {
            return !!(remoteId && remoteStreams[remoteId]);
        },

        closePeer: function (remoteId) {
            var state = peers[remoteId];
            if (state) {
                try { state.pc.close(); } catch (_) { }
                delete peers[remoteId];
            }
            delete remoteStreams[remoteId];
            delete pendingConnect[remoteId];
            document.querySelectorAll('video[data-rtc-peer="' + remoteId + '"]').forEach(function (el) {
                el.srcObject = null;
            });
        },

        stop: function () {
            Object.keys(peers).forEach(function (id) {
                try { peers[id].pc.close(); } catch (_) { }
            });
            peers = {};
            remoteStreams = {};
            pendingConnect = {};
            var main = document.querySelector("video[data-rtc-main]");
            if (main) main.srcObject = null;
            document.querySelectorAll("video[data-rtc-peer]").forEach(function (el) {
                el.srcObject = null;
            });
        }
    };
})();
