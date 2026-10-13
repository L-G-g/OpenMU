// <copyright file="Program.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System.IO;
using MUnique.OpenMU.ClientDataExporter;

if (args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
{
    Console.WriteLine("Usage: MUnique.OpenMU.ClientDataExporter <client folder> [output folder]");
    Console.WriteLine("Exports the item definitions of the client's Data\\Local\\item.bmd into items.csv.");
    return 1;
}

var clientFolder = args[0];
var outputFolder = args.Length > 1 ? args[1] : Path.Combine(Environment.CurrentDirectory, "client-data");

var itemFile = FindFile(clientFolder, ItemFileReader.FileName);
if (itemFile is null)
{
    Console.WriteLine($"{ItemFileReader.FileName} wasn't found below {clientFolder}.");
    return 2;
}

try
{
    var items = ItemFileReader.Read(itemFile);
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

// The client keeps its tables in Data\Local; the file name casing differs between client versions.
static string? FindFile(string folder, string fileName)
{
    if (File.Exists(folder))
    {
        return folder;
    }

    if (!Directory.Exists(folder))
    {
        return null;
    }

    var options = new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive, RecurseSubdirectories = true };
    return Directory.EnumerateFiles(folder, fileName, options)
        .OrderBy(f => f.Contains("Local", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
        .ThenBy(f => f.Length)
        .FirstOrDefault();
}
