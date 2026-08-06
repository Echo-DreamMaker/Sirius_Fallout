// SPDX-License-Identifier: AGPL-3.0-or-later


namespace Content.Shared._Misfits.Genetics.Abilities;

/// <summary>
/// Adds an offset to cold and/or heat damage thresholds.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class TemperatureDamageMutationComponent : Component
{
    [DataField]
    public float ColdOffset;

    [DataField]
    public float HeatOffset;
}
