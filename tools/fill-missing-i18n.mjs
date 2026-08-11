import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const resDir = path.join(__dirname, "../src/TutorSphere.Web/Resources");
function readJson(p) {
  let t = fs.readFileSync(p, "utf8");
  if (t.charCodeAt(0) === 0xfeff) t = t.slice(1);
  return JSON.parse(t);
}

const missing = readJson(path.join(resDir, "_missing-for-satellites.json"));
const curated = readJson(path.join(resDir, "_missing-translations.curated.json"));

function upsertResx(filePath, pairs) {
  let text = fs.readFileSync(filePath, "utf8");
  let added = 0;
  let updated = 0;
  for (const [name, value] of Object.entries(pairs)) {
    const esc = String(value)
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;");
    const block = `  <data name="${name}" xml:space="preserve">\r\n    <value>${esc}</value>\r\n  </data>`;
    const re = new RegExp(
      `  <data name="${name.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")}"[^>]*>[\\s\\S]*?</data>`
    );
    if (re.test(text)) {
      text = text.replace(re, block);
      updated++;
    } else {
      if (!text.includes("</root>")) throw new Error("no </root> in " + filePath);
      text = text.replace(/\s*<\/root>\s*$/, `\r\n${block}\r\n</root>\r\n`);
      added++;
    }
  }
  fs.writeFileSync(filePath, text, "utf8");
  return { added, updated };
}

const langs = ["es", "de", "pt", "zh-Hans", "ar"];
const files = {
  es: "SharedResources.es.resx",
  de: "SharedResources.de.resx",
  pt: "SharedResources.pt.resx",
  "zh-Hans": "SharedResources.zh-Hans.resx",
  ar: "SharedResources.ar.resx",
};

const missingCurated = Object.keys(missing).filter((k) => !curated[k]);
if (missingCurated.length) {
  console.error("Curated missing keys:", missingCurated.length, missingCurated.slice(0, 10));
  process.exit(1);
}

for (const lang of langs) {
  const pairs = {};
  for (const key of Object.keys(missing)) {
    const tr = curated[key]?.[lang];
    if (!tr) {
      console.error(`No ${lang} for ${key}`);
      process.exit(1);
    }
    pairs[key] = tr;
  }
  const r = upsertResx(path.join(resDir, files[lang]), pairs);
  console.log(`${lang}: +${r.added} ~${r.updated}`);
}

console.log("Done.");
