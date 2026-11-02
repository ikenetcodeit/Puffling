using System.Numerics;
using Puffling.Core;
using Puffling.Core.Animation;
using Puffling.Core.Behavior;
using Puffling.Core.Character;
using Puffling.Core.Interaction;
using Puffling.Core.Movement;
using Puffling.Core.Settings;

namespace Puffling.Tests;

public static class CompanionRosterTests
{
    [Test]
    public static void GroupPlay_PairAndTrio_UseDifferentSynchronizedRoutines()
    {
        var geometry = ScreenGeometry.Single(1920, 1080);
        var pair = MakePets(geometry, 2);
        var pairCoordinator = new GroupInteractionCoordinator();
        pairCoordinator.Tick(0, pair);
        pairCoordinator.Tick(8, pair);
        Assert.True(pair.All(p => p.CurrentBehavior == BehaviorKind.PairPlay), "two pets begin the pair routine together");
        Assert.True(pair.All(p => p.Animator.Clip.Name == "Pair play"), "pair has its own clip");
        Assert.True(pair.All(p => p.Animator.Clip.Evaluate(0.5f, default).Particles == ParticleKind.Hearts), "pair shows hearts");
        Assert.True(pair[0].Animator.Clip.Evaluate(0.5f, default).OffsetY !=
                    pair[1].Animator.Clip.Evaluate(0.5f, default).OffsetY, "pair hops alternate");

        var trio = MakePets(geometry, 3);
        var trioCoordinator = new GroupInteractionCoordinator();
        trioCoordinator.Tick(0, trio);
        trioCoordinator.Tick(8, trio);
        Assert.True(trio.All(p => p.CurrentBehavior == BehaviorKind.TrioPlay), "three pets begin the carousel together");
        Assert.True(trio.All(p => p.Animator.Clip.Name == "Trio carousel"), "trio has its own clip");
        Assert.True(trio.All(p => p.Animator.Clip.Evaluate(0.5f, default).Particles == ParticleKind.Notes), "trio shows notes");
        Assert.True(trio.Select(p => p.Animator.Clip.Evaluate(0.5f, default).OffsetY).Distinct().Count() == 3,
                    "three jumps are staggered");

        var input = new FrameInputs(new Vector2(3000, 3000), 20);
        for (int frame = 0; frame < 70; frame++)
        {
            foreach (var p in trio) p.Tick(0.1f, input);
            Assert.True(trio.All(p => geometry.IsComfortablyVisible(p.Position, p.Brain.Margin)),
                        "carousel stays on screen");
        }
        Assert.True(Vector2.Distance(trio[0].Position, trio[1].Position) > 35, "carousel keeps pets apart");
    }

    [Test]
    public static void GroupPlay_ClickStopsTheWholeGroup_AndSoloDoesNotStart()
    {
        var pets = MakePets(ScreenGeometry.Single(1920, 1080), 2);
        var coordinator = new GroupInteractionCoordinator();
        coordinator.Tick(0, pets);
        coordinator.Tick(8, pets);
        pets[0].Execute(CompanionCommand.Happy);
        coordinator.Tick(8.1, pets);
        Assert.Equal(BehaviorKind.Happy, pets[0].CurrentBehavior, "clicked pet responds normally");
        Assert.Equal(BehaviorKind.Idle, pets[1].CurrentBehavior, "partner stops playing");

        coordinator.Tick(50, pets.Take(1).ToArray());
        Assert.True(pets[0].CurrentBehavior != BehaviorKind.PairPlay, "one pet has no group routine");
    }

    [Test]
    public static void GroupPlay_WaitsWhenPetsAreOnDifferentMonitors()
    {
        var geometry = new ScreenGeometry([
            new MonitorInfo("left", new RectI(0, 0, 800, 600), new RectI(0, 0, 800, 600), 1, true),
            new MonitorInfo("right", new RectI(800, 0, 1600, 600), new RectI(800, 0, 1600, 600), 1, false),
        ]);
        var pets = new[]
        {
            new Companion(new CompanionSettings { LastX = 400, LastY = 300 }, geometry, 1),
            new Companion(new CompanionSettings { LastX = 1200, LastY = 300 }, geometry, 2),
        };
        var coordinator = new GroupInteractionCoordinator();
        coordinator.Tick(0, pets);
        coordinator.Tick(8, pets);
        Assert.True(pets.All(p => p.CurrentBehavior == BehaviorKind.Idle), "no group steers between displays");
    }

    private static Companion[] MakePets(ScreenGeometry geometry, int count)
    {
        var pets = new Companion[count];
        for (int i = 0; i < count; i++)
            pets[i] = new Companion(new CompanionSettings { LastX = 850 + i * 120, LastY = 500 }, geometry,
                i + 100, CompanionVariants.ForSlot(i));
        return pets;
    }

