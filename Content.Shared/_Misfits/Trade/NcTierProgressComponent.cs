namespace Content.Shared._Misfits.Trade;

// Tracks per-player contract tier unlock progress for the current round.
// Attached to a player entity on first interaction with any tier-enabled trade vendor.
// #Misfits Fix - Removed [NetworkedComponent]: no [AutoGenerateComponentState] was present,
// causing NullReferenceException in NetSerializer. Tier data is sent via StoreDynamicState instead.
[RegisterComponent]
public sealed partial class NcTierProgressComponent : Component
{
    public const string BaseProfile = "Base";
    public const string NcrProfile = "NCR";
    public const string LegionProfile = "Legion";

    private static readonly IReadOnlyDictionary<string, string[]> TierProfiles =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [BaseProfile] = new[] { "Road Kill", "Lazy Lizard", "Junktown Rat", "Hub Mercenary", "Bunker Buster", "Wasteland Legend" },
            [NcrProfile] = new[] { "Tribal", "Settler", "Citizen", "Caravaneer", "Caravan Master", "Brahmin Baron" },
            [LegionProfile] = new[] { "Servus", "Plebeian", "Auxiliary", "Legionary", "Decanus", "Centurion" },
        };

    public static IReadOnlyList<string> GetTiers(string profile) =>
        TierProfiles.TryGetValue(profile, out var tiers) ? tiers : TierProfiles[BaseProfile];

    public static string GetEntryTier(string profile) => GetTiers(profile)[0];

    // Number of contracts a player must complete in a tier before the next tier unlocks.
    public const int ContractsToAdvance = 3;

    // Progress is kept separately for each trader profile.
    [ViewVariables]
    public Dictionary<string, HashSet<string>> UnlockedTiersByProfile { get; } = new();

    // How many contracts have been completed per profile and tier this round.
    [ViewVariables]
    public Dictionary<string, Dictionary<string, int>> CompletedByTierByProfile { get; } = new();

    public HashSet<string> GetUnlockedTiers(string profile) =>
        UnlockedTiersByProfile.TryGetValue(profile, out var tiers)
            ? tiers
            : UnlockedTiersByProfile[profile] = new();

    public Dictionary<string, int> GetCompletedByTier(string profile) =>
        CompletedByTierByProfile.TryGetValue(profile, out var completed)
            ? completed
            : CompletedByTierByProfile[profile] = new();
}
