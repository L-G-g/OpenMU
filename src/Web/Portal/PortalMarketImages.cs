// <copyright file="PortalMarketImages.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Portal;

using System.IO;

/// <summary>
/// The pictures of the items, which come with the item editor of the admin panel.
/// </summary>
public static class PortalMarketImages
{
    /// <summary>
    /// The url under which the portal serves the pictures.
    /// </summary>
    public const string UrlPrefix = "/item-images/";

    private const string PublishedPath = "wwwroot/_content/MUnique.OpenMU.Web.ItemEditor/img/items";
    private const string SourcePath = "src/Web/ItemEditor/wwwroot/img/items";

    /// <summary>
    /// Finds the folder of the pictures: next to the published server, or in the source folder when it runs from a build.
    /// </summary>
    /// <returns>The folder, or <c>null</c> if there is none.</returns>
    public static string? FindDirectory()
    {
        var published = Path.Combine(AppContext.BaseDirectory, PublishedPath);
        if (Directory.Exists(published))
        {
            return published;
        }

        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var source = Path.Combine(dir.FullName, SourcePath);
            if (Directory.Exists(source))
            {
                return source;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the file names of the pictures.
    /// </summary>
    /// <returns>The file names.</returns>
    public static HashSet<string> FindFileNames()
    {
        var directory = FindDirectory();
        return directory is null
            ? []
            : Directory.EnumerateFiles(directory, "item_*.png").Select(Path.GetFileName).OfType<string>().ToHashSet(StringComparer.Ordinal);
    }
}
