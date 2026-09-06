using Content.Shared.Access.Systems;
using Content.Shared.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC.Vendors;

public abstract partial class SharedCMAutomatedVendorSystem : EntitySystem
{
    [Dependency] private AccessReaderSystem _access = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMAutomatedVendorComponent, ActivatableUIOpenAttemptEvent>(OnOpenAttempt);
    }

    protected virtual void OnOpenAttempt(Entity<CMAutomatedVendorComponent> vendor, ref ActivatableUIOpenAttemptEvent args)
    {
        if (vendor.Comp.Hacked || _access.IsAllowed(args.User, vendor))
            return;

        args.Cancel();
    }
}
