import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const resDir = path.join(__dirname, "../src/TutorSphere.Web/Resources");

const entries = {
  Expert_SpaceTitle: {
    fr: "Espace expert",
    en: "Expert space",
    es: "Espacio experto",
    de: "Expertenbereich",
    pt: "Espaço especialista",
    "zh-Hans": "专家空间",
    ar: "مساحة الخبير",
  },
};

function parseResx(text) {
  const map = {};
  const re = /<data name="([^"]+)"[^>]*>\s*<value>([\s\S]*?)<\/value>/g;
  let m;
  while ((m = re.exec(text))) {
    map[m[1]] = m[2]
      .replace(/&lt;/g, "<")
      .replace(/&gt;/g, ">")
      .replace(/&amp;/g, "&");
  }
  return map;
}

function esc(v) {
  return String(v)
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;");
}

function buildResx(pairs) {
  const keys = Object.keys(pairs).sort((a, b) =>
    a.localeCompare(b, "en", { sensitivity: "base" })
  );
  const body = keys
    .map(
      (k) =>
        `  <data name="${k}" xml:space="preserve">\r\n    <value>${esc(pairs[k])}</value>\r\n  </data>`
    )
    .join("\r\n");
  return (
    `<?xml version="1.0" encoding="utf-8"?>\r\n` +
    `<root>\r\n` +
    `  <resheader name="resmimetype">\r\n` +
    `    <value>text/microsoft-resx</value>\r\n` +
    `  </resheader>\r\n` +
    `  <resheader name="version">\r\n` +
    `    <value>1.3</value>\r\n` +
    `  </resheader>\r\n` +
    `${body}\r\n` +
    `</root>\r\n`
  );
}

const files = {
  "": "SharedResources.resx",
  fr: "SharedResources.fr.resx",
  en: "SharedResources.en.resx",
  es: "SharedResources.es.resx",
  de: "SharedResources.de.resx",
  pt: "SharedResources.pt.resx",
  "zh-Hans": "SharedResources.zh-Hans.resx",
  ar: "SharedResources.ar.resx",
};

for (const [lang, file] of Object.entries(files)) {
  const p = path.join(resDir, file);
  const map = parseResx(fs.readFileSync(p, "utf8"));
  const locale = lang === "" ? "fr" : lang;
  for (const [key, tr] of Object.entries(entries)) {
    map[key] = tr[locale] ?? tr.en;
  }
  fs.writeFileSync(p, buildResx(map), "utf8");
  console.log("updated", file);
}
