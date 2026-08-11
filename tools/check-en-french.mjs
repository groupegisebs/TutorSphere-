import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const resDir = path.join(__dirname, "../src/TutorSphere.Web/Resources");

function parseResx(file) {
  const t = fs.readFileSync(path.join(resDir, file), "utf8");
  const map = {};
  const re = /<data name="([^"]+)"[^>]*>\s*<value>([\s\S]*?)<\/value>/g;
  let m;
  while ((m = re.exec(t))) map[m[1]] = m[2];
  return map;
}

const base = parseResx("SharedResources.resx"); // FR default
const en = parseResx("SharedResources.en.resx");
const fr = parseResx("SharedResources.fr.resx");

const frMarkers =
  /\b(le|la|les|des|une|vous|votre|avec|pour|dans|être|êtes|cours|élève|élève|connexion|mot de passe|aucun|abonnement|devoir|rapport|enfant)\b/i;

let enLooksFrench = [];
for (const [k, v] of Object.entries(en)) {
  if (!v || v.length < 8) continue;
  if (fr[k] && fr[k] === v && frMarkers.test(v)) {
    enLooksFrench.push(`${k}: ${v.slice(0, 90)}`);
  } else if (frMarkers.test(v) && base[k] === v) {
    // en equals base french
    enLooksFrench.push(`${k}: ${v.slice(0, 90)}`);
  }
}
console.log("EN values that look French / equal FR:", enLooksFrench.length);
enLooksFrench.slice(0, 40).forEach((s) => console.log(" ", s));

// Also: base (default) should be FR; spot-check EN differs for key UI strings
const critical = [
  "Login",
  "Save",
  "Cancel",
  "Hero_Headline",
  "Expert_Approve",
  "Expert_Reject",
  "Expert_InviteTitle",
  "Nav_Dashboard",
  "Button_Continue",
  "Register_Title",
  "TutorRegister_Title",
];
console.log("\nCritical key check:");
for (const k of critical) {
  console.log(
    `  ${k}\n    base: ${(base[k] || "<missing>").slice(0, 60)}\n    fr:   ${(fr[k] || "<missing>").slice(0, 60)}\n    en:   ${(en[k] || "<missing>").slice(0, 60)}`
  );
}
