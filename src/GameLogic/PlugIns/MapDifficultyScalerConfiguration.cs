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
        new(2, 8.92f, 3.36f, 5.39f, 4.08f, 9.5f), // Devias, 2 resets
        new(1, 4.95f, 3.03f, 4.43f, 3.85f, 7.43f), // Dungeon, 8 resets
        new(4, 5.16f, 3.77f, 5.33f, 5.14f, 7.6f), // Lost Tower, 19 resets
        new(7, 4.1f, 3.28f, 4.97f, 6.46f, 7.66f), // Atlans, 33 resets
        new(8, 4.78f, 4.19f, 7.17f, 8.22f, 8.88f), // Tarkan, 52 resets
        new(33, 4.22f, 5.46f, 8.73f, 8.77f, 8.79f), // Aida, 75 resets
        new(10, 4.12f, 4.33f, 10.35f, 9.05f, 8.89f), // Icarus, 102 resets
        new(37, 4.1f, 5.14f, 10.5f, 9.21f, 8.85f), // Kanturu Ruins, 133 resets
        new(38, 5.61f, 5.04f, 14.4f, 10.88f, 10.02f), // Kanturu Relics, 169 resets
        new(80, 6.69f, 7.44f, 18.09f, 12.01f, 10.5f), // Karutan, 208 resets
        new(81, 6.69f, 7.44f, 18.09f, 12.01f, 10.5f), // Karutan, 208 resets
        new(56, 3.52f, 4.38f, 18.97f, 8.97f, 4.73f), // Swamp of Calmness, 252 resets
        new(57, 3.16f, 3.82f, 19.42f, 3.02f, 3.37f), // Raklion, 300 resets
        new(58, 3.16f, 3.82f, 19.42f, 3.02f, 3.37f), // Raklion, 300 resets
        new(12, 1.64f, 2.51f, 2.68f, 3.5f, 4.52f), // Blood Castle 2, 29 resets
        new(13, 2.45f, 3.3f, 4.09f, 5.6f, 5.66f), // Blood Castle 3, 57 resets
        new(14, 2.8f, 3.47f, 5.75f, 6.96f, 6.58f), // Blood Castle 4, 86 resets
        new(15, 3.45f, 4.16f, 7.58f, 8.34f, 7.66f), // Blood Castle 5, 114 resets
        new(16, 3.07f, 4.41f, 8f, 9.67f, 7.34f), // Blood Castle 6, 143 resets
        new(17, 5.37f, 4.1f, 11.19f, 11.44f, 8.52f), // Blood Castle 7, 171 resets
        new(52, 1.72f, 2.99f, 13.09f, 12.62f, 5.49f), // Blood Castle 8, 200 resets
        new(19, 16.38f, 5.74f, 10.48f, 9.43f, 13.2f), // Chaos Castle 2, 33 resets
        new(20, 17.73f, 8.44f, 16.81f, 14.57f, 16.29f), // Chaos Castle 3, 67 resets
        new(21, 3.64f, 9.64f, 20.66f, 17.66f, 16.57f), // Chaos Castle 4, 100 resets
        new(22, 17.39f, 10.75f, 25.77f, 19.96f, 19.17f), // Chaos Castle 5, 133 resets
        new(23, 16.52f, 11.04f, 28.41f, 21.13f, 19.96f), // Chaos Castle 6, 167 resets
        new(53, 16.89f, 10.67f, 32.81f, 23.5f, 3.23f), // Chaos Castle 7, 200 resets
        new(25, 8.19f, 4.32f, 8.08f, 7.76f, 11.44f), // Kalima 2, 33 resets
        new(26, 8.25f, 5.16f, 9.7f, 10.64f, 11.47f), // Kalima 3, 67 resets
        new(27, 3.89f, 4.29f, 8.96f, 2f, 8f), // Kalima 4, 100 resets
        new(28, 2.82f, 3.69f, 8.41f, 9.13f, 7.99f), // Kalima 5, 133 resets
        new(29, 2.46f, 3.39f, 8.73f, 9.2f, 7.52f), // Kalima 6, 167 resets
        new(36, 2.39f, 3.24f, 9.4f, 9.75f, 7.37f), // Kalima 7, 200 resets
        new(9, 2.8f, 3.47f, 5.75f, 6.96f, 6.58f), // Devil Square 1 to 4, like Blood Castle 4, 86 resets
        new(32, 1f, 4.15f, 13.37f, 8.43f, 6.06f), // Devil Square 5 to 7, 200 resets
        new(34, 2.55f, 3.24f, 9.69f, 10.25f, 7.58f), // Crywolf, 200 resets
        new(41, 4.36f, 5.77f, 17.57f, 9.4f, 8.81f), // Barracks of Balgass, 200 resets
        new(42, 3.2f, 1f, 13.76f, 9.31f, 10.27f), // Refuge of Balgass, 250 resets
    ];

    /// <summary>
    /// Creates the default multipliers of the monsters.
    /// </summary>
    /// <returns>The default multipliers of the monsters.</returns>
    internal static List<DifficultyMultipliers> CreateDefaultMonsters() =>
    [
        new(43, 16.75f, 4.64f, 3.77f, 3.53f, 2.42f), // Golden Budge Dragon, Box of Kundun +1, 25 resets
        new(78, 11.88f, 4.06f, 3.39f, 17.66f, 14.5f), // Golden Goblin, Box of Kundun +1, 25 resets
        new(54, 10.53f, 3.92f, 1.7f, 3.53f, 2.42f), // Golden Soldier, Box of Kundun +1, 25 resets
        new(53, 15.33f, 8.44f, 2.91f, 9.09f, 5.06f), // Golden Titan, Box of Kundun +2, 75 resets
        new(81, 19.46f, 7.57f, 2.18f, 14.67f, 16.86f), // Golden Vepar, Box of Kundun +2, 75 resets
        new(83, 15.91f, 6.64f, 2.83f, 18.86f, 14.25f), // Golden Wheel, Box of Kundun +3, 125 resets
        new(79, 10.37f, 5.02f, 2.12f, 18.15f, 25.34f), // Golden Dragon, Box of Kundun +3, 125 resets
        new(80, 15.81f, 5.8f, 3.51f, 23.94f, 20.07f), // Golden Lizard King, Box of Kundun +4, 175 resets
        new(82, 13.55f, 7.56f, 4.19f, 28.05f, 20.23f), // Golden Tantallos, Box of Kundun +5, 225 resets
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
