// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Misfits.Genetics.Abilities;

[RegisterComponent, NetworkedComponent]
public sealed partial class RandomTriggerComponent : Component
{
    [DataField(required: true)]
    public float Prob;

    [DataField]
    public TimeSpan UpdateDelay = TimeSpan.FromSeconds(1);

    public TimeSpan NextUpdate;
}
