using Content.Shared.Chemistry.Components;
using Content.Shared.FixedPoint;
using Content.Shared.MedicalScanner;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Misfits.Medical.Cryogenics;

[RegisterComponent, NetworkedComponent]
public sealed partial class MisfitsCryoPodComponent : Component
{
    [DataField]
    public string SolutionName = "cryoChamber";

    [ViewVariables]
    public Entity<SolutionComponent>? ChamberSolution;

    [DataField]
    public string BeakerSlotId = "beaker";

    [DataField]
    public string CoolantReagent = "Cryzine";

    [DataField]
    public float TransferTime = 1f;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan? NextUpdateTime;

    [DataField]
    public FixedPoint2 TransferAmount = 2;

    [DataField]
    public FixedPoint2 MinTransferAmount = 0.1;

    [DataField]
    public FixedPoint2 MaxTransferAmount = 20;

    [DataField]
    public float TargetTemperature = 90f;

    [DataField]
    public float BaseCoolingFraction = 0.25f;

    [DataField]
    public float CoolingBoostPerUnit = 0.02f;

    [DataField]
    public float MaxCoolingFraction = 0.9f;
}

[Serializable, NetSerializable]
public enum MisfitsCryoPodUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class MisfitsCryoReagentReadout
{
    public readonly string ReagentId;
    public readonly string Name;
    public readonly Color Color;
    public readonly FixedPoint2 Quantity;

    public MisfitsCryoReagentReadout(string reagentId, string name, Color color, FixedPoint2 quantity)
    {
        ReagentId = reagentId;
        Name = name;
        Color = color;
        Quantity = quantity;
    }
}

[Serializable, NetSerializable]
public sealed class MisfitsCryoPodBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly HealthAnalyzerScannedUserMessage HealthScan;

    public readonly float PatientTemperature;
    public readonly float TargetTemperature;
    public readonly MisfitsCryoReagentReadout[] PatientReagents;

    public readonly FixedPoint2 ChamberMaxVolume;
    public readonly MisfitsCryoReagentReadout[] ChamberReagents;

    public readonly bool HasBeaker;
    public readonly string BeakerName;
    public readonly FixedPoint2 BeakerMaxVolume;
    public readonly MisfitsCryoReagentReadout[] BeakerReagents;

    public readonly FixedPoint2 TransferAmount;
    public readonly FixedPoint2 MinTransferAmount;
    public readonly FixedPoint2 MaxTransferAmount;

    public MisfitsCryoPodBoundUserInterfaceState(
        HealthAnalyzerScannedUserMessage healthScan,
        float patientTemperature,
        float targetTemperature,
        MisfitsCryoReagentReadout[] patientReagents,
        FixedPoint2 chamberMaxVolume,
        MisfitsCryoReagentReadout[] chamberReagents,
        bool hasBeaker,
        string beakerName,
        FixedPoint2 beakerMaxVolume,
        MisfitsCryoReagentReadout[] beakerReagents,
        FixedPoint2 transferAmount,
        FixedPoint2 minTransferAmount,
        FixedPoint2 maxTransferAmount)
    {
        HealthScan = healthScan;
        PatientTemperature = patientTemperature;
        TargetTemperature = targetTemperature;
        PatientReagents = patientReagents;
        ChamberMaxVolume = chamberMaxVolume;
        ChamberReagents = chamberReagents;
        HasBeaker = hasBeaker;
        BeakerName = beakerName;
        BeakerMaxVolume = beakerMaxVolume;
        BeakerReagents = beakerReagents;
        TransferAmount = transferAmount;
        MinTransferAmount = minTransferAmount;
        MaxTransferAmount = maxTransferAmount;
    }
}

[Serializable, NetSerializable]
public sealed class MisfitsCryoPodDepositReagentMessage : BoundUserInterfaceMessage
{
    public readonly string ReagentId;
    public readonly FixedPoint2 Amount;

    public MisfitsCryoPodDepositReagentMessage(string reagentId, FixedPoint2 amount)
    {
        ReagentId = reagentId;
        Amount = amount;
    }
}

[Serializable, NetSerializable]
public sealed class MisfitsCryoPodSetTransferAmountMessage : BoundUserInterfaceMessage
{
    public readonly FixedPoint2 Amount;

    public MisfitsCryoPodSetTransferAmountMessage(FixedPoint2 amount)
    {
        Amount = amount;
    }
}
