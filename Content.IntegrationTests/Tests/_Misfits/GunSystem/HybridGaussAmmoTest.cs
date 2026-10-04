using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using System.Collections.Generic;

namespace Content.IntegrationTests.Tests._Misfits.GunSystem;

/// <summary>
/// Guards the hybrid gauss weapon: the M72 is fed by a cartridge magazine that is also a battery,
/// so every shot must spend a round <b>and</b> charge.
///
/// The provider split is load-bearing, not cosmetic. When HybridAmmoProvider was the gun's only
/// ammo provider it handled TakeAmmoEvent on the server alone, so under gun prediction the shooter
/// never got a locally spawned cartridge and the bullet never left the client. Ammo now flows
/// through the shared MagazineAmmoProvider -> BallisticAmmoProvider -> CartridgeAmmo chain and
/// HybridAmmoProvider only gates and drains charge.
/// </summary>
[TestFixture]
public sealed class HybridGaussAmmoTest
{
    private const string RifleProto = "N14WeaponSniperM72GaussRifleSirius";
    private const string PistolProto = "N14WeaponPistol2mmECPPK12Sirius";
    private const string MinigunProto = "N14WeaponMECGaussMinigunSirius";

    private const string RifleMagazine = "N14MagazineRifle2mmEC";
    private const string PistolMagazine = "N14MagazinePistol2mmEC";
    private const string MinigunMagazine = "N14MagazineGaussMinigun2mmEC";

    private const string MagazineSlot = "gun_magazine";

    /// <summary>
    /// Typed so the analyzer does not flag a literal passed to HasIndex; keeping it in one place
    /// also ties the assertion below to the prototype the cartridge actually references.
    /// </summary>
    private static readonly EntProtoId FlashProto = "GaussMuzzleFlashEffect";
    private const float FireCost = 50f;

    /// <summary>
    /// The gun must carry both providers. Losing MagazineAmmoProvider reintroduces the
    /// server-only-ammo bug; losing HybridAmmoProvider makes the weapon shoot for free.
    ///
    /// Parameterised over all three gauss weapons so the PPK12 and the MEC minigun cannot silently
    /// drift back to being free-firing, and so each cell's capacity stays tied to its round count.
    /// </summary>
    [TestCase(RifleProto, RifleMagazine, 1000f, 20)]
    [TestCase(PistolProto, PistolMagazine, 600f, 12)]
    [TestCase(MinigunProto, MinigunMagazine, 4000f, 80)]
    public async Task HybridGunUsesSharedMagazineChain(
        string gunProto,
        string magazineProto,
        float expectedMaxCharge,
        int expectedCharges)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var gun = entMan.SpawnEntity(gunProto, map.GridCoords);

            Assert.That(entMan.HasComponent<MagazineAmmoProviderComponent>(gun), Is.True,
                $"{gunProto} needs MagazineAmmoProvider: it is what makes ammo take shared code " +
                "and therefore prediction-correct.");

            Assert.That(entMan.HasComponent<HybridAmmoProviderComponent>(gun), Is.True,
                $"{gunProto} needs HybridAmmoProvider to gate and drain the cell.");

            var hybrid = entMan.GetComponent<HybridAmmoProviderComponent>(gun);
            Assert.That(hybrid.FireCost, Is.EqualTo(FireCost),
                "FireCost drives both the pre-fire check and the drain; the two must not drift apart.");

            Assert.That(hybrid.HasCell, Is.True,
                "The starting magazine is a battery cell, so the networked mirror must be populated.");

            Assert.That(hybrid.MaxCharge, Is.EqualTo(expectedMaxCharge).Within(0.01f));
            Assert.That(hybrid.Charge, Is.EqualTo(expectedMaxCharge).Within(0.01f),
                $"{gunProto} should spawn with a full cell.");

            // The mirror must agree with the real battery, since the client gates shots off it.
            var battery = entMan.GetComponent<BatteryComponent>(GetMagazine(entMan, gun));
            Assert.That(hybrid.Charge, Is.EqualTo(battery.CurrentCharge).Within(0.01f));

            // Shots, not joules. This is the number the examine line shows, and it has to line up
            // with the round count or the two HUD/examine readouts would contradict each other.
            Assert.That(hybrid.ChargesMax, Is.EqualTo(expectedCharges));
            Assert.That(hybrid.ChargesLeft, Is.EqualTo(expectedCharges));

