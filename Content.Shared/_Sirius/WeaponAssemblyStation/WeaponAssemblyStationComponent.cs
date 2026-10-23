// #Misfits Add - Weapon assembly and repair station
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Sirius.WeaponAssemblyStation;

[RegisterComponent, NetworkedComponent]
public sealed partial class WeaponAssemblyStationComponent : Component
{
    public const string MainSlotId = "weapon_main";
    public const string Part1SlotId = "part_1"; // barrel
    public const string Part2SlotId = "part_2"; // bolt
    public const string Part3SlotId = "part_3"; // grip
    public const string Part4SlotId = "part_4"; // stock
    public const string Part6SlotId = "part_6"; // magazine

    [ViewVariables]
    public ContainerSlot MainContainer = default!;
    [ViewVariables]
    public ContainerSlot Part1Container = default!;
    [ViewVariables]
    public ContainerSlot Part2Container = default!;
    [ViewVariables]
    public ContainerSlot Part3Container = default!;
    [ViewVariables]
    public ContainerSlot Part4Container = default!;
    [ViewVariables]
    public ContainerSlot Part6Container = default!;

    [DataField("disassembleDelay")]
    public float DisassembleDelay = 4f;

    /// <summary>Internal solution the station burns through while it works.</summary>
    [DataField("fuelSolution")]
    public string FuelSolutionName = "fuel";

    [DataField("fuelReagent")]
    public string FuelReagent = "WeldingFuel";

    [DataField("assembleDelay")]
    public float AssembleDelay = 8f;

    [DataField("repairStepDelay")]
    public float RepairStepDelay = 1f;

    /// <summary>
    /// Durability restored per repair step. A part that still needs more is
    /// repaired step by step until it is whole again.
    /// </summary>
    [DataField("repairStepRestore")]
    public float RepairStepRestore = 10f;

    [DataField("assembleFuelCost")]
    public int AssembleFuelCost = 25;

    [DataField("repairFuelCostPerStep")]
    public int RepairFuelCostPerStep = 5;

    [DataField("idlePowerUsage")]
    public float IdlePowerUsage = 250f;

    [DataField("activePowerUsage")]
    public float ActivePowerUsage = 2500f;

    // #Misfits Add - Intelligence gate (mirrors the workbench lathe) plus job exemptions.
    [DataField("intelligenceThreshold")]
    public int IntelligenceThreshold = 3;

    [DataField("exemptJobs")]
    public List<string> ExemptJobs = new()
    {
        "TownMechanic",
        "NCREngineer",
        "BoSScribe",
        "BoSMidScribe",
        "BoSWestScribe",
    };

    [ViewVariables]
    public bool Powered = false;

    [ViewVariables]
    public bool IsBolted = false;

    [ViewVariables]
    public bool IsBusy = false;

    [Serializable, NetSerializable]
    public enum WeaponAssemblyVisuals : byte
    {
        IsOn,
        IsOpen,
    }
}