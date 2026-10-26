// <copyright file="MapDifficultyScalerConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

using MUnique.OpenMU.DataModel.Composition;

/// <summary>
/// Configuration for the <see cref="MapDifficultyScaler"/>.
/// </summary>
/// <remarks>
/// The defaults put the hunting maps on a progressive curve of resets: a Dark Knight with the best equipment
/// (an excellent +15 set of Karutan, an excellent +15 weapon and +15 wings of the third level) needs about
/// 300 * (position / 12)^2 resets to hunt in a map, e.g. 2 in Devias, 52 in Tarkan and 300 in Raklion.
/// The events need up to 200 resets in their highest level, and the golden monsters of the invasions
/// 50 resets per level of the Box of Kundun they drop, minus 25 (25 for a box +1, 225 for a box +5).
/// </remarks>
public class MapDifficultyScalerConfiguration
{
    /// <summary>
    /// Gets or sets the multipliers of the monsters of the maps, by the number of the map.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.MapDifficultyScalerConfiguration_Maps_Name), Description = nameof(PlugInResources.MapDifficultyScalerConfiguration_Maps_Description))]
    [MemberOfAggregate]
    [ScaffoldColumn(true)]
    public ICollection<DifficultyMultipliers> Maps { get; set; } = CreateDefaultMaps();

    /// <summary>
    /// Gets or sets the multipliers of monsters, by the number of the monster. They replace the multipliers of the map.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.MapDifficultyScalerConfiguration_Monsters_Name), Description = nameof(PlugInResources.MapDifficultyScalerConfiguration_Monsters_Description))]
    [MemberOfAggregate]
    [ScaffoldColumn(true)]
    public ICollection<DifficultyMultipliers> Monsters { get; set; } = CreateDefaultMonsters();

    /// <summary>
    /// Creates the default multipliers of the maps.
    /// </summary>
    /// <returns>The default multipliers of the maps.</returns>
    internal static List<DifficultyMultipliers> CreateDefaultMaps() =>
    [
        new(2, 16.96f, 9.91f, 8.61f, 3.97f, 9.47f), // Devias, 2 resets
        new(1, 8.27f, 8.78f, 6.22f, 3.79f, 7.41f), // Dungeon, 8 resets
        new(4, 7.54f, 12.78f, 6.54f, 5.18f, 7.64f), // Lost Tower, 19 resets
        new(7, 5.67f, 10.71f, 5.77f, 6.42f, 7.64f), // Atlans, 33 resets
        new(8, 5.93f, 11.96f, 7.73f, 8.24f, 8.89f), // Tarkan, 52 resets
        new(33, 5.11f, 18.19f, 9.21f, 8.74f, 8.77f), // Aida, 75 resets
        new(10, 4.98f, 12.72f, 10.87f, 9.04f, 8.89f), // Icarus, 102 resets
        new(37, 4.89f, 17.37f, 10.89f, 9.21f, 8.85f), // Kanturu Ruins, 133 resets
        new(38, 6.55f, 15.32f, 14.63f, 10.88f, 10.02f), // Kanturu Relics, 169 resets
        new(80, 6.69f, 19.25f, 18.09f, 12.01f, 10.5f), // Karutan 1, 208 resets
        new(81, 6.69f, 19.25f, 18.09f, 12.01f, 10.5f), // Karutan 2, 208 resets
        new(56, 3.52f, 12.21f, 18.97f, 8.97f, 4.73f), // Swamp of Calmness, 252 resets
        new(57, 3.16f, 10.65f, 19.42f, 3.02f, 3.37f), // Raklion, 300 resets
        new(58, 3.16f, 10.65f, 19.42f, 3.02f, 3.37f), // Hatchery of Selupan, 300 resets
        new(12, 2.3f, 8.2f, 3.14f, 3.52f, 4.54f), // Blood Castle 2, 29 resets
        new(13, 3.03f, 9.89f, 4.4f, 5.62f, 5.67f), // Blood Castle 3, 57 resets
        new(14, 3.37f, 10.61f, 6.03f, 6.94f, 6.57f), // Blood Castle 4, 86 resets
        new(15, 4.15f, 12.83f, 7.92f, 8.33f, 7.66f), // Blood Castle 5, 114 resets
        new(16, 3.66f, 13.6f, 8.28f, 9.67f, 7.34f), // Blood Castle 6, 143 resets
        new(17, 6.27f, 12.65f, 11.37f, 11.44f, 8.52f), // Blood Castle 7, 171 resets
        new(52, 2.01f, 9.28f, 13.28f, 12.63f, 5.49f), // Blood Castle 8, 200 resets
        new(19, 22.63f, 18.73f, 12.17f, 9.36f, 13.17f), // Chaos Castle 2, 33 resets
        new(20, 21.71f, 25.58f, 17.91f, 14.6f, 16.31f), // Chaos Castle 3, 67 resets
        new(21, 4.36f, 29.67f, 21.54f, 17.61f, 16.55f), // Chaos Castle 4, 100 resets
        new(22, 20.75f, 33.02f, 26.75f, 19.96f, 19.17f), // Chaos Castle 5, 133 resets
        new(23, 19.58f, 34.29f, 29.29f, 21.13f, 19.96f), // Chaos Castle 6, 167 resets
        new(53, 19.69f, 33.14f, 33.27f, 23.5f, 3.23f), // Chaos Castle 7, 200 resets
        new(25, 11.32f, 14.09f, 9.38f, 7.71f, 11.41f), // Kalima 2, 33 resets
        new(26, 10.1f, 15.64f, 10.33f, 10.66f, 11.49f), // Kalima 3, 67 resets
        new(27, 4.66f, 13.22f, 9.34f, 1.99f, 7.98f), // Kalima 4, 100 resets
        new(28, 3.37f, 11.35f, 8.73f, 9.13f, 7.99f), // Kalima 5, 133 resets
        new(29, 2.91f, 10.52f, 9f, 9.2f, 7.52f), // Kalima 6, 167 resets
        new(36, 2.79f, 10.08f, 9.53f, 9.75f, 7.37f), // Kalima 7, 200 resets
        new(9, 3.37f, 10.61f, 6.03f, 6.94f, 6.57f), // Devil Square 1 to 4, like Blood Castle 4, 86 resets
        new(32, 1.06f, 13.91f, 13.55f, 8.43f, 6.06f), // Devil Square 5 to 7, 200 resets
        new(34, 2.97f, 10.06f, 9.82f, 10.26f, 7.58f), // Crywolf, 200 resets
        new(41, 5.08f, 17.91f, 17.82f, 9.4f, 8.81f), // Barracks of Balgass, 200 resets
        new(42, 3.2f, 1f, 13.76f, 9.31f, 10.27f), // Refuge of Balgass, 250 resets
    ];

