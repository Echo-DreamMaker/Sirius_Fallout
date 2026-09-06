// #Misfits Add - Flyable vertibird POC state and pilot action wiring.
using System.Numerics;
using System;
using Content.Shared.Actions;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.Vehicles.Vertibird;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class VertibirdComponent : Component
{
    [DataField, AutoNetworkedField]
    public VertibirdFlightState State = VertibirdFlightState.Grounded;

    [DataField, AutoNetworkedField]
    public EntityUid? Pilot;

    /// <summary>
    /// Number of seats this vehicle offers. The seat array is resized to this
    /// value on component init so each vehicle can define its own capacity.
    /// </summary>
    [DataField]
    public int SeatCount = 10;

    [ViewVariables]
    public EntityUid?[] SeatOccupants = [];

    /// <summary>
    /// How many crates the cargo bay holds. Crates are dragged aboard and travel
    /// with everything still packed inside them.
    /// </summary>
    [DataField]
    public int CargoCapacity = 2;

    [DataField, AutoNetworkedField]
    public EntityUid? FlightActionEntity;

    [DataField, AutoNetworkedField]
    public EntityUid? LandActionEntity;

    [DataField, AutoNetworkedField]
    public EntityUid? MoveUpActionEntity;

    [DataField, AutoNetworkedField]
    public EntityUid? MoveDownActionEntity;

    [DataField]
    public EntProtoId FlightAction = "ActionVertibirdTakeOff";

    [DataField]
    public EntProtoId LandAction = "ActionVertibirdLand";

    [DataField]
    public EntProtoId MoveUpAction = "ActionVertibirdMoveUp";

    [DataField]
    public EntProtoId MoveDownAction = "ActionVertibirdMoveDown";

    [DataField]
    public float StartupDuration = 55f;

    public TimeSpan StartupStartedAt = TimeSpan.Zero;

    public TimeSpan StartupFinishedAt = TimeSpan.Zero;

    public int StartupEmoteIndex;

    [DataField]
    public SoundSpecifier? StartupSound = new SoundPathSpecifier("/Audio/_Misfits/N14/Vehicles/vertibird_start.ogg",
        AudioParams.Default.WithVolume(-1f).WithMaxDistance(18f));

    [DataField]
    public SoundSpecifier? FlightLoopSound = new SoundPathSpecifier("/Audio/_Misfits/N14/Vehicles/vertibird_loop.ogg",
        AudioParams.Default.WithLoop(true).WithVolume(-2f).WithMaxDistance(18f));

    [DataField]
    public SoundSpecifier? LandingSound = new SoundPathSpecifier("/Audio/_Misfits/N14/Vehicles/vertibird_stop.ogg",
        AudioParams.Default.WithVolume(-1f).WithMaxDistance(18f));

    public EntityUid? StartupSoundStream;

    public EntityUid? FlightSoundStream;

    [DataField]
    public float HoverAltitude = 0.85f;

    [DataField]
    public float VerticalSpeed = 0.75f;

    [DataField]
    public float ThrustAcceleration = 6f;

    [DataField]
    public float ReverseAcceleration = 2f;

    [DataField]
    public float MaxFlightSpeed = 12f;

    [DataField]
    public float FlightDrag = 0.75f;

    [DataField]
    public float TurnSpeedDegrees = 90f;

    [DataField]
    public float AltitudeTransitionDuration = 1.25f;

    [DataField]
    public string FuelSolution = "vertibirdFuel";

    [DataField]
    public ProtoId<ReagentPrototype> FuelReagent = "WeldingFuel";

    [DataField]
    public FixedPoint2 FuelUsePerSecond = FixedPoint2.New(0.5f);

    [DataField]
    public FixedPoint2 MinimumTakeoffFuel = FixedPoint2.New(30f);

    [DataField]
    public float LowFuelWarningFraction = 0.25f;

    [DataField]
    public float CriticalFuelWarningFraction = 0.10f;

    [ViewVariables]
    public bool LowFuelWarningIssued;

    [ViewVariables]
    public bool CriticalFuelWarningIssued;

    [ViewVariables]
    public float FuelAccumulator;

    [ViewVariables]
    public TimeSpan NextFuelUiUpdate;

    [ViewVariables]
    public bool FuelEmergencyActive;

    [ViewVariables]
    public bool EmergencyLandingActive;

    [DataField]
    public Vector2 DriftVelocity = Vector2.Zero;

    [ViewVariables]
    public VertibirdControlInput HeldInputs;

    [ViewVariables]
    public TimeSpan AltitudeTransitionFinishedAt = TimeSpan.Zero;

    [ViewVariables]
    public EntityUid? AltitudeTargetMap;

    [ViewVariables]
    public int AltitudeOffset;

    [DataField]
    public string MapConfigId = "Wendover";

    /// <summary>
    /// Blunt damage taken per Z-level when someone steps out of an airborne craft.
    /// One level up is a survivable but serious landing; higher is progressively worse.
    /// </summary>
    [DataField]
    public float FallDamagePerLevel = 100f;

    // ---- Admin debug toggles (Tricks verb menu). Off on every normal craft. ----

    /// <summary>Fuel never drains and the tank never blocks takeoff.</summary>
    [DataField]
    public bool DebugInfiniteFuel;

    /// <summary>Startup, takeoff, landing and Z-changes complete on the next tick.</summary>
    [DataField]
    public bool DebugInstantFlight;

    /// <summary>The turret fires without spending or needing charges.</summary>
    [DataField]
    public bool DebugInfiniteAmmo;

    /// <summary>
    /// Keeps the craft airborne when it loses its pilot, instead of descending
    /// automatically. Lets one tester ghost into a gunner or a target mob without
    /// the vertibird landing itself the moment they leave the pilot's body.
    /// </summary>
    [DataField]
    public bool DebugIgnorePilotLoss;

    // ---- Sprite / visual state (per-vehicle, so balloons/vertibirds use their own RSI states) ----
    /// <summary>RSI state shown while grounded.</summary>
    [DataField]
    public string GroundedSpriteState = "vertibird";

    /// <summary>RSI state shown while airborne.</summary>
    [DataField]
    public string FlyingSpriteState = "vertibird_flying";

    /// <summary>
    /// Effect entity spawned on a timer while airborne (rotor wash, burner embers,
    /// etc). Null/empty means no per-frame effect.
    /// </summary>
    [DataField]
    public string? FlightEffectPrototype = "VertibirdRotorWashEffect";

    /// <summary>Local offsets (rotated with the vehicle) where the flight effect spawns.</summary>
    [DataField]
    public Vector2[] FlightEffectOffsets = [new(-1.6f, 0.8f), new(1.6f, 0.8f)];

    [DataField]
    public float FlightEffectInterval = 0.2f;

    // ---- RP emote locale ids (per-vehicle flavor) ----
    [DataField]
    public string StartupEmote = "vertibird-rp-startup";

    [DataField]
    public string[] StartupProgressEmotes =
    [
        "vertibird-rp-startup-switches",
        "vertibird-rp-startup-avionics",
        "vertibird-rp-startup-rotors",
        "vertibird-rp-startup-throttle",
    ];

    [DataField]
    public string TakeoffEmote = "vertibird-rp-takeoff";

    [DataField]
    public string LandingEmote = "vertibird-rp-landing";

    [DataField]
    public string ZUpEmote = "vertibird-rp-z-up";

    [DataField]
    public string ZDownEmote = "vertibird-rp-z-down";

    [DataField]
    public string FuelEmergencyEmote = "vertibird-rp-fuel-emergency";

    [DataField]
    public string PilotDisconnectedEmote = "vertibird-rp-pilot-disconnected";

    // ---- GUI ----
    /// <summary>Locale key for the seat-manifest window title.</summary>
    [DataField]
    public string WindowTitleLocId = "vertibird-window-title";
}

