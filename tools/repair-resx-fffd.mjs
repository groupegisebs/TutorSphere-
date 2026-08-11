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
  while ((m = re.exec(text))) map[m[1]] = m[2];
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

function gitShow(rel) {
  return execSync(`git show HEAD:${rel}`, {
    cwd: root,
    maxBuffer: 20 * 1024 * 1024,
    encoding: "utf8",
  });
}

function repair(fileName) {
  const rel = `src/TutorSphere.Web/Resources/${fileName}`;
  const workText = fs.readFileSync(path.join(resDir, fileName), "utf8");
  const headText = gitShow(rel);
  const work = parseResx(workText);
  const head = parseResx(headText);
  let restored = 0;
  let keptNew = 0;
  const out = { ...head };
  for (const [k, v] of Object.entries(work)) {
    if (!(k in head)) {
      // new key — keep if not corrupted, else skip for now
      if (!v.includes("\uFFFD")) {
        out[k] = v;
        keptNew++;
      } else {
        console.warn(`  skip corrupted new key: ${k}`);
      }
    } else if (v.includes("\uFFFD") || (head[k] && !v.includes("\uFFFD") && head[k] !== v && work[k].includes("\uFFFD"))) {
      out[k] = head[k];
      restored++;
    } else if (v.includes("\uFFFD")) {
      out[k] = head[k];
      restored++;
    } else {
      // prefer work if no FFFD (may have intentional updates)
      out[k] = v;
    }
  }
  // ensure any HEAD-only keys remain
  for (const [k, v] of Object.entries(head)) {
    if (!(k in out)) out[k] = v;
  }
  fs.writeFileSync(path.join(resDir, fileName), buildResx(out), "utf8");
  console.log(
    `${fileName}: keys=${Object.keys(out).length} restored=${restored} keptNew=${keptNew}`
  );
}

// Files with FFFD corruption
for (const f of [
  "SharedResources.resx",
  "SharedResources.fr.resx",
  "SharedResources.pt.resx",
]) {
  repair(f);
}
