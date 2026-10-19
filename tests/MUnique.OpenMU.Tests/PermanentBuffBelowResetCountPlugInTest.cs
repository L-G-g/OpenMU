// <copyright file="PermanentBuffBelowResetCountPlugInTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.Threading;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.Resets;

/// <summary>
/// Tests for the <see cref="PermanentBuffBelowResetCountPlugIn"/>.
/// </summary>
[TestFixture]
public class PermanentBuffBelowResetCountPlugInTest
{
    private const short EffectNumber = 3;

    private const float DefenseBoost = 50;

    /// <summary>
    /// Tests that a character with less resets than configured gets the buff without an end, with its power-ups.
    /// </summary>
    [Test]
    public async ValueTask BuffIsAddedBelowResetCountAsync()
    {
        var player = await CreatePlayerAsync(resets: 9).ConfigureAwait(false);
        var defenseBefore = player.Attributes![Stats.DefenseFinal];

        await CreatePlugIn().ObjectAddedToMapAsync(player.CurrentMap!, player).ConfigureAwait(false);

        Assert.That(player.MagicEffectList.TryGetEffect(EffectNumber, out var effect), Is.True);
        Assert.That(effect!.Duration, Is.EqualTo(Timeout.InfiniteTimeSpan));
        Assert.That(player.Attributes[Stats.DefenseFinal], Is.EqualTo(defenseBefore + DefenseBoost));
        await player.MagicEffectList.ClearAllEffectsAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Tests that a character which reached the reset count doesn't get the buff.
    /// </summary>
    [Test]
    public async ValueTask BuffIsNotAddedAtResetCountAsync()
    {
        var player = await CreatePlayerAsync(resets: 10).ConfigureAwait(false);

        await CreatePlugIn().ObjectAddedToMapAsync(player.CurrentMap!, player).ConfigureAwait(false);

        Assert.That(player.MagicEffectList.ContainsEffect(EffectNumber), Is.False);
    }

    /// <summary>
    /// Tests that the buff is removed when the character reached the reset count.
    /// </summary>
    [Test]
    public async ValueTask BuffIsRemovedAtResetCountAsync()
    {
        var player = await CreatePlayerAsync(resets: 9).ConfigureAwait(false);
        var defenseBefore = player.Attributes![Stats.DefenseFinal];
        var plugIn = CreatePlugIn();
        await plugIn.ObjectAddedToMapAsync(player.CurrentMap!, player).ConfigureAwait(false);

        player.Attributes[Stats.Resets] = 10;
        await plugIn.ObjectAddedToMapAsync(player.CurrentMap!, player).ConfigureAwait(false);

        Assert.That(player.MagicEffectList.ContainsEffect(EffectNumber), Is.False);
        Assert.That(player.Attributes[Stats.DefenseFinal], Is.EqualTo(defenseBefore));
    }

    /// <summary>
    /// Tests that a buff with a duration, e.g. from the NPC, doesn't run out anymore.
    /// </summary>
    [Test]
    public async ValueTask BuffWithDurationBecomesPermanentAsync()
    {
        var player = await CreatePlayerAsync(resets: 0).ConfigureAwait(false);
        var definition = player.GameContext.Configuration.MagicEffects.Single();
        var npcBuff = new MagicEffect(
            TimeSpan.FromHours(1),
            definition,
            definition.PowerUpDefinitions.Select(p => new MagicEffect.ElementWithTarget(player.Attributes!.CreateElement(p), p.TargetAttribute!)).ToArray());
        await player.MagicEffectList.AddEffectAsync(npcBuff).ConfigureAwait(false);

        await CreatePlugIn().ObjectAddedToMapAsync(player.CurrentMap!, player).ConfigureAwait(false);

        Assert.That(player.MagicEffectList.TryGetEffect(EffectNumber, out var effect), Is.True);
        Assert.That(effect!.Duration, Is.EqualTo(Timeout.InfiniteTimeSpan));
        await player.MagicEffectList.ClearAllEffectsAsync().ConfigureAwait(false);
    }

    private static PermanentBuffBelowResetCountPlugIn CreatePlugIn()
    {
        return new PermanentBuffBelowResetCountPlugIn { Configuration = new PermanentBuffBelowResetCountConfiguration { EffectNumber = EffectNumber, ResetCount = 10 } };
    }

    private static async ValueTask<Player> CreatePlayerAsync(int resets)
    {
        var gameContext = GameContextTestHelper.CreateGameContext();
        var effectDefinition = new Persistence.BasicModel.MagicEffectDefinition { Number = EffectNumber, Name = "Elf Soldier Buff", InformObservers = true, StopByDeath = true };
        effectDefinition.PowerUpDefinitions.Add(new Persistence.BasicModel.PowerUpDefinition
        {
            TargetAttribute = Stats.DefenseFinal,
            Boost = new Persistence.BasicModel.PowerUpDefinitionValue
            {
                ConstantValue = { Value = DefenseBoost, AggregateType = AggregateType.AddFinal },
            },
        });
        gameContext.Configuration.MagicEffects.Add(effectDefinition);

        var player = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        player.Attributes![Stats.Resets] = resets;
        return player;
    }
}
