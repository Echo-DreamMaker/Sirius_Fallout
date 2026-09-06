using Content.Shared.Access;
using Content.Shared.Lathe.Prototypes;
using Content.Shared._Misfits.Currency.Components;
using Content.Shared.Roles;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC.Vendors;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(SharedCMAutomatedVendorSystem))]
public sealed partial class CMAutomatedVendorComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<CMVendorSection> Sections = new();

    [DataField, AutoNetworkedField]
    public List<ProtoId<JobPrototype>> Jobs = new();

    /// <summary>
    /// Optional job gate for the authority-tier stock only. Department storage and replenishment remain available
    /// to everyone who can access the faction vendor.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<ProtoId<JobPrototype>> TierJobs = new();

    /// <summary>
    /// Jobs that may issue every allocation tier at this vendor without also granting their access-card tags
    /// everywhere else. This is deliberately vendor-local for roles such as a requisitions officer.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<ProtoId<JobPrototype>> FullAllocationJobs = new();

    [DataField, AutoNetworkedField]
    public List<ProtoId<AccessLevelPrototype>> Access = new();

    /// <summary>
    /// Maximum blueprint-style authority tier exposed by this vendor.
    /// Misfits vendor tiers intentionally stop at four.
    /// </summary>
    [DataField]
    public int MaxAuthorityTier = 4;

    /// <summary>
    /// Required cumulative access tag(s) for each authority tier.
    /// Higher tiers should be assigned to higher faction authority groups.
    /// </summary>
    [DataField]
    public Dictionary<int, List<ProtoId<AccessLevelPrototype>>> AuthorityTierAccess = new();

    /// <summary>
    /// Player-facing allocation labels for each authority level, e.g. "Tier 1 - Trooper allocation".
    /// </summary>
    [DataField]
    public Dictionary<int, string> AuthorityTierNames = new();

    /// <summary>
    /// Player-facing faction/department name used by the shared equipment area.
    /// </summary>
    [DataField]
    public string DepartmentName = "department";

    /// <summary>
    /// Ordered allocation sub-tabs shown by this vendor. Additional categories used by entries are appended.
    /// </summary>
    [DataField]
    public List<string> AllocationCategories = new();

    /// <summary>
    /// Ordered shared-equipment sub-tabs shown by this vendor. Additional categories used by stored items are appended.
    /// </summary>
    [DataField]
    public List<string> SharedEquipmentCategories = new();

    [DataField]
    public string DefaultAllocationCategory = "Other";

    [DataField]
    public string DefaultSharedEquipmentCategory = "Other";

    /// <summary>
    /// Optional prototype-to-sub-tab map for stored physical equipment.
    /// </summary>
    [DataField]
    public Dictionary<EntProtoId, string> StorageCategories = new();

    /// Builds stock from the same blueprint lathe recipes used by faction crafting.
    /// Explicit Sections remain available for non-blueprint stock and RMC bundles.
    [DataField]
    public List<ProtoId<LatheCategoryPrototype>> BlueprintCategories = new();

    [DataField]
    public bool PopulateFromBlueprints = true;

    [DataField]
    public bool BlueprintStockInitialized;

    /// Legacy categories without a T1-T4 suffix, currently used by Enclave recipes.
    [DataField]
    public Dictionary<EntProtoId, int> BlueprintTierOverrides = new();

    /// <summary>
    /// Per-result catalog controls. Use these in a faction YAML file to disable a blueprint result or change its
    /// authority tier, finite stock ceiling, and issue-point cost without changing the vendor system.
    /// </summary>
    [DataField]
    public Dictionary<EntProtoId, CMVendorBlueprintEntryOverride> BlueprintEntryOverrides = new();

    /// <summary>
    /// Default finite stock by authority tier for blueprint-generated entries. Individual entries may override it.
    /// </summary>
    [DataField]
    public Dictionary<int, int> BlueprintStockByTier = new()
    {
        [1] = 10,
        [2] = 8,
        [3] = 5,
        [4] = 2,
    };

    /// <summary>
    /// Physical currencies this faction accepts to replenish finite stock.
    /// Currency is consumed into a shared vendor-local balance.
    /// </summary>
    [DataField]
    public List<CMVendorReplenishmentRule> Replenishment = new();

    /// <summary>
    /// Shared replenishment balance. It is spent automatically on depleted stock.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int ReplenishmentPoints;

    /// <summary>
    /// Cost in replenishment points for one unit of stock at each authority tier.
    /// </summary>
    [DataField]
    public Dictionary<int, int> ReplenishmentCosts = new();

    /// <summary>
    /// Allows authorized department members to escrow physical equipment as free shared issue.
    /// </summary>
    [DataField]
    public bool AllowEquipmentStorage = true;

    [DataField]
    public int MaxStoredItems = 50;

    /// <summary>
    /// Optional further restriction for deposited equipment. The Item component is always required.
    /// </summary>
    [DataField]
    public EntityWhitelist? StorageWhitelist;

    /// <summary>
    /// Optional hard exclusion for stored items. Applied after the whitelist.
    /// </summary>
    [DataField]
    public EntityWhitelist? StorageBlacklist;

    [DataField, AutoNetworkedField]
    public bool Hacked;

    [DataField, AutoNetworkedField]
    public bool Hackable;
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class CMVendorReplenishmentRule
{
    [DataField(required: true)]
    public CurrencyType Currency;

    /// <summary>
    /// Multiplies the physical item's value before it is added to the vendor's shared stock pool.
    /// </summary>
    [DataField]
    public int Multiplier = 1;
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class CMVendorBlueprintEntryOverride
{
    [DataField]
    public bool Enabled = true;

    [DataField]
    public int? Tier;

    [DataField]
    public int? Amount;

    [DataField]
    public int? MaxAmount;

    [DataField]
    public int? Points;

    [DataField]
    public int? ReplenishmentCost;

    [DataField]
    public string? Category;
}
