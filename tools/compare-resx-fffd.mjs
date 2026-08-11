import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";
import { execSync } from "child_process";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const resDir = path.join(__dirname, "../src/TutorSphere.Web/Resources");

function countFffd(buf) {
  let n = 0;
  for (let i = 0; i < buf.length - 2; i++) {
    if (buf[i] === 0xef && buf[i + 1] === 0xbf && buf[i + 2] === 0xbd) n++;
  }
  return n;
}

const langs = ["", "fr", "en", "es", "de", "pt", "zh-Hans", "ar"];
for (const lang of langs) {
  const name = lang ? `SharedResources.${lang}.resx` : "SharedResources.resx";
  const work = fs.readFileSync(path.join(resDir, name));
  let head;
  try {
    head = execSync(`git show HEAD:src/TutorSphere.Web/Resources/${name}`, {
      cwd: path.join(__dirname, ".."),
      maxBuffer: 20 * 1024 * 1024,
    });
  } catch {
    console.log(name, "NO HEAD");
    continue;
  }
  console.log(
    `${name.padEnd(40)} work_fffd=${countFffd(work)} head_fffd=${countFffd(head)} work=${work.length} head=${head.length}`
  );
}
