import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const resDir = path.join(__dirname, "../src/TutorSphere.Web/Resources");

function parseResx(file) {
  const t = fs.readFileSync(path.join(resDir, file), "utf8");
  const map = {};
  const re = /<data name="([^"]+)"[^>]*>\s*<value>([\s\S]*?)<\/value>/g;
  let m;
  while ((m = re.exec(t))) map[m[1]] = m[2];
  return map;
}

const base = parseResx("SharedResources.resx");
const fr = parseResx("SharedResources.fr.resx");
const en = parseResx("SharedResources.en.resx");
const es = parseResx("SharedResources.es.resx");

const extra = Object.keys(fr).filter((k) => !(k in base));
console.log("extra keys vs base:", extra);
extra.forEach((k) => console.log(`  ${k}: fr=${fr[k]} | es=${es[k]}`));

// FR values identical to EN (likely untranslated) — sample meaningful ones
let same = 0;
const sameSamples = [];
for (const k of Object.keys(base)) {
  if (!fr[k] || !en[k]) continue;
  if (fr[k] === en[k] && /[A-Za-z]{4,}/.test(en[k]) && en[k].length > 12) {
    // skip obvious shared tokens
    if (/^https?:|^[A-Z0-9_.-]+$/.test(en[k])) continue;
    same++;
    if (sameSamples.length < 25) sameSamples.push(`${k}: ${en[k].slice(0, 70)}`);
  }
}
console.log("\nFR identical to EN (possible untranslated):", same);
sameSamples.forEach((s) => console.log(" ", s));

// FR broken check
for (const [k, v] of Object.entries(fr)) {
  if (/Ã.|Â[\u0080-\u00BF]|â€.|\uFFFD/.test(v) && !/^Âge|café|naïve/i.test(v)) {
    // Âge is legitimate
    if (v.includes("Âge") && !/Ã.|â€.|\uFFFD/.test(v.replace(/Âge/g, ""))) continue;
    console.log("FR suspect:", k, v.slice(0, 80));
  }
}
