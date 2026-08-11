import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const resDir = path.join(__dirname, "../src/TutorSphere.Web/Resources");

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

const translations = {
  en: {
    ParentDashboard_NoHomework: "No homework pending.",
    ParentDashboard_NoReports: "No reports available.",
    ParentDashboard_NoSubscription: "No active subscription",
    ParentDashboard_ProgressSummary: "{0} child(ren) — family average: {1}",
  },
  es: {
    ParentDashboard_NoHomework: "No hay deberes pendientes.",
    ParentDashboard_NoReports: "No hay informes disponibles.",
    ParentDashboard_NoSubscription: "Sin suscripción activa",
    ParentDashboard_ProgressSummary: "{0} niño(s) — media familiar: {1}",
  },
  de: {
    ParentDashboard_NoHomework: "Keine ausstehenden Hausaufgaben.",
    ParentDashboard_NoReports: "Keine Berichte verfügbar.",
    ParentDashboard_NoSubscription: "Kein aktives Abonnement",
    ParentDashboard_ProgressSummary: "{0} Kind(er) — Familiendurchschnitt: {1}",
  },
  pt: {
    ParentDashboard_NoHomework: "Nenhum dever de casa pendente.",
    ParentDashboard_NoReports: "Nenhum relatório disponível.",
    ParentDashboard_NoSubscription: "Nenhuma assinatura ativa",
    ParentDashboard_ProgressSummary: "{0} filho(s) — média familiar: {1}",
  },
  "zh-Hans": {
    ParentDashboard_NoHomework: "暂无待完成作业。",
    ParentDashboard_NoReports: "暂无可用报告。",
    ParentDashboard_NoSubscription: "无有效订阅",
    ParentDashboard_ProgressSummary: "{0} 名孩子 — 家庭平均：{1}",
  },
  ar: {
    ParentDashboard_NoHomework: "لا واجبات معلقة.",
    ParentDashboard_NoReports: "لا تقارير متاحة.",
    ParentDashboard_NoSubscription: "لا اشتراك نشط",
    ParentDashboard_ProgressSummary: "{0} طفل/أطفال — متوسط العائلة: {1}",
  },
};

for (const [lang, pairs] of Object.entries(translations)) {
  const file = path.join(resDir, `SharedResources.${lang}.resx`);
  const map = parseResx(fs.readFileSync(file, "utf8"));
  Object.assign(map, pairs);
  fs.writeFileSync(file, buildResx(map), "utf8");
  console.log("Updated", lang);
}

// Also fix ParentDashboard_PayOnline capitalization in es if lowercase
const esFile = path.join(resDir, "SharedResources.es.resx");
const es = parseResx(fs.readFileSync(esFile, "utf8"));
if (es.ParentDashboard_PayOnline === "pagar en línea") {
  es.ParentDashboard_PayOnline = "Pagar en línea";
  fs.writeFileSync(esFile, buildResx(es), "utf8");
  console.log("Fixed es ParentDashboard_PayOnline casing");
}
