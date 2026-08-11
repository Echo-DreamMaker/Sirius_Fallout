using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Client.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    private void InitializeHybrid()
    {
        SubscribeLocalEvent<HybridAmmoProviderComponent, UpdateAmmoCounterEvent>(OnHybridUpdateAmmo);
        SubscribeLocalEvent<HybridAmmoProviderComponent, AmmoCounterControlEvent>(OnHybridControl);
    }

    private void OnHybridUpdateAmmo(EntityUid uid, HybridAmmoProviderComponent component, UpdateAmmoCounterEvent args)
    {
        if (args.Control is DefaultStatusControl control)
        {
            var ev = new GetAmmoCountEvent();
            RaiseLocalEvent(uid, ref ev, false);
            control.Update(ev.Count, ev.Capacity);
        }
    }

    private void OnHybridControl(EntityUid uid, HybridAmmoProviderComponent component, AmmoCounterControlEvent args)
    {
        args.Control = new DefaultStatusControl();
    }
}
