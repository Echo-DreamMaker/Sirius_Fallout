// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Paper;
using Content.Shared.UserInterface;
using Content.Shared._Misfits.Paper;

namespace Content.Server._Misfits.Paper;

public sealed partial class BlockReadingSystem : EntitySystem
{
    public override void Initialize()
        => SubscribeLocalEvent<PaperComponent, ActivatableUIOpenAttemptEvent>(OnOpenAttempt);

    private void OnOpenAttempt(Entity<PaperComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (HasComp<BlockReadingComponent>(args.User))
            args.Cancel();
    }
}
