window.tsImageCrop = {
  async cropCenter(base64, contentType, aspectW, aspectH, outW, outH) {
    const blob = await (async () => {
      const bin = atob(base64);
      const bytes = new Uint8Array(bin.length);
      for (let i = 0; i < bin.length; i++) bytes[i] = bin.charCodeAt(i);
      return new Blob([bytes], { type: contentType || "image/png" });
    })();
    const img = await createImageBitmap(blob);
    const srcAspect = img.width / img.height;
    const dstAspect = aspectW / aspectH;
    let sx = 0, sy = 0, sw = img.width, sh = img.height;
    if (srcAspect > dstAspect) {
      sw = img.height * dstAspect;
      sx = (img.width - sw) / 2;
    } else {
      sh = img.width / dstAspect;
      sy = (img.height - sh) / 2;
    }
    const canvas = document.createElement("canvas");
    canvas.width = outW;
    canvas.height = outH;
    const ctx = canvas.getContext("2d");
    ctx.drawImage(img, sx, sy, sw, sh, 0, 0, outW, outH);
    const out = await new Promise((resolve) => canvas.toBlob(resolve, "image/webp", 0.9));
    if (!out) throw new Error("crop-failed");
    const buf = await out.arrayBuffer();
    const arr = new Uint8Array(buf);
    let s = "";
    for (let i = 0; i < arr.length; i++) s += String.fromCharCode(arr[i]);
    return btoa(s);
  }
};
