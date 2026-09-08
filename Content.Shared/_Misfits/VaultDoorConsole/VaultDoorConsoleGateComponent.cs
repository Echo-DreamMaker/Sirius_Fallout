using Robust.Shared.GameStates;

namespace Content.Shared._Misfits.VaultDoorConsole;


[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VaultDoorConsoleGateComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool RaidActive;

    [DataField, AutoNetworkedField]
    public bool BypassRaidRequirement;

    // #Misfits Add - Faction flavour for the "no raid on record" refusal. Prototype data, so it is
    // the same on both sides without networking; the default is the Vault-Tec wording.
    [DataField]
    public string LockoutMessage = "SECURITY LOCKOUT: no active operation against this vault is on record.";
}
