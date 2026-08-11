import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const resDir = path.join(__dirname, "../src/TutorSphere.Web/Resources");
const t = fs.readFileSync(path.join(resDir, "SharedResources.pt.resx"), "utf8");
const re = /<data name="([^"]+)"[^>]*>\s*<value>([\s\S]*?)<\/value>/g;
let m;
const fffd = [];
let good = 0;
let total = 0;
while ((m = re.exec(t))) {
  total++;
  if (m[2].includes("\uFFFD")) fffd.push(m[1]);
  else good++;
}
console.log({ total, good, fffd: fffd.length });
fs.writeFileSync(path.join(resDir, "_pt-fffd-keys.json"), JSON.stringify(fffd, null, 2));
console.log(fffd.slice(0, 40).join("\n"));
