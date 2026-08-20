window.tsContractSign = {
    _pads: {},

    init: function (canvasId) {
        var canvas = document.getElementById(canvasId);
        if (!canvas) return;
        var ctx = canvas.getContext("2d");
        var ratio = window.devicePixelRatio || 1;
        var rect = canvas.getBoundingClientRect();
        canvas.width = Math.max(300, rect.width) * ratio;
        canvas.height = 140 * ratio;
        canvas.style.width = Math.max(300, rect.width) + "px";
        canvas.style.height = "140px";
        ctx.scale(ratio, ratio);
        ctx.strokeStyle = "#1f1b4d";
        ctx.lineWidth = 2;
        ctx.lineCap = "round";
        ctx.lineJoin = "round";
        var drawing = false;
        var last = null;

        var pos = function (e) {
            var r = canvas.getBoundingClientRect();
            var p = e.touches && e.touches[0] ? e.touches[0] : e;
            return { x: p.clientX - r.left, y: p.clientY - r.top };
        };
        var start = function (e) {
            drawing = true;
            last = pos(e);
            e.preventDefault();
        };
        var move = function (e) {
            if (!drawing) return;
            var cur = pos(e);
            ctx.beginPath();
            ctx.moveTo(last.x, last.y);
            ctx.lineTo(cur.x, cur.y);
            ctx.stroke();
            last = cur;
            e.preventDefault();
        };
        var end = function () { drawing = false; };
        canvas.onmousedown = start;
        canvas.onmousemove = move;
        canvas.onmouseup = end;
        canvas.onmouseleave = end;
        canvas.ontouchstart = start;
        canvas.ontouchmove = move;
        canvas.ontouchend = end;
        this._pads[canvasId] = { canvas: canvas, dirty: false };
        var mark = function () { window.tsContractSign._pads[canvasId].dirty = true; };
        canvas.addEventListener("mousedown", mark);
        canvas.addEventListener("touchstart", mark);
    },

    clear: function (canvasId) {
        var pad = this._pads[canvasId];
        if (!pad) return;
        var ctx = pad.canvas.getContext("2d");
        ctx.save();
        ctx.setTransform(1, 0, 0, 1, 0, 0);
        ctx.clearRect(0, 0, pad.canvas.width, pad.canvas.height);
        ctx.restore();
        pad.dirty = false;
    },

    toDataUrl: function (canvasId) {
        var pad = this._pads[canvasId];
        if (!pad || !pad.dirty) return "";
        return pad.canvas.toDataURL("image/png");
    }
};
