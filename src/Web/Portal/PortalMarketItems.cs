// <copyright file="PortalMarketItems.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Portal;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.Interfaces;

/// <summary>
/// How the market names, groups and shows the items: their categories, the items which are really several
/// different items (like the four Horns of Fenrir or the boxes of the Box of Luck), and the excellent options.
/// </summary>
public static class PortalMarketItems
{
    /// <summary>
    /// The categories, in the order in which the market shows them.
    /// </summary>
    public static readonly IReadOnlyList<string> Categories =
    [
        Weapons, Shields, Sets, Wings, Pets, Jewelry, Jewels, Sockets, Skills, Materials, EventEntries, Boxes, Consumables, Others,
    ];

    /// <summary>
    /// The excellent options of weapons and pendants, as the market names them.
    /// </summary>
    public static readonly IReadOnlyList<string> AttackExcellentOptions =
    [
        "Daño excelente +10%", "Daño +nivel/20", "Daño +2%", "Velocidad de ataque +7", "Vida al matar", "Maná al matar",
    ];

    /// <summary>
    /// The excellent options of armors, shields and rings, as the market names them.
    /// </summary>
    public static readonly IReadOnlyList<string> DefenseExcellentOptions =
    [
        "Vida máx. +4%", "Maná máx. +4%", "Reduce daño 4%", "Refleja daño 5%", "Tasa de defensa +10%", "Zen +40%",
    ];

    /// <summary>
    /// The prefix of an option line which is an excellent option.
    /// </summary>
    public const string ExcellentPrefix = "Exc: ";

    private const string Weapons = "Armas";
    private const string Shields = "Escudos";
    private const string Sets = "Sets";
    private const string Wings = "Alas";
    private const string Pets = "Mascotas";
    private const string Jewelry = "Anillos y collares";
    private const string Jewels = "Joyas";
    private const string Sockets = "Sockets";
    private const string Skills = "Orbes y pergaminos";
    private const string Materials = "Materiales";
    private const string EventEntries = "Entradas a eventos";
    private const string Boxes = "Cajas";
    private const string Consumables = "Consumibles";
    private const string Others = "Otros";

