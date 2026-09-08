using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Misfits.GunSystem;

[TestFixture]
public sealed partial class BallisticAmmoProviderTests : BallisticAmmoProviderSetUp
{

    [Test]
    public async Task BallisticAmmoProviderBasicUse()
    {
        EntityUid urist = default;
        EntityUid ammoBoxOne = default;
        EntityUid ammoBoxTwo = default;
        EntityUid ammoBoxEmpty = default;

        await server.WaitPost(() =>
        {
            var coords = testMap.GridCoords;
            ammoBoxOne = sEntMan.SpawnEntity(BasicAmmoUseCaseProto, coords);
            ammoBoxEmpty = sEntMan.SpawnEntity(BasicAmmoUseCaseEmptyProto, coords);
            ammoBoxTwo = sEntMan.SpawnEntity(BasicAmmoUseCaseProto, coords);
            urist = sEntMan.SpawnEntity("MobHuman", coords);

        });

        var ammoCompOne = sEntMan.GetComponent<BallisticAmmoProviderComponent>(ammoBoxOne);
        var ammoCompEmpty = sEntMan.GetComponent<BallisticAmmoProviderComponent>(ammoBoxEmpty);
        var ammoCompTwo = sEntMan.GetComponent<BallisticAmmoProviderComponent>(ammoBoxTwo);


        await server.WaitPost(() =>
                {
                    sEntMan.EventBus.RaiseLocalEvent(ammoBoxOne,
                    new AfterInteractEvent(urist, ammoBoxOne, ammoBoxEmpty, testMap.GridCoords, true));
                });

        await server.WaitRunTicks(sTime.TickRate * 4);

        Assert.Multiple(() =>
                {
                    TestContext.Out.WriteLine($"Empty ammo being filled values are AmmoCount:{ammoCompEmpty.AmmoCount} UnspawnedCount: {ammoCompEmpty.UnspawnedCount} SpawnedCountPredict: {ammoCompEmpty.SpawnedCountPredict} IndexPredict: {ammoCompEmpty.IndexPredict}");
                    TestContext.Out.WriteLine($"Full ammo giving to empty values   AmmoCount:{ammoCompOne.AmmoCount} UnspawnedCount: {ammoCompOne.UnspawnedCount} SpawnedCountPredict: {ammoCompOne.SpawnedCountPredict} IndexPredict: {ammoCompOne.IndexPredict}");

                    Assert.That(ammoCompEmpty.AmmoCount == ammoCompEmpty.Capacity);
                    Assert.That(ammoCompEmpty.UnspawnedCount == 0);
                    Assert.That(ammoCompEmpty.SpawnedCountPredict == ammoCompEmpty.Capacity);
                    Assert.That(ammoCompEmpty.IndexPredict == 0);

                    Assert.That(ammoCompOne.AmmoCount == 0);
                    Assert.That(ammoCompOne.UnspawnedCount == 0);
                    Assert.That(ammoCompOne.SpawnedCountPredict == 0);
                    Assert.That(ammoCompOne.IndexPredict == 0);
                });
        var cnt = 0;
        await server.WaitPost(() =>
                {
                    for (int i = 0; 100 > i; i++)
                    {
                        sEntMan.EventBus.RaiseLocalEvent(ammoBoxTwo, new UseInHandEvent(urist));
                    }
                    var ents = sLookup.GetEntitiesInRange(testMap.GridCoords, 10f, LookupFlags.Uncontained);
                    foreach (var ent in ents)
                    {
                        if (sEntMan.TryGetComponent<MetaDataComponent>(ent, out var comp) &&
                        comp?.EntityPrototype?.ID is not null && comp.EntityPrototype.ID == BasicAmmoCart)
                        {

                            cnt += 1;
                        }
                    }
                });

        Assert.Multiple(() =>
                {
                    Assert.That(cnt == ammoCompTwo.Capacity);
                    Assert.That(ammoCompTwo.AmmoCount == 0);
                    Assert.That(ammoCompTwo.UnspawnedCount == 0);
                    Assert.That(ammoCompTwo.SpawnedCountPredict == 0);
                });

    }


}



/*
            await using var pair = await PoolManager.GetServerClient();
            var server = pair.Server;
            var sEntMan = server.ResolveDependency<IEntityManager>();
            var sProtoMan = server.ResolveDependency<IPrototypeManager>();
            var sEntSysMan = server.ResolveDependency<IEntitySystemManager>();
            var sTime = server.ResolveDependency<IGameTiming>();

            var sLookup = sEntMan.System<EntityLookupSystem>();
            var sGunSysShared = sEntMan.System<SharedGunSystem>();
            var sMapShared = sEntMan.System<SharedMapSystem>();
            //var sMapMan = server.ResolveDependency<MapMan>();
            var testMap = await pair.CreateTestMap();
    */
