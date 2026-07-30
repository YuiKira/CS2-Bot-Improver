using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace BotRandomizer;

internal sealed class BotRandomizerCustomConfig
{
    [JsonPropertyName("knife_def_indexes")]
    public List<ushort> KnifeDefIndexes { get; set; } = [507, 515];

    [JsonPropertyName("knife_paint_kits")]
    public List<int> KnifePaintKits { get; set; } = [415, 416];

    [JsonPropertyName("knife_skin_settings")]
    public Dictionary<int, WeaponSkinSettings> KnifeSkinSettings { get; set; } = [];

    [JsonPropertyName("knife_paint_kits_by_def_index")]
    public Dictionary<ushort, List<int>> KnifePaintKitsByDefIndex { get; set; } = [];

    [JsonPropertyName("knife_skin_settings_by_def_index")]
    public Dictionary<ushort, Dictionary<int, WeaponSkinSettings>> KnifeSkinSettingsByDefIndex { get; set; } = [];

    [JsonPropertyName("weapon_paint_kits")]
    public Dictionary<ushort, List<int>> WeaponPaintKits { get; set; } = [];

    [JsonPropertyName("weapon_skin_settings")]
    public Dictionary<ushort, Dictionary<int, WeaponSkinSettings>> WeaponSkinSettings { get; set; } = [];

    internal static BotRandomizerCustomConfig Load(
        string path,
        CosmeticCatalog catalog,
        ILogger logger)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        try
        {
            var config = File.Exists(path)
                ? JsonSerializer.Deserialize<BotRandomizerCustomConfig>(File.ReadAllText(path), options)
                : null;
            config = Normalize(config ?? new BotRandomizerCustomConfig(), catalog);
            File.WriteAllText(path, JsonSerializer.Serialize(config, options));
            return config;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[BotRandomizer] Failed to load BotRandomizer.custom.json; defaults are active");
            return Normalize(new BotRandomizerCustomConfig(), catalog);
        }
    }

    internal WeaponSkinSettings? GetKnifeSettings(ushort defIndex, int paintKit)
    {
        if (KnifeSkinSettingsByDefIndex.TryGetValue(defIndex, out var byPaint)
            && byPaint.TryGetValue(paintKit, out var configured))
        {
            return configured;
        }
        return KnifeSkinSettings.GetValueOrDefault(paintKit);
    }

    internal WeaponSkinSettings? GetWeaponSettings(ushort defIndex, int paintKit)
        => WeaponSkinSettings.TryGetValue(defIndex, out var byPaint)
            ? byPaint.GetValueOrDefault(paintKit)
            : null;

    private static BotRandomizerCustomConfig Normalize(
        BotRandomizerCustomConfig config,
        CosmeticCatalog catalog)
    {
        var validKnifeDefs = catalog.KnifeDefIndexes.ToHashSet();
        config.KnifeDefIndexes = (config.KnifeDefIndexes ?? [])
            .Where(validKnifeDefs.Contains)
            .Distinct()
            .Order()
            .ToList();
        if (config.KnifeDefIndexes.Count == 0)
            config.KnifeDefIndexes = [507, 515];

        var allKnifePaints = catalog.KnifeDefIndexes
            .SelectMany(defIndex => catalog.TryGetKnifePaints(defIndex, out var paints)
                ? paints.Select(paint => paint.PaintKit)
                : [])
            .ToHashSet();
        config.KnifePaintKits = (config.KnifePaintKits ?? [])
            .Where(allKnifePaints.Contains)
            .Distinct()
            .Order()
            .ToList();

        config.KnifePaintKitsByDefIndex = (config.KnifePaintKitsByDefIndex ?? [])
            .Where(pair => catalog.TryGetKnifePaints(pair.Key, out _))
            .ToDictionary(
                pair => pair.Key,
                pair =>
                {
                    catalog.TryGetKnifePaints(pair.Key, out var paints);
                    var available = paints.Select(paint => paint.PaintKit).ToHashSet();
                    return (pair.Value ?? []).Where(available.Contains).Distinct().Order().ToList();
                });

        config.KnifeSkinSettings = NormalizeSettings(config.KnifeSkinSettings, allKnifePaints);
        config.KnifeSkinSettingsByDefIndex = (config.KnifeSkinSettingsByDefIndex ?? [])
            .Where(pair => catalog.TryGetKnifePaints(pair.Key, out _))
            .ToDictionary(
                pair => pair.Key,
                pair =>
                {
                    catalog.TryGetKnifePaints(pair.Key, out var paints);
                    return NormalizeSettings(pair.Value, paints.Select(paint => paint.PaintKit).ToHashSet());
                });

        config.WeaponPaintKits = (config.WeaponPaintKits ?? [])
            .Where(pair => catalog.TryGetWeapon(pair.Key, out _))
            .ToDictionary(
                pair => pair.Key,
                pair =>
                {
                    catalog.TryGetWeapon(pair.Key, out var weapon);
                    var available = weapon.Paints.Select(paint => paint.PaintKit).ToHashSet();
                    return (pair.Value ?? []).Where(available.Contains).Distinct().Order().ToList();
                });

        config.WeaponSkinSettings = (config.WeaponSkinSettings ?? [])
            .Where(pair => catalog.TryGetWeapon(pair.Key, out _))
            .ToDictionary(
                pair => pair.Key,
                pair =>
                {
                    catalog.TryGetWeapon(pair.Key, out var weapon);
                    return NormalizeSettings(pair.Value, weapon.Paints.Select(paint => paint.PaintKit).ToHashSet());
                });
        return config;
    }

    private static Dictionary<int, WeaponSkinSettings> NormalizeSettings(
        Dictionary<int, WeaponSkinSettings>? settings,
        HashSet<int> availablePaints)
        => (settings ?? [])
            .Where(pair => availablePaints.Contains(pair.Key) && pair.Value is not null)
            .ToDictionary(pair => pair.Key, pair => NormalizeSettings(pair.Value));

    private static WeaponSkinSettings NormalizeSettings(WeaponSkinSettings settings)
    {
        var minimum = Math.Clamp(settings.StatTrakMin, 0, 999999);
        return new WeaponSkinSettings
        {
            Wear = Math.Clamp(float.IsFinite(settings.Wear) ? settings.Wear : 0.01f, 0.000001f, 1.0f),
            Seed = Math.Clamp(settings.Seed, 0, 1000),
            StatTrak = settings.StatTrak,
            StatTrakMin = minimum,
            StatTrakMax = Math.Clamp(settings.StatTrakMax, minimum, 999999)
        };
    }
}

internal sealed class WeaponSkinSettings
{
    [JsonPropertyName("wear")]
    public float Wear { get; set; } = 0.01f;

    [JsonPropertyName("seed")]
    public int Seed { get; set; }

    [JsonPropertyName("stattrak")]
    public bool StatTrak { get; set; }

    [JsonPropertyName("stattrak_min")]
    public int StatTrakMin { get; set; }

    [JsonPropertyName("stattrak_max")]
    public int StatTrakMax { get; set; } = 99999;
}
