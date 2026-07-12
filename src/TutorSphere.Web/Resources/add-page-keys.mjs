import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));

/** French source + English; other langs filled via translate API */
const KEYS = {
  // Common
  Common_Cancel: { fr: "Annuler", en: "Cancel" },
  Common_Save: { fr: "Enregistrer", en: "Save" },
  Common_Create: { fr: "Créer", en: "Create" },
  Common_Edit: { fr: "Modifier", en: "Edit" },
  Common_Delete: { fr: "Supprimer", en: "Delete" },
  Common_Export: { fr: "Exporter", en: "Export" },
  Common_Search: { fr: "Rechercher", en: "Search" },
  Common_Loading: { fr: "Chargement…", en: "Loading…" },
  Common_Actions: { fr: "Actions", en: "Actions" },
  Common_Status: { fr: "Statut", en: "Status" },
  Common_Date: { fr: "Date", en: "Date" },
  Common_Amount: { fr: "Montant", en: "Amount" },
  Common_Mode: { fr: "Mode", en: "Mode" },
  Common_Offer: { fr: "Offre", en: "Offer" },
  Common_AllStatuses: { fr: "Tous les statuts", en: "All statuses" },
  Common_AllModes: { fr: "Tous les modes", en: "All modes" },
  Common_Language: { fr: "Langue", en: "Language" },
  Common_Timezone: { fr: "Fuseau horaire", en: "Time zone" },
  Common_General: { fr: "Général", en: "General" },
  Common_Description: { fr: "Description", en: "Description" },
  Common_Title: { fr: "Titre", en: "Title" },
  Common_Subject: { fr: "Matière", en: "Subject" },
  Common_Student: { fr: "Élève", en: "Student" },
  Common_Parent: { fr: "Parent", en: "Parent" },
  Common_Duration: { fr: "Durée", en: "Duration" },
  Common_Online: { fr: "En ligne", en: "Online" },
  Common_InPerson: { fr: "Présentiel", en: "In person" },
  Common_Pending: { fr: "En attente", en: "Pending" },
  Common_Paid: { fr: "Payé", en: "Paid" },
  Common_Failed: { fr: "Échoué", en: "Failed" },
  Common_Refunded: { fr: "Remboursé", en: "Refunded" },
  Common_Card: { fr: "Carte", en: "Card" },
  Common_Transfer: { fr: "Virement", en: "Bank transfer" },
  Common_Cash: { fr: "Espèces", en: "Cash" },
  Common_Planned: { fr: "Planifié", en: "Planned" },
  Common_InProgress: { fr: "En cours", en: "In progress" },
  Common_Completed: { fr: "Terminé", en: "Completed" },
  Common_Cancelled: { fr: "Annulé", en: "Cancelled" },
  Common_Today: { fr: "Aujourd'hui", en: "Today" },
  Common_ThisWeek: { fr: "Cette semaine", en: "This week" },
  Common_ThisMonth: { fr: "Ce mois", en: "This month" },
  Common_Published: { fr: "Publié", en: "Published" },
  Common_Draft: { fr: "Brouillon", en: "Draft" },
  Common_Active: { fr: "Actif", en: "Active" },
  Common_Inactive: { fr: "Inactif", en: "Inactive" },
  Common_Copy: { fr: "Copier", en: "Copy" },
  Common_Add: { fr: "Ajouter", en: "Add" },
  Common_View: { fr: "Voir", en: "View" },
  Common_FirstName: { fr: "Prénom", en: "First name" },
  Common_LastName: { fr: "Nom", en: "Last name" },
  Common_Email: { fr: "Courriel", en: "Email" },
  Common_Phone: { fr: "Téléphone", en: "Phone" },
  Common_Notifications: { fr: "Notifications", en: "Notifications" },
  Common_Calendar: { fr: "Agenda", en: "Calendar" },

  // Payments
  Payments_Title: { fr: "Paiements", en: "Payments" },
  Payments_Subtitle: { fr: "Historique et suivi de vos encaissements", en: "Payment history and tracking" },
  Payments_CreateInvoice: { fr: "Créer une facture", en: "Create invoice" },
  Payments_CreateInvoiceManual: { fr: "Créer une facture manuelle", en: "Create manual invoice" },
  Payments_CollectedThisMonth: { fr: "Encaissé ce mois", en: "Collected this month" },
  Payments_Pending: { fr: "En attente", en: "Pending" },
  Payments_FailedLate: { fr: "Échec / Retard", en: "Failed / Late" },
  Payments_TotalTransactions: { fr: "Transactions totales", en: "Total transactions" },
  Payments_SearchPlaceholder: { fr: "Chercher par parent, élève…", en: "Search by parent, student…" },
  Payments_Col_ParentStudent: { fr: "Parent / Élève", en: "Parent / Student" },
  Payments_OfferDesc: { fr: "Offre / Description *", en: "Offer / Description *" },
  Payments_AmountLabel: { fr: "Montant ($) *", en: "Amount ($) *" },
  Payments_PaymentMethod: { fr: "Mode de paiement", en: "Payment method" },
  Payments_ParentRequired: { fr: "Parent *", en: "Parent *" },
  Payments_PdfInvoice: { fr: "Facture PDF", en: "PDF invoice" },
  Payments_Receipt: { fr: "Reçu", en: "Receipt" },
  Payments_MarkPaid: { fr: "Marquer payé", en: "Mark as paid" },
  Payments_InvoiceNumber: { fr: "Facture #{0}", en: "Invoice #{0}" },

  // Lessons
  Lessons_Title: { fr: "Cours & Séances", en: "Courses & Sessions" },
  Lessons_Subtitle: { fr: "Historique et planification de vos séances", en: "Session history and scheduling" },
  Lessons_New: { fr: "Nouvelle séance", en: "New session" },
  Lessons_Edit: { fr: "Modifier la séance", en: "Edit session" },
  Lessons_CompletedCount: { fr: "Terminées", en: "Completed" },
  Lessons_HoursTaught: { fr: "Heures enseignées", en: "Hours taught" },
  Lessons_SearchPlaceholder: { fr: "Élève, matière…", en: "Student, subject…" },
  Lessons_Col_DateTime: { fr: "Date / Heure", en: "Date / Time" },
  Lessons_Start: { fr: "Démarrer", en: "Start" },
  Lessons_Report: { fr: "Rapport", en: "Report" },
  Lessons_Attendance: { fr: "Présences", en: "Attendance" },
  Lessons_Delete: { fr: "Supprimer", en: "Delete" },
  Lessons_DurationHours: { fr: "Durée (h)", en: "Duration (h)" },
  Lessons_StartTime: { fr: "Début", en: "Start" },
  Lessons_Type: { fr: "Type", en: "Type" },
  Lessons_Individual: { fr: "Individuel", en: "Individual" },
  Lessons_Group: { fr: "Collectif", en: "Group" },
  Lessons_Intensive: { fr: "Intensif", en: "Intensive" },
  Lessons_Recurrence: { fr: "Récurrence", en: "Recurrence" },
  Lessons_Once: { fr: "Une seule fois", en: "One-time" },
  Lessons_Weekly: { fr: "Chaque semaine", en: "Weekly" },
  Lessons_Biweekly: { fr: "Toutes les 2 semaines", en: "Every 2 weeks" },
  Lessons_Monthly: { fr: "Chaque mois", en: "Monthly" },
  Lessons_Occurrences: { fr: "Nombre de séances", en: "Number of sessions" },
  Lessons_Until: { fr: "Ou jusqu’au", en: "Or until" },
  Lessons_Description: { fr: "Description / Objectifs", en: "Description / Goals" },
  Lessons_SubjectRequired: { fr: "Matière *", en: "Subject *" },
  Lessons_DateRequired: { fr: "Date *", en: "Date *" },

  // Offers
  Offers_Title: { fr: "Mes Offres", en: "My Offers" },
  Offers_Subtitle: { fr: "Gérez vos formules, tarifs et horaires de cours", en: "Manage your packages, rates and schedules" },
  Offers_PublicCatalog: { fr: "Catalogue public", en: "Public catalog" },
  Offers_New: { fr: "Nouvelle offre", en: "New offer" },
  Offers_Created: { fr: "Offres créées", en: "Offers created" },
  Offers_PublishedCount: { fr: "Publiées", en: "Published" },
  Offers_ActiveSubscribers: { fr: "Abonnés actifs", en: "Active subscribers" },
  Offers_MonthlyRevenue: { fr: "Revenus / mois", en: "Revenue / month" },
  Offers_NameRequired: { fr: "Nom de l'offre *", en: "Offer name *" },
  Offers_PackagePrice: { fr: "Prix forfait *", en: "Package price *" },
  Offers_HourlyRate: { fr: "Taux horaire ($/h)", en: "Hourly rate ($/h)" },
  Offers_Currency: { fr: "Devise", en: "Currency" },
  Offers_BillingPeriod: { fr: "Période de facturation", en: "Billing period" },
  Offers_Publish: { fr: "Publier", en: "Publish" },
  Offers_Unpublish: { fr: "Dépublier", en: "Unpublish" },
  Offers_CreateOffer: { fr: "Créer l'offre", en: "Create offer" },
  Offers_Week: { fr: "Semaine", en: "Week" },
  Offers_Month: { fr: "Mois", en: "Month" },

  // Settings
  Settings_Title: { fr: "Paramètres", en: "Settings" },
  Settings_Subtitle: { fr: "Notifications, agenda et préférences", en: "Notifications, calendar and preferences" },
  Settings_Saved: { fr: "Paramètres sauvegardés.", en: "Settings saved." },
  Settings_EmailNotifications: { fr: "Notifications par e-mail", en: "Email notifications" },
  Settings_LessonReminder: { fr: "Rappel de cours", en: "Lesson reminder" },
  Settings_LessonReminderDesc: { fr: "Recevoir un e-mail avant chaque séance planifiée.", en: "Receive an email before each scheduled session." },
  Settings_SyncCalendar: { fr: "Synchroniser mon agenda", en: "Sync my calendar" },
  Settings_NoActiveSub: { fr: "Aucun abonnement actif.", en: "No active subscription." },
  Settings_HttpsUrl: { fr: "URL HTTPS (Google, Outlook…)", en: "HTTPS URL (Google, Outlook…)" },
  Settings_WebcalUrl: { fr: "URL webcal (Apple Calendar…)", en: "webcal URL (Apple Calendar…)" },

  // Revenue
  Revenue_Title: { fr: "Revenus", en: "Revenue" },
  Revenue_Subtitle: { fr: "Analyse financière de votre activité", en: "Financial analysis of your activity" },
  Revenue_ThisQuarter: { fr: "Ce trimestre", en: "This quarter" },
  Revenue_ThisYear: { fr: "Cette année", en: "This year" },
  Revenue_ActiveSubscriptions: { fr: "Abonnements actifs", en: "Active subscriptions" },
  Revenue_PendingPayments: { fr: "Paiements en attente", en: "Pending payments" },
  Revenue_ActiveStudents: { fr: "Élèves actifs", en: "Active students" },
  Revenue_Evolution: { fr: "Évolution des revenus", en: "Revenue trend" },
  Revenue_BySubject: { fr: "Par matière", en: "By subject" },
  Revenue_ByStudent: { fr: "Par élève", en: "By student" },
  Revenue_RecentTransactions: { fr: "Dernières transactions", en: "Recent transactions" },

  // Subscriptions (tutor)
  TutorSubs_Title: { fr: "Abonnements", en: "Subscriptions" },
  TutorSubs_CreatePlan: { fr: "Créer un plan", en: "Create a plan" },
  TutorSubs_Active: { fr: "Abonnements actifs", en: "Active subscriptions" },
  TutorSubs_ExpiringSoon: { fr: "Expirent bientôt", en: "Expiring soon" },
  TutorSubs_MRR: { fr: "MRR (revenus récurrents)", en: "MRR (recurring revenue)" },
  TutorSubs_AvgAmount: { fr: "Montant moyen", en: "Average amount" },
  TutorSubs_MyPlans: { fr: "Mes plans", en: "My plans" },
  TutorSubs_Subscribers: { fr: "Abonnés", en: "Subscribers" },
  TutorSubs_StudentParent: { fr: "Élève / Parent", en: "Student / Parent" },
  TutorSubs_Renewal: { fr: "Renouvellement", en: "Renewal" },
  TutorSubs_Suspend: { fr: "Suspendre", en: "Suspend" },

  // Students
  Students_Title: { fr: "Mes Élèves", en: "My Students" },
  Students_Add: { fr: "Ajouter un élève", en: "Add a student" },
  Students_Search: { fr: "Rechercher un élève…", en: "Search for a student…" },
  Students_AllLevels: { fr: "Tous les niveaux", en: "All levels" },
  Students_NoneFound: { fr: "Aucun élève trouvé.", en: "No students found." },
  Students_ViewProfile: { fr: "Voir le profil", en: "View profile" },
  Students_BirthDate: { fr: "Date de naissance", en: "Date of birth" },
  Students_School: { fr: "École", en: "School" },
  Students_MainSubject: { fr: "Matière principale", en: "Main subject" },

  // Parents
  Parents_Title: { fr: "Parents", en: "Parents" },
  Parents_Add: { fr: "Ajouter un parent", en: "Add a parent" },
  Parents_Search: { fr: "Rechercher un parent…", en: "Search for a parent…" },
  Parents_None: { fr: "Aucun parent enregistré.", en: "No parents registered." },
  Parents_Children: { fr: "Enfants", en: "Children" },

  // Homework
  Homework_Title: { fr: "Devoirs", en: "Homework" },
  Homework_New: { fr: "Nouveau devoir", en: "New homework" },
  Homework_Search: { fr: "Chercher un devoir…", en: "Search homework…" },
  Homework_Due: { fr: "À rendre", en: "Due" },
  Homework_Submitted: { fr: "Rendu", en: "Submitted" },
  Homework_Graded: { fr: "Corrigé", en: "Graded" },
  Homework_Late: { fr: "En retard", en: "Late" },
  Homework_AllSubjects: { fr: "Toutes matières", en: "All subjects" },
  Homework_DueDate: { fr: "Date limite", en: "Due date" },

  // Reports
  Reports_Title: { fr: "Rapports de séance", en: "Session reports" },
  Reports_Write: { fr: "Rédiger un rapport", en: "Write a report" },
  Reports_New: { fr: "Nouveau rapport", en: "New report" },
  Reports_Back: { fr: "Retour", en: "Back" },
  Reports_SendToParents: { fr: "Envoyer aux parents", en: "Send to parents" },
  Reports_SaveDraft: { fr: "Sauvegarder brouillon", en: "Save draft" },
  Reports_Search: { fr: "Chercher un rapport…", en: "Search a report…" },

  // Documents
  Documents_Title: { fr: "Documents", en: "Documents" },
  Documents_NewFolder: { fr: "Nouveau dossier", en: "New folder" },
  Documents_Upload: { fr: "Téléverser", en: "Upload" },
  Documents_MyFolders: { fr: "Mes dossiers", en: "My folders" },
  Documents_MyDocs: { fr: "Mes Documents", en: "My Documents" },
  Documents_Search: { fr: "Rechercher…", en: "Search…" },
  Documents_Name: { fr: "Nom", en: "Name" },
  Documents_Type: { fr: "Type", en: "Type" },
  Documents_Size: { fr: "Taille", en: "Size" },
  Documents_Modified: { fr: "Modifié", en: "Modified" },

  // Messages
  Messages_Title: { fr: "Messagerie", en: "Messages" },
  Messages_New: { fr: "Nouveau message", en: "New message" },
  Messages_Write: { fr: "Écrire un message…", en: "Write a message…" },
  Messages_SelectConversation: { fr: "Sélectionnez une conversation", en: "Select a conversation" },

  // School / Profile
  School_Title: { fr: "Mon École", en: "My School" },
  School_ViewPublic: { fr: "Voir ma page publique", en: "View my public page" },
  Profile_Title: { fr: "Mon Profil", en: "My Profile" },
  Profile_Presentation: { fr: "Présentation", en: "Presentation" },
  Profile_Credentials: { fr: "Diplômes & Certifications", en: "Degrees & Certifications" },
  Profile_Subjects: { fr: "Matières", en: "Subjects" },
  Profile_Availability: { fr: "Disponibilités", en: "Availability" },
  Profile_Saved: { fr: "Profil mis à jour avec succès", en: "Profile updated successfully" },
};

