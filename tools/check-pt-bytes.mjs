import { execSync } from "child_process";
import path from "path";
import { fileURLToPath } from "url";
import fs from "fs";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.join(__dirname, "..");

function showBytes(label, text, key) {
  const re = new RegExp(
    `<data name="${key}"[^>]*>\\s*<value>([\\s\\S]*?)<\\/value>`
  );
  const m = text.match(re);
  if (!m) {
    console.log(label, key, "MISSING");
    return;
  }
  const v = m[1];
  const buf = Buffer.from(v, "utf8");
  console.log(label, key, "=>", v.slice(0, 80));
  console.log(
    "  hex:",
    [...buf.slice(0, 40)].map((b) => b.toString(16).padStart(2, "0")).join(" ")
  );
  // true double encode: C3 83 (Ã) then C2 xx
  let doubles = 0;
  for (let i = 0; i < buf.length - 1; i++) {
    if (buf[i] === 0xc3 && buf[i + 1] === 0x83) doubles++;
  }
  console.log("  C383 count", doubles);
}

const head = execSync(
  "git show HEAD:src/TutorSphere.Web/Resources/SharedResources.pt.resx",
  { cwd: root, maxBuffer: 20e6, encoding: "buffer" }
);
const headText = head.toString("utf8");
const work = fs.readFileSync(
  path.join(root, "src/TutorSphere.Web/Resources/SharedResources.pt.resx")
);
const workText = work.toString("utf8");

for (const k of [
  "AdminDashboard_Distribution",
  "Billing_PerSession",
  "Activate_F1",
  "Hero_Headline",
]) {
  showBytes("HEAD", headText, k);
  showBytes("WORK", workText, k);
}
