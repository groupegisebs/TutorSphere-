window.classroomRtc = (function () {
    var peers = {}; // connectionId -> { pc, makingOffer, ignoreOffer, polite }
    var remoteStreams = {}; // connectionId -> MediaStream
    var dotNetRef = null;
    var iceServers = [
        { urls: "stun:stun.l.google.com:19302" },
        { urls: "stun:stun1.l.google.com:19302" }
    ];
    var selfPolite = true;

    function getPublishStream() {
        if (window.classroomMedia && typeof classroomMedia.getPublishStream === "function")
            return classroomMedia.getPublishStream();
        return null;
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

        var local = getPublishStream();
        if (local) {
            local.getTracks().forEach(function (track) {
                try { pc.addTrack(track, local); } catch (_) { }
            });
        }
        // Sans flux local : PC vide — ontrack reste déclenché à la réception de l'offre distante.

        pc.onicecandidate = function (ev) {
            if (!ev.candidate) return;
            sendSignal(remoteId, "ice", JSON.stringify(ev.candidate));
        };

        pc.ontrack = function (ev) {
            var stream = ev.streams && ev.streams[0]
                ? ev.streams[0]
                : new MediaStream([ev.track]);
            remoteStreams[remoteId] = stream;
            attachToDom(remoteId, stream);
            notifyRemoteMedia(remoteId, stream);
        };

        pc.onnegotiationneeded = async function () {
            try {
                state.makingOffer = true;
                await pc.setLocalDescription();
                sendSignal(remoteId, "sdp", JSON.stringify(pc.localDescription));
            } catch (ex) {
                console.warn("classroomRtc negotiationneeded", ex);
            } finally {
                state.makingOffer = false;
            }
        };

        pc.onconnectionstatechange = function () {
            if (pc.connectionState === "failed" || pc.connectionState === "closed" || pc.connectionState === "disconnected") {
                if (pc.connectionState === "failed") {
                    try { pc.restartIce(); } catch (_) { }
                }
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
        var hasVideo = stream.getVideoTracks().some(function (t) {
            return t.readyState === "live" && t.enabled;
        });
        var hasAudio = stream.getAudioTracks().some(function (t) {
            return t.readyState === "live" && t.enabled;
        });
        dotNetRef.invokeMethodAsync("OnRtcRemoteMedia", remoteId, hasVideo, hasAudio).catch(function () { });
    }

    function attachToDom(remoteId, stream) {
        var el = document.querySelector('video[data-rtc-peer="' + remoteId + '"]');
        if (!el) return;
        el.srcObject = stream;
        el.muted = false;
        el.playsInline = true;
        el.autoplay = true;
        el.classList.remove("cr-tile-video--no-mirror");
        var play = el.play();
        if (play && typeof play.catch === "function")
            play.catch(function () { });
    }

    function attachMain(stream) {
        var el = document.querySelector("video[data-rtc-main]");
        if (!el || !stream) return;
        el.srcObject = stream;
        el.muted = false;
        el.playsInline = true;
        var play = el.play();
        if (play && typeof play.catch === "function")
            play.catch(function () { });
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
            sendSignal(remoteId, "sdp", JSON.stringify(pc.localDescription));
        }
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

    return {
        init: function (ref, opts) {
            dotNetRef = ref;
            opts = opts || {};
            if (opts.polite === false)
                selfPolite = false;
            if (opts.iceServers && opts.iceServers.length)
                iceServers = opts.iceServers;
        },

        /** Join negotiation with an existing remote peer. New joiner should call with createOffer=true. */
        connect: async function (remoteId, createOffer) {
            if (!remoteId) return { ok: false };
            var state = ensurePc(remoteId);
            if (createOffer) {
                try {
                    state.makingOffer = true;
                    await state.pc.setLocalDescription();
                    sendSignal(remoteId, "sdp", JSON.stringify(state.pc.localDescription));
                } catch (ex) {
                    console.warn("classroomRtc connect offer", ex);
                    return { ok: false, error: ex && ex.message ? ex.message : "offer failed" };
                } finally {
                    state.makingOffer = false;
                }
            }
            // re-attach if stream already known
            if (remoteStreams[remoteId])
                attachToDom(remoteId, remoteStreams[remoteId]);
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

        /** Publish current local/camera/screen tracks to all peers (or replace). */
        refreshLocalTracks: async function () {
            var local = getPublishStream();
            var ids = Object.keys(peers);
            for (var i = 0; i < ids.length; i++) {
                var pc = peers[ids[i]].pc;
                var senders = pc.getSenders();
                if (!local) {
                    senders.forEach(function (s) {
                        if (s.track) try { pc.removeTrack(s); } catch (_) { }
                    });
                    continue;
                }
                var tracks = local.getTracks();
                for (var t = 0; t < tracks.length; t++) {
                    var track = tracks[t];
                    var sender = senders.find(function (s) {
                        return s.track && s.track.kind === track.kind;
                    });
                    if (sender)
                        await sender.replaceTrack(track);
                    else
                        pc.addTrack(track, local);
                }
            }
            return { ok: true };
        },

        focusRemote: function (remoteId) {
            var stream = remoteStreams[remoteId];
            if (stream) attachMain(stream);
            return !!stream;
        },

        clearMain: function () {
            var el = document.querySelector("video[data-rtc-main]");
            if (el) el.srcObject = null;
        },

        remount: function (remoteId) {
            if (remoteId && remoteStreams[remoteId])
                attachToDom(remoteId, remoteStreams[remoteId]);
        },

        remountAll: function () {
            Object.keys(remoteStreams).forEach(function (id) {
                attachToDom(id, remoteStreams[id]);
            });
        },

        closePeer: function (remoteId) {
            var state = peers[remoteId];
            if (state) {
                try { state.pc.close(); } catch (_) { }
                delete peers[remoteId];
            }
            delete remoteStreams[remoteId];
            var el = document.querySelector('video[data-rtc-peer="' + remoteId + '"]');
            if (el) el.srcObject = null;
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
