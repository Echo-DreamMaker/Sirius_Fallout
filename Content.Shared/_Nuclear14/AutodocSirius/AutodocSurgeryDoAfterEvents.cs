using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Nuclear14.AutodocSirius;

[Serializable, NetSerializable]
public sealed partial class AutodocSurgeryOperationDoAfterEvent : SimpleDoAfterEvent
{
    public readonly string PartId;
    public readonly string OperationId;
    public readonly NetEntity AutodocUid;
    public AutodocSurgeryOperationDoAfterEvent(string partId, string operationId, NetEntity autodocUid)
    {
        PartId = partId;
        OperationId = operationId;
        AutodocUid = autodocUid;
    }
}
