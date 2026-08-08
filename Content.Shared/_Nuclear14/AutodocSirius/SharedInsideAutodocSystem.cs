using Content.Shared.Standing;
using Robust.Shared.Containers;

namespace Content.Shared._Nuclear14.AutodocSirius;

public abstract partial class SharedSiriusAutodocSystem
{
    public virtual void InitializeInsideAutodoc()
    {
        SubscribeLocalEvent<InsideAutodocComponent, DownAttemptEvent>(HandleDown);
        SubscribeLocalEvent<InsideAutodocComponent, EntGotRemovedFromContainerMessage>(OnEntGotRemovedFromContainer);
    }
    private void HandleDown(EntityUid uid, InsideAutodocComponent component, DownAttemptEvent args)
    {
        args.Cancel();
    }
    private void OnEntGotRemovedFromContainer(EntityUid uid, InsideAutodocComponent component, EntGotRemovedFromContainerMessage args)
    {
        if (Terminating(uid))
            return;
        RemComp<InsideAutodocComponent>(uid);
    }
}
