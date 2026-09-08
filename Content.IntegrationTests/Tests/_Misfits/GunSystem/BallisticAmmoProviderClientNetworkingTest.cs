


using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;

namespace Content.IntegrationTests.Tests._Misfits.GunSystem;

[TestFixture]
public sealed partial class BallisticAmmoProviderNetworkTests : BallisticAmmoProviderSetUp
{

    [Test]
    public async Task BallisticAmmoProviderClientNet()
    {

        EntityUid ammoBoxOne = default;
        NetEntity ammoBoxOneNet = default;

        EntityUid ammoBoxEmpty = default;
        NetEntity ammoBoxEmptyNet = default;

        await server.WaitPost(() =>
        {
            var coords = testMap.GridCoords;
            ammoBoxOne = sEntMan.SpawnEntity(BasicAmmoUseCaseProto, coords);
            ammoBoxOneNet = sEntMan.GetNetEntity(ammoBoxOne);

            ammoBoxEmpty = sEntMan.SpawnEntity(BasicAmmoUseCaseEmptyProto, coords);
            ammoBoxEmptyNet = sEntMan.GetNetEntity(ammoBoxEmpty);

        });

        await pair.SyncTicks();
        await pair.RunTicksSync(25);
        EntityUid ammoClientOne = default;
        EntityUid ammoClientEmpty = default;


        BallisticAmmoProviderComponent ammoCompOne = default;
        BallisticAmmoProviderComponent ammoCompEmpty = default;

        await client.WaitPost(() =>
        {
            ammoClientOne = cEntMan.GetEntity(ammoBoxOneNet);
            ammoClientEmpty = cEntMan.GetEntity(ammoBoxEmptyNet);

            ammoCompOne = cEntMan.GetComponent<BallisticAmmoProviderComponent>(ammoClientOne);
            ammoCompEmpty = cEntMan.GetComponent<BallisticAmmoProviderComponent>(ammoClientEmpty);


        });

        await server.WaitPost(() =>
               {
                   var playerEnt = sEntMan.GetEntity(player);
                   var hand = sEntMan.GetComponent<HandsComponent>(playerEnt);
                   Assert.That(server.System<SharedHandsSystem>().TryPickup(playerEnt, ammoBoxOne, hand.ActiveHand, false, false, hand));
               });


        await pair.SyncTicks();
        await pair.RunTicksSync(10);

        await Interact(EngineKeyFunctions.Use, BoundKeyState.Down, testMap.GridCoords, ammoClientEmpty);
        await Interact(EngineKeyFunctions.Use, BoundKeyState.Up, testMap.GridCoords, ammoClientEmpty);

        await pair.RunTicksSync(2);
        await pair.SyncTicks();
        var ammoCompOneS = sEntMan.GetComponent<BallisticAmmoProviderComponent>(ammoBoxOne);
        var ammoCompEmptyS = sEntMan.GetComponent<BallisticAmmoProviderComponent>(ammoBoxEmpty);

        await pair.SyncTicks();
        await pair.RunTicksSync(sTime.TickRate * 8);
        await pair.SyncTicks();
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

        Assert.Multiple(() =>
        {
            TestContext.Out.WriteLine($"S: Empty ammo being filled values are AmmoCount:{ammoCompEmptyS.AmmoCount} UnspawnedCount: {ammoCompEmptyS.UnspawnedCount} SpawnedCountPredict: {ammoCompEmptyS.SpawnedCountPredict} IndexPredict: {ammoCompEmptyS.IndexPredict}");
            TestContext.Out.WriteLine($"S: Full ammo giving to empty values   AmmoCount:{ammoCompOneS.AmmoCount} UnspawnedCount: {ammoCompOneS.UnspawnedCount} SpawnedCountPredict: {ammoCompOneS.SpawnedCountPredict} IndexPredict: {ammoCompOneS.IndexPredict}");
            Assert.That(ammoCompEmptyS.AmmoCount == ammoCompEmptyS.Capacity);
            Assert.That(ammoCompEmptyS.UnspawnedCount == 0);
            Assert.That(ammoCompEmptyS.SpawnedCountPredict == ammoCompEmptyS.Capacity);
            Assert.That(ammoCompEmptyS.IndexPredict == 0);

            Assert.That(ammoCompOneS.AmmoCount == 0);
            Assert.That(ammoCompOneS.UnspawnedCount == 0);
            Assert.That(ammoCompOneS.SpawnedCountPredict == 0);
            Assert.That(ammoCompOneS.IndexPredict == 0);
        });
    }

}
