using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.Deathclaw;

public sealed partial class BwonsamdiRitualClaimActionEvent : EntityTargetActionEvent;
public sealed partial class BwonsamdiMercyActionEvent : EntityTargetActionEvent;

[Serializable, NetSerializable]
public sealed partial class BwonsamdiRitualClaimDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class BwonsamdiMercyDoAfterEvent : SimpleDoAfterEvent;
