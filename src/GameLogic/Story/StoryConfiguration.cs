// <copyright file="StoryConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Story;

/// <summary>
/// The configuration of the <see cref="StoryModeFeaturePlugIn"/>.
/// </summary>
public class StoryConfiguration
{
    /// <summary>
    /// Gets or sets the minimum resets to duel the stranger (step 2).
    /// </summary>
    [Display(Name = "Step 2 (duel against the stranger): minimum resets", Description = "The minimum resets to duel the stranger of Lorencia. Winning opens Devias.")]
    public int StrangerDuelMinimumResets { get; set; } = 2;

    /// <summary>
    /// Gets or sets the minimum resets of step 4, in which the stranger tells its name and opens the Dungeon.
    /// </summary>
    [Display(Name = "Step 4 (Artorias, Dungeon): minimum resets", Description = "The minimum resets for the scene in which the stranger tells its name and opens the Dungeon.")]
    public int DungeonMinimumResets { get; set; } = 5;

    /// <summary>
    /// Gets or sets a value indicating whether step 4 requires the finished dungeon of Devias.
    /// </summary>
    [Display(Name = "Step 4 requires the dungeon of Devias", Description = "If enabled, the player has to finish the dungeon of Devias (be there when its last boss falls) before step 4. Characters which entered the dungeon before they started the story count as finished.")]
    public bool DungeonRequiresDeviasDungeon { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum resets of step 5, in which Artorias sends the player to Sebina.
    /// </summary>
    [Display(Name = "Step 5 (Artorias sends to Sebina): minimum resets", Description = "The minimum resets for the scene in which Artorias tells the story of Kundun and sends the player to Sebina, who opens the Lost Tower and the first class change.")]
    public int SebinaMinimumResets { get; set; } = 15;

    /// <summary>
    /// Gets or sets the minimum resets of step 7, in which Lumen opens the remaining maps.
    /// </summary>
    [Display(Name = "Step 7 (Lumen, old world): minimum resets", Description = "The minimum resets for the last scene of the first part, in which Lumen opens the remaining maps.")]
    public int OldWorldMinimumResets { get; set; } = 20;

    /// <summary>
    /// Gets or sets a value indicating whether the maps are locked until the story opens them.
    /// </summary>
    [Display(Name = "Lock maps", Description = "If enabled, the players can't warp or walk to the maps below until the story opens them.")]
    public bool LockMaps { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether game masters can enter the locked maps.
    /// </summary>
    [Display(Name = "Game masters ignore the map locks", Description = "If enabled, game masters can always enter the locked maps. Disable it to test the story with a game master.")]
    public bool GameMastersIgnoreLocks { get; set; } = true;

    /// <summary>
    /// Gets or sets the numbers of the maps which the stranger opens after the duel (step 3).
    /// </summary>
    [Display(Name = "Maps opened by step 3 (Devias)", Description = "The numbers of the maps which the stranger opens after the duel, separated by commas.")]
    public string DeviasMaps { get; set; } = "2";

    /// <summary>
    /// Gets or sets the numbers of the maps which Artorias opens with step 4.
    /// </summary>
    [Display(Name = "Maps opened by step 4 (Dungeon)", Description = "The numbers of the maps which the stranger opens when it tells its name, separated by commas.")]
    public string DungeonMaps { get; set; } = "1";

    /// <summary>
    /// Gets or sets the numbers of the maps which Sebina opens with step 6.
    /// </summary>
    [Display(Name = "Maps opened by step 6 (Lost Tower)", Description = "The numbers of the maps which Sebina opens, separated by commas.")]
    public string LostTowerMaps { get; set; } = "4";

    /// <summary>
    /// Gets or sets the numbers of the maps which Lumen opens with step 7.
    /// </summary>
    [Display(Name = "Maps opened by step 7 (old world)", Description = "The numbers of the maps which Lumen opens at the end of the first part, separated by commas: Atlans, Tarkan, Icarus, Kalima, Aida, Crywolf, Kanturu, Barracks, Refuge, Swamp of Calmness, Raklion, Vulcanus and Karutan.")]
    public string OldWorldMaps { get; set; } = "7,8,10,24,25,26,27,28,29,33,34,36,37,38,41,42,56,57,58,63,80,81";

    /// <summary>
    /// Gets or sets a value indicating whether the first class change quest of Sebina requires step 6.
    /// </summary>
    [Display(Name = "Class change requires Sebina's scene", Description = "If enabled, Sebina only starts her quests of the first class change after her scene of the story (step 6).")]
    public bool ClassChangeRequiresStory { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the first skill quest requires the first talk to Lumen.
    /// </summary>
    [Display(Name = "Skill quests require Lumen's first scene", Description = "If enabled, a character which didn't finish any skill quest yet has to talk to Lumen (step 1) before the NPCs offer their skill quests.")]
    public bool SkillQuestsRequireStory { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum distance between the player and the NPC to play a scene.
    /// </summary>
    [Display(Name = "Interaction range", Description = "The maximum distance (in tiles) between the player and the NPC to start a scene or to press its buttons.")]
    public int InteractionRange { get; set; } = 8;

    /// <summary>
    /// Gets or sets the level of the stranger in the duel.
    /// </summary>
    [Display(Name = "Stranger: level", Description = "The level of the stranger in the duel. A change takes effect at the next duel of each player.")]
    public int StrangerLevel { get; set; } = 250;

    /// <summary>
    /// Gets or sets the resets of the stranger in the duel.
    /// </summary>
    [Display(Name = "Stranger: resets", Description = "The resets of the stranger in the duel; it gets the stat points of these resets.")]
    public int StrangerResets { get; set; } = 1;

    /// <summary>
    /// Gets or sets the skill tier of the stranger in the duel.
    /// </summary>
    [Display(Name = "Stranger: skill tier", Description = "The stranger is a knight with the skills of a spectre rival of this evolution (0 to 8).")]
    public int StrangerSkillTier { get; set; } = 1;

    /// <summary>
    /// Gets or sets the level of the items of the stranger in the duel.
    /// </summary>
    [Display(Name = "Stranger: item level", Description = "The level of the armor and the sword of the stranger in the duel (0 to 15).")]
    public int StrangerItemLevel { get; set; } = 3;

    /// <summary>
    /// Gets the minimum resets of a step.
    /// </summary>
    /// <param name="step">The number of the step.</param>
    /// <returns>The minimum resets.</returns>
    public int GetMinimumResets(int step) => step switch
    {
        StoryScript.StrangerDuel => this.StrangerDuelMinimumResets,
        StoryScript.Dungeon => this.DungeonMinimumResets,
        StoryScript.OldPacts => this.SebinaMinimumResets,
        StoryScript.OldWorld => this.OldWorldMinimumResets,
        _ => 0,
    };

    /// <summary>
    /// Gets the step which opens a map.
    /// </summary>
    /// <param name="mapNumber">The number of the map.</param>
    /// <returns>The number of the step which the player has to finish to enter the map; 0, if the map is always open.</returns>
    public int GetUnlockingStep(short mapNumber)
    {
        if (Contains(this.DeviasMaps, mapNumber))
        {
            return StoryScript.Devias;
        }

        if (Contains(this.DungeonMaps, mapNumber))
        {
            return StoryScript.Dungeon;
        }

        if (Contains(this.LostTowerMaps, mapNumber))
        {
            return StoryScript.SchoolOfLight;
        }

        return Contains(this.OldWorldMaps, mapNumber) ? StoryScript.OldWorld : 0;
    }

    private static bool Contains(string? list, short mapNumber)
    {
        foreach (var part in (list ?? string.Empty).Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries))
        {
            if (short.TryParse(part, out var number) && number == mapNumber)
            {
                return true;
            }
        }

        return false;
    }
}
