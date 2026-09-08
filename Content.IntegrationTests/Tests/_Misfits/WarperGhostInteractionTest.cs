// #Misfits Add - Integration test: verifies regular ghosts can traverse warpers by examining them.
using System.Linq;
using System.Numerics;
using Content.Shared.Examine;
using Content.Shared.Ghost;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._Misfits;

[TestFixture]
public sealed class WarperGhostInteractionTest
{
    [Test]
    public async Task RegularGhostCanTraverseWarperByExamining()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = false
        });

        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        var entMan = server.ResolveDependency<IEntityManager>();
        var playerMan = server.ResolveDependency<IPlayerManager>();
        var conHost = client.ResolveDependency<IConsoleHost>();

        EntityUid ladder = default;
        EntityUid destination = default;
        EntityUid ghost = default;
        var destinationCoords = new EntityCoordinates(map.Grid, 3f, 1f);

        await server.WaitPost(() =>
        {
            ladder = entMan.SpawnEntity(null, map.GridCoords);
            destination = entMan.SpawnEntity(null, destinationCoords);

            var warper = entMan.AddComponent<Content.Server.Warps.WarperComponent>(ladder);
            warper.ID = "warper-ghost-test";

            var warpPoint = entMan.AddComponent<Content.Server.Warps.WarpPointComponent>(destination);
            warpPoint.ID = "warper-ghost-test";
        });

        conHost.ExecuteCommand("ghost");
        await pair.RunTicksSync(10);

        ghost = playerMan.Sessions.Single().AttachedEntity!.Value;
        Assert.That(entMan.HasComponent<GhostComponent>(ghost));

        await server.WaitPost(() =>
        {
            entMan.EventBus.RaiseLocalEvent(ladder,
                new ExaminedEvent(new FormattedMessage(), ladder, ghost, true, false));
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            // #Misfits Change /Fix/: compare world positions, not EntityCoordinates. Warping ends in
            // AttachToGridOrMap, which re-parents the entity, so the same spot is expressed relative
            // to the grid or to the map depending on what is underfoot. The old assertion compared
            // the parent too and failed on it even though the ghost landed on the right tile.
            var xform = server.System<SharedTransformSystem>();
            var ghostCoords = xform.GetMapCoordinates(ghost);
            var expected = xform.ToMapCoordinates(destinationCoords);

            Assert.That(ghostCoords.MapId, Is.EqualTo(expected.MapId));
            Assert.That(Vector2.Distance(ghostCoords.Position, expected.Position), Is.LessThan(0.01f));
        });

        await pair.CleanReturnAsync();
    }
}