    /// <summary>
    /// Creates the default multipliers of the monsters.
    /// </summary>
    /// <returns>The default multipliers of the monsters.</returns>
    internal static List<DifficultyMultipliers> CreateDefaultMonsters() =>
    [
        new(43, 23.75f, 24.19f, 4.49f, 3.56f, 2.43f), // Golden Budge Dragon, Box of Kundun +1, 25 resets
        new(78, 16.85f, 21.17f, 4.04f, 17.79f, 14.56f), // Golden Goblin, Box of Kundun +1, 25 resets
        new(54, 14.93f, 20.44f, 2.02f, 3.56f, 2.43f), // Golden Soldier, Box of Kundun +1, 25 resets
        new(53, 18.57f, 38.82f, 3.06f, 9.06f, 5.05f), // Golden Titan, Box of Kundun +2, 75 resets
        new(81, 23.59f, 34.84f, 2.3f, 14.62f, 16.83f), // Golden Vepar, Box of Kundun +2, 75 resets
        new(83, 19.06f, 31.07f, 2.95f, 18.84f, 14.25f), // Golden Wheel, Box of Kundun +3, 125 resets
        new(79, 12.43f, 23.48f, 2.21f, 18.14f, 25.33f), // Golden Dragon, Box of Kundun +3, 125 resets
        new(80, 18.46f, 27.13f, 3.57f, 23.95f, 20.07f), // Golden Lizard King, Box of Kundun +4, 175 resets
        new(82, 13.55f, 26.56f, 4.19f, 28.05f, 20.23f), // Golden Tantallos, Box of Kundun +5, 225 resets
    ];

    /// <summary>
    /// Multipliers of the stats of the monsters of a map, or of a monster.
    /// </summary>
    public class DifficultyMultipliers
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DifficultyMultipliers"/> class.
        /// </summary>
        public DifficultyMultipliers()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DifficultyMultipliers"/> class.
        /// </summary>
        /// <param name="number">The number of the map or monster.</param>
        /// <param name="health">The multiplier of the health.</param>
        /// <param name="damage">The multiplier of the damage.</param>
        /// <param name="defense">The multiplier of the defense.</param>
        /// <param name="attackRate">The multiplier of the attack rate.</param>
        /// <param name="defenseRate">The multiplier of the defense rate.</param>
        internal DifficultyMultipliers(short number, float health, float damage, float defense, float attackRate, float defenseRate)
        {
            this.Number = number;
            this.Health = health;
            this.Damage = damage;
            this.Defense = defense;
            this.AttackRate = attackRate;
            this.DefenseRate = defenseRate;
        }

        /// <summary>
        /// Gets or sets the number of the map or monster.
        /// </summary>
        [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DifficultyMultipliers_Number_Name))]
        public short Number { get; set; }

        /// <summary>
        /// Gets or sets the multiplier of the maximum health.
        /// </summary>
        [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DifficultyMultipliers_Health_Name))]
        public float Health { get; set; } = 1;

        /// <summary>
        /// Gets or sets the multiplier of the minimum and maximum damage.
        /// </summary>
        [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DifficultyMultipliers_Damage_Name))]
        public float Damage { get; set; } = 1;

        /// <summary>
        /// Gets or sets the multiplier of the defense.
        /// </summary>
        [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DifficultyMultipliers_Defense_Name))]
        public float Defense { get; set; } = 1;

        /// <summary>
        /// Gets or sets the multiplier of the attack rate.
        /// </summary>
        [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DifficultyMultipliers_AttackRate_Name))]
        public float AttackRate { get; set; } = 1;

        /// <summary>
        /// Gets or sets the multiplier of the defense rate.
        /// </summary>
        [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.DifficultyMultipliers_DefenseRate_Name))]
        public float DefenseRate { get; set; } = 1;
    }
}
