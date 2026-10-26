using BotRandomizer;
using System.Reflection;
using System.Collections;
using Microsoft.Extensions.Logging.Abstractions;

var root = Path.GetFullPath(args[0]);
var catalog = CosmeticCatalog.Load(Path.Combine(root, "cosmetic_catalog.json"));
var charms = CharmPlacementCatalog.Load(Path.Combine(root, "charm_placements.json"), catalog);
var count = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); count++; }
var weapon = catalog.Weapons.First(w => w.Paints.Count > 0);
var paint = weapon.Paints[0];
var knifeDef = catalog.KnifeDefIndexes.First();
Check(catalog.TryGetKnifePaints(knifeDef, out var knives), "knife catalog");
var knifePaint = knives[0];
var config = new BotRandomizerCustomConfig {
    KnifeDefIndexes = [knifeDef], KnifePaintKits = [knifePaint.PaintKit],
    WeaponPaintKits = new() { [weapon.DefIndex] = [paint.PaintKit] },
    WeaponSkinSettings = new() { [weapon.DefIndex] = new() { [paint.PaintKit] = new() {
        Wear = paint.WearMin, Seed = 313, StatTrak = true, StatTrakMin = 17, StatTrakMax = 17,
        StickerMode = "off" } } },
    KnifeSkinSettings = new() { [knifePaint.PaintKit] = new() { Wear = knifePaint.WearMin, Seed = 321 } }
};
var roller = new CosmeticRoller(catalog, charms, config, new Random(147));
foreach (byte team in new byte[] { 2, 3 }) {
    for (int i = 0; i < 25; i++) {
        var loadout = roller.RollLoadout(team, catalog.MusicKits[0]);
        var agents = team == 3 ? RandomizerAssets.CounterTerroristAgents : RandomizerAssets.TerroristAgents;
        Check(agents.Any(a => a.DefIndex == loadout.AgentDefIndex && a.ModelPath == loadout.AgentModel), "agent model/intro match");
        Check(loadout.Knife.DefIndex == knifeDef && loadout.Knife.PaintKit == knifePaint.PaintKit && loadout.Knife.Seed == 321, "custom knife preserved");
        var selected = roller.GetOrCreateWeapon(loadout, weapon.DefIndex)!;
        Check(selected.PaintKit == paint.PaintKit && selected.Seed == 313 && selected.Wear == paint.WearMin, "custom paint/seed/wear preserved");
        Check(selected.StatTrak && selected.StatTrakValue == 17 && selected.Stickers.Count == 0, "StatTrak and sticker controls preserved");
        Check(ReferenceEquals(selected, roller.GetOrCreateWeapon(loadout, weapon.DefIndex)), "stable loadout reuse");
    }
}
var randomConfig = new BotRandomizerCustomConfig { KnifeDefIndexes = [], KnifePaintKits = [] };
var randomRoller = new CosmeticRoller(catalog, charms, randomConfig, new Random(134));
for (int i = 0; i < 30; i++) {
    var loadout = randomRoller.RollLoadout(3);
    randomRoller.GetOrCreateWeapon(loadout, weapon.DefIndex);
}
var pool = (IDictionary)typeof(CosmeticRoller).GetField("_weaponRarityPools", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(randomRoller)!;
Check(pool.Count == 1, "immutable weapon rarity pool cached once");
var knifePool = (IDictionary)typeof(CosmeticRoller).GetField("_knifeFinishPools", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(randomRoller)!;
Check(knifePool.Count > 0 && knifePool.Count <= RandomizerAssets.Knives.Length, "knife pools cached by catalog");
Console.WriteLine($"PASS: {count} cosmetic merge checks; custom choices, seed/wear, StatTrak, sticker controls, agent matching and pool caching.");
