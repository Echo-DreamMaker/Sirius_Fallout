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
        Dictionary<string, string>? availableParts = null)
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
