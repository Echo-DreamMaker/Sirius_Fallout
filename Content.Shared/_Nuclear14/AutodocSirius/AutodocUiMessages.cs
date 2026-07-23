using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Nuclear14.AutodocSirius;

[Serializable, NetSerializable]
public sealed partial class AutodocUiEjectOccupantMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed partial class AutodocUiEjectBeakerMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed partial class AutodocUiStartTreatmentMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed partial class AutodocUiButtonPressedMessage : BoundUserInterfaceMessage
{
    public AutodocUiButton Button;

    public AutodocUiButtonPressedMessage(AutodocUiButton button)
    {
        Button = button;
    }
}

[Serializable, NetSerializable]
public sealed partial class AutodocUiToggleOpenMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed partial class AutodocTreatmentDoAfterEvent : SimpleDoAfterEvent
{
}

[Serializable, NetSerializable]
public enum AutodocUiButton
{
    OpenDoor,
    CloseDoor,
    EjectBeaker,
    EjectPatient,
    StartTreatment,
    StartSurgery,
    Close
}
