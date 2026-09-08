using System.Linq;
using Content.Shared._Misfits.Special;
using Content.Shared._Misfits.Supporter; // #Cythisiax Add - Patreon supporter loadouts
using Content.Shared._NC.Sponsor; // Forge-Change
using Content.Shared.Body.Systems;
using Content.Shared.CCVar;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.Loadouts.Prototypes;
using Content.Shared.Customization.Systems;
using Content.Shared.Inventory;
using Content.Shared.Paint;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Station;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Content.Shared.Storage;

namespace Content.Shared.Clothing.Loadouts.Systems;

public sealed class SharedLoadoutSystem : EntitySystem
{
    [Dependency] private readonly SharedStationSpawningSystem _station = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly CharacterRequirementsSystem _characterRequirements = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedTransformSystem _sharedTransformSystem = default!;
    [Dependency] private readonly ILogManager _log = default!;
    [Dependency] private readonly ISharedSponsorManager _sponsorManager = default!; // Forge-Change
    [Dependency] private readonly ISharedSupporterManager _supporterManager = default!; // #Cythisiax Add - Patreon supporter loadouts
    [Dependency] private readonly MetaDataSystem _metaData = default!;

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LoadoutComponent, MapInitEvent>(OnMapInit, after: [typeof(SharedBodySystem)]);
        _sawmill = _log.GetSawmill("loadouts");
    }

    private void OnMapInit(EntityUid uid, LoadoutComponent component, MapInitEvent args)
    {
        if (component.StartingGear is null || component.StartingGear.Count <= 0)
            return;

        var proto = _prototype.Index(_random.Pick(component.StartingGear));
        _station.EquipStartingGear(uid, proto);
    }

    public (List<EntityUid>, List<(EntityUid, LoadoutPreference, int)>) ApplyCharacterLoadout(
        EntityUid uid,
        ProtoId<JobPrototype> job,
        HumanoidCharacterProfile profile,
        Dictionary<string, TimeSpan> playTimes,
        bool whitelisted,
        out List<(EntityUid, LoadoutPreference)> heirlooms,
        NetUserId? player = null) // #Cythisiax Edited - player for Patreon tier enforcement
    {
        var jobPrototype = _prototype.Index(job);
        return ApplyCharacterLoadout(uid, jobPrototype, profile, playTimes, whitelisted, out heirlooms, player);
    }

    /// <summary>
    ///     Equips entities from a <see cref="HumanoidCharacterProfile"/>'s loadout preferences to a given entity
    /// </summary>
    /// <param name="uid">The entity to give the loadout items to</param>
    /// <param name="job">The job to use for loadout whitelist/blacklist (should be the job of the entity)</param>
    /// <param name="profile">The profile to get loadout items from (should be the entity's, or at least have the same species as the entity)</param>
    /// <param name="playTimes">Playtime for the player for use with playtime requirements</param>
    /// <param name="whitelisted">If the player is whitelisted</param>
    /// <param name="heirlooms">Every entity the player selected as a potential heirloom</param>
    /// <param name="player">The owning player, used to enforce Patreon supporter loadout tiers server-side</param>
    /// <returns>A list of loadout items that couldn't be equipped but passed checks</returns>
    public (List<EntityUid>, List<(EntityUid, LoadoutPreference, int)>) ApplyCharacterLoadout(
        EntityUid uid,
        JobPrototype job,
        HumanoidCharacterProfile profile,
        Dictionary<string, TimeSpan> playTimes,
        bool whitelisted,
        out List<(EntityUid, LoadoutPreference)> heirlooms,
        NetUserId? player = null) // #Cythisiax Edited - player for Patreon tier enforcement
    {
        var failedLoadouts = new List<EntityUid>();
        var allLoadouts = new List<(EntityUid, LoadoutPreference, int)>();
        heirlooms = new();
        if (!job.SpawnLoadout)
            return (failedLoadouts, allLoadouts);

        var remainingPoints = Math.Max(0, _configuration.GetCVar(CCVars.GameLoadoutsPoints)
            + SharedSpecialSystem.GetCharismaLoadoutPointModifier(SpecialProfile.EnsureValid(profile.Special).Charisma));

        foreach (var loadout in profile.LoadoutPreferences)
        {
            if (!loadout.Selected)
                continue;

            var slot = "";

            if (!_prototype.TryIndex<LoadoutPrototype>(loadout.LoadoutName, out var loadoutProto))
                continue;

            // #Cythisiax Added - server-side Patreon supporter enforcement (defense against edited profiles)
            if (loadoutProto.SupporterTier != SupporterTier.None)
            {
                if (player is not { } userId
                    || !_supporterManager.TryGetSupporterTier(userId, out var supporterTier)
                    || loadoutProto.SupporterTier > supporterTier)
                {
                    _sawmill.Verbose($"Skipping supporter loadout {loadoutProto.ID} for {player}; requires tier {loadoutProto.SupporterTier}.");
                    continue;
                }
            }

            if (!_characterRequirements.CheckRequirementsValid(
                loadoutProto.Requirements, job, profile, playTimes, whitelisted, loadoutProto,
                EntityManager, _prototype, _configuration, _sponsorManager,
                out _))
                continue;

            if (loadoutProto.Cost > remainingPoints)
                continue;

            remainingPoints -= loadoutProto.Cost;

            var spawned = EntityManager.SpawnEntities(
                _sharedTransformSystem.GetMapCoordinates(uid),
                loadoutProto.Items.Select(p => (string?) p.ToString()).ToList());

            var i = 0;
            foreach (var item in spawned)
            {
                if (item == EntityUid.Invalid || !Exists(item))
                {
                    _sawmill.Warning($"Item {ToPrettyString(item)} failed to spawn or did not exist.");
                    continue;
                }

                allLoadouts.Add((item, loadout, i));
                if (i == 0 && loadout.CustomHeirloom == true)
                    heirlooms.Add((item, loadout));

                // Применяем кастомизацию до экипировки
                if (loadoutProto.CustomName && loadout.CustomName != null)
                    _metaData.SetEntityName(item, loadout.CustomName);
                if (loadoutProto.CustomDescription && loadout.CustomDescription != null)
                    _metaData.SetEntityDescription(item, loadout.CustomDescription);
                if (loadoutProto.CustomColorTint && !string.IsNullOrEmpty(loadout.CustomColorTint))
                {
                    EnsureComp<AppearanceComponent>(item);
                    EnsureComp<PaintedComponent>(item, out var paint);
                    paint.Color = Color.FromHex(loadout.CustomColorTint);
                    paint.Enabled = true;
                    _appearance.SetData(item, PaintVisuals.Painted, true);
                }

                // Попытка экипировки
                if (EntityManager.TryGetComponent<ClothingComponent>(item, out var clothingComp)
                    && _characterRequirements.CanEntityWearItem(uid, item, true)
                    && _inventory.TryGetSlots(uid, out var slotDefinitions))
                {
                    var deleted = false;
                    foreach (var curSlot in slotDefinitions)
                    {
                        if (!clothingComp.Slots.HasFlag(curSlot.SlotFlags) || deleted)
                            continue;

                        // Запрет экипировки в back/belt, если предмет не хранилище
                        if ((curSlot.SlotFlags.HasFlag(SlotFlags.BACK) || curSlot.SlotFlags.HasFlag(SlotFlags.BELT))
                            && !HasComp<StorageComponent>(item))
                            continue;

                        slot = curSlot.Name;

                        // Для suitstorage не применяем Exclusive (чтобы не удалять существующий предмет)
                        if (loadoutProto.Exclusive && !curSlot.SlotFlags.HasFlag(SlotFlags.SUITSTORAGE))
                        {
                            if (!_inventory.TryGetSlotEntity(uid, curSlot.Name, out var slotItem))
                                continue;

                            EntityManager.DeleteEntity(slotItem.Value);
                            deleted = true;
                        }
                    }
                }

                // Дублирующая покраска (оставлена для обратной совместимости, но уже не нужна)
                if (loadout.CustomColorTint != null)
                {
                    EnsureComp<AppearanceComponent>(item);
                    EnsureComp<PaintedComponent>(item, out var paint);
                    paint.Color = Color.FromHex(loadout.CustomColorTint);
                    paint.Enabled = true;
                    _appearance.TryGetData(item, PaintVisuals.Painted, out bool data);
                    _appearance.SetData(item, PaintVisuals.Painted, !data);
                }

                if (!_inventory.TryEquip(uid, item, slot, true, !string.IsNullOrEmpty(slot), true))
                    failedLoadouts.Add(item);

                i++;
            }
        }

        return (failedLoadouts, allLoadouts);
    }
}

[Serializable, NetSerializable, ImplicitDataDefinitionForInheritors]
public abstract partial class Loadout
{
    [DataField] public string LoadoutName { get; set; }
    [DataField] public string? CustomName { get; set; }
    [DataField] public string? CustomDescription { get; set; }
    [DataField] public string? CustomColorTint { get; set; }
    [DataField] public bool? CustomHeirloom { get; set; }

    protected Loadout(
        string loadoutName,
        string? customName = null,
        string? customDescription = null,
        string? customColorTint = null,
        bool? customHeirloom = null
    )
    {
        LoadoutName = loadoutName;
        CustomName = customName;
        CustomDescription = customDescription;
        CustomColorTint = customColorTint;
        CustomHeirloom = customHeirloom;
    }
}

[Serializable, NetSerializable]
public sealed partial class LoadoutPreference : Loadout
{
    [DataField] public bool Selected;

    public LoadoutPreference(
        string loadoutName,
        string? customName = null,
        string? customDescription = null,
        string? customColorTint = null,
        bool? customHeirloom = null
    ) : base(loadoutName, customName, customDescription, customColorTint, customHeirloom) { }
}
