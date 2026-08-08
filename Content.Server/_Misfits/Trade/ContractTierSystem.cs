using Content.Server.Popups;
using Content.Shared._Misfits.Trade;
using Robust.Shared.Prototypes;

namespace Content.Server._Misfits.Trade;

// Manages per-player contract tier progression for tier-enabled trade vendors.
// Awards badge items on first access and on tier advancement.
// Maintains a round-scoped Hall of Fame roster on each participating vendor.
public sealed class ContractTierSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popups = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    // Badge entity prototype IDs awarded when each tier is first unlocked.
    // Defined in Resources/Prototypes/_Misfits/Trade/ContractBadges.yml.
    private static readonly Dictionary<string, string> TierBadgeProtos = new()
    {
        { "Road Kill", "N14ContractBadgeRoadKill" },
        { "Lazy Lizard",   "N14ContractBadgeLazyLizard"   },
        { "Junktown Rat", "N14ContractBadgeJunktownRat"  },
        { "Hub Mercenary",   "N14ContractBadgeHubMercenary"    },
        { "Bunker Buster","N14ContractBadgeBunkerBuster" },
        { "Wasteland Legend","N14ContractBadgeWastelandLegend" },
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MisfitsContractFirstAccessEvent>(OnFirstAccess);
        SubscribeLocalEvent<MisfitsContractClaimedEvent>(OnContractClaimed);
    }

    // Fired when a player first opens a tier-enabled vendor this round.
    // Initialises their NcTierProgressComponent, unlocks Road Kill, and awards the entry badge.
    private void OnFirstAccess(MisfitsContractFirstAccessEvent ev)
    {
        var user = ev.User;
        var store = ev.Store;

        // EnsureComp returns true when the component already existed.
        var alreadyHad = EnsureComp<NcTierProgressComponent>(user, out var prog);

        if (!alreadyHad)
        {
            // Very first access — unlock Road Kill and hand them their entry badge.
            prog.UnlockedTiers.Add("Road Kill");
            SpawnBadge("Road Kill", user);
            _popups.PopupEntity(Loc.GetString("nc-contract-tier-first-access"), user, user);
        }

        RecordRosterVisit(store, user, prog);
    }

    // Fired after a contract is successfully claimed.
    // Increments the tier completion counter and checks whether the next tier unlocks.
    private void OnContractClaimed(MisfitsContractClaimedEvent ev)
    {
        var user = ev.User;
        var store = ev.Store;
        var tier = ev.Difficulty;

        if (!TryComp<NcTierProgressComponent>(user, out var prog))
            return;

        // Increment completion count for this tier.
        prog.CompletedByTier.TryGetValue(tier, out var prev);
        prog.CompletedByTier[tier] = prev + 1;

        TryAdvanceTier(store, user, prog, tier);
        RecordRosterVisit(store, user, prog);
    }

    // Checks whether the player has earned enough completions in currentTier to unlock the next one.
    private void TryAdvanceTier(EntityUid store, EntityUid user, NcTierProgressComponent prog, string currentTier)
    {
        var idx = System.Array.IndexOf(NcTierProgressComponent.AllTiers, currentTier);
        if (idx < 0 || idx >= NcTierProgressComponent.AllTiers.Length - 1)
            return; // Not found or already at Diamond.

        var nextTier = NcTierProgressComponent.AllTiers[idx + 1];
        if (prog.UnlockedTiers.Contains(nextTier))
            return; // Already unlocked.

        prog.CompletedByTier.TryGetValue(currentTier, out var done);
        if (done < NcTierProgressComponent.ContractsToAdvance)
            return;

        // Unlock the next tier and award the corresponding badge.
        prog.UnlockedTiers.Add(nextTier);
        SpawnBadge(nextTier, user);
        _popups.PopupEntity(Loc.GetString("nc-contract-tier-unlocked", ("tier", nextTier)), user, user);
    }

    // Spawns the physical badge item at the player's location.
    private void SpawnBadge(string tier, EntityUid user)
    {
        if (!TierBadgeProtos.TryGetValue(tier, out var protoId))
            return;

        if (!_proto.HasIndex<EntityPrototype>(protoId))
            return;

        Spawn(protoId, Transform(user).Coordinates);
    }

    // Updates (or inserts) this player's entry in the vendor's Hall of Fame roster.
    private void RecordRosterVisit(EntityUid store, EntityUid user, NcTierProgressComponent prog)
    {
        if (!TryComp<NcContractRosterComponent>(store, out var roster))
            return;

        var name = MetaData(user).EntityName;

        // Determine the highest unlocked tier.
        var highestTier = "Road Kill";
        foreach (var tier in NcTierProgressComponent.AllTiers)
        {
            if (prog.UnlockedTiers.Contains(tier))
                highestTier = tier;
        }

        var total = 0;
        foreach (var v in prog.CompletedByTier.Values)
            total += v;

        roster.UpdateEntry(name, highestTier, total);
    }
}
