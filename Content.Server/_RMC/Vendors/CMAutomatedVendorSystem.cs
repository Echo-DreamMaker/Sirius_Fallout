using Content.Shared._RMC.Vendors;
using Content.Shared._Misfits.Currency.Components;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Containers;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Item;
using Content.Shared.Lathe.Prototypes;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Content.Shared.Roles.Jobs;
using Content.Shared.Stacks;
using Content.Shared.UserInterface;
using Content.Shared.Whitelist;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._RMC.Vendors;

public sealed partial class CMAutomatedVendorSystem : SharedCMAutomatedVendorSystem
{
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private SharedMindSystem _minds = default!;
    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private ContainerSystem _container = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private SharedTransformSystem _xform = default!;

    private const string EquipmentStorageContainer = "rmc-vendor-equipment";

    public override void Initialize()
    {
        base.Initialize();
        Subs.BuiEvents<CMAutomatedVendorComponent>(CMAutomatedVendorUiKey.Key, subs =>
        {
            subs.Event<CMAutomatedVendorVendMessage>(OnVend);
            subs.Event<CMAutomatedVendorReplenishMessage>(OnReplenish);
            subs.Event<CMAutomatedVendorStoreHeldMessage>(OnStoreHeld);
            subs.Event<CMAutomatedVendorWithdrawStoredMessage>(OnWithdrawStored);
        });
    }

    protected override void OnOpenAttempt(Entity<CMAutomatedVendorComponent> vendor, ref ActivatableUIOpenAttemptEvent args)
    {
        base.OnOpenAttempt(vendor, ref args);
        if (args.Cancelled)
            return;

        if (!HasAuthorizedJob(vendor, args.User))
        {
            args.Cancel();
            _popup.PopupEntity("Your assignment does not authorize this vendor.", vendor, args.User);
            return;
        }

        PopulateBlueprintSections(vendor);
        InitializeStockCaps(vendor);
        ReplenishStock(vendor);
        EnsureComp<CMVendorUserComponent>(args.User);
        UpdateState(vendor, args.User);
    }

    private void PopulateBlueprintSections(Entity<CMAutomatedVendorComponent> vendor)
    {
        if (!vendor.Comp.PopulateFromBlueprints || vendor.Comp.BlueprintStockInitialized || vendor.Comp.BlueprintCategories.Count == 0)
            return;

        var categories = vendor.Comp.BlueprintCategories.Select(x => x.ToString()).ToHashSet();
        // Preserve hand-authored allocation entries (for former job loadout kits) while adding blueprint stock.
        // A matching authority section is reused so both sources remain under the same rank allocation.
        var sections = vendor.Comp.Sections
            .ToDictionary(section => section.Name, section => section);
        foreach (var recipe in _prototypes.EnumeratePrototypes<LatheRecipePrototype>())
        {
            if (recipe.Result is not { } result || recipe.Category is not { } category ||
                !categories.Contains(category.ToString()))
                continue;

            var tier = vendor.Comp.BlueprintTierOverrides.TryGetValue(result, out var overrideTier)
                ? overrideTier
                : GetBlueprintTier(category.ToString());
            vendor.Comp.BlueprintEntryOverrides.TryGetValue(result, out var entryOverride);
            if (entryOverride is { Enabled: false })
                continue;

            tier = entryOverride?.Tier ?? tier;
            if (tier is < 1 or > 4 || !_prototypes.HasIndex<EntityPrototype>(result))
                continue;

            var sectionName = vendor.Comp.AuthorityTierNames.GetValueOrDefault(tier, $"Authority Tier {tier}");
            if (!sections.TryGetValue(sectionName, out var section))
            {
                section = new CMVendorSection
                {
                    Name = sectionName
                };
                sections.Add(sectionName, section);
            }

            if (section.Entries.Any(entry => entry.Id == result))
                continue;

            var defaultAmount = vendor.Comp.BlueprintStockByTier.GetValueOrDefault(tier);
            var amount = entryOverride?.Amount ?? defaultAmount;
            section.Entries.Add(new CMVendorEntry
            {
                Id = result,
                Amount = amount,
                MaxAmount = entryOverride?.MaxAmount ?? amount,
                Points = entryOverride?.Points,
                ReplenishmentCost = entryOverride?.ReplenishmentCost,
                Category = entryOverride?.Category ?? vendor.Comp.DefaultAllocationCategory,
                Tier = tier,
            });
        }

        vendor.Comp.Sections = sections.Values
            .OrderBy(section => section.Entries.Count == 0 ? int.MaxValue : section.Entries.Min(entry => entry.Tier))
            .ThenBy(section => section.Name)
            .ToList();
        vendor.Comp.BlueprintStockInitialized = true;
        Dirty(vendor);
    }

