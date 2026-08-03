using Content.Shared._Shitmed.Body.Events;
using Content.Shared._Shitmed.Body.Organ;
using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Robust.Shared.Containers;
using Robust.Shared.Timing;
using System.Linq;

namespace Content.Shared._Nuclear14.AutodocSirius;

public abstract class SharedSiriusAutodocSurgerySystem : EntitySystem
{
    [Dependency] protected readonly IGameTiming _gameTiming = default!;
    [Dependency] protected readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] protected readonly SharedBodySystem _bodySystem = default!;
    [Dependency] protected readonly SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] protected readonly SharedTransformSystem _transform = default!;
    public const float SurgeryOperationDuration = 5f;
    protected static readonly Dictionary<string, string> BodyPartDisplayNames = new()
{
    { "head", "autodoc-body-part-head" },
    { "torso", "autodoc-body-part-torso" },
    { "leftarm", "autodoc-body-part-leftarm" },
    { "rightarm", "autodoc-body-part-rightarm" },
    { "lefthand", "autodoc-body-part-lefthand" },
    { "righthand", "autodoc-body-part-righthand" },
    { "leftleg", "autodoc-body-part-leftleg" },
    { "rightleg", "autodoc-body-part-rightleg" },
    { "leftfoot", "autodoc-body-part-leftfoot" },
    { "rightfoot", "autodoc-body-part-rightfoot" },
};
    protected static string? GetAutodocSlotForOrgan(string organType)
    {
        return OrganSlotMap.TryGetValue(organType, out var slot) ? slot : null;
    }
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
        { "head", new BodyPartInfo(BodyPartType.Head, BodyPartSymmetry.None) },
        { "torso", new BodyPartInfo(BodyPartType.Torso, BodyPartSymmetry.None) },
        { "leftarm", new BodyPartInfo(BodyPartType.Arm, BodyPartSymmetry.Left) },
        { "rightarm", new BodyPartInfo(BodyPartType.Arm, BodyPartSymmetry.Right) },
        { "lefthand", new BodyPartInfo(BodyPartType.Hand, BodyPartSymmetry.Left) },
        { "righthand", new BodyPartInfo(BodyPartType.Hand, BodyPartSymmetry.Right) },
        { "leftleg", new BodyPartInfo(BodyPartType.Leg, BodyPartSymmetry.Left) },
        { "rightleg", new BodyPartInfo(BodyPartType.Leg, BodyPartSymmetry.Right) },
        { "leftfoot", new BodyPartInfo(BodyPartType.Foot, BodyPartSymmetry.Left) },
        { "rightfoot", new BodyPartInfo(BodyPartType.Foot, BodyPartSymmetry.Right) },
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
    protected static string? GetAutodocSlotForBodyPart(BodyPartType partType, BodyPartSymmetry symmetry)
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
    protected static string? GetBodyPartSlotForBodyPart(BodyPartType partType, BodyPartSymmetry symmetry)
    {
        return partType switch
        {
            BodyPartType.Head => "head",
            BodyPartType.Torso => "torso",
            BodyPartType.Arm => symmetry == BodyPartSymmetry.Left ? "left arm" : "right arm",
            BodyPartType.Hand => symmetry == BodyPartSymmetry.Left ? "left hand" : "right hand",
            BodyPartType.Leg => symmetry == BodyPartSymmetry.Left ? "left leg" : "right leg",
            BodyPartType.Foot => symmetry == BodyPartSymmetry.Left ? "left foot" : "right foot",
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
    private void OnItemInserted(EntityUid uid, SiriusAutodocComponent component, EntInsertedIntoContainerMessage args)
    {
        var slotId = args.Container.ID;
        if (IsOrganOrPartSlot(slotId))
        {
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
                return;
            }
        }
        else if (TryComp<BodyPartComponent>(args.Item, out var bodyPartComp))
        {
            var partType = bodyPartComp.PartType;
            var symmetry = bodyPartComp.Symmetry;

            var correctSlot = GetAutodocSlotForBodyPart(partType, symmetry);

            if (correctSlot == null || correctSlot != slotId)
            {
                args.Cancelled = true;
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
        foreach (var slotMapping in OrganSlotMap)
        {
            var slotId = slotMapping.Value;
            var organType = slotMapping.Key;

            var item = _itemSlots.GetItemOrNull(uid, slotId);
            if (item == null)
            {
                continue;
            }
            if (TryComp<OrganComponent>(item, out var organComp))
            {
                var organSlotId = organComp.SlotId?.ToLowerInvariant();
                if (!string.IsNullOrEmpty(organSlotId) && SiriusAutodocSurgeryComponent.TransplantableOrgans.Contains(organSlotId))
                {
                    if (organComp.Enabled && organComp.CanEnable)
                    {
                        component.AvailableOrgans[organSlotId] = item.Value;
                    }
                    else
                    {
                        if (organComp.CanEnable && !organComp.Enabled)
                        {
                            var enableEvent = new OrganEnableChangedEvent(true);
                            RaiseLocalEvent(item.Value, ref enableEvent);
                            if (TryComp<OrganComponent>(item, out var updatedOrganComp) &&
                                updatedOrganComp.Enabled && updatedOrganComp.CanEnable)
                            {
                                component.AvailableOrgans[organSlotId] = item.Value;
                            }
                        }
                    }
                }
            }
        }
        foreach (var slotId in partSlots)
        {
            var item = _itemSlots.GetItemOrNull(uid, slotId);
            if (item == null)
            {
                continue;
            }
            if (TryComp<BodyPartComponent>(item, out var partComp))
            {
                if (partComp.Enabled && partComp.CanEnable)
                {
                    var key = (partComp.PartType, partComp.Symmetry);
                    component.AvailableParts[key] = item.Value;
                }
                else
                {
                    if (partComp.CanEnable && !partComp.Enabled)
                    {
                        var enableEvent = new BodyPartEnableChangedEvent(true);
                        RaiseLocalEvent(item.Value, ref enableEvent);

                        if (TryComp<BodyPartComponent>(item, out var updatedPartComp) &&
                            updatedPartComp.Enabled && updatedPartComp.CanEnable)
                        {
                            var key = (updatedPartComp.PartType, updatedPartComp.Symmetry);
                            component.AvailableParts[key] = item.Value;
                        }
                    }
                }
            }
        }
        for (int i = 0; i < SiriusAutodocComponent.MaxSlots; i++)
        {
            var slotId = autodoc.PartSlotIds[i];
            if (string.IsNullOrEmpty(slotId))
                continue;
            var item = _itemSlots.GetItemOrNull(uid, slotId);
            if (item == null)
                continue;
            if (TryComp<BodyPartComponent>(item, out var partComp))
            {
                if (partComp.Enabled && partComp.CanEnable)
                {
                    var key = (partComp.PartType, partComp.Symmetry);
                    if (!component.AvailableParts.ContainsKey(key))
                    {
                        component.AvailableParts[key] = item.Value;
                    }
                }
            }
        }
    }
    public List<AutodocBodyPartData> GetBodyPartsData(EntityUid patient)
    {
        var result = new List<AutodocBodyPartData>();
        if (!patient.IsValid())
            return result;
        if (!HasComp<BodyComponent>(patient))
            return result;
        var bodySystem = _bodySystem;
        foreach (var partMapping in BodyPartMap)
        {
            var partId = partMapping.Key;
            var partInfo = partMapping.Value;
            var displayNameKey = BodyPartDisplayNames.GetValueOrDefault(partId, partId);
            var displayName = Loc.GetString(displayNameKey);
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
        return result;
    }
    public virtual List<AutodocOperationData> GetOperationsForPart(EntityUid patient, string partId, EntityUid autodocUid)
    {
        return new List<AutodocOperationData>();
    }
    protected List<(string Id, string DisplayNameKey, bool IsAvailable)> GetOperationsForPartType(string partId)
    {
        var result = new List<(string, string, bool)>();
        switch (partId)
        {
            case "head":
                result.Add(("TendBrute", "autodoc-surgery-op-tend-brute", true));
                result.Add(("TendBurn", "autodoc-surgery-op-tend-burn", true));
                result.Add(("ToggleBrain", "autodoc-surgery-op-toggle-brain", true));
                result.Add(("ToggleEyes", "autodoc-surgery-op-toggle-eyes", true));
                break;
            case "torso":
                result.Add(("TendBrute", "autodoc-surgery-op-tend-brute", true));
                result.Add(("TendBurn", "autodoc-surgery-op-tend-burn", true));
                result.Add(("ToggleHeart", "autodoc-surgery-op-toggle-heart", true));
                result.Add(("ToggleLiver", "autodoc-surgery-op-toggle-liver", true));
                result.Add(("ToggleLungs", "autodoc-surgery-op-toggle-lungs", true));
                result.Add(("ToggleStomach", "autodoc-surgery-op-toggle-stomach", true));
                result.Add(("ToggleHead", "autodoc-surgery-op-toggle-head", true));
                result.Add(("ToggleLeftArm", "autodoc-surgery-op-toggle-leftarm", true));
                result.Add(("ToggleRightArm", "autodoc-surgery-op-toggle-rightarm", true));
                result.Add(("ToggleLeftLeg", "autodoc-surgery-op-toggle-leftleg", true));
                result.Add(("ToggleRightLeg", "autodoc-surgery-op-toggle-rightleg", true));
                break;
            case "leftarm":
                result.Add(("TendBrute", "autodoc-surgery-op-tend-brute", true));
                result.Add(("TendBurn", "autodoc-surgery-op-tend-burn", true));
                result.Add(("ToggleLeftHand", "autodoc-surgery-op-toggle-lefthand", true));
                break;
            case "rightarm":
                result.Add(("TendBrute", "autodoc-surgery-op-tend-brute", true));
                result.Add(("TendBurn", "autodoc-surgery-op-tend-burn", true));
                result.Add(("ToggleRightHand", "autodoc-surgery-op-toggle-righthand", true));
                break;
            case "lefthand":
                result.Add(("TendBrute", "autodoc-surgery-op-tend-brute", true));
                result.Add(("TendBurn", "autodoc-surgery-op-tend-burn", true));
                break;
            case "righthand":
                result.Add(("TendBrute", "autodoc-surgery-op-tend-brute", true));
                result.Add(("TendBurn", "autodoc-surgery-op-tend-burn", true));
                break;
            case "leftleg":
                result.Add(("TendBrute", "autodoc-surgery-op-tend-brute", true));
                result.Add(("TendBurn", "autodoc-surgery-op-tend-burn", true));
                result.Add(("ToggleLeftFoot", "autodoc-surgery-op-toggle-leftfoot", true));
                break;
            case "rightleg":
                result.Add(("TendBrute", "autodoc-surgery-op-tend-brute", true));
                result.Add(("TendBurn", "autodoc-surgery-op-tend-burn", true));
                result.Add(("ToggleRightFoot", "autodoc-surgery-op-toggle-rightfoot", true));
                break;
            case "leftfoot":
                result.Add(("TendBrute", "autodoc-surgery-op-tend-brute", true));
                result.Add(("TendBurn", "autodoc-surgery-op-tend-burn", true));
                break;
            case "rightfoot":
                result.Add(("TendBrute", "autodoc-surgery-op-tend-brute", true));
                result.Add(("TendBurn", "autodoc-surgery-op-tend-burn", true));
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
                        var autodocSlotName = GetAutodocSlotForOrgan(organType);
                        if (!string.IsNullOrEmpty(autodocSlotName))
                        {
                            var inserted = _itemSlots.TryInsert(autodocUid, autodocSlotName, organ.Item1, null);
                            if (!inserted)
                            {
                                _transform.DropNextTo(organ.Item1, autodocUid);
                            }
                        }
                        else
                        {
                            _transform.DropNextTo(organ.Item1, autodocUid);
                        }
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
        var slotName = GetBodyPartSlotForBodyPart(partInfo.Type, partInfo.Symmetry ?? BodyPartSymmetry.None);

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
            var autodocSlotName = GetAutodocSlotForBodyPart(partInfo.Type, partInfo.Symmetry ?? BodyPartSymmetry.None);
            if (!string.IsNullOrEmpty(autodocSlotName))
            {
                _itemSlots.TryEject(autodocUid, autodocSlotName, null, out _);
            }
            return true;
        }
        return false;
    }
}
