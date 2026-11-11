// <copyright file="SpectreNames.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Spectres;

/// <summary>
/// The names which the spectres remember when their owner finishes their story (the last victory against their rival):
/// a random name of the pools of the configuration (<see cref="SpectreConfiguration.MaleNames"/> and
/// <see cref="SpectreConfiguration.FemaleNames"/>), for the male and the female kinds.
/// </summary>
/// <remarks>
/// The owner character keeps the position of the name in its pool (see <see cref="SpectreProgress.NameAttributes"/>), so
/// names which are added at the end of a pool don't change the names which spectres already have.
/// </remarks>
public static class SpectreNames
{
    /// <summary>
    /// The default pool of the names of the male spectres.
    /// </summary>
    public const string DefaultMaleNames = "Aldric, Alarico, Anselmo, Arnaldo, Bertrán, Bruno, Conrado, Dagoberto, Edmundo, Fadrique, Fernán, Galván, Gerardo, Godofredo, Gonzalo, Guillermo, Gualterio, Hugo, Lotario, Manfredo, Martín, Nuño, Odón, Osvaldo, Pelayo, Ramiro, Raimundo, Ricardo, Rodrigo, Rolando, Sancho, Teobaldo, Tristán, Ulrico, Vermudo, Wilfredo";

    /// <summary>
    /// The default pool of the names of the female spectres.
    /// </summary>
    public const string DefaultFemaleNames = "Adela, Aldonza, Beatriz, Berenguela, Blanca, Brunilda, Catalina, Constanza, Elvira, Ermesinda, Estefanía, Gisela, Guiomar, Inés, Isolda, Jimena, Leonor, Matilde, Mencía, Oria, Sancha, Teresa, Urraca, Violante, Yolanda";

    /// <summary>
    /// Gets the names of a pool, which are separated by commas.
    /// </summary>
    /// <param name="pool">The pool.</param>
    /// <returns>The names.</returns>
    public static IReadOnlyList<string> Parse(string? pool)
        => (pool ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// Gets the name of the spectre of a kind of a character, if its story is finished.
    /// </summary>
    /// <param name="character">The owner character.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="configuration">The configuration of the spectres.</param>
    /// <returns>The name, or <c>null</c> if the spectre has no name yet.</returns>
    public static string? GetName(Character character, SpectreKind kind, SpectreConfiguration? configuration)
    {
        var position = (int)SpectreProgress.GetValue(character, SpectreProgress.NameAttributes[(int)kind]);
        if (position <= 0)
        {
            return null;
        }

        var names = GetPool(kind, configuration);
        return names.Count == 0 ? null : names[(position - 1) % names.Count];
    }

    /// <summary>
    /// Gives the spectre of a kind of the owner a random name of its pool, if it has none yet.
    /// </summary>
    /// <param name="owner">The owner.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="configuration">The configuration of the spectres.</param>
    /// <returns>The name, or <c>null</c> if the pool is empty or the name couldn't be kept.</returns>
    public static string? Assign(Player owner, SpectreKind kind, SpectreConfiguration? configuration)
    {
        if (owner.SelectedCharacter is not { } character)
        {
            return null;
        }

        if (GetName(character, kind, configuration) is { } existing)
        {
            return existing;
        }

        var names = GetPool(kind, configuration);
        if (names.Count == 0)
        {
            return null;
        }

        var position = Random.Shared.Next(names.Count) + 1;
        return SpectreProgress.SetValue(owner.GameContext.Configuration, owner.PersistenceContext, character, SpectreProgress.NameAttributes[(int)kind], position)
            ? names[position - 1]
            : null;
    }

    /// <summary>
    /// Gets the title of a spectre with a name, as the clients show it above the spectre, e.g. "Godofredo el Legendario".
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="name">The name.</param>
    /// <returns>The title.</returns>
    public static string GetTitle(SpectreKind kind, string name)
        => SpectreEvolution.IsFemale(kind) ? $"{name} la Legendaria" : $"{name} el Legendario";

    private static IReadOnlyList<string> GetPool(SpectreKind kind, SpectreConfiguration? configuration)
        => SpectreEvolution.IsFemale(kind)
            ? Parse(configuration?.FemaleNames ?? DefaultFemaleNames)
            : Parse(configuration?.MaleNames ?? DefaultMaleNames);
}
