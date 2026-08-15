import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));

const KEYS = {
  PublicProfile_ChooseOffer: {
    fr: "Choisir cette offre",
    en: "Choose this offer",
    es: "Elegir esta oferta",
    de: "Dieses Angebot wählen",
    pt: "Escolher esta oferta",
    zh: "选择此套餐",
    ar: "اختيار هذا العرض"
  },
  Search_GroupLogoFallback: {
    fr: "Logo du groupe",
    en: "Group logo",
    es: "Logo del grupo",
    de: "Gruppenlogo",
    pt: "Logótipo do grupo",
    zh: "小组标志",
    ar: "شعار المجموعة"
  }
};

function upsert(file, locale) {
  const loc = locale === "zh-Hans" ? "zh" : locale;
  let xml = fs.readFileSync(file, "utf8");
  let added = 0;
  for (const [key, map] of Object.entries(KEYS)) {
    if (xml.includes(`<data name="${key}"`)) continue;
    const value = map[loc] ?? map.fr;
    xml = xml.replace(
      "</root>",
      `  <data name="${key}" xml:space="preserve">\n    <value>${value}</value>\n  </data>\n</root>`
    );
    added++;
  }
  fs.writeFileSync(file, xml);
  console.log(path.basename(file), "added", added);
}

for (const [file, loc] of [
  ["SharedResources.resx", "fr"],
  ["SharedResources.fr.resx", "fr"],
  ["SharedResources.en.resx", "en"],
  ["SharedResources.es.resx", "es"],
  ["SharedResources.de.resx", "de"],
  ["SharedResources.pt.resx", "pt"],
  ["SharedResources.zh-Hans.resx", "zh"],
  ["SharedResources.ar.resx", "ar"]
]) {
  upsert(path.join(__dirname, file), loc);
}
