// #Misfits Add - Integration tests for the bunker tunnel teleporter pair.
using System.Collections.Generic;
using System.Numerics;
using Content.Server._Misfits.Warps;
using Content.Shared.Interaction;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Misfits;

/// <summary>
/// Covers how the bunker hatch and tunnel door decide where they send you. Every test puts its
/// entities on their own channel, so leftovers from another test can never be picked up by mistake.
/// </summary>
[TestFixture]
public sealed class BunkerTeleporterTest
{
    private const string HatchProto = "N14BunkerHatchTunnel";
    private const string DoorProto = "N14BunkerTunnelDoor";
    private const string ExitProto = "N14BunkerTunnelExit";
    private const string PlainLadderProto = "LadderTopBunkerOpen";

    [Test]
    public async Task HatchPicksAnExitAndAlwaysComesOutThere()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var xform = server.System<SharedTransformSystem>();
        var map = await pair.CreateTestMap();
        const string channel = "test-hatch-sticks";

        EntityUid hatch = default;
        EntityUid user = default;
        var exits = new List<EntityUid>();

        await server.WaitPost(() =>
        {
            RunMap(server, map.MapId);

            for (var i = 0; i < 4; i++)
            {
                var exit = entMan.SpawnEntity(ExitProto, new EntityCoordinates(map.Grid, 5f + i * 3f, 5f));
                entMan.GetComponent<BunkerTunnelExitComponent>(exit).Channel = channel;
                exits.Add(exit);
            }

            hatch = entMan.SpawnEntity(HatchProto, map.GridCoords);
            entMan.GetComponent<BunkerTeleporterComponent>(hatch).Channel = channel;

            user = entMan.SpawnEntity(null, map.GridCoords);
        });

