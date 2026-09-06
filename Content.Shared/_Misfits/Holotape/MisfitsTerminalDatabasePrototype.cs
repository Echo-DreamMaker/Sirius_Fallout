using Robust.Shared.Prototypes;

// #Misfits Add - Faction-shared terminal database prototype.
// Each entry defines one faction's "Database" (e.g. BoS 509th Archive), its
// display name/colour, and the access tag lists used to gate read/write/approve.

namespace Content.Shared._Misfits.Holotape;

/// <summary>
/// Prototype defining a faction-shared terminal database. Multiple terminal entities
/// can reference the same prototype id via TerminalDatabaseComponent.databaseId,
/// allowing every terminal in a faction's bunker/HQ to read/write a shared store.
/// </summary>
[Prototype("misfitsTerminalDatabase")]
public sealed partial class MisfitsTerminalDatabasePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Human-friendly name shown in the Database tab header (e.g. "509th Archive").
    /// </summary>
    [DataField("displayName")]
    public string DisplayName { get; private set; } = "Untitled Database";

    /// <summary>
    /// Hex colour string used as the accent for this database's UI elements.
    /// Defaults to standard terminal green if unset.
    /// </summary>
    [DataField("accentColor")]
    public string AccentColor { get; private set; } = "#33FF33";

    /// <summary>
    /// Access tags that grant read access to this database. Empty = anyone may read.
    /// Checked against the user's ID card via AccessReaderSystem.FindAccessTags.
    /// </summary>
    [DataField("accessRead")]
    public List<string> AccessRead { get; private set; } = new();

    /// <summary>
    /// #Misfits Tweak - Access tags that grant subfolder/document creation + edit access
    /// underneath existing root folders. Tier 6. Cannot create root folders, cannot delete.
    /// Empty = nobody below Leadership can write.
    /// </summary>
    [DataField("accessWrite")]
    public List<string> AccessWrite { get; private set; } = new();

    /// <summary>
    /// #Misfits Add - Tier 7 "Leadership" gate, by JOB PROTOTYPE ID (not access tags) so
    /// admin-spawned ID cards cannot grant it. Leaders may:
    ///  • Create root folders / root documents.
    ///  • Roll back document revisions.
    ///  • #Misfits Add - Move (relocate) any non-Admin-protected folder/subfolder/document
    ///    into another container (tidying, e.g. into a "TRASH" folder).
    /// Leaders cannot delete or restore entries; those actions are reserved for AdminJobs.
    /// Empty list = no one can create root entries (DB becomes read-only effectively).
    /// </summary>
    [DataField("leadershipJobs")]
    public List<string> LeadershipJobs { get; private set; } = new();

    /// <summary>
    /// #Misfits Add - Tier 8/highest-rank gate, by JOB PROTOTYPE ID. Admins are the only
    /// ones who can delete or restore entries and manage Admin-protected content.
    /// Admins also have all Leadership powers. Empty list = no Admin tier; the Admin
    /// checkbox is hidden and no faction job can perform structural deletion.
    /// </summary>
    [DataField("adminJobs")]
    public List<string> AdminJobs { get; private set; } = new();

    /// <summary>
    /// #Misfits Add - Display label for the Admin checkbox in the create-folder editor,
    /// rendered as e.g. "[ ☐  ELDER COUNCIL ]". Faction-themed: "ELDER COUNCIL" (BoS),
    /// "HIGH COMMAND" (NCR/Enclave), "MANAGEMENT" (Vault). Defaults to "ADMIN" if unset.
    /// </summary>
    [DataField("adminLabel")]
    public string AdminLabel { get; private set; } = "ADMIN";

    /// <summary>
    /// #Misfits Add - Entity prototype id of the holotape spawned by the Database
    /// document [ EXPORT ] action. Defaults to the neutral yellow holotape; factions
    /// may override per-database (e.g. NCR red, BoS green) for thematic flavor.
    /// </summary>
    [DataField("exportHolotapePrototype")]
    public string ExportHolotapePrototype { get; private set; } = "N14HolotapeYellow";
}
