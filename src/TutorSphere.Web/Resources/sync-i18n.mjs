import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));

function parseResx(file) {
  const xml = fs.readFileSync(file, "utf8");
  const map = {};
  const re = /<data name="([^"]+)"[^>]*>\s*<value>([\s\S]*?)<\/value>/g;
  let m;
  while ((m = re.exec(xml)) !== null) {
    map[m[1]] = decodeXml(m[2]);
  }
  return map;
}

function decodeXml(s) {
  return s
    .replace(/&lt;/g, "<")
    .replace(/&gt;/g, ">")
    .replace(/&amp;/g, "&")
    .replace(/&quot;/g, '"')
    .replace(/&apos;/g, "'");
}

function encodeXml(s) {
  return String(s)
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

function writeResx(file, entries) {
  const header = `<?xml version="1.0" encoding="utf-8"?>
<root>
  <xsd:schema id="root" xmlns="" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:msdata="urn:schemas-microsoft-com:xml-msdata">
    <xsd:element name="root" msdata:IsDataSet="true">
      <xsd:complexType>
        <xsd:choice maxOccurs="unbounded">
          <xsd:element name="data">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
                <xsd:element name="comment" type="xsd:string" minOccurs="0" msdata:Ordinal="2" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" msdata:Ordinal="1" />
              <xsd:attribute name="type" type="xsd:string" msdata:Ordinal="3" />
              <xsd:attribute name="mimetype" type="xsd:string" msdata:Ordinal="4" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="resheader">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" use="required" />
            </xsd:complexType>
          </xsd:element>
        </xsd:choice>
      </xsd:complexType>
    </xsd:element>
  </xsd:schema>
  <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
  <resheader name="version"><value>1.3</value></resheader>
  <resheader name="reader"><value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=2.0.3500.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
  <resheader name="writer"><value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=2.0.3500.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
`;

  const body = entries
    .map(
      ([k, v]) =>
        `  <data name="${encodeXml(k)}" xml:space="preserve">\n    <value>${encodeXml(v)}</value>\n  </data>`
    )
    .join("\n");

  fs.writeFileSync(file, `${header}${body}\n</root>\n`, "utf8");
}

async function translateBatch(texts, source, target) {
  const results = [];
  for (const text of texts) {
    if (!text || !text.trim()) {
      results.push(text ?? "");
      continue;
    }
    if (/^TutorSphere$/i.test(text.trim()) || /^[\d\s.,%$€+\-/:]+$/.test(text.trim())) {
      results.push(text);
      continue;
    }

    // Protect .NET format placeholders {0}, {1}, ...
    const placeholders = [];
    const protectedText = text.replace(/\{\d+\}/g, (m) => {
      placeholders.push(m);
      return `[[PH${placeholders.length - 1}]]`;
    });

    const url =
      "https://translate.googleapis.com/translate_a/single?client=gtx&sl=" +
      encodeURIComponent(source) +
      "&tl=" +
      encodeURIComponent(target) +
      "&dt=t&q=" +
      encodeURIComponent(protectedText.slice(0, 900));

    try {
      const res = await fetch(url);
      if (!res.ok) {
        results.push(text);
      } else {
        const data = await res.json();
        let translated = Array.isArray(data?.[0])
          ? data[0].map((part) => part?.[0] ?? "").join("")
          : text;
        placeholders.forEach((ph, i) => {
          translated = translated.replace(`[[PH${i}]]`, ph).replace(`[PH${i}]`, ph);
        });
        results.push(translated || text);
      }
    } catch {
      results.push(text);
    }
    await sleep(40);
  }
  return results;
}

function sleep(ms) {
  return new Promise((r) => setTimeout(r, ms));
}

const TARGETS = [
  { code: "fr", file: "SharedResources.fr.resx", pair: "en|fr", prefer: "fr" },
  { code: "en", file: "SharedResources.en.resx", pair: "fr|en", prefer: "en" },
  { code: "es", file: "SharedResources.es.resx", pair: "en|es", prefer: null },
  { code: "de", file: "SharedResources.de.resx", pair: "en|de", prefer: null },
  { code: "pt", file: "SharedResources.pt.resx", pair: "en|pt", prefer: null },
  { code: "zh-Hans", file: "SharedResources.zh-Hans.resx", pair: "en|zh-CN", prefer: null },
  { code: "ar", file: "SharedResources.ar.resx", pair: "en|ar", prefer: null },
];

async function main() {
  const base = parseResx(path.join(__dirname, "SharedResources.resx"));
  const existing = Object.fromEntries(
    TARGETS.map((t) => [t.code, parseResx(path.join(__dirname, t.file))])
  );

  const keys = Object.keys(base).sort((a, b) => a.localeCompare(b));
  console.log(`Source keys: ${keys.length}`);

  // Ensure FR and EN are complete first (no API if we can merge)
  const frComplete = {};
  const enComplete = {};
  for (const k of keys) {
    frComplete[k] = existing.fr[k] ?? base[k] ?? existing.en[k] ?? k;
    enComplete[k] = existing.en[k] ?? (looksFrench(base[k]) ? null : base[k]) ?? null;
  }

  // Fill EN missing from FR via translate
  const enMissingKeys = keys.filter((k) => !enComplete[k]);
  console.log(`EN missing: ${enMissingKeys.length}`);
  if (enMissingKeys.length) {
    const src = enMissingKeys.map((k) => frComplete[k]);
    const [srcLang, tgtLang] = ["fr", "en"];
    const translated = await translateBatch(src, srcLang, tgtLang);
    enMissingKeys.forEach((k, i) => {
      enComplete[k] = translated[i] || frComplete[k];
    });
  }

  // Fill FR missing from EN
  const frMissingFromBase = keys.filter((k) => !(existing.fr[k] || base[k]));
  // Actually FR should use base which is French — already handled
  writeResx(
    path.join(__dirname, "SharedResources.fr.resx"),
    keys.map((k) => [k, frComplete[k]])
  );
  writeResx(
    path.join(__dirname, "SharedResources.en.resx"),
    keys.map((k) => [k, enComplete[k] || frComplete[k]])
  );
  // Keep default (neutral) as French
  writeResx(
    path.join(__dirname, "SharedResources.resx"),
    keys.map((k) => [k, frComplete[k]])
  );
  console.log("Wrote fr/en/base");

  for (const target of TARGETS.filter((t) => !["fr", "en"].includes(t.code))) {
    const current = existing[target.code] || {};
    const out = {};
    const toTranslateKeys = [];
    for (const k of keys) {
      if (current[k] && current[k].trim()) {
        out[k] = current[k];
      } else {
        toTranslateKeys.push(k);
      }
    }
    console.log(`${target.code}: translating ${toTranslateKeys.length} keys…`);
    const [source, dest] = target.pair.split("|");
    const batchSize = 25;
    for (let i = 0; i < toTranslateKeys.length; i += batchSize) {
      const chunk = toTranslateKeys.slice(i, i + batchSize);
      const texts = chunk.map((k) => enComplete[k] || frComplete[k]);
      const translated = await translateBatch(texts, source, dest);
      chunk.forEach((k, idx) => {
        out[k] = translated[idx] || enComplete[k] || frComplete[k];
      });
      console.log(`  ${target.code}: ${Math.min(i + batchSize, toTranslateKeys.length)}/${toTranslateKeys.length}`);
    }
    writeResx(
      path.join(__dirname, target.file),
      keys.map((k) => [k, out[k] || enComplete[k] || frComplete[k]])
    );
    console.log(`Wrote ${target.file}`);
  }

  console.log("Done.");
}

function looksFrench(s) {
  if (!s) return false;
  return /[àâäéèêëïîôùûüçœæ]/i.test(s) ||
    /\b(le|la|les|des|une|pour|avec|votre|vos|mes|nous|être|cours|élève|enseignants?)\b/i.test(s);
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