[Flags]
public enum VertibirdControlInput : byte
{
    None = 0,
    Forward = 1 << 0,
    Back = 1 << 1,
    Left = 1 << 2,
    Right = 1 << 3,
}

[RegisterComponent, NetworkedComponent]
public sealed partial class VertibirdHiddenOccupantComponent : Component
{
    [DataField]
    public bool HadStealth;

    [DataField]
    public float PreviousVisibility = 1f;
}

[Serializable, NetSerializable]
public enum VertibirdUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class VertibirdSeatBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly string Title;
    public readonly VertibirdFlightState FlightState;
    public readonly int Altitude;
    public readonly float Fuel;
    public readonly float MaxFuel;
    public readonly float StructuralIntegrity;
    public readonly float MaxStructuralIntegrity;
    public readonly VertibirdSeatUiState[] Seats;
    public readonly VertibirdCargoUiState[] Cargo;
    public readonly int CargoCapacity;

    public VertibirdSeatBoundUserInterfaceState(
        string title,
        VertibirdFlightState flightState,
        int altitude,
        float fuel,
        float maxFuel,
        float structuralIntegrity,
        float maxStructuralIntegrity,
        VertibirdSeatUiState[] seats,
        VertibirdCargoUiState[] cargo,
        int cargoCapacity)
    {
        Cargo = cargo;
        CargoCapacity = cargoCapacity;
        Title = title;
        FlightState = flightState;
        Altitude = altitude;
        Fuel = fuel;
        MaxFuel = maxFuel;
        StructuralIntegrity = structuralIntegrity;
        MaxStructuralIntegrity = maxStructuralIntegrity;
        Seats = seats;
    }
}

