import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));

const KEYS = {
  Students_CountInSchool: { fr: "{0} élève(s) dans votre école", en: "{0} student(s) in your school" },
  Students_Empty: { fr: "Aucun élève trouvé. Ajoutez votre premier élève.", en: "No students found. Add your first student." },
  Students_Loading: { fr: "Chargement des élèves…", en: "Loading students…" },
  Students_Since: { fr: "Depuis {0}", en: "Since {0}" },
  Students_NoneParent: { fr: "— Aucun —", en: "— None —" },
  Students_Notes: { fr: "Observations", en: "Notes" },
  Students_Level: { fr: "Niveau", en: "Level" },
  Students_Autonomous: { fr: "Autonome", en: "Autonomous" },
  Students_AgeYears: { fr: "{0} ans", en: "{0} yrs" },
  Common_Saving: { fr: "Enregistrement…", en: "Saving…" },
  Common_RequiredStar: { fr: "*", en: "*" },
  Level_Primary: { fr: "Primaire", en: "Primary" },
  Level_Middle: { fr: "Collège", en: "Middle school" },
  Level_High: { fr: "Lycée", en: "High school" },
  Level_University: { fr: "Université", en: "University" },
  Parents_Count: { fr: "{0} parent(s)", en: "{0} parent(s)" },
  Parents_Loading: { fr: "Chargement des parents…", en: "Loading parents…" },
  Parents_Info: { fr: "Infos", en: "Info" },
  Parents_ChildrenCount: { fr: "Enfant(s)", en: "Child(ren)" },
  Homework_Loading: { fr: "Chargement…", en: "Loading…" },
  Documents_ComingSoon: { fr: "Bientôt", en: "Coming soon" },
  Documents_SharedWith: { fr: "Partagé avec", en: "Shared with" },
  Documents_UploadFiles: { fr: "Téléverser des fichiers", en: "Upload files" },
  Documents_DropHere: { fr: "Glissez vos fichiers ici", en: "Drop your files here" },
  Messages_Search: { fr: "Rechercher…", en: "Search…" },
  Messages_VideoCall: { fr: "Appel vidéo", en: "Video call" },
  Messages_Call: { fr: "Appel", en: "Call" },
  Messages_Attachment: { fr: "Pièce jointe", en: "Attachment" },
  TutorSubs_Subtitle: { fr: "Gérez les abonnements de vos élèves", en: "Manage your students' subscriptions" },
  School_Subtitle: { fr: "Identité et présence en ligne de votre école", en: "Your school's identity and online presence" },
  Profile_Subtitle: { fr: "Présentez-vous aux parents et élèves", en: "Present yourself to parents and students" },
  Reports_Subtitle: { fr: "Compte-rendus envoyés aux parents", en: "Reports sent to parents" },
  Common_Back: { fr: "Retour", en: "Back" },
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
  const ph = [];
  const protectedText = text.replace(/\{\d+\}/g, (m) => { ph.push(m); return `[[PH${ph.length - 1}]]`; });
  try {
    const url = `https://translate.googleapis.com/translate_a/single?client=gtx&sl=en&tl=${tl}&dt=t&q=${encodeURIComponent(protectedText)}`;
    const data = await (await fetch(url)).json();
    let out = Array.isArray(data?.[0]) ? data[0].map((p) => p?.[0] ?? "").join("") : text;
    ph.forEach((p, i) => { out = out.replaceAll(`[[PH${i}]]`, p); });
    return out || text;
  } catch { return text; }
}
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

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

async function main() {
  const cache = {};
  for (const [k, v] of Object.entries(KEYS)) {
    cache[k] = { fr: v.fr, en: v.en };
    for (const tl of ["es", "de", "pt", "zh-CN", "ar"]) {
      cache[k][tl] = await tr(v.en, tl);
      await sleep(20);
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
    console.log("updated", loc.file, Object.keys(map).length);
  }
}
main();
