// #Misfits Add - Vertibird co-pilot turret: remote gunnery from one Z-level below the craft.
using Content.Shared.Actions;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Misfits.Vehicles.Vertibird;

/// <summary>
/// Sits on the vertibird. Tracks the co-pilot gunner and the camera entity they
/// occupy while manning the turret. Ammunition lives in a LimitedChargesComponent
/// on the same entity so the restock item can top it up with the shared charges API.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VertibirdTurretComponent : Component
{
    /// <summary>
    /// Seat index that owns the turret. Seat 0 is the pilot, seat 1 is the crew chief.
    /// </summary>
    [DataField]
    public int GunnerSeat = 1;

    [DataField, AutoNetworkedField]
    public EntityUid? Gunner;

    /// <summary>
    /// Camera entity on the map below, spawned while the gunner is manning the turret.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? TurretEye;

    [DataField, AutoNetworkedField]
    public EntityUid? EnterActionEntity;

    [DataField, AutoNetworkedField]
    public EntityUid? ExitActionEntity;

    [DataField, AutoNetworkedField]
    public EntityUid? FireActionEntity;

    [DataField]
    public EntProtoId EnterAction = "ActionVertibirdEnterTurret";

    [DataField]
    public EntProtoId ExitAction = "ActionVertibirdExitTurret";

    [DataField]
    public EntProtoId FireAction = "ActionVertibirdFireTurret";

    /// <summary>
    /// Camera entity spawned on the map one Z-level below the craft.
    /// </summary>
    [DataField]
    public EntProtoId EyeProto = "VertibirdTurretEye";

    // Gun run parameters, handed to VertibirdSupportSystem.ScheduleSupport on fire.
    // Tuned tighter and far faster than the called-in air support flare: this is a
    // door gun the gunner is aiming, not an airstrike being requested.

    /// <summary>
    /// Delay between pulling the trigger and the first round landing.
    /// </summary>
    [DataField]
    public TimeSpan FireDelay = TimeSpan.FromSeconds(1);

    [DataField]
    public int Shots = 15;

    [DataField]
    public TimeSpan ShotInterval = TimeSpan.FromSeconds(0.1);

    [DataField]
    public float Spread = 3f;

    [DataField]
    public float LineLength = 5f;

    [DataField]
    public float Intensity = 5f;

    [DataField]
    public float Slope = 2f;

    [DataField]
    public float MaxIntensity = 5f;

    /// <summary>
    /// Left at zero so the gun run chews mobs without demolishing the map.
    /// </summary>
    [DataField]
    public float TileBreakScale;

    [DataField]
    public SoundSpecifier? FireSound =
        new SoundPathSpecifier("/Audio/_Nuclear14/Effects/a10_warthog_brrrt.ogg");
}

/// <summary>
/// Sits on the spawned camera entity so the turret system can find its way back
/// to the craft and the gunner if anything is cleaned up out of order.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class VertibirdTurretEyeComponent : Component
{
    [DataField]
    public EntityUid? Vertibird;

    [DataField]
    public EntityUid? Gunner;
}

/// <summary>
/// Marks an item as a turret ammunition belt. Charges carried in LimitedChargesComponent;
/// using it on a vertibird transfers only the rounds the craft is actually short.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class VertibirdTurretRestockComponent : Component;

/// <summary>
/// Marks trained vertibird crew (Lancers and pilots). Speeds up vertibird repair.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class VertibirdCrewComponent : Component
{
    /// <summary>
    /// Multiplier applied to the repair do-after. Below 1 is faster.
    /// </summary>
    [DataField]
    public float RepairSpeedMultiplier = 0.5f;
}

public sealed partial class VertibirdEnterTurretActionEvent : InstantActionEvent;

public sealed partial class VertibirdExitTurretActionEvent : InstantActionEvent;

public sealed partial class VertibirdFireTurretActionEvent : WorldTargetActionEvent;
