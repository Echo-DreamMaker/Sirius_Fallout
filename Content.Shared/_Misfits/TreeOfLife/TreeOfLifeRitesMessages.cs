using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.TreeOfLife;

[Serializable, NetSerializable]
public sealed class TreeOfLifeRitesState : BoundUserInterfaceState
{
    public readonly TreeOfLifeRite ActiveRite;
    public readonly int SecondsUntilChange;
    public readonly bool CanSelect;

    public TreeOfLifeRitesState(TreeOfLifeRite activeRite, int secondsUntilChange, bool canSelect)
    {
        ActiveRite = activeRite;
        SecondsUntilChange = secondsUntilChange;
        CanSelect = canSelect;
    }
}

[Serializable, NetSerializable]
public sealed class TreeOfLifeSelectRiteMessage : BoundUserInterfaceMessage
{
    public readonly TreeOfLifeRite Rite;

    public TreeOfLifeSelectRiteMessage(TreeOfLifeRite rite)
    {
        Rite = rite;
    }
}
