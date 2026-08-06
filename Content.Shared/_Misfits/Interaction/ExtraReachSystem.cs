// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Interaction;

namespace Content.Shared._Misfits.Interaction;

public sealed class ExtraReachSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        // run before TK so it can use the extra reach for its check
        SubscribeLocalEvent<ExtraReachComponent, InRangeOverrideEvent>(OnRangeOverride,
            before: new[] { typeof(TelekinesisSystem) });
    }

    private void OnRangeOverride(Entity<ExtraReachComponent> ent, ref InRangeOverrideEvent args)
    {
        var userXform = Transform(args.User);
        var targetXform = Transform(args.Target);
        if (userXform.MapUid != targetXform.MapUid)
            return;

        var userPos = _transform.GetMapCoordinates(args.User, userXform).Position;
        var targetPos = _transform.GetMapCoordinates(args.Target, targetXform).Position;
        var range = SharedInteractionSystem.InteractionRange + ent.Comp.Bonus;
        args.Handled = true;
        args.InRange = (userPos - targetPos).LengthSquared() <= range * range;
    }

}
