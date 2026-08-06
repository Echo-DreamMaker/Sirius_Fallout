// SPDX-License-Identifier: AGPL-3.0-or-later


namespace Content.Shared._Misfits.Genetics.Abilities;

/// <summary>
/// Component for the strength mutation.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class StrengthMutationComponent : Component
{
    /// <summary>
    /// Modifier for melee damage dealt.
    /// </summary>
    [DataField]
    public float MeleeModifier = 1.2f;
}
