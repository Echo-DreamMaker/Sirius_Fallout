using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Misfits.GunSystem;

/// <summary>
/// Verifies the unified toggle-weapon behavior: every toggle weapon spawns off,
/// wield (Z) never toggles it, only the activate interaction (E) turns it on/off,
/// and the off state gates the fire rate to zero.
/// </summary>
[TestFixture]
public sealed class ToggleWeaponBehaviorTest
{
    private static readonly string[] ToggleProtos =
    [
        "N14WeaponMinigun",
        "N14WeaponMinigunAvenger",
        "N14WeaponMECGaussMinigunSirius",
        "N14WeaponLaserGatling",
        "N14WeaponSniperM72GaussRifleSirius67",
        "N14WeaponSniperM72GaussRifleSirius",
        "N14WeaponGaussRifle",
        "N14WeaponGaussPistol",
    ];

    [Test]
    public async Task ToggleWeaponSpawnsOffAndIsManuallyActivated()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var guns = entMan.System<SharedGunSystem>();

        foreach (var proto in ToggleProtos)
        {
            await server.WaitAssertion(() =>
            {
                var user = entMan.SpawnEntity("MobHuman", map.GridCoords);
                var gunUid = entMan.SpawnEntity(proto, map.GridCoords);
                var toggle = entMan.GetComponent<ItemToggleComponent>(gunUid);
                var gun = entMan.GetComponent<GunComponent>(gunUid);

                Assert.That(toggle.Activated, Is.False, $"{proto}: should spawn OFF.");

                guns.RefreshModifiers(gunUid);
                Assert.That(gun.FireRateModified, Is.LessThanOrEqualTo(0f),
                    $"{proto}: OFF state must gate fire rate to zero, got {gun.FireRateModified}.");

                // Wielding (Z) must not turn the weapon on.
                var wield = new ItemWieldedEvent();
                entMan.EventBus.RaiseLocalEvent(gunUid, ref wield);
                Assert.That(toggle.Activated, Is.False, $"{proto}: wielding must not auto-activate.");

                // Using in hand (Z) must not toggle it either.
                var useInHand = new UseInHandEvent(user);
                entMan.EventBus.RaiseLocalEvent(gunUid, useInHand);
                Assert.That(toggle.Activated, Is.False, $"{proto}: Z must not toggle the weapon.");

                // Activating in the world (E) turns it on at the proper fire rate.
                var activate = new ActivateInWorldEvent(user, gunUid, true);
                entMan.EventBus.RaiseLocalEvent(gunUid, activate);
                Assert.That(toggle.Activated, Is.True, $"{proto}: E should activate the weapon.");

                guns.RefreshModifiers(gunUid);
                var expectedOn = entMan.GetComponent<MinigunToggleComponent>(gunUid).ActivatedFireRate > 0f
                    ? entMan.GetComponent<MinigunToggleComponent>(gunUid).ActivatedFireRate
                    : gun.FireRate;
                Assert.That(gun.FireRateModified, Is.EqualTo(expectedOn).Within(0.001f),
                    $"{proto}: ON state should fire at {expectedOn}, got {gun.FireRateModified}.");

                // Activating again (E) turns it off and gates the fire rate.
                var deactivate = new ActivateInWorldEvent(user, gunUid, true);
                entMan.EventBus.RaiseLocalEvent(gunUid, deactivate);
                Assert.That(toggle.Activated, Is.False, $"{proto}: E should deactivate the weapon.");

                guns.RefreshModifiers(gunUid);
                Assert.That(gun.FireRateModified, Is.LessThanOrEqualTo(0f),
                    $"{proto}: back to OFF must gate fire rate to zero.");
            });
        }

        await pair.CleanReturnAsync();
    }
}