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
    private static readonly ISawmill _sawmill = Logger.GetSawmill("autodoc");

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
        _sawmill.Info($"=== ExecuteSurgeryOperation START ===");
        _sawmill.Info($"autodocUid={autodocUid}, patient={patient}, partId={partId}, operationId={operationId}");

        string targetPartId = partId;

        if (operationId.StartsWith("Remove") && !operationId.StartsWith("RemoveBrain") &&
            !operationId.StartsWith("RemoveHeart") && !operationId.StartsWith("RemoveLiver") &&
            !operationId.StartsWith("RemoveLungs") && !operationId.StartsWith("RemoveStomach") &&
            !operationId.StartsWith("RemoveEyes"))
        {
            var partName = operationId.Replace("Remove", "").ToLowerInvariant();
            _sawmill.Info($"Remove operation detected, partName={partName}");
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
            _sawmill.Info($"Attach operation detected, partName={partName}");
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

        _sawmill.Info($"Final targetPartId={targetPartId}");

        if (!BodyPartMap.TryGetValue(targetPartId, out var partInfo))
        {
            _sawmill.Error($"BodyPartMap does not contain key={targetPartId}");
            return false;
        }

        if (operationId.StartsWith("Toggle"))
        {
            _sawmill.Warning($"Toggle operation should not reach ExecuteSurgeryOperation");
            return false;
        }

        var bodySystem = _bodySystem;
        var parts = bodySystem.GetBodyChildrenOfType(patient, partInfo.Type, symmetry: partInfo.Symmetry ?? BodyPartSymmetry.None);
        var partEntity = parts.FirstOrDefault().Id;
        _sawmill.Info($"Found part entity={partEntity}, count={parts.Count()}");

        if (partEntity == default && !operationId.StartsWith("Insert") && !operationId.StartsWith("Attach") && !operationId.StartsWith("Remove"))
        {
            _sawmill.Warning($"No part entity found for operation {operationId}");
            return false;
        }

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

        _sawmill.Info($"ExecuteSurgeryOperation result={result}");
        return result;
    }

    private bool ExecuteInsertOrgan(EntityUid autodocUid, EntityUid patient, string organType)
    {
        _sawmill.Info($"ExecuteInsertOrgan: organType={organType}");

        var availableOrgans = GetAvailableOrgansInAutodoc(autodocUid);

        if (!availableOrgans.TryGetValue(organType, out var organEntity))
        {
            _sawmill.Warning($"ExecuteInsertOrgan: organ {organType} not found in autodoc");
            return false;
        }

        if (!TryComp<OrganComponent>(organEntity, out var organComp))
        {
            _sawmill.Warning($"ExecuteInsertOrgan: organEntity has no OrganComponent");
            return false;
        }

        var targetPartType = organType switch
        {
            "brain" or "eyes" => BodyPartType.Head,
            "heart" or "liver" or "lungs" or "stomach" or "kidneys" or "appendix" => BodyPartType.Torso,
            _ => BodyPartType.Torso
        };

        var bodySystem = _bodySystem;
        var targetParts = bodySystem.GetBodyChildrenOfType(patient, targetPartType);

        if (!targetParts.Any())
        {
            _sawmill.Warning($"ExecuteInsertOrgan: no target part of type {targetPartType} found");
            return false;
        }

        var targetPart = targetParts.First().Id;

        if (!TryComp<BodyPartComponent>(targetPart, out var partComp))
        {
            _sawmill.Warning($"ExecuteInsertOrgan: targetPart has no BodyPartComponent");
            return false;
        }

        var slotId = organComp.SlotId;
        if (string.IsNullOrEmpty(slotId))
        {
            _sawmill.Warning($"ExecuteInsertOrgan: organComp.SlotId is null or empty");
            return false;
        }

        if (bodySystem.InsertOrgan(targetPart, organEntity, slotId, partComp, organComp))
        {
            if (OrganSlotMap.TryGetValue(organType, out var autodocSlotName))
            {
                _sawmill.Info($"Ejecting organ from autodoc slot {autodocSlotName}");
                _itemSlots.TryEject(autodocUid, autodocSlotName, null, out _);
            }
            _sawmill.Info($"ExecuteInsertOrgan: SUCCESS");
            return true;
        }

        _sawmill.Warning($"ExecuteInsertOrgan: InsertOrgan returned false");
        return false;
    }

    private bool ExecuteAttachPart(EntityUid autodocUid, EntityUid patient, string partId)
    {
        _sawmill.Info($"ExecuteAttachPart: partId={partId}");

        if (!BodyPartMap.TryGetValue(partId, out var partInfo))
        {
            _sawmill.Warning($"ExecuteAttachPart: partId {partId} not in BodyPartMap");
            return false;
        }

        var availableParts = GetAvailablePartsInAutodoc(autodocUid);
        var key = (partInfo.Type, partInfo.Symmetry);

        if (!availableParts.TryGetValue(key, out var partEntity))
        {
            _sawmill.Warning($"ExecuteAttachPart: part not found in autodoc storage");
            return false;
        }

        if (!TryComp<BodyPartComponent>(partEntity, out var partComp))
        {
            _sawmill.Warning($"ExecuteAttachPart: partEntity has no BodyPartComponent");
            return false;
        }

        var bodySystem = _bodySystem;
        var existingParts = bodySystem.GetBodyChildrenOfType(patient, partInfo.Type, symmetry: partInfo.Symmetry ?? BodyPartSymmetry.None);
        if (existingParts.Any())
        {
            _sawmill.Warning($"ExecuteAttachPart: patient already has part {partId}");
            return false;
        }

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

        var parentParts = bodySystem.GetBodyChildrenOfType(patient, parentType, symmetry: partInfo.Symmetry ?? BodyPartSymmetry.None);
        if (parentParts.Any())
        {
            parentPart = parentParts.First().Id;
        }

        bool success;
        var slotName = GetBodyPartSlotForBodyPart(partInfo.Type, partInfo.Symmetry ?? BodyPartSymmetry.None);
        _sawmill.Info($"ExecuteAttachPart: parentPart={parentPart}, slotName={slotName}");

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
            var autodocSlotName = GetAutodocSlotForBodyPart(partInfo.Type, partInfo.Symmetry ?? BodyPartSymmetry.None);
            if (!string.IsNullOrEmpty(autodocSlotName))
            {
                _itemSlots.TryEject(autodocUid, autodocSlotName, null, out _);
            }
            _sawmill.Info($"ExecuteAttachPart: SUCCESS");
            return true;
        }

        _sawmill.Warning($"ExecuteAttachPart: AttachPart returned false");
        return false;
    }

    private bool ExecuteAttachSpecificPart(EntityUid autodocUid, EntityUid patient, string partId)
    {
        _sawmill.Info($"=== ExecuteAttachSpecificPart START ===");
        _sawmill.Info($"autodocUid={autodocUid}, patient={patient}, partId={partId}");

        if (!BodyPartMap.TryGetValue(partId, out var partInfo))
        {
            _sawmill.Error($"BodyPartMap does not contain key={partId}");
            return false;
        }
        _sawmill.Info($"partInfo.Type={partInfo.Type}, partInfo.Symmetry={partInfo.Symmetry}");

        var bodySystem = _bodySystem;

        var existingParts = bodySystem.GetBodyChildrenOfType(patient, partInfo.Type, symmetry: partInfo.Symmetry ?? BodyPartSymmetry.None);
        _sawmill.Info($"existingParts count={existingParts.Count()}");

        if (existingParts.Any())
        {
            _sawmill.Warning($"Patient already has part {partId}, skipping");
            return false;
        }

        var availableParts = GetAvailablePartsInAutodoc(autodocUid);
        _sawmill.Info($"availableParts count={availableParts.Count}");

        var key = (partInfo.Type, partInfo.Symmetry);

        if (!availableParts.TryGetValue(key, out var partEntity))
        {
            _sawmill.Error($"Part not found in autodoc storage for Type={partInfo.Type}, Symmetry={partInfo.Symmetry}");
            return false;
        }
        _sawmill.Info($"partEntity={partEntity}");

        if (!TryComp<BodyPartComponent>(partEntity, out var partComp))
        {
            _sawmill.Error($"partEntity has no BodyPartComponent");
            return false;
        }
        _sawmill.Info($"partComp.Enabled={partComp.Enabled}, partComp.CanEnable={partComp.CanEnable}");

        if (!partComp.Enabled)
        {
            _sawmill.Info($"Part is disabled, enabling...");
            var enableEvent = new BodyPartEnableChangedEvent(true);
            RaiseLocalEvent(partEntity, ref enableEvent);
            if (TryComp<BodyPartComponent>(partEntity, out var updatedPartComp))
            {
                partComp = updatedPartComp;
            }
        }

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

        _sawmill.Info($"parentType={parentType}");

        var parentParts = bodySystem.GetBodyChildrenOfType(patient, parentType, symmetry: partInfo.Symmetry ?? BodyPartSymmetry.None);
        _sawmill.Info($"parentParts count={parentParts.Count()}, parentType={parentType}");

        if (parentParts.Any())
        {
            parentPart = parentParts.First().Id;
            _sawmill.Info($"parentPart={parentPart}");
        }

        if (parentPart == null)
        {
            _sawmill.Error($"No parent part found for {partId}");
            return false;
        }

        var bodySlotName = GetBodyPartSlotForBodyPart(partInfo.Type, partInfo.Symmetry ?? BodyPartSymmetry.None);
        _sawmill.Info($"bodySlotName={bodySlotName}");

        if (string.IsNullOrEmpty(bodySlotName))
        {
            _sawmill.Error($"bodySlotName is null or empty");
            return false;
        }

        if (TryComp<BodyPartComponent>(parentPart.Value, out var parentComp))
        {
            _sawmill.Info($"parentComp.Children keys: {string.Join(", ", parentComp.Children.Keys)}");
            if (!parentComp.Children.ContainsKey(bodySlotName))
            {
                _sawmill.Error($"Slot {bodySlotName} not found in parent's Children! Available: {string.Join(", ", parentComp.Children.Keys)}");
                return false;
            }
        }

        var containerId = SharedBodySystem.GetPartSlotContainerId(bodySlotName);
        _sawmill.Info($"containerId={containerId}");

        if (_containerSystem.TryGetContainer(parentPart.Value, containerId, out var container))
        {
            _sawmill.Info($"container found, containedEntities count={container.ContainedEntities.Count}");
            if (container.ContainedEntities.Count > 0)
            {
                foreach (var ent in container.ContainedEntities.ToList())
                {
                    _sawmill.Info($"Removing {ent} from container");
                    _containerSystem.Remove(ent, container);
                }
            }
        }
        else
        {
            _sawmill.Error($"Container {containerId} not found on parent {parentPart}");
            return false;
        }

        _sawmill.Info($"Calling AttachPart with: parent={parentPart}, slotName={bodySlotName}, part={partEntity}");
        bool success = bodySystem.AttachPart(parentPart.Value, bodySlotName, partEntity, parentComp, partComp);
        _sawmill.Info($"AttachPart result: {success}");

        if (success)
        {
            _sawmill.Info($"Attach succeeded");
            EnsureComp<BodyPartReattachedComponent>(partEntity);
            var attachedEvent = new BodyPartAttachedEvent((partEntity, partComp));
            RaiseLocalEvent(patient, ref attachedEvent);

            var autodocSlotName = GetAutodocSlotForBodyPart(partInfo.Type, partInfo.Symmetry ?? BodyPartSymmetry.None);
            if (!string.IsNullOrEmpty(autodocSlotName))
            {
                _sawmill.Info($"Ejecting from autodoc slot {autodocSlotName}");
                _itemSlots.TryEject(autodocUid, autodocSlotName, null, out _);
            }

            if (TryComp<SiriusAutodocComponent>(autodocUid, out var autodoc) &&
                autodoc.SiriusSurgeryComponent != null &&
                TryComp<SiriusAutodocSurgeryComponent>(autodoc.SiriusSurgeryComponent.Value, out var surgeryComp))
            {
                UpdateAvailableParts(autodocUid, surgeryComp);
            }

            _sawmill.Info($"ExecuteAttachSpecificPart SUCCESS");
            return true;
        }

        _sawmill.Error($"AttachPart returned false, operation failed");
        return false;
    }

    private bool ExecuteRemoveSpecificPart(EntityUid autodocUid, EntityUid patient, string partId)
    {
        _sawmill.Info($"ExecuteRemoveSpecificPart: partId={partId}");

        if (!BodyPartMap.TryGetValue(partId, out var partInfo))
        {
            _sawmill.Warning($"partId {partId} not in BodyPartMap");
            return false;
        }

        var bodySystem = _bodySystem;
        var parts = bodySystem.GetBodyChildrenOfType(patient, partInfo.Type, symmetry: partInfo.Symmetry ?? BodyPartSymmetry.None);

        if (!parts.Any())
        {
            _sawmill.Warning($"No part {partId} found on patient");
            return false;
        }

        var partEntity = parts.First().Id;
        _sawmill.Info($"Found part entity={partEntity}");

        if (bodySystem.IsPartRoot(patient, partEntity))
        {
            _sawmill.Warning($"Cannot remove root part (Torso)");
            return false;
        }

        if (!TryComp<BodyPartComponent>(partEntity, out var partComp))
        {
            _sawmill.Warning($"partEntity has no BodyPartComponent");
            return false;
        }

        var disableEvent = new BodyPartEnableChangedEvent(false);
        RaiseLocalEvent(partEntity, ref disableEvent);

        if (!_containerSystem.TryGetContainingContainer(partEntity, out var container))
        {
            _sawmill.Warning($"Could not get containing container for part");
            return false;
        }

        if (!_containerSystem.Remove(partEntity, container))
        {
            _sawmill.Warning($"Could not remove part from container");
            return false;
        }

        var autodocSlotName = GetAutodocSlotForBodyPart(partInfo.Type, partInfo.Symmetry ?? BodyPartSymmetry.None);
        _sawmill.Info($"autodocSlotName={autodocSlotName}");

        if (!string.IsNullOrEmpty(autodocSlotName))
        {
            var inserted = _itemSlots.TryInsert(autodocUid, autodocSlotName, partEntity, null);
            if (!inserted)
            {
                _sawmill.Warning($"Could not insert part into slot {autodocSlotName}, dropping on floor");
                _transform.DropNextTo(partEntity, autodocUid);
            }
            else
            {
                _sawmill.Info($"Part inserted into slot {autodocSlotName}");
            }
        }
        else
        {
            _sawmill.Warning($"No slot name, dropping on floor");
            _transform.DropNextTo(partEntity, autodocUid);
        }

        return true;
    }
}
