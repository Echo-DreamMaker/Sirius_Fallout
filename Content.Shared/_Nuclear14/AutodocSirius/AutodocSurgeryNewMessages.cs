using Robust.Shared.Serialization;

namespace Content.Shared._Nuclear14.AutodocSirius;

[Serializable, NetSerializable]
public sealed class AutodocSurgeryPartSelectedMessage : BoundUserInterfaceMessage
{
    public readonly string PartId;

    public AutodocSurgeryPartSelectedMessage(string partId)
    {
        PartId = partId;
    }
}

[Serializable, NetSerializable]
public sealed class AutodocSurgeryOperationMessage : BoundUserInterfaceMessage
{
    public readonly string PartId;
    public readonly string OperationId;

    public AutodocSurgeryOperationMessage(string partId, string operationId)
    {
        PartId = partId;
        OperationId = operationId;
    }
}

[Serializable, NetSerializable]
public sealed class AutodocSurgeryBackMessage : BoundUserInterfaceMessage
{
}
