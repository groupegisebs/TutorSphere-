import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const resDir = path.join(__dirname, "../src/TutorSphere.Web/Resources");

function parseResx(file) {
  const t = fs.readFileSync(file, "utf8");
  const map = {};
  const re = /<data name="([^"]+)"[^>]*>\s*<value>([\s\S]*?)<\/value>/g;
  let m;
  while ((m = re.exec(t))) {
    map[m[1]] = m[2]
      .replace(/&lt;/g, "<")
      .replace(/&gt;/g, ">")
      .replace(/&amp;/g, "&")
      .replace(/&quot;/g, '"');
  }
  return map;
}

function countMojibake(map) {
  let n = 0;
  const samples = [];
  for (const [k, v] of Object.entries(map)) {
    if (/Ã[\u0080-\u00BF]|Â[\u0080-\u00BF]|â€.|ï¿½|\uFFFD/.test(v)) {
      n++;
      if (samples.length < 5) samples.push(`${k}=>${v.slice(0, 60)}`);
    }
  }
  return { n, samples };
}

const langs = ["fr", "en", "es", "de", "pt", "zh-Hans", "ar"];
const base = parseResx(path.join(resDir, "SharedResources.resx"));
console.log("base keys=" + Object.keys(base).length, "mojibake=" + countMojibake(base).n);

for (const lang of langs) {
  const map = parseResx(path.join(resDir, `SharedResources.${lang}.resx`));
  const missing = Object.keys(base).filter((k) => !(k in map));
  const extra = Object.keys(map).filter((k) => !(k in base));
  const r = countMojibake(map);
  console.log(
    `${lang} keys=${Object.keys(map).length} missing=${missing.length} extra=${extra.length} mojibake=${r.n}`
  );
  if (missing.length) console.log("  missing:", missing.join(", "));
  if (r.samples.length) console.log("  samples:", r.samples.join(" | "));
}
