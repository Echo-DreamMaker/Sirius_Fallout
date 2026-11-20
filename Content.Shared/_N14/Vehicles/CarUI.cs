// #Sirius Add - car console (Vertibird-style) UI.
using System;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._N14.Vehicles;

[Serializable, NetSerializable]
public enum CarUiKey : byte
{
    Key,
}

/// <summary>
/// #Sirius Add - car console state: hull health, engine state, seats and cargo.
/// </summary>
[Serializable, NetSerializable]
public sealed class CarBoundUiState : BoundUserInterfaceState
{
    public readonly string Title;
    public readonly float Integrity;
    public readonly float MaxIntegrity;
    public readonly bool EngineRunning;
    public readonly CarSeatUiState[] Seats;
    public readonly CarCargoUiState[] Cargo;
    public readonly int CargoCapacity;

    public CarBoundUiState(
        string title,
        float integrity,
        float maxIntegrity,
        bool engineRunning,
        CarSeatUiState[] seats,
        CarCargoUiState[] cargo,
        int cargoCapacity)
    {
        Title = title;
        Integrity = integrity;
        MaxIntegrity = maxIntegrity;
        EngineRunning = engineRunning;
        Seats = seats;
        Cargo = cargo;
        CargoCapacity = cargoCapacity;
    }
}

[Serializable, NetSerializable]
public readonly record struct CarSeatUiState(int Index, string Name, string? OccupantName);

[Serializable, NetSerializable]
public readonly record struct CarCargoUiState(NetEntity Crate, string Name);

/// <summary>
/// #Sirius Add - board the requested seat (0 = pilot, 1-4 = passengers).
/// </summary>
[Serializable, NetSerializable]
public sealed class CarSelectSeatMessage : BoundUserInterfaceMessage
{
    public readonly int SeatIndex;

    public CarSelectSeatMessage(int seatIndex)
    {
        SeatIndex = seatIndex;
    }
}

[Serializable, NetSerializable]
public sealed class CarLoadCargoMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class CarUnloadCargoMessage : BoundUserInterfaceMessage
{
    public readonly NetEntity Crate;

    public CarUnloadCargoMessage(NetEntity crate)
    {
        Crate = crate;
    }
}

/// <summary>
/// #Sirius Add - action used by cars to open their own console instead of the
/// generic mech equipment window.
/// </summary>
public sealed partial class CarOpenUiEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed partial class CarPassengerBoardDoAfterEvent : DoAfterEvent
{
    public int SeatIndex;

    public CarPassengerBoardDoAfterEvent(int seatIndex)
    {
        SeatIndex = seatIndex;
    }

    public override DoAfterEvent Clone() => new CarPassengerBoardDoAfterEvent(SeatIndex);
}

/// <summary>
/// #Sirius Add - loading/unloading a crate. The crate rides in the event because a
/// crate inside the cargo bay has no broadphase for DoAfterArgs' range check.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class CarCargoDoAfterEvent : DoAfterEvent
{
    public NetEntity Crate;

    public CarCargoDoAfterEvent(NetEntity crate)
    {
        Crate = crate;
    }

    public override DoAfterEvent Clone() => new CarCargoDoAfterEvent(Crate);
}
