// #Misfits Add - Power armour combat drop. A suited passenger can step out of a cruising
// vertibird and ride the fall down a Z-level, landing hard but unhurt.
using Content.Shared.Actions;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Misfits.Vehicles.Vertibird;

/// <summary>
/// Sits on the vertibird. Hands a drop action to any occupant wearing power armour
/// while the craft is cruising with a level underneath it.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class VertibirdCombatDropComponent : Component
{
    [DataField]
    public EntProtoId DropAction = "ActionVertibirdCombatDrop";

    /// <summary>
    /// Impact effect spawned at the landing point.
    /// </summary>
    [DataField]
    public EntProtoId ImpactEffect = "VertibirdCombatDropImpactEffect";

    /// <summary>
    /// Occupants who currently hold the drop action, so it can be taken back cleanly.
    /// </summary>
    [ViewVariables]
    public Dictionary<EntityUid, EntityUid> DropActions = new();

    /// <summary>
    /// Radius in which the landing rattles screens.
    /// </summary>
    [DataField]
    public float ShakeRange = 10f;

    /// <summary>
    /// Camera kick magnitude at the point of impact, falling off to nothing at ShakeRange.
    /// </summary>
    [DataField]
    public float ShakeStrength = 4f;

    [DataField]
    public SoundSpecifier ImpactSound = new SoundCollectionSpecifier("MetalSlam");
}

public sealed partial class VertibirdCombatDropActionEvent : InstantActionEvent;
