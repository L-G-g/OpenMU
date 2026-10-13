// <copyright file="Program.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System.IO;
using MUnique.OpenMU.ClientDataExporter;

if (args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
{
    Console.WriteLine("Usage: MUnique.OpenMU.ClientDataExporter <client folder | item file> [output folder] [language]");
    Console.WriteLine("Exports the item definitions of the client's Data\\Local\\<language>\\item_<language>.bmd into items.csv.");
    Console.WriteLine("The language is the name of the client's language folder, e.g. Eng (default), Spn or Por.");
    return 1;
}

var input = args[0];
var outputFolder = args.Length > 1 ? args[1] : Path.Combine(Environment.CurrentDirectory, "client-data");
var language = args.Length > 2 ? args[2] : "Eng";

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var itemFile = File.Exists(input) ? input : FindLanguageFile(input, language);
if (itemFile is null)
{
    Console.WriteLine($"item_{language}.bmd wasn't found in the language folder {language} of {input}.");
    return 2;
}

try
{
    var items = ItemFileReader.Read(itemFile, GetNameEncoding(itemFile));
    Directory.CreateDirectory(outputFolder);
    var outputFile = Path.Combine(outputFolder, "items.csv");
    ItemCsvWriter.Write(outputFile, items);
    Console.WriteLine($"Exported {items.Count} items of {itemFile} to {outputFile}.");
    return 0;
}
catch (InvalidDataException ex)
{
    Console.WriteLine(ex.Message);
    return 3;
}

// The client reads its items from the language folder, e.g. Data\Local\Eng\item_eng.bmd.
// The other item files in the client folder are leftovers: Data\Local\item.bmd has Japanese
// names and fewer items, Data\Local\Eng\item.bmd is outdated as well.
// The casing of the file names differs between client versions.
static string? FindLanguageFile(string clientFolder, string language)
{
    if (!Directory.Exists(clientFolder))
    {
        return null;
    }

    var options = new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive, RecurseSubdirectories = true };
    return Directory.EnumerateFiles(clientFolder, $"item_{language}.bmd", options)
        .FirstOrDefault(f => string.Equals(Path.GetFileName(Path.GetDirectoryName(f)), language, StringComparison.OrdinalIgnoreCase));
}

// The names are stored in the ANSI code page of the language; the file outside of
// the language folders is the one of the Japanese client.
static Encoding GetNameEncoding(string itemFile)
{
    var folder = Path.GetFileName(Path.GetDirectoryName(itemFile));
    return string.Equals(folder, "Local", StringComparison.OrdinalIgnoreCase)
        ? Encoding.GetEncoding(932)
        : Encoding.GetEncoding(1252);
}
