using Content.Server.Popups;
using Content.Shared._NC.Trade;
using Content.Shared._Misfits.Trade;
using Robust.Shared.Prototypes;

namespace Content.Server._Misfits.Trade;

// Manages per-player contract tier progression for tier-enabled trade vendors.
// Awards badge items on first access and on tier advancement.
// Maintains a round-scoped Hall of Fame roster on each participating vendor.
public sealed partial class ContractTierSystem : EntitySystem
{
    [Dependency] private PopupSystem _popups = default!;
    [Dependency] private IPrototypeManager _proto = default!;

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
        { "Servus", "N14ContractBadgeRoadKill" },
        { "Plebeian",   "N14ContractBadgeLazyLizard"   },
        { "Auxiliary", "N14ContractBadgeJunktownRat"  },
        { "Legionary",   "N14ContractBadgeHubMercenary"    },
        { "Decanus","N14ContractBadgeBunkerBuster" },
        { "Centurion","N14ContractBadgeWastelandLegend" },
        { "Tribal", "N14ContractBadgeRoadKill" },
        { "Settler",   "N14ContractBadgeLazyLizard"   },
        { "Citizen", "N14ContractBadgeJunktownRat"  },
        { "Caravaneer",   "N14ContractBadgeHubMercenary"    },
        { "Caravan Master","N14ContractBadgeBunkerBuster" },
        { "Brahmin Baron","N14ContractBadgeWastelandLegend" },
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

        var profile = TryComp(store, out NcStoreComponent? storeComp)
            ? storeComp.ContractTierProfile
            : NcTierProgressComponent.BaseProfile;
        EnsureComp<NcTierProgressComponent>(user, out var prog);
        var unlocked = prog.GetUnlockedTiers(profile);

        if (unlocked.Count == 0)
        {
            var entryTier = NcTierProgressComponent.GetEntryTier(profile);
            unlocked.Add(entryTier);
            SpawnBadge(entryTier, user);
            _popups.PopupEntity(Loc.GetString("nc-contract-tier-first-access"), user, user);
        }

        RecordRosterVisit(store, user, prog, profile);
    }

    // Fired after a contract is successfully claimed.
    // Increments the tier completion counter and checks whether the next tier unlocks.
    private void OnContractClaimed(MisfitsContractClaimedEvent ev)
    {
        var user = ev.User;
        var store = ev.Store;
        var tier = ev.Difficulty;
        var profile = ev.Profile;

        if (!TryComp<NcTierProgressComponent>(user, out var prog))
            return;

        var completed = prog.GetCompletedByTier(profile);
        completed.TryGetValue(tier, out var prev);
        completed[tier] = prev + 1;

        TryAdvanceTier(user, prog, profile, tier);
        RecordRosterVisit(store, user, prog, profile);
    }

    // Checks whether the player has earned enough completions in currentTier to unlock the next one.
    private void TryAdvanceTier(EntityUid user, NcTierProgressComponent prog, string profile, string currentTier)
    {
        var tiers = NcTierProgressComponent.GetTiers(profile);
        var idx = -1;
        for (var i = 0; i < tiers.Count; i++)
        {
            if (tiers[i] == currentTier)
            {
                idx = i;
                break;
            }
        }
        if (idx < 0 || idx >= tiers.Count - 1)
            return; // Not found or already at Diamond.

        var nextTier = tiers[idx + 1];
        var unlocked = prog.GetUnlockedTiers(profile);
        if (unlocked.Contains(nextTier))
            return; // Already unlocked.

        prog.GetCompletedByTier(profile).TryGetValue(currentTier, out var done);
        if (done < NcTierProgressComponent.ContractsToAdvance)
            return;

        // Unlock the next tier and award the corresponding badge.
        unlocked.Add(nextTier);
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
    private void RecordRosterVisit(EntityUid store, EntityUid user, NcTierProgressComponent prog, string profile)
    {
        if (!TryComp<NcContractRosterComponent>(store, out var roster))
            return;

        var name = MetaData(user).EntityName;

        // Determine the highest unlocked tier.
        var tiers = NcTierProgressComponent.GetTiers(profile);
        var unlocked = prog.GetUnlockedTiers(profile);
        var highestTier = tiers[0];
        foreach (var tier in tiers)
        {
            if (unlocked.Contains(tier))
                highestTier = tier;
        }

        var total = 0;
        foreach (var v in prog.GetCompletedByTier(profile).Values)
            total += v;

        roster.UpdateEntry(name, highestTier, total);
    }
}