            var ammo = entMan.GetComponent<BallisticAmmoProviderComponent>(GetMagazine(entMan, gun));
            Assert.That(ammo.Capacity, Is.EqualTo(expectedCharges),
                $"{magazineProto} is the {gunProto} cell, so its round capacity and its charge count " +
                "must describe the same thing.");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Every gauss cell must carry the components the weapon battery chargers look for, plus the
    /// marker that makes it report its own charge when examined loose.
    /// </summary>
    [TestCase(RifleMagazine, 1000f, 20)]
    [TestCase(PistolMagazine, 600f, 12)]
    [TestCase(MinigunMagazine, 4000f, 80)]
    public async Task GaussMagazineIsAChargeCell(string magazineProto, float expectedMaxCharge, int expectedCharges)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var magazine = entMan.SpawnEntity(magazineProto, map.GridCoords);

            Assert.That(entMan.HasComponent<BatteryComponent>(magazine), Is.True,
                $"{magazineProto} is the charge source; ChargerSystem finds this component directly.");

            Assert.That(entMan.HasComponent<GaussChargeCellComponent>(magazine), Is.True,
                $"{magazineProto} needs GaussChargeCell to report its charge when examined on its own.");

            var cell = entMan.GetComponent<GaussChargeCellComponent>(magazine);
            Assert.That(cell.ChargeCost, Is.EqualTo(FireCost),
                "ChargeCost must match the gun's FireCost or the two examine lines would disagree.");

            var battery = entMan.GetComponent<BatteryComponent>(magazine);
            Assert.That(battery.MaxCharge, Is.EqualTo(expectedMaxCharge).Within(0.01f));

            // The tag is what puts the cell in the weapon battery chargers' whitelist.
            var tags = entMan.GetComponent<TagComponent>(magazine);
            Assert.That(tags.Tags, Does.Contain("N14GaussChargeCell"));
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Taking a round must go through the magazine chain and decrement the ballistic count.
    /// </summary>
    [Test]
    public async Task TakingAmmoUsesTheMagazine()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var user = entMan.SpawnEntity("MobHuman", map.GridCoords);
            var gun = entMan.SpawnEntity(RifleProto, map.GridCoords);

            var before = new GetAmmoCountEvent();
            entMan.EventBus.RaiseLocalEvent(gun, ref before);
            Assert.That(before.Count, Is.EqualTo(20));

            var ammo = new List<(EntityUid? Entity, IShootable Shootable)>();
            var take = new TakeAmmoEvent(1, ammo, map.GridCoords, user);
            entMan.EventBus.RaiseLocalEvent(gun, take);

            Assert.That(take.Ammo.Count, Is.EqualTo(1),
                "TakeAmmoEvent must yield exactly one cartridge.");

            // The shootable must be a real cartridge that spawns the gauss projectile, not a bare
            // bullet conjured by a server-only provider.
            var cartridge = take.Ammo[0].Entity;
            Assert.That(cartridge, Is.Not.Null);
            Assert.That(entMan.GetComponent<CartridgeAmmoComponent>(cartridge!.Value).Prototype,
                Is.EqualTo("N14Bullet2mmEC"));

            var after = new GetAmmoCountEvent();
            entMan.EventBus.RaiseLocalEvent(gun, ref after);
            Assert.That(after.Count, Is.EqualTo(before.Count - 1),
                "Taking a round must decrement the magazine.");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// The gauss flash lives on the cartridge, because that is the shootable handed to
    /// CreateAndFireProjectiles. Setting it on the spawned bullet alone is dead config.
    /// </summary>
    [Test]
    public async Task GaussFlashIsCarriedByTheCartridge()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var cartridge = entMan.SpawnEntity("N14Cartridge2mmEC", map.GridCoords);
            var ammo = entMan.GetComponent<CartridgeAmmoComponent>(cartridge);

            Assert.That(ammo.MuzzleFlash, Is.EqualTo(FlashProto.Id),
                "CreateAndFireProjectiles reads MuzzleFlash off the cartridge, so the gauss flash " +
                "has to be declared here to ever appear.");

            Assert.That(protoMan.HasIndex(FlashProto), Is.True,
                "GaussMuzzleFlashEffect prototype must exist, or the flash spawns nothing.");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// A shot that went off must cost charge. AttemptShoot raises GunShotEvent on the gun once the
    /// projectiles exist, and the amount is scaled by the shot count so a burst is charged per
    /// projectile rather than per trigger pull.
    /// </summary>
    [Test]
    public async Task GunShotDrainsChargeAndRefreshesMirror()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var user = entMan.SpawnEntity("MobHuman", map.GridCoords);
            var gun = entMan.SpawnEntity(RifleProto, map.GridCoords);
            var hybrid = entMan.GetComponent<HybridAmmoProviderComponent>(gun);