function parseResx(file) {
  const xml = fs.readFileSync(file, "utf8");
  const map = {};
  const re = /<data name="([^"]+)"[^>]*>\s*<value>([\s\S]*?)<\/value>/g;
  let m;
  while ((m = re.exec(xml)) !== null) map[m[1]] = decode(m[2]);
  return map;
}

function decode(s) {
  return s.replace(/&lt;/g, "<").replace(/&gt;/g, ">").replace(/&amp;/g, "&").replace(/&quot;/g, '"').replace(/&apos;/g, "'");
}
function encode(s) {
  return String(s).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
}

function writeResx(file, map) {
  const keys = Object.keys(map).sort((a, b) => a.localeCompare(b));
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
  const body = keys
    .map((k) => `  <data name="${encode(k)}" xml:space="preserve">\n    <value>${encode(map[k])}</value>\n  </data>`)
    .join("\n");
  fs.writeFileSync(file, `${header}${body}\n</root>\n`, "utf8");
}

async function translate(text, tl) {
  if (!text) return text;
  if (/^TutorSphere$/i.test(text) || /^[\d\s.,%$€+\-/:#{}]+$/.test(text.trim())) return text;
  const placeholders = [];
  const protectedText = text.replace(/\{\d+\}/g, (m) => {
    placeholders.push(m);
    return `[[PH${placeholders.length - 1}]]`;
  });
  const url =
    "https://translate.googleapis.com/translate_a/single?client=gtx&sl=en&tl=" +
    encodeURIComponent(tl) +
    "&dt=t&q=" +
    encodeURIComponent(protectedText.slice(0, 900));
  try {
    const res = await fetch(url);
    if (!res.ok) return text;
    const data = await res.json();
    let out = Array.isArray(data?.[0]) ? data[0].map((p) => p?.[0] ?? "").join("") : text;
    placeholders.forEach((ph, i) => {
      out = out.replaceAll(`[[PH${i}]]`, ph);
    });
    return out || text;
  } catch {
    return text;
  }
}

function sleep(ms) {
  return new Promise((r) => setTimeout(r, ms));
}

const LOCALES = [
  { code: "base", file: "SharedResources.resx", lang: "fr" },
  { code: "fr", file: "SharedResources.fr.resx", lang: "fr" },
  { code: "en", file: "SharedResources.en.resx", lang: "en" },
  { code: "es", file: "SharedResources.es.resx", tl: "es" },
  { code: "de", file: "SharedResources.de.resx", tl: "de" },
  { code: "pt", file: "SharedResources.pt.resx", tl: "pt" },
  { code: "zh-Hans", file: "SharedResources.zh-Hans.resx", tl: "zh-CN" },
  { code: "ar", file: "SharedResources.ar.resx", tl: "ar" },
];

async function main() {
  const entries = Object.entries(KEYS);
  console.log(`Adding ${entries.length} keys…`);

  // Precompute translations for non fr/en
  const cache = { es: {}, de: {}, pt: {}, "zh-CN": {}, ar: {} };
  const toTranslate = entries.filter(([, v]) => v.en);
  let i = 0;
  for (const [key, v] of toTranslate) {
    for (const tl of Object.keys(cache)) {
      cache[tl][key] = await translate(v.en, tl);
      await sleep(15);
    }
    i++;
    if (i % 20 === 0) console.log(`  translated ${i}/${toTranslate.length}`);
  }

  for (const loc of LOCALES) {
    const file = path.join(__dirname, loc.file);
    const map = parseResx(file);
    let added = 0;
    for (const [key, v] of entries) {
      if (map[key] && String(map[key]).trim()) continue;
      if (loc.lang === "fr") map[key] = v.fr;
      else if (loc.lang === "en") map[key] = v.en;
      else map[key] = cache[loc.tl][key] || v.en;
      added++;
    }
    writeResx(file, map);
    console.log(`${loc.file}: +${added} (total ${Object.keys(map).length})`);
  }
  console.log("Done.");
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
