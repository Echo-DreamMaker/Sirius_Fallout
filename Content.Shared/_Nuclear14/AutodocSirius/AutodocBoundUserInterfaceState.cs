using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;

namespace Content.Shared._Nuclear14.AutodocSirius;

[Serializable, NetSerializable]
public sealed class AutodocBoundUserInterfaceState : BoundUserInterfaceState
{
    public bool IsOpen;
    public bool Powered;
    public bool HasOccupant;
    public bool IsTreating;
    public OccupantStatus OccStatus;
    public Dictionary<string, FixedPoint2> OccupantDamage;
    public string? OccupantName;
    public bool HasBeaker;
    public FixedPoint2 BeakerCurrentVolume;
    public FixedPoint2 BeakerMaxVolume;
    public FixedPoint2 BeakerStimulantsAmount;
    public bool TreatButtonEnabled;
    public float TreatmentProgress;
    public bool CanSurgery;
    public Dictionary<string, string> AvailableParts;
    public bool SurgeryMode;
    public List<AutodocBodyPartData> BodyParts;
    public string? SelectedPartId;
    public List<AutodocOperationData> AvailableOperations;
    public bool IsOperating;
    public float OperationProgress;
    public string? CurrentOperationName;

    public AutodocBoundUserInterfaceState(
        bool isOpen,
        bool powered,
        bool hasOccupant,
        bool isTreating,
        OccupantStatus occStatus,
        Dictionary<string, FixedPoint2> occupantDamage,
        string? occupantName,
        bool hasBeaker,
        FixedPoint2 beakerCurrentVolume,
        FixedPoint2 beakerMaxVolume,
        FixedPoint2 beakerStimulantsAmount,
        bool treatButtonEnabled,
        float treatmentProgress = 0f,
        bool canSurgery = false,
        Dictionary<string, string>? availableParts = null,
        bool surgeryMode = false,
        List<AutodocBodyPartData>? bodyParts = null,
        string? selectedPartId = null,
        List<AutodocOperationData>? availableOperations = null,
        bool isOperating = false,
        float operationProgress = 0f,
        string? currentOperationName = null)
    {
        IsOpen = isOpen;
        Powered = powered;
        HasOccupant = hasOccupant;
        IsTreating = isTreating;
        OccStatus = occStatus;
        OccupantDamage = occupantDamage;
        OccupantName = occupantName;
        HasBeaker = hasBeaker;
        BeakerCurrentVolume = beakerCurrentVolume;
        BeakerMaxVolume = beakerMaxVolume;
        BeakerStimulantsAmount = beakerStimulantsAmount;
        TreatButtonEnabled = treatButtonEnabled;
        TreatmentProgress = treatmentProgress;
        CanSurgery = canSurgery;
        AvailableParts = availableParts ?? new Dictionary<string, string>();
        SurgeryMode = surgeryMode;
        BodyParts = bodyParts ?? new List<AutodocBodyPartData>();
        SelectedPartId = selectedPartId;
        AvailableOperations = availableOperations ?? new List<AutodocOperationData>();
        IsOperating = isOperating;
        OperationProgress = operationProgress;
        CurrentOperationName = currentOperationName;
    }
}

[Serializable, NetSerializable]
public sealed class AutodocBodyPartData
{
    public string Id;
    public string DisplayName;
    public bool IsPresent;
    public bool HasDamage;

    public AutodocBodyPartData(string id, string displayName, bool isPresent, bool hasDamage)
    {
        Id = id;
        DisplayName = displayName;
        IsPresent = isPresent;
        HasDamage = hasDamage;
    }
}

[Serializable, NetSerializable]
public sealed class AutodocOperationData
{
    public string Id;
    public string DisplayName;
    public bool IsAvailable;
    public string? Tooltip;

    public AutodocOperationData(string id, string displayName, bool isAvailable, string? tooltip = null)
    {
        Id = id;
        DisplayName = displayName;
        IsAvailable = isAvailable;
        Tooltip = tooltip;
    }
}

[Serializable, NetSerializable]
public enum OccupantStatus : byte
{
    None,
    Alive,
    Critical,
    Dead
}
