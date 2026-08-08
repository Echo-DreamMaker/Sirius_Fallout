using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Nuclear14.AutodocSirius;

[RegisterComponent, NetworkedComponent]
public sealed partial class SiriusAutodocComponent : Component
{
    public const string SiriusBeakerSlotId = "siriusBeakerSlot";
    public const int MaxSlots = 20;
    public const string BrainSlotId = "brainSlot";
    public const string EyesSlotId = "eyesSlot";
    public const string HeartSlotId = "heartSlot";
    public const string LiverSlotId = "liverSlot";
    public const string LungsSlotId = "lungsSlot";
    public const string StomachSlotId = "stomachSlot";
    public const string KidneysSlotId = "kidneysSlot";
    public const string AppendixSlotId = "appendixSlot";
    public const string TongueSlotId = "tongueSlot";
    public const string EarsSlotId = "earsSlot";
    public const string LeftArmSlotId = "leftArmSlot";
    public const string RightArmSlotId = "rightArmSlot";
    public const string LeftHandSlotId = "leftHandSlot";
    public const string RightHandSlotId = "rightHandSlot";
    public const string LeftLegSlotId = "leftLegSlot";
    public const string RightLegSlotId = "rightLegSlot";
    public const string LeftFootSlotId = "leftFootSlot";
    public const string RightFootSlotId = "rightFootSlot";
    public const string HeadSlotId = "headSlot";
    public const string TorsoSlotId = "torsoSlot";

    [ViewVariables]
    public ContainerSlot BodyContainer = default!;
    [ViewVariables]
    public readonly string[] PartSlotIds = new string[MaxSlots];
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("entryDelay")]
    public float EntryDelay = 2f;
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("treatmentDuration")]
    public float TreatmentDuration = 10f;
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("isOpen")]
    public bool IsOpen = true;
    [ViewVariables]
    public bool Powered = false;
    [ViewVariables]
    public bool IsTreating = false;
    [ViewVariables]
    public bool IsEjecting = false;
    [ViewVariables]
    public EntityUid? CurrentPatient = null;
    [ViewVariables]
    public EntityUid? SiriusSurgeryComponent = null;
    [Serializable, NetSerializable]
    public enum AutodocVisuals : byte
    {
        IsOn,
        IsOpen
    }
}
