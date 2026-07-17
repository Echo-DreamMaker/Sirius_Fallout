using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.Silicon;

// [Changed by MisfitsCrew/Operator] Station AI action and do-after events for remotely hacking camera access.
public sealed partial class StationAiHackCameraActionEvent : EntityTargetActionEvent;

[Serializable, NetSerializable]
public sealed partial class StationAiHackCameraDoAfterEvent : SimpleDoAfterEvent;
