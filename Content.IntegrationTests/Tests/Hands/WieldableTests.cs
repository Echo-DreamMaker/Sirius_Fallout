// #Misfits Change /Add/ - Guards wielding from dropping occupied hands or active pulls to make space.
using System.Linq;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.Hands;

[TestFixture]
public sealed class WieldableTests
{
    [Test]
    public async Task TestCannotWieldWhenOtherHandOccupied()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = false
        });

        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var playerMan = server.ResolveDependency<IPlayerManager>();
        var mapMan = server.ResolveDependency<SharedMapSystem>();

        var handsSystem = entMan.System<SharedHandsSystem>();
        var wieldSystem = entMan.System<WieldableSystem>();
        var transformSystem = entMan.System<TransformSystem>();

        var data = await pair.CreateTestMap();
        await pair.RunTicksSync(5);

        EntityUid player = default;
        EntityUid wieldable = default;
        EntityUid offhandItem = default;
        HandsComponent hands = default!;
        WieldableComponent wieldableComponent = default!;

        await server.WaitPost(() =>
        {
            player = playerMan.Sessions.First().AttachedEntity!.Value;
            var coordinates = transformSystem.GetMapCoordinates(player);

            wieldable = entMan.SpawnEntity("BaseBallBat", coordinates);
            offhandItem = entMan.SpawnEntity("Crowbar", coordinates);

            hands = entMan.GetComponent<HandsComponent>(player);
            wieldableComponent = entMan.GetComponent<WieldableComponent>(wieldable);

            Assert.That(handsSystem.TryPickup(player, wieldable, hands.ActiveHand!, checkActionBlocker: false));

            var otherHandName = hands.SortedHands.First(name => name != hands.ActiveHand!.Name);
            Assert.That(handsSystem.TrySetActiveHand(player, otherHandName, hands));
            Assert.That(hands.ActiveHand, Is.Not.Null);
            Assert.That(handsSystem.TryPickup(player, offhandItem, hands.ActiveHand!, checkActionBlocker: false));

            Assert.That(wieldSystem.TryWield(wieldable, wieldableComponent, player), Is.False);
        });

        await pair.RunTicksSync(5);

        Assert.Multiple(() =>
        {
            Assert.That(wieldableComponent.Wielded, Is.False);
            Assert.That(hands.Hands.Values.Any(hand => hand.HeldEntity == offhandItem), Is.True);
            Assert.That(entMan.EntityExists(offhandItem), Is.True);
        });

        await server.WaitPost(() => mapMan.DeleteMap(data.MapId));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TestCannotWieldWhilePulling()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = false
        });

        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var playerMan = server.ResolveDependency<IPlayerManager>();
        var mapMan = server.ResolveDependency<SharedMapSystem>();

        var handsSystem = entMan.System<SharedHandsSystem>();
        var wieldSystem = entMan.System<WieldableSystem>();
        var pullingSystem = entMan.System<PullingSystem>();
        var transformSystem = entMan.System<TransformSystem>();

        var data = await pair.CreateTestMap();
        await pair.RunTicksSync(5);

        EntityUid player = default;
        EntityUid target = default;
        EntityUid wieldable = default;
        PullerComponent puller = default!;
        HandsComponent hands = default!;
        WieldableComponent wieldableComponent = default!;

        await server.WaitPost(() =>
        {
            player = playerMan.Sessions.First().AttachedEntity!.Value;
            var coordinates = transformSystem.GetMapCoordinates(player);

            target = entMan.SpawnEntity("MobHuman", coordinates);
            wieldable = entMan.SpawnEntity("BaseBallBat", coordinates);

            hands = entMan.GetComponent<HandsComponent>(player);
            puller = entMan.GetComponent<PullerComponent>(player);
            wieldableComponent = entMan.GetComponent<WieldableComponent>(wieldable);

            Assert.That(handsSystem.TryPickup(player, wieldable, hands.ActiveHand!, checkActionBlocker: false));
            Assert.That(pullingSystem.TryStartPull(player, target), Is.True);
            Assert.That(wieldSystem.TryWield(wieldable, wieldableComponent, player), Is.False);
        });

        await pair.RunTicksSync(5);

        Assert.Multiple(() =>
        {
            Assert.That(wieldableComponent.Wielded, Is.False);
            Assert.That(puller.Pulling, Is.EqualTo(target));
            Assert.That(hands.Hands.Values.Count(hand => hand.IsEmpty), Is.EqualTo(0));
        });

        await server.WaitPost(() => mapMan.DeleteMap(data.MapId));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TestWieldedWeaponAppliesAndClearsMovementSlowdown()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = false
        });

        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var playerMan = server.ResolveDependency<IPlayerManager>();
        var mapMan = server.ResolveDependency<SharedMapSystem>();

        var handsSystem = entMan.System<SharedHandsSystem>();
        var wieldSystem = entMan.System<WieldableSystem>();
        var movementSpeedSystem = entMan.System<MovementSpeedModifierSystem>();
        var transformSystem = entMan.System<TransformSystem>();

        var data = await pair.CreateTestMap();
        await pair.RunTicksSync(5);

        EntityUid player = default;
        EntityUid wieldable = default;
        HandsComponent hands = default!;
        WieldableComponent wieldableComponent = default!;
        MovementSpeedModifierComponent movementSpeed = default!;

        await server.WaitPost(() =>
        {
            player = playerMan.Sessions.First().AttachedEntity!.Value;
            var coordinates = transformSystem.GetMapCoordinates(player);

            wieldable = entMan.SpawnEntity("BaseBallBat", coordinates);

            hands = entMan.GetComponent<HandsComponent>(player);
            wieldableComponent = entMan.GetComponent<WieldableComponent>(wieldable);
            movementSpeed = entMan.GetComponent<MovementSpeedModifierComponent>(player);

            Assert.That(handsSystem.TryPickup(player, wieldable, hands.ActiveHand!, checkActionBlocker: false));

            movementSpeedSystem.RefreshMovementSpeedModifiers(player, movementSpeed);
            Assert.That(movementSpeed.WalkSpeedModifier, Is.EqualTo(1f).Within(0.001f));
            Assert.That(movementSpeed.SprintSpeedModifier, Is.EqualTo(1f).Within(0.001f));

            Assert.That(wieldSystem.TryWield(wieldable, wieldableComponent, player), Is.True);

            movementSpeedSystem.RefreshMovementSpeedModifiers(player, movementSpeed);
            Assert.That(movementSpeed.WalkSpeedModifier, Is.EqualTo(WieldableComponent.DefaultWeaponWieldedSpeedModifier).Within(0.001f));
            Assert.That(movementSpeed.SprintSpeedModifier, Is.EqualTo(WieldableComponent.DefaultWeaponWieldedSpeedModifier).Within(0.001f));

            Assert.That(handsSystem.TryDrop(player, wieldable, null!));
            movementSpeedSystem.RefreshMovementSpeedModifiers(player, movementSpeed);
        });

        await pair.RunTicksSync(5);

        Assert.Multiple(() =>
        {
            Assert.That(wieldableComponent.Wielded, Is.False);
            Assert.That(movementSpeed.WalkSpeedModifier, Is.EqualTo(1f).Within(0.001f));
            Assert.That(movementSpeed.SprintSpeedModifier, Is.EqualTo(1f).Within(0.001f));
        });

        await server.WaitPost(() => mapMan.DeleteMap(data.MapId));
        await pair.CleanReturnAsync();
    }
}