    private static int GetBlueprintTier(string category)
    {
        for (var tier = 4; tier >= 1; tier--)
        {
            if (category.EndsWith($"T{tier}", StringComparison.OrdinalIgnoreCase))
                return tier;
        }

        // Legacy Enclave categories are normalized by BlueprintTierOverrides.
        return 1;
    }

    private void OnVend(Entity<CMAutomatedVendorComponent> vendor, ref CMAutomatedVendorVendMessage message)
    {
        var user = message.Actor;
        if (!CanUseVendor(vendor, user))
            return;

        if (!HasTierAuthorization(vendor, user))
        {
            Deny(vendor, user, "Your assignment does not authorize tiered armory stock.");
            return;
        }

        if (message.Section < 0 || message.Section >= vendor.Comp.Sections.Count)
            return;

        var section = vendor.Comp.Sections[message.Section];
        if (!HasSectionAuthorization(vendor, section, user))
        {
            Deny(vendor, user, "Your assignment does not authorize this allocation.");
            return;
        }

        if (message.Entry < 0 || message.Entry >= section.Entries.Count)
            return;

        var entry = section.Entries[message.Entry];
        if (entry.Tier is < 1 or > 4 || entry.Tier > Math.Min(4, vendor.Comp.MaxAuthorityTier) ||
            !HasAuthorityTier(vendor, user, entry.Tier))
        {
            Deny(vendor, user, "Your faction authority is insufficient for this equipment.");
            return;
        }

        if (!TryComp<CMVendorUserComponent>(user, out var userComp))
        {
            userComp = EnsureComp<CMVendorUserComponent>(user);
        }

        if (section.Choices is { } choices &&
            userComp.SectionPurchases.GetValueOrDefault(choices.Id) >= choices.Amount)
        {
            Deny(vendor, user, "This equipment selection is already exhausted.");
            return;
        }

        if (entry.Amount is <= 0)
        {
            Deny(vendor, user, "That equipment is out of stock.");
            return;
        }

        if (entry.Points is { } cost && userComp.Points < cost)
        {
            Deny(vendor, user, "You do not have enough armory points.");
            return;
        }

        if (section.TakeOne != null && userComp.SectionPurchases.ContainsKey(section.TakeOne))
        {
            Deny(vendor, user, "You have already selected equipment from this section.");
            return;
        }

        if (section.TakeAll != null && userComp.SectionPurchases.ContainsKey(section.TakeAll))
        {
            Deny(vendor, user, "You have already claimed this equipment kit.");
            return;
        }

        entry.Amount--;
        if (entry.Points is { } points)
            userComp.Points -= points;

        if (section.Choices is { } choice)
            userComp.SectionPurchases[choice.Id] = userComp.SectionPurchases.GetValueOrDefault(choice.Id) + 1;
        if (section.TakeOne is { } takeOne)
            userComp.SectionPurchases[takeOne] = 1;
        if (section.TakeAll is { } takeAll)
            userComp.SectionPurchases[takeAll] = 1;

        Vend(user, entry.Id);
        foreach (var linked in entry.LinkedEntries)
            Vend(user, linked);

        Dirty(user, userComp);
        Dirty(vendor);
        UpdateState(vendor, user);
    }

    private void OnReplenish(Entity<CMAutomatedVendorComponent> vendor, ref CMAutomatedVendorReplenishMessage message)
    {
        var user = message.Actor;
        if (!CanUseVendor(vendor, user))
            return;

        if (vendor.Comp.Replenishment.Count == 0)
        {
            Deny(vendor, user, "This vendor does not accept replenishment supplies.");
            return;
        }

        if (!_hands.TryGetActiveItem(user, out var heldItem) || heldItem is not { } held ||
            !TryComp<ConsumableCurrencyComponent>(held, out var currency))
        {
            Deny(vendor, user, "Hold an accepted replenishment currency item first.");
            return;
        }

        var rule = vendor.Comp.Replenishment.FirstOrDefault(rule => rule.Currency == currency.CurrencyType);
        if (rule == null)
        {
            Deny(vendor, user, "That currency is not accepted by this vendor.");
            return;
        }

        var count = TryComp<StackComponent>(held, out var stack) ? stack.Count : 1;
        var contribution = count * currency.ValuePerUnit * Math.Max(1, rule.Multiplier);
        if (contribution <= 0)
        {
            Deny(vendor, user, "That item has no replenishment value.");
            return;
        }

        Del(held);
        vendor.Comp.ReplenishmentPoints += contribution;
        InitializeStockCaps(vendor);
        ReplenishStock(vendor);
        Dirty(vendor);
        _popup.PopupEntity($"Added {contribution} replenishment point(s) to faction stock.", vendor, user);
        UpdateState(vendor, user);
    }

