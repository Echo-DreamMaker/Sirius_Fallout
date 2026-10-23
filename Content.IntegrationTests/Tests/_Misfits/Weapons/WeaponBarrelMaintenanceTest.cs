// #Misfits Add - Barrel maintenance rules: a barrel taken out of the weapon is
// cleaned with the cleaning rod rather than taped back together, and swabbing is
// not a one-shot stroke but keeps going until the recoverable wear is gone.
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared._Misfits.Weapons.Durability;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Misfits.Weapons;

[TestFixture]
public sealed class WeaponBarrelMaintenanceTest
{
    private const string BarrelProto = "N14WeaponPartBarrel556Standard";
    private const string StockProto = "N14WeaponPartStockPolymer";
    private const string RodProto = "N14WeaponCleaningRod";
    private const string TapeProto = "N14JunkDuctTape";
    private const string HumanProto = "MobHuman";

    private const float BarrelMax = 250f;
    private const float TapeRepair = 50f;
    private const float RodRepair = 10f;

    [Test]
    public async Task TapeDoesNotRepairALooseBarrelButStillMendsAStock()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var hands = entMan.System<SharedHandsSystem>();

        WeaponPartDurabilityComponent stockComp = null!;

        await server.WaitAssertion(() =>
        {
            var user = entMan.SpawnEntity(HumanProto, map.GridCoords);
            var tape = entMan.SpawnEntity(TapeProto, map.GridCoords);
            Assert.That(hands.TryPickupAnyHand(user, tape, checkActionBlocker: false), Is.True);

            var barrel = entMan.SpawnEntity(BarrelProto, map.GridCoords);
            var barrelComp = entMan.GetComponent<WeaponPartDurabilityComponent>(barrel);
            barrelComp.Current = 100f;
            barrelComp.Deferred = 0f;

            entMan.EventBus.RaiseLocalEvent(barrel,
                new InteractUsingEvent(user, tape, barrel, map.GridCoords));

            Assert.That(barrelComp.Current, Is.EqualTo(100f),
                "duct tape must not repair a barrel");
            Assert.That(entMan.Deleted(tape), Is.False,
                "tape refused by a barrel must not be consumed");

            // Control: the same tape still mends a loose stock after its action
            // completes, so the refusal above really is the barrel rule and not a
            // dead interaction.
            var stock = entMan.SpawnEntity(StockProto, map.GridCoords);
            stockComp = entMan.GetComponent<WeaponPartDurabilityComponent>(stock);
            stockComp.Current = 100f;

            entMan.EventBus.RaiseLocalEvent(stock,
                new InteractUsingEvent(user, tape, stock, map.GridCoords));

            Assert.That(stockComp.Current, Is.EqualTo(100f),
                "tape repair should be deferred, not applied instantly");
        });

        // Binding furniture takes 4 seconds.
        await pair.RunSeconds(4.5f);
        await server.WaitAssertion(() =>
        {
            Assert.That(stockComp.Current, Is.EqualTo(100f + TapeRepair),
                "duct tape should still repair a loose stock");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RodSwabsALooseBarrelRepeatedlyUntilItIsClean()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var hands = entMan.System<SharedHandsSystem>();

        WeaponPartDurabilityComponent barrelComp = null!;

        await server.WaitAssertion(() =>
        {
            var user = entMan.SpawnEntity(HumanProto, map.GridCoords);
            var rod = entMan.SpawnEntity(RodProto, map.GridCoords);
            Assert.That(hands.TryPickupAnyHand(user, rod, checkActionBlocker: false), Is.True);

            var barrel = entMan.SpawnEntity(BarrelProto, map.GridCoords);
            barrelComp = entMan.GetComponent<WeaponPartDurabilityComponent>(barrel);
            barrelComp.Current = BarrelMax - 30f;
            barrelComp.Deferred = 30f;

            entMan.EventBus.RaiseLocalEvent(barrel,
                new InteractUsingEvent(user, rod, barrel, map.GridCoords));
        });

        // One two-second stroke.
        await pair.RunSeconds(2.5f);
        await server.WaitAssertion(() =>
        {
            Assert.That(barrelComp.Deferred, Is.EqualTo(20f).Within(0.01f),
                "a stroke should recover 10, not the whole bore at once");
            Assert.That(barrelComp.Current, Is.EqualTo(BarrelMax - 20f).Within(0.01f));
        });

        // The rod has to keep going on its own from here: no further clicks.
        await pair.RunSeconds(5f);
        await server.WaitAssertion(() =>
        {
            Assert.That(barrelComp.Deferred, Is.EqualTo(0f).Within(0.01f),
                "swabbing must repeat until the recoverable wear is gone");
            Assert.That(barrelComp.Current, Is.EqualTo(BarrelMax).Within(0.01f),
                "the recovered wear must come back as barrel health");
        });

        await pair.CleanReturnAsync();
    }
}
