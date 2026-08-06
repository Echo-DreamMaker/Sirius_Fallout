// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Misfits.Genetics.Abilities;

[RegisterComponent, NetworkedComponent]
public sealed partial class TriggerOnFlashedComponent : Component
{
    [DataField]
    public float Prob = 1f;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class TriggerOnWalkComponent : Component;
