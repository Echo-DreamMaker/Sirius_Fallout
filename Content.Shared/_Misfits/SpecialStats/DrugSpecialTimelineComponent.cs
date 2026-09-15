// #Misfits Add - Tracks a multi-phase drug timeline that writes absolute SPECIAL
// stat deltas into DrugSpecialBoostComponent on each phase transition.
// Server-only phases list; client only sees current phase index + resist set via AutoNetworked fields.

using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.SpecialStats;

/// <summary>
///     A single phase in a drug's lifecycle.
///     Stat values are ABSOLUTE (not deltas) — they replace the previous phase's values entirely.
///     <see cref="DamageResistSet"/> is a <c>DamageModifierSetPrototype</c> id (e.g. "N14DrugResist50"),
///     or <c>null</c> for no resistance.
/// </summary>
[Serializable, NetSerializable, DataDefinition]
public sealed partial class DrugTimelinePhase
{
    /// <summary>Seconds offset from the timeline anchor when this phase becomes active.</summary>
    [DataField("time")]
    public float Time;

    [DataField("strengthBoost")] public int Strength;
    [DataField("perceptionBoost")] public int Perception;
    [DataField("enduranceBoost")] public int Endurance;
    [DataField("charismaBoost")] public int Charisma;
    [DataField("agilityBoost")] public int Agility;
    [DataField("intelligenceBoost")] public int Intelligence;
    [DataField("luckBoost")] public int Luck;

    /// <summary>
    ///     DamageModifierSetPrototype id applied during this phase (e.g. "N14DrugResist50").
    ///     <c>null</c> or empty = no damage resistance.
    /// </summary>
    [DataField("damageResistSet")]
    public string? DamageResistSet;
}

/// <summary>
///     Server-authoritative timeline component for multi-phase drug effects.
///     Managed by <see cref="DrugSpecialTimelineSystem"/>.
///     Writes the current phase's stat deltas into <see cref="DrugSpecialBoostComponent"/>
///     and manages damage resistance via <c>DamageableComponent.DamageModifierSets</c>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DrugSpecialTimelineComponent : Component
{
    /// <summary>Prototype id of the reagent that started this timeline.</summary>
    [DataField, AutoNetworkedField]
    public string ReagentId = string.Empty;

    /// <summary>Game-time when the first phase was applied (timeline anchor).</summary>
    [DataField(serverOnly: true)]
    public TimeSpan Anchor;

    /// <summary>
    ///     Last game-time this reagent was observed during metabolism.
    ///     Used to detect a NEW dose (gap longer than 10 s) so the timeline can re-anchor.
    /// </summary>
    [DataField(serverOnly: true)]
    public TimeSpan LastSeen;

    /// <summary>
    ///     Last observed reagent quantity in the bloodstream.
    ///     A quantity increase between observations indicates the player dosed again.
    /// </summary>
    [DataField(serverOnly: true)]
    public FixedPoint2 LastQuantity;

    /// <summary>Index of the currently active phase in <see cref="Phases"/>.</summary>
    [DataField, AutoNetworkedField]
    public int PhaseIndex;

    /// <summary>Ordered list of phases (server-only). Never sent to clients.</summary>
    [DataField(serverOnly: true)]
    public List<DrugTimelinePhase> Phases = new();

    /// <summary>
    ///     The DamageModifierSetPrototype id currently applied for damage resistance.
    ///     Managed by the system on phase transitions.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? ActiveResistSet;
}
