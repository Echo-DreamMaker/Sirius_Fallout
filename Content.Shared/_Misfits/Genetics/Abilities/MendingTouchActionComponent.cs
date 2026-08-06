// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Misfits.Genetics.Abilities;

/// <summary>
/// Mending Touch action: lay your hands on someone to pull a portion of their physical
/// injuries out of them and into your own body. You heal them at the cost of hurting yourself.
/// </summary>
// no Access restriction: the handling system lives in Content.Server (it needs the bloodstream)
[RegisterComponent, NetworkedComponent]
public sealed partial class MendingTouchActionComponent : Component
{
    /// <summary>
    /// Maximum total damage transferred from the target to the user per use.
    /// </summary>
    [DataField]
    public float Amount = 15f;

    /// <summary>
    /// Which damage types can be transferred. Defaults to physical (brute + burn) injuries;
    /// things like poison, radiation or genetic damage are left in the target.
    /// </summary>
    [DataField]
    public List<ProtoId<DamageTypePrototype>> Types = new()
    {
        "Blunt", "Slash", "Piercing", "Heat", "Cold", "Shock", "Caustic",
    };

    /// <summary>
    /// Using the touch on yourself heals you instead, but costs this fraction of your
    /// maximum blood volume every use. The wounds have to go somewhere.
    /// </summary>
    [DataField]
    public float SelfBloodFraction = 0.25f;
}

public sealed partial class MendingTouchActionEvent : EntityTargetActionEvent;
