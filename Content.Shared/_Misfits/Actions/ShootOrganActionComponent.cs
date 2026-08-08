// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;
using Content.Shared.Polymorph;

namespace Content.Shared._Misfits.Actions;

[RegisterComponent, NetworkedComponent]
public sealed partial class ShootOrganActionComponent : Component
{
    [DataField(required: true)]
    public string Organ = string.Empty;

    [DataField(required: true)]
    public ProtoId<PolymorphPrototype> Polymorph;

    /// <summary>
    /// Projectile to spawn directly when the body has no matching organ.
    /// Most bodies in this fork don't actually have tongue organ slots, so without
    /// this the action always failed with "you don't have a tongue".
    /// </summary>
    [DataField]
    public EntProtoId? Fallback;

    /// <summary>
    /// How long the organ takes to regrow after firing. While it regrows the user
    /// can't speak right (stutters).
    /// </summary>
    [DataField]
    public TimeSpan RegrowTime = TimeSpan.FromSeconds(60);
}

public sealed partial class ShootOrganActionEvent : WorldTargetActionEvent;
