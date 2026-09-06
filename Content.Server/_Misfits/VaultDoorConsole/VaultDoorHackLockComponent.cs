namespace Content.Server._Misfits.VaultDoorConsole;

[RegisterComponent, Access(typeof(VaultDoorConsoleSystem))]
public sealed partial class VaultDoorHackLockComponent : Component
{
    public TimeSpan LockedUntil;

    // #Misfits Add - Copied off the console that bolted this door. OnVaultButtonActivate starts from
    // a button and only ever finds the door, never the console, so the noun has to ride along here.
    public string DoorNoun = "vault door";
}
