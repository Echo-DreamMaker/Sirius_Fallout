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
        _sawmill.Info($"=== GetOperationsForPart START ===");
        _sawmill.Info($"Patient: {patient}, PartId: {partId}, Autodoc: {autodocUid}");

        var result = new List<AutodocOperationData>();

        if (!patient.IsValid() || string.IsNullOrEmpty(partId))
        {
            _sawmill.Warning($"GetOperationsForPart: Invalid patient or partId");
            return result;
        }

        var entityManager = EntityManager;
        var operations = GetOperationsForPartType(partId);
        var availableOrgans = GetAvailableOrgansInAutodoc(autodocUid);
        var availableParts = GetAvailablePartsInAutodoc(autodocUid);

        _sawmill.Info($"Available Organs in Autodoc: {availableOrgans.Count}");
        foreach (var organ in availableOrgans)
        {
            _sawmill.Info($"  - Organ: {organ.Key} (Entity: {organ.Value})");
        }

        _sawmill.Info($"Available Parts in Autodoc: {availableParts.Count}");
        foreach (var part in availableParts)
        {
            _sawmill.Info($"  - Part: Type={part.Key.Type}, Symmetry={part.Key.Symmetry} (Entity: {part.Value})");
        }

        foreach (var (opId, displayName, isAvailable) in operations)
        {
            _sawmill.Info($"Processing operation: {opId} ({displayName})");

            if (opId.StartsWith("Toggle"))
            {
                var organType = opId.Replace("Toggle", "").ToLowerInvariant();
                var hasOrgan = HasOrganBySlotId(patient, organType, entityManager);
                var hasOrganInAutodoc = availableOrgans.ContainsKey(organType);

                _sawmill.Info($"  Toggle organ: {organType}, HasOrgan={hasOrgan}, HasOrganInAutodoc={hasOrganInAutodoc}");

                string actualOpId;
                string actualDisplayName;
                bool finalIsAvailable;

                if (hasOrgan)
                {
                    actualOpId = $"Remove{char.ToUpper(organType[0])}{organType.Substring(1)}";
                    actualDisplayName = $"Удалить {displayName.ToLower()}";
                    finalIsAvailable = true;
                    _sawmill.Info($"  -> Will remove organ (available)");
                }
                else if (hasOrganInAutodoc)
                {
                    actualOpId = $"Insert{char.ToUpper(organType[0])}{organType.Substring(1)}";
                    actualDisplayName = $"Вставить {displayName.ToLower()}";
                    finalIsAvailable = true;
                    _sawmill.Info($"  -> Will insert organ (available)");
                }
                else
                {
                    actualOpId = opId;
                    actualDisplayName = displayName;
                    finalIsAvailable = false;
                    _sawmill.Info($"  -> NOT available");
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
                _sawmill.Info($"  Attach operation: {opId}");
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
                    var key = (partInfo.Type, partInfo.Symmetry);
                    var hasPartInAutodoc = availableParts.ContainsKey(key);
                    var partExistsInBody = HasBodyPart(patient, partInfo.Type, partInfo.Symmetry);

                    _sawmill.Info($"    Target part: {targetPartId}, Type={partInfo.Type}, Symmetry={partInfo.Symmetry}");
                    _sawmill.Info($"    HasPartInAutodoc={hasPartInAutodoc}, PartExistsInBody={partExistsInBody}");

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
                _sawmill.Info($"  AttachPart operation for {partId}");
                bool finalIsAvailable = isAvailable;
                if (BodyPartMap.TryGetValue(partId, out var partInfo))
                {
                    var key = (partInfo.Type, partInfo.Symmetry);
                    var hasPartInAutodoc = availableParts.ContainsKey(key);
                    var partExistsInBody = HasBodyPart(patient, partInfo.Type, partInfo.Symmetry);

                    _sawmill.Info($"    Part: {partId}, Type={partInfo.Type}, Symmetry={partInfo.Symmetry}");
                    _sawmill.Info($"    HasPartInAutodoc={hasPartInAutodoc}, PartExistsInBody={partExistsInBody}");

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

        _sawmill.Info($"=== GetOperationsForPart END: {result.Count} operations ===");
        return result;
    }

    private Dictionary<string, EntityUid> GetAvailableOrgansInAutodoc(EntityUid autodocUid)
    {
        _sawmill.Info($"GetAvailableOrgansInAutodoc for {autodocUid}");
        var result = new Dictionary<string, EntityUid>();

        if (!TryComp<SiriusAutodocComponent>(autodocUid, out var autodoc))
        {
            _sawmill.Warning($"  No SiriusAutodocComponent found");
            return result;
        }

        if (autodoc.SiriusSurgeryComponent == null)
        {
            _sawmill.Warning($"  No SiriusSurgeryComponent found");
            return result;
        }

        if (!TryComp<SiriusAutodocSurgeryComponent>(autodoc.SiriusSurgeryComponent.Value, out var surgeryComp))
        {
            _sawmill.Warning($"  No SiriusAutodocSurgeryComponent found");
            return result;
        }

        _sawmill.Info($"  Found {surgeryComp.AvailableOrgans.Count} organs");
        return surgeryComp.AvailableOrgans;
    }

    private Dictionary<(BodyPartType Type, BodyPartSymmetry? Symmetry), EntityUid> GetAvailablePartsInAutodoc(EntityUid autodocUid)
    {
        _sawmill.Info($"GetAvailablePartsInAutodoc for {autodocUid}");
        var result = new Dictionary<(BodyPartType, BodyPartSymmetry?), EntityUid>();

        if (!TryComp<SiriusAutodocComponent>(autodocUid, out var autodoc))
        {
            _sawmill.Warning($"  No SiriusAutodocComponent found");
            return result;
        }

        if (autodoc.SiriusSurgeryComponent == null)
        {
            _sawmill.Warning($"  No SiriusSurgeryComponent found");
            return result;
        }

        if (!TryComp<SiriusAutodocSurgeryComponent>(autodoc.SiriusSurgeryComponent.Value, out var surgeryComp))
        {
            _sawmill.Warning($"  No SiriusAutodocSurgeryComponent found");
            return result;
        }

        _sawmill.Info($"  Found {surgeryComp.AvailableParts.Count} parts");
        return surgeryComp.AvailableParts;
    }

    private bool HasBodyPart(EntityUid patient, BodyPartType partType, BodyPartSymmetry? symmetry)
    {
        if (!patient.IsValid())
            return false;

        var bodySystem = _bodySystem;
        var parts = bodySystem.GetBodyChildrenOfType(patient, partType, symmetry: symmetry ?? BodyPartSymmetry.None);
        var hasPart = parts.Any();

        _sawmill.Info($"HasBodyPart: Patient={patient}, Type={partType}, Symmetry={symmetry}, Result={hasPart}");
        return hasPart;
    }

    public override bool HasAvailableOrganInAutodoc(string organType, EntityUid autodocUid)
    {
        var availableOrgans = GetAvailableOrgansInAutodoc(autodocUid);
        var result = availableOrgans.ContainsKey(organType.ToLowerInvariant());
        _sawmill.Info($"HasAvailableOrganInAutodoc: Organ={organType}, Result={result}");
        return result;
    }

    public override bool HasAvailablePartInAutodoc(BodyPartType partType, BodyPartSymmetry? symmetry, EntityUid autodocUid)
    {
        var availableParts = GetAvailablePartsInAutodoc(autodocUid);
        var key = (partType, symmetry);
        var result = availableParts.ContainsKey(key);
        _sawmill.Info($"HasAvailablePartInAutodoc: PartType={partType}, Symmetry={symmetry}, Result={result}");
        return result;
    }

    public override EntityUid? GetAvailableOrganInAutodoc(string organType, EntityUid autodocUid)
    {
        var availableOrgans = GetAvailableOrgansInAutodoc(autodocUid);
        if (availableOrgans.TryGetValue(organType.ToLowerInvariant(), out var organ))
        {
            _sawmill.Info($"GetAvailableOrganInAutodoc: Found organ {organType} -> {organ}");
            return organ;
        }
        _sawmill.Warning($"GetAvailableOrganInAutodoc: Organ {organType} NOT found");
        return null;
    }

    public override EntityUid? GetAvailablePartInAutodoc(BodyPartType partType, BodyPartSymmetry? symmetry, EntityUid autodocUid)
    {
        var availableParts = GetAvailablePartsInAutodoc(autodocUid);
        var key = (partType, symmetry);
        if (availableParts.TryGetValue(key, out var part))
        {
            _sawmill.Info($"GetAvailablePartInAutodoc: Found part Type={partType}, Symmetry={symmetry} -> {part}");
            return part;
        }
        _sawmill.Warning($"GetAvailablePartInAutodoc: Part Type={partType}, Symmetry={symmetry} NOT found");
        return null;
    }

    public override bool ExecuteSurgeryOperation(EntityUid autodocUid, EntityUid patient, string partId, string operationId)
    {
        _sawmill.Info($"=== ExecuteSurgeryOperation START ===");
        _sawmill.Info($"Autodoc: {autodocUid}, Patient: {patient}, PartId: {partId}, Operation: {operationId}");

        if (!BodyPartMap.TryGetValue(partId, out var partInfo))
        {
            _sawmill.Warning($"ExecuteSurgeryOperation: PartId {partId} not found in BodyPartMap");
            return false;
        }

        var bodySystem = _bodySystem;
        var parts = bodySystem.GetBodyChildrenOfType(patient, partInfo.Type, symmetry: partInfo.Symmetry ?? BodyPartSymmetry.None);
        var partEntity = parts.FirstOrDefault().Id;

        if (partEntity == default && !operationId.StartsWith("Insert") && !operationId.StartsWith("Attach"))
        {
            _sawmill.Warning($"ExecuteSurgeryOperation: Part entity not found and operation is not Insert/Attach");
            return false;
        }

        _sawmill.Info($"ExecuteSurgeryOperation: Part entity: {partEntity}");

        var result = operationId switch
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

        _sawmill.Info($"ExecuteSurgeryOperation RESULT: {result}");
        _sawmill.Info($"=== ExecuteSurgeryOperation END ===");
        return result;
    }

    private bool ExecuteInsertOrgan(EntityUid autodocUid, EntityUid patient, string organType)
    {
        _sawmill.Info($"=== ExecuteInsertOrgan START ===");
        _sawmill.Info($"Autodoc: {autodocUid}, Patient: {patient}, OrganType: {organType}");

        var availableOrgans = GetAvailableOrgansInAutodoc(autodocUid);

        if (!availableOrgans.TryGetValue(organType, out var organEntity))
        {
            _sawmill.Warning($"ExecuteInsertOrgan: No organ of type {organType} found in autodoc slots");
            _sawmill.Info($"Available organs: {string.Join(", ", availableOrgans.Keys)}");
            return false;
        }

        _sawmill.Info($"ExecuteInsertOrgan: Found organ entity: {organEntity}");

        if (!TryComp<OrganComponent>(organEntity, out var organComp))
        {
            _sawmill.Warning($"ExecuteInsertOrgan: Organ entity {organEntity} has no OrganComponent");
            return false;
        }

        var targetPartType = organType switch
        {
            "brain" or "eyes" => BodyPartType.Head,
            "heart" or "liver" or "lungs" or "stomach" or "kidneys" or "appendix" => BodyPartType.Torso,
            _ => BodyPartType.Torso
        };

        _sawmill.Info($"ExecuteInsertOrgan: Target part type: {targetPartType}");

        var bodySystem = _bodySystem;
        var targetParts = bodySystem.GetBodyChildrenOfType(patient, targetPartType);

        if (!targetParts.Any())
        {
            _sawmill.Warning($"ExecuteInsertOrgan: No target part of type {targetPartType} found in patient");
            return false;
        }

        var targetPart = targetParts.First().Id;
        _sawmill.Info($"ExecuteInsertOrgan: Target part: {targetPart}");

        if (!TryComp<BodyPartComponent>(targetPart, out var partComp))
        {
            _sawmill.Warning($"ExecuteInsertOrgan: Target part {targetPart} has no BodyPartComponent");
            return false;
        }

        var slotId = organComp.SlotId;
        if (string.IsNullOrEmpty(slotId))
        {
            _sawmill.Warning($"ExecuteInsertOrgan: Organ has no SlotId");
            return false;
        }

        _sawmill.Info($"ExecuteInsertOrgan: SlotId: {slotId}");

        if (bodySystem.InsertOrgan(targetPart, organEntity, slotId, partComp, organComp))
        {
            _sawmill.Info($"ExecuteInsertOrgan: Successfully inserted organ {organType}");
            if (OrganSlotMap.TryGetValue(organType, out var slotName))
            {
                _sawmill.Info($"ExecuteInsertOrgan: Ejecting from slot {slotName}");
                _itemSlots.TryEject(autodocUid, slotName, null, out _);
            }
            return true;
        }

        _sawmill.Warning($"ExecuteInsertOrgan: Failed to insert organ");
        return false;
    }

    private bool ExecuteAttachPart(EntityUid autodocUid, EntityUid patient, string partId)
    {
        _sawmill.Info($"=== ExecuteAttachPart START ===");
        _sawmill.Info($"Autodoc: {autodocUid}, Patient: {patient}, PartId: {partId}");

        if (!BodyPartMap.TryGetValue(partId, out var partInfo))
        {
            _sawmill.Warning($"ExecuteAttachPart: PartId {partId} not found in BodyPartMap");
            return false;
        }

        var availableParts = GetAvailablePartsInAutodoc(autodocUid);
        var key = (partInfo.Type, partInfo.Symmetry);

        _sawmill.Info($"ExecuteAttachPart: Looking for Type={partInfo.Type}, Symmetry={partInfo.Symmetry}");
        _sawmill.Info($"ExecuteAttachPart: Available parts: {availableParts.Count}");
        foreach (var part in availableParts)
        {
            _sawmill.Info($"  - Type={part.Key.Type}, Symmetry={part.Key.Symmetry} -> {part.Value}");
        }

        if (!availableParts.TryGetValue(key, out var partEntity))
        {
            _sawmill.Warning($"ExecuteAttachPart: No part of type {partInfo.Type} with symmetry {partInfo.Symmetry} found in autodoc slots");
            return false;
        }

        _sawmill.Info($"ExecuteAttachPart: Found part entity: {partEntity}");

        if (!TryComp<BodyPartComponent>(partEntity, out var partComp))
        {
            _sawmill.Warning($"ExecuteAttachPart: Part entity {partEntity} has no BodyPartComponent");
            return false;
        }

        var bodySystem = _bodySystem;
        var existingParts = bodySystem.GetBodyChildrenOfType(patient, partInfo.Type, symmetry: partInfo.Symmetry ?? BodyPartSymmetry.None);
        if (existingParts.Any())
        {
            _sawmill.Warning($"ExecuteAttachPart: Patient already has part {partId}");
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

        _sawmill.Info($"ExecuteAttachPart: Parent part: {parentPart}");

        bool success;
        var slotName = GetSlotForBodyPart(partInfo.Type, partInfo.Symmetry ?? BodyPartSymmetry.None);
        _sawmill.Info($"ExecuteAttachPart: Slot name: {slotName}");

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
            _sawmill.Info($"ExecuteAttachPart: Successfully attached part {partId}");
            if (!string.IsNullOrEmpty(slotName))
            {
                _sawmill.Info($"ExecuteAttachPart: Ejecting from slot {slotName}");
                _itemSlots.TryEject(autodocUid, slotName, null, out _);
            }
            return true;
        }

        _sawmill.Warning($"ExecuteAttachPart: Failed to attach part");
        return false;
    }

    private bool ExecuteAttachSpecificPart(EntityUid autodocUid, EntityUid patient, string partId)
    {
        _sawmill.Info($"=== ExecuteAttachSpecificPart START ===");
        _sawmill.Info($"Autodoc: {autodocUid}, Patient: {patient}, PartId: {partId}");

        if (!BodyPartMap.TryGetValue(partId, out var partInfo))
        {
            _sawmill.Warning($"ExecuteAttachSpecificPart: PartId {partId} not found in BodyPartMap");
            return false;
        }

        var availableParts = GetAvailablePartsInAutodoc(autodocUid);
        var key = (partInfo.Type, partInfo.Symmetry);

        if (!availableParts.TryGetValue(key, out var partEntity))
        {
            _sawmill.Warning($"ExecuteAttachSpecificPart: No part of type {partInfo.Type} with symmetry {partInfo.Symmetry} found");
            return false;
        }

        _sawmill.Info($"ExecuteAttachSpecificPart: Found part entity: {partEntity}");

        if (!TryComp<BodyPartComponent>(partEntity, out var partComp))
        {
            _sawmill.Warning($"ExecuteAttachSpecificPart: Part entity {partEntity} has no BodyPartComponent");
            return false;
        }

        var bodySystem = _bodySystem;
        var existingParts = bodySystem.GetBodyChildrenOfType(patient, partInfo.Type, symmetry: partInfo.Symmetry ?? BodyPartSymmetry.None);
        if (existingParts.Any())
        {
            _sawmill.Warning($"ExecuteAttachSpecificPart: Patient already has part {partId}");
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
            _sawmill.Info($"ExecuteAttachSpecificPart: Successfully attached part {partId}");
            if (!string.IsNullOrEmpty(slotName))
            {
                _sawmill.Info($"ExecuteAttachSpecificPart: Ejecting from slot {slotName}");
                _itemSlots.TryEject(autodocUid, slotName, null, out _);
            }
            return true;
        }

        _sawmill.Warning($"ExecuteAttachSpecificPart: Failed to attach part");
        return false;
    }
}
