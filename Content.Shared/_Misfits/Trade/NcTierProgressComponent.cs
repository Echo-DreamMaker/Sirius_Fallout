namespace Content.Shared._Misfits.Trade;

// Tracks per-player contract tier unlock progress for the current round.
// Attached to a player entity on first interaction with any tier-enabled trade vendor.
// #Misfits Fix - Removed [NetworkedComponent]: no [AutoGenerateComponentState] was present,
// causing NullReferenceException in NetSerializer. Tier data is sent via StoreDynamicState instead.
[RegisterComponent]
public sealed partial class NcTierProgressComponent : Component
{
    // Ordered list of all six tiers from lowest to highest.
    public static readonly string[] AllTiers =
        { "Road Kill", "Lazy Lizard", "Junktown Rat", "Hub Mercenary", "Bunker Buster", "Wasteland Legend" };

    // Number of contracts a player must complete in a tier before the next tier unlocks.
    public const int ContractsToAdvance = 3;

    // Tiers this player currently has access to (starting empty; Road Kill is granted on first vendor access).
    [ViewVariables]
    public HashSet<string> UnlockedTiers { get; } = new();

    // How many contracts have been completed per tier this round.
    [ViewVariables]
    public Dictionary<string, int> CompletedByTier { get; } = new();
}
