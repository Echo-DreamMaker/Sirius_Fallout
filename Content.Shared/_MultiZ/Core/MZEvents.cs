// Origin: ColonialMarinesUniverse (AU-14) — Multi Z system
//   PR #1058 "Multi Z addition" & #1119 "Multi z fixes" by TheHellFireo
//   Based on Crystall Edge (crystallpunk-14) Multi-Z system
//   Performance refactors from TTMC (ttmc14)
// Ported to misfits-14 _MultiZ/ — renamed &amp; adapted
// #Cythisiax Ported — Multi-Z level support for misfits-14

using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._MultiZ.Core;

/// <summary>
/// Do-after event raised when a player climbs a Z-level ladder.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class MZLadderDoAfterEvent : DoAfterEvent
{
    public override DoAfterEvent Clone() => this;
}

/// <summary>
/// Raised on an entity when it moves between Z-levels.
/// </summary>
/// <param name="Offset">
/// How many levels were crossed. Negative = downward, positive = upward.
/// </param>
public sealed class MZLevelMoveEvent(int offset) : EntityEventArgs
{
    public int Offset = offset;
}

/// <summary>
/// Triggered when an entity falls to a lower Z-level under gravity.
/// </summary>
public sealed class MZLevelFallEvent : EntityEventArgs;

/// <summary>
/// Called on an entity when it hits the floor or ceiling with force.
/// </summary>
/// <param name="impactPower">Speed at moment of impact. Always positive.</param>
public sealed class MZLevelHitEvent(float impactPower) : EntityEventArgs
{
    public float ImpactPower = impactPower;
}

/// <summary>
/// Action event for toggling look-up mode on MZViewerComponent.
/// </summary>
public sealed partial class MZToggleLookUpAction : InstantActionEvent;

/// <summary>
/// Raised when look-up mode is enabled.
/// </summary>
public record struct MZLookUpEnabledEvent;
