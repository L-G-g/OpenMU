// <copyright file="MarketExtensions.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.Extensions.ModelBuilder;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MUnique.OpenMU.Persistence.EntityFramework.Model;

/// <summary>
/// Extensions for the item market related <see cref="EntityTypeBuilder"/>s.
/// </summary>
internal static class MarketExtensions
{
    /// <summary>
    /// Applies the settings for the <see cref="MarketOrder"/> entity.
    /// </summary>
    /// <param name="builder">The builder.</param>
    public static void Apply(this EntityTypeBuilder<MarketOrder> builder)
    {
        builder.HasIndex(order => new { order.State, order.Kind });
        builder.HasIndex(order => order.AccountId);
        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(order => order.AccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    /// <summary>
    /// Applies the settings for the <see cref="MarketTrade"/> entity.
    /// </summary>
    /// <param name="builder">The builder.</param>
    public static void Apply(this EntityTypeBuilder<MarketTrade> builder)
    {
        builder.HasIndex(trade => trade.CreatedAt);
        builder.HasIndex(trade => new { trade.ItemGroup, trade.ItemNumber });
    }
}
