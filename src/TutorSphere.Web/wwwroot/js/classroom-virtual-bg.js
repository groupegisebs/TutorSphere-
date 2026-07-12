/**
 * Virtual background for local classroom camera (blur or image).
 * Uses MediaPipe Selfie Segmentation when available; otherwise a soft center-mask fallback.
 */
window.classroomVirtualBg = (function () {
    var mode = "none"; // none | blur | image
    var bgImage = null;
    var running = false;
    var rawVideo = null;
    var outCanvas = null;
    var outCtx = null;
    var displayEl = null;
    var sourceStream = null;
    var outputStream = null;
    var rafId = 0;
    var selfie = null;
    var selfieLoading = null;
    var lastMask = null;
    var tmpCanvas = null;
    var tmpCtx = null;

    function ensureElements() {
        if (!rawVideo) {
            rawVideo = document.createElement("video");
            rawVideo.muted = true;
            rawVideo.playsInline = true;
            rawVideo.setAttribute("playsinline", "");
            rawVideo.style.display = "none";
            document.body.appendChild(rawVideo);
        }
        if (!outCanvas) {
            outCanvas = document.createElement("canvas");
            outCtx = outCanvas.getContext("2d", { alpha: false });
        }
        if (!tmpCanvas) {
            tmpCanvas = document.createElement("canvas");
            tmpCtx = tmpCanvas.getContext("2d", { willReadFrequently: true });
        }
    }

    function loadScript(src) {
        return new Promise(function (resolve, reject) {
            var existing = document.querySelector('script[data-cr-vbg="' + src + '"]');
            if (existing) {
                if (existing.dataset.loaded === "1") resolve();
                else existing.addEventListener("load", function () { resolve(); });
                return;
            }
            var s = document.createElement("script");
            s.src = src;
            s.async = true;
            s.dataset.crVbg = src;
            s.onload = function () { s.dataset.loaded = "1"; resolve(); };
            s.onerror = reject;
            document.head.appendChild(s);
        });
    }

    async function ensureSelfie() {
        if (selfie) return true;
        if (selfieLoading) return selfieLoading;
        selfieLoading = (async function () {
            try {
                await loadScript("https://cdn.jsdelivr.net/npm/@mediapipe/selfie_segmentation/selfie_segmentation.js");
                if (typeof SelfieSegmentation === "undefined") return false;
                selfie = new SelfieSegmentation({
                    locateFile: function (file) {
                        return "https://cdn.jsdelivr.net/npm/@mediapipe/selfie_segmentation/" + file;
                    }
                });
                selfie.setOptions({ modelSelection: 1 });
                await new Promise(function (resolve) {
                    selfie.onResults(function (results) {
                        lastMask = results;
                        resolve();
                    });
                    // Warm-up resolved on first real frame; resolve immediately after init
                    setTimeout(resolve, 50);
                });
                return true;
            } catch (_) {
                selfie = null;
                return false;
            } finally {
                selfieLoading = null;
            }
        })();
        return selfieLoading;
    }

    function drawCover(ctx, img, w, h) {
        var iw = img.naturalWidth || img.videoWidth || img.width || w;
        var ih = img.naturalHeight || img.videoHeight || img.height || h;
        if (!iw || !ih) {
            ctx.fillStyle = "#1e1b4b";
            ctx.fillRect(0, 0, w, h);
            return;
        }
        var scale = Math.max(w / iw, h / ih);
        var dw = iw * scale;
        var dh = ih * scale;
        var dx = (w - dw) / 2;
        var dy = (h - dh) / 2;
        ctx.drawImage(img, dx, dy, dw, dh);
    }

    function drawBlurredBackground(ctx, video, w, h) {
        ctx.save();
        ctx.filter = "blur(16px)";
        ctx.drawImage(video, 0, 0, w, h);
        ctx.filter = "none";
        ctx.fillStyle = "rgba(15, 23, 42, 0.15)";
        ctx.fillRect(0, 0, w, h);
        ctx.restore();
    }

    function drawImageBackground(ctx, w, h) {
        if (bgImage && (bgImage.complete || bgImage.naturalWidth)) {
            drawCover(ctx, bgImage, w, h);
        } else {
            var g = ctx.createLinearGradient(0, 0, w, h);
            g.addColorStop(0, "#312e81");
            g.addColorStop(1, "#0f172a");
            ctx.fillStyle = g;
            ctx.fillRect(0, 0, w, h);
        }
    }

    function compositeWithMask(ctx, video, maskCanvas, w, h) {
        // Background
        if (mode === "image") drawImageBackground(ctx, w, h);
        else drawBlurredBackground(ctx, video, w, h);

        // Person layer via destination-in mask
        tmpCanvas.width = w;
        tmpCanvas.height = h;
        tmpCtx.clearRect(0, 0, w, h);
        tmpCtx.drawImage(video, 0, 0, w, h);
        tmpCtx.globalCompositeOperation = "destination-in";
        tmpCtx.drawImage(maskCanvas, 0, 0, w, h);
        tmpCtx.globalCompositeOperation = "source-over";
        ctx.drawImage(tmpCanvas, 0, 0);
    }

    function compositeFallback(ctx, video, w, h) {
        if (mode === "image") drawImageBackground(ctx, w, h);
        else drawBlurredBackground(ctx, video, w, h);

        // Soft elliptical person approximation (centered)
        ctx.save();
        var grd = ctx.createRadialGradient(w * 0.5, h * 0.55, w * 0.12, w * 0.5, h * 0.52, w * 0.38);
        // Use clip path ellipse
        ctx.beginPath();
        ctx.ellipse(w * 0.5, h * 0.52, w * 0.32, h * 0.46, 0, 0, Math.PI * 2);
        ctx.clip();
        ctx.drawImage(video, 0, 0, w, h);
        ctx.restore();
        void grd;
    }

    async function processFrame() {
        if (!running || !rawVideo || !outCanvas || !outCtx) return;
        var w = rawVideo.videoWidth || 1280;
        var h = rawVideo.videoHeight || 720;
        if (w < 2 || h < 2) {
            rafId = requestAnimationFrame(processFrame);
            return;
        }
        if (outCanvas.width !== w || outCanvas.height !== h) {
            outCanvas.width = w;
            outCanvas.height = h;
        }

        try {
            if (selfie) {
                await selfie.send({ image: rawVideo });
                if (lastMask && lastMask.segmentationMask) {
                    compositeWithMask(outCtx, rawVideo, lastMask.segmentationMask, w, h);
                } else {
                    compositeFallback(outCtx, rawVideo, w, h);
                }
            } else {
                compositeFallback(outCtx, rawVideo, w, h);
            }
        } catch (_) {
            compositeFallback(outCtx, rawVideo, w, h);
        }

        rafId = requestAnimationFrame(processFrame);
    }

    function attachOutput(videoEl) {
        if (!videoEl || !outCanvas) return;
        if (!outputStream) {
            outputStream = outCanvas.captureStream(30);
        }
        videoEl.srcObject = outputStream;
        videoEl.muted = true;
        videoEl.playsInline = true;
        videoEl.classList.remove("cr-tile-video--no-mirror");
        // Already mirrored in camera; processed canvas faces same as raw — keep mirror CSS
        var play = videoEl.play();
        if (play && typeof play.catch === "function") play.catch(function () { });
    }

    async function start(videoEl, mediaStream, opts) {
        opts = opts || {};
        mode = opts.mode || "none";
        if (mode === "none") {
            stop();
            return { ok: true, mode: "none" };
        }
        if (!mediaStream || mediaStream.getVideoTracks().length === 0) {
            return { ok: false, error: "Aucune piste vidéo pour l'effet d'arrière-plan." };
        }

        ensureElements();
        displayEl = videoEl;
        sourceStream = mediaStream;

        if (opts.imageUrl) {
            await setImage(opts.imageUrl);
        }

        await ensureSelfie();

        rawVideo.srcObject = mediaStream;
        try { await rawVideo.play(); } catch (_) { }

        running = true;
        if (rafId) cancelAnimationFrame(rafId);
        // Reset output stream when restarting
        if (outputStream) {
            outputStream.getTracks().forEach(function (t) { try { t.stop(); } catch (_) { } });
            outputStream = null;
        }
        rafId = requestAnimationFrame(processFrame);
        // Small delay so first frame is drawn
        await new Promise(function (r) { setTimeout(r, 80); });
        attachOutput(videoEl);
        return { ok: true, mode: mode, segmentation: !!selfie };
    }

    function stop() {
        running = false;
        if (rafId) {
            cancelAnimationFrame(rafId);
            rafId = 0;
        }
        if (outputStream) {
            outputStream.getTracks().forEach(function (t) { try { t.stop(); } catch (_) { } });
            outputStream = null;
        }
        if (rawVideo) {
            rawVideo.srcObject = null;
        }
        mode = "none";
        lastMask = null;
    }

    function setImage(url) {
        return new Promise(function (resolve) {
            if (!url) {
                bgImage = null;
                resolve(true);
                return;
            }
            var img = new Image();
            img.crossOrigin = "anonymous";
            img.onload = function () {
                bgImage = img;
                resolve(true);
            };
            img.onerror = function () {
                bgImage = null;
                resolve(false);
            };
            img.src = url;
        });
    }

    /** Built-in gradient/scene presets as data URLs */
    function presetDataUrl(id) {
        var c = document.createElement("canvas");
        c.width = 1280;
        c.height = 720;
        var ctx = c.getContext("2d");
        if (id === "office") {
            var g = ctx.createLinearGradient(0, 0, 0, 720);
            g.addColorStop(0, "#e2e8f0");
            g.addColorStop(0.55, "#cbd5e1");
            g.addColorStop(0.55, "#94a3b8");
            g.addColorStop(1, "#64748b");
            ctx.fillStyle = g;
            ctx.fillRect(0, 0, 1280, 720);
            ctx.fillStyle = "#1e293b";
            for (var x = 40; x < 1280; x += 120) {
                ctx.fillRect(x, 80, 8, 280);
            }
        } else if (id === "library") {
            ctx.fillStyle = "#3f2a1a";
            ctx.fillRect(0, 0, 1280, 720);
            for (var row = 0; row < 6; row++) {
                for (var col = 0; col < 16; col++) {
                    ctx.fillStyle = ["#7c2d12", "#9a3412", "#b45309", "#92400e", "#78350f"][(row + col) % 5];
                    ctx.fillRect(40 + col * 76, 60 + row * 100, 60, 88);
                }
            }
        } else if (id === "classroom") {
            var g2 = ctx.createLinearGradient(0, 0, 0, 720);
            g2.addColorStop(0, "#dbeafe");
            g2.addColorStop(0.5, "#bfdbfe");
            g2.addColorStop(0.5, "#86efac");
            g2.addColorStop(1, "#4ade80");
            ctx.fillStyle = g2;
            ctx.fillRect(0, 0, 1280, 720);
            ctx.fillStyle = "#166534";
            ctx.fillRect(0, 360, 1280, 24);
        } else {
            // soft abstract
            var g3 = ctx.createRadialGradient(640, 360, 40, 640, 360, 700);
            g3.addColorStop(0, "#7c3aed");
            g3.addColorStop(0.5, "#4c1d95");
            g3.addColorStop(1, "#0f172a");
            ctx.fillStyle = g3;
            ctx.fillRect(0, 0, 1280, 720);
        }
        return c.toDataURL("image/jpeg", 0.85);
    }

    return {
        start: start,
        stop: stop,
        setImage: setImage,
        presetDataUrl: presetDataUrl,
        getMode: function () { return mode; },
        isRunning: function () { return running; },
        /** Re-bind output to a video element after layout changes */
        reattach: function (videoEl) {
            if (running && videoEl) attachOutput(videoEl);
        }
    };
})();