    [Test]
    public static void RosterSlots_HaveDistinctPersistentAppearanceAndRhythm()
    {
        var geometry = ScreenGeometry.Single(1920, 1080);
        var variants = Enumerable.Range(0, 3).Select(CompanionVariants.ForSlot).ToArray();
        Assert.Equal(3, variants.Distinct().Count(), "three distinct roster variants");
        Assert.True(variants.Select(v => v.IdleDuration()).Distinct().Count() == 3, "idle rhythm differs");
        Assert.True(variants.Select(v => v.WanderSpeed()).Distinct().Count() == 3, "movement rhythm differs");

        var settings = new CompanionSettings { CompanionCount = 3, LastX = 800, LastY = 500,
            AdditionalPositions = [new(900, 500), new(1000, 500)] };
        var firstRun = Enumerable.Range(0, settings.CompanionCount)
            .Select(i => new Companion(settings.Clone(), geometry, seed: 100 + i, variant: variants[i])).ToArray();
        var restored = Enumerable.Range(0, settings.Clone().Normalize().CompanionCount)
            .Select(i => new Companion(settings.Clone(), geometry, seed: 200 + i,
                variant: CompanionVariants.ForSlot(i))).ToArray();
        var firstHashes = firstRun.Select(p => p.Render().Frame.ComputeHash()).ToArray();
        var restoredHashes = restored.Select(p => p.Render().Frame.ComputeHash()).ToArray();
        Assert.True(firstHashes.Distinct().Count() == 3, "styles look different at desktop size");
        Assert.True(firstHashes.SequenceEqual(restoredHashes), "roster styles persist across recreation");
    }

    [Test]
    public static void EachVariant_HasItsOwnRequestedAction()
    {
        var geometry = ScreenGeometry.Single(1920, 1080);
        var names = new HashSet<string>();
        for (int i = 0; i < 3; i++)
        {
            var pet = new Companion(new CompanionSettings(), geometry, seed: i + 1,
                variant: CompanionVariants.ForSlot(i));
            pet.Execute(CompanionCommand.Signature);
            Assert.Equal(BehaviorKind.Signature, pet.CurrentBehavior, "signature action starts");
            names.Add(pet.Animator.Clip.Name);
        }
        Assert.Equal(3, names.Count, "each variant has a different move");
    }

    [Test]
    public static void EachVariant_ReactsDifferentlyToAClick()
    {
        var expected = new[] { BehaviorKind.Curious, BehaviorKind.Happy, BehaviorKind.Excited };
        for (int i = 0; i < 3; i++)
        {
            var pet = new Companion(new CompanionSettings(), ScreenGeometry.Single(1920, 1080),
                seed: i + 1, variant: CompanionVariants.ForSlot(i));
            pet.Brain.Handle(new InteractionEvent(InteractionKind.Click, pet.Position, Vector2.Zero));
            Assert.Equal(expected[i], pet.CurrentBehavior, $"{pet.Variant} click response");
        }
    }

    [Test]
    public static void GroupGames_CycleAndStayOnScreen()
    {
        var geometry = ScreenGeometry.Single(1920, 1080);
        foreach (int count in new[] { 2, 3 })
        {
            var pets = MakePets(geometry, count);
            var coordinator = new GroupInteractionCoordinator();
            var expected = count == 2
                ? new[] { BehaviorKind.PairPlay, BehaviorKind.GroupChase, BehaviorKind.GroupSpar }
                : new[] { BehaviorKind.TrioPlay, BehaviorKind.GroupChase, BehaviorKind.GroupSpar, BehaviorKind.GroupPong };
            coordinator.Tick(0, pets);
            for (int routine = 0; routine < expected.Length; routine++)
            {
                coordinator.Tick(8 + routine * 60, pets);
                Assert.True(pets.All(p => p.CurrentBehavior == expected[routine]), $"routine {routine} starts together");
                for (int frame = 0; frame < 80; frame++)
                {
                    foreach (var pet in pets)
                    {
                        pet.Tick(0.1f, new FrameInputs(new Vector2(3000, 3000), 20));
                        Assert.True(geometry.IsComfortablyVisible(pet.Position, pet.Brain.Margin),
                            $"{expected[routine]} stays on screen");
                    }
                }
                pets[0].Execute(CompanionCommand.Happy);
                coordinator.Tick(8.1 + routine * 60, pets);
                Assert.True(pets.Skip(1).All(p => p.CurrentBehavior == BehaviorKind.Idle),
                    $"{expected[routine]} yields to direct attention");
                foreach (var pet in pets) pet.Brain.Interrupt(new IdleBehavior());
            }
        }
    }

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
