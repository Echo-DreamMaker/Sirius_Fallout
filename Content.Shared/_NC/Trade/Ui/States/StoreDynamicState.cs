using Content.Shared._Misfits.Trade;
using Robust.Shared.Serialization;

namespace Content.Shared._NC.Trade;

[Serializable, NetSerializable]
public sealed class StoreDynamicState : BoundUserInterfaceState
{
    public StoreDynamicState(
        int revision,
        int catalogRevision,
        Dictionary<string, int> balanceByCurrency,
        Dictionary<string, int> remainingById,
        Dictionary<string, int> ownedById,
        Dictionary<string, int> crateUnitsById,
        Dictionary<string, int> massSellTotals,
        List<ContractClientData> contracts,
        bool hasBuyTab,
        bool hasSellTab,
        bool hasContractsTab,
        // #Misfits Add вЂ” tier progression data
        string contractTierProfile,
        HashSet<string> playerUnlockedTiers,
        List<TierRosterEntry> roster)
    {
        Revision = revision;
        CatalogRevision = catalogRevision;
        BalanceByCurrency = balanceByCurrency;
        RemainingById = remainingById;
        OwnedById = ownedById;
        CrateUnitsById = crateUnitsById;
        MassSellTotals = massSellTotals;
        Contracts = contracts;
        HasBuyTab = hasBuyTab;
        HasSellTab = hasSellTab;
        HasContractsTab = hasContractsTab;
        ContractTierProfile = contractTierProfile;
        PlayerUnlockedTiers = playerUnlockedTiers;
        Roster = roster;
    }

    public int Revision { get; }
    public int CatalogRevision { get; }

    public Dictionary<string, int> BalanceByCurrency { get; }
    public Dictionary<string, int> RemainingById { get; }
    public Dictionary<string, int> OwnedById { get; }
    public Dictionary<string, int> CrateUnitsById { get; }

    public Dictionary<string, int> MassSellTotals { get; }

    public List<ContractClientData> Contracts { get; }

    public bool HasBuyTab { get; }
    public bool HasSellTab { get; }
    public bool HasContractsTab { get; }

    public string ContractTierProfile { get; }

    // #Misfits Add вЂ” which contract tiers this player has unlocked
    public HashSet<string> PlayerUnlockedTiers { get; }

    // #Misfits Add вЂ” Hall of Fame roster entries for this vendor
    public List<TierRosterEntry> Roster { get; }
}
