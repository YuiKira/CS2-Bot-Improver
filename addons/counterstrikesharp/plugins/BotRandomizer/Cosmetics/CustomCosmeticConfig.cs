using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace BotRandomizer;

internal sealed class BotRandomizerCustomConfig
{
    internal const string RandomStickerMode = "random";
    internal const string DisabledStickerMode = "off";
    internal const string CustomStickerMode = "custom";

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

    [JsonPropertyName("stickers_enabled")]
    public bool StickersEnabled { get; set; } = true;

    [JsonPropertyName("sticker_presets")]
    public Dictionary<ushort, List<StickerPresetSettings>> StickerPresets { get; set; } = [];

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
                    return NormalizeWeaponSettings(pair.Value, weapon, catalog);
                });
        config.StickerPresets = NormalizeStickerPresets(config.StickerPresets, catalog);
        return config;
    }

    private static Dictionary<int, WeaponSkinSettings> NormalizeWeaponSettings(
        Dictionary<int, WeaponSkinSettings>? settings,
        WeaponCatalogEntry weapon,
        CosmeticCatalog catalog)
    {
        var paints = weapon.Paints.ToDictionary(paint => paint.PaintKit);
        return (settings ?? [])
            .Where(pair => paints.ContainsKey(pair.Key) && pair.Value is not null)
            .ToDictionary(
                pair => pair.Key,
                pair => NormalizeWeaponSettings(
                    pair.Value,
                    paints[pair.Key].Legacy
                        ? weapon.LegacyStickerSchemaCount
                        : weapon.StickerSchemaCount,
                    catalog));
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
            StatTrakMax = Math.Clamp(settings.StatTrakMax, minimum, 999999),
            StickerMode = null,
            Stickers = null
        };
    }

    private static WeaponSkinSettings NormalizeWeaponSettings(
        WeaponSkinSettings settings,
        int schemaCount,
        CosmeticCatalog catalog)
    {
        var normalized = NormalizeSettings(settings);
        var mode = settings.StickerMode?.Trim().ToLowerInvariant();
        normalized.StickerMode = mode is DisabledStickerMode or CustomStickerMode
            ? mode
            : RandomStickerMode;
        normalized.Stickers = NormalizeStickerSlots(settings.Stickers, schemaCount, catalog);
        return normalized;
    }

    private static Dictionary<ushort, List<StickerPresetSettings>> NormalizeStickerPresets(
        Dictionary<ushort, List<StickerPresetSettings>>? presets,
        CosmeticCatalog catalog)
        => (presets ?? [])
            .Where(pair => catalog.TryGetWeapon(pair.Key, out _))
            .ToDictionary(
                pair => pair.Key,
                pair =>
                {
                    catalog.TryGetWeapon(pair.Key, out var weapon);
                    var slots = Math.Min(5, Math.Max(
                        weapon.StickerSchemaCount,
                        weapon.LegacyStickerSchemaCount));
                    var usedIds = new HashSet<string>(StringComparer.Ordinal);
                    return (pair.Value ?? [])
                        .Where(preset => preset is not null)
                        .Take(100)
                        .Select(preset =>
                        {
                            var id = string.IsNullOrWhiteSpace(preset.Id)
                                ? Guid.NewGuid().ToString("N")
                                : preset.Id.Trim()[..Math.Min(64, preset.Id.Trim().Length)];
                            while (!usedIds.Add(id))
                                id = Guid.NewGuid().ToString("N");
                            var name = string.IsNullOrWhiteSpace(preset.Name)
                                ? "未命名方案"
                                : preset.Name.Trim()[..Math.Min(60, preset.Name.Trim().Length)];
                            return new StickerPresetSettings
                            {
                                Id = id,
                                Name = name,
                                Stickers = NormalizeStickerSlots(preset.Stickers, slots, catalog)
                            };
                        })
                        .ToList();
                });

    private static List<StickerSlotSettings> NormalizeStickerSlots(
        List<StickerSlotSettings>? stickers,
        int schemaCount,
        CosmeticCatalog catalog)
    {
        var maximumSlots = Math.Min(5, Math.Max(0, schemaCount));
        return (stickers ?? [])
            .Where(sticker => sticker is not null
                && sticker.Slot >= 0
                && sticker.Slot < maximumSlots
                && catalog.ContainsSticker(sticker.DefIndex))
            .GroupBy(sticker => sticker.Slot)
            .Select(group => group.First())
            .OrderBy(sticker => sticker.Slot)
            .Select(sticker => new StickerSlotSettings
            {
                DefIndex = sticker.DefIndex,
                Slot = sticker.Slot,
                Wear = Math.Clamp(float.IsFinite(sticker.Wear) ? sticker.Wear : 0.0f, 0.0f, 1.0f),
                Rotation = NormalizeOptional(sticker.Rotation, -180.0f, 180.0f),
                X = NormalizeOptional(sticker.X, -1.0f, 1.0f),
                Y = NormalizeOptional(sticker.Y, -1.0f, 1.0f)
            })
            .ToList();
    }

    private static float? NormalizeOptional(float? value, float minimum, float maximum)
        => value is float number && float.IsFinite(number)
            ? Math.Clamp(number, minimum, maximum)
            : null;
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

    [JsonPropertyName("sticker_mode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StickerMode { get; set; }

    [JsonPropertyName("stickers")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<StickerSlotSettings>? Stickers { get; set; }
}

internal sealed class StickerSlotSettings
{
    [JsonPropertyName("def_index")]
    public uint DefIndex { get; set; }

    [JsonPropertyName("slot")]
    public int Slot { get; set; }

    [JsonPropertyName("wear")]
    public float Wear { get; set; }

    [JsonPropertyName("rotation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public float? Rotation { get; set; }

    [JsonPropertyName("x")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public float? X { get; set; }

    [JsonPropertyName("y")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public float? Y { get; set; }
}

internal sealed class StickerPresetSettings
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("stickers")]
    public List<StickerSlotSettings> Stickers { get; set; } = [];
}
