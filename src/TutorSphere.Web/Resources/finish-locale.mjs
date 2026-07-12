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

function sleep(ms) {
  return new Promise((r) => setTimeout(r, ms));
}

async function translateOne(text, source, target) {
  if (!text || !text.trim()) return text ?? "";
  if (/^TutorSphere$/i.test(text.trim()) || /^[\d\s.,%$€+\-/:]+$/.test(text.trim())) return text;

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
    if (!res.ok) return text;
    const data = await res.json();
    let translated = Array.isArray(data?.[0])
      ? data[0].map((part) => part?.[0] ?? "").join("")
      : text;
    placeholders.forEach((ph, i) => {
      translated = translated.replaceAll(`[[PH${i}]]`, ph).replaceAll(`[PH${i}]`, ph);
    });
    return translated || text;
  } catch {
    return text;
  }
}

async function main() {
  const only = process.argv[2] || "ar";
  const pair = {
    ar: ["en", "ar"],
    es: ["en", "es"],
    de: ["en", "de"],
    pt: ["en", "pt"],
    "zh-Hans": ["en", "zh-CN"],
    fr: ["en", "fr"],
    en: ["fr", "en"],
  }[only];

  if (!pair) {
    console.error("Unknown locale", only);
    process.exit(1);
  }

  const fileName =
    only === "zh-Hans" ? "SharedResources.zh-Hans.resx" : `SharedResources.${only}.resx`;
  const en = parseResx(path.join(__dirname, "SharedResources.en.resx"));
  const fr = parseResx(path.join(__dirname, "SharedResources.fr.resx"));
  const current = parseResx(path.join(__dirname, fileName));
  const keys = Object.keys(en).sort((a, b) => a.localeCompare(b));

  const out = { ...current };
  const missing = keys.filter((k) => !current[k] || !String(current[k]).trim());
  console.log(`${only}: ${missing.length} missing of ${keys.length}`);

  const [source, dest] = pair;
  const concurrency = 8;
  let done = 0;

  for (let i = 0; i < missing.length; i += concurrency) {
    const chunk = missing.slice(i, i + concurrency);
    const results = await Promise.all(
      chunk.map(async (k) => {
        const src = en[k] || fr[k] || k;
        const translated = await translateOne(src, source, dest);
        await sleep(20);
        return [k, translated];
      })
    );
    for (const [k, v] of results) out[k] = v;
    done += chunk.length;
    console.log(`  ${only}: ${done}/${missing.length}`);
  }

  // Ensure every key exists
  for (const k of keys) {
    if (!out[k]) out[k] = en[k] || fr[k] || k;
  }

  writeResx(
    path.join(__dirname, fileName),
    keys.map((k) => [k, out[k]])
  );
  console.log(`Wrote ${fileName} (${keys.length} keys)`);
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