            var magazine = GetMagazine(entMan, gun);
            var battery = entMan.GetComponent<BatteryComponent>(magazine);

            // One projectile.
            var single = new GunShotEvent(user, new List<(EntityUid? Entity, IShootable Shootable)>
            {
                MakeCartridge(entMan, map.GridCoords, 1)[0],
            });

            entMan.EventBus.RaiseLocalEvent(gun, ref single);

            Assert.That(battery.CurrentCharge, Is.EqualTo(1000f - FireCost).Within(0.01f),
                "One projectile must cost exactly one FireCost.");
            Assert.That(hybrid.Charge, Is.EqualTo(battery.CurrentCharge).Within(0.01f),
                "The networked mirror must be republished after a shot or the client gate goes stale.");

            // Three projectiles in one shot (burst catch-up).
            var burst = new GunShotEvent(user, MakeCartridge(entMan, map.GridCoords, 3));
            entMan.EventBus.RaiseLocalEvent(gun, ref burst);

            Assert.That(battery.CurrentCharge, Is.EqualTo(1000f - FireCost * 4f).Within(0.01f),
                "A three-round burst must cost 3 * FireCost, not one FireCost.");
            Assert.That(hybrid.Charge, Is.EqualTo(battery.CurrentCharge).Within(0.01f));
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// The pre-fire gate is the whole reason the client can refuse a shot locally. It must cancel
    /// when the cell cannot pay and stay out of the way when it can.
    /// </summary>
    [Test]
    public async Task ShotIsRefusedWhenTheCellCannotPay()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var batteries = entMan.System<BatterySystem>();

