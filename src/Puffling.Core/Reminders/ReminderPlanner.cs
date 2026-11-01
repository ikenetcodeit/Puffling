using System.Globalization;

namespace Puffling.Core.Reminders;

public sealed record CalendarReminder(string Id, string Title, DateTimeOffset StartsAt, bool IsAllDay);
public sealed record TodoReminder(string Id, string Title, DateTimeOffset? DueAt, bool Completed);
public sealed record ReminderSnapshot(IReadOnlyList<CalendarReminder> Events, IReadOnlyList<TodoReminder> Tasks);

/// <summary>Decides when a fetched Microsoft snapshot should become a short speech bubble.</summary>
public sealed class ReminderPlanner
{
    private DateOnly? _dailyShown;
    private readonly HashSet<string> _eventsShown = new();

    public IReadOnlyList<string> Due(DateTimeOffset now, ReminderSnapshot snapshot, string dailyTime, int eventLeadMinutes)
    {
        var messages = new List<string>();
        var local = now.ToLocalTime();
        var today = DateOnly.FromDateTime(local.Date);
        if (TimeOnly.TryParseExact(dailyTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            && _dailyShown != today && TimeOnly.FromDateTime(local.DateTime) >= time)
        {
            _dailyShown = today;
            var open = snapshot.Tasks.Where(t => !t.Completed)
                .OrderBy(t => t.DueAt?.ToLocalTime().Date ?? DateTime.MaxValue)
                .ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase).ToList();
            if (open.Count == 0) messages.Add("Your To Do list is clear today. Nice work!");
            else
            {
                string names = string.Join("; ", open.Take(3).Select(t => Clean(t.Title)));
                string more = open.Count > 3 ? $" (+{open.Count - 3} more)" : "";
                messages.Add($"You have {open.Count} to-do{(open.Count == 1 ? "" : "s")}: {names}{more}");
            }
        }

        int lead = Math.Clamp(eventLeadMinutes, 1, 120);
        foreach (var e in snapshot.Events.Where(e => !e.IsAllDay && e.StartsAt > now && e.StartsAt <= now.AddMinutes(lead))
                     .OrderBy(e => e.StartsAt))
        {
            string key = e.Id + "|" + e.StartsAt.UtcTicks;
            if (!_eventsShown.Add(key)) continue;
            int minutes = Math.Max(1, (int)Math.Ceiling((e.StartsAt - now).TotalMinutes));
            messages.Add($"Coming up in {minutes} min: {Clean(e.Title)}");
        }
        if (_eventsShown.Count > 1000) _eventsShown.Clear();
        return messages;
    }

    private static string Clean(string title)
    {
        string singleLine = string.Join(' ', (title ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return singleLine.Length == 0 ? "Untitled" : singleLine.Length <= 70 ? singleLine : singleLine[..67] + "…";
    }
}
