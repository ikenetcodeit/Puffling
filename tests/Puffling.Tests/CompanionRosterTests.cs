using System.Numerics;
using Puffling.Core;
using Puffling.Core.Behavior;
using Puffling.Core.Movement;
using Puffling.Core.Settings;

namespace Puffling.Tests;

public static class CompanionRosterTests
{
    [Test]
    public static void Settings_CapRosterAtThree_AndKeepPositionsIndependent()
    {
        var settings = new CompanionSettings
        {
            CompanionCount = 99,
            AdditionalPositions =
            [
                new(100, 100), new(200, 200), new(300, 300), new(float.NaN, 400)
            ]
        }.Normalize();
        Assert.Equal(CompanionSettings.MaxCompanions, settings.CompanionCount, "maximum roster");
        Assert.Equal(2, settings.AdditionalPositions.Count, "only two extra positions are kept");
        var clone = settings.Clone();
        clone.AdditionalPositions.Clear();
        Assert.Equal(2, settings.AdditionalPositions.Count, "cloned positions are independent");
        settings.CompanionCount = -4;
        Assert.Equal(1, settings.Normalize().CompanionCount, "at least one companion");
    }

    [Test]
    public static void NewCompanions_StartApart_AndBehaveIndependently()
    {
        var geometry = ScreenGeometry.Single(1920, 1080);
        var settings = new CompanionSettings { LastX = 900, LastY = 500 };
        var first = new Companion(settings.Clone(), geometry, seed: 11);
        Vector2 secondPosition = CompanionPlacement.Find(geometry, first.Position, [first.Position], first.Brain.Margin);
        var secondSettings = settings.Clone();
        secondSettings.LastX = secondPosition.X; secondSettings.LastY = secondPosition.Y;
        var second = new Companion(secondSettings, geometry, seed: 12);
        Vector2 thirdPosition = CompanionPlacement.Find(geometry, second.Position,
            [first.Position, second.Position], second.Brain.Margin);
        Assert.True(Vector2.Distance(first.Position, second.Position) >= 90, "second companion starts apart");
        Assert.True(Vector2.Distance(thirdPosition, first.Position) >= 90 &&
                    Vector2.Distance(thirdPosition, second.Position) >= 90, "third companion starts apart");
        Assert.True(geometry.IsComfortablyVisible(thirdPosition, second.Brain.Margin), "third companion is on screen");
        Vector2 edge = geometry.NearestSafePoint(new Vector2(1910, 1030), first.Brain.Margin);
        Vector2 besideEdge = CompanionPlacement.Find(geometry, edge, [edge], first.Brain.Margin);
        Assert.True(geometry.IsComfortablyVisible(besideEdge, first.Brain.Margin), "edge spawn is on screen");
        Assert.True(Vector2.Distance(edge, besideEdge) >= 90, "edge spawn is not stacked");

        first.Execute(CompanionCommand.Dance);
        Assert.Equal(BehaviorKind.Dance, first.CurrentBehavior, "selected companion dances");
        Assert.Equal(BehaviorKind.Idle, second.CurrentBehavior, "other companion keeps its own behavior");
    }
}
