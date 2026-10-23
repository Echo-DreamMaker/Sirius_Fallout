using System.Collections.Generic;
using System.Numerics;
using Content.Server.NPC.Components;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using ServerGunSystem = Content.Server.Weapons.Ranged.Systems.GunSystem;

namespace Content.IntegrationTests.Tests._Misfits.GunSystem;

/// <summary>
/// Regression probe for the bloatfly (Дутень) reported as "it makes a noise but no acid comes out".
/// The creature shoots with a plain BallisticAmmoProvider on its own body, so the shot path under
/// test is the ordinary shared one — nothing here involves the hybrid gauss providers.
///
/// The layers are checked separately so a failure names itself:
///   1. <see cref="DirectShotLeavesTheBarrelOnServer"/> — can the gun alone produce a projectile?
///   2. <see cref="DirectShotIsVisibleToTheClient"/> — does that projectile reach a player?
///   3. <see cref="NpcCombatSystemActuallyPullsTheTrigger"/> — does the NPC combat layer fire at all?
///   4. <see cref="SpentCreatureAmmoNeverComesBack"/> — documents the one layer that is broken:
///      the magazine is finite and the recharge component wired to it can never run.
/// </summary>
[TestFixture]
public sealed class CreatureAcidShotTest
{
    private const string FlyProto = "N14MobBloatfly";
    private const string AcidProto = "N14BulletAcid";
    private const string TargetProto = "CrateGenericSteel";

    private static int CountAcid(IEntityManager entMan)
    {
        var count = 0;
        var query = entMan.AllEntityQueryEnumerator<ProjectileComponent>();

        while (query.MoveNext(out var uid, out _))
        {
            if (entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == AcidProto)
                count++;
        }

        return count;
    }

    /// <summary>
    /// Counts by prototype alone, so a client that received the entity without
    /// ProjectileComponent is told apart from one that received nothing at all.
    /// </summary>
    private static int CountProto(IEntityManager entMan, string protoId)
    {
        var count = 0;
        var query = entMan.AllEntityQueryEnumerator<MetaDataComponent>();

        while (query.MoveNext(out var uid, out _))
        {
            if (entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == protoId)
                count++;
        }

        return count;
    }

