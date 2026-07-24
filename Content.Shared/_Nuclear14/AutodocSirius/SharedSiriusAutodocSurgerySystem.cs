using Content.Shared._Nuclear14.AutodocSirius;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Containers;
using Content.Shared.DoAfter;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Timing;
using Robust.Shared.Serialization;
using System.Linq;
using System;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Body.Components;

namespace Content.Shared._Nuclear14.AutodocSirius;

public abstract class SharedSiriusAutodocSurgerySystem : EntitySystem
{
    [Dependency] protected readonly IGameTiming _gameTiming = default!;
    [Dependency] protected readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] protected readonly SharedBodySystem _bodySystem = default!;
    [Dependency] protected readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] protected readonly SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public const float SurgeryOperationDuration = 5f;
    private static readonly ISawmill _sawmill = Logger.GetSawmill("autodoc");

    protected static readonly Dictionary<string, string> BodyPartDisplayNames = new()
    {
        { "head", "Голова" },
        { "torso", "Торс" },
        { "left_arm", "Левая рука" },
        { "right_arm", "Правая рука" },
        { "left_hand", "Левая кисть" },
        { "right_hand", "Правая кисть" },
        { "left_leg", "Левая нога" },
        { "right_leg", "Правая нога" },
        { "left_foot", "Левая стопа" },
        { "right_foot", "Правая стопа" },
    };

    protected sealed class BodyPartInfo
    {
        public BodyPartType Type { get; }
        public BodyPartSymmetry? Symmetry { get; }

        public BodyPartInfo(BodyPartType type, BodyPartSymmetry? symmetry)
        {
            Type = type;
            Symmetry = symmetry;
        }
    }

    protected static readonly Dictionary<string, BodyPartInfo> BodyPartMap = new()
    {
        { "head", new BodyPartInfo(BodyPartType.Head, null) },
        { "torso", new BodyPartInfo(BodyPartType.Torso, null) },
        { "left_arm", new BodyPartInfo(BodyPartType.Arm, BodyPartSymmetry.Left) },
        { "right_arm", new BodyPartInfo(BodyPartType.Arm, BodyPartSymmetry.Right) },
        { "left_hand", new BodyPartInfo(BodyPartType.Hand, BodyPartSymmetry.Left) },
        { "right_hand", new BodyPartInfo(BodyPartType.Hand, BodyPartSymmetry.Right) },
        { "left_leg", new BodyPartInfo(BodyPartType.Leg, BodyPartSymmetry.Left) },
        { "right_leg", new BodyPartInfo(BodyPartType.Leg, BodyPartSymmetry.Right) },
        { "left_foot", new BodyPartInfo(BodyPartType.Foot, BodyPartSymmetry.Left) },
        { "right_foot", new BodyPartInfo(BodyPartType.Foot, BodyPartSymmetry.Right) },
    };

    protected static readonly Dictionary<string, string> OrganSlotMap = new()
    {
        { "brain", "brainSlot" },
        { "eyes", "eyesSlot" },
        { "heart", "heartSlot" },
        { "liver", "liverSlot" },
        { "lungs", "lungsSlot" },
        { "stomach", "stomachSlot" },
        { "kidneys", "kidneysSlot" },
        { "appendix", "appendixSlot" },
        { "tongue", "tongueSlot" },
        { "ears", "earsSlot" }
    };

    protected static readonly Dictionary<string, string> OrganSlotIdMap = new()
    {
        { "brain", "brain" },
        { "heart", "heart" },
        { "liver", "liver" },
        { "lungs", "lungs" },
        { "stomach", "stomach" },
        { "eyes", "eyes" },
    };

    protected static string? GetSlotForBodyPart(BodyPartType partType, BodyPartSymmetry symmetry)
    {
        return partType switch
        {
            BodyPartType.Head => "headSlot",
            BodyPartType.Torso => "torsoSlot",
            BodyPartType.Arm => symmetry == BodyPartSymmetry.Left ? "leftArmSlot" : "rightArmSlot",
            BodyPartType.Hand => symmetry == BodyPartSymmetry.Left ? "leftHandSlot" : "rightHandSlot",
            BodyPartType.Leg => symmetry == BodyPartSymmetry.Left ? "leftLegSlot" : "rightLegSlot",
            BodyPartType.Foot => symmetry == BodyPartSymmetry.Left ? "leftFootSlot" : "rightFootSlot",
            _ => null
        };
    }

    public virtual bool HasAvailableOrganInAutodoc(string organType, EntityUid autodocUid) => false;
    public virtual bool HasAvailablePartInAutodoc(BodyPartType partType, BodyPartSymmetry? symmetry, EntityUid autodocUid) => false;
    public virtual EntityUid? GetAvailableOrganInAutodoc(string organType, EntityUid autodocUid) => null;
    public virtual EntityUid? GetAvailablePartInAutodoc(BodyPartType partType, BodyPartSymmetry? symmetry, EntityUid autodocUid) => null;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SiriusAutodocSurgeryComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<SiriusAutodocSurgeryComponent, ComponentStartup>(OnComponentStartup);
        SubscribeLocalEvent<SiriusAutodocComponent, ItemSlotInsertAttemptEvent>(OnItemSlotInsertAttempt);
    }

    private void OnComponentInit(EntityUid uid, SiriusAutodocSurgeryComponent component, ComponentInit args)
    {
        _sawmill.Info($"OnComponentInit for {uid}");

        if (TryComp<SiriusAutodocComponent>(uid, out var autodoc))
        {
            component.CurrentPatient = autodoc.CurrentPatient;
        }
        UpdateAvailableParts(uid, component);
    }

    private void OnComponentStartup(EntityUid uid, SiriusAutodocSurgeryComponent component, ComponentStartup args)
    {
        _sawmill.Info($"OnComponentStartup for {uid}");

        if (TryComp<SiriusAutodocComponent>(uid, out var autodoc))
        {
            component.CurrentPatient = autodoc.CurrentPatient;
        }
        UpdateAvailableParts(uid, component);
    }
    private void OnItemInserted(EntityUid uid, SiriusAutodocComponent component, EntInsertedIntoContainerMessage args)
    {
        var slotId = args.Container.ID;
        if (IsOrganOrPartSlot(slotId))
        {
            _sawmill.Info($"Item inserted into slot {slotId}: {args.Entity}");
            if (TryComp<SiriusAutodocSurgeryComponent>(uid, out var surgeryComp))
            {
                UpdateAvailableParts(uid, surgeryComp);
            }
        }
    }

    private void OnItemRemoved(EntityUid uid, SiriusAutodocComponent component, EntRemovedFromContainerMessage args)
    {
        var slotId = args.Container.ID;
        if (IsOrganOrPartSlot(slotId))
        {
            _sawmill.Info($"Item removed from slot {slotId}: {args.Entity}");
            if (TryComp<SiriusAutodocSurgeryComponent>(uid, out var surgeryComp))
            {
                UpdateAvailableParts(uid, surgeryComp);
            }
        }
    }
    private bool IsOrganOrPartSlot(string slotId)
    {
        var organSlots = new[]
        {
        SiriusAutodocComponent.BrainSlotId,
        SiriusAutodocComponent.EyesSlotId,
        SiriusAutodocComponent.HeartSlotId,
        SiriusAutodocComponent.LiverSlotId,
        SiriusAutodocComponent.LungsSlotId,
        SiriusAutodocComponent.StomachSlotId,
        SiriusAutodocComponent.KidneysSlotId,
        SiriusAutodocComponent.AppendixSlotId,
        SiriusAutodocComponent.TongueSlotId,
        SiriusAutodocComponent.EarsSlotId
    };

        var partSlots = new[]
        {
        SiriusAutodocComponent.LeftArmSlotId,
        SiriusAutodocComponent.RightArmSlotId,
        SiriusAutodocComponent.LeftHandSlotId,
        SiriusAutodocComponent.RightHandSlotId,
        SiriusAutodocComponent.LeftLegSlotId,
        SiriusAutodocComponent.RightLegSlotId,
        SiriusAutodocComponent.LeftFootSlotId,
        SiriusAutodocComponent.RightFootSlotId,
        SiriusAutodocComponent.HeadSlotId,
        SiriusAutodocComponent.TorsoSlotId
    };

        return organSlots.Contains(slotId) || partSlots.Contains(slotId);
    }
    private void OnItemSlotInsertAttempt(Entity<SiriusAutodocComponent> entity, ref ItemSlotInsertAttemptEvent args)
    {
        var slotId = args.Slot.ID;

        if (slotId == SiriusAutodocComponent.SiriusBeakerSlotId)
            return;

        if (TryComp<OrganComponent>(args.Item, out var organComp))
        {
            var organSlotId = organComp.SlotId?.ToLowerInvariant();

            if (string.IsNullOrEmpty(organSlotId))
            {
                args.Cancelled = true;
                return;
            }

            if (!OrganSlotMap.TryGetValue(organSlotId, out var correctSlot))
            {
                args.Cancelled = true;
                return;
            }

            if (correctSlot != slotId)
            {
                args.Cancelled = true;
                if (args.User != null)
                {
                    _popupSystem.PopupClient(
                        Loc.GetString("autodoc-wrong-slot", ("organ", organSlotId)),
                        entity,
                        args.User.Value);
                }
                return;
            }
        }
        else if (TryComp<BodyPartComponent>(args.Item, out var bodyPartComp))
        {
            var partType = bodyPartComp.PartType;
            var symmetry = bodyPartComp.Symmetry;

            var correctSlot = GetSlotForBodyPart(partType, symmetry);

            if (correctSlot == null || correctSlot != slotId)
            {
                args.Cancelled = true;
                if (args.User != null)
                {
                    _popupSystem.PopupClient(
                        Loc.GetString("autodoc-wrong-slot-part", ("part", partType.ToString())),
                        entity,
                        args.User.Value);
                }
                return;
            }
        }
    }

    public virtual void UpdateAvailableParts(EntityUid uid, SiriusAutodocSurgeryComponent component)
    {
        _sawmill.Info($"=== UpdateAvailableParts START for {uid} ===");

        component.AvailableParts.Clear();
        component.AvailableOrgans.Clear();

        if (!TryComp<SiriusAutodocComponent>(uid, out var autodoc))
        {
            _sawmill.Warning("No SiriusAutodocComponent found");
            return;
        }

        _sawmill.Info($"Checking all ItemSlots for {uid}:");

        var organSlots = new[]
        {
        SiriusAutodocComponent.BrainSlotId,
        SiriusAutodocComponent.EyesSlotId,
        SiriusAutodocComponent.HeartSlotId,
        SiriusAutodocComponent.LiverSlotId,
        SiriusAutodocComponent.LungsSlotId,
        SiriusAutodocComponent.StomachSlotId,
        SiriusAutodocComponent.KidneysSlotId,
        SiriusAutodocComponent.AppendixSlotId,
        SiriusAutodocComponent.TongueSlotId,
        SiriusAutodocComponent.EarsSlotId
    };

        var partSlots = new[]
        {
        SiriusAutodocComponent.LeftArmSlotId,
        SiriusAutodocComponent.RightArmSlotId,
        SiriusAutodocComponent.LeftHandSlotId,
        SiriusAutodocComponent.RightHandSlotId,
        SiriusAutodocComponent.LeftLegSlotId,
        SiriusAutodocComponent.RightLegSlotId,
        SiriusAutodocComponent.LeftFootSlotId,
        SiriusAutodocComponent.RightFootSlotId,
        SiriusAutodocComponent.HeadSlotId,
        SiriusAutodocComponent.TorsoSlotId
    };

        var allSlots = organSlots.Concat(partSlots).ToArray();
        foreach (var slotId in allSlots)
        {
            var item = _itemSlots.GetItemOrNull(uid, slotId);
            if (item != null)
            {
                _sawmill.Info($"  Slot {slotId} contains: {item.Value}");

                if (TryComp<OrganComponent>(item, out var organComp))
                {
                    _sawmill.Info($"    -> Is Organ: SlotId={organComp.SlotId}, Enabled={organComp.Enabled}, CanEnable={organComp.CanEnable}");
                }
                else if (TryComp<BodyPartComponent>(item, out var partComp))
                {
                    _sawmill.Info($"    -> Is BodyPart: Type={partComp.PartType}, Symmetry={partComp.Symmetry}, Enabled={partComp.Enabled}, CanEnable={partComp.CanEnable}");
                }
                else
                {
                    _sawmill.Info($"    -> Unknown item type (not Organ or BodyPart)");
                }
            }
            else
            {
                _sawmill.Info($"  Slot {slotId} is empty");
            }
        }

        _sawmill.Info("Processing organs...");
        foreach (var slotMapping in OrganSlotMap)
        {
            var slotId = slotMapping.Value;
            var organType = slotMapping.Key;

            var item = _itemSlots.GetItemOrNull(uid, slotId);
            if (item == null)
            {
                _sawmill.Info($"  Slot {slotId} (organ: {organType}) is empty");
                continue;
            }

            _sawmill.Info($"  Slot {slotId} (organ: {organType}) contains: {item.Value}");

            if (TryComp<OrganComponent>(item, out var organComp))
            {
                var organSlotId = organComp.SlotId?.ToLowerInvariant();
                _sawmill.Info($"    OrganComp: SlotId={organSlotId}, Enabled={organComp.Enabled}, CanEnable={organComp.CanEnable}");

                if (!string.IsNullOrEmpty(organSlotId) && SiriusAutodocSurgeryComponent.TransplantableOrgans.Contains(organSlotId))
                {
                    if (organComp.Enabled && organComp.CanEnable)
                    {
                        component.AvailableOrgans[organSlotId] = item.Value;
                        _sawmill.Info($"    ADDED organ: {organSlotId} -> {item.Value}");
                    }
                    else
                    {
                        _sawmill.Info($"    Organ {organSlotId} NOT added: Enabled={organComp.Enabled}, CanEnable={organComp.CanEnable}");
                    }
                }
                else
                {
                    _sawmill.Info($"    Organ {organSlotId} NOT in TransplantableOrgans list");
                }
            }
            else
            {
                _sawmill.Warning($"  Item in slot {slotId} is not an OrganComponent");
            }
        }

        _sawmill.Info("Processing body parts...");
        foreach (var slotId in partSlots)
        {
            var item = _itemSlots.GetItemOrNull(uid, slotId);
            if (item == null)
            {
                _sawmill.Info($"  Slot {slotId} is empty");
                continue;
            }

            _sawmill.Info($"  Slot {slotId} contains: {item.Value}");

            if (TryComp<BodyPartComponent>(item, out var partComp))
            {
                _sawmill.Info($"    PartComp: Type={partComp.PartType}, Symmetry={partComp.Symmetry}, Enabled={partComp.Enabled}, CanEnable={partComp.CanEnable}");

                if (partComp.Enabled && partComp.CanEnable)
                {
                    var key = (partComp.PartType, partComp.Symmetry);
                    component.AvailableParts[key] = item.Value;
                    _sawmill.Info($"    ADDED part: Type={partComp.PartType}, Symmetry={partComp.Symmetry} -> {item.Value}");
                }
                else
                {
                    _sawmill.Info($"    Part NOT added: Enabled={partComp.Enabled}, CanEnable={partComp.CanEnable}");
                }
            }
            else
            {
                _sawmill.Warning($"  Item in slot {slotId} is not a BodyPartComponent");
            }
        }

        _sawmill.Info("Checking legacy part slots...");
        for (int i = 0; i < SiriusAutodocComponent.MaxSlots; i++)
        {
            var slotId = autodoc.PartSlotIds[i];
            if (string.IsNullOrEmpty(slotId))
                continue;

            var item = _itemSlots.GetItemOrNull(uid, slotId);
            if (item == null)
            {
                _sawmill.Info($"  Legacy slot {slotId} is empty");
                continue;
            }

            _sawmill.Info($"  Legacy slot {slotId} contains: {item.Value}");

            if (TryComp<BodyPartComponent>(item, out var partComp))
            {
                _sawmill.Info($"    PartComp: Type={partComp.PartType}, Symmetry={partComp.Symmetry}, Enabled={partComp.Enabled}, CanEnable={partComp.CanEnable}");

                if (partComp.Enabled && partComp.CanEnable)
                {
                    var key = (partComp.PartType, partComp.Symmetry);
                    if (!component.AvailableParts.ContainsKey(key))
                    {
                        component.AvailableParts[key] = item.Value;
                        _sawmill.Info($"    ADDED from legacy slot: Type={partComp.PartType}, Symmetry={partComp.Symmetry} -> {item.Value}");
                    }
                }
            }
        }

        _sawmill.Info($"UpdateAvailableParts END: Organs={component.AvailableOrgans.Count}, Parts={component.AvailableParts.Count}");

        if (component.AvailableOrgans.Count > 0)
        {
            _sawmill.Info("Final organs:");
            foreach (var organ in component.AvailableOrgans)
            {
                _sawmill.Info($"  {organ.Key} -> {organ.Value}");
            }
        }

        if (component.AvailableParts.Count > 0)
        {
            _sawmill.Info("Final parts:");
            foreach (var part in component.AvailableParts)
            {
                _sawmill.Info($"  Type={part.Key.Type}, Symmetry={part.Key.Symmetry} -> {part.Value}");
            }
        }
    }

    public List<AutodocBodyPartData> GetBodyPartsData(EntityUid patient)
    {
        var result = new List<AutodocBodyPartData>();

        if (!patient.IsValid())
        {
            _sawmill.Warning($"GetBodyPartsData: patient {patient} is invalid");
            return result;
        }

        if (!HasComp<BodyComponent>(patient))
        {
            _sawmill.Warning($"GetBodyPartsData: patient {patient} has no BodyComponent");
            return result;
        }

        var bodySystem = _bodySystem;

        foreach (var partMapping in BodyPartMap)
        {
            var partId = partMapping.Key;
            var partInfo = partMapping.Value;
            var displayName = BodyPartDisplayNames.GetValueOrDefault(partId, partId);

            var parts = bodySystem.GetBodyChildrenOfType(patient, partInfo.Type, symmetry: partInfo.Symmetry ?? BodyPartSymmetry.None);
            var isPresent = parts.Any();

            var hasDamage = false;
            if (isPresent)
            {
                var partEntity = parts.First().Id;
                if (TryComp<DamageableComponent>(partEntity, out var damageable))
                {
                    hasDamage = damageable.TotalDamage > 0;
                }
            }

            result.Add(new AutodocBodyPartData(partId, displayName, isPresent, hasDamage));
        }

        _sawmill.Info($"GetBodyPartsData: found {result.Count} parts for patient {patient}");
        return result;
    }

    public virtual List<AutodocOperationData> GetOperationsForPart(EntityUid patient, string partId, EntityUid autodocUid)
    {
        return new List<AutodocOperationData>();
    }

    protected List<(string Id, string DisplayName, bool IsAvailable)> GetOperationsForPartType(string partId)
    {
        var result = new List<(string, string, bool)>();

        switch (partId)
        {
            case "head":
                result.Add(("TendBrute", "Лечить ушибы", true));
                result.Add(("TendBurn", "Лечить ожоги", true));
                result.Add(("ToggleBrain", "Мозг", true));
                result.Add(("ToggleEyes", "Глаза", true));
                break;
            case "torso":
                result.Add(("TendBrute", "Лечить ушибы", true));
                result.Add(("TendBurn", "Лечить ожоги", true));
                result.Add(("ToggleHeart", "Сердце", true));
                result.Add(("ToggleLiver", "Печень", true));
                result.Add(("ToggleLungs", "Лёгкие", true));
                result.Add(("ToggleStomach", "Желудок", true));
                result.Add(("AttachHead", "Пришить голову", true));
                result.Add(("AttachLeftArm", "Пришить левую руку", true));
                result.Add(("AttachRightArm", "Пришить правую руку", true));
                result.Add(("AttachLeftLeg", "Пришить левую ногу", true));
                result.Add(("AttachRightLeg", "Пришить правую ногу", true));
                break;
            case "left_arm":
                result.Add(("TendBrute", "Лечить ушибы", true));
                result.Add(("TendBurn", "Лечить ожоги", true));
                result.Add(("AttachHand", "Пришить левую кисть", true));
                break;
            case "right_arm":
                result.Add(("TendBrute", "Лечить ушибы", true));
                result.Add(("TendBurn", "Лечить ожоги", true));
                result.Add(("AttachHand", "Пришить правую кисть", true));
                break;
            case "left_hand":
                result.Add(("TendBrute", "Лечить ушибы", true));
                result.Add(("TendBurn", "Лечить ожоги", true));
                break;
            case "right_hand":
                result.Add(("TendBrute", "Лечить ушибы", true));
                result.Add(("TendBurn", "Лечить ожоги", true));
                break;
            case "left_leg":
                result.Add(("TendBrute", "Лечить ушибы", true));
                result.Add(("TendBurn", "Лечить ожоги", true));
                result.Add(("AttachFoot", "Пришить левую стопу", true));
                break;
            case "right_leg":
                result.Add(("TendBrute", "Лечить ушибы", true));
                result.Add(("TendBurn", "Лечить ожоги", true));
                result.Add(("AttachFoot", "Пришить правую стопу", true));
                break;
            case "left_foot":
                result.Add(("TendBrute", "Лечить ушибы", true));
                result.Add(("TendBurn", "Лечить ожоги", true));
                break;
            case "right_foot":
                result.Add(("TendBrute", "Лечить ушибы", true));
                result.Add(("TendBurn", "Лечить ожоги", true));
                break;
            default:
                break;
        }

        return result;
    }

    protected bool HasOrganBySlotId(EntityUid patient, string organType, IEntityManager entityManager)
    {
        if (!patient.IsValid())
            return false;

        if (!OrganSlotIdMap.TryGetValue(organType, out var targetSlotId))
            return false;

        var bodySystem = entityManager.System<SharedBodySystem>();
        var parts = bodySystem.GetBodyChildren(patient);

        foreach (var part in parts)
        {
            var organs = bodySystem.GetPartOrgans(part.Id);
            foreach (var organ in organs)
            {
                if (entityManager.TryGetComponent<OrganComponent>(organ.Item1, out var organComp))
                {
                    if (string.Equals(organComp.SlotId, targetSlotId, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
        }

        return false;
    }

    public virtual bool ExecuteSurgeryOperation(EntityUid autodocUid, EntityUid patient, string partId, string operationId)
    {
        return false;
    }

    protected bool ExecuteTendBrute(EntityUid patient, EntityUid partEntity)
    {
        if (!TryComp<DamageableComponent>(patient, out var damageable))
            return false;

        var healSpec = new DamageSpecifier();
        var bruteTypes = new[] { "Blunt", "Slash", "Piercing" };

        foreach (var damageType in bruteTypes)
        {
            if (damageable.Damage.DamageDict.TryGetValue(damageType, out var amount) && amount > 0)
            {
                healSpec.DamageDict[damageType] = -Math.Min(amount.Float(), 15);
            }
        }

        if (!healSpec.Empty)
        {
            _damageable.TryChangeDamage(patient, healSpec, true);
            return true;
        }

        return false;
    }

    protected bool ExecuteTendBurn(EntityUid patient, EntityUid partEntity)
    {
        if (!TryComp<DamageableComponent>(patient, out var damageable))
            return false;

        var healSpec = new DamageSpecifier();
        var burnTypes = new[] { "Heat", "Shock", "Caustic" };

        foreach (var damageType in burnTypes)
        {
            if (damageable.Damage.DamageDict.TryGetValue(damageType, out var amount) && amount > 0)
            {
                healSpec.DamageDict[damageType] = -Math.Min(amount.Float(), 15);
            }
        }

        if (!healSpec.Empty)
        {
            _damageable.TryChangeDamage(patient, healSpec, true);
            return true;
        }

        return false;
    }

    protected bool ExecuteRemoveOrgan(EntityUid autodocUid, EntityUid patient, string organType)
    {
        if (!OrganSlotIdMap.TryGetValue(organType, out var targetSlotId))
            return false;

        var bodySystem = _bodySystem;
        var parts = bodySystem.GetBodyChildren(patient);

        foreach (var part in parts)
        {
            var organs = bodySystem.GetPartOrgans(part.Id);
            foreach (var organ in organs)
            {
                if (EntityManager.TryGetComponent<OrganComponent>(organ.Item1, out var organComp))
                {
                    if (string.Equals(organComp.SlotId, targetSlotId, StringComparison.OrdinalIgnoreCase))
                    {
                        bodySystem.RemoveOrgan(organ.Item1, organComp);
                        _transform.DropNextTo(organ.Item1, autodocUid);
                        return true;
                    }
                }
            }
        }

        return false;
    }

    protected bool ExecuteInsertOrgan(EntityUid autodocUid, EntityUid patient, string organType)
    {
        var organEntity = GetAvailableOrganInAutodoc(organType, autodocUid);
        if (organEntity == null)
            return false;

        if (!TryComp<OrganComponent>(organEntity, out var organComp))
            return false;

        var targetPartType = organType switch
        {
            "brain" or "eyes" => BodyPartType.Head,
            "heart" or "liver" or "lungs" or "stomach" or "kidneys" or "appendix" => BodyPartType.Torso,
            _ => BodyPartType.Torso
        };

        var bodySystem = _bodySystem;
        var targetParts = bodySystem.GetBodyChildrenOfType(patient, targetPartType);

        if (!targetParts.Any())
            return false;

        var targetPart = targetParts.First().Id;
        if (!TryComp<BodyPartComponent>(targetPart, out var partComp))
            return false;

        var slotId = organComp.SlotId;
        if (string.IsNullOrEmpty(slotId))
            return false;

        if (bodySystem.InsertOrgan(targetPart, organEntity.Value, slotId, partComp, organComp))
        {
            if (OrganSlotMap.TryGetValue(organType, out var slotName))
            {
                _itemSlots.TryEject(autodocUid, slotName, null, out _);
            }
            return true;
        }

        return false;
    }

    protected bool ExecuteAttachPart(EntityUid autodocUid, EntityUid patient, string partId)
    {
        if (!BodyPartMap.TryGetValue(partId, out var partInfo))
            return false;

        var partEntity = GetAvailablePartInAutodoc(partInfo.Type, partInfo.Symmetry, autodocUid);
        if (partEntity == null)
            return false;

        if (!TryComp<BodyPartComponent>(partEntity, out var partComp))
            return false;

        var bodySystem = _bodySystem;

        EntityUid? parentPart = null;
        var parentType = partInfo.Type switch
        {
            BodyPartType.Head => BodyPartType.Torso,
            BodyPartType.Arm or BodyPartType.Hand => BodyPartType.Torso,
            BodyPartType.Leg or BodyPartType.Foot => BodyPartType.Torso,
            _ => BodyPartType.Torso
        };

        var parentParts = bodySystem.GetBodyChildrenOfType(patient, parentType);
        if (parentParts.Any())
        {
            parentPart = parentParts.First().Id;
        }

        bool success;
        var slotName = GetSlotForBodyPart(partInfo.Type, partInfo.Symmetry ?? BodyPartSymmetry.None);

        if (parentPart != null && !string.IsNullOrEmpty(slotName))
        {
            success = bodySystem.AttachPart(parentPart.Value, slotName, partEntity.Value, partComp, partComp);
        }
        else
        {
            success = bodySystem.AttachPartToRoot(patient, partEntity.Value);
        }

        if (success)
        {
            if (!string.IsNullOrEmpty(slotName))
            {
                _itemSlots.TryEject(autodocUid, slotName, null, out _);
            }
            return true;
        }

        return false;
    }
}
