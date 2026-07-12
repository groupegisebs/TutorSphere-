import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));

const KEYS = {
  Pricing_Faq_Title: { fr: "Questions fréquentes", en: "Frequently asked questions" },
  Pricing_Faq1_Q: { fr: "Puis-je annuler mon abonnement à tout moment ?", en: "Can I cancel my subscription at any time?" },
  Pricing_Faq1_A: { fr: "Oui. Vous pouvez annuler votre abonnement depuis les paramètres de votre compte. L'accès reste actif jusqu'à la fin de la période déjà payée.", en: "Yes. You can cancel from your account settings. Access remains active until the end of the paid period." },
  Pricing_Faq2_Q: { fr: "Qu'est-ce qui se passe après l'essai gratuit ?", en: "What happens after the free trial?" },
  Pricing_Faq2_A: { fr: "Après 14 jours d'essai, votre carte sera débitée automatiquement selon le forfait choisi. Aucun frais pendant l'essai.", en: "After 14 days, your card is charged automatically for the chosen plan. No fees during the trial." },
  Pricing_Faq3_Q: { fr: "Quel forfait choisir ?", en: "Which plan should I choose?" },
  Pricing_Faq3_A: { fr: "Le forfait Professional convient à la plupart des enseignants indépendants. Passez au Business si vous gérez plusieurs enseignants, ou contactez-nous pour Enterprise.", en: "Professional suits most independent teachers. Upgrade to Business if you manage multiple teachers, or contact us for Enterprise." },
  Pricing_Faq4_Q: { fr: "La facturation est-elle sécurisée ?", en: "Is billing secure?" },
  Pricing_Faq4_A: { fr: "Oui. Tous les paiements sont traités par Stripe, leader mondial du paiement en ligne. Nous ne stockons jamais vos informations bancaires.", en: "Yes. All payments are processed by Stripe. We never store your banking details." },
  Pricing_PlanActive: { fr: "Plan Actif", en: "Active plan" },
  Pricing_FreeTrialBadge: { fr: "Essai gratuit", en: "Free trial" },
  Pricing_SubscribeError: { fr: "Pour vous abonner, veuillez contacter le support ou créer un abonnement élève dans l'onglet Abonnements, puis accéder au paiement.", en: "To subscribe, please contact support or create a student subscription under Subscriptions, then proceed to payment." },
  Pricing_Val_Basic: { fr: "Basique", en: "Basic" },
  Pricing_Val_Full: { fr: "Complet", en: "Full" },
  Pricing_Val_Unlimited: { fr: "Illimité", en: "Unlimited" },
  Pricing_Val_Email: { fr: "Courriel", en: "Email" },
  Pricing_Val_Priority: { fr: "Prioritaire", en: "Priority" },
  Pricing_Val_Dedicated: { fr: "Dédié", en: "Dedicated" },
  Pricing_Val_Advanced: { fr: "Avancé", en: "Advanced" },
  Pricing_Val_5Gb: { fr: "5 Go", en: "5 GB" },
  Pricing_Val_25Gb: { fr: "25 Go", en: "25 GB" },
  Pricing_Val_100Gb: { fr: "100 Go", en: "100 GB" },
  Pricing_Val_5hMonth: { fr: "5 h/mois", en: "5 h/month" },
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
