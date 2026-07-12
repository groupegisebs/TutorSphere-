import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const dir = path.dirname(fileURLToPath(import.meta.url));

function parse(file) {
  const xml = fs.readFileSync(file, "utf8");
  const map = {};
  const re = /<data name="([^"]+)"[^>]*>\s*<value>([\s\S]*?)<\/value>/g;
  let m;
  while ((m = re.exec(xml)) !== null) map[m[1]] = m[2];
  return map;
}

const base = parse(path.join(dir, "SharedResources.resx"));
const baseKeys = Object.keys(base);
console.log("base", baseKeys.length);

for (const loc of ["fr", "en", "es", "de", "pt", "zh-Hans", "ar"]) {
  const file = path.join(dir, `SharedResources.${loc}.resx`);
  const map = parse(file);
  const missing = baseKeys.filter((k) => !map[k]);
  console.log(`${loc}: ${Object.keys(map).length} keys, missing=${missing.length}`);
}

const sample = ["Nav_Dashboard", "Nav_MyStudents", "Button_Search", "PublicProfile_Subscribe", "Lang_Arabic"];
for (const loc of ["fr", "en", "es", "de", "pt", "zh-Hans", "ar"]) {
  const map = parse(path.join(dir, `SharedResources.${loc}.resx`));
  console.log("==", loc);
  for (const k of sample) console.log(" ", k, ":", map[k]);
}
