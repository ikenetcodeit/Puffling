using System.Numerics;
using Puffling.Core.Animation;

namespace Puffling.Core.Behavior;

/// <summary>Starts one synchronized routine for the visible roster, then gives each pet its freedom back.</summary>
public sealed class GroupInteractionCoordinator
{
    private readonly List<Companion> _active = new(3);
    private int _lastCount = -1;
    private double _nextAt;
    private int _routineIndex;

    private static bool IsGroupBehavior(BehaviorKind kind) => kind is
        BehaviorKind.PairPlay or BehaviorKind.TrioPlay or BehaviorKind.GroupChase or
        BehaviorKind.GroupSpar or BehaviorKind.GroupPong;

    public void Tick(double now, IReadOnlyList<Companion> companions)
    {
        if (companions.Count != _lastCount)
        {
            Cancel();
            _lastCount = companions.Count;
            _nextAt = now + 8;
            _routineIndex = 0;
        }

        if (_active.Count > 0)
        {
            if (_active.Count != companions.Count ||
                _active.Any(p => !companions.Contains(p) || !IsGroupBehavior(p.CurrentBehavior)))
                Cancel();
            else return;
        }

        if (companions.Count is not (2 or 3) || now < _nextAt) return;

        // A shared dance stays on one monitor. Do not steer through the gap between displays.
        var monitor = companions[0].CurrentMonitor;
        if (companions.Any(p => p.CurrentMonitor.Id != monitor.Id || p.Settings.Paused ||
            p.Settings.SleepMode || p.IsSleeping ||
            p.CurrentBehavior is not (BehaviorKind.Idle or BehaviorKind.Wander or BehaviorKind.Travel or BehaviorKind.Curious)))
        {
            _nextAt = now + 5;
            return;
        }

        float dpi = monitor.Scale;
        float radius = companions.Count == 2 ? 52f : 66f;
        int margin = companions.Max(p => p.Brain.Margin) + (int)MathF.Ceiling(radius * dpi);
        var area = monitor.WorkArea.Deflate(margin);
        Vector2 centre = Vector2.Zero;
        foreach (var p in companions) centre += p.Position;
        centre = area.Clamp(centre / companions.Count);

        GroupGameKind? game = (_routineIndex++ % (companions.Count == 3 ? 4 : 3)) switch
        {
            1 => GroupGameKind.Chase,
            2 => GroupGameKind.Spar,
            3 => GroupGameKind.Pong,
            _ => null,
        };
        for (int i = 0; i < companions.Count; i++)
        {
            var p = companions[i];
            p.Brain.Interrupt(game is { } kind
                ? new GroupGameBehavior(centre, i, companions.Count, radius, kind)
                : new GroupPlayBehavior(centre, i, companions.Count, radius));
            _active.Add(p);
        }
        _nextAt = now + 45;
    }

    private void Cancel()
    {
        foreach (var p in _active)
            if (IsGroupBehavior(p.CurrentBehavior))
                p.Brain.Interrupt(new IdleBehavior());
        _active.Clear();
    }
}
