using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.Scavenging;

/// <summary>
/// Completes a player's search of a junk pile.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class JunkPileSearchDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public bool FinderSearch;
}