        await server.WaitAssertion(() =>
        {
            Use(entMan, hatch, user);

            var component = entMan.GetComponent<BunkerTeleporterComponent>(hatch);
            Assert.That(component.CachedDestination, Is.Not.Null, "hatch did not pick an exit");
            var rolled = component.CachedDestination!.Value;
            Assert.That(exits, Does.Contain(rolled), "hatch picked something that is not an exit marker");
            AssertSamePlace(xform, user, rolled, "hatch did not move the user to the exit it picked");

            // The roll has to stick, or a hatch would come out somewhere different every time.
            for (var i = 0; i < 10; i++)
            {
                xform.SetCoordinates(user, map.GridCoords);
                Use(entMan, hatch, user);

                Assert.That(component.CachedDestination, Is.EqualTo(rolled), "hatch changed its exit between uses");
                AssertSamePlace(xform, user, rolled, "hatch came out somewhere different on a later use");
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HatchRerollsWhenItsExitIsDeleted()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var xform = server.System<SharedTransformSystem>();
        var map = await pair.CreateTestMap();
        const string channel = "test-hatch-reroll";

        EntityUid hatch = default;
        EntityUid user = default;
        EntityUid firstExit = default;
        EntityUid secondExit = default;

        await server.WaitPost(() =>
        {
            RunMap(server, map.MapId);

            firstExit = entMan.SpawnEntity(ExitProto, new EntityCoordinates(map.Grid, 5f, 5f));
            secondExit = entMan.SpawnEntity(ExitProto, new EntityCoordinates(map.Grid, 9f, 9f));
            entMan.GetComponent<BunkerTunnelExitComponent>(firstExit).Channel = channel;
            entMan.GetComponent<BunkerTunnelExitComponent>(secondExit).Channel = channel;

            hatch = entMan.SpawnEntity(HatchProto, map.GridCoords);
            entMan.GetComponent<BunkerTeleporterComponent>(hatch).Channel = channel;

            user = entMan.SpawnEntity(null, map.GridCoords);
        });

        EntityUid rolled = default;
        await server.WaitAssertion(() =>
        {
            Use(entMan, hatch, user);
            rolled = entMan.GetComponent<BunkerTeleporterComponent>(hatch).CachedDestination!.Value;
            Assert.That(rolled, Is.EqualTo(firstExit).Or.EqualTo(secondExit));
        });

        // Deleting the exit a hatch settled on must not strand it.
        await server.WaitPost(() => entMan.DeleteEntity(rolled));

        await server.WaitAssertion(() =>
        {
            var survivor = rolled == firstExit ? secondExit : firstExit;

            xform.SetCoordinates(user, map.GridCoords);
            Use(entMan, hatch, user);

            Assert.That(entMan.GetComponent<BunkerTeleporterComponent>(hatch).CachedDestination, Is.EqualTo(survivor),
                "hatch did not re-roll after its exit was deleted");
            AssertSamePlace(xform, user, survivor, "hatch did not move the user to the re-rolled exit");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DoorReturnsToNearestHatchAndIgnoresOrdinaryLadders()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var xform = server.System<SharedTransformSystem>();
        var map = await pair.CreateTestMap();
        const string channel = "test-door-nearest";

        EntityUid door = default;
        EntityUid user = default;
        EntityUid nearHatch = default;

        await server.WaitPost(() =>
        {
            RunMap(server, map.MapId);

            door = entMan.SpawnEntity(DoorProto, map.GridCoords);
            entMan.GetComponent<BunkerTeleporterComponent>(door).Channel = channel;

            nearHatch = entMan.SpawnEntity(HatchProto, new EntityCoordinates(map.Grid, 6f, 0f));
            entMan.GetComponent<BunkerTeleporterComponent>(nearHatch).Channel = channel;

            var farHatch = entMan.SpawnEntity(HatchProto, new EntityCoordinates(map.Grid, 20f, 0f));
            entMan.GetComponent<BunkerTeleporterComponent>(farHatch).Channel = channel;

            // A plain warper ladder sitting closer than either hatch. The door must not pick it up:
            // only entities carrying BunkerTeleporterComponent count as partners.
            entMan.SpawnEntity(PlainLadderProto, new EntityCoordinates(map.Grid, 1f, 0f));

            user = entMan.SpawnEntity(null, map.GridCoords);
        });

        await server.WaitAssertion(() =>
        {
            Use(entMan, door, user);
            AssertSamePlace(xform, user, nearHatch, "door did not return to the nearest bunker hatch");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HatchFallsBackToTheDoorWhenNoExitsArePlaced()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var xform = server.System<SharedTransformSystem>();
        var map = await pair.CreateTestMap();
        const string channel = "test-hatch-fallback";

        EntityUid hatch = default;
        EntityUid door = default;
        EntityUid user = default;

        await server.WaitPost(() =>
        {
            RunMap(server, map.MapId);

            door = entMan.SpawnEntity(DoorProto, new EntityCoordinates(map.Grid, 12f, 3f));
            entMan.GetComponent<BunkerTeleporterComponent>(door).Channel = channel;

            hatch = entMan.SpawnEntity(HatchProto, map.GridCoords);
            entMan.GetComponent<BunkerTeleporterComponent>(hatch).Channel = channel;

            user = entMan.SpawnEntity(null, map.GridCoords);
        });

        await server.WaitAssertion(() =>
        {
            // No exit markers on this channel, so an admin-spawned pair still has to work on its own.
            Use(entMan, hatch, user);
            AssertSamePlace(xform, user, door, "hatch did not fall back to the paired door with no exits placed");
        });

        await pair.CleanReturnAsync();
    }

    private static void Use(IEntityManager entMan, EntityUid target, EntityUid user)
    {
        entMan.EventBus.RaiseLocalEvent(target, new InteractHandEvent(user, target));
    }

    /// <summary>
    /// A warp is refused when the destination map is not running, so make sure the test map is.
    /// </summary>
    private static void RunMap(Robust.UnitTesting.RobustIntegrationTest.ServerIntegrationInstance server, MapId mapId)
    {
        server.System<SharedMapSystem>().SetPaused(mapId, false);
    }

    /// <summary>
    /// Compares world positions rather than EntityCoordinates: warping re-parents the entity, so the
    /// same spot can be expressed relative to the grid or to the map depending on what is underfoot.
    /// </summary>
    private static void AssertSamePlace(SharedTransformSystem xform, EntityUid moved, EntityUid destination, string message)
    {
        var actual = xform.GetMapCoordinates(moved);
        var expected = xform.GetMapCoordinates(destination);

        Assert.That(actual.MapId, Is.EqualTo(expected.MapId), message);
        Assert.That(Vector2.Distance(actual.Position, expected.Position), Is.LessThan(0.01f), message);
    }
}
