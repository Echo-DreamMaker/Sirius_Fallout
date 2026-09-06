using Content.Shared._Misfits.VaultDoorConsole;

namespace Content.Server._Misfits.VaultDoorConsole;

public enum VaultDoorConsoleDudEffect : byte
{
    ResetAttempts,
    RemoveDud,
}

[RegisterComponent, Access(typeof(VaultDoorConsoleSystem))]
public sealed partial class VaultDoorConsoleComponent : Component
{
    [DataField]
    public string SignalPort = "Pressed";

    [DataField]
    public int WordLength = 8;

    [DataField]
    public int PoolSize = 10;

    [DataField]
    public int DudCount = 3;

    [DataField]
    public int NoiseRowCount = 6;

    [DataField]
    public int MaxAttempts = 4;

    [DataField]
    public TimeSpan LockoutDuration = TimeSpan.FromMinutes(5);

    [DataField]
    public TimeSpan SuccessLockDuration = TimeSpan.FromMinutes(20);

    [DataField]
    public string RaidFaction = "Vault";

    // #Misfits Add - Faction flavour. Lets a second faction's door reuse this system instead of
    // forking it: only the wording changes, the minigame is identical. Defaults are the Vault-Tec
    // text this console already showed, so the existing vault door is unaffected.
    [DataField]
    public string TerminalTitle = "Vault-Tec Security Terminal";

    /// <summary>
    /// What this console calls the door it opens, e.g. "vault door". Used in the success popup and
    /// copied onto <see cref="VaultDoorHackLockComponent"/> so the inside button can name it too.
    /// </summary>
    [DataField]
    public string DoorNoun = "vault door";

    [ViewVariables]
    public List<string> WordPool = new();

    [ViewVariables]
    public string TargetWord = string.Empty;

    [ViewVariables]
    public HashSet<string> RemovedWords = new();

    [ViewVariables]
    public Dictionary<string, VaultDoorConsoleDudEffect> Duds = new();

    [ViewVariables]
    public HashSet<string> ConsumedDuds = new();

    [ViewVariables]
    public List<List<VaultDoorConsoleSegment>> ColumnA = new();

    [ViewVariables]
    public List<List<VaultDoorConsoleSegment>> ColumnB = new();

    [ViewVariables]
    public int AttemptsRemaining;

    [ViewVariables]
    public List<string> Log = new();

    [ViewVariables]
    public bool Solved;

    [ViewVariables]
    public TimeSpan? SolvedUntil;

    [ViewVariables]
    public HashSet<EntityUid> BoltedDoors = new();

    [ViewVariables]
    public TimeSpan? LockedOutUntil;
}
