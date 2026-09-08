using Content.Shared._Misfits.SmokeSignal;

namespace Content.Server._Misfits.TreeOfLife;

/// <summary>
///     Tracks non-Tribe players who cross into the sacred root zone.
/// </summary>
[RegisterComponent]
public sealed partial class TreeOfLifeWatchComponent : Component
{
    [DataField]
    public float Range = 4f;

    [DataField]
    public TimeSpan EntryCooldown = TimeSpan.FromSeconds(60);

    [DataField]
    public string TargetDepartment = "Tribe";

    [ViewVariables]
    public HashSet<EntityUid> IntrudersInRange = new();

    [ViewVariables]
    public Dictionary<EntityUid, TimeSpan> NextAlert = new();
}
