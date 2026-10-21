// <copyright file="ZenByMonsterLevel.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.PlugIns.MoneyDrop;

/// <summary>
/// The monsters drop money by their level instead of by the experience of the kill, and the excellent option
/// for more money adds 40 % per item instead of multiplying the money by 1.4 per item.
/// </summary>
/// <remarks>
/// The money comes from the <see cref="MonsterLevelMoneyDropPlugIn"/> with its default configuration:
/// 0.019 × level ^ 3.5. Five excellent items with more money give 3 times the money (1 + 5 × 0.4), so that
/// 40 minutes of hunting in Raklion give about 1,000 million zen with them, and about 333 million without them.
/// Multiplied per item, five of them gave 5.4 times the money.
/// </remarks>
internal static class ZenByMonsterLevel
{
    /// <summary>
    /// The money rate which the excellent option adds per item.
    /// </summary>
    internal const float ExcellentMoneyRateBonus = 0.4f;

    /// <summary>
    /// Applies the changes to the game configuration.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    internal static void Apply(IContext context, GameConfiguration gameConfiguration)
    {
        ResetExperienceScheme.ActivatePlugIn(context, gameConfiguration, typeof(MonsterLevelMoneyDropPlugIn), new MonsterLevelMoneyDropConfiguration());
        MakeExcellentMoneyOptionAdditive(gameConfiguration);
    }

    /// <summary>
    /// Gets the excellent options which increase the money rate.
    /// </summary>
    /// <param name="gameConfiguration">The game configuration.</param>
    /// <returns>The excellent options which increase the money rate.</returns>
    internal static IEnumerable<IncreasableItemOption> GetExcellentMoneyOptions(GameConfiguration gameConfiguration)
    {
        return gameConfiguration.ItemOptions
            .SelectMany(definition => definition.PossibleOptions)
            .Where(option => object.Equals(option.OptionType, ItemOptionTypes.Excellent)
                             && option.PowerUpDefinition?.TargetAttribute?.Id == Stats.MoneyAmountRate.Id
                             && option.PowerUpDefinition!.Boost?.ConstantValue is not null);
    }

    private static void MakeExcellentMoneyOptionAdditive(GameConfiguration gameConfiguration)
    {
        foreach (var option in GetExcellentMoneyOptions(gameConfiguration))
        {
            var value = option.PowerUpDefinition!.Boost!.ConstantValue;
            value.Value = ExcellentMoneyRateBonus;
            value.AggregateType = AggregateType.AddRaw;
        }
    }
}
