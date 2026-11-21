// <copyright file="PeriodicTaskConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.PeriodicTasks;

using MUnique.OpenMU.Interfaces;

/// <summary>
/// Configuration for the <see cref="PeriodicTaskBasePlugIn{TConfiguration,TState}"/>.
/// </summary>
/// <remarks>
/// Display order values are spaced by inheritance level, so adding a property never
/// requires renumbering another level: this class uses 1–99, direct subclasses
/// 100–199, their subclasses 200–299, and so on.
/// </remarks>
public class PeriodicTaskConfiguration
{
    /// <summary>
    /// Gets or sets a time delay.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.PeriodicTaskConfiguration_PreStartMessageDelay_Name), Order = 1)]
    public TimeSpan PreStartMessageDelay { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Gets or sets the minimum time of the task duration.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.PeriodicTaskConfiguration_TaskDuration_Name), Order = 2)]
    public TimeSpan TaskDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the text which prints as a golden message in the game when task starts.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.PeriodicTaskConfiguration_StartMessage_Name), Order = 3)]
    public LocalizedString StartMessage { get; set; }

    /// <summary>
    /// Gets or sets the text which prints as a golden message in the game when task ends.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.PeriodicTaskConfiguration_EndMessage_Name), Order = 4)]
    public LocalizedString EndMessage { get; set; }

    /// <summary>
    /// Gets or sets a timetable for the event.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.PeriodicTaskConfiguration_Timetable_Name), Order = 5)]
    public IList<TimeOnly> Timetable { get; set; } = new List<TimeOnly>();

    /// <summary>
    /// Gets or sets the identifiers of the game servers on which the event runs.
    /// An empty list means the event runs on all game servers.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.PeriodicTaskConfiguration_AllowedServerIds_Name), Description = nameof(PlugInResources.PeriodicTaskConfiguration_AllowedServerIds_Description), Order = 6)]
    public IList<byte> AllowedServerIds { get; set; } = new List<byte>();

    /// <summary>
    /// Gets or sets the length of a repeating cycle of the event. When it's set (more than zero), the task starts once
    /// per cycle, at <see cref="CycleOffset"/> after the start of the cycle, and the <see cref="Timetable"/> is ignored.
    /// The cycles are counted from <see cref="CycleEpochUtc"/>, so that the starts don't depend on the day and stay the
    /// same after a restart. Several events with the same cycle length and different offsets follow each other.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.PeriodicTaskConfiguration_CycleLength_Name), Description = nameof(PlugInResources.PeriodicTaskConfiguration_CycleLength_Description), Order = 7)]
    public TimeSpan CycleLength { get; set; }

    /// <summary>
    /// Gets or sets the time after the start of each cycle at which the task starts, see <see cref="CycleLength"/>.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.PeriodicTaskConfiguration_CycleOffset_Name), Description = nameof(PlugInResources.PeriodicTaskConfiguration_CycleOffset_Description), Order = 8)]
    public TimeSpan CycleOffset { get; set; }

    /// <summary>
    /// Gets the time from which the cycles of <see cref="CycleLength"/> are counted.
    /// </summary>
    public static DateTime CycleEpochUtc { get; } = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Gets a value indicating whether the task starts by a repeating cycle instead of the <see cref="Timetable"/>.
    /// </summary>
    protected bool UsesCycle => this.CycleLength > TimeSpan.Zero;

    /// <summary>
    /// Generate a sequence of time points like [00:00, 00:01, ...].
    /// </summary>
    /// <param name="duration">The duration.</param>
    /// <param name="startLimit">The start limit, inclusive.</param>
    /// <param name="endLimit">The end limit, exclusive.</param>
    public static IEnumerable<TimeOnly> GenerateTimeSequence(TimeSpan duration, TimeOnly? startLimit = null, TimeOnly? endLimit = null)
    {
        var limit = endLimit?.ToTimeSpan() ?? TimeSpan.FromDays(1);
        var current = startLimit?.ToTimeSpan() ?? TimeSpan.Zero;

        while (current < limit)
        {
            yield return TimeOnly.FromTimeSpan(current);
            current = current.Add(duration);
        }
    }

    /// <summary>
    /// Determines whether the event runs on the game server with the specified identifier.
    /// A missing (<c>null</c>) or empty list means the event runs on all servers.
    /// </summary>
    /// <param name="serverId">The identifier of the game server.</param>
    /// <returns><c>true</c> if the event runs on the server; otherwise, <c>false</c>.</returns>
    public bool IsEnabledOnServer(byte serverId)
    {
        return this.AllowedServerIds is null or { Count: 0 } || this.AllowedServerIds.Contains(serverId);
    }

    /// <summary>
    /// Check if current time is OK for starting an invasion.
    /// </summary>
    /// <param name="serverTimeZone">
    /// The server time zone used to interpret the <see cref="Timetable"/> times of day.
    /// Pass <see cref="TimeZoneInfo.Utc"/> to keep the previous UTC-based behavior.
    /// </param>
    /// <returns>Returns true if the invasion can be started.</returns>
    public virtual bool IsItTimeToStart(TimeZoneInfo serverTimeZone)
    {
        if (this.UsesCycle)
        {
            // Between the start of the cycle and 5 seconds after it.
            var sinceStart = this.GetTimeSinceCycleStart(DateTime.UtcNow);
            return sinceStart >= TimeSpan.Zero && sinceStart <= TimeSpan.FromSeconds(5);
        }

        if (this.Timetable.Count == 0)
        {
            return false;
        }

        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, serverTimeZone);
        var nowTime = TimeOnly.FromDateTime(nowLocal);
        var earlier = nowTime.Add(TimeSpan.FromSeconds(-5));

        // For example, p = 00:00. Check that time between 00:00:00 and 00:00:05
        return this.Timetable.Any(p => p.IsBetween(earlier, nowTime));
    }

    /// <summary>
    /// Gets the next start time of the task, according to the <see cref="Timetable"/>.
    /// </summary>
    /// <param name="utcNow">The current time, in UTC.</param>
    /// <param name="serverTimeZone">The time zone of the server, in which the times of the <see cref="Timetable"/> are defined.</param>
    /// <returns>The next start time, in UTC; or <see langword="null"/>, if the timetable is empty.</returns>
    public DateTime? GetNextStartUtc(DateTime utcNow, TimeZoneInfo serverTimeZone)
    {
        // A context without a time zone (e.g. a mocked one) uses UTC.
        serverTimeZone ??= TimeZoneInfo.Utc;
        if (this.UsesCycle)
        {
            var sinceStart = this.GetTimeSinceCycleStart(utcNow);
            return utcNow - sinceStart + this.CycleLength;
        }

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcNow, serverTimeZone));
        DateTime? result = null;

        // Today, the remaining times of the timetable can still come; otherwise the first one of tomorrow.
        foreach (var date in new[] { today, today.AddDays(1) })
        {
            foreach (var time in this.Timetable)
            {
                var startUtc = TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(time, DateTimeKind.Unspecified), serverTimeZone);
                if (startUtc > utcNow && (result is null || startUtc < result))
                {
                    result = startUtc;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Gets the time since the last start of the cycle, see <see cref="CycleLength"/>: from zero up to the cycle length.
    /// </summary>
    /// <param name="utcNow">The current time, in UTC.</param>
    /// <returns>The time since the last start.</returns>
    private TimeSpan GetTimeSinceCycleStart(DateTime utcNow)
    {
        var ticks = (utcNow - CycleEpochUtc - this.CycleOffset).Ticks % this.CycleLength.Ticks;
        if (ticks < 0)
        {
            ticks += this.CycleLength.Ticks;
        }

        return TimeSpan.FromTicks(ticks);
    }
}
