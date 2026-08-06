// #Misfits Add — Smoke Signal system: fire-based scoped tribal announcement component.
// Placed on a signal fire entity. All Tribe department players can use it to broadcast
// a short message to all online tribe members.

namespace Content.Shared._Misfits.SmokeSignal;

/// <summary>
/// When placed on an entity, allows tribe members to send a short announcement
/// to all other online Tribe-department players via a smoke signal message.
/// </summary>
[RegisterComponent]
public sealed partial class SmokeSignalComponent : Component
{
    // #Misfits Add - Optional leadership gating and themed Tree presentation.
    [DataField]
    public HashSet<string>? ActivatorJobs;

    [DataField]
    public bool OpenOnActivate = true;

    [DataField]
    public string Verb = "smoke-signal-verb";

    [DataField]
    public string BroadcastMessage = "smoke-signal-broadcast";

    [DataField]
    public string CooldownMessage = "smoke-signal-cooldown";

    /// <summary>
    /// How long the cooldown is between signals.
    /// </summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromMinutes(5);

    /// <summary>
    /// When the cooldown expires (server time). Null = never used.
    /// </summary>
    [DataField]
    public TimeSpan? CooldownEnd;

    /// <summary>
    /// Maximum character length of a signal message.
    /// </summary>
    [DataField]
    public int MaxMessageLength = 128;

    /// <summary>
    /// Department ID whose members can use this and receive the broadcast.
    /// </summary>
    [DataField]
    public string TargetDepartment = "Tribe";

    /// <summary>
    /// HEX color used for the announcement text. Defaults to the Tribe department color.
    /// </summary>
    [DataField]
    public Color AnnouncementColor = Color.FromHex("#d69b3d");

    /// <summary>
    /// Radius (tiles) within which non-tribe bystanders receive an atmospheric smoke notice.
    /// Set to 0 to disable the nearby broadcast.
    /// </summary>
    [DataField]
    public float NearbyRange = 18f;
}
