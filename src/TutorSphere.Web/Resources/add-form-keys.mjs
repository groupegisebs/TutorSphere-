import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));

const KEYS = {
  Parents_Empty: { fr: "Aucun parent enregistré. Ajoutez votre premier contact parent.", en: "No parents registered. Add your first parent contact." },
  Parents_NoChildren: { fr: "Aucun enfant lié.", en: "No linked children." },
  Parents_FullName: { fr: "Nom complet", en: "Full name" },
  Parents_RequiredName: { fr: "Prénom et nom sont obligatoires.", en: "First and last name are required." },
  Parents_RequiredEmail: { fr: "Le courriel est obligatoire.", en: "Email is required." },
  Parents_CreateFailed: { fr: "Création impossible.", en: "Could not create." },
  Homework_Subtitle: { fr: "Créez, gérez et corrigez les devoirs de vos élèves", en: "Create, manage and grade your students' homework" },
  Homework_Edit: { fr: "Modifier le devoir", en: "Edit homework" },
  Homework_Grade: { fr: "Corriger", en: "Grade" },
  Homework_GradeTitle: { fr: "Corriger : {0}", en: "Grade: {0}" },
  Homework_Students: { fr: "Élèves concernés", en: "Assigned students" },
  Homework_ValidateGrades: { fr: "Valider corrections", en: "Confirm grades" },
  Homework_TitlePlaceholder: { fr: "Exercices chapitre 4", en: "Chapter 4 exercises" },
  Homework_DescPlaceholder: { fr: "Décrivez le travail à faire…", en: "Describe the work to do…" },
  Documents_Subtitle: { fr: "Gérez et partagez vos fichiers pédagogiques", en: "Manage and share your teaching files" },
  Documents_FilesCount: { fr: "{0} fichier(s)", en: "{0} file(s)" },
  Documents_Browse: { fr: "Parcourir", en: "Browse" },
  Documents_Formats: { fr: "PDF, Word, Excel, PowerPoint, Images — max 50 Mo", en: "PDF, Word, Excel, PowerPoint, Images — max 50 MB" },
  Documents_DestFolder: { fr: "Dossier destination", en: "Destination folder" },
  Documents_ShareOptional: { fr: "Partager avec (optionnel)", en: "Share with (optional)" },
  Common_Download: { fr: "Télécharger", en: "Download" },
  Common_Share: { fr: "Partager", en: "Share" },
  Common_All: { fr: "Tous", en: "All" },
  Common_Choose: { fr: "— Choisir —", en: "— Choose —" },
  TutorSubs_Title: { fr: "Abonnements", en: "Subscriptions" },
  TutorSubs_CreatePlan: { fr: "Créer un plan", en: "Create a plan" },
  TutorSubs_Active: { fr: "Abonnements actifs", en: "Active subscriptions" },
  TutorSubs_ExpiringSoon: { fr: "Expirent bientôt", en: "Expiring soon" },
  TutorSubs_MRR: { fr: "MRR (revenus récurrents)", en: "MRR (recurring revenue)" },
  TutorSubs_AvgAmount: { fr: "Montant moyen", en: "Average amount" },
  TutorSubs_MyPlans: { fr: "Mes plans", en: "My plans" },
  TutorSubs_Popular: { fr: "Populaire", en: "Popular" },
  TutorSubs_Subscribers: { fr: "{0} abonné(s)", en: "{0} subscriber(s)" },
  TutorSubs_Activate: { fr: "Activer", en: "Activate" },
  TutorSubs_Deactivate: { fr: "Désactiver", en: "Deactivate" },
  School_Title: { fr: "Mon École", en: "My School" },
  School_Saved: { fr: "Modifications enregistrées avec succès.", en: "Changes saved successfully." },
  School_ChangeLogo: { fr: "Changer le logo", en: "Change logo" },
  School_Banner: { fr: "Bannière", en: "Banner" },
  School_ChangeBanner: { fr: "Changer la bannière", en: "Change banner" },
  School_Name: { fr: "Nom de l'école", en: "School name" },
  School_TabIdentity: { fr: "Identité", en: "Identity" },
  Profile_Title: { fr: "Mon Profil", en: "My Profile" },
  Profile_ChangePhoto: { fr: "Changer la photo", en: "Change photo" },
  Profile_Status: { fr: "Statut", en: "Status" },
  Profile_Bio: { fr: "Biographie courte", en: "Short bio" },
  Profile_BioPlaceholder: { fr: "Décrivez votre approche pédagogique…", en: "Describe your teaching approach…" },
  Profile_YearsExp: { fr: "Années d'expérience", en: "Years of experience" },
  Profile_HourlyRate: { fr: "Tarif horaire ($/h)", en: "Hourly rate ($/h)" },
  Profile_Languages: { fr: "Langues", en: "Languages" },
  Profile_Updated: { fr: "Profil mis à jour avec succès.", en: "Profile updated successfully." },
  Profile_MySchool: { fr: "Mon école", en: "My school" },
  Reports_SessionReport: { fr: "Rapport de séance", en: "Session report" },
  Reports_NewReport: { fr: "Nouveau rapport", en: "New report" },
  Reports_SessionDate: { fr: "Date de la séance", en: "Session date" },
  Reports_Duration: { fr: "Durée (h)", en: "Duration (h)" },
  Reports_Evaluation: { fr: "Évaluation de la séance", en: "Session evaluation" },
  Reports_Content: { fr: "Contenu de la séance", en: "Session content" },
  Reports_WorkDone: { fr: "Travail effectué", en: "Work done" },
  Reports_Strengths: { fr: "Points forts", en: "Strengths" },
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
      await sleep(25);
    }
    process.stdout.write(".");
  }
  console.log("");
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
