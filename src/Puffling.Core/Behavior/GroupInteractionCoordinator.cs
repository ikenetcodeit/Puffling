using System.Numerics;
using Puffling.Core.Animation;
using Puffling.Core.Character;
using Puffling.Core.Movement;

namespace Puffling.Core.Behavior;

/// <summary>
/// Starts synchronized routines for the visible roster, then gives each pet its freedom back.
///
/// Two independent routines, so frequent small moments don't crowd out the rare, memorable ones:
///   - A "full" routine (everyone currently visible) is deliberately rare — every 3 to 5 minutes —
///     so it reads as a little surprise rather than a scripted loop that erodes each Puffling's
///     own personality.
///   - With all three present, a separate, somewhat more frequent "duo" routine picks two of the
///     three to chase, spar, or have a staring contest, leaving the third to do its own thing.
///     The pair is chosen with a personality-shaped weighting: red and blue (the composed leader
///     and the bubbly one) gravitate to each other most, red occasionally draws green in, and
///     blue and green pair up least — green mostly keeps to itself.
/// With exactly one Puffling visible, there is no group routine; see <see cref="Brain.Searching"/>
/// -style loneliness reactions instead (driven by <see cref="Brain.CompanionsPresent"/>).
/// </summary>
public sealed class GroupInteractionCoordinator
{
    private readonly Random _rng;
    private readonly List<Companion> _active = new(3);
    private int _lastCount = -1;
    private double _nextFullAt, _nextPairAt;
    private int _routineIndex, _pairRoutineIndex;

    public GroupInteractionCoordinator(int? seed = null) => _rng = seed is { } s ? new Random(s) : new Random();

    private static bool IsGroupBehavior(BehaviorKind kind) => kind is
        BehaviorKind.PairPlay or BehaviorKind.TrioPlay or BehaviorKind.GroupChase or
        BehaviorKind.GroupSpar or BehaviorKind.GroupPong or BehaviorKind.GroupStare;

    public void Tick(double now, IReadOnlyList<Companion> companions)
    {
        if (companions.Count != _lastCount)
        {
            Cancel();
            _lastCount = companions.Count;
            _nextFullAt = now + RandomRange(20, 60);
            _nextPairAt = now + RandomRange(30, 90);
            _routineIndex = 0;
            _pairRoutineIndex = 0;
        }

        if (_active.Count > 0)
        {
            if (_active.Any(p => !companions.Contains(p) || !IsGroupBehavior(p.CurrentBehavior)))
                Cancel();
            else
                return;
        }

        if (companions.Count == 2)
        {
            TryStartFull(now, companions);
        }
        else if (companions.Count == 3)
        {
            // The rare full-trio moment takes priority whenever it's due; the smaller duo
            // routine fills the quieter time in between.
            if (now >= _nextFullAt) TryStartFull(now, companions);
            else if (now >= _nextPairAt) TryStartPair(now, companions);
        }
    }

    private void TryStartFull(double now, IReadOnlyList<Companion> group)
    {
        if (now < _nextFullAt) return;
        if (!AllEligible(group)) { _nextFullAt = now + 5; return; }

        Vector2 centre = Centre(group, out float radius);
        GroupGameKind? game = (_routineIndex++ % (group.Count == 3 ? 4 : 3)) switch
        {
            1 => GroupGameKind.Chase,
            2 => GroupGameKind.Spar,
            3 => GroupGameKind.Pong,
            _ => null,
        };
        Start(group, centre, radius, game);
        _nextFullAt = now + RandomRange(180, 300);
        // Don't let a duo routine land immediately after the big trio moment.
        _nextPairAt = Math.Max(_nextPairAt, now + RandomRange(45, 90));
    }

    private void TryStartPair(double now, IReadOnlyList<Companion> companions)
    {
        if (now < _nextPairAt) return;
        var (a, b) = PickPair(companions);
        var pair = new[] { a, b };
        if (!AllEligible(pair)) { _nextPairAt = now + 5; return; }

        Vector2 centre = Centre(pair, out float radius);
        GroupGameKind? game = (_pairRoutineIndex++ % 3) switch
        {
            1 => GroupGameKind.Chase,
            2 => GroupGameKind.Stare,
            _ => null, // a little duet dance (PairPlay)
        };
        Start(pair, centre, radius, game);
        _nextPairAt = now + RandomRange(70, 150);
    }

    /// <summary>Picks two of the three companions to pair up, weighted by how well their
    /// temperaments get along: red and blue most, red and green sometimes, blue and green least.</summary>
    private (Companion, Companion) PickPair(IReadOnlyList<Companion> companions)
    {
        var combos = new (Companion A, Companion B, float Weight)[]
        {
            (companions[0], companions[1], Affinity(companions[0].Variant, companions[1].Variant)),
            (companions[0], companions[2], Affinity(companions[0].Variant, companions[2].Variant)),
            (companions[1], companions[2], Affinity(companions[1].Variant, companions[2].Variant)),
        };
        float total = combos.Sum(c => c.Weight);
        float r = (float)_rng.NextDouble() * total;
        foreach (var c in combos)
        {
            if (r < c.Weight) return (c.A, c.B);
            r -= c.Weight;
        }
        return (combos[0].A, combos[0].B);
    }

    private static float Affinity(CompanionVariant a, CompanionVariant b)
    {
        bool Has(CompanionVariant x, CompanionVariant y) => (a == x && b == y) || (a == y && b == x);
        if (Has(CompanionVariant.Red, CompanionVariant.Blue)) return 5f;
        if (Has(CompanionVariant.Red, CompanionVariant.Green)) return 3f;
        return 1f; // blue + green: least drawn together, green tends to do its own thing
    }

    private static bool AllEligible(IReadOnlyList<Companion> group)
    {
        // A shared routine stays on one monitor; do not steer anyone through a display gap.
        var monitor = group[0].CurrentMonitor;
        return group.All(p => p.CurrentMonitor.Id == monitor.Id && !p.Settings.Paused &&
            !p.Settings.SleepMode && !p.IsSleeping &&
            p.CurrentBehavior is BehaviorKind.Idle or BehaviorKind.Wander or BehaviorKind.Travel or BehaviorKind.Curious);
    }

    private static Vector2 Centre(IReadOnlyList<Companion> group, out float radius)
    {
        var monitor = group[0].CurrentMonitor;
        float dpi = monitor.Scale;
        radius = group.Count == 2 ? 52f : 66f;
        int margin = group.Max(p => p.Brain.Margin) + (int)MathF.Ceiling(radius * dpi);
        var area = monitor.WorkArea.Deflate(margin);
        Vector2 centre = Vector2.Zero;
        foreach (var p in group) centre += p.Position;
        return area.Clamp(centre / group.Count);
    }

    private void Start(IReadOnlyList<Companion> group, Vector2 centre, float radius, GroupGameKind? game)
    {
        for (int i = 0; i < group.Count; i++)
        {
            var p = group[i];
            p.Brain.Interrupt(game is { } kind
                ? new GroupGameBehavior(centre, i, group.Count, radius, kind)
                : new GroupPlayBehavior(centre, i, group.Count, radius));
            _active.Add(p);
        }
    }

    private float RandomRange(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

    private void Cancel()
    {
        foreach (var p in _active)
            if (IsGroupBehavior(p.CurrentBehavior))
                p.Brain.Interrupt(new IdleBehavior());
        _active.Clear();
    }
}
