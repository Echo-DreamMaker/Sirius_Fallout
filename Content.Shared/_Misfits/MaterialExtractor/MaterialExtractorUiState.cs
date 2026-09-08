using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.MaterialExtractor;

[Serializable, NetSerializable]
public enum MaterialExtractorUiKey
{
    Key
}

[Serializable, NetSerializable]
public sealed class MaterialExtractorStartMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class MaterialExtractorStopMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class MaterialExtractorEjectFuelMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class MaterialExtractorUiState : BoundUserInterfaceState
{
    public bool Running;
    public float Fuel;
    public float FuelCapacity;
    public bool Unattended;
    public bool FuelDepleted;
}
