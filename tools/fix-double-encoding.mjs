import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const resDir = path.join(__dirname, "../src/TutorSphere.Web/Resources");

function parseResx(text) {
  const map = {};
  const re = /<data name="([^"]+)"[^>]*>\s*<value>([\s\S]*?)<\/value>/g;
  let m;
  while ((m = re.exec(text))) {
    map[m[1]] = m[2]
      .replace(/&lt;/g, "<")
      .replace(/&gt;/g, ">")
      .replace(/&amp;/g, "&")
      .replace(/&quot;/g, '"');
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

/** Common UTF-8 sequences mis-decoded as Windows-1252 / CP1252 */
const CP1252_FIXES = [
  [/â€¦/g, "…"],
  [/â€“/g, "–"],
  [/â€”/g, "—"],
  [/â€˜/g, "‘"],
  [/â€™/g, "’"],
  [/â€œ/g, "“"],
  [/â€/g, "”"],
  [/â€¢/g, "•"],
  [/â„¢/g, "™"],
  [/â‚¬/g, "€"],
  [/â‰¥/g, "≥"],
  [/â‰¤/g, "≤"],
  [/Â°/g, "°"],
  [/Â«/g, "«"],
  [/Â»/g, "»"],
];

/** Map Unicode chars that came from CP1252 0x80-0x9F back to those bytes */
const CP1252_TO_BYTE = new Map([
  [0x20ac, 0x80], // €
  [0x201a, 0x82], // ‚
  [0x0192, 0x83], // ƒ
  [0x201e, 0x84], // „
  [0x2026, 0x85], // …
  [0x2020, 0x86], // †
  [0x2021, 0x87], // ‡
  [0x02c6, 0x88], // ˆ
  [0x2030, 0x89], // ‰
  [0x0160, 0x8a], // Š
  [0x2039, 0x8b], // ‹
  [0x0152, 0x8c], // Œ
  [0x017d, 0x8e], // Ž
  [0x2018, 0x91], // ‘
  [0x2019, 0x92], // ’
  [0x201c, 0x93], // “
  [0x201d, 0x94], // ”
  [0x2022, 0x95], // •
  [0x2013, 0x96], // –
  [0x2014, 0x97], // —
  [0x02dc, 0x98], // ˜
  [0x2122, 0x99], // ™
  [0x0161, 0x9a], // š
  [0x203a, 0x9b], // ›
  [0x0153, 0x9c], // œ
  [0x017e, 0x9e], // ž
  [0x0178, 0x9f], // Ÿ
]);

function toCp1252Bytes(s) {
  const bytes = [];
  for (const ch of s) {
    const c = ch.codePointAt(0);
    if (c <= 0xff) {
      bytes.push(c);
      continue;
    }
    const b = CP1252_TO_BYTE.get(c);
    if (b === undefined) return null; // cannot represent
    bytes.push(b);
  }
  return Buffer.from(bytes);
}

function looksDoubleEncoded(s) {
  return /Ã.|Â.|â€.|Ã§|Ã£|Ã¡|Ã©|Ã­|Ã³|Ãº|Ã±|Ã¼|Ã¶|Ã¤|Ã“|Ã’|Ã‘/.test(s);
}

function fixCp1252(s) {
  let out = s;
  for (const [re, rep] of CP1252_FIXES) out = out.replace(re, rep);
  return out;
}

function fixDoubleUtf8(s) {
  // Iterate: punctuation mojibake + full CP1252 reinterpret
  let prev;
  let cur = s;
  do {
    prev = cur;
    cur = fixCp1252(cur);
    if (!looksDoubleEncoded(cur)) break;
    const buf = toCp1252Bytes(cur);
    if (!buf) break;
    const fixed = buf.toString("utf8");
    if (fixed.includes("\uFFFD") || fixed === cur) break;
    cur = fixed;
  } while (cur !== prev);
  // Strip accidental zero-width spaces introduced by bad encoding
  cur = cur.replace(/\u200B+/g, "");
  return cur;
}

function processFile(fileName) {
  const p = path.join(resDir, fileName);
  const text = fs.readFileSync(p, "utf8");
  const map = parseResx(text);
  let fixed = 0;
  for (const [k, v] of Object.entries(map)) {
    const n = fixDoubleUtf8(v);
    if (n !== v) {
      map[k] = n;
      fixed++;
    }
  }
  fs.writeFileSync(p, buildResx(map), "utf8");
  console.log(`${fileName}: fixed ${fixed} values`);
}

const files = [
  "SharedResources.resx",
  "SharedResources.fr.resx",
  "SharedResources.en.resx",
  "SharedResources.es.resx",
  "SharedResources.de.resx",
  "SharedResources.pt.resx",
  "SharedResources.zh-Hans.resx",
  "SharedResources.ar.resx",
];
for (const f of files) processFile(f);

// Add missing Billing keys for pt / zh-Hans / ar
const billing = {
  pt: {
    Billing_QuarterlyPerValidated:
      "Assinatura trimestral (por aula validada)",
    Billing_QuarterlyPerValidated_Hint:
      "Compromisso trimestral: cada aula é cobrada apenas após validação / confirmação de que ocorreu.",
  },
  "zh-Hans": {
    Billing_QuarterlyPerValidated: "季度订阅（按已确认课时）",
    Billing_QuarterlyPerValidated_Hint:
      "季度承诺：每节课仅在确认/验证已实际上课后收费。",
  },
  ar: {
    Billing_QuarterlyPerValidated: "اشتراك ربع سنوي (لكل حصة مؤكدة)",
    Billing_QuarterlyPerValidated_Hint:
      "التزام ربع سنوي: تُحسب كل حصة فقط بعد التحقق / تأكيد أنها أُجريت فعليًا.",
  },
};

function upsert(filePath, pairs) {
  let text = fs.readFileSync(filePath, "utf8");
  const map = parseResx(text);
  Object.assign(map, pairs);
  fs.writeFileSync(filePath, buildResx(map), "utf8");
}

for (const [lang, pairs] of Object.entries(billing)) {
  upsert(path.join(resDir, `SharedResources.${lang}.resx`), pairs);
  console.log(`Added Billing keys to ${lang}`);
}
