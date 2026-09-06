// Origin: ColonialMarinesUniverse (AU-14) — Multi Z system
//   PR #1058 "Multi Z addition" & #1119 "Multi z fixes" by TheHellFireo
//   Based on Crystall Edge (crystallpunk-14) Multi-Z system
// Ported to misfits-14 _MultiZ/ — renamed &amp; adapted
// #Cythisiax Ported — Multi-Z level support for misfits-14

using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._MultiZ.Ghost;

/// <summary>
/// Allows ghosts to quickly move between Z-levels via action buttons.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MZGhostMoverComponent : Component
{
    [DataField]
    public EntProtoId UpActionProto = "ActionMultiZGhostUp";

    [DataField, AutoNetworkedField]
    public EntityUid? ZLevelUpActionEntity;

    [DataField]
    public EntProtoId DownActionProto = "ActionMultiZGhostDown";

    [DataField, AutoNetworkedField]
    public EntityUid? ZLevelDownActionEntity;
}

/// <summary>
/// Raised when ghost uses the "move up a Z-level" action.
/// </summary>
public sealed partial class MZGhostActionUp : InstantActionEvent;

/// <summary>
/// Raised when ghost uses the "move down a Z-level" action.
/// </summary>
public sealed partial class MZGhostActionDown : InstantActionEvent;
