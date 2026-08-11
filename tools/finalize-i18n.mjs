import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";
import { execSync } from "child_process";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.join(__dirname, "..");
const resDir = path.join(root, "src/TutorSphere.Web/Resources");

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

// Manual fix remaining PT
const ptPath = path.join(resDir, "SharedResources.pt.resx");
const pt = parseResx(fs.readFileSync(ptPath, "utf8"));
pt.Parent_LastReportText =
  "Ótima sessão — progresso notável em álgebra.";
fs.writeFileSync(ptPath, buildResx(pt), "utf8");
console.log("Fixed Parent_LastReportText");

// Compare keys that exist in both HEAD and work for ar/zh/es - report if work looks broken
function looksBroken(s) {
  return /Ã.|Â.|â€.|\uFFFD/.test(s);
}

for (const lang of ["ar", "zh-Hans", "es", "de", "pt", "fr", "en"]) {
  const file = `SharedResources.${lang}.resx`;
  const work = parseResx(fs.readFileSync(path.join(resDir, file), "utf8"));
  let headText;
  try {
    headText = execSync(`git show HEAD:src/TutorSphere.Web/Resources/${file}`, {
      cwd: root,
      maxBuffer: 20e6,
      encoding: "utf8",
    });
  } catch {
    continue;
  }
  const head = parseResx(headText);
  let worse = 0;
  let samples = [];
  for (const [k, hv] of Object.entries(head)) {
    const wv = work[k];
    if (wv == null) continue;
    if (!looksBroken(hv) && looksBroken(wv)) {
      worse++;
      if (samples.length < 5) samples.push(`${k}: ${hv.slice(0, 40)} => ${wv.slice(0, 40)}`);
    }
  }
  const brokenNow = Object.entries(work).filter(([, v]) => looksBroken(v)).length;
  console.log(`${lang}: newly_broken_vs_head=${worse} broken_now=${brokenNow}`);
  samples.forEach((s) => console.log(" ", s));
}
