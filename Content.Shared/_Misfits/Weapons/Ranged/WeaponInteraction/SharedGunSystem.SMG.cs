using Content.Shared.Interaction.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Wieldable;

namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{


    protected void InitializeSMGInteractions()
    {
        // SubscribeLocalEvent<ChamberMagazineAmmoProviderComponent, BeforeUseInHandEvent>(PreChamberUse);
        SubscribeLocalEvent<GunSMGInteractComponent, BeforeUseInHandEvent>(OnUseInHand);
    }

    private void OnUseInHand(EntityUid uid, GunSMGInteractComponent component, BeforeUseInHandEvent args)
    {

        if (TryComp<ChamberMagazineAmmoProviderComponent>(uid, out var comp)
            && comp.BoltClosed == false)
        {
            args.Handled = true;
            SetBoltClosed(uid, comp, !comp.BoltClosed.Value, args.User);
            //ToggleBolt(uid, comp, args.User);
        }



        //component.BoltClosed

    }

    private void PreChamberUse(EntityUid uid, ChamberMagazineAmmoProviderComponent component, BeforeUseInHandEvent args)
    {

    }

}
