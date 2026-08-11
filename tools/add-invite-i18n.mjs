import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const resDir = path.join(__dirname, "../src/TutorSphere.Web/Resources");

const entries = {
  Expert_InvitesListTitle: {
    fr: "Invitations envoyées",
    en: "Sent invitations",
    es: "Invitaciones enviadas",
    de: "Gesendete Einladungen",
    pt: "Convites enviados",
    "zh-Hans": "已发送的邀请",
    ar: "الدعوات المرسلة",
  },
  Expert_InvitesListSubtitle: {
    fr: "Toutes les invitations du groupe et leur statut actuel.",
    en: "All invitations from your group and their current status.",
    es: "Todas las invitaciones del grupo y su estado actual.",
    de: "Alle Einladungen Ihrer Gruppe und deren aktueller Status.",
    pt: "Todos os convites do grupo e o respetivo estado atual.",
    "zh-Hans": "您所在专家组的全部邀请及其当前状态。",
    ar: "جميع دعوات مجموعتك وحالتها الحالية.",
  },
  Expert_InvitesEmpty: {
    fr: "Aucune invitation envoyée pour le moment.",
    en: "No invitations sent yet.",
    es: "Aún no se han enviado invitaciones.",
    de: "Noch keine Einladungen gesendet.",
    pt: "Ainda não foram enviados convites.",
    "zh-Hans": "尚未发送任何邀请。",
    ar: "لم تُرسل أي دعوات بعد.",
  },
  Expert_InviteColInvitedBy: {
    fr: "Envoyée par",
    en: "Sent by",
    es: "Enviada por",
    de: "Gesendet von",
    pt: "Enviado por",
    "zh-Hans": "发送人",
    ar: "أُرسلت بواسطة",
  },
  Expert_InviteColStatus: {
    fr: "Statut",
    en: "Status",
    es: "Estado",
    de: "Status",
    pt: "Estado",
    "zh-Hans": "状态",
    ar: "الحالة",
  },
  Expert_InviteStatus_Sent: {
    fr: "Envoyée",
    en: "Sent",
    es: "Enviada",
    de: "Gesendet",
    pt: "Enviado",
    "zh-Hans": "已发送",
    ar: "مُرسلة",
  },
  Expert_InviteStatus_Registered: {
    fr: "Inscrit — en revue",
    en: "Registered — under review",
    es: "Registrado — en revisión",
    de: "Registriert — in Prüfung",
    pt: "Registado — em análise",
    "zh-Hans": "已注册 — 审核中",
    ar: "مسجّل — قيد المراجعة",
  },
  Expert_InviteStatus_Approved: {
    fr: "Approuvé",
    en: "Approved",
    es: "Aprobado",
    de: "Genehmigt",
    pt: "Aprovado",
    "zh-Hans": "已批准",
    ar: "موافق عليه",
  },
  Expert_InviteStatus_Rejected: {
    fr: "Rejeté",
    en: "Rejected",
    es: "Rechazado",
    de: "Abgelehnt",
    pt: "Rejeitado",
    "zh-Hans": "已拒绝",
    ar: "مرفوض",
  },
  Expert_InviteStatus_Expired: {
    fr: "Expirée",
    en: "Expired",
    es: "Caducada",
    de: "Abgelaufen",
    pt: "Expirado",
    "zh-Hans": "已过期",
    ar: "منتهية",
  },
};

function parseResx(text) {
  const map = {};
  const re = /<data name="([^"]+)"[^>]*>\s*<value>([\s\S]*?)<\/value>/g;
  let m;
  while ((m = re.exec(text))) {
    map[m[1]] = m[2]
      .replace(/&lt;/g, "<")
      .replace(/&gt;/g, ">")
      .replace(/&amp;/g, "&");
  }
  return map;
}

function esc(v) {
  return String(v)
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;");
}

function buildResx(pairs) {
  const keys = Object.keys(pairs).sort((a, b) =>
    a.localeCompare(b, "en", { sensitivity: "base" })
  );
  const body = keys
    .map(
      (k) =>
        `  <data name="${k}" xml:space="preserve">\r\n    <value>${esc(pairs[k])}</value>\r\n  </data>`
    )
    .join("\r\n");
  return (
    `<?xml version="1.0" encoding="utf-8"?>\r\n` +
    `<root>\r\n` +
    `  <resheader name="resmimetype">\r\n` +
    `    <value>text/microsoft-resx</value>\r\n` +
    `  </resheader>\r\n` +
    `  <resheader name="version">\r\n` +
    `    <value>1.3</value>\r\n` +
    `  </resheader>\r\n` +
    `${body}\r\n` +
    `</root>\r\n`
  );
}

const files = {
  "": "SharedResources.resx",
  fr: "SharedResources.fr.resx",
  en: "SharedResources.en.resx",
  es: "SharedResources.es.resx",
  de: "SharedResources.de.resx",
  pt: "SharedResources.pt.resx",
  "zh-Hans": "SharedResources.zh-Hans.resx",
  ar: "SharedResources.ar.resx",
};

for (const [lang, file] of Object.entries(files)) {
  const p = path.join(resDir, file);
  const map = parseResx(fs.readFileSync(p, "utf8"));
  const locale = lang === "" ? "fr" : lang;
  for (const [key, tr] of Object.entries(entries)) {
    map[key] = tr[locale] ?? tr.en;
  }
  fs.writeFileSync(p, buildResx(map), "utf8");
  console.log("updated", file);
}
