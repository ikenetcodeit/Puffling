using System.Numerics;

namespace Puffling.Core.Behavior;

/// <summary>
/// Smaller, independent emergent behaviors that need to see the whole roster each tick but don't
/// belong in <see cref="GroupInteractionCoordinator"/>'s synchronized-routine machinery, since
/// they're one-sided reactions rather than a shared choreography the whole group holds to: an
/// awake companion occasionally pausing to watch another one sleep, and occasionally wandering
/// over to visit another companion's remembered favorite spot. The companion being watched or
/// visited never changes what it's doing.
/// </summary>
public sealed class EnvironmentalInteractionCoordinator
{
    private readonly Random _rng;
    private double _nextVisitAt;
    private readonly Dictionary<Companion, double> _watchCooldownUntil = new();

    public EnvironmentalInteractionCoordinator(int? seed = null) => _rng = seed is { } s ? new Random(s) : new Random();

    public void Tick(double now, IReadOnlyList<Companion> companions)
    {
        if (companions.Count < 2) return;
        TryWatchSleepers(now, companions);
        TryVisitFavoriteSpot(now, companions);
    }

    private void TryWatchSleepers(double now, IReadOnlyList<Companion> companions)
    {
        foreach (var sleeper in companions)
        {
            if (sleeper.CurrentBehavior != BehaviorKind.Sleeping) continue;
            foreach (var watcher in companions)
            {
                if (watcher == sleeper || !IsEligibleToWatch(watcher, sleeper)) continue;
                if (_watchCooldownUntil.TryGetValue(watcher, out var until) && now < until) continue;
                watcher.Brain.Interrupt(new WatchingSleepBehavior(sleeper));
                _watchCooldownUntil[watcher] = now + RandomRange(60, 120);
            }
        }
    }

    private static bool IsEligibleToWatch(Companion watcher, Companion sleeper) =>
        !watcher.Settings.Paused && !watcher.Settings.SleepMode && !watcher.IsSleeping &&
        watcher.CurrentMonitor.Id == sleeper.CurrentMonitor.Id &&
        watcher.CurrentBehavior is BehaviorKind.Idle or BehaviorKind.Wander or BehaviorKind.Curious &&
        Vector2.Distance(watcher.Position, sleeper.Position) < 260f;

    private void TryVisitFavoriteSpot(double now, IReadOnlyList<Companion> companions)
    {
        if (now < _nextVisitAt) return;
        _nextVisitAt = now + RandomRange(150, 300);

        var visitors = companions.Where(IsEligibleToVisit).ToList();
        if (visitors.Count == 0) return;
        var visitor = visitors[_rng.Next(visitors.Count)];

        // Stay on one monitor, same reasoning as GroupInteractionCoordinator.AllEligible: don't
        // steer anyone through a display gap just to see someone else's favorite spot.
        var owners = companions.Where(o => o != visitor && o.Brain.FavoriteSpot is { } spot &&
            o.Geometry.MonitorAt(spot).Id == visitor.CurrentMonitor.Id).ToList();
        if (owners.Count == 0) return;
        var owner = owners[_rng.Next(owners.Count)];

        visitor.Brain.Interrupt(new GoToSpotBehavior(owner.Brain.FavoriteSpot!.Value, BehaviorKind.VisitingFavoriteSpot));
    }

    private static bool IsEligibleToVisit(Companion c) =>
        c.Brain.CanMove && !c.Settings.Paused && !c.Settings.SleepMode && !c.IsSleeping &&
        c.CurrentBehavior is BehaviorKind.Idle or BehaviorKind.Wander or BehaviorKind.Curious;

    private float RandomRange(float min, float max) => min + (float)_rng.NextDouble() * (max - min);
}
