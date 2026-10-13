// <copyright file="ItemCsvWriter.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.ClientDataExporter;

using System.Globalization;
using System.IO;

/// <summary>
/// Writes item definitions into a CSV file, one row per item.
/// </summary>
public static class ItemCsvWriter
{
    private static readonly string[] ClassColumns = ["DW", "DK", "Elf", "MG", "DL", "Summoner", "RF"];

    private static readonly string[] ResistanceColumns = ["Ice", "Poison", "Lightning", "Fire", "Earth", "Wind", "Water", "Resistance7"];

    /// <summary>
    /// Writes the items into the file.
    /// </summary>
    /// <param name="path">The path of the CSV file.</param>
    /// <param name="items">The items.</param>
    public static void Write(string path, IEnumerable<ItemRecord> items)
    {
        using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
        var header = new[]
            {
                "Group", "Number", "Name", "TwoHanded", "DropLevel", "ItemSlot", "SkillNumber", "Width", "Height",
                "MinimumDamage", "MaximumDamage", "DefenseRate", "Defense", "MagicDefense", "AttackSpeed", "WalkSpeed",
                "Durability", "MagicDurability", "MagicPower", "RequiredStrength", "RequiredAgility", "RequiredEnergy",
                "RequiredVitality", "RequiredLeadership", "RequiredLevel", "Value", "Zen", "AttackType",
            }
            .Concat(ClassColumns.Select(c => "Class" + c))
            .Concat(ResistanceColumns.Select(r => "Resistance" + r));
        writer.WriteLine(string.Join(',', header));

        foreach (var item in items)
        {
            var values = new object[]
                {
                    item.Group, item.Number, Quote(item.Name), item.TwoHanded ? 1 : 0, item.DropLevel, item.ItemSlot,
                    item.SkillNumber, item.Width, item.Height, item.MinimumDamage, item.MaximumDamage, item.DefenseRate,
                    item.Defense, item.MagicDefense, item.AttackSpeed, item.WalkSpeed, item.Durability,
                    item.MagicDurability, item.MagicPower, item.RequiredStrength, item.RequiredAgility,
                    item.RequiredEnergy, item.RequiredVitality, item.RequiredLeadership, item.RequiredLevel,
                    item.Value, item.Zen, item.AttackType,
                }
                .Concat(item.ClassRequirements.Select(b => (object)b))
                .Concat(item.Resistances.Select(b => (object)b));
            writer.WriteLine(string.Join(',', values.Select(v => Convert.ToString(v, CultureInfo.InvariantCulture))));
        }
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
