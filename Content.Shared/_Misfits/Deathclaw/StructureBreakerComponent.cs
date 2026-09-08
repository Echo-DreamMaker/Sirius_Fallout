// #Cythisiax Added - Shared marker for Bwonsamdi's structure-breaking melee validation.
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Misfits.Deathclaw;

[RegisterComponent, NetworkedComponent]
public sealed partial class StructureBreakerComponent : Component
{
    /// <summary>
    /// Sound played after a structure is successfully broken through the special bypass.
    /// </summary>
    [DataField]
    public SoundSpecifier BreakSound = new SoundPathSpecifier("/Audio/Effects/break_stone.ogg");
}

/// <summary>
/// Raised on a melee user when the normal light-attack target validation rejects a
/// non-Damageable target. Systems may allow that specific target.
/// </summary>
[ByRefEvent]
public record struct MeleeNonDamageableTargetAttemptEvent(EntityUid Target, bool Allowed = false);