    private void OnStoreHeld(Entity<CMAutomatedVendorComponent> vendor, ref CMAutomatedVendorStoreHeldMessage message)
    {
        var user = message.Actor;
        if (!CanUseVendor(vendor, user))
            return;

        if (!vendor.Comp.AllowEquipmentStorage)
        {
            Deny(vendor, user, "This vendor does not accept stored equipment.");
            return;
        }

        if (!_hands.TryGetActiveItem(user, out var heldItem) || heldItem is not { } held || !HasComp<ItemComponent>(held))
        {
            Deny(vendor, user, "Hold an item to store it for your department.");
            return;
        }

        if (HasComp<ConsumableCurrencyComponent>(held))
        {
            Deny(vendor, user, "Currency must be contributed through replenishment, not equipment storage.");
            return;
        }

        if (!_whitelist.IsWhitelistPassOrNull(vendor.Comp.StorageWhitelist, held))
        {
            Deny(vendor, user, "That item is not accepted as department equipment.");
            return;
        }

        if (_whitelist.IsBlacklistPass(vendor.Comp.StorageBlacklist, held))
        {
            Deny(vendor, user, "That item belongs in the other department vendor.");
            return;
        }

        var storage = _container.EnsureContainer<Container>(vendor, EquipmentStorageContainer);
        if (storage.ContainedEntities.Count >= vendor.Comp.MaxStoredItems)
        {
            Deny(vendor, user, "Department equipment storage is full.");
            return;
        }

        if (!_container.Insert(held, storage))
        {
            Deny(vendor, user, "The vendor could not store that equipment.");
            return;
        }

        _popup.PopupEntity("Equipment stored for your department.", vendor, user);
        UpdateState(vendor, user);
    }

    private void OnWithdrawStored(Entity<CMAutomatedVendorComponent> vendor, ref CMAutomatedVendorWithdrawStoredMessage message)
    {
        var user = message.Actor;
        if (!CanUseVendor(vendor, user))
            return;

        if (!_container.TryGetContainer(vendor, EquipmentStorageContainer, out var storage) ||
            message.Index < 0 || message.Index >= storage.ContainedEntities.Count)
            return;

        var item = storage.ContainedEntities.ElementAt(message.Index);
        if (!_container.Remove(item, storage))
            return;

        if (!_hands.TryPickupAnyHand(user, item, checkActionBlocker: false))
            _xform.DropNextTo(item, vendor.Owner);

        UpdateState(vendor, user);
    }

    private bool HasAuthorizedJob(Entity<CMAutomatedVendorComponent> vendor, EntityUid user)
    {
        return vendor.Comp.Jobs.Count == 0 ||
               _minds.TryGetMind(user, out var mindId, out _) &&
               vendor.Comp.Jobs.Any(job => _jobs.MindHasJobWithId(mindId, job.ToString()));
    }

    private bool HasTierAuthorization(Entity<CMAutomatedVendorComponent> vendor, EntityUid user)
    {
        return vendor.Comp.TierJobs.Count == 0 ||
               _minds.TryGetMind(user, out var mindId, out _) &&
               vendor.Comp.TierJobs.Any(job => _jobs.MindHasJobWithId(mindId, job.ToString()));
    }

    private bool HasFullAllocationAuthorization(Entity<CMAutomatedVendorComponent> vendor, EntityUid user)
    {
        return vendor.Comp.FullAllocationJobs.Count > 0 &&
               _minds.TryGetMind(user, out var mindId, out _) &&
               vendor.Comp.FullAllocationJobs.Any(job => _jobs.MindHasJobWithId(mindId, job.ToString()));
    }

    private bool HasSectionAuthorization(Entity<CMAutomatedVendorComponent> vendor, CMVendorSection section, EntityUid user)
    {
        return section.Jobs.Count == 0 ||
               HasFullAllocationAuthorization(vendor, user) ||
               _minds.TryGetMind(user, out var mindId, out _) &&
               section.Jobs.Any(job => _jobs.MindHasJobWithId(mindId, job.ToString()));
    }

    private bool CanUseVendor(Entity<CMAutomatedVendorComponent> vendor, EntityUid user)
    {
        if (!vendor.Comp.Hacked && !_access.IsAllowed(user, vendor))
        {
            Deny(vendor, user, "Your access does not authorize this vendor.");
            return false;
        }

        if (!HasAuthorizedJob(vendor, user))
        {
            Deny(vendor, user, "Your assignment does not authorize this vendor.");
            return false;
        }

        return true;
    }

    private static void InitializeStockCaps(Entity<CMAutomatedVendorComponent> vendor)
    {
        foreach (var entry in vendor.Comp.Sections.SelectMany(section => section.Entries))
        {
            // Former roundstart-kit entries are authored directly in YAML rather than generated from blueprints.
            // Give them the same finite faction stock and replenishment path by default.
            entry.Amount ??= vendor.Comp.BlueprintStockByTier.GetValueOrDefault(entry.Tier);
            entry.MaxAmount ??= entry.Amount;
        }
    }

