using Content.Server.Construction;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Misfits.Weapons;

/// <summary>
/// When a modular weapon is finished by hand, the magazine the player seated (staged in
/// "craft_magazine") must end up in the weapon's "gun_magazine" slot, and the free magazine
/// and chambered round the prototype spawns must be gone.
/// </summary>
[TestFixture]
public sealed class WeaponAssemblyMagazineTest
{
    private const string IncompleteFrame = "IncompleteN14WeaponPartFrameAssault";
    private const string DoneNode = "done_N14WeaponRifle556R91";
    private const string AssembledProto = "N14WeaponRifle556R91";
    private const string StagedContainer = "craft_magazine";
    private const string MagazineSlot = "gun_magazine";

    [Test]
    public async Task StagedMagazineMovesIntoTheMagazineSlot()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var construction = entMan.System<ConstructionSystem>();
        var containers = entMan.System<SharedContainerSystem>();
        var itemSlots = entMan.System<ItemSlotsSystem>();

        var installedMagazine = EntityUid.Invalid;

        await server.WaitPost(() =>
        {
            var frame = entMan.SpawnEntity(IncompleteFrame, map.GridCoords);

            var staged = containers.EnsureContainer<Container>(frame, StagedContainer);
            installedMagazine = entMan.SpawnEntity("Magazine556Rifle", map.GridCoords);
            Assert.That(containers.Insert(installedMagazine, staged), Is.True, "could not stage the magazine");

            Assert.That(construction.ChangeNode(frame, null, DoneNode), Is.True, "the node change failed");
        });

        // The handover is deferred by a tick so ItemSlotsSystem.OnMapInit has run.
        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            EntityUid finished = EntityUid.Invalid;
            var query = entMan.AllEntityQueryEnumerator<MetaDataComponent>();
            while (query.MoveNext(out var uid, out var meta))
            {
                if (meta.EntityPrototype?.ID == AssembledProto)
                {
                    finished = uid;
                    break;
                }
            }

            Assert.That(finished.IsValid(), Is.True, "assembly did not produce the finished weapon");

            Assert.That(itemSlots.TryGetSlot(finished, MagazineSlot, out var magazineSlot), Is.True);
            Assert.That(magazineSlot.HasItem, Is.True,
                "the player-seated magazine should be in gun_magazine");
            Assert.That(magazineSlot.Item, Is.EqualTo(installedMagazine),
                "the magazine in gun_magazine should be the one the player seated");

            Assert.That(containers.TryGetContainer(finished, StagedContainer, out var leftover), Is.True);
            Assert.That(leftover.Count, Is.EqualTo(0), "the staged container should be empty");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Sawing a finished weapon's stock off swaps it for its stockless prototype. The
    /// construction swap only carries the construction-owned containers, so the loose ammo a
    /// ballistic weapon holds has to be carried across by hand - otherwise the classic
    /// pump-shotgun-to-short conversion silently empties the gun.
    /// </summary>
    [Test]
    public async Task SawingOffTheStockCarriesBallisticAmmo()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var construction = entMan.System<ConstructionSystem>();

        const int ammo = 3;

        await server.WaitPost(() =>
        {
            var shotgun = entMan.SpawnEntity("N14WeaponShotgun", map.GridCoords);
            var provider = entMan.GetComponent<BallisticAmmoProviderComponent>(shotgun);
            provider.UnspawnedCount = ammo;
            provider.SpawnedCountPredict = 0;

            Assert.That(construction.ChangeNode(shotgun, null, "done_N14WeaponShotgunShort"), Is.True,
                "the saw node change failed");
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            EntityUid sawn = EntityUid.Invalid;
            var query = entMan.AllEntityQueryEnumerator<MetaDataComponent>();
            while (query.MoveNext(out var uid, out var meta))
            {
                if (meta.EntityPrototype?.ID == "N14WeaponShotgunShort")
                {
                    sawn = uid;
                    break;
                }
            }

            Assert.That(sawn.IsValid(), Is.True, "sawing did not produce the short shotgun");

            var provider = entMan.GetComponent<BallisticAmmoProviderComponent>(sawn);
            Assert.That(provider.UnspawnedCount, Is.EqualTo(ammo), "the shells should survive the saw");
            Assert.That(provider.Proto, Is.Not.Null, "the ammunition type should survive the saw");
        });

        await pair.CleanReturnAsync();
    }
}
