import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));

const KEYS = {
  ParentSubs_PageTitle: { fr: "Mes Abonnements – TutorSphere", en: "My Subscriptions – TutorSphere" },
  ParentSubs_Subtitle: { fr: "Gérez les abonnements de vos enfants", en: "Manage your children's subscriptions" },
  ParentSubs_Loading: { fr: "Chargement des abonnements…", en: "Loading subscriptions…" },
  ParentSubs_TabPending: { fr: "Paiement en attente", en: "Pending payment" },
  ParentSubs_TabActive: { fr: "Actifs", en: "Active" },
  ParentSubs_TabCancelled: { fr: "Résiliés", en: "Cancelled" },
  ParentSubs_TabHistory: { fr: "Historique", en: "History" },
  ParentSubs_ColOffer: { fr: "Offre", en: "Offer" },
  ParentSubs_ColPeriod: { fr: "Période", en: "Period" },
  ParentSubs_ColSessions: { fr: "Séances", en: "Sessions" },
  ParentSubs_Cancel: { fr: "Résilier", en: "Cancel" },
  ParentSubs_Pay: { fr: "Payer", en: "Pay" },
  ParentSubs_StatusPendingPayment: { fr: "En attente de paiement", en: "Awaiting payment" },
  ParentSubs_StatusPaused: { fr: "En pause", en: "Paused" },
  ParentSubs_StatusExpired: { fr: "Expiré", en: "Expired" },
  ParentSubs_EmptyPending: { fr: "Aucun paiement en attente.", en: "No pending payments." },
  ParentSubs_EmptyActive: { fr: "Aucun abonnement actif.", en: "No active subscriptions." },
  ParentSubs_EmptyCancelled: { fr: "Aucun abonnement résilié.", en: "No cancelled subscriptions." },
  ParentSubs_EmptyHistory: { fr: "Aucun abonnement dans l'historique.", en: "No subscriptions in history." },
  ParentSubs_BannerSaved: { fr: "Abonnement enregistré. Vous pouvez suivre son statut ci-dessous.", en: "Subscription saved. You can track its status below." },
  ParentSubs_BannerCanceled: { fr: "Paiement annulé. Vous pouvez réessayer quand vous voulez.", en: "Payment cancelled. You can try again anytime." },
  ParentSubs_LoadError: { fr: "Impossible de charger les abonnements : {0}", en: "Unable to load subscriptions: {0}" },
  ParentSubs_PayUnavailable: { fr: "Paiement indisponible pour le moment.", en: "Payment unavailable at the moment." },
};

function parseResx(file) {
  const xml = fs.readFileSync(file, "utf8");
  const map = {};
  const re = /<data name="([^"]+)"[^>]*>\s*<value>([\s\S]*?)<\/value>/g;
  let m;
  while ((m = re.exec(xml))) map[m[1]] = m[2]
    .replace(/&lt;/g, "<").replace(/&gt;/g, ">").replace(/&amp;/g, "&").replace(/&quot;/g, '"').replace(/&apos;/g, "'");
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
      await sleep(25);
    }
  }
  for (const loc of LOCALES) {
    const file = path.join(__dirname, loc.file);
    const map = parseResx(file);
    for (const [k, v] of Object.entries(KEYS)) {
      map[k] = loc.lang === "fr" ? v.fr : loc.lang === "en" ? v.en : cache[k][loc.tl];
    }
    writeResx(file, map);
    console.log("updated", loc.file);
  }
}
main();
