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

    private static readonly Dictionary<string, string> OrganSlotMap = new()
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
    private static readonly Dictionary<string, string> OrganSlotIdMap = new()
    {
        { "brain", "brain" },
        { "heart", "heart" },
        { "liver", "liver" },
        { "lungs", "lungs" },
        { "stomach", "stomach" },
        { "eyes", "eyes" },
    };

    private static string? GetSlotForBodyPart(BodyPartType partType, BodyPartSymmetry symmetry)
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

    public virtual bool HasAvailableOrganInAutodoc(string organType) => false;
    public virtual bool HasAvailablePartInAutodoc(BodyPartType partType, BodyPartSymmetry? symmetry) => false;
    public virtual EntityUid? GetAvailableOrganInAutodoc(string organType) => null;
    public virtual EntityUid? GetAvailablePartInAutodoc(BodyPartType partType, BodyPartSymmetry? symmetry) => null;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SiriusAutodocSurgeryComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<SiriusAutodocSurgeryComponent, ComponentStartup>(OnComponentStartup);
        SubscribeLocalEvent<SiriusAutodocComponent, ItemSlotInsertAttemptEvent>(OnItemSlotInsertAttempt);
    }

    private void OnComponentInit(EntityUid uid, SiriusAutodocSurgeryComponent component, ComponentInit args)
    {
        if (TryComp<SiriusAutodocComponent>(uid, out var autodoc))
        {
            component.CurrentPatient = autodoc.CurrentPatient;
        }
        UpdateAvailableParts(uid, component);
    }

    private void OnComponentStartup(EntityUid uid, SiriusAutodocSurgeryComponent component, ComponentStartup args)
    {
        if (TryComp<SiriusAutodocComponent>(uid, out var autodoc))
        {
            component.CurrentPatient = autodoc.CurrentPatient;
        }
        UpdateAvailableParts(uid, component);
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
        component.AvailableParts.Clear();
        component.AvailableOrgans.Clear();

        if (!TryComp<SiriusAutodocComponent>(uid, out var autodoc))
            return;

        foreach (var slotMapping in OrganSlotMap)
        {
            var item = _itemSlots.GetItemOrNull(uid, slotMapping.Value);
            if (item == null)
                continue;

            if (TryComp<OrganComponent>(item, out var organComp))
            {
                var organType = organComp.SlotId;
                if (SiriusAutodocSurgeryComponent.TransplantableOrgans.Contains(organType))
                {
                    if (organComp.Enabled && organComp.CanEnable)
                    {
                        component.AvailableOrgans[organType] = item.Value;
                    }
                }
            }
        }

        var bodyPartSlots = new[]
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

        foreach (var slotId in bodyPartSlots)
        {
            var item = _itemSlots.GetItemOrNull(uid, slotId);
            if (item == null)
                continue;

            if (TryComp<BodyPartComponent>(item, out var partComp))
            {
                if (partComp.Enabled && partComp.CanEnable)
                {
                    component.AvailableParts[partComp.PartType] = item.Value;
                }
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

    public List<AutodocOperationData> GetOperationsForPart(EntityUid patient, string partId, EntityUid autodocUid)
    {
        var result = new List<AutodocOperationData>();

        if (!patient.IsValid() || string.IsNullOrEmpty(partId))
            return result;

        var entityManager = EntityManager;

        var operations = GetOperationsForPartType(partId);

        foreach (var (opId, displayName, isAvailable) in operations)
        {
            bool finalIsAvailable = isAvailable;

            if (opId.StartsWith("Remove"))
            {
                var organType = opId.Replace("Remove", "").ToLowerInvariant();
                var hasOrgan = HasOrganBySlotId(patient, organType, entityManager);
                finalIsAvailable = hasOrgan;
            }
            else if (opId.StartsWith("Insert"))
            {
                var organType = opId.Replace("Insert", "").ToLowerInvariant();
                var hasOrgan = HasOrganBySlotId(patient, organType, entityManager);
                var hasOrganInAutodoc = HasAvailableOrganInAutodoc(organType);
                finalIsAvailable = !hasOrgan && hasOrganInAutodoc;
            }
            else if (opId == "AttachPart")
            {
                if (BodyPartMap.TryGetValue(partId, out var partInfo))
                {
                    var hasPartInAutodoc = HasAvailablePartInAutodoc(partInfo.Type, partInfo.Symmetry);
                    finalIsAvailable = isAvailable && hasPartInAutodoc;
                }
            }

            string? tooltip = null;
            if (!finalIsAvailable)
            {
                if (opId == "AttachPart")
                {
                    tooltip = Loc.GetString("autodoc-surgery-no-part-in-autodoc");
                }
                else if (opId.StartsWith("Insert"))
                {
                    tooltip = Loc.GetString("autodoc-surgery-no-organ-in-autodoc");
                }
                else if (opId.StartsWith("Remove"))
                {
                    tooltip = Loc.GetString("autodoc-surgery-organ-not-present");
                }
                else if (opId == "TendBrute")
                {
                    tooltip = Loc.GetString("autodoc-surgery-no-brute-damage");
                }
                else if (opId == "TendBurn")
                {
                    tooltip = Loc.GetString("autodoc-surgery-no-burn-damage");
                }
            }

            result.Add(new AutodocOperationData(opId, displayName, finalIsAvailable, tooltip));
        }

        return result;
    }

    private bool HasOrganBySlotId(EntityUid patient, string organType, IEntityManager entityManager)
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

    private List<(string Id, string DisplayName, bool IsAvailable)> GetOperationsForPartType(string partId)
    {
        var result = new List<(string, string, bool)>();

        switch (partId)
        {
            case "head":
                result.Add(("TendBrute", "Лечить ушибы", true));
                result.Add(("TendBurn", "Лечить ожоги", true));
                result.Add(("RemoveBrain", "Удалить мозг", true));
                result.Add(("InsertBrain", "Вставить мозг", true));
                break;
            case "torso":
                result.Add(("TendBrute", "Лечить ушибы", true));
                result.Add(("TendBurn", "Лечить ожоги", true));
                result.Add(("RemoveHeart", "Удалить сердце", true));
                result.Add(("InsertHeart", "Вставить сердце", true));
                result.Add(("RemoveLiver", "Удалить печень", true));
                result.Add(("InsertLiver", "Вставить печень", true));
                result.Add(("RemoveLungs", "Удалить лёгкие", true));
                result.Add(("InsertLungs", "Вставить лёгкие", true));
                result.Add(("RemoveStomach", "Удалить желудок", true));
                result.Add(("InsertStomach", "Вставить желудок", true));
                break;
            case "left_arm":
            case "right_arm":
                result.Add(("TendBrute", "Лечить ушибы", true));
                result.Add(("TendBurn", "Лечить ожоги", true));
                result.Add(("AttachPart", "Пришить руку", true));
                break;
            case "left_hand":
            case "right_hand":
                result.Add(("TendBrute", "Лечить ушибы", true));
                result.Add(("TendBurn", "Лечить ожоги", true));
                result.Add(("AttachPart", "Пришить кисть", true));
                break;
            case "left_leg":
            case "right_leg":
                result.Add(("TendBrute", "Лечить ушибы", true));
                result.Add(("TendBurn", "Лечить ожоги", true));
                result.Add(("AttachPart", "Пришить ногу", true));
                break;
            case "left_foot":
            case "right_foot":
                result.Add(("TendBrute", "Лечить ушибы", true));
                result.Add(("TendBurn", "Лечить ожоги", true));
                result.Add(("AttachPart", "Пришить стопу", true));
                break;
            default:
                break;
        }

        return result;
    }

    public virtual bool ExecuteSurgeryOperation(EntityUid autodocUid, EntityUid patient, string partId, string operationId)
    {
        _sawmill.Info($"ExecuteSurgeryOperation: partId={partId}, operationId={operationId}, patient={patient}");

        if (!BodyPartMap.TryGetValue(partId, out var partInfo))
        {
            _sawmill.Info($"BodyPartMap doesn't contain {partId}");
            return false;
        }

        var bodySystem = _bodySystem;
        var parts = bodySystem.GetBodyChildrenOfType(patient, partInfo.Type, symmetry: partInfo.Symmetry ?? BodyPartSymmetry.None);
        var partEntity = parts.FirstOrDefault().Id;

        if (partEntity == default)
        {
            _sawmill.Info($"No part found for {partId}");
            return false;
        }

        _sawmill.Info($"Found part entity: {partEntity}");

        bool result = operationId switch
        {
            "TendBrute" => ExecuteTendBrute(patient, partEntity),
            "TendBurn" => ExecuteTendBurn(patient, partEntity),
            "RemoveBrain" => ExecuteRemoveOrgan(autodocUid, patient, "brain"),
            "InsertBrain" => ExecuteInsertOrgan(autodocUid, patient, "brain"),
            "RemoveHeart" => ExecuteRemoveOrgan(autodocUid, patient, "heart"),
            "InsertHeart" => ExecuteInsertOrgan(autodocUid, patient, "heart"),
            "RemoveLiver" => ExecuteRemoveOrgan(autodocUid, patient, "liver"),
            "InsertLiver" => ExecuteInsertOrgan(autodocUid, patient, "liver"),
            "RemoveLungs" => ExecuteRemoveOrgan(autodocUid, patient, "lungs"),
            "InsertLungs" => ExecuteInsertOrgan(autodocUid, patient, "lungs"),
            "RemoveStomach" => ExecuteRemoveOrgan(autodocUid, patient, "stomach"),
            "InsertStomach" => ExecuteInsertOrgan(autodocUid, patient, "stomach"),
            "AttachPart" => ExecuteAttachPart(autodocUid, patient, partId),
            _ => false
        };

        _sawmill.Info($"ExecuteSurgeryOperation result: {result}");
        return result;
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
        _sawmill.Info($"ExecuteRemoveOrgan: organType={organType}, patient={patient}");

        if (!OrganSlotIdMap.TryGetValue(organType, out var targetSlotId))
        {
            _sawmill.Info($"OrganSlotIdMap doesn't contain {organType}");
            return false;
        }

        _sawmill.Info($"Looking for organ with SlotId: {targetSlotId}");

        var bodySystem = _bodySystem;
        var parts = bodySystem.GetBodyChildren(patient);
        _sawmill.Info($"Found {parts.Count()} body parts");

        foreach (var part in parts)
        {
            var organs = bodySystem.GetPartOrgans(part.Id);
            _sawmill.Info($"Part {part.Id} has {organs.Count()} organs");

            foreach (var organ in organs)
            {
                if (EntityManager.TryGetComponent<OrganComponent>(organ.Item1, out var organComp))
                {
                    _sawmill.Info($"Found organ with SlotId: '{organComp.SlotId}' (looking for '{targetSlotId}')");

                    if (string.Equals(organComp.SlotId, targetSlotId, StringComparison.OrdinalIgnoreCase))
                    {
                        bodySystem.RemoveOrgan(organ.Item1, organComp);
                        _sawmill.Info($"Removed organ {organ.Item1} from body");
                        _transform.DropNextTo(organ.Item1, autodocUid);
                        _sawmill.Info($"Dropped organ next to autodoc");

                        return true;
                    }
                }
            }
        }

        _sawmill.Info($"No organ found with SlotId: {targetSlotId}");
        return false;
    }

    protected bool ExecuteInsertOrgan(EntityUid autodocUid, EntityUid patient, string organType)
    {
        var organEntity = GetAvailableOrganInAutodoc(organType);
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

        var partEntity = GetAvailablePartInAutodoc(partInfo.Type, partInfo.Symmetry);
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

[Serializable, NetSerializable]
public sealed partial class SiriusAutodocSurgeryDoAfterEvent : SimpleDoAfterEvent
{
}

[Serializable, NetSerializable]
public sealed partial class SiriusAutodocSurgeryAllDoAfterEvent : SimpleDoAfterEvent
{
}
