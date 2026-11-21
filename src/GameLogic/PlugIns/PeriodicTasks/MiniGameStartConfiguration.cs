// <copyright file="MiniGameStartConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.PeriodicTasks;

using MUnique.OpenMU.Interfaces;

/// <summary>
/// The minimal minigame start configuration.
/// </summary>
public abstract class MiniGameStartConfiguration : PeriodicTaskConfiguration
{
    /// <summary>
    /// Gets or sets the entrance opened message.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.MiniGameStartConfiguration_EntranceOpenedMessage_Name), Order = 100)]
    public LocalizedString EntranceOpenedMessage { get; set; }

    /// <summary>
    /// Gets or sets the entrance closed message.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.MiniGameStartConfiguration_EntranceClosedMessage_Name), Order = 101)]
    public LocalizedString EntranceClosedMessage { get; set; }

    /// <summary>
    /// Gets or sets how many minutes before the entrance opens the <see cref="PreOpenMessage"/> is announced, once per
    /// minute (e.g. 5: at 5, 4, 3, 2 and 1 minute before). 0 doesn't announce the opening in advance.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.MiniGameStartConfiguration_PreOpenAnnouncementMinutes_Name), Description = nameof(PlugInResources.MiniGameStartConfiguration_PreOpenAnnouncementMinutes_Description), Order = 102)]
    public int PreOpenAnnouncementMinutes { get; set; }

    /// <summary>
    /// Gets or sets the golden message which announces the minutes until the entrance opens ({0}: the minutes),
    /// see <see cref="PreOpenAnnouncementMinutes"/>.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.MiniGameStartConfiguration_PreOpenMessage_Name), Order = 103)]
    public LocalizedString PreOpenMessage { get; set; }
}
