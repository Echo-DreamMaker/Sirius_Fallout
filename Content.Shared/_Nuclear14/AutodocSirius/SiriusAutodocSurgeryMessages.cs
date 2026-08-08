using Robust.Shared.Serialization;

namespace Content.Shared._Nuclear14.AutodocSirius;

[Serializable, NetSerializable]
public sealed class AutodocSurgeryAllMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class AutodocSurgeryPartMessage : BoundUserInterfaceMessage
{
    public readonly string PartType;

    public AutodocSurgeryPartMessage(string partType)
    {
        PartType = partType;
    }
}
