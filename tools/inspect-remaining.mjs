import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const resDir = path.join(__dirname, "../src/TutorSphere.Web/Resources");

function getVal(file, key) {
  const t = fs.readFileSync(path.join(resDir, file), "utf8");
  const re = new RegExp(
    `<data name="${key}"[^>]*>\\s*<value>([\\s\\S]*?)<\\/value>`
  );
  const m = t.match(re);
  return m ? m[1] : null;
}

const v = getVal("SharedResources.pt.resx", "Parent_LastReportText");
console.log("value:", v);
console.log(
  "codes:",
  [...v].slice(0, 8).map((c) => "U+" + c.codePointAt(0).toString(16).toUpperCase())
);
console.log("hex utf8:", Buffer.from(v, "utf8").slice(0, 40).toString("hex"));

// Check a few keys that might have been wrongly "fixed" in ar/zh/es
for (const [lang, key] of [
  ["ar", "About"],
  ["ar", "Hero_Headline"],
  ["zh-Hans", "About"],
  ["zh-Hans", "Hero_Headline"],
  ["es", "About"],
]) {
  console.log(lang, key, "=>", getVal(`SharedResources.${lang}.resx`, key)?.slice(0, 80));
}
