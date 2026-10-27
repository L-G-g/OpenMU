// <copyright file="ElfSoldierBuffEffect.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Attributes;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Persistence.Initialization.Properties;
using MUnique.OpenMU.Persistence.Initialization.Skills;

/// <summary>
/// The magic effect of the buff of the Elf Soldier: more defense and damage, depending on the character level.
/// </summary>
internal static class ElfSoldierBuffEffect
{
    /// <summary>
    /// Gets the effect of the game configuration, or creates it if it doesn't exist.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    /// <returns>The effect.</returns>
    internal static MagicEffectDefinition GetOrCreate(IContext context, GameConfiguration gameConfiguration)
    {
        if (gameConfiguration.MagicEffects.FirstOrDefault(e => e.Number == (short)MagicEffectNumber.ElfSoldierBuff) is { } existing)
        {
            return existing;
        }

        var buffEffect = context.CreateNew<MagicEffectDefinition>();
        gameConfiguration.MagicEffects.Add(buffEffect);
        buffEffect.Number = (short)MagicEffectNumber.ElfSoldierBuff;
        buffEffect.Name = LocalizedString.FromResource(() => MagicEffectNames.ElfSoldierBuff);
        buffEffect.InformObservers = true;
        buffEffect.StopByDeath = true;

        // Duration: 60 minutes
        buffEffect.Duration = context.CreateNew<PowerUpDefinitionValue>();
        buffEffect.Duration.ConstantValue.Value = 3600;

        // Defense boost: 50 + (Level / 5)
        var defensePowerUp = context.CreateNew<PowerUpDefinition>();
        defensePowerUp.TargetAttribute = Stats.DefenseFinal.GetPersistent(gameConfiguration);
        defensePowerUp.Boost = context.CreateNew<PowerUpDefinitionValue>();
        defensePowerUp.Boost.ConstantValue.Value = 50;
        defensePowerUp.Boost.ConstantValue.AggregateType = AggregateType.AddFinal;
        var defensePerLevel = context.CreateNew<AttributeRelationship>();
        defensePerLevel.InputAttribute = Stats.Level.GetPersistent(gameConfiguration);
        defensePerLevel.InputOperand = 1f / 5;
        defensePerLevel.InputOperator = InputOperator.Multiply;
        defensePowerUp.Boost.RelatedValues.Add(defensePerLevel);
        buffEffect.PowerUpDefinitions.Add(defensePowerUp);

        // Damage boost: 45 + (Level / 3)
        var damagePowerUp = context.CreateNew<PowerUpDefinition>();
        damagePowerUp.TargetAttribute = Stats.GreaterDamageBonus.GetPersistent(gameConfiguration);
        damagePowerUp.Boost = context.CreateNew<PowerUpDefinitionValue>();
        damagePowerUp.Boost.ConstantValue.Value = 45;
        damagePowerUp.Boost.ConstantValue.AggregateType = AggregateType.AddRaw;
        var damagePerLevel = context.CreateNew<AttributeRelationship>();
        damagePerLevel.InputAttribute = Stats.Level.GetPersistent(gameConfiguration);
        damagePerLevel.InputOperand = 1f / 3;
        damagePerLevel.InputOperator = InputOperator.Multiply;
        damagePowerUp.Boost.RelatedValues.Add(damagePerLevel);
        buffEffect.PowerUpDefinitions.Add(damagePowerUp);

        return buffEffect;
    }
}
