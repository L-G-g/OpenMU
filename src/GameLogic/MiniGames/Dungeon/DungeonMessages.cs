// <copyright file="DungeonMessages.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.MiniGames.Dungeon;

/// <summary>
/// The messages of the dungeon, in Spanish for the players of PeroniaMU.
/// They have no accents, because the game client doesn't show them.
/// </summary>
internal static class DungeonMessages
{
    /// <summary>The dungeon isn't configured.</summary>
    public const string NotAvailable = "La dungeon no esta disponible.";

    /// <summary>A party member who isn't the leader talked to the NPC.</summary>
    public const string OnlyPartyLeader = "Solo el lider de la party puede abrir la dungeon.";

    /// <summary>A member can't enter, so nobody enters.</summary>
    public const string EntryCancelled = "Entrada a la dungeon cancelada: todos los miembros de la party tienen que poder entrar.";

    /// <summary>The entry of a member failed after the checks.</summary>
    public const string EntryFailed = "No pudiste entrar a la dungeon. Si tu party acaba de salir, espera un minuto a que se cierre.";

    /// <summary>The member isn't in the game.</summary>
    public const string ProblemNotInGame = "no esta en el juego";

    /// <summary>The member is in another event.</summary>
    public const string ProblemInEvent = "esta en otro evento";

    /// <summary>The member is dead.</summary>
    public const string ProblemDead = "esta muerto";

    /// <summary>The level of the member doesn't fit, {0} minimum and {1} maximum level.</summary>
    public const string ProblemLevelFormat = "necesita nivel {0} a {1}";

    /// <summary>The member is a player killer.</summary>
    public const string ProblemPlayerKiller = "es PK";

    /// <summary>The member entered less than the cooldown ago, {0} hours and {1} minutes remaining.</summary>
    public const string ProblemCooldownFormat = "ya entro hoy, puede volver en {0} h {1} min";

    /// <summary>The dungeon started, {0} is the duration in minutes.</summary>
    public const string StartedFormat = "La dungeon empezo! Tienen {0} minutos.";

    /// <summary>The remaining minutes, {0}.</summary>
    public const string RemainingMinutesFormat = "Quedan {0} minutos de dungeon.";

    /// <summary>One minute remains.</summary>
    public const string RemainingOneMinute = "Queda 1 minuto de dungeon.";

    /// <summary>The players killed a part of the monsters of the stage: {0} kills of {1} monsters named {2}.</summary>
    public const string KillProgressFormat = "{0}/{1} {2}";

    /// <summary>The players killed the monsters of the stage ({0} of them), but the boss {1} appears only in {2} seconds.</summary>
    public const string StageReadyFormat = "Ya mataron {0}! {1} aparece en {2} segundos.";

    /// <summary>The boss of the last stage is dead.</summary>
    public const string Completed = "Completaron la dungeon! Aprovechen el tiempo que queda.";

    /// <summary>The boss appeared, {0} is its name, {1} and {2} its coordinates.</summary>
    public const string BossAppearedFormat = "Aparecio {0} en {1}, {2}!";

    /// <summary>The boss died, {0} is its name.</summary>
    public const string BossKilledFormat = "Derrotaron a {0}!";

    /// <summary>The time is over.</summary>
    public const string TimeOver = "Se termino el tiempo de la dungeon.";

    /// <summary>A roll started: {0} id, {1} item, {2} seconds.</summary>
    public const string RollStartedFormat = "Botin {0}: {1}. Escribi /need {0}, /greed {0} o /pass {0} ({2} s).";

    /// <summary>A roll started for a player who can't use the item: {0} id, {1} item, {2} seconds.</summary>
    public const string RollStartedWithoutNeedFormat = "Botin {0}: {1}. Tu clase no lo puede usar: /greed {0} o /pass {0} ({2} s).";

    /// <summary>The choice of the player was taken: {0} choice, {1} id.</summary>
    public const string ChoiceTakenFormat = "Elegiste {0} para el botin {1}.";

    /// <summary>The player can't choose need for this item, {0} id.</summary>
    public const string NeedNotAllowedFormat = "Tu clase no puede usar el botin {0}, solo greed o pass.";

    /// <summary>There is no open roll for the player.</summary>
    public const string NoOpenRoll = "No hay ningun botin esperando tu eleccion.";

    /// <summary>The command is only for the dungeon.</summary>
    public const string NotInDungeon = "Este comando es para repartir el botin de la dungeon.";

    /// <summary>The winner of a roll: {0} id, {1} item, {2} winner, {3} the choices of all.</summary>
    public const string RollWonFormat = "Botin {0} ({1}): gana {2}. {3}";

    /// <summary>All passed: {0} id, {1} item.</summary>
    public const string RollAllPassedFormat = "Botin {0} ({1}): todos pasaron, queda en el piso para cualquiera.";

    /// <summary>The won item didn't fit into the inventory, {0} item.</summary>
    public const string InventoryFullFormat = "Tu inventario esta lleno: {0} quedo a tus pies.";
}
