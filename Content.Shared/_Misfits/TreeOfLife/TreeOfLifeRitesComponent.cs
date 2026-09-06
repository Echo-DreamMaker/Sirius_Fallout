using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.TreeOfLife;

[Serializable, NetSerializable]
public enum TreeOfLifeRite : byte
{
    None,
    Returning,
    Hearth,
    GreenHand,
}

[Serializable, NetSerializable]
public enum TreeOfLifeRitesUiKey : byte
{
    Key,
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TreeOfLifeRitesComponent : Component
{
    [DataField, AutoNetworkedField]
    public TreeOfLifeRite ActiveRite;

    [DataField]
    public TimeSpan ChangeCooldown = TimeSpan.FromMinutes(20);

    [DataField, AutoNetworkedField]
    public TimeSpan? NextChangeAt;

    [DataField]
    public HashSet<string> ShamanJobs = new() { "TribalShaman" };
}
