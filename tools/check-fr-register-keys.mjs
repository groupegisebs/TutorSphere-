import fs from "fs";
import path from "path";

const resDir = "src/TutorSphere.Web/Resources";
function v(file, key) {
  const t = fs.readFileSync(path.join(resDir, file), "utf8");
  const re = new RegExp(
    `<data name="${key}"[^>]*>\\s*<value>([\\s\\S]*?)<\\/value>`
  );
  const m = t.match(re);
  return m ? m[1] : null;
}

const keys = [
  "TutorRegister_Title",
  "TutorRegister_Subtitle",
  "TutorRegister_InviteOnlyHint",
  "Register_Step1",
  "Register_Step2",
  "Register_Step3",
  "Benefits_Title",
  "Label_SchoolName",
  "Label_FirstName",
  "Button_CreateAccount",
  "Language_Select",
];

for (const k of keys) {
  console.log("\n" + k);
  for (const f of [
    "SharedResources.resx",
    "SharedResources.fr.resx",
    "SharedResources.en.resx",
  ]) {
    const val = v(f, k);
    console.log(" ", f, "=>", (val || "<missing>").slice(0, 100));
  }
}
