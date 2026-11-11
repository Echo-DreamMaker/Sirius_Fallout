// #Misfits Add - regression tests for the six-fix weapon pass:
//   * barrel-less modular energy weapons (laser rifles) must be allowed to
//     fire, put their recoverable shot fouling on the optics, and keep a
//     nominal spread instead of reading the absent barrel as a worn one;
//   * the cleaning rod swabs those optics like a bore;
//   * blades flagged recoverable accumulate melee wear the whetstone restores;
//   * internal-magazine pistols must carry a single ammo provider.
using System.Collections.Generic;
using System.Linq;
using Content.Server.Weapons.Ranged.Components;
using Content.Shared.Containers;
using Content.Shared.Damage;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared._Sirius.Weapons.Durability;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Misfits.Weapons;

[TestFixture]
public sealed class WeaponDurabilityRegressionTest
{
    private const string LaserRifleProto = "N14WeaponLaserRifleAuto";
    private const string KnifeProto = "N14CombatKnife";
    private const string HatchetProto = "N14Hatchet";
    private const string ChinesePistolProto = "N14WeaponPistol9mmChinese";
    private const string SpyPistolProto = "N14WeaponPistol9mmSpy";
    private const string RodProto = "N14WeaponCleaningRod";
    private const string WhetstoneProto = "N14Whetstone";
    private const string HumanProto = "MobHuman";

    // PartShotWear(Optics, with stock) = 2, of which 90% is recoverable.
    private const float OpticsShotWear = 2f;
    private const float RecoverableFraction = 0.9f;
    // Default melee wear is 1 per hit, 90% of it recoverable.
    private const float KnifeMeleeWear = 1f;

    /// <summary>
    /// Mirrors SharedWeaponDurabilitySystem.GetParts: every entity with a
    /// WeaponPartDurabilityComponent inside any container of the weapon.
    /// </summary>
    private static List<(EntityUid Uid, WeaponPartDurabilityComponent Comp)> GetParts(
        IEntityManager entMan, EntityUid weapon)
    {
        var parts = new List<(EntityUid, WeaponPartDurabilityComponent)>();

        var manager = entMan.GetComponent<ContainerManagerComponent>(weapon);
        foreach (var container in manager.Containers.Values)
        {
            foreach (var contained in container.ContainedEntities)
            {
                if (entMan.TryGetComponent<WeaponPartDurabilityComponent>(contained, out var part))
                    parts.Add((contained, part));
            }
        }

        return parts;
    }

    [Test]
    public async Task BarrellessLaserFiresThroughOpticsAndTheRodCleansThem()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var hands = entMan.System<SharedHandsSystem>();

        EntityUid user = default;
        EntityUid laser = default;

        await server.WaitPost(() =>
        {
            user = entMan.SpawnEntity(HumanProto, map.GridCoords);
            laser = entMan.SpawnEntity(LaserRifleProto, map.GridCoords);
        });

        // Let MapInit / ContainerFill seat the factory parts.
        await server.WaitRunTicks(2);

        WeaponPartDurabilityComponent optics = null!;
        WeaponDurabilityComponent durability = null!;

        await server.WaitAssertion(() =>
        {
            durability = entMan.GetComponent<WeaponDurabilityComponent>(laser);
            Assert.That(durability.Modular, Is.True, "the AER-9 family is a modular weapon");

            var parts = GetParts(entMan, laser);
            Assert.That(parts, Is.Not.Empty, "the weapon should spawn with its factory parts");

            Assert.That(parts.Exists(p => p.Comp.Role == WeaponPartRole.Barrel), Is.False,
                "this laser rifle prototype carries no barrel part");
            Assert.That(parts.Exists(p => p.Comp.Role == WeaponPartRole.Bolt), Is.True,
                "sanity: the shot permission still needs the bolt");

            optics = parts.First(p => p.Comp.Role == WeaponPartRole.Optics).Comp;
            Assert.That(optics.Deferred, Is.EqualTo(0f), "sanity: a fresh optics part is not fouled");

            // Regression: the server used to cancel every shot of a barrel-less
            // modular weapon ("misfits-weapon-no-barrel") while the client
            // predicted the beam, so lasers showed a shot but dealt no damage.
            var attempt = new AttemptShootEvent(user, null);
            entMan.EventBus.RaiseLocalEvent(laser, ref attempt);
            Assert.That(attempt.Cancelled, Is.False,
                "a barrel-less modular energy weapon must be allowed to fire through its optics");

            // The wear of that shot lands on the optics.
            entMan.EventBus.RaiseLocalEvent(laser, new AmmoShotEvent { FiredProjectiles = new List<EntityUid>() });

            Assert.That(optics.Current, Is.EqualTo(optics.Max - OpticsShotWear).Within(0.01f),
                "the optics part takes the barrel's per-shot wear");
            Assert.That(optics.Deferred, Is.EqualTo(OpticsShotWear * RecoverableFraction).Within(0.01f),
                "the optics fouling must be recoverable");

            // Regression: the absent barrel used to read as ratio 0 and double
            // the spread of every modular energy weapon out of the box.
            Assert.That(durability.SpreadMultiplier, Is.EqualTo(1f),
                "a barrel-less modular weapon must not start with worn-barrel spread");
        });

