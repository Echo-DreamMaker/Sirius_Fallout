using Content.Shared.Interaction.Events;
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Shared.Weapons.Ranged.Systems;

public partial class SharedGunSystem
{


    protected virtual void Misfit_InitializeRevolver()
    {
        SubscribeLocalEvent<RevolverAmmoProviderComponent, BeforeUseInHandEvent>(BeforeOnRevolverUse);
    }

    private void BeforeOnRevolverUse(EntityUid gunUid, RevolverAmmoProviderComponent comp, BeforeUseInHandEvent args)
    {
        if (GetRevolverUnspentCount(comp) > 0) return;
        EmptyRevolver(gunUid, comp, args.User);
        args.Handled = true;
    }
}

