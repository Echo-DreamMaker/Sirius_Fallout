using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using System.Collections.Generic;
using System.Numerics;

namespace Content.IntegrationTests.Tests._Misfits.GunSystem;

/// <summary>
/// Verifies that magazine-fed miniguns actually produce projectiles: the ammo
/// provider must hand out cartridges and the gun must turn them into bullets.
/// </summary>
[TestFixture]
public sealed class MinigunAmmoFireTest
{
    private static readonly string[] MinigunProtos =
    [
        "N14WeaponMinigun",
        "N14WeaponMinigunAvenger",
        "N14WeaponMECGaussMinigunSirius",
    ];

    [Test]
    public async Task MinigunTakeAmmoAndShootProducesProjectile()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var guns = entMan.System<SharedGunSystem>();

        foreach (var proto in MinigunProtos)
        {
            await server.WaitAssertion(() =>
            {
                var user = entMan.SpawnEntity("MobHuman", map.GridCoords);
                var gunUid = entMan.SpawnEntity(proto, map.GridCoords);
                var gun = entMan.GetComponent<GunComponent>(gunUid);

                var ammoBefore = new GetAmmoCountEvent();
                entMan.EventBus.RaiseLocalEvent(gunUid, ref ammoBefore);
                Assert.That(ammoBefore.Count, Is.GreaterThan(0),
                    $"{proto}: should have ammo loaded on spawn, got {ammoBefore.Count}.");

                // Take one round from the ammo provider (magazine -> box -> cartridge).
                var ammo = new List<(EntityUid? Entity, IShootable Shootable)>();
                var take = new TakeAmmoEvent(1, ammo, map.GridCoords, user);
                entMan.EventBus.RaiseLocalEvent(gunUid, take);
                Assert.That(take.Ammo.Count, Is.EqualTo(1),
                    $"{proto}: TakeAmmoEvent should yield one cartridge.");

                var ammoAfter = new GetAmmoCountEvent();
                entMan.EventBus.RaiseLocalEvent(gunUid, ref ammoAfter);
                Assert.That(ammoAfter.Count, Is.EqualTo(ammoBefore.Count - 1),
                    $"{proto}: taking ammo should decrement the count ({ammoBefore.Count} -> {ammoAfter.Count}).");

                // Now shoot that cartridge and expect a real projectile on the map.
                var projectileCountBefore = CountBullets(entMan);
                var fromCoords = map.GridCoords;
                var toCoords = new EntityCoordinates(gunUid, new Vector2(10f, 0f));
                var projectiles = guns.Shoot(gunUid, gun, take.Ammo, fromCoords, toCoords, out _, user);
                var projectileCountAfter = CountBullets(entMan);

                TestContext.Out.WriteLine(
                    $"{proto}: ammo {ammoBefore.Count} -> {ammoAfter.Count}, " +
                    $"bullets {projectileCountBefore} -> {projectileCountAfter}, " +
                    $"shotProjectiles={projectiles?.Count ?? 0}");

                Assert.That(projectileCountAfter, Is.GreaterThan(projectileCountBefore),
                    $"{proto}: shooting should spawn a projectile.");
            });
        }

        await pair.CleanReturnAsync();
    }

    private static int CountBullets(IEntityManager entMan)
    {
        var count = 0;
        var query = entMan.EntityQueryEnumerator<ProjectileComponent>();
        while (query.MoveNext(out _))
            count++;
        return count;
    }
}