        await server.WaitAssertion(() =>
        {
            var rod = entMan.SpawnEntity(RodProto, map.GridCoords);
            Assert.That(hands.TryPickupAnyHand(user, rod, checkActionBlocker: false), Is.True);
            entMan.EventBus.RaiseLocalEvent(laser, new InteractUsingEvent(user, rod, laser, map.GridCoords));
        });

        // One two-second swab.
        await pair.RunSeconds(2.5f);

        await server.WaitAssertion(() =>
        {
            Assert.That(optics.Deferred, Is.EqualTo(0f).Within(0.01f),
                "the cleaning rod must swab the optics fouling");
            Assert.That(optics.Current,
                Is.EqualTo(optics.Max - OpticsShotWear * (1f - RecoverableFraction)).Within(0.01f),
                "the recovered fouling must come back as optics health");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WhetstoneRestoresMeleeWearOnRecoverableBlades()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var user = entMan.SpawnEntity(HumanProto, map.GridCoords);
            var victim = entMan.SpawnEntity(HumanProto, map.GridCoords);
            var knife = entMan.SpawnEntity(KnifeProto, map.GridCoords);
            var whetstone = entMan.SpawnEntity(WhetstoneProto, map.GridCoords);

            var comp = entMan.GetComponent<WeaponDurabilityComponent>(knife);
            Assert.That(comp.Modular, Is.False);
            Assert.That(comp.Recoverable, Is.True,
                "knives must be whetstone-recoverable; without it they never accrued repairable wear");

            // A melee hit puts recoverable wear on the blade.
            entMan.EventBus.RaiseLocalEvent(knife,
                new MeleeHitEvent(new List<EntityUid> { victim }, user, knife, new DamageSpecifier(), null));

            Assert.That(comp.Deferred, Is.EqualTo(KnifeMeleeWear * RecoverableFraction).Within(0.01f),
                "90% of knife melee wear must be recoverable");
            Assert.That(comp.FrameCurrent, Is.EqualTo(comp.FrameMax - KnifeMeleeWear).Within(0.01f));

            // The whetstone grinds that wear straight back.
            entMan.EventBus.RaiseLocalEvent(knife, new InteractUsingEvent(user, whetstone, knife, map.GridCoords));

            Assert.That(comp.Deferred, Is.EqualTo(0f).Within(0.01f),
                "the whetstone must restore the recoverable melee wear");
            Assert.That(comp.FrameCurrent,
                Is.EqualTo(comp.FrameMax - KnifeMeleeWear * (1f - RecoverableFraction)).Within(0.01f));

            // Axes got the same recoverable flag.
            var hatchet = entMan.SpawnEntity(HatchetProto, map.GridCoords);
            Assert.That(entMan.GetComponent<WeaponDurabilityComponent>(hatchet).Recoverable, Is.True,
                "hatchets must be whetstone-recoverable");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task InternalMagazinePistolsCountOnlyTheirOwnRounds()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var chinese = entMan.SpawnEntity(ChinesePistolProto, map.GridCoords);
            Assert.That(entMan.HasComponent<ChamberMagazineAmmoProviderComponent>(chinese), Is.False,
                "the internal-magazine pistols must not carry a chamber provider next to their magazine");
            Assert.That(entMan.TryGetComponent<BallisticAmmoProviderComponent>(chinese, out var chineseAmmo), Is.True);
            Assert.That(chineseAmmo.Capacity, Is.EqualTo(14), "the chinese pistol's own magazine is 14 rounds");
            Assert.That(entMan.HasComponent<AmmoCounterComponent>(chinese), Is.True,
                "the held-gun readout needs an ammo counter");

            var spy = entMan.SpawnEntity(SpyPistolProto, map.GridCoords);
            Assert.That(entMan.HasComponent<ChamberMagazineAmmoProviderComponent>(spy), Is.False,
                "the spy pistol must not carry a chamber provider next to its magazine");
            Assert.That(entMan.TryGetComponent<BallisticAmmoProviderComponent>(spy, out var spyAmmo), Is.True);
            Assert.That(spyAmmo.Capacity, Is.EqualTo(24), "the spy pistol's own magazine is 24 rounds");
            Assert.That(entMan.HasComponent<AmmoCounterComponent>(spy), Is.True);
        });

        await pair.CleanReturnAsync();
    }
}