    /// <summary>
    /// The excellent options by the designation of their attribute; physical and wizardry options have the same name.
    /// </summary>
    private static readonly Dictionary<string, string> ExcellentNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Excellent Damage Chance"] = "Daño excelente +10%",
        ["Physical Base Damage (min and max)"] = "Daño +nivel/20",
        ["Wizardry Base Damage (min and max)"] = "Daño +nivel/20",
        ["Physical Base Damage Increase"] = "Daño +2%",
        ["Wizardry Base Damage Increase"] = "Daño +2%",
        ["Attack Speed Any"] = "Velocidad de ataque +7",
        ["Health recover after Monster kill, multiplier of max health"] = "Vida al matar",
        ["Mana recover after Monster kill, multiplier of max mana"] = "Maná al matar",
        ["Maximum Health"] = "Vida máx. +4%",
        ["Maximum Mana"] = "Maná máx. +4%",
        ["Armor Damage Decrease"] = "Reduce daño 4%",
        ["Damage Reflection"] = "Refleja daño 5%",
        ["Defense Rate (PvM)"] = "Tasa de defensa +10%",
        ["Money Drop Amount Rate"] = "Zen +40%",
    };

    /// <summary>
    /// The items which are different items by their level, like the boxes of the Box of Luck.
    /// </summary>
    private static readonly Dictionary<(byte Group, short Number), string[]> LevelVariants = CreateLevelVariants();

    /// <summary>
    /// The four Horns of Fenrir, by the suffix of their key.
    /// </summary>
    private static readonly (string Suffix, string Name, ItemOptionType? Option)[] Fenrirs =
    [
        ("rojo", "Fenrir rojo", null),
        ("negro", "Fenrir negro (Destroy)", ItemOptionTypes.BlackFenrir),
        ("azul", "Fenrir azul (Protect)", ItemOptionTypes.BlueFenrir),
        ("dorado", "Fenrir dorado (Illusion)", ItemOptionTypes.GoldFenrir),
    ];

    /// <summary>
    /// Gets the key of an item definition, "group-number".
    /// </summary>
    /// <param name="group">The group.</param>
    /// <param name="number">The number.</param>
    /// <returns>The key.</returns>
    public static string KeyOf(byte group, short number) => $"{group}-{number}";

    /// <summary>
    /// Gets the different items of an item definition: the definition itself, or its variants.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <param name="number">The number.</param>
    /// <param name="name">The name of the definition.</param>
    /// <returns>The key, the name and the item level for the picture of each item.</returns>
    public static IEnumerable<(string Key, string Name, byte ImageLevel)> VariantsOf(byte group, short number, string name)
    {
        var key = KeyOf(group, number);
        if (IsFenrir(group, number))
        {
            return Fenrirs.Select(f => ($"{key}-{f.Suffix}", f.Name, (byte)0));
        }

        if (LevelVariants.TryGetValue((group, number), out var names))
        {
            return names.Select((n, level) => ($"{key}-{level}", n, (byte)level));
        }

        return [(key, name.Split(';')[0], (byte)0)];
    }

    /// <summary>
    /// Gets the key, the name and the level which the market keeps of an item. The level of an item which is a
    /// different item by its level is 0, because its name says it.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>The key, the name and the level.</returns>
    public static (string Key, string Name, byte Level) Identify(Item item)
    {
        var definition = item.Definition!;
        var key = KeyOf(definition.Group, definition.Number);
        if (IsFenrir(definition.Group, definition.Number))
        {
            var types = item.ItemOptions.Select(o => o.ItemOption?.OptionType).ToList();
            var fenrir = Fenrirs.LastOrDefault(f => f.Option is not null && types.Contains(f.Option));
            if (fenrir.Name is null)
            {
                fenrir = Fenrirs[0];
            }

            return ($"{key}-{fenrir.Suffix}", fenrir.Name, 0);
        }

        if (LevelVariants.TryGetValue((definition.Group, definition.Number), out var names) && item.Level < names.Length)
        {
            return ($"{key}-{item.Level}", names[item.Level], 0);
        }

        return (key, (new LocalizedString(definition.Name).ValueInNeutralLanguage ?? string.Empty).Split(';')[0], item.Level);
    }

    /// <summary>
    /// Gets the key of the item of an order or a trade, by its definition and the name which the market gave it.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <param name="number">The number.</param>
    /// <param name="itemName">The name of the item.</param>
    /// <param name="level">The level which the market kept; orders from before the variants kept the level of the variant.</param>
    /// <returns>The key.</returns>
    public static string KeyOf(byte group, short number, string itemName, byte level = 0)
    {
        if (IsFenrir(group, number))
        {
            var fenrir = Fenrirs.FirstOrDefault(f => f.Name == itemName);
            return $"{KeyOf(group, number)}-{fenrir.Suffix ?? Fenrirs[0].Suffix}";
        }

        if (LevelVariants.TryGetValue((group, number), out var names))
        {
            var variant = Array.IndexOf(names, itemName);
            if (variant < 0)
            {
                variant = level < names.Length ? level : 0;
            }

            return $"{KeyOf(group, number)}-{variant}";
        }

        return KeyOf(group, number);
    }

    /// <summary>
    /// Gets the category of an item definition.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <returns>The category.</returns>
    public static string CategoryOf(ItemDefinition definition)
    {
        var name = new LocalizedString(definition.Name).ValueInNeutralLanguage ?? string.Empty;
        bool Has(string part) => name.Contains(part, StringComparison.OrdinalIgnoreCase);
        var slot = definition.ItemSlot?.ItemSlots.FirstOrDefault() ?? -1;
        return definition.Group switch
        {
            4 when definition.Number is 7 or 15 => Consumables, // Bolt, Arrows
            <= 5 => Weapons,
            6 => Shields,
            <= 11 => Sets,
            12 when Has("Wing") || Has("Cape") => Wings,
            12 when Has("Orb of") || Has("Scroll") || Has("Crystal of") || Has("Parchment") => Skills,
            12 when Has("Seed") || Has("Sphere") => Sockets,
            12 when Has("refine stone") => Materials,
            12 when Has("Jewel") || Has("Gemstone") => Jewels,
            12 when Has("Box") => Boxes,
            13 when definition.Number == 30 => Wings, // Cape of Lord
            13 when slot == 8 || definition.Number == 5 => Pets, // the pet slot, and the Dark Raven which is held in the hand
            13 when slot is 9 or 10 => Jewelry,
            13 when definition.Number is 16 or 17 or 18 or 29 or 49 or 50 or 51 or 125 => EventEntries,
            13 when definition.Number == 15 => Consumables, // Fruits
            13 => Materials,
            14 when Has("Jewellery Case") => Boxes,
            14 when Has("Jewel") || Has("Gemstone") => Jewels,
            14 when Has("refine stone") || definition.Number is 87 or 90 => Materials,
            14 when definition.Number is 17 or 18 or 19 or 28 or 29 or (>= 101 and <= 111) => EventEntries,
            14 when Has("Box") || Has("Case") || Has("Firecracker") || Has("Christmas Star") || Has("Pumpkin") => Boxes,
            14 when definition.Number is 91 or 162 or 163 or 169 => Others,
            14 => Consumables,
            15 => Skills,
            _ => Others,
        };
    }

    /// <summary>
    /// Gets the name of an excellent option, by the designation of its attribute.
    /// </summary>
    /// <param name="designation">The designation of the attribute.</param>
    /// <returns>The name.</returns>
    public static string ExcellentName(string? designation)
        => designation is not null && ExcellentNames.TryGetValue(designation, out var name) ? name : designation ?? "opción excelente";

    /// <summary>
    /// Translates an option line which was saved before the excellent options had their market names.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <returns>The line with the market name of the option.</returns>
    public static string TranslateOptionLine(string line)
        => line.StartsWith(ExcellentPrefix, StringComparison.Ordinal) ? ExcellentPrefix + ExcellentName(line[ExcellentPrefix.Length..]) : line;

    /// <summary>
    /// Gets the name of the picture of an item, like the item editor of the admin panel names them.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <param name="number">The number.</param>
    /// <param name="level">The level of the item, or of the variant.</param>
    /// <param name="excellent">If set to <c>true</c>, the item is excellent.</param>
    /// <param name="ancient">If set to <c>true</c>, the item is ancient.</param>
    /// <returns>The candidate file names, best first.</returns>
    public static IEnumerable<string> ImageNames(byte group, short number, byte level, bool excellent, bool ancient)
    {
        int[] wearableLevels = [0, 0, 0, 3, 3, 5, 5, 7, 7, 9, 9, 11, 11, 13, 13, 15, 15];
        var imageLevel = group < 12 ? (level < wearableLevels.Length ? wearableLevels[level] : 0) : level;
        var suffix = group < 12 ? (ancient ? "_a" : excellent ? "_e" : string.Empty) : string.Empty;
        yield return $"item_{group}_{number}_{imageLevel}{suffix}.png";
        if (suffix.Length > 0)
        {
            yield return $"item_{group}_{number}_{imageLevel}.png";
        }

        yield return $"item_{group}_{number}_0.png";
    }

    private static Dictionary<(byte Group, short Number), string[]> CreateLevelVariants()
    {
        var variants = new Dictionary<(byte Group, short Number), string[]>
        {
        [(14, 11)] =
        [
            "Box of Luck", "Star of Sacred Birth", "Firecracker Box", "Heart of Love", "Olive of Love", "Silver Medal", "Gold Medal", "Box of Heaven",
            "Box of Kundun +1", "Box of Kundun +2", "Box of Kundun +3", "Box of Kundun +4", "Box of Kundun +5", "Heart of Dark Lord", "Lucky Blue Pouch", "Lucky Red Pouch",
        ],
        [(14, 7)] = ["Potion of Bless", "Potion of Soul"],
        [(13, 14)] = ["Loch's Feather", "Crest of Monarch"],
        [(13, 10)] =
        [
            "Transformation Ring (Budge Dragon)", "Transformation Ring (Giant)", "Transformation Ring (Skeleton Warrior)",
            "Transformation Ring (Poison Bull)", "Transformation Ring (Thunder Lich)", "Transformation Ring (Death Cow)",
        ],
        [(12, 11)] =
        [
            "Orb of Summoning (Goblin)", "Orb of Summoning (Stone Golem)", "Orb of Summoning (Assassin)", "Orb of Summoning (Elite Yeti)",
            "Orb of Summoning (Dark Knight)", "Orb of Summoning (Bali)", "Orb of Summoning (Soldier)",
        ],
        };

        // The packed jewels hold 10, 20 or 30 jewels by their level.
        (short Number, string Name)[] packed =
        [
            (30, "Jewel of Bless"), (31, "Jewel of Soul"), (136, "Jewel of Life"), (137, "Jewel of Creation"), (138, "Jewel of Guardian"),
            (139, "Gemstone"), (140, "Jewel of Harmony"), (141, "Jewel of Chaos"), (142, "Lower refine stone"), (143, "Higher refine stone"),
        ];
        foreach (var (number, name) in packed)
        {
            variants[(12, number)] = [$"Packed {name} x10", $"Packed {name} x20", $"Packed {name} x30"];
        }

        return variants;
    }

    private static bool IsFenrir(byte group, short number) => group == 13 && number == 37;
}
