using Content.Shared._Nuclear14.AutodocSirius;
using Content.Shared._Shitmed.Body.Events;
using Content.Shared._Shitmed.Medical.Surgery.Steps.Parts;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Containers.ItemSlots;
using System.Linq;

namespace Content.Server._Nuclear14.AutodocSirius;

public sealed class SiriusAutodocSurgerySystem : SharedSiriusAutodocSurgerySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    public override List<AutodocOperationData> GetOperationsForPart(EntityUid patient, string partId, EntityUid autodocUid)
    {
        var result = new List<AutodocOperationData>();
        if (!patient.IsValid() || string.IsNullOrEmpty(partId))
            return result;
        var entityManager = EntityManager;
        var operations = GetOperationsForPartType(partId);
        var availableOrgans = GetAvailableOrgansInAutodoc(autodocUid);
        var availableParts = GetAvailablePartsInAutodoc(autodocUid);
        foreach (var (opId, displayNameKey, isAvailable) in operations)
        {
            if (opId.StartsWith("ToggleHead") ||
                opId.StartsWith("ToggleLeftArm") ||
                opId.StartsWith("ToggleRightArm") ||
                opId.StartsWith("ToggleLeftLeg") ||
                opId.StartsWith("ToggleRightLeg") ||
                opId.StartsWith("ToggleLeftHand") ||
                opId.StartsWith("ToggleRightHand") ||
                opId.StartsWith("ToggleLeftFoot") ||
                opId.StartsWith("ToggleRightFoot"))
            {
                var partTypeName = opId.Replace("Toggle", "").ToLowerInvariant();
                if (BodyPartMap.TryGetValue(partTypeName, out var partInfo))
                {
                    if ((partInfo.Type == BodyPartType.Leg || partInfo.Type == BodyPartType.Arm) &&
                        !HasBodyPart(patient, BodyPartType.Torso, BodyPartSymmetry.None))
                    {
                        continue;
                    }
                    if (partInfo.Type == BodyPartType.Hand)
                    {
                        var parentSymmetry = partInfo.Symmetry ?? BodyPartSymmetry.None;
                        if (!HasBodyPart(patient, BodyPartType.Arm, parentSymmetry))
                        {
                            if (!HasBodyPart(patient, partInfo.Type, partInfo.Symmetry))
                                continue;
                        }
                    }
                    if (partInfo.Type == BodyPartType.Foot)
                    {
                        var parentSymmetry = partInfo.Symmetry ?? BodyPartSymmetry.None;
                        if (!HasBodyPart(patient, BodyPartType.Leg, parentSymmetry))
                        {
                            if (!HasBodyPart(patient, partInfo.Type, partInfo.Symmetry))
                                continue;
                        }
                    }
                    var hasPart = HasBodyPart(patient, partInfo.Type, partInfo.Symmetry);
                    var key = (partInfo.Type, partInfo.Symmetry);
                    var hasPartInAutodoc = availableParts.ContainsKey(key);
                    string actualOpId;
                    string displayName;
                    bool finalIsAvailable;
                    if (hasPart)
                    {
                        actualOpId = "Remove" + char.ToUpper(partTypeName[0]) + partTypeName.Substring(1);
                        displayName = Loc.GetString($"autodoc-surgery-op-remove-{partTypeName.ToLower()}");
                        finalIsAvailable = true;
                    }
                    else if (hasPartInAutodoc)
                    {
                        actualOpId = "Attach" + char.ToUpper(partTypeName[0]) + partTypeName.Substring(1);
                        displayName = Loc.GetString($"autodoc-surgery-op-attach-{partTypeName.ToLower()}");
                        finalIsAvailable = true;
                    }
                    else
                    {
                        actualOpId = opId;
                        displayName = Loc.GetString($"autodoc-surgery-op-toggle-{partTypeName.ToLower()}");
                        finalIsAvailable = false;
                    }
                    string? tooltip = null;
                    if (!finalIsAvailable)
                    {
                        if (hasPart)
                            tooltip = Loc.GetString("autodoc-surgery-part-present");
                        else if (!hasPartInAutodoc)
                            tooltip = Loc.GetString("autodoc-surgery-no-part-in-autodoc");
                    }
                    result.Add(new AutodocOperationData(actualOpId, displayName, finalIsAvailable, tooltip));
                    continue;
                }
            }
            if (opId.StartsWith("Toggle"))
            {
                var organType = opId.Replace("Toggle", "").ToLowerInvariant();
                var hasOrgan = HasOrganBySlotId(patient, organType, entityManager);
                var hasOrganInAutodoc = availableOrgans.ContainsKey(organType);
                string actualOpId;
                string displayName;
                bool finalIsAvailable;
                if (hasOrgan)
                {
                    actualOpId = $"Remove{char.ToUpper(organType[0])}{organType.Substring(1)}";
                    displayName = Loc.GetString($"autodoc-surgery-op-remove-{organType}");
                    finalIsAvailable = true;
                }
                else if (hasOrganInAutodoc)
                {
                    actualOpId = $"Insert{char.ToUpper(organType[0])}{organType.Substring(1)}";
                    displayName = Loc.GetString($"autodoc-surgery-op-insert-{organType}");
                    finalIsAvailable = true;
                }
                else
                {
                    actualOpId = opId;
                    displayName = Loc.GetString($"autodoc-surgery-op-toggle-{organType}");
                    finalIsAvailable = false;
                }
                string? tooltip = null;
                if (!finalIsAvailable)
                {
                    if (hasOrgan)
                        tooltip = Loc.GetString("autodoc-surgery-organ-present");
                    else if (!hasOrganInAutodoc)
                        tooltip = Loc.GetString("autodoc-surgery-no-organ-in-autodoc");
                }
                result.Add(new AutodocOperationData(actualOpId, displayName, finalIsAvailable, tooltip));
                continue;
            }
            if (opId.StartsWith("Attach") && opId != "AttachPart")
            {
                continue;
            }
            if (opId == "AttachPart")
            {
                bool finalIsAvailable = isAvailable;
                if (BodyPartMap.TryGetValue(partId, out var partInfo))
                {
                    var key = (partInfo.Type, partInfo.Symmetry);
                    var hasPartInAutodoc = availableParts.ContainsKey(key);
                    var partExistsInBody = HasBodyPart(patient, partInfo.Type, partInfo.Symmetry);
                    finalIsAvailable = !partExistsInBody && hasPartInAutodoc;
                }
                string? tooltip = null;
                if (!finalIsAvailable)
                {
                    if (BodyPartMap.TryGetValue(partId, out var info))
                    {
                        if (HasBodyPart(patient, info.Type, info.Symmetry))
                            tooltip = Loc.GetString("autodoc-surgery-part-present");
                        else
                            tooltip = Loc.GetString("autodoc-surgery-no-part-in-autodoc");
                    }
                    else
                        tooltip = Loc.GetString("autodoc-surgery-no-part-in-autodoc");
                }
                result.Add(new AutodocOperationData(opId, Loc.GetString(displayNameKey), finalIsAvailable, tooltip));
                continue;
            }
            if (opId == "TendBrute" || opId == "TendBurn")
            {
                bool finalIsAvailable = isAvailable;
                string? normalTooltip = null;
                if (!finalIsAvailable)
                {
                    if (opId == "TendBrute")
                        normalTooltip = Loc.GetString("autodoc-surgery-no-brute-damage");
                    else if (opId == "TendBurn")
                        normalTooltip = Loc.GetString("autodoc-surgery-no-burn-damage");
                }
                result.Add(new AutodocOperationData(opId, Loc.GetString(displayNameKey), finalIsAvailable, normalTooltip));
                continue;
            }
        }
        return result;
    }
    private Dictionary<string, EntityUid> GetAvailableOrgansInAutodoc(EntityUid autodocUid)
    {
        var result = new Dictionary<string, EntityUid>();

        if (!TryComp<SiriusAutodocComponent>(autodocUid, out var autodoc))
            return result;

        if (autodoc.SiriusSurgeryComponent == null)
            return result;

        if (!TryComp<SiriusAutodocSurgeryComponent>(autodoc.SiriusSurgeryComponent.Value, out var surgeryComp))
            return result;

        return surgeryComp.AvailableOrgans;
    }
    private Dictionary<(BodyPartType Type, BodyPartSymmetry? Symmetry), EntityUid> GetAvailablePartsInAutodoc(EntityUid autodocUid)
    {
        var result = new Dictionary<(BodyPartType, BodyPartSymmetry?), EntityUid>();
        if (!TryComp<SiriusAutodocComponent>(autodocUid, out var autodoc))
            return result;
        if (autodoc.SiriusSurgeryComponent == null)
            return result;
        if (!TryComp<SiriusAutodocSurgeryComponent>(autodoc.SiriusSurgeryComponent.Value, out var surgeryComp))
            return result;
        return surgeryComp.AvailableParts;
    }
    private bool HasBodyPart(EntityUid patient, BodyPartType partType, BodyPartSymmetry? symmetry)
    {
        if (!patient.IsValid())
            return false;
        var bodySystem = _bodySystem;
        var sym = symmetry ?? BodyPartSymmetry.None;
        var parts = bodySystem.GetBodyChildrenOfType(patient, partType, symmetry: sym);
        return parts.Any();
    }
    public override bool HasAvailableOrganInAutodoc(string organType, EntityUid autodocUid)
    {
        var availableOrgans = GetAvailableOrgansInAutodoc(autodocUid);
        return availableOrgans.ContainsKey(organType.ToLowerInvariant());
    }
    public override bool HasAvailablePartInAutodoc(BodyPartType partType, BodyPartSymmetry? symmetry, EntityUid autodocUid)
    {
        var availableParts = GetAvailablePartsInAutodoc(autodocUid);
        var key = (partType, symmetry);
        return availableParts.ContainsKey(key);
    }
    public override EntityUid? GetAvailableOrganInAutodoc(string organType, EntityUid autodocUid)
    {
        var availableOrgans = GetAvailableOrgansInAutodoc(autodocUid);
        if (availableOrgans.TryGetValue(organType.ToLowerInvariant(), out var organ))
            return organ;
        return null;
    }
    public override EntityUid? GetAvailablePartInAutodoc(BodyPartType partType, BodyPartSymmetry? symmetry, EntityUid autodocUid)
    {
        var availableParts = GetAvailablePartsInAutodoc(autodocUid);
        var key = (partType, symmetry ?? BodyPartSymmetry.None);
        if (availableParts.TryGetValue(key, out var part))
            return part;
        return null;
    }
    public override bool ExecuteSurgeryOperation(EntityUid autodocUid, EntityUid patient, string partId, string operationId)
    {
        string targetPartId = partId;
        if (operationId.StartsWith("Remove") && !operationId.StartsWith("RemoveBrain") &&
            !operationId.StartsWith("RemoveHeart") && !operationId.StartsWith("RemoveLiver") &&
            !operationId.StartsWith("RemoveLungs") && !operationId.StartsWith("RemoveStomach") &&
            !operationId.StartsWith("RemoveEyes"))
        {
            var partName = operationId.Replace("Remove", "").ToLowerInvariant();
            if (partName == "leftarm") targetPartId = "leftarm";
            else if (partName == "rightarm") targetPartId = "rightarm";
            else if (partName == "leftleg") targetPartId = "leftleg";
            else if (partName == "rightleg") targetPartId = "rightleg";
            else if (partName == "lefthand") targetPartId = "lefthand";
            else if (partName == "righthand") targetPartId = "righthand";
            else if (partName == "leftfoot") targetPartId = "leftfoot";
            else if (partName == "rightfoot") targetPartId = "rightfoot";
            else if (partName == "head") targetPartId = "head";
        }
        if (operationId.StartsWith("Attach") && operationId != "AttachPart")
        {
            var partName = operationId.Replace("Attach", "").ToLowerInvariant();
            if (partName == "leftarm") targetPartId = "leftarm";
            else if (partName == "rightarm") targetPartId = "rightarm";
            else if (partName == "leftleg") targetPartId = "leftleg";
            else if (partName == "rightleg") targetPartId = "rightleg";
            else if (partName == "lefthand") targetPartId = "lefthand";
            else if (partName == "righthand") targetPartId = "righthand";
            else if (partName == "leftfoot") targetPartId = "leftfoot";
            else if (partName == "rightfoot") targetPartId = "rightfoot";
            else if (partName == "head") targetPartId = "head";
        }
        if (!BodyPartMap.TryGetValue(targetPartId, out var partInfo))
            return false;
        if (operationId.StartsWith("Toggle"))
            return false;
        var bodySystem = _bodySystem;
        var parts = bodySystem.GetBodyChildrenOfType(patient, partInfo.Type, symmetry: partInfo.Symmetry ?? BodyPartSymmetry.None);
        var partEntity = parts.FirstOrDefault().Id;
        if (partEntity == default && !operationId.StartsWith("Insert") && !operationId.StartsWith("Attach") && !operationId.StartsWith("Remove"))
            return false;
        var result = operationId switch
        {
            "TendBrute" => ExecuteTendBrute(patient, partEntity),
            "TendBurn" => ExecuteTendBurn(patient, partEntity),
            "RemoveHead" => ExecuteRemoveSpecificPart(autodocUid, patient, "head"),
            "RemoveLeftarm" => ExecuteRemoveSpecificPart(autodocUid, patient, "leftarm"),
            "RemoveRightarm" => ExecuteRemoveSpecificPart(autodocUid, patient, "rightarm"),
            "RemoveLeftleg" => ExecuteRemoveSpecificPart(autodocUid, patient, "leftleg"),
            "RemoveRightleg" => ExecuteRemoveSpecificPart(autodocUid, patient, "rightleg"),
            "RemoveLefthand" => ExecuteRemoveSpecificPart(autodocUid, patient, "lefthand"),
            "RemoveRighthand" => ExecuteRemoveSpecificPart(autodocUid, patient, "righthand"),
            "RemoveLeftfoot" => ExecuteRemoveSpecificPart(autodocUid, patient, "leftfoot"),
            "RemoveRightfoot" => ExecuteRemoveSpecificPart(autodocUid, patient, "rightfoot"),
            "AttachHead" => ExecuteAttachSpecificPart(autodocUid, patient, "head"),
            "AttachLeftarm" => ExecuteAttachSpecificPart(autodocUid, patient, "leftarm"),
            "AttachRightarm" => ExecuteAttachSpecificPart(autodocUid, patient, "rightarm"),
            "AttachLeftleg" => ExecuteAttachSpecificPart(autodocUid, patient, "leftleg"),
            "AttachRightleg" => ExecuteAttachSpecificPart(autodocUid, patient, "rightleg"),
            "AttachLefthand" => ExecuteAttachSpecificPart(autodocUid, patient, "lefthand"),
            "AttachRighthand" => ExecuteAttachSpecificPart(autodocUid, patient, "righthand"),
            "AttachLeftfoot" => ExecuteAttachSpecificPart(autodocUid, patient, "leftfoot"),
            "AttachRightfoot" => ExecuteAttachSpecificPart(autodocUid, patient, "rightfoot"),
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
            "RemoveEyes" => ExecuteRemoveOrgan(autodocUid, patient, "eyes"),
            "InsertEyes" => ExecuteInsertOrgan(autodocUid, patient, "eyes"),
            "AttachPart" => ExecuteAttachPart(autodocUid, patient, partId),
            _ => false
        };
        return result;
    }
    private bool ExecuteInsertOrgan(EntityUid autodocUid, EntityUid patient, string organType)
    {
        var availableOrgans = GetAvailableOrgansInAutodoc(autodocUid);
        if (!availableOrgans.TryGetValue(organType, out var organEntity))
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
        if (bodySystem.InsertOrgan(targetPart, organEntity, slotId, partComp, organComp))
        {
            if (OrganSlotMap.TryGetValue(organType, out var autodocSlotName))
            {
                _itemSlots.TryEject(autodocUid, autodocSlotName, null, out _);
            }
            return true;
        }
        return false;
    }
    private bool ExecuteAttachPart(EntityUid autodocUid, EntityUid patient, string partId)
    {
        if (!BodyPartMap.TryGetValue(partId, out var partInfo))
            return false;
        var availableParts = GetAvailablePartsInAutodoc(autodocUid);
        var key = (partInfo.Type, partInfo.Symmetry);
        if (!availableParts.TryGetValue(key, out var partEntity))
            return false;
        if (!TryComp<BodyPartComponent>(partEntity, out var partComp))
            return false;
        var bodySystem = _bodySystem;
        var existingParts = bodySystem.GetBodyChildrenOfType(patient, partInfo.Type, symmetry: partInfo.Symmetry ?? BodyPartSymmetry.None);
        if (existingParts.Any())
            return false;
        EntityUid? parentPart = null;
        BodyPartType parentType;
        switch (partInfo.Type)
        {
            case BodyPartType.Head:
                parentType = BodyPartType.Torso;
                break;
            case BodyPartType.Torso:
                parentType = BodyPartType.Torso;
                break;
            case BodyPartType.Hand:
                parentType = BodyPartType.Arm;
                break;
            case BodyPartType.Foot:
                parentType = BodyPartType.Leg;
                break;
            case BodyPartType.Arm:
            case BodyPartType.Leg:
            default:
                parentType = BodyPartType.Torso;
                break;
        }
        var parentParts = bodySystem.GetBodyChildrenOfType(patient, parentType, symmetry: BodyPartSymmetry.None);
        if (parentParts.Any())
        {
            parentPart = parentParts.First().Id;
        }
        bool success;
        var slotName = GetBodyPartSlotForBodyPart(partInfo.Type, partInfo.Symmetry ?? BodyPartSymmetry.None);

        if (parentPart != null && !string.IsNullOrEmpty(slotName) && TryComp<BodyPartComponent>(parentPart.Value, out var parentComp))
        {
            if (!parentComp.Children.ContainsKey(slotName))
            {
                bodySystem.TryCreatePartSlot(parentPart.Value, slotName, partInfo.Type, out var _);
            }
        }
        if (parentPart != null && !string.IsNullOrEmpty(slotName))
        {
            success = bodySystem.AttachPart(parentPart.Value, slotName, partEntity, null, partComp);
        }
        else
        {
            success = bodySystem.AttachPartToRoot(patient, partEntity);
        }
        if (success)
        {
            if (partInfo.Type == BodyPartType.Arm)
            {
                var handSlotName = GetBodyPartSlotForBodyPart(BodyPartType.Hand, partInfo.Symmetry ?? BodyPartSymmetry.None);
                if (!string.IsNullOrEmpty(handSlotName))
                {
                    bodySystem.TryCreatePartSlot(partEntity, handSlotName, BodyPartType.Hand, out var _);
                }
            }
            if (partInfo.Type == BodyPartType.Leg)
            {
                var footSlotName = GetBodyPartSlotForBodyPart(BodyPartType.Foot, partInfo.Symmetry ?? BodyPartSymmetry.None);
                if (!string.IsNullOrEmpty(footSlotName))
                {
                    bodySystem.TryCreatePartSlot(partEntity, footSlotName, BodyPartType.Foot, out var _);
                }
            }
            var autodocSlotName = GetAutodocSlotForBodyPart(partInfo.Type, partInfo.Symmetry ?? BodyPartSymmetry.None);
            if (!string.IsNullOrEmpty(autodocSlotName))
            {
                _itemSlots.TryEject(autodocUid, autodocSlotName, null, out _);
            }
            return true;
        }
        return false;
    }
    private bool ExecuteAttachSpecificPart(EntityUid autodocUid, EntityUid patient, string partId)
    {
        if (!BodyPartMap.TryGetValue(partId, out var partInfo))
            return false;

        var bodySystem = _bodySystem;

        if (partInfo.Type == BodyPartType.Leg || partInfo.Type == BodyPartType.Arm)
        {
            var torsoCheck = bodySystem.GetBodyChildrenOfType(patient, BodyPartType.Torso, symmetry: BodyPartSymmetry.None);
            if (!torsoCheck.Any())
            {
                return false;
            }
        }
        if (partInfo.Type == BodyPartType.Hand || partInfo.Type == BodyPartType.Foot)
        {
            BodyPartType parentType = partInfo.Type == BodyPartType.Hand ? BodyPartType.Arm : BodyPartType.Leg;
            var parentCheck = bodySystem.GetBodyChildrenOfType(patient, parentType, symmetry: partInfo.Symmetry ?? BodyPartSymmetry.None);
            if (!parentCheck.Any())
            {
                return false;
            }
        }
        var existingParts = bodySystem.GetBodyChildrenOfType(patient, partInfo.Type, symmetry: partInfo.Symmetry ?? BodyPartSymmetry.None);
        if (existingParts.Any())
            return false;
        var availableParts = GetAvailablePartsInAutodoc(autodocUid);
        var key = (partInfo.Type, partInfo.Symmetry);
        if (!availableParts.TryGetValue(key, out var partEntity))
            return false;
        if (!TryComp<BodyPartComponent>(partEntity, out var partComp))
            return false;
        if (!partComp.Enabled)
        {
            var enableEvent = new BodyPartEnableChangedEvent(true);
            RaiseLocalEvent(partEntity, ref enableEvent);
            if (TryComp<BodyPartComponent>(partEntity, out var updatedPartComp))
            {
                partComp = updatedPartComp;
            }
        }
        EntityUid? parentPart = null;
        BodyPartType parentTypeForSearch;
        switch (partInfo.Type)
        {
            case BodyPartType.Head:
                parentTypeForSearch = BodyPartType.Torso;
                break;
            case BodyPartType.Torso:
                parentTypeForSearch = BodyPartType.Torso;
                break;
            case BodyPartType.Hand:
                parentTypeForSearch = BodyPartType.Arm;
                break;
            case BodyPartType.Foot:
                parentTypeForSearch = BodyPartType.Leg;
                break;
            case BodyPartType.Arm:
            case BodyPartType.Leg:
            default:
                parentTypeForSearch = BodyPartType.Torso;
                break;
        }
        BodyPartSymmetry searchSymmetry;
        if (partInfo.Type == BodyPartType.Hand || partInfo.Type == BodyPartType.Foot)
        {
            searchSymmetry = partInfo.Symmetry ?? BodyPartSymmetry.None;
        }
        else
        {
            searchSymmetry = BodyPartSymmetry.None;
        }
        var parentParts = bodySystem.GetBodyChildrenOfType(patient, parentTypeForSearch, symmetry: searchSymmetry);

        if (parentParts.Any())
        {
            parentPart = parentParts.First().Id;
        }
        if (parentPart == null)
            return false;
        var bodySlotName = GetBodyPartSlotForBodyPart(partInfo.Type, partInfo.Symmetry ?? BodyPartSymmetry.None);
        if (string.IsNullOrEmpty(bodySlotName))
            return false;
        if (TryComp<BodyPartComponent>(parentPart.Value, out var parentComp))
        {
            if (!parentComp.Children.ContainsKey(bodySlotName))
            {
                bodySystem.TryCreatePartSlot(parentPart.Value, bodySlotName, partInfo.Type, out var _);
            }
        }
        var containerId = SharedBodySystem.GetPartSlotContainerId(bodySlotName);
        if (_containerSystem.TryGetContainer(parentPart.Value, containerId, out var container))
        {
            if (container.ContainedEntities.Count > 0)
            {
                foreach (var ent in container.ContainedEntities.ToList())
                {
                    _containerSystem.Remove(ent, container);
                }
            }
        }
        else
        {
            return false;
        }
        bool success = bodySystem.AttachPart(parentPart.Value, bodySlotName, partEntity, parentComp, partComp);
        if (success)
        {
            EnsureComp<BodyPartReattachedComponent>(partEntity);
            var attachedEvent = new BodyPartAttachedEvent((partEntity, partComp));
            RaiseLocalEvent(patient, ref attachedEvent);

            if (partInfo.Type == BodyPartType.Arm)
            {
                var handSlotName = GetBodyPartSlotForBodyPart(BodyPartType.Hand, partInfo.Symmetry ?? BodyPartSymmetry.None);
                if (!string.IsNullOrEmpty(handSlotName))
                {
                    bodySystem.TryCreatePartSlot(partEntity, handSlotName, BodyPartType.Hand, out var _);
                }
            }
            if (partInfo.Type == BodyPartType.Leg)
            {
                var footSlotName = GetBodyPartSlotForBodyPart(BodyPartType.Foot, partInfo.Symmetry ?? BodyPartSymmetry.None);
                if (!string.IsNullOrEmpty(footSlotName))
                {
                    bodySystem.TryCreatePartSlot(partEntity, footSlotName, BodyPartType.Foot, out var _);
                }
            }
            var autodocSlotName = GetAutodocSlotForBodyPart(partInfo.Type, partInfo.Symmetry ?? BodyPartSymmetry.None);
            if (!string.IsNullOrEmpty(autodocSlotName))
            {
                _itemSlots.TryEject(autodocUid, autodocSlotName, null, out _);
            }
            if (TryComp<SiriusAutodocComponent>(autodocUid, out var autodoc) &&
                autodoc.SiriusSurgeryComponent != null &&
                TryComp<SiriusAutodocSurgeryComponent>(autodoc.SiriusSurgeryComponent.Value, out var surgeryComp))
            {
                UpdateAvailableParts(autodocUid, surgeryComp);
            }
            return true;
        }
        return false;
    }
    private bool ExecuteRemoveSpecificPart(EntityUid autodocUid, EntityUid patient, string partId)
    {
        if (!BodyPartMap.TryGetValue(partId, out var partInfo))
            return false;
        var bodySystem = _bodySystem;
        var parts = bodySystem.GetBodyChildrenOfType(patient, partInfo.Type, symmetry: partInfo.Symmetry ?? BodyPartSymmetry.None);
        if (!parts.Any())
            return false;
        var partEntity = parts.First().Id;
        if (bodySystem.IsPartRoot(patient, partEntity))
            return false;
        if (!TryComp<BodyPartComponent>(partEntity, out var partComp))
            return false;
        var disableEvent = new BodyPartEnableChangedEvent(false);
        RaiseLocalEvent(partEntity, ref disableEvent);
        if (!_containerSystem.TryGetContainingContainer(partEntity, out var container))
            return false;
        if (!_containerSystem.Remove(partEntity, container))
            return false;
        var autodocSlotName = GetAutodocSlotForBodyPart(partInfo.Type, partInfo.Symmetry ?? BodyPartSymmetry.None);
        if (!string.IsNullOrEmpty(autodocSlotName))
        {
            var inserted = _itemSlots.TryInsert(autodocUid, autodocSlotName, partEntity, null);
            if (!inserted)
            {
                _transform.DropNextTo(partEntity, autodocUid);
            }
        }
        else
        {
            _transform.DropNextTo(partEntity, autodocUid);
        }
        return true;
    }
    public void CancelCurrentOperation(EntityUid autodocUid)
    {
        if (TryComp<SiriusAutodocSurgeryComponent>(autodocUid, out var surgeryComp))
        {
            surgeryComp.IsOperating = false;
            surgeryComp.OperationProgress = 0f;
            surgeryComp.CurrentOperationId = null;
            surgeryComp.CurrentPartId = null;
            surgeryComp.CurrentOperationName = null;
            surgeryComp.SelectedPartId = null;
        }
        if (TryComp<SiriusAutodocComponent>(autodocUid, out var autodoc))
        {
            var system = EntityManager.System<SiriusAutodocSystem>();
            system.UpdateUiState((autodocUid, autodoc));
        }
    }
}