    private static int CountAll(IEntityManager entMan)
    {
        var count = 0;
        var query = entMan.AllEntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out _, out _))
            count++;

        return count;
    }

    private static EntityUid FindAcid(IEntityManager entMan)
    {
        var query = entMan.AllEntityQueryEnumerator<ProjectileComponent>();

        while (query.MoveNext(out var uid, out _))
        {
            if (entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == AcidProto)
                return uid;
        }

        return EntityUid.Invalid;
    }

    /// <summary>
    /// Aims the creature's own gun straight at open floor and fires it once.
    /// </summary>
    [Test]
    public async Task DirectShotLeavesTheBarrelOnServer()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var gunSys = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<ServerGunSystem>();

        await server.WaitAssertion(() =>
        {
            var fly = entMan.SpawnEntity(FlyProto, map.GridCoords);

            var ammo = entMan.GetComponent<BallisticAmmoProviderComponent>(fly);
            Assert.That(ammo.AmmoCount, Is.GreaterThan(0),
                $"{FlyProto} spawns with no ammo at all, so it can never produce a projectile.");

            var gun = entMan.GetComponent<GunComponent>(fly);
            var target = map.GridCoords.Offset(new Vector2(3f, 0f));

            gunSys.AttemptShoot(fly, fly, gun, target);

            Assert.That(CountAcid(entMan), Is.EqualTo(1),
                "The gun path produced no acid spit. Ammo was reported available, so either " +
                "ShotAttemptedEvent was cancelled, TakeAmmoEvent came back empty, or Shoot() " +
                "failed to spawn the projectile.");

            // The projectile entity is created by Spawn(giverComp.Proto), which puts it in
            // MapCoordinates.Nullspace. It only becomes visible if ShootProjectile reparents it
            // onto a real map, so check where it actually landed, not merely that it exists.
            var acid = FindAcid(entMan);
            var xform = entMan.GetComponent<TransformComponent>(acid);

            Assert.That(xform.MapID == map.MapId && xform.ParentUid != EntityUid.Invalid, Is.True,
                $"The acid projectile is at MapId {xform.MapID} (test map is {map.MapId}) with " +
                $"parent {xform.ParentUid}. OnBallisticTakeAmmo creates it with Spawn(proto), which " +
                "is MapCoordinates.Nullspace, and ShootProjectile's reparent-to-map step derives its " +
                "destination from GetMapCoordinates of the projectile itself - so a nullspace " +
                "projectile is reparented to nullspace again. An entity outside every map is never " +
                "replicated to a player and never collides, which is exactly 'the gun makes its shot " +
                "sound but no projectile ever appears'.");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Same shot, but checked from a connected client: the projectile must exist in the pool
    /// entity manager, not only on the server.
    /// </summary>
    [Test]
    public async Task DirectShotIsVisibleToTheClient()
    {
        // Connected is load-bearing: without it the client is never in PVS and simply holds zero
        // entities, which looks identical to "the projectile was not replicated".
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        var sEntMan = server.ResolveDependency<IEntityManager>();
        var cEntMan = client.ResolveDependency<IEntityManager>();
        var gunSys = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<ServerGunSystem>();

        await server.WaitAssertion(() =>
        {
            var fly = sEntMan.SpawnEntity(FlyProto, map.GridCoords);
            var gun = sEntMan.GetComponent<GunComponent>(fly);

            gunSys.AttemptShoot(fly, fly, gun, map.GridCoords.Offset(new Vector2(3f, 0f)));
            Assert.That(CountAcid(sEntMan), Is.EqualTo(1), "Server-side shot failed.");
        });

        await pair.SyncTicks();
        await pair.RunTicksSync(10);

        var serverAfter = 0;
        var acidMap = MapId.Nullspace;
        var acidParent = EntityUid.Invalid;
        await server.WaitAssertion(() =>
        {
            serverAfter = CountAcid(sEntMan);
            Assert.That(serverAfter, Is.EqualTo(1),
                "The acid spit vanished from the server before it could be sent, so this is a " +
                "server-side lifetime problem rather than a replication one.");

            var acid = FindAcid(sEntMan);
            var xform = sEntMan.GetComponent<TransformComponent>(acid);
            acidMap = xform.MapID;
            acidParent = xform.ParentUid;
        });

        await client.WaitAssertion(() =>
        {
            var seen = CountProto(cEntMan, AcidProto);
            var withProjectile = CountAcid(cEntMan);
            var clientTotal = CountAll(cEntMan);

            Assert.That(seen, Is.EqualTo(serverAfter),
                $"Client received {seen} entity/entities with prototype {AcidProto} and " +
                $"{withProjectile} of them carrying ProjectileComponent, while the server still " +
                $"holds {serverAfter}. Client entity total = {clientTotal} " +
                "(0 would mean the client is not in PVS for this map at all). " +
                $"Server-side projectile location: MapId {acidMap} (test map {map.MapId}), " +
                $"parent {acidParent}.");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// The creature's recharge used to be a no-op: <c>RechargeBasicEntityAmmoSystem.Update</c>
    /// joined <c>RechargeBasicEntityAmmoComponent</c> with <c>BasicEntityAmmoProviderComponent</c>,
    /// but this prototype carries a <c>BallisticAmmoProviderComponent</c> instead, so the query
    /// never matched and the 100-round magazine never refilled.
    ///
    /// That matters beyond the raw ammo count: NPCCombatSystem.Ranged treats "zero ammo AND has a
    /// recharge component" as "still recharging" and keeps the mob in ranged combat forever, so a
    /// spent bloatfly aims at its target indefinitely and never falls back to melee.
    /// </summary>
    [Test]
    public async Task SpentCreatureAmmoRefills()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var fly = EntityUid.Invalid;
        var capacity = 0;

        await server.WaitAssertion(() =>
        {
            fly = entMan.SpawnEntity(FlyProto, map.GridCoords);

            Assert.That(entMan.HasComponent<RechargeBasicEntityAmmoComponent>(fly), Is.True,
                "The prototype asks for a recharge component, so the NPC ammo gate will treat an " +
                "empty magazine as 'still recharging' rather than giving up on ranged combat.");

            Assert.That(entMan.HasComponent<BasicEntityAmmoProviderComponent>(fly), Is.False,
                "The recharge system must handle this creature through the ballistic query; if it " +
                "gains a BasicEntityAmmoProvider the old query path covers it and this test is moot.");

            var ammo = entMan.GetComponent<BallisticAmmoProviderComponent>(fly);
            capacity = ammo.AmmoCount;
            Assert.That(capacity, Is.GreaterThan(0), "Spawned with no ammo.");

            // Drain the magazine through the real ammo-taking path rather than writing the field,
            // so this keeps failing for the right reason if the field is ever made read-only.
            var coords = entMan.GetComponent<TransformComponent>(fly).Coordinates;
            for (var i = 0; i < capacity; i++)
            {
                entMan.EventBus.RaiseLocalEvent(fly,
                    new TakeAmmoEvent(1, new List<(EntityUid?, IShootable)>(), coords, fly));
            }

            Assert.That(ammo.AmmoCount, Is.EqualTo(0), "Drain did not empty the magazine.");
        });

        // Well past rechargeCooldown (1s) - needs enough ticks for the timer to fire at all,
        // then a couple of further cooldown cycles.
        await pair.RunTicksSync(120);

        await server.WaitAssertion(() =>
        {
            var ammo = entMan.GetComponent<BallisticAmmoProviderComponent>(fly);
            Assert.That(ammo.AmmoCount, Is.GreaterThan(0),
                "A drained prototype-fed magazine never refilled: RechargeBasicEntityAmmoSystem " +
                "does not query BallisticAmmoProviderComponent.");
            Assert.That(ammo.AmmoCount, Is.LessThanOrEqualTo(capacity),
                $"Recharge overshot capacity: {ammo.AmmoCount} > {capacity}.");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Drives the real <c>NPCCombatSystem</c> path instead of calling the gun directly: line of
    /// sight, rotation, ammo-count gate and the shoot delay all have to line up for a creature
    /// to actually pull its own trigger.
    /// </summary>
    [Test]
    public async Task NpcCombatSystemActuallyPullsTheTrigger()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var fly = entMan.SpawnEntity(FlyProto, map.GridCoords);
            var target = entMan.SpawnEntity(TargetProto, map.GridCoords.Offset(new Vector2(3f, 0f)));

            Assert.That(entMan.HasComponent<PhysicsComponent>(target), Is.True,
                $"{TargetProto} has no physics body; the NPC combat system drops any target that does.");

            // The HTN GunOperator normally adds this mid-plan. Adding it by hand lets us exercise
            // NPCCombatSystem without also depending on the planner finding a target.
            var ranged = entMan.EnsureComponent<NPCRangedCombatComponent>(fly);
            ranged.Target = target;
            ranged.Status = CombatStatus.Normal;
            ranged.ShootDelay = 0f;
            ranged.ShootAccumulator = 1f;
            ranged.LOSAccumulator = 1f;
            ranged.TargetInLOS = true;
        });

        // The projectile only lives for TimedDespawn's 1s and reaches the crate long before a
        // single late sample would run, so poll instead: what matters is that at some point a
        // projectile existed AND sat on the test map, which is the property the bug broke.
        var seenOnMap = false;
        var wrongPlace = "";
        var neverAppeared = "";

        for (var i = 0; i < 30 && !seenOnMap; i++)
        {
            await pair.RunTicksSync(1);

            await server.WaitAssertion(() =>
            {
                var acid = FindAcid(entMan);

                if (acid == EntityUid.Invalid)
                {
                    neverAppeared =
                        "NPCCombatSystem ran with a valid target, clear line of sight and a full " +
                        "magazine, yet no projectile was ever spawned. Either CanShoot/TryGetGun " +
                        "rejected the shot, the ammo-count gate kept waiting, or AttemptShoot " +
                        "returned before TakeAmmo.";
                    return;
                }

                var xform = entMan.GetComponent<TransformComponent>(acid);

                if (xform.MapID == map.MapId && xform.ParentUid != EntityUid.Invalid)
                {
                    seenOnMap = true;
                }
                else
                {
                    wrongPlace = $"The projectile exists but sits at MapId {xform.MapID} " +
                                 $"(test map is {map.MapId}) with parent {xform.ParentUid}. " +
                                 "It is outside every map, so no player can receive it and it " +
                                 "can never collide.";
                }
            });
        }

        Assert.That(seenOnMap, Is.True, wrongPlace.Length > 0 ? wrongPlace : neverAppeared);

        await pair.CleanReturnAsync();
    }
}
