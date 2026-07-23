using Content.Shared._Nuclear14.AutodocSirius;
using Content.Shared._Shitmed.Body.Organ;
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

    public Dictionary<BodyPartType, bool> GetMissingParts(EntityUid patient)
    {
        var missing = new Dictionary<BodyPartType, bool>();

        var bodyStatus = _bodySystem.GetBodyPartStatus(patient);

        foreach (var part in SiriusAutodocSurgeryComponent.PartDependencies.Keys)
        {
            if (part == BodyPartType.Other)
                continue;

            var targetPart = _bodySystem.GetTargetBodyPart(part, BodyPartSymmetry.None);
            if (targetPart == null)
            {
                if (part == BodyPartType.Arm || part == BodyPartType.Hand ||
                    part == BodyPartType.Leg || part == BodyPartType.Foot)
                {
                    var left = _bodySystem.GetTargetBodyPart(part, BodyPartSymmetry.Left);
                    var right = _bodySystem.GetTargetBodyPart(part, BodyPartSymmetry.Right);

                    if (left != null)
                    {
                        var leftStatus = bodyStatus.GetValueOrDefault(left.Value, TargetIntegrity.Severed);
                        if (leftStatus == TargetIntegrity.Severed)
                            missing[part] = true;
                    }
                    if (right != null)
                    {
                        var rightStatus = bodyStatus.GetValueOrDefault(right.Value, TargetIntegrity.Severed);
                        if (rightStatus == TargetIntegrity.Severed)
                            missing[part] = true;
                    }
                }
                continue;
            }

            var status = bodyStatus.GetValueOrDefault(targetPart.Value, TargetIntegrity.Severed);
            if (status == TargetIntegrity.Severed)
                missing[part] = true;
        }

        return missing;
    }

    public List<string> GetRequiredSurgeries(Dictionary<BodyPartType, bool> missingParts, SiriusAutodocSurgeryComponent component)
    {
        var surgeries = new List<string>();

        foreach (var kvp in missingParts.OrderBy(x => GetPartPriority(x.Key)))
        {
            var part = kvp.Key;
            var isMissing = kvp.Value;

            if (!isMissing)
                continue;

            if (component.AvailableParts.ContainsKey(part))
            {
                var deps = SiriusAutodocSurgeryComponent.PartDependencies
                    .FirstOrDefault(x => x.Value.Contains(part));

                if (deps.Key != BodyPartType.Other)
                {
                    if (!missingParts.ContainsKey(deps.Key) || !missingParts[deps.Key])
                        surgeries.Add(part.ToString());
                }
                else
                {
                    surgeries.Add(part.ToString());
                }
            }
        }

        foreach (var organType in SiriusAutodocSurgeryComponent.TransplantableOrgans)
        {
            if (component.AvailableOrgans.ContainsKey(organType))
            {
                if (!HasOrgan(component.CurrentPatient ?? EntityUid.Invalid, organType))
                    surgeries.Add(organType);
            }
        }

        return surgeries;
    }

    public bool HasRequiredPart(string surgery, SiriusAutodocSurgeryComponent component)
    {
        if (Enum.TryParse<BodyPartType>(surgery, true, out var partType))
        {
            return component.AvailableParts.ContainsKey(partType);
        }
        else if (SiriusAutodocSurgeryComponent.TransplantableOrgans.Contains(surgery))
        {
            return component.AvailableOrgans.ContainsKey(surgery);
        }
        return false;
    }

    private bool HasOrgan(EntityUid patient, string organType)
    {
        if (patient == EntityUid.Invalid)
            return false;

        var parts = _bodySystem.GetBodyChildren(patient);
        foreach (var part in parts)
        {
            var organs = _bodySystem.GetPartOrgans(part.Id);
            foreach (var organ in organs)
            {
                if (organ.Item2.SlotId == organType)
                    return true;
            }
        }
        return false;
    }

    private int GetPartPriority(BodyPartType partType)
    {
        return partType switch
        {
            BodyPartType.Torso => 0,
            BodyPartType.Head => 1,
            BodyPartType.Arm => 2,
            BodyPartType.Leg => 2,
            BodyPartType.Hand => 3,
            BodyPartType.Foot => 3,
            _ => 4
        };
    }

    private bool CanAttachPart(EntityUid patient, BodyPartType partType, out EntityUid? parentPart)
    {
        parentPart = null;

        foreach (var kvp in SiriusAutodocSurgeryComponent.PartDependencies)
        {
            var parent = kvp.Key;
            var children = kvp.Value;

            if (children.Contains(partType))
            {
                var parentParts = _bodySystem.GetBodyChildrenOfType(patient, parent);
                if (parentParts.Any())
                {
                    parentPart = parentParts.First().Id;
                    return true;
                }
                return false;
            }
        }

        if (partType == BodyPartType.Head)
        {
            var torsoParts = _bodySystem.GetBodyChildrenOfType(patient, BodyPartType.Torso);
            if (torsoParts.Any())
            {
                parentPart = torsoParts.First().Id;
                return true;
            }
            return false;
        }

        if (partType == BodyPartType.Torso)
            return true;

        return false;
    }

    private bool TryAttachPart(EntityUid patient, EntityUid part, EntityUid? parentPart)
    {
        if (!TryComp<BodyPartComponent>(part, out var partComp))
            return false;

        if (parentPart != null && TryComp<BodyPartComponent>(parentPart.Value, out var parentPartComp))
        {
            var slotName = _bodySystem.GetSlotFromBodyPart(partComp);
            return _bodySystem.AttachPart(parentPart.Value, slotName, part, parentPartComp, partComp);
        }

        return _bodySystem.AttachPartToRoot(patient, part);
    }

    public virtual void PerformPartSurgery(EntityUid uid, EntityUid patient, BodyPartType partType, EntityUid user, SiriusAutodocSurgeryComponent component)
    {
        if (!component.AvailableParts.TryGetValue(partType, out var part))
        {
            _popupSystem.PopupEntity(Loc.GetString("autodoc-surgery-part-not-available"), uid, user);
            return;
        }

        if (!CanAttachPart(patient, partType, out var parentPart))
        {
            var dep = SiriusAutodocSurgeryComponent.PartDependencies
                .FirstOrDefault(x => x.Value.Contains(partType));
            if (dep.Key != BodyPartType.Other)
            {
                _popupSystem.PopupEntity(
                    Loc.GetString("autodoc-surgery-missing-parent", ("parent", dep.Key.ToString())),
                    uid, user);
            }
            return;
        }

        string? slotId = null;
        if (TryComp<BodyPartComponent>(part, out var partComp))
        {
            slotId = GetSlotForBodyPart(partComp.PartType, partComp.Symmetry);
        }

        if (slotId != null)
        {
            _itemSlots.TryEject(uid, slotId, user, out _);
        }

        if (TryAttachPart(patient, part, parentPart))
        {
            _popupSystem.PopupEntity(Loc.GetString("autodoc-surgery-part-attached", ("part", partType.ToString())), uid, user);

            var healSpec = new DamageSpecifier();
            if (TryComp<DamageableComponent>(patient, out var damageable))
            {
                foreach (var damage in damageable.Damage.DamageDict)
                {
                    if (damage.Value > 0)
                    {
                        var damageValue = damage.Value.Float();
                        healSpec.DamageDict[damage.Key] = -Math.Min(damageValue, 10);
                    }
                }
            }
            if (!healSpec.Empty)
                _damageable.TryChangeDamage(patient, healSpec, true);
        }
        else
        {
            _popupSystem.PopupEntity(Loc.GetString("autodoc-surgery-attach-failed"), uid, user);
        }

        UpdateAvailableParts(uid, component);
    }

    public virtual void PerformOrganSurgery(EntityUid uid, EntityUid patient, string organType, EntityUid user, SiriusAutodocSurgeryComponent component)
    {
        if (!component.AvailableOrgans.TryGetValue(organType, out var organ))
        {
            _popupSystem.PopupEntity(Loc.GetString("autodoc-surgery-organ-not-available"), uid, user);
            return;
        }

        var targetPart = FindOrganTargetPart(patient, organType);
        if (targetPart == null)
        {
            _popupSystem.PopupEntity(Loc.GetString("autodoc-surgery-no-organ-slot", ("organ", organType)), uid, user);
            return;
        }

        string? slotId = null;
        if (TryComp<OrganComponent>(organ, out var organComp))
        {
            var organSlotId = organComp.SlotId?.ToLowerInvariant();
            if (!string.IsNullOrEmpty(organSlotId))
            {
                OrganSlotMap.TryGetValue(organSlotId, out slotId);
            }
        }

        if (slotId != null)
        {
            _itemSlots.TryEject(uid, slotId, user, out _);
        }

        if (!TryComp<OrganComponent>(organ, out var organComp2))
        {
            _popupSystem.PopupEntity(Loc.GetString("autodoc-surgery-attach-failed"), uid, user);
            UpdateAvailableParts(uid, component);
            return;
        }

        if (!TryComp<BodyPartComponent>(targetPart, out var partComp))
        {
            _popupSystem.PopupEntity(Loc.GetString("autodoc-surgery-attach-failed"), uid, user);
            UpdateAvailableParts(uid, component);
            return;
        }

        if (string.IsNullOrEmpty(organComp2.SlotId))
        {
            _popupSystem.PopupEntity(Loc.GetString("autodoc-surgery-attach-failed"), uid, user);
            UpdateAvailableParts(uid, component);
            return;
        }

        if (_bodySystem.InsertOrgan(targetPart.Value, organ, organComp2.SlotId, partComp, organComp2))
        {
            _popupSystem.PopupEntity(Loc.GetString("autodoc-surgery-organ-attached", ("organ", organType)), uid, user);

            var healSpec = new DamageSpecifier();
            if (TryComp<DamageableComponent>(patient, out var damageable))
            {
                foreach (var damage in damageable.Damage.DamageDict)
                {
                    if (damage.Value > 0)
                    {
                        var damageValue = damage.Value.Float();
                        healSpec.DamageDict[damage.Key] = -Math.Min(damageValue, 15);
                    }
                }
            }
            if (!healSpec.Empty)
                _damageable.TryChangeDamage(patient, healSpec, true);
        }
        else
        {
            _popupSystem.PopupEntity(Loc.GetString("autodoc-surgery-attach-failed"), uid, user);
        }

        UpdateAvailableParts(uid, component);
    }

    private EntityUid? FindOrganTargetPart(EntityUid patient, string organType)
    {
        var targetPartType = organType switch
        {
            "brain" or "eyes" => BodyPartType.Head,
            "heart" or "liver" or "lungs" or "stomach" or "kidneys" or "appendix" => BodyPartType.Torso,
            _ => BodyPartType.Torso
        };

        var parts = _bodySystem.GetBodyChildrenOfType(patient, targetPartType);
        return parts.FirstOrDefault().Id;
    }

    public virtual void CompleteAllSurgery(EntityUid uid, SiriusAutodocSurgeryComponent component)
    {
        component.IsOperating = false;
        component.OperationProgress = 0f;

        if (component.CurrentPatient is not { } patient)
        {
            component.PendingSurgeries.Clear();
            return;
        }

        var surgeries = component.PendingSurgeries.ToList();
        component.PendingSurgeries.Clear();

        var failedSurgeries = new List<string>();

        foreach (var surgery in surgeries)
        {
            if (Enum.TryParse<BodyPartType>(surgery, true, out var partType))
            {
                if (component.AvailableParts.TryGetValue(partType, out var part))
                {
                    if (CanAttachPart(patient, partType, out var parentPart))
                    {
                        string? slotId = null;
                        if (TryComp<BodyPartComponent>(part, out var partComp))
                        {
                            slotId = GetSlotForBodyPart(partComp.PartType, partComp.Symmetry);
                        }

                        if (slotId != null)
                        {
                            _itemSlots.TryEject(uid, slotId, null, out _);
                        }

                        if (TryAttachPart(patient, part, parentPart))
                        {
                            var healSpec = new DamageSpecifier();
                            if (TryComp<DamageableComponent>(patient, out var damageable))
                            {
                                foreach (var damage in damageable.Damage.DamageDict)
                                {
                                    if (damage.Value > 0)
                                    {
                                        var damageValue = damage.Value.Float();
                                        healSpec.DamageDict[damage.Key] = -Math.Min(damageValue, 10);
                                    }
                                }
                            }
                            if (!healSpec.Empty)
                                _damageable.TryChangeDamage(patient, healSpec, true);
                        }
                        else
                        {
                            failedSurgeries.Add(surgery);
                        }
                    }
                    else
                    {
                        failedSurgeries.Add(surgery);
                    }
                }
                else
                {
                    failedSurgeries.Add(surgery);
                }
            }
            else if (SiriusAutodocSurgeryComponent.TransplantableOrgans.Contains(surgery))
            {
                if (component.AvailableOrgans.TryGetValue(surgery, out var organ))
                {
                    var targetPart = FindOrganTargetPart(patient, surgery);
                    if (targetPart != null)
                    {
                        string? slotId = null;
                        if (TryComp<OrganComponent>(organ, out var organComp))
                        {
                            var organSlotId = organComp.SlotId?.ToLowerInvariant();
                            if (!string.IsNullOrEmpty(organSlotId))
                            {
                                OrganSlotMap.TryGetValue(organSlotId, out slotId);
                            }
                        }

                        if (slotId != null)
                        {
                            _itemSlots.TryEject(uid, slotId, null, out _);
                        }

                        if (TryComp<OrganComponent>(organ, out var organComp2) &&
                            TryComp<BodyPartComponent>(targetPart, out var partComp) &&
                            !string.IsNullOrEmpty(organComp2.SlotId))
                        {
                            if (_bodySystem.InsertOrgan(targetPart.Value, organ, organComp2.SlotId, partComp, organComp2))
                            {
                                var healSpec = new DamageSpecifier();
                                if (TryComp<DamageableComponent>(patient, out var damageable))
                                {
                                    foreach (var damage in damageable.Damage.DamageDict)
                                    {
                                        if (damage.Value > 0)
                                        {
                                            var damageValue = damage.Value.Float();
                                            healSpec.DamageDict[damage.Key] = -Math.Min(damageValue, 15);
                                        }
                                    }
                                }
                                if (!healSpec.Empty)
                                    _damageable.TryChangeDamage(patient, healSpec, true);
                            }
                            else
                            {
                                failedSurgeries.Add(surgery);
                            }
                        }
                        else
                        {
                            failedSurgeries.Add(surgery);
                        }
                    }
                    else
                    {
                        failedSurgeries.Add(surgery);
                    }
                }
                else
                {
                    failedSurgeries.Add(surgery);
                }
            }
        }

        if (failedSurgeries.Count > 0)
        {
            _popupSystem.PopupEntity(
                Loc.GetString("autodoc-surgery-some-failed", ("count", failedSurgeries.Count)),
                uid, uid);
        }
        else
        {
            _popupSystem.PopupEntity(Loc.GetString("autodoc-surgery-all-complete"), uid, uid);
        }

        UpdateAvailableParts(uid, component);
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
