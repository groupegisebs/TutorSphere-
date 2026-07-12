window.whiteboard = (function () {
    let canvas, ctx, drawing = false;
    let tool = 'pen', color = '#000000', lineWidth = 5;
    let lastX = 0, lastY = 0;
    let resizeObserver = null;

    const cursors = { pen: 'crosshair', eraser: 'cell', select: 'default', rect: 'crosshair', text: 'text', line: 'crosshair' };

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

    function startDraw(e) {
        if (tool === 'select') return;
        e.preventDefault();
        drawing = true;
        const pos = getPos(e);
        lastX = pos.x;
        lastY = pos.y;
        ctx.globalCompositeOperation = 'source-over';
        const r = (tool === 'eraser' ? lineWidth * 3 : lineWidth) / 2;
        ctx.beginPath();
        ctx.arc(pos.x, pos.y, Math.max(r, 0.5), 0, Math.PI * 2);
        ctx.fillStyle = tool === 'eraser' ? '#ffffff' : color;
        ctx.fill();
    }

    function moveDraw(e) {
        if (!drawing || tool === 'select') return;
        e.preventDefault();
        const pos = getPos(e);
        ctx.globalCompositeOperation = 'source-over';
        ctx.beginPath();
        ctx.strokeStyle = tool === 'eraser' ? '#ffffff' : color;
        ctx.lineWidth = tool === 'eraser' ? lineWidth * 3 : lineWidth;
        ctx.lineCap = 'round';
        ctx.lineJoin = 'round';
        ctx.moveTo(lastX, lastY);
        ctx.lineTo(pos.x, pos.y);
        ctx.stroke();
        lastX = pos.x;
        lastY = pos.y;
    }

    function endDraw() { drawing = false; }

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
            ctx.fillStyle = '#ffffff';
            ctx.fillRect(0, 0, w, h);
            const img = new Image();
            img.onload = () => ctx.drawImage(img, 0, 0);
            img.src = snapshot;
        } else {
            canvas.width = w;
            canvas.height = h;
            ctx.fillStyle = '#ffffff';
            ctx.fillRect(0, 0, w, h);
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

            resizeObserver = new ResizeObserver(() => resizeCanvas(true));
            resizeObserver.observe(canvas.parentElement);
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

        clear() {
            if (!canvas || !ctx) return;
            ctx.fillStyle = '#ffffff';
            ctx.fillRect(0, 0, canvas.width, canvas.height);
        },

        getCanvas() {
            return canvas || null;
        }
    };
})();
