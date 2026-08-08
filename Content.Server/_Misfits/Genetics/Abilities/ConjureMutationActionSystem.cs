// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Misfits.Genetics.Abilities;

namespace Content.Server._Misfits.Genetics.Abilities;

public sealed class ConjureMutationActionSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ConjureMutationActionEvent>(OnConjure);
    }

    private void OnConjure(ConjureMutationActionEvent args)
    {
        if (args.Handled)
            return;

        Spawn(args.Prototype, Transform(args.Performer).Coordinates);
        args.Handled = true;
    }
}