    /// <summary>
    /// Restores one item per entry per pass so shared resources do not refill only the first listing in a tier.
    /// Unspent points remain on the vendor until enough supplies are contributed for another stock unit.
    /// </summary>
    private static void ReplenishStock(Entity<CMAutomatedVendorComponent> vendor)
    {
        var replenished = true;
        while (replenished)
        {
            replenished = false;
            foreach (var entry in vendor.Comp.Sections.SelectMany(section => section.Entries).OrderBy(entry => entry.Tier))
            {
                var cost = entry.ReplenishmentCost ?? vendor.Comp.ReplenishmentCosts.GetValueOrDefault(entry.Tier);
                if (cost <= 0 || entry.Amount is not { } amount || entry.MaxAmount is not { } maxAmount ||
                    amount >= maxAmount || vendor.Comp.ReplenishmentPoints < cost)
                    continue;

                entry.Amount++;
                vendor.Comp.ReplenishmentPoints -= cost;
                replenished = true;
            }
        }
    }

    private void Vend(EntityUid user, EntProtoId prototype)
    {
        if (!_prototypes.HasIndex(prototype))
            return;

        var item = Spawn(prototype, Transform(user).Coordinates);
        if (TryComp<CMVendorBundleComponent>(item, out var bundle))
        {
            foreach (var bundled in bundle.Bundle)
                Vend(user, bundled);
        }

        _hands.TryPickupAnyHand(user, item, checkActionBlocker: false);
    }

    private void Deny(Entity<CMAutomatedVendorComponent> vendor, EntityUid user, string message)
    {
        _popup.PopupEntity(message, vendor, user);
        UpdateState(vendor, user);
    }

    private void UpdateState(Entity<CMAutomatedVendorComponent> vendor, EntityUid user)
    {
        if (!TryComp<CMVendorUserComponent>(user, out var userComp))
            return;

        var sections = new List<CMVendorSectionState>();
        if (HasTierAuthorization(vendor, user))
        {
            foreach (var section in vendor.Comp.Sections)
            {
                if (!HasSectionAuthorization(vendor, section, user))
                    continue;

                var purchases = section.Choices is { } choices
                    ? userComp.SectionPurchases.GetValueOrDefault(choices.Id)
                    : 0;
                sections.Add(new CMVendorSectionState(
                    section.Name,
                    section.Choices?.Amount,
                    purchases,
                section.Entries.Select(entry => new CMVendorEntryState(
                entry.Name ?? entry.Id.ToString(),
                entry.Id,
                entry.Amount,
                entry.Points,
                entry.Tier,
                HasAuthorityTier(vendor, user, entry.Tier),
                vendor.Comp.AuthorityTierNames.GetValueOrDefault(entry.Tier, $"authority tier {entry.Tier}"),
                entry.Category ?? vendor.Comp.DefaultAllocationCategory)).ToList()));
            }
        }

        var storedItems = new List<CMVendorStoredItemState>();
        if (_container.TryGetContainer(vendor, EquipmentStorageContainer, out var storage))
        {
            foreach (var item in storage.ContainedEntities)
            {
                var prototype = MetaData(item).EntityPrototype;
                if (prototype == null)
                    continue;

                storedItems.Add(new CMVendorStoredItemState(
                    prototype.Name,
                    prototype.ID,
                    vendor.Comp.StorageCategories.GetValueOrDefault(prototype.ID, vendor.Comp.DefaultSharedEquipmentCategory)));
            }
        }

        _ui.SetUiState(vendor.Owner, CMAutomatedVendorUiKey.Key,
            new CMAutomatedVendorState(
                sections,
                storedItems,
                userComp.Points,
                vendor.Comp.ReplenishmentPoints,
                vendor.Comp.Replenishment.Count > 0,
                vendor.Comp.AllowEquipmentStorage,
                vendor.Comp.DepartmentName,
                vendor.Comp.AllocationCategories,
                vendor.Comp.SharedEquipmentCategories));
    }

    private bool HasAuthorityTier(Entity<CMAutomatedVendorComponent> vendor, EntityUid user, int tier)
    {
        if (HasFullAllocationAuthorization(vendor, user))
            return true;

        if (!vendor.Comp.AuthorityTierAccess.TryGetValue(tier, out var required) || required.Count == 0)
            return tier == 1;

        if (!_access.FindAccessItemsInventory(user, out var items))
            return false;

        var tags = new HashSet<ProtoId<AccessLevelPrototype>>();
        foreach (var item in items)
        {
            var ev = new GetAccessTagsEvent(tags, _prototypes);
            RaiseLocalEvent(item, ref ev);
        }

        return required.Any(tags.Contains);
    }
}
