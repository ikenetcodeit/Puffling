using Puffling.Core.Reminders;
using Puffling.Core.Settings;

namespace Puffling.Tests;

public static class ReminderTests
{
    [Test]
    public static void DailySummary_UsesConfiguredTime_AndRunsOncePerDay()
    {
        var planner = new ReminderPlanner();
        var local = new DateTimeOffset(2026, 11, 1, 8, 59, 0, TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 11, 1)));
        var snapshot = new ReminderSnapshot([], [
            new TodoReminder("a", "Pay electricity", null, false),
            new TodoReminder("b", "Already done", null, true)
        ]);
        Assert.Equal(0, planner.Due(local, snapshot, "09:00", 15).Count, "before chosen time");
        var due = planner.Due(local.AddMinutes(1), snapshot, "09:00", 15);
        Assert.Equal(1, due.Count, "daily summary");
        Assert.True(due[0].Contains("Pay electricity") && !due[0].Contains("Already done"), "only open tasks are spoken");
        Assert.Equal(0, planner.Due(local.AddHours(1), snapshot, "09:00", 15).Count, "no duplicate");
        Assert.Equal(1, planner.Due(local.AddDays(1).AddMinutes(1), snapshot, "09:00", 15).Count, "next day");
    }

    [Test]
    public static void EventReminder_UsesLeadTime_AndDoesNotRepeat()
    {
        var planner = new ReminderPlanner();
        var now = DateTimeOffset.Now;
        var snapshot = new ReminderSnapshot([
            new CalendarReminder("meeting", "Project sync", now.AddMinutes(20), false),
            new CalendarReminder("holiday", "Holiday", now.AddMinutes(5), true)
        ], []);
        Assert.True(!planner.Due(now, snapshot, "-", 15).Any(), "outside lead window and all-day ignored");
        var due = planner.Due(now.AddMinutes(6), snapshot, "-", 15);
        Assert.Equal(1, due.Count, "meeting reminder");
        Assert.True(due[0].Contains("Project sync"), "event title");
        Assert.Equal(0, planner.Due(now.AddMinutes(7), snapshot, "-", 15).Count, "no repeat");
    }

    [Test]
    public static void Settings_ClampReminderInputs()
    {
        var settings = new CompanionSettings { DailyReminderTime = "banana", EventReminderMinutes = 999, MicrosoftClientId = "not-a-guid" }.Normalize();
        Assert.Equal("09:00", settings.DailyReminderTime, "daily time fallback");
        Assert.Equal(120, settings.EventReminderMinutes, "lead limit");
        Assert.Equal("", settings.MicrosoftClientId, "invalid client ID");
    }
}
