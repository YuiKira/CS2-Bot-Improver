const fs = require("fs");
const path = require("path");

const [englishPath, chinesePath, outputPath = "BotStickerCatalog.zh-CN.json"] = process.argv.slice(2);
if (!englishPath || !chinesePath) {
  throw new Error(
    "Usage: node scripts/generate-sticker-catalog.js <stickers-en.json> <stickers-zh-CN.json> [output.json]"
  );
}

const root = path.resolve(__dirname, "..");
const runtimeCatalog = JSON.parse(fs.readFileSync(
  path.join(root, "addons/counterstrikesharp/plugins/BotRandomizer/cosmetic_catalog.json"),
  "utf8"
));
const english = JSON.parse(fs.readFileSync(path.resolve(englishPath), "utf8"));
const chinese = JSON.parse(fs.readFileSync(path.resolve(chinesePath), "utf8"));
const englishById = new Map(english.map(sticker => [Number(sticker.def_index), sticker]));
const chineseById = new Map(chinese.map(sticker => [Number(sticker.def_index), sticker]));

function cleanChinese(value) {
  return String(value || "")
    .replace(/^印花\s*\|\s*/, "")
    .replace(/闪耀/g, "闪亮")
    .trim();
}

function cleanChinesePart(value) {
  return String(value || "")
    .trim()
    .replace(/^(\d{4})年\s*/, (_, year) => `${year} `)
    .replace(/锦标赛/g, "")
    .replace(/\s+/g, " ")
    .trim();
}

function displayChinese(value) {
  const parts = cleanChinese(value).split("|").map(cleanChinesePart).filter(Boolean);
  if (parts.length < 2) return parts[0] || "未知印花";
  return `${parts.at(-1)} | ${parts.slice(0, -1).join(" | ")}`;
}

function displayEnglish(value) {
  const parts = String(value || "")
    .replace(/^Sticker\s*\|\s*/, "")
    .split("|")
    .map(part => part.trim())
    .filter(Boolean);
  if (parts.length < 2) return parts[0] || "Unknown Sticker";
  return `${parts.at(-1)} | ${parts.slice(0, -1).join(" | ")}`;
}

const stickers = runtimeCatalog.stickerKits
  .map(runtimeSticker => {
    const id = Number(runtimeSticker.defIndex);
    const en = englishById.get(id);
    const zh = chineseById.get(id);
    return {
      id,
      name: zh ? displayChinese(zh.name) : `印花 #${id}`,
      english: en ? displayEnglish(en.name) : `Sticker #${id}`,
      image: zh?.image || en?.image || null,
      finish: en?.effect && en.effect !== "Other"
        ? en.effect.toLowerCase()
        : runtimeSticker.finish,
      type: en?.type || "Other"
    };
  })
  .sort((left, right) => left.id - right.id);

const weaponSlots = Object.fromEntries(runtimeCatalog.weapons.map(weapon => [
  String(weapon.defIndex),
  {
    standard: Math.min(5, weapon.stickerSchemaCount),
    legacy: Math.min(5, weapon.legacyStickerSchemaCount),
    legacyPaints: weapon.paints.filter(paint => paint.legacy).map(paint => paint.paintKit)
  }
]));

const destination = path.resolve(outputPath);
fs.writeFileSync(destination, `${JSON.stringify({weaponSlots, stickers})}\n`, "utf8");
const missingImages = stickers.filter(sticker => !sticker.image).length;
console.log(`Wrote ${stickers.length} stickers to ${destination}; ${missingImages} have no image.`);
