using Content.Shared._Nuclear14.AutodocSirius;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Containers.ItemSlots;
using System.Linq;

namespace Content.Server._Nuclear14.AutodocSirius;

public sealed class SiriusAutodocSurgerySystem : SharedSiriusAutodocSurgerySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    private readonly ISawmill _sawmill = Logger.GetSawmill("autodoc");

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
                bool finalIsAvailable = isAvailable;
                var targetPartId = opId switch
                {
                    "AttachHead" => "head",
                    "AttachLeftArm" => "left_arm",
                    "AttachRightArm" => "right_arm",
                    "AttachLeftLeg" => "left_leg",
                    "AttachRightLeg" => "right_leg",
                    "AttachHand" => partId == "left_arm" ? "left_hand" : "right_hand",
                    "AttachFoot" => partId == "left_leg" ? "left_foot" : "right_foot",
                    _ => ""
                };

                if (!string.IsNullOrEmpty(targetPartId) && BodyPartMap.TryGetValue(targetPartId, out var partInfo))
                {
                    var hasPartInAutodoc = availableParts.ContainsKey(partInfo.Type);
                    var partExistsInBody = HasBodyPart(patient, partInfo.Type, partInfo.Symmetry);
                    finalIsAvailable = !partExistsInBody && hasPartInAutodoc;
                }

                string? tooltip = null;
                if (!finalIsAvailable)
                {
                    if (BodyPartMap.TryGetValue(opId.Replace("Attach", "").ToLowerInvariant(), out var info))
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
            if (opId == "AttachPart")
            {
                bool finalIsAvailable = isAvailable;
                if (BodyPartMap.TryGetValue(partId, out var partInfo))
                {
                    var hasPartInAutodoc = availableParts.ContainsKey(partInfo.Type);
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

    private Dictionary<BodyPartType, EntityUid> GetAvailablePartsInAutodoc(EntityUid autodocUid)
    {
        var result = new Dictionary<BodyPartType, EntityUid>();

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

    public override EntityUid? GetAvailableOrganInAutodoc(string organType)
    {
        return null;
    }

    public override EntityUid? GetAvailablePartInAutodoc(BodyPartType partType, BodyPartSymmetry? symmetry)
    {
        return null;
    }

    public override bool HasAvailableOrganInAutodoc(string organType) => false;
    public override bool HasAvailablePartInAutodoc(BodyPartType partType, BodyPartSymmetry? symmetry) => false;

    public override bool ExecuteSurgeryOperation(EntityUid autodocUid, EntityUid patient, string partId, string operationId)
    {
        if (!BodyPartMap.TryGetValue(partId, out var partInfo))
            return false;

        var bodySystem = _bodySystem;
        var parts = bodySystem.GetBodyChildrenOfType(patient, partInfo.Type, symmetry: partInfo.Symmetry ?? BodyPartSymmetry.None);
        var partEntity = parts.FirstOrDefault().Id;

        if (partEntity == default && !operationId.StartsWith("Insert") && !operationId.StartsWith("Attach"))
            return false;

        return operationId switch
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
            "RemoveEyes" => ExecuteRemoveOrgan(autodocUid, patient, "eyes"),
            "InsertEyes" => ExecuteInsertOrgan(autodocUid, patient, "eyes"),
            "AttachPart" => ExecuteAttachPart(autodocUid, patient, partId),
            "AttachHead" => ExecuteAttachSpecificPart(autodocUid, patient, "head"),
            "AttachLeftArm" => ExecuteAttachSpecificPart(autodocUid, patient, "left_arm"),
            "AttachRightArm" => ExecuteAttachSpecificPart(autodocUid, patient, "right_arm"),
            "AttachLeftLeg" => ExecuteAttachSpecificPart(autodocUid, patient, "left_leg"),
            "AttachRightLeg" => ExecuteAttachSpecificPart(autodocUid, patient, "right_leg"),
            "AttachHand" => ExecuteAttachSpecificPart(autodocUid, patient,
                partId == "left_arm" ? "left_hand" : "right_hand"),
            "AttachFoot" => ExecuteAttachSpecificPart(autodocUid, patient,
                partId == "left_leg" ? "left_foot" : "right_foot"),
            _ => false
        };
    }

    private bool ExecuteInsertOrgan(EntityUid autodocUid, EntityUid patient, string organType)
    {
        var availableOrgans = GetAvailableOrgansInAutodoc(autodocUid);

        if (!availableOrgans.TryGetValue(organType, out var organEntity))
        {
            _sawmill.Info($"No organ of type {organType} found in autodoc slots");
            return false;
        }

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

        if (!availableParts.TryGetValue(partInfo.Type, out var partEntity))
        {
            _sawmill.Info($"No part of type {partInfo.Type} found in autodoc slots");
            return false;
        }

        if (!TryComp<BodyPartComponent>(partEntity, out var partComp))
            return false;

        var bodySystem = _bodySystem;
        var existingParts = bodySystem.GetBodyChildrenOfType(patient, partInfo.Type, symmetry: partInfo.Symmetry ?? BodyPartSymmetry.None);
        if (existingParts.Any())
        {
            _sawmill.Info($"Patient already has part {partId}");
            return false;
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

        bool success;
        var slotName = GetSlotForBodyPart(partInfo.Type, partInfo.Symmetry ?? BodyPartSymmetry.None);

        if (parentPart != null && !string.IsNullOrEmpty(slotName))
        {
            success = bodySystem.AttachPart(parentPart.Value, slotName, partEntity, partComp, partComp);
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

        var availableParts = GetAvailablePartsInAutodoc(autodocUid);
        if (!availableParts.TryGetValue(partInfo.Type, out var partEntity))
        {
            _sawmill.Info($"No part of type {partInfo.Type} found in autodoc slots");
            return false;
        }

        if (!TryComp<BodyPartComponent>(partEntity, out var partComp))
            return false;
        var bodySystem = _bodySystem;
        var existingParts = bodySystem.GetBodyChildrenOfType(patient, partInfo.Type, symmetry: partInfo.Symmetry ?? BodyPartSymmetry.None);
        if (existingParts.Any())
        {
            _sawmill.Info($"Patient already has part {partId}");
            return false;
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

        bool success;
        var slotName = GetSlotForBodyPart(partInfo.Type, partInfo.Symmetry ?? BodyPartSymmetry.None);

        if (parentPart != null && !string.IsNullOrEmpty(slotName))
        {
            success = bodySystem.AttachPart(parentPart.Value, slotName, partEntity, partComp, partComp);
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
}
