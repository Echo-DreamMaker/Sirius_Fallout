using Content.Shared._Nuclear14.AutodocSirius;
using Content.Shared._Shitmed.Body.Events;
using Content.Shared._Shitmed.Medical.Surgery.Steps.Parts;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Physics;
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

        foreach (var (opId, displayName, isAvailable) in operations)
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
                    var hasPart = HasBodyPart(patient, partInfo.Type, partInfo.Symmetry);
                    var key = (partInfo.Type, partInfo.Symmetry);
                    var hasPartInAutodoc = availableParts.ContainsKey(key);

                    string actualOpId;
                    string actualDisplayName;
                    bool finalIsAvailable;

                    if (hasPart)
                    {
                        actualOpId = "Remove" + char.ToUpper(partTypeName[0]) + partTypeName.Substring(1);
                        actualDisplayName = $"Удалить {displayName.ToLower()}";
                        finalIsAvailable = true;
                    }
                    else if (hasPartInAutodoc)
                    {
                        actualOpId = "Attach" + char.ToUpper(partTypeName[0]) + partTypeName.Substring(1);
                        actualDisplayName = $"Вставить {displayName.ToLower()}";
                        finalIsAvailable = true;
                    }
                    else
                    {
                        actualOpId = opId;
                        actualDisplayName = displayName;
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

                    result.Add(new AutodocOperationData(actualOpId, actualDisplayName, finalIsAvailable, tooltip));
                    continue;
                }
            }

            if (opId.StartsWith("Toggle"))
            {
                var organType = opId.Replace("Toggle", "").ToLowerInvariant();
                var hasOrgan = HasOrganBySlotId(patient, organType, entityManager);
                var hasOrganInAutodoc = availableOrgans.ContainsKey(organType);

                string actualOpId;
                string actualDisplayName;
                bool finalIsAvailable;

                if (hasOrgan)
                {
                    actualOpId = $"Remove{char.ToUpper(organType[0])}{organType.Substring(1)}";
                    actualDisplayName = $"Удалить {displayName.ToLower()}";
                    finalIsAvailable = true;
                }
                else if (hasOrganInAutodoc)
                {
                    actualOpId = $"Insert{char.ToUpper(organType[0])}{organType.Substring(1)}";
                    actualDisplayName = $"Вставить {displayName.ToLower()}";
                    finalIsAvailable = true;
                }
                else
                {
                    actualOpId = opId;
                    actualDisplayName = displayName;
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

                result.Add(new AutodocOperationData(actualOpId, actualDisplayName, finalIsAvailable, tooltip));
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

                result.Add(new AutodocOperationData(opId, displayName, finalIsAvailable, tooltip));
                continue;
            }

            string? normalTooltip = null;
            if (!isAvailable)
            {
                if (opId == "TendBrute")
                    normalTooltip = Loc.GetString("autodoc-surgery-no-brute-damage");
                else if (opId == "TendBurn")
                    normalTooltip = Loc.GetString("autodoc-surgery-no-burn-damage");
            }

            result.Add(new AutodocOperationData(opId, displayName, isAvailable, normalTooltip));
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
        var parts = bodySystem.GetBodyChildrenOfType(patient, partType, symmetry: symmetry ?? BodyPartSymmetry.None);
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
        var key = (partType, symmetry);
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
            if (partName == "left_arm") targetPartId = "left_arm";
            else if (partName == "right_arm") targetPartId = "right_arm";
            else if (partName == "left_leg") targetPartId = "left_leg";
            else if (partName == "right_leg") targetPartId = "right_leg";
            else if (partName == "left_hand") targetPartId = "left_hand";
            else if (partName == "right_hand") targetPartId = "right_hand";
            else if (partName == "left_foot") targetPartId = "left_foot";
            else if (partName == "right_foot") targetPartId = "right_foot";
            else if (partName == "head") targetPartId = "head";
        }

        if (operationId.StartsWith("Attach") && operationId != "AttachPart")
        {
            var partName = operationId.Replace("Attach", "").ToLowerInvariant();
            if (partName == "left_arm") targetPartId = "left_arm";
            else if (partName == "right_arm") targetPartId = "right_arm";
            else if (partName == "left_leg") targetPartId = "left_leg";
            else if (partName == "right_leg") targetPartId = "right_leg";
            else if (partName == "left_hand") targetPartId = "left_hand";
            else if (partName == "right_hand") targetPartId = "right_hand";
            else if (partName == "left_foot") targetPartId = "left_foot";
            else if (partName == "right_foot") targetPartId = "right_foot";
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
            if (OrganSlotMap.TryGetValue(organType, out var slotName))
            {
                _itemSlots.TryEject(autodocUid, slotName, null, out _);
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
            success = bodySystem.AttachPart(parentPart.Value, slotName, partEntity, null, partComp);
        }
        else
        {
            success = bodySystem.AttachPartToRoot(patient, partEntity);
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

    private bool ExecuteAttachSpecificPart(EntityUid autodocUid, EntityUid patient, string partId)
    {
        if (!BodyPartMap.TryGetValue(partId, out var partInfo))
            return false;

        var bodySystem = _bodySystem;

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

        if (parentPart == null)
            return false;

        var slotName = GetSlotForBodyPart(partInfo.Type, partInfo.Symmetry ?? BodyPartSymmetry.None);

        if (string.IsNullOrEmpty(slotName))
            return false;

        var containerId = SharedBodySystem.GetPartSlotContainerId(slotName);

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

        bool success;
        if (!string.IsNullOrEmpty(slotName))
        {
            success = bodySystem.AttachPart(parentPart.Value, slotName, partEntity, null, partComp);
        }
        else
        {
            success = bodySystem.AttachPartToRoot(patient, partEntity);
        }

        if (success)
        {
            EnsureComp<BodyPartReattachedComponent>(partEntity);
            var attachedEvent = new BodyPartAttachedEvent((partEntity, partComp));
            RaiseLocalEvent(patient, ref attachedEvent);
            if (!string.IsNullOrEmpty(slotName))
            {
                _itemSlots.TryEject(autodocUid, slotName, null, out _);
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

        var slotName = GetSlotForBodyPart(partInfo.Type, partInfo.Symmetry ?? BodyPartSymmetry.None);

        if (!string.IsNullOrEmpty(slotName))
        {
            var inserted = _itemSlots.TryInsert(autodocUid, slotName, partEntity, null);
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
}
