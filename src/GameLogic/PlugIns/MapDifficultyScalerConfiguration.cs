// <copyright file="MapDifficultyScalerConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

using MUnique.OpenMU.DataModel.Composition;

/// <summary>
/// Configuration for the <see cref="MapDifficultyScaler"/>.
/// </summary>
/// <remarks>
/// The defaults put the hunting maps on a progressive curve of resets: a Dark Knight with the best excellent
/// equipment which drops in the map (+15 set and weapon, +15 wings of the first level up to Atlans, of the second
/// level from Tarkan to Kanturu Relics and of the third level from Karutan on; Swamp of Calmness and Raklion use
/// the equipment of Karutan) needs about 300 * (position / 12)^2 resets to hunt in a map, e.g. 2 in Devias,
/// 52 in Tarkan and 300 in Raklion. There, without potions, it survives a spot of monsters for about 1.5 times
/// the time it needs to kill them. Events and golden monsters assume the equipment of the highest map
/// which is reached with their resets.
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
        new(2, 8.86f, 4.19f, 2.46f, 4.49f, 9.5f), // Devias, 2 resets
        new(1, 4.92f, 3.78f, 1.92f, 4.24f, 7.43f), // Dungeon, 8 resets
        new(4, 5.13f, 4.56f, 2.1f, 5.65f, 7.6f), // Lost Tower, 19 resets
        new(7, 4.07f, 3.99f, 1.76f, 7.11f, 7.66f), // Atlans, 33 resets
        new(8, 4.75f, 5.03f, 2.23f, 9.04f, 8.88f), // Tarkan, 52 resets
        new(33, 4.19f, 6.3f, 2.37f, 9.64f, 8.79f), // Aida, 75 resets
        new(10, 4.09f, 5.15f, 2.45f, 9.95f, 8.89f), // Icarus, 102 resets
        new(37, 4.07f, 5.91f, 2.17f, 10.13f, 8.85f), // Kanturu Ruins, 133 resets
        new(38, 5.57f, 5.93f, 2.59f, 11.96f, 10.02f), // Kanturu Relics, 169 resets
        new(80, 6.64f, 8.39f, 2.85f, 13.21f, 10.5f), // Karutan, 208 resets
        new(81, 6.64f, 8.39f, 2.85f, 13.21f, 10.5f), // Karutan, 208 resets
        new(56, 3.49f, 4.83f, 2.62f, 9.86f, 4.73f), // Swamp of Calmness, 252 resets
        new(57, 3.14f, 4.21f, 2.37f, 3.32f, 3.37f), // Raklion, 300 resets
        new(58, 3.14f, 4.21f, 2.37f, 3.32f, 3.37f), // Raklion, 300 resets
        new(12, 1.62f, 3.05f, 1f, 3.85f, 4.52f), // Blood Castle 2, 29 resets
        new(13, 2.43f, 3.91f, 1.23f, 6.16f, 5.66f), // Blood Castle 3, 57 resets
        new(14, 2.78f, 4.1f, 1.47f, 7.66f, 6.58f), // Blood Castle 4, 86 resets
        new(15, 3.43f, 4.88f, 1.69f, 9.17f, 7.66f), // Blood Castle 5, 114 resets
        new(16, 3.05f, 5.18f, 1.58f, 10.64f, 7.34f), // Blood Castle 6, 143 resets
        new(17, 5.33f, 4.81f, 2f, 12.58f, 8.52f), // Blood Castle 7, 171 resets
        new(52, 1.71f, 3.5f, 2.12f, 13.89f, 5.49f), // Blood Castle 8, 200 resets
        new(19, 16.27f, 6.98f, 3.71f, 10.37f, 13.2f), // Chaos Castle 2, 33 resets
        new(20, 17.61f, 9.98f, 4.78f, 16.03f, 16.29f), // Chaos Castle 3, 67 resets
        new(21, 3.62f, 11.33f, 4.93f, 19.43f, 16.57f), // Chaos Castle 4, 100 resets
        new(22, 17.27f, 12.63f, 5.32f, 21.95f, 19.17f), // Chaos Castle 5, 133 resets
        new(23, 16.41f, 12.94f, 5.14f, 23.24f, 19.96f), // Chaos Castle 6, 167 resets
        new(53, 16.78f, 12.49f, 5.3f, 25.85f, 3.23f), // Chaos Castle 7, 200 resets
        new(25, 8.13f, 5.25f, 2.86f, 8.54f, 11.44f), // Kalima 2, 33 resets
        new(26, 8.2f, 6.1f, 2.76f, 11.71f, 11.47f), // Kalima 3, 67 resets
        new(27, 3.86f, 5.05f, 2.14f, 2.2f, 8f), // Kalima 4, 100 resets
        new(28, 2.8f, 4.34f, 1.73f, 10.04f, 7.99f), // Kalima 5, 133 resets
        new(29, 2.44f, 3.97f, 1.58f, 10.12f, 7.52f), // Kalima 6, 167 resets
        new(36, 2.38f, 3.8f, 1.52f, 10.73f, 7.37f), // Kalima 7, 200 resets
        new(9, 2.78f, 4.1f, 1.47f, 7.66f, 6.58f), // Devil Square 1 to 4, like Blood Castle 4, 86 resets
        new(32, 1f, 4.91f, 2.16f, 9.27f, 6.06f), // Devil Square 5 to 7, 200 resets
        new(34, 2.53f, 3.79f, 1.57f, 11.28f, 7.58f), // Crywolf, 200 resets
        new(41, 4.33f, 6.75f, 2.84f, 10.34f, 8.81f), // Barracks of Balgass, 200 resets
        new(42, 3.18f, 1f, 1.91f, 10.24f, 10.27f), // Refuge of Balgass, 250 resets
    ];

    /// <summary>
    /// Creates the default multipliers of the monsters.
    /// </summary>
    /// <returns>The default multipliers of the monsters.</returns>
    internal static List<DifficultyMultipliers> CreateDefaultMonsters() =>
    [
        new(43, 16.63f, 5.26f, 1.42f, 3.89f, 2.42f), // Golden Budge Dragon, Box of Kundun +1, 25 resets
        new(78, 11.81f, 4.6f, 1.28f, 19.43f, 14.5f), // Golden Goblin, Box of Kundun +1, 25 resets
        new(54, 8.43f, 4.44f, 1f, 3.89f, 2.42f), // Golden Soldier, Box of Kundun +1, 25 resets
        new(53, 13.84f, 9.27f, 1f, 10f, 5.06f), // Golden Titan, Box of Kundun +2, 75 resets
        new(81, 14.79f, 8.32f, 1f, 16.13f, 16.86f), // Golden Vepar, Box of Kundun +2, 75 resets
        new(83, 12.24f, 7.26f, 1f, 20.75f, 14.25f), // Golden Wheel, Box of Kundun +3, 125 resets
        new(79, 6.03f, 5.48f, 1f, 19.97f, 25.34f), // Golden Dragon, Box of Kundun +3, 125 resets
        new(80, 12.38f, 6.33f, 1f, 26.34f, 20.07f), // Golden Lizard King, Box of Kundun +4, 175 resets
        new(82, 10.72f, 8.07f, 1f, 30.86f, 20.23f), // Golden Tantallos, Box of Kundun +5, 225 resets
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
