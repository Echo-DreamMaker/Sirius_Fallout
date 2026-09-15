using Content.Shared.Hands.EntitySystems;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Misfits.GunSystem;

/// <summary>
/// Verifies the gauss / energy weapon toggle system:
///   * BypassChamber weapons always load a full magazine on spawn.
///   * They spawn turned OFF with fire rate 0 (open bolt: cannot fire).
///   * The power button gates firing only — ON restores the prototype fire rate.
///   * Both OFF and ON rifles remain pickable at all times.
/// </summary>
[TestFixture]
public sealed class GaussRiflePickupAmmoTest
{
    private static readonly string[] GaussRifleProtos =
    [
        "N14WeaponSniperM72GaussRifleSirius67",
        "N14WeaponSniperM72GaussRifleSirius",
        "N14WeaponGaussRifle",
    ];

    private static readonly string[] AllBypassChamberProtos =
    [
        "N14WeaponSniperM72GaussRifleSirius67",
        "N14WeaponSniperM72GaussRifleSirius",
        "N14WeaponGaussRifle",
        "N14WeaponPistol2mmECPPK12Sirius",
        "N14WeaponGaussPistol",
        "N14WeaponMECGaussMinigunSirius",
    ];

    [Test]
    public async Task GaussRifleLoadsFullMagazine()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            foreach (var proto in GaussRifleProtos)
            {
                var rifle = entMan.SpawnEntity(proto, map.GridCoords);

                var ammoEv = new GetAmmoCountEvent();
                entMan.EventBus.RaiseLocalEvent(rifle, ref ammoEv);
                Assert.That(ammoEv.Count, Is.EqualTo(20),
                    $"{proto}: magazine should contain 20 rounds after spawn, got {ammoEv.Count}.");
                Assert.That(ammoEv.Capacity, Is.EqualTo(20),
                    $"{proto}: magazine capacity should be 20.");
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BypassChamberWeaponSpawnsOffAndPickable()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var hands = entMan.System<SharedHandsSystem>();
        var toggle = entMan.System<ItemToggleSystem>();

        foreach (var proto in AllBypassChamberProtos)
        {
            await server.WaitAssertion(() =>
            {
                var user = entMan.SpawnEntity("MobHuman", map.GridCoords);
                var rifle = entMan.SpawnEntity(proto, map.GridCoords);
                var toggleComp = entMan.GetComponent<ItemToggleComponent>(rifle);
                var gun = entMan.GetComponent<GunComponent>(rifle);

                // Must spawn turned OFF with a zeroed fire rate (open bolt).
                Assert.That(toggleComp.Activated, Is.False,
                    $"{proto}: should spawn turned off.");
                Assert.That(gun.FireRateModified, Is.EqualTo(0f),
                    $"{proto}: off weapon must not fire, expected 0, got {gun.FireRateModified}.");

                // OFF weapons remain pickable (no TimeSpan overflow from 1/0).
                Assert.That(hands.TryPickupAnyHand(user, rifle), Is.True,
                    $"{proto}: pickup of OFF weapon should succeed.");

                // Magazine is still loaded while OFF.
                var ammoEv = new GetAmmoCountEvent();
                entMan.EventBus.RaiseLocalEvent(rifle, ref ammoEv);
                Assert.That(ammoEv.Count, Is.GreaterThan(0),
                    $"{proto}: off weapon should keep its loaded magazine.");

                // Turning the button on restores the prototype fire rate.
                Assert.That(toggle.TryActivate(rifle), Is.True,
                    $"{proto}: TryActivate should succeed.");
                Assert.That(gun.FireRateModified, Is.EqualTo(gun.FireRate),
                    $"{proto}: on weapon should fire at prototype rate {gun.FireRate}, got {gun.FireRateModified}.");

                // Turning it back off zeroes the rate again.
                Assert.That(toggle.TryDeactivate(rifle), Is.True,
                    $"{proto}: TryDeactivate should succeed.");
                Assert.That(gun.FireRateModified, Is.EqualTo(0f),
                    $"{proto}: off weapon must not fire again.");

                // Drop + re-pickup still works in both states.
                Assert.That(hands.TryDrop(user, rifle), Is.True,
                    $"{proto}: dropping weapon should succeed.");
                Assert.That(hands.TryPickupAnyHand(user, rifle), Is.True,
                    $"{proto}: re-picking up OFF weapon should succeed.");

                Assert.That(toggle.TryActivate(rifle), Is.True);
                Assert.That(hands.TryDrop(user, rifle), Is.True,
                    $"{proto}: dropping ON weapon should succeed.");
                Assert.That(hands.TryPickupAnyHand(user, rifle), Is.True,
                    $"{proto}: re-picking up ON weapon should succeed.");
            });
        }

        await pair.CleanReturnAsync();
    }
}