[Serializable, NetSerializable]
public readonly record struct VertibirdSeatUiState(int Index, string Name, string? OccupantName, bool RequiresPilotPerk);

[Serializable, NetSerializable]
public readonly record struct VertibirdCargoUiState(NetEntity Crate, string Name);

/// <summary>
/// Loads whatever crate the actor is pulling. The console cannot reach out and pick a
/// crate for them, so the one they hauled over is the one that goes in.
/// </summary>
[Serializable, NetSerializable]
public sealed class VertibirdLoadCargoMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class VertibirdUnloadCargoMessage : BoundUserInterfaceMessage
{
    public readonly NetEntity Crate;

    public VertibirdUnloadCargoMessage(NetEntity crate)
    {
        Crate = crate;
    }
}

[Serializable, NetSerializable]
public sealed class VertibirdSelectSeatMessage : BoundUserInterfaceMessage
{
    public readonly int SeatIndex;

    public VertibirdSelectSeatMessage(int seatIndex)
    {
        SeatIndex = seatIndex;
    }
}

[Serializable, NetSerializable]
public sealed class VertibirdControlInputMessage : EntityEventArgs
{
    public VertibirdControlInput Input;
    public bool Pressed;

    public VertibirdControlInputMessage(VertibirdControlInput input, bool pressed)
    {
        Input = input;
        Pressed = pressed;
    }
}

[Serializable, NetSerializable]
public sealed partial class VertibirdBoardDoAfterEvent : DoAfterEvent
{
    public int SeatIndex;

    public VertibirdBoardDoAfterEvent(int seatIndex)
    {
        SeatIndex = seatIndex;
    }

    public override DoAfterEvent Clone() => new VertibirdBoardDoAfterEvent(SeatIndex);
}

/// <summary>
/// Loading a crate into the cargo bay, or hauling one back out. Which one it is
/// depends on whether the crate is already in the bay when the do-after lands.
/// The crate rides in the event rather than in DoAfterArgs.Used, because once it is
/// in the bay it has no broadphase for the args' range check to work against.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class VertibirdCargoDoAfterEvent : DoAfterEvent
{
    public NetEntity Crate;

    public VertibirdCargoDoAfterEvent(NetEntity crate)
    {
        Crate = crate;
    }

    public override DoAfterEvent Clone() => new VertibirdCargoDoAfterEvent(Crate);
}

/// <summary>
/// Keeps the server's PVS viewers aligned with the pilot's client-side cursor camera.
/// </summary>
[Serializable, NetSerializable]
public sealed class VertibirdCameraOffsetMessage : EntityEventArgs
{
    public Vector2 Offset;

    public VertibirdCameraOffsetMessage(Vector2 offset)
    {
        Offset = offset;
    }
}

public enum VertibirdFlightState : byte
{
    Grounded,
    Starting,
    TakingOff,
    Cruising,
    ChangingAltitude,
    Landing,
}

public enum VertibirdVisualLayers : byte
{
    Shadow,
    Base,
}

public sealed partial class VertibirdFlightActionEvent : InstantActionEvent;

public sealed partial class VertibirdLandActionEvent : InstantActionEvent;

public sealed partial class VertibirdMoveUpActionEvent : InstantActionEvent;

public sealed partial class VertibirdMoveDownActionEvent : InstantActionEvent;
