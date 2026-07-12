import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));

const KEYS = {
  // School
  School_TabIdentity: { fr: "Identité", en: "Identity" },
  School_TabContact: { fr: "Coordonnées", en: "Contact" },
  School_TabWeb: { fr: "Présence web", en: "Web presence" },
  School_TabMedia: { fr: "Médias", en: "Media" },
  School_Tagline: { fr: "Slogan", en: "Tagline" },
  School_TaglinePlaceholder: { fr: "Votre réussite, notre mission.", en: "Your success, our mission." },
  School_Description: { fr: "Description", en: "Description" },
  School_Mission: { fr: "Mission", en: "Mission" },
  School_Vision: { fr: "Vision", en: "Vision" },
  School_Country: { fr: "Pays", en: "Country" },
  School_Province: { fr: "Province / État", en: "Province / State" },
  School_City: { fr: "Ville", en: "City" },
  School_PostalCode: { fr: "Code postal", en: "Postal code" },
  School_Address: { fr: "Adresse complète", en: "Full address" },
  School_ContactEmail: { fr: "Courriel de contact", en: "Contact email" },
  School_Subdomain: { fr: "Sous-domaine TutorSphere", en: "TutorSphere subdomain" },
  School_PublicUrl: { fr: "Votre URL publique :", en: "Your public URL:" },
  School_CustomDomain: { fr: "Domaine personnalisé", en: "Custom domain" },
  School_DnsHint: { fr: "Configurez un CNAME vers tutorsphere.com dans votre registraire DNS.", en: "Configure a CNAME to tutorsphere.com in your DNS registrar." },
  School_Social: { fr: "Réseaux sociaux", en: "Social networks" },
  School_MediaHint: { fr: "Gérez la galerie photos, vidéos et certificats de votre école.", en: "Manage your school's photo, video and certificate gallery." },
  School_DropFiles: { fr: "Glissez des fichiers ici ou cliquez pour téléverser", en: "Drop files here or click to upload" },
  School_MediaFormats: { fr: "JPG, PNG, PDF · Max 10 Mo", en: "JPG, PNG, PDF · Max 10 MB" },

  // Profile
  Profile_TabPresentation: { fr: "Présentation", en: "Presentation" },
  Profile_TabCredentials: { fr: "Diplômes & Certifications", en: "Degrees & Certifications" },
  Profile_TabSubjects: { fr: "Matières", en: "Subjects" },
  Profile_TabAvailability: { fr: "Disponibilités", en: "Availability" },
  Profile_FullCv: { fr: "Présentation complète (CV)", en: "Full presentation (CV)" },
  Profile_FullCvPlaceholder: { fr: "Parcours, méthode pédagogique, spécialités…", en: "Background, teaching method, specialties…" },
  Profile_StatusAvailable: { fr: "Disponible", en: "Available" },
  Profile_StatusFull: { fr: "Complet", en: "Fully booked" },
  Profile_StatusInactive: { fr: "Inactif", en: "Inactive" },
  Profile_Diplomas: { fr: "Diplômes", en: "Degrees" },
  Profile_Certifications: { fr: "Certifications", en: "Certifications" },
  Profile_AddDiploma: { fr: "Ajouter le diplôme", en: "Add degree" },
  Profile_AddCert: { fr: "Ajouter la certification", en: "Add certification" },
  Profile_NoDiplomas: { fr: "Aucun diplôme. Cliquez sur « Ajouter » pour en créer un.", en: "No degrees yet. Click “Add” to create one." },
  Profile_NoCerts: { fr: "Aucune certification. Cliquez sur « Ajouter » pour en créer une.", en: "No certifications yet. Click “Add” to create one." },
  Profile_TitleLabel: { fr: "Intitulé", en: "Title" },
  Profile_Institution: { fr: "Établissement", en: "Institution" },
  Profile_Organization: { fr: "Organisme", en: "Organization" },
  Profile_Year: { fr: "Année", en: "Year" },
  Profile_SubjectsHint: { fr: "Sélectionnez les matières que vous enseignez.", en: "Select the subjects you teach." },
  Profile_AvailabilityHint: { fr: "Indiquez vos plages horaires disponibles.", en: "Indicate your available time slots." },
  Profile_CredentialRequired: { fr: "Intitulé et établissement sont obligatoires.", en: "Title and institution are required." },

  // Reports extras
  Reports_Improvements: { fr: "Points à améliorer", en: "Areas to improve" },
  Reports_Recommendations: { fr: "Recommandations & objectifs", en: "Recommendations & goals" },
  Reports_Homework: { fr: "Devoir à rendre", en: "Homework assigned" },
  Reports_Sent: { fr: "Envoyé", en: "Sent" },
  Reports_SentPlural: { fr: "Envoyés", en: "Sent" },
  Reports_Draft: { fr: "Brouillon", en: "Draft" },
  Reports_Drafts: { fr: "Brouillons", en: "Drafts" },
  Reports_Send: { fr: "Envoyer", en: "Send" },
  Reports_Pdf: { fr: "PDF", en: "PDF" },

  // Tutor subscriptions
  TutorSubs_SubscribersTitle: { fr: "Abonnés", en: "Subscribers" },
  TutorSubs_StudentParent: { fr: "Élève / Parent", en: "Student / Parent" },
  TutorSubs_Plan: { fr: "Plan", en: "Plan" },
  TutorSubs_Amount: { fr: "Montant", en: "Amount" },
  TutorSubs_Renewal: { fr: "Renouvellement", en: "Renewal" },
  TutorSubs_Suspend: { fr: "Suspendre", en: "Suspend" },
  TutorSubs_Reactivate: { fr: "Réactiver", en: "Reactivate" },
  TutorSubs_Invoice: { fr: "Facture", en: "Invoice" },
  TutorSubs_CreatePlanTitle: { fr: "Créer un plan d'abonnement", en: "Create a subscription plan" },
  TutorSubs_PlanName: { fr: "Nom du plan", en: "Plan name" },
  TutorSubs_Price: { fr: "Prix ($)", en: "Price ($)" },
  TutorSubs_Period: { fr: "Période", en: "Period" },
  TutorSubs_PeriodMonth: { fr: "mois", en: "month" },
  TutorSubs_PeriodQuarter: { fr: "trimestre", en: "quarter" },
  TutorSubs_PeriodSemester: { fr: "semestre", en: "semester" },
  TutorSubs_PeriodYear: { fr: "an", en: "year" },
  TutorSubs_Features: { fr: "Fonctionnalités incluses (une par ligne)", en: "Included features (one per line)" },
  TutorSubs_MarkPopular: { fr: "Marquer comme \"Populaire\"", en: "Mark as “Popular”" },
  TutorSubs_Create: { fr: "Créer", en: "Create" },
  TutorSubs_StatusActive: { fr: "Actif", en: "Active" },
  TutorSubs_StatusSuspended: { fr: "Suspendu", en: "Suspended" },
  TutorSubs_StatusCancelled: { fr: "Résilié", en: "Cancelled" },

  // Offers form extras
  Offers_CreateTitle: { fr: "Nouvelle offre", en: "New offer" },
  Offers_EditTitle: { fr: "Modifier l'offre", en: "Edit offer" },
  Offers_Name: { fr: "Nom de l'offre", en: "Offer name" },
  Offers_Online: { fr: "En ligne", en: "Online" },
  Offers_InPerson: { fr: "Présentiel", en: "In person" },
  Offers_Published: { fr: "Publié", en: "Published" },
  Offers_Draft: { fr: "Brouillon", en: "Draft" },
  Offers_Sessions: { fr: "séances", en: "sessions" },
  Offers_Subscribers: { fr: "abonné(s)", en: "subscriber(s)" },

  // Calendar
  Calendar_NewLesson: { fr: "Nouveau cours", en: "New lesson" },
  Calendar_Unavailability: { fr: "Indisponibilité", en: "Unavailability" },
  Calendar_ModeOnline: { fr: "En ligne", en: "Online" },
  Calendar_ModeInPerson: { fr: "Présentiel", en: "In person" },
  Calendar_NoStudents: { fr: "Aucun élève disponible.", en: "No students available." },

  // Parent children
  Children_Title: { fr: "Mes Enfants", en: "My Children" },
  Children_Add: { fr: "Ajouter un enfant", en: "Add a child" },
  Children_Empty: { fr: "Aucun enfant enregistré.", en: "No children registered." },
  Children_School: { fr: "École", en: "School" },
  Children_Subjects: { fr: "Matières suivies", en: "Subjects taken" },
  Children_Search: { fr: "Rechercher un enfant…", en: "Search for a child…" },

  // Classroom core
  Classroom_Title: { fr: "Salle de classe", en: "Classroom" },
  Classroom_EndSession: { fr: "Terminer la séance", en: "End session" },
  Classroom_Gallery: { fr: "Affichage Galerie", en: "Gallery view" },
  Classroom_Pen: { fr: "Crayon", en: "Pen" },
  Classroom_Eraser: { fr: "Gomme", en: "Eraser" },
  Classroom_ClearAll: { fr: "Effacer tout", en: "Clear all" },
  Classroom_TypeMessage: { fr: "Tapez un message…", en: "Type a message…" },
  Classroom_Mic: { fr: "Micro", en: "Mic" },
  Classroom_Camera: { fr: "Caméra", en: "Camera" },
  Classroom_Share: { fr: "Partager", en: "Share" },
  Classroom_Leave: { fr: "Quitter", en: "Leave" },

  // Common extras
  Common_Create: { fr: "Créer", en: "Create" },
  Common_Send: { fr: "Envoyer", en: "Send" },
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
  const entries = Object.entries(KEYS);
  for (let i = 0; i < entries.length; i++) {
    const [k, v] = entries[i];
    cache[k] = { fr: v.fr, en: v.en };
    for (const tl of ["es", "de", "pt", "zh-CN", "ar"]) {
      cache[k][tl] = await tr(v.en, tl);
      await sleep(20);
    }
    if (i % 10 === 0) process.stdout.write(`${i}/${entries.length} `);
  }
  console.log("\nwriting…");
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