        await server.WaitAssertion(() =>
        {
            var user = entMan.SpawnEntity("MobHuman", map.GridCoords);
            var gun = entMan.SpawnEntity(RifleProto, map.GridCoords);
            var gunComp = entMan.GetComponent<GunComponent>(gun);
            var hybrid = entMan.GetComponent<HybridAmmoProviderComponent>(gun);

            // Healthy cell: the hybrid layer must not interfere.
            var allowed = new ShotAttemptedEvent { User = user, Used = (gun, gunComp) };
            entMan.EventBus.RaiseLocalEvent(gun, ref allowed);
            Assert.That(allowed.Cancelled, Is.False,
                "A charged cell must not cancel the shot.");

            var magazine = GetMagazine(entMan, gun);
            var battery = entMan.GetComponent<BatteryComponent>(magazine);

            // Flatten the cell from outside the weapon.
            batteries.TrySetCharge(magazine, 10f, battery);
            entMan.EventBus.RaiseLocalEvent(gun, ref allowed);

            Assert.That(hybrid.Charge, Is.EqualTo(10f).Within(0.01f),
                "An externally discharged cell must still be reflected in the mirror.");

            var refused = new ShotAttemptedEvent { User = user, Used = (gun, gunComp) };
            entMan.EventBus.RaiseLocalEvent(gun, ref refused);

            Assert.That(refused.Cancelled, Is.True,
                $"A cell at 10J cannot pay FireCost {FireCost}J, so the shot must be refused " +
                "before it eats a cartridge.");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// The gate has to exist on the client, not just the server. This is the regression guard for the
    /// exact failure that shipped once already: <c>InitializeHybrid</c> lives in the shared partial,
    /// so dropping the call from the client <c>GunSystem.Initialize</c> left the client with zero
    /// subscriptions. The client then fired rounds the server refused, spawning projectiles only
    /// locally and stalling damage prediction for seconds when they hit something.
    ///
    /// A server-only test cannot catch this, because the server was always wired up correctly.
    /// </summary>
    [Test]
    public async Task ClientRefusesShotWhenTheCellCannotPay()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var cEntMan = client.ResolveDependency<IEntityManager>();

        // No mob here on purpose: SharedHumanoidAppearanceSystem resolves an IoC service that the
        // client instance does not register, so MobHuman cannot be spawned client-side. The gate
        // only reads Charge/FireCost, so the shooter identity is irrelevant to what is under test.
        var sGun = sEntMan.SpawnEntity(RifleProto, map.GridCoords);

        await pair.SyncTicks();
        await pair.RunTicksSync(5);

        var cGun = cEntMan.GetEntity(sEntMan.GetNetEntity(sGun));

        await client.WaitAssertion(() =>
        {
            var gunComp = cEntMan.GetComponent<GunComponent>(cGun);
            var hybrid = cEntMan.GetComponent<HybridAmmoProviderComponent>(cGun);

            // Mirror what the server publishes once the cell is flat.
            hybrid.Charge = 0f;
            hybrid.MaxCharge = 1000f;
            hybrid.HasCell = true;

            var refused = new ShotAttemptedEvent { User = cGun, Used = (cGun, gunComp) };
            cEntMan.EventBus.RaiseLocalEvent(cGun, ref refused);

            Assert.That(refused.Cancelled, Is.True,
                "The client must run the hybrid charge gate. Without it the client spends a cartridge " +
                "and spawns a bullet the server never creates, desyncing ammo and damage prediction.");

            // And the mirror must have arrived over the wire in the first place, since that is the
            // only thing the client has to gate on.
            hybrid.Charge = FireCost;
            hybrid.MaxCharge = 1000f;
            hybrid.HasCell = true;

            var allowed = new ShotAttemptedEvent { User = cGun, Used = (cGun, gunComp) };
            cEntMan.EventBus.RaiseLocalEvent(cGun, ref allowed);

            Assert.That(allowed.Cancelled, Is.False,
                "A cell that can pay exactly FireCost must be allowed to shoot.");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// The gauss magazine is a battery cell, so it must fit the weapon battery chargers. Their
    /// whitelists key off battery ammo-provider components that a magazine deliberately does not
    /// carry, so it needs an explicit tag entry or it simply cannot be inserted.
    /// </summary>
    [Test]
    public async Task GaussMagazineFitsWeaponBatteryCharger()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var slots = entMan.System<ItemSlotsSystem>();

        string[] chargers =
        [
            "WeaponCapacitorRecharger",
            "WallWeaponCapacitorRecharger",
            // Turbo accepts everything the weapon chargers do, so it has to accept the gauss cell too
            // or it would refuse a cell the plainer chargers charge.
            "TurboItemRecharger",
        ];

        await server.WaitAssertion(() =>
        {
            foreach (var proto in chargers)
            {
                foreach (var cell in new[] { RifleMagazine, PistolMagazine, MinigunMagazine })
                {
                    var charger = entMan.SpawnEntity(proto, map.GridCoords);
                    var magazine = entMan.SpawnEntity(cell, map.GridCoords);

                    Assert.That(slots.TryInsert(charger, "charger_slot", magazine, null), Is.True,
                        $"{proto} should accept {cell}: it is a weapon battery cell, and " +
                        "ChargerSystem finds its Battery directly on the entity.");
                }
            }

            // Whitelist and blacklist are checked independently, so adding tags to the turbo
            // whitelist must not have disarmed its blacklist.
            var turbo = entMan.SpawnEntity("TurboItemRecharger", map.GridCoords);
            var potato = entMan.SpawnEntity("PowerCellPotato", map.GridCoords);

            Assert.That(slots.TryInsert(turbo, "charger_slot", potato, null), Is.False,
                "TurboItemRecharger blacklists PotatoBattery; adding a whitelist tag must not change that.");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// The gauss cell sits in the gun_magazine ContainerSlot, which is what the weapon reads.
    /// </summary>
    private static EntityUid GetMagazine(IEntityManager entMan, EntityUid gun)
    {
        var containers = entMan.System<SharedContainerSystem>();
        Assert.That(containers.TryGetContainer(gun, MagazineSlot, out var container), Is.True,
            $"{gun} should have a {MagazineSlot} slot.");
        Assert.That(container, Is.InstanceOf<ContainerSlot>(),
            $"{MagazineSlot} must be a ContainerSlot for the weapon to read it.");
        return ((ContainerSlot) container!).ContainedEntity!.Value;
    }

    private static List<(EntityUid? Entity, IShootable Shootable)> MakeCartridge(
        IEntityManager entMan,
        EntityCoordinates coords,
        int count)
    {
        var ammo = new List<(EntityUid? Entity, IShootable Shootable)>(count);

        for (var i = 0; i < count; i++)
        {
            var uid = entMan.SpawnEntity("N14Cartridge2mmEC", coords);
            ammo.Add((uid, (IShootable) entMan.GetComponent<CartridgeAmmoComponent>(uid)));
        }

        return ammo;
    }
}
