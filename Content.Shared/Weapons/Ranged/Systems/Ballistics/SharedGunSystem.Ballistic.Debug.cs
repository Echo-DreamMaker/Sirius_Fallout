using Content.Shared.Weapons.Ranged.Components;

namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    /// <summary>
    /// debug just for knowing which prototypes in yaml cause issues for future reference
    /// </summary>
    private void DebugInfo(EntityUid uid, BallisticAmmoProviderComponent comp)
    {
        if (comp.UnspawnedCount > 0 && comp.Proto is null)
            Log.Error($"Ballistic Comp has ammo but no ammo prototype... uid:{uid} Proto:{Prototype(uid)} ");

        if (comp.UnspawnedCount > comp.Capacity)
            Log.Error($"Ballistic Comp of Proto: {Prototype(uid)} owner UID:{uid} unspawnedCount > capacity: {comp.UnspawnedCount} > {comp.Capacity}");

        if (comp.AmmoCount > comp.Capacity)
            Log.Error($"Ballistic Comp of Proto: {Prototype(uid)} owner UID:{uid} GetBallisticShots(component) > capacity: {comp.AmmoCount} > {comp.Capacity}");

        if (comp.Container.ContainedEntities.Count > comp.Capacity)
            Log.Error($"Ballistic Comp of Proto: {Prototype(uid)} owner UID:{uid} Container.ContainedEntities.Count > capacity: {comp.Container.ContainedEntities.Count} > {comp.Capacity}");
    }
    public bool DebugFireRate(float fireRateModified)
    {
        Log.Debug($"fire rate negative: {fireRateModified}");
        return true;
    }

    //[System.Diagnostics.Conditional("DEBUG")]
    public bool DebugCheckNullAmmo(IReadOnlyList<EntityUid>? list, int index)
    {
        bool isGood = true;
        if (list is null)
        {
            Log.Debug($"Client Ammo List is null???");
            isGood = false;
            return isGood;
        }
        for (int i = 0; i < list.Count; i++)
        {
            if (list.ElementAtOrDefault(i) == default)
            {
                Log.Debug($"Client Ammo List of size {list.Count} has gap at index:{i}");
                isGood = false;
            }
        }
        if (index > list.Count - 1)
        {
            Log.Debug($"index of {index} out of range for list of size {list.Count}!!");
            isGood = false;
        }
        if (index < 0)
        {
            Log.Debug($"{index} index is fucking negative???!!");
            isGood = false;

        }
        return isGood;
    }
    //[System.Diagnostics.Conditional("DEBUG")]
    public bool DebugAmmoProviderChange(BallisticAmmoProviderComponent giverComp)
    {
        Log.Debug($"SpawnedCountPredict: {giverComp.SpawnedCountPredict}");
        Log.Debug($"UnspawnedCount: {giverComp.UnspawnedCount}");
        Log.Debug($"Index: {giverComp.IndexPredict}");
        Log.Debug($"Cur Tick: {Timing.CurTick}");
        return true;
    }
    //[System.Diagnostics.Conditional("DEBUG")]
    public bool DebugAmmoProviderClientDirty(EntityUid uid)
    {
        Log.Debug($"Dirtied uid:{MetaData(uid).EntityName} on tick:{Timing.CurTick.Value}");
        return true;
    }
    private bool DebugEjectCartRNG(int seed, int ammoCount, Vector2 pRNG, Vector2 pBase, Angle rRNG)
    {
        Log.Debug($"curTick:{Timing.CurTick} seed: {seed} ammoCount: {ammoCount} RngPos:{pRNG} basePos:{pBase} , RngR:{rRNG.Reduced()}");
        return true;
    }
}
