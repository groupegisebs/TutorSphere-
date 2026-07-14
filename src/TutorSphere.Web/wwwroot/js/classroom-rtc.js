/**
 * WebRTC mesh for classroom A/V.
 * Perfect Negotiation: politeness per pair from ConnectionId (lexicographic),
 * only the joining peer creates the initial offer — avoids glare so everyone sees cameras.
 */
window.classroomRtc = (function () {
    var peers = {}; // connectionId -> state
    var remoteStreams = {};
    var dotNetRef = null;
    var selfId = "";
    var iceServers = [
        { urls: "stun:stun.l.google.com:19302" },
        { urls: "stun:stun1.l.google.com:19302" }
    ];
    var remountTimer = null;

    function getPublishStream() {
        if (window.classroomMedia && typeof classroomMedia.getPublishStream === "function")
            return classroomMedia.getPublishStream();
        return null;
    }

    function isPoliteToward(remoteId) {
        // Deterministic: smaller ConnectionId is polite. Works for any role pair.
        if (!selfId || !remoteId) return true;
        return selfId < remoteId;
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
            syncLocalMirrors();
            syncPlaybackRouting();
        }, 80);
    }

    function remountMainIfFocused() {
        var main = document.querySelector("video[data-rtc-main]");
        if (!main || !main.dataset.focusedPeer) return;
        var stream = remoteStreams[main.dataset.focusedPeer];
        if (stream) attachMain(stream, main.dataset.focusedPeer);
    }

    function syncLocalMirrors() {
        var local = getPublishStream();
        var hasLiveVideo = !!(local && local.getVideoTracks().some(function (t) {
            return t.readyState === "live" && t.enabled;
        }));
        document.querySelectorAll("video[data-rtc-local], video[data-rtc-local-stage]").forEach(function (el) {
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
            if (hasLiveVideo)
                el.classList.remove("is-hidden");
            else
                el.classList.add("is-hidden");
            tryPlay(el);
            var thumb = el.closest(".cr-pro-thumb");
            if (thumb) {
                if (hasLiveVideo) thumb.classList.add("has-video");
                else thumb.classList.remove("has-video");
            }
        });
    }

    function addLocalTracks(pc) {
        var local = getPublishStream();
        var hasAudio = false;
        var hasVideo = false;
        try {
            pc.getTransceivers().forEach(function (t) {
                var k = (t.receiver && t.receiver.track && t.receiver.track.kind)
                    || (t.sender && t.sender.track && t.sender.track.kind);
                if (k === "audio") hasAudio = true;
                if (k === "video") hasVideo = true;
            });
        } catch (_) { }

        try {
            if (local && local.getAudioTracks().length) {
                if (!pc.getSenders().some(function (s) { return s.track && s.track.kind === "audio"; }))
                    pc.addTrack(local.getAudioTracks()[0], local);
                hasAudio = true;
            } else if (!hasAudio) {
                pc.addTransceiver("audio", { direction: "sendrecv" });
            }
        } catch (_) { }

        try {
            if (local && local.getVideoTracks().length) {
                if (!pc.getSenders().some(function (s) { return s.track && s.track.kind === "video"; }))
                    pc.addTrack(local.getVideoTracks()[0], local);
                hasVideo = true;
            } else if (!hasVideo) {
                pc.addTransceiver("video", { direction: "sendrecv" });
            }
        } catch (_) { }
    }

    async function applyLocalTracks(pc) {
        var local = getPublishStream();
        if (!local) return { changed: false };

        var changed = false;
        var kinds = ["audio", "video"];
        for (var k = 0; k < kinds.length; k++) {
            var kind = kinds[k];
            var track = local.getTracks().find(function (t) {
                return t.kind === kind && t.readyState !== "ended";
            }) || null;

            var sender = pc.getSenders().find(function (s) {
                return s.track && s.track.kind === kind;
            });
            if (!sender) {
                var tr = pc.getTransceivers().find(function (t) {
                    if (!t.sender) return false;
                    if (t.sender.track && t.sender.track.kind === kind) return true;
                    if (t.sender.track) return false;
                    // sender sans piste : déduire le kind via le receiver
                    return !!(t.receiver && t.receiver.track && t.receiver.track.kind === kind);
                });
                if (tr) sender = tr.sender;
            }

            if (track) {
                if (kind === "video") {
                    try { track.contentHint = track.contentHint || "detail"; } catch (_) { }
                }
                if (sender) {
                    if (sender.track !== track) {
                        await sender.replaceTrack(track);
                        changed = true;
                    }
                } else {
                    pc.addTrack(track, local);
                    changed = true;
                }
            } else if (sender && sender.track) {
                // Piste retirée (fin partage sans caméra) : libérer l'émetteur.
                await sender.replaceTrack(null);
                changed = true;
            }
        }
        return { changed: changed };
    }

    function ensurePc(remoteId) {
        if (peers[remoteId])
            return peers[remoteId];

        var pc = new RTCPeerConnection({ iceServers: iceServers });
        var state = {
            pc: pc,
            makingOffer: false,
            ignoreOffer: false,
            polite: isPoliteToward(remoteId),
            // Suppress negotiationneeded until initial connect strategy is applied.
            suppressNegotiation: true,
            chain: Promise.resolve()
        };
        peers[remoteId] = state;

        addLocalTracks(pc);

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
                scheduleRemount(remoteId);
            };
            ev.track.onmute = function () {
                notifyRemoteMedia(remoteId, stream);
            };
        };

        pc.onnegotiationneeded = function () {
            if (state.suppressNegotiation) return;
            enqueue(remoteId, async function () {
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
            });
        };

        pc.onconnectionstatechange = function () {
            if (pc.connectionState === "failed") {
                try { pc.restartIce(); } catch (_) { }
            }
            if (pc.connectionState === "connected") {
                scheduleRemount(remoteId);
                var rs = remoteStreams[remoteId];
                if (rs) notifyRemoteMedia(remoteId, rs);
            }
            if (dotNetRef && typeof dotNetRef.invokeMethodAsync === "function") {
                dotNetRef.invokeMethodAsync("OnRtcConnectionState", remoteId, pc.connectionState).catch(function () { });
            }
        };

        // Gel fréquent : ICE passe brièvement en disconnected sans tuer la session.
        pc.oniceconnectionstatechange = function () {
            var ice = pc.iceConnectionState;
            if (ice === "failed") {
                try { pc.restartIce(); } catch (_) { }
            } else if (ice === "disconnected") {
                setTimeout(function () {
                    var s = peers[remoteId];
                    if (!s || s.pc !== pc) return;
                    if (pc.iceConnectionState === "disconnected" || pc.iceConnectionState === "failed") {
                        try { pc.restartIce(); } catch (_) { }
                    }
                }, 1500);
            } else if (ice === "connected" || ice === "completed") {
                scheduleRemount(remoteId);
            }
        };

        return state;
    }

    function enqueue(remoteId, fn) {
        var state = peers[remoteId];
        if (!state) return Promise.resolve();
        state.chain = state.chain.then(fn, fn).catch(function (ex) {
            console.warn("classroomRtc chain", remoteId, ex);
        });
        return state.chain;
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
        // readyState live suffit : muted/enabled transitent pendant replaceTrack / ICE
        // et provoquaient un faux « pas de vidéo » (tuile figée / avatar).
        var hasVideo = stream.getVideoTracks().some(function (t) {
            return t.readyState === "live";
        });
        var hasAudio = stream.getAudioTracks().some(function (t) {
            return t.readyState === "live";
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

    function syncPlaybackRouting() {
        var gallery = isGalleryLayout();
        var main = document.querySelector("video[data-rtc-main]");
        var focused = main && main.dataset.focusedPeer ? main.dataset.focusedPeer : null;
        var mainActive = !!(main && !gallery && focused && main.srcObject
            && !main.classList.contains("is-hidden"));

        document.querySelectorAll("video[data-rtc-peer]").forEach(function (el) {
            var id = el.getAttribute("data-rtc-peer");
            if (el.srcObject) {
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
            tryPlay(el);
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

        state.suppressNegotiation = true;
        try {
            // Perfect Negotiation : le pair poli annule son offre locale en collision.
            if (offerCollision && state.polite) {
                try {
                    await pc.setLocalDescription({ type: "rollback" });
                } catch (rbEx) {
                    console.warn("classroomRtc rollback", rbEx);
                }
            }
            await pc.setRemoteDescription(description);
            if (description.type === "offer") {
                await applyLocalTracks(pc);
                await pc.setLocalDescription();
                if (pc.localDescription)
                    sendSignal(remoteId, "sdp", JSON.stringify(pc.localDescription));
            }
        } catch (ex) {
            console.warn("classroomRtc handleSdp", remoteId, ex);
        } finally {
            state.suppressNegotiation = false;
        }

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
        state.suppressNegotiation = true;
        try {
            state.makingOffer = true;
            await applyLocalTracks(state.pc);
            await state.pc.setLocalDescription();
            if (state.pc.localDescription)
                sendSignal(remoteId, "sdp", JSON.stringify(state.pc.localDescription));
            return { ok: true };
        } catch (ex) {
            console.warn("classroomRtc makeOffer", ex);
            return { ok: false, error: ex && ex.message ? ex.message : "offer failed" };
        } finally {
            state.makingOffer = false;
            state.suppressNegotiation = false;
        }
    }

    return {
        init: function (ref, opts) {
            dotNetRef = ref;
            opts = opts || {};
            if (opts.selfConnectionId)
                selfId = String(opts.selfConnectionId);
            if (opts.iceServers && opts.iceServers.length)
                iceServers = opts.iceServers;
            // Recalculate politeness for existing peers if selfId arrived late.
            Object.keys(peers).forEach(function (id) {
                peers[id].polite = isPoliteToward(id);
            });
        },

        setSelfId: function (id) {
            selfId = id ? String(id) : "";
            Object.keys(peers).forEach(function (rid) {
                peers[rid].polite = isPoliteToward(rid);
            });
        },

        /**
         * @param remoteId peer SignalR connection id
         * @param createOffer true = this side must create the SDP offer (ID-based or joiner)
         */
        connect: async function (remoteId, createOffer) {
            if (!remoteId) return { ok: false };
            if (!selfId) {
                console.warn("classroomRtc.connect: selfId manquant — attente de l'offre distante");
                createOffer = false;
            }
            var state = ensurePc(remoteId);
            state.polite = isPoliteToward(remoteId);

            if (createOffer) {
                state.suppressNegotiation = true;
                await enqueue(remoteId, function () { return makeOffer(remoteId); });
            } else {
                state.suppressNegotiation = true;
                await applyLocalTracks(state.pc);
                setTimeout(function () {
                    var s = peers[remoteId];
                    if (!s) return;
                    if (s.pc.remoteDescription) return;
                    if (remoteStreams[remoteId]) return;
                    console.warn("classroomRtc: recovery offer toward", remoteId);
                    enqueue(remoteId, function () { return makeOffer(remoteId); });
                }, 2000);
            }

            if (remoteStreams[remoteId])
                scheduleRemount(remoteId);
            return { ok: true };
        },

        handleSignal: async function (fromId, type, payload) {
            if (!fromId || !type || !payload) return;
            try {
                var data = typeof payload === "string" ? JSON.parse(payload) : payload;
                if (type === "sdp")
                    await enqueue(fromId, function () { return handleSdp(fromId, data); });
                else if (type === "ice")
                    await enqueue(fromId, function () { return handleIce(fromId, data); });
            } catch (ex) {
                console.warn("classroomRtc handleSignal", ex);
            }
        },

        /**
         * Update published A/V on all peers.
         * After screen/camera switch, always renegotiate established links so remotes remount the new track.
         */
        refreshLocalTracks: async function () {
            var local = getPublishStream();
            var ids = Object.keys(peers);
            for (var i = 0; i < ids.length; i++) {
                var id = ids[i];
                var state = peers[id];
                var pc = state.pc;
                await enqueue(id, async function () {
                    var result = await applyLocalTracks(pc);
                    if (!pc.remoteDescription || pc.signalingState !== "stable")
                        return;
                    var linkOk = pc.connectionState === "connected"
                        || pc.iceConnectionState === "connected"
                        || pc.iceConnectionState === "completed"
                        || pc.iceConnectionState === "checking";
                    if (!linkOk) return;
                    // replaceTrack seul ne déclenche pas toujours ontrack côté distant (partage écran).
                    if (result && result.changed) {
                        try { await makeOffer(id); } catch (_) { }
                    }
                });
            }
            syncLocalMirrors();
            syncPlaybackRouting();
            return { ok: true, peers: ids.length, hasLocal: !!local };
        },

        /** Re-évalue le flux distant et notifie Blazor (caméra / partage réactivés). */
        probeRemoteMedia: function (remoteId) {
            if (!remoteId) return false;
            var stream = remoteStreams[remoteId];
            if (!stream) return false;
            attachToDom(remoteId, stream);
            notifyRemoteMedia(remoteId, stream);
            syncPlaybackRouting();
            return true;
        },

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
        },

        hasRemote: function (remoteId) {
            return !!(remoteId && remoteStreams[remoteId]);
        },

        /**
         * SignalR PeerMediaState → masquer/afficher la vignette distante.
         * IMPORTANT : ne jamais cacher l'avatar si camOn=true mais flux pas encore arrivé
         * (sinon tuile noire vide).
         */
        setRemoteMediaVisible: function (remoteId, camOn) {
            if (!remoteId) return;
            var stream = remoteStreams[remoteId];
            var hasLiveVideo = !!(stream && stream.getVideoTracks().some(function (t) {
                return t.readyState !== "ended";
            }));

            document.querySelectorAll('video[data-rtc-peer="' + remoteId + '"]').forEach(function (el) {
                var thumb = el.closest(".cr-pro-thumb");
                if (!camOn) {
                    // Caméra masquée → avatar immédiat.
                    if (thumb) thumb.classList.remove("has-video");
                    el.classList.add("is-hidden");
                    return;
                }
                // camOn=true : n'afficher la vidéo que si un flux existe déjà.
                if (!stream || !hasLiveVideo) {
                    if (thumb) thumb.classList.remove("has-video");
                    el.classList.add("is-hidden");
                    return;
                }
                if (el.srcObject !== stream)
                    el.srcObject = stream;
                el.classList.remove("is-hidden");
                if (thumb) thumb.classList.add("has-video");
                tryPlay(el);
            });
            var main = document.querySelector("video[data-rtc-main]");
            if (main && main.dataset.focusedPeer === remoteId) {
                if (!camOn || !stream) {
                    main.classList.add("is-hidden");
                } else {
                    attachMain(stream, remoteId);
                }
            }
            syncPlaybackRouting();
        },

        closePeer: function (remoteId) {
            var state = peers[remoteId];
            if (state) {
                try { state.pc.close(); } catch (_) { }
                delete peers[remoteId];
            }
            delete remoteStreams[remoteId];
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
            var main = document.querySelector("video[data-rtc-main]");
            if (main) main.srcObject = null;
            document.querySelectorAll("video[data-rtc-peer]").forEach(function (el) {
                el.srcObject = null;
            });
        }
    };
})();
