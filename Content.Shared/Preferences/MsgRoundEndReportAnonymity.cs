using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Preferences
{
    /// <summary>
    /// The client sends this to toggle end-of-round report anonymity for their characters.
    /// </summary>
    public sealed class MsgRoundEndReportAnonymity : NetMessage
    {
        public override MsgGroups MsgGroup => MsgGroups.Command;

        public bool Anonymous;

        public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
        {
            Anonymous = buffer.ReadBoolean();
        }

        public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
        {
            buffer.Write(Anonymous);
        }
    }
}
