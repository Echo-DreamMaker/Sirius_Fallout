using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.Supporter;

/// <summary>
///     Sent server -> client so the client knows a player's Patreon supporter tier.
///     Used to gate supporter-exclusive features (e.g. the Patreon loadout tab).
/// </summary>
public sealed class MsgSyncSupporterData : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.EntityEvent;

    public NetUserId UserId;
    public SupporterTier Tier;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        UserId = (NetUserId) buffer.ReadGuid();
        Tier = (SupporterTier) buffer.ReadByte();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(UserId);
        buffer.Write((byte) Tier);
    }
}
