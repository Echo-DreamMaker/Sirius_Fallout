using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Server.Power.EntitySystems;
using Content.Shared.Power.Components;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Content.Server.Power.Components;

namespace Content.Server.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    private void InitializeHybrid()
    {
        SubscribeLocalEvent<HybridAmmoProviderComponent, TakeAmmoEvent>(OnHybridTakeAmmo);
        SubscribeLocalEvent<HybridAmmoProviderComponent, GetAmmoCountEvent>(OnHybridGetAmmoCount);
        SubscribeLocalEvent<HybridAmmoProviderComponent, EntInsertedIntoContainerMessage>(OnHybridMagazineInsert);
        SubscribeLocalEvent<HybridAmmoProviderComponent, EntRemovedFromContainerMessage>(OnHybridMagazineRemove);
    }

    private void OnHybridMagazineInsert(EntityUid uid, HybridAmmoProviderComponent component, EntInsertedIntoContainerMessage args)
    {
        if (component.MagazineSlot != args.Container.ID)
            return;
        UpdateAmmoCount(uid);
    }

    private void OnHybridMagazineRemove(EntityUid uid, HybridAmmoProviderComponent component, EntRemovedFromContainerMessage args)
    {
        if (component.MagazineSlot != args.Container.ID)
            return;
        UpdateAmmoCount(uid);
    }

    private void OnHybridTakeAmmo(EntityUid uid, HybridAmmoProviderComponent component, TakeAmmoEvent args)
    {
        // 1. Получаем магазин из слота
        var magazineEntity = GetMagazineEntity(uid);
        if (magazineEntity == null)
        {
            args.Reason = Loc.GetString("gun-no-magazine");
            return;
        }

        // 2. Проверяем патроны (BallisticAmmoProvider)
        if (!TryComp<BallisticAmmoProviderComponent>(magazineEntity.Value, out var ballistic))
        {
            args.Reason = Loc.GetString("gun-no-ammo");
            return;
        }

        // Получаем текущее количество патронов
        var currentCount = GetBallisticShots(ballistic);
        if (currentCount <= 0)
        {
            args.Reason = Loc.GetString("gun-no-ammo");
            return;
        }

        // 3. Проверяем энергию (BatteryComponent)
        if (!TryComp<BatteryComponent>(magazineEntity.Value, out var battery))
        {
            args.Reason = Loc.GetString("gun-no-battery");
            return;
        }
        if (battery.CurrentCharge < component.FireCost)
        {
            args.Reason = Loc.GetString("gun-not-enough-energy");
            return;
        }

        // 4. Тратим патрон: удаляем последний патрон из контейнера или уменьшаем UnspawnedCount
        if (ballistic.Container.ContainedEntities.Count > 0)
        {
            var lastEntity = ballistic.Container.ContainedEntities[^1];
            Containers.Remove(lastEntity, ballistic.Container);
            QueueDel(lastEntity); // Удаляем сущность патрона (гильза не нужна)
        }
        else if (ballistic.UnspawnedCount > 0)
        {
            ballistic.UnspawnedCount--;
        }
        else
        {
            args.Reason = Loc.GetString("gun-no-ammo");
            return;
        }

        // 5. Тратим энергию
        _battery.UseCharge(magazineEntity.Value, component.FireCost);

        // 6. Создаём снаряд в координатах выстрела
        var fromCoordinates = args.Coordinates;
        var mapCoords = fromCoordinates.ToMap(EntityManager, _transform);
        var projectile = Spawn(component.Prototype, mapCoords);

        // 7. Добавляем снаряд в список для выстрела (основной GunSystem обработает его)
        args.Ammo.Add((projectile, EnsureShootable(projectile)));

        // 8. Обновляем счётчик на клиенте
        Dirty(magazineEntity.Value, ballistic);
        UpdateAmmoCount(uid);
    }

    private void OnHybridGetAmmoCount(EntityUid uid, HybridAmmoProviderComponent component, ref GetAmmoCountEvent args)
    {
        var magazineEntity = GetMagazineEntity(uid);
        if (magazineEntity != null && TryComp<BallisticAmmoProviderComponent>(magazineEntity.Value, out var ballistic))
        {
            args.Count = GetBallisticShots(ballistic);
            args.Capacity = ballistic.Capacity;
        }
        else
        {
            args.Count = 0;
            args.Capacity = 0;
        }
    }

    private int GetBallisticShots(BallisticAmmoProviderComponent component)
    {
        return component.UnspawnedCount + component.Container.ContainedEntities.Count;
    }
}
