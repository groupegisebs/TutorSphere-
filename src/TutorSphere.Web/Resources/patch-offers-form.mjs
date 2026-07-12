import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));

const KEYS = {
  Offers_Cadence: { fr: "Fréquence des cours", en: "Lesson frequency" },
  Offers_DurationMin: { fr: "Durée (min)", en: "Duration (min)" },
  Offers_SessionsPerPeriod: { fr: "Séances / période", en: "Sessions / period" },
  Offers_SessionsHint: { fr: "Calculé selon jours × période", en: "Calculated from days × period" },
  Offers_DaysTimes: { fr: "Jours et heures du cours *", en: "Lesson days and times *" },
  Offers_DaysHint: { fr: "Cochez les jours et précisez l'heure de début pour chaque créneau.", en: "Check the days and set a start time for each slot." },
  Offers_Mode: { fr: "Mode", en: "Mode" },
  Offers_Hybrid: { fr: "Hybride", en: "Hybrid" },
  Offers_CancellationPolicy: { fr: "Politique d'annulation", en: "Cancellation policy" },
  Offers_NameRequiredError: { fr: "Le nom est obligatoire.", en: "Name is required." },
  Offers_DaysRequiredError: { fr: "Sélectionnez au moins un jour et une heure de cours.", en: "Select at least one day and lesson time." },
  Offers_CreateFailed: { fr: "Création impossible.", en: "Could not create." },
  Offers_UpdateFailed: { fr: "Mise à jour impossible.", en: "Could not update." },
  Day_Monday: { fr: "Lundi", en: "Monday" },
  Day_Tuesday: { fr: "Mardi", en: "Tuesday" },
  Day_Wednesday: { fr: "Mercredi", en: "Wednesday" },
  Day_Thursday: { fr: "Jeudi", en: "Thursday" },
  Day_Friday: { fr: "Vendredi", en: "Friday" },
  Day_Saturday: { fr: "Samedi", en: "Saturday" },
  Day_Sunday: { fr: "Dimanche", en: "Sunday" },
};

function parseResx(file) {
  const xml = fs.readFileSync(file, "utf8");
  const map = {};
  const re = /<data name="([^"]+)"[^>]*>\s*<value>([\s\S]*?)<\/value>/g;
  let m;
  while ((m = re.exec(xml)) !== null) {
    map[m[1]] = m[2]
      .replace(/&lt;/g, "<").replace(/&gt;/g, ">").replace(/&amp;/g, "&")
      .replace(/&quot;/g, '"').replace(/&apos;/g, "'");
  }
  return map;
}
function enc(s) {
  return String(s).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
}
function writeResx(file, map) {
  const keys = Object.keys(map).sort((a, b) => a.localeCompare(b));
  const header = `<?xml version="1.0" encoding="utf-8"?>
<root>
  <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
  <resheader name="version"><value>1.3</value></resheader>
`;
  const body = keys.map((k) => `  <data name="${enc(k)}" xml:space="preserve">\n    <value>${enc(map[k])}</value>\n  </data>`).join("\n");
  fs.writeFileSync(file, `${header}${body}\n</root>\n`, "utf8");
}
async function tr(text, tl) {
  try {
    const url = `https://translate.googleapis.com/translate_a/single?client=gtx&sl=en&tl=${tl}&dt=t&q=${encodeURIComponent(text)}`;
    const data = await (await fetch(url)).json();
    return Array.isArray(data?.[0]) ? data[0].map((p) => p?.[0] ?? "").join("") || text : text;
  } catch { return text; }
}

const LOCALES = [
  { file: "SharedResources.resx", lang: "fr" },
  { file: "SharedResources.fr.resx", lang: "fr" },
  { file: "SharedResources.en.resx", lang: "en" },
  { file: "SharedResources.es.resx", tl: "es" },
  { file: "SharedResources.de.resx", tl: "de" },
  { file: "SharedResources.pt.resx", tl: "pt" },
  { file: "SharedResources.zh-Hans.resx", tl: "zh-CN" },
  { file: "SharedResources.ar.resx", tl: "ar" },
];

const cache = {};
for (const [k, v] of Object.entries(KEYS)) {
  cache[k] = { fr: v.fr, en: v.en };
  for (const tl of ["es", "de", "pt", "zh-CN", "ar"]) {
    cache[k][tl] = await tr(v.en, tl);
  }
}
for (const loc of LOCALES) {
  const file = path.join(__dirname, loc.file);
  const map = parseResx(file);
  for (const [k, v] of Object.entries(KEYS)) {
    if (map[k]?.trim()) continue;
    map[k] = loc.lang === "fr" ? v.fr : loc.lang === "en" ? v.en : cache[k][loc.tl];
  }
  writeResx(file, map);
  console.log("ok", loc.file);
}
