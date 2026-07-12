window.whiteboard = (function () {
    let canvas, ctx, drawing = false;
    let tool = 'pen', color = '#000000', lineWidth = 5;
    let lastX = 0, lastY = 0;
    let resizeObserver = null;
    let backgroundUrl = null;
    let backgroundImg = null;
    let strokeCallback = null;
    let applyingRemote = false;

    const cursors = { pen: 'crosshair', eraser: 'cell', select: 'default', rect: 'crosshair', text: 'text', line: 'crosshair' };

    function emitStroke(phase, x, y) {
        if (!strokeCallback || applyingRemote || !canvas) return;
        var w = canvas.width || 1;
        var h = canvas.height || 1;
        try {
            strokeCallback({
                phase: phase,
                x: x / w,
                y: y / h,
                tool: tool === 'eraser' ? 'eraser' : 'pen',
                color: color,
                width: tool === 'eraser' ? lineWidth * 3 : lineWidth
            });
        } catch (_) { }
    }

    function getPos(e) {
        const rect = canvas.getBoundingClientRect();
        const scaleX = canvas.width / rect.width;
        const scaleY = canvas.height / rect.height;
        if (e.touches && e.touches.length > 0) {
            return {
                x: (e.touches[0].clientX - rect.left) * scaleX,
                y: (e.touches[0].clientY - rect.top) * scaleY
            };
        }
        return {
            x: (e.clientX - rect.left) * scaleX,
            y: (e.clientY - rect.top) * scaleY
        };
    }

    function paintDot(x, y, strokeColor, width) {
        ctx.globalCompositeOperation = 'source-over';
        const r = Math.max(width / 2, 0.5);
        ctx.beginPath();
        ctx.arc(x, y, r, 0, Math.PI * 2);
        ctx.fillStyle = strokeColor;
        ctx.fill();
    }

    function paintSegment(x0, y0, x1, y1, strokeColor, width) {
        ctx.globalCompositeOperation = 'source-over';
        ctx.beginPath();
        ctx.strokeStyle = strokeColor;
        ctx.lineWidth = width;
        ctx.lineCap = 'round';
        ctx.lineJoin = 'round';
        ctx.moveTo(x0, y0);
        ctx.lineTo(x1, y1);
        ctx.stroke();
    }

    function startDraw(e) {
        if (tool === 'select') return;
        e.preventDefault();
        drawing = true;
        const pos = getPos(e);
        lastX = pos.x;
        lastY = pos.y;
        const strokeColor = tool === 'eraser' ? '#ffffff' : color;
        const width = tool === 'eraser' ? lineWidth * 3 : lineWidth;
        paintDot(pos.x, pos.y, strokeColor, width);
        emitStroke('start', pos.x, pos.y);
    }

    function moveDraw(e) {
        if (!drawing || tool === 'select') return;
        e.preventDefault();
        const pos = getPos(e);
        const strokeColor = tool === 'eraser' ? '#ffffff' : color;
        const width = tool === 'eraser' ? lineWidth * 3 : lineWidth;
        paintSegment(lastX, lastY, pos.x, pos.y, strokeColor, width);
        lastX = pos.x;
        lastY = pos.y;
        emitStroke('move', pos.x, pos.y);
    }

    function endDraw() {
        if (!drawing) return;
        drawing = false;
        emitStroke('end', lastX, lastY);
    }

    function redrawBackground(thenDrawSnapshot) {
        if (!canvas || !ctx) return;
        ctx.fillStyle = '#ffffff';
        ctx.fillRect(0, 0, canvas.width, canvas.height);
        if (backgroundImg && backgroundImg.complete && backgroundImg.naturalWidth) {
            var iw = backgroundImg.naturalWidth;
            var ih = backgroundImg.naturalHeight;
            var scale = Math.min(canvas.width / iw, canvas.height / ih);
            var dw = iw * scale;
            var dh = ih * scale;
            var dx = (canvas.width - dw) / 2;
            var dy = (canvas.height - dh) / 2;
            ctx.drawImage(backgroundImg, dx, dy, dw, dh);
        }
        if (typeof thenDrawSnapshot === 'function') thenDrawSnapshot();
    }

    function resizeCanvas(preserveContent) {
        const container = canvas.parentElement;
        if (!container) return;
        const w = container.clientWidth;
        const h = container.clientHeight;
        if (w === 0 || h === 0) return;
        if (preserveContent) {
            const snapshot = canvas.toDataURL();
            canvas.width = w;
            canvas.height = h;
            redrawBackground(function () {
                const img = new Image();
                img.onload = function () { ctx.drawImage(img, 0, 0, w, h); };
                img.src = snapshot;
            });
        } else {
            canvas.width = w;
            canvas.height = h;
            redrawBackground();
        }
    }

    var remoteLast = {};

    function applyRemoteStroke(stroke) {
        if (!canvas || !ctx || !stroke) return;
        applyingRemote = true;
        try {
            var w = canvas.width || 1;
            var h = canvas.height || 1;
            var x = (stroke.x ?? stroke.X ?? 0) * w;
            var y = (stroke.y ?? stroke.Y ?? 0) * h;
            var phase = (stroke.phase || stroke.Phase || 'move').toLowerCase();
            var t = (stroke.tool || stroke.Tool || 'pen').toLowerCase();
            var c = stroke.color || stroke.Color || '#000000';
            var width = Number(stroke.width ?? stroke.Width ?? 5);
            var id = stroke.senderId || stroke.SenderId || 'peer';
            if (t === 'eraser') c = '#ffffff';

            var prev = remoteLast[id];
            if (phase === 'start') {
                paintDot(x, y, c, width);
                remoteLast[id] = { x: x, y: y };
            } else if (phase === 'move' || phase === 'end') {
                if (prev)
                    paintSegment(prev.x, prev.y, x, y, c, width);
                else
                    paintDot(x, y, c, width);
                if (phase === 'end')
                    delete remoteLast[id];
                else
                    remoteLast[id] = { x: x, y: y };
            }
        } finally {
            applyingRemote = false;
        }
    }

    return {
        init(canvasEl) {
            canvas = canvasEl;
            ctx = canvas.getContext('2d');
            tool = 'pen';
            color = '#000000';
            lineWidth = 5;
            drawing = false;
            backgroundUrl = null;
            backgroundImg = null;

            if (resizeObserver) { resizeObserver.disconnect(); resizeObserver = null; }

            canvas.removeEventListener('mousedown', startDraw);
            canvas.removeEventListener('mousemove', moveDraw);
            canvas.removeEventListener('mouseup', endDraw);
            canvas.removeEventListener('mouseleave', endDraw);
            canvas.removeEventListener('touchstart', startDraw);
            canvas.removeEventListener('touchmove', moveDraw);
            canvas.removeEventListener('touchend', endDraw);

            canvas.addEventListener('mousedown', startDraw);
            canvas.addEventListener('mousemove', moveDraw);
            canvas.addEventListener('mouseup', endDraw);
            canvas.addEventListener('mouseleave', endDraw);
            canvas.addEventListener('touchstart', startDraw, { passive: false });
            canvas.addEventListener('touchmove', moveDraw, { passive: false });
            canvas.addEventListener('touchend', endDraw);

            canvas.style.cursor = cursors[tool] || 'crosshair';
            canvas.style.touchAction = 'none';

            resizeCanvas(false);

            resizeObserver = new ResizeObserver(function () { resizeCanvas(true); });
            resizeObserver.observe(canvas.parentElement);
        },

        setStrokeCallback(cb) {
            strokeCallback = typeof cb === 'function' ? cb : null;
        },

        /** Blazor DotNetObjectReference with OnLocalBoardStroke(stroke). */
        setStrokeDotNetRef(dotNetRef) {
            if (!dotNetRef) {
                strokeCallback = null;
                return;
            }
            strokeCallback = function (s) {
                dotNetRef.invokeMethodAsync('OnLocalBoardStroke', s).catch(function () { });
            };
        },

        setTool(t) {
            tool = t;
            if (canvas) canvas.style.cursor = cursors[t] || 'crosshair';
        },

        setColor(c) {
            color = c;
            tool = 'pen';
            if (canvas) canvas.style.cursor = 'crosshair';
        },

        setSize(s) {
            if (s === 'thin') lineWidth = 2;
            else if (s === 'medium') lineWidth = 5;
            else if (s === 'thick') lineWidth = 12;
            else lineWidth = Number(s) || 5;
        },

        clear(keepBackground) {
            if (!canvas || !ctx) return;
            if (keepBackground) redrawBackground();
            else {
                backgroundUrl = null;
                backgroundImg = null;
                ctx.fillStyle = '#ffffff';
                ctx.fillRect(0, 0, canvas.width, canvas.height);
            }
        },

        /** Place a document/image under the ink layer (Paint-style annotation). */
        setBackground(url) {
            return new Promise(function (resolve) {
                if (!canvas || !ctx) { resolve(false); return; }
                if (!url) {
                    backgroundUrl = null;
                    backgroundImg = null;
                    redrawBackground();
                    resolve(true);
                    return;
                }
                var img = new Image();
                img.onload = function () {
                    backgroundUrl = url;
                    backgroundImg = img;
                    redrawBackground();
                    resolve(true);
                };
                img.onerror = function () { resolve(false); };
                img.src = url;
            });
        },

        applyRemoteStroke: applyRemoteStroke,

        getCanvas() {
            return canvas || null;
        },

        getBackgroundUrl() {
            return backgroundUrl;
        }
    };
})();
