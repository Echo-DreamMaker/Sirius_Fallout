using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.Trade;

// Raised on the user entity after a contract is successfully claimed.
// Allows ContractTierSystem to check for tier advancement without modifying upstream code.
public sealed class MisfitsContractClaimedEvent : EntityEventArgs
{
    public readonly EntityUid Store;
    public readonly EntityUid User;
    public readonly string ContractId;
    public readonly string Difficulty;

    public MisfitsContractClaimedEvent(EntityUid store, EntityUid user, string contractId, string difficulty)
    {
        Store = store;
        User = user;
        ContractId = contractId;
        Difficulty = difficulty;
    }
}

// Raised on the user entity when they open a tier-enabled vendor for the first time this round.
// Allows ContractTierSystem to award the Road Kill entry badge.
public sealed class MisfitsContractFirstAccessEvent : EntityEventArgs
{
    public readonly EntityUid Store;
    public readonly EntityUid User;

    public MisfitsContractFirstAccessEvent(EntityUid store, EntityUid user)
    {
        Store = store;
        User = user;
    }
}

// Net-serializable snapshot of a player's entry in the vendor roster.
// Sent to clients as part of StoreDynamicState for the Hall of Fame display.
[Serializable, NetSerializable]
public sealed class TierRosterEntry
{
    public string PlayerName = string.Empty;
    public string HighestTier = "Road Kill";
    public int TotalCompleted;

    public TierRosterEntry() { }

    public TierRosterEntry(string name, string highestTier, int total)
    {
        PlayerName = name;
        HighestTier = highestTier;
        TotalCompleted = total;
    }
}
