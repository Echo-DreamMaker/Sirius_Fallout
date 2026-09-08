
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{

    protected virtual void InitCompGen()
    {
        SubscribeLocalEvent<BallisticAmmoProviderComponent, ComponentGetState>(OnGetState);
        SubscribeLocalEvent<BallisticAmmoProviderComponent, ComponentHandleState>(OnHandleState);

    }

    private void OnGetState(EntityUid uid, BallisticAmmoProviderComponent comp, ref ComponentGetState args)
    {
        // Get full state
        args.State = new BallisticAmmoState
        {
            UnspawnedCount = comp.UnspawnedCount,
            SpawnedCountPredict = comp.Container.Count,
            CurIndex = comp.IndexPredict,
            FromTick = Timing.CurTick.Value
        };
    }

    private void OnHandleState(EntityUid uid, BallisticAmmoProviderComponent comp, ref ComponentHandleState args)
    {
        BallisticAmmoState? stateToApply = null;
        var ev = new OnCompHandling(args.Current, args.Next, stateToApply);
        RaiseLocalEvent(ev);

        if (ev.StateToApply is not BallisticAmmoState state)
            return;

        comp.UnspawnedCount = state.UnspawnedCount;
        comp.SpawnedCountPredict = state.SpawnedCountPredict;
        comp.IndexPredict = state.CurIndex;
        UpdateBallisticAppearance(uid, comp);
        UpdateAmmoCount(uid);

    }

    [Serializable, NetSerializable]
    public sealed class BallisticAmmoState : IComponentState
    {
        public int UnspawnedCount = default;
        public int SpawnedCountPredict = default;
        public int CurIndex = default;
        public uint FromTick = default;
    }

}


