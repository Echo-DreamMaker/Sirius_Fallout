// #Misfits Add - One end of the bunker tunnel teleporter (surface hatch or tunnel door).
namespace Content.Server._Misfits.Warps;

/// <summary>
/// Marks an entity as one end of the bunker tunnel teleporter.
/// Unlike WarperComponent this stores no destination id on disk. The destination is worked out when
/// a player uses it, so a pair works the moment both halves exist, spawned in any order, with no
/// mapping or admin setup.
/// </summary>
[RegisterComponent]
public sealed partial class BunkerTeleporterComponent : Component
{
    /// <summary>
    /// True for the surface hatch, false for the tunnel door. A teleporter only ever looks for the
    /// opposite kind, so two hatches never lead to each other.
    /// </summary>
    [DataField]
    public bool IsSurface;

    /// <summary>
    /// Which tunnel network this belongs to. Both halves ship with the same default, which is why an
    /// admin-spawned pair links instantly. Mappers can set a different channel to run separate
    /// networks that ignore each other.
    /// </summary>
    [DataField]
    public string Channel = "bunker_tunnel";

    /// <summary>
    /// The exit this hatch rolled the first time it was used, kept so the hatch always comes out in
    /// the same place. Deliberately not a DataField: it is rolled fresh each round, and rolled again
    /// if the exit it picked is deleted.
    /// </summary>
    [ViewVariables]
    public EntityUid? CachedDestination;
}
