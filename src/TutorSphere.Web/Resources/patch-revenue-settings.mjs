import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));

const KEYS = {
  Revenue_ThisWeek: { fr: "cette semaine", en: "this week" },
  Revenue_ThisMonthShort: { fr: "ce mois", en: "this month" },
  Revenue_ThisQuarterShort: { fr: "ce trimestre", en: "this quarter" },
  Revenue_ThisYearShort: { fr: "cette année", en: "this year" },
  Revenue_Sessions: { fr: "Séances", en: "Sessions" },
  Revenue_Total: { fr: "Total", en: "Total" },
  Settings_RemindersInfo: {
    fr: "Les rappels de cours sont envoyés via Mail Sender environ 24 h avant la séance (enseignant, élève et parent selon les inscriptions).",
    en: "Lesson reminders are sent via Mail Sender about 24 hours before the session (teacher, student, and parent based on enrollments).",
  },
  Settings_CalendarSubscribeInfo: {
    fr: "Abonnez-vous à votre calendrier TutorSphere depuis Google Agenda, Outlook, Apple Calendar ou toute application compatible ICS / webcal.",
    en: "Subscribe to your TutorSphere calendar from Google Calendar, Outlook, Apple Calendar, or any ICS / webcal compatible app.",
  },
  Settings_Email: { fr: "E-mail", en: "Email" },
  Settings_EnableSync: { fr: "Activer la synchronisation", en: "Enable sync" },
  Settings_RegenerateLink: { fr: "Régénérer le lien", en: "Regenerate link" },
  Settings_Disable: { fr: "Désactiver", en: "Disable" },
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
