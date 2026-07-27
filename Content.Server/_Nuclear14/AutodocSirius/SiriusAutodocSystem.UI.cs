using Content.Server.Chemistry.Containers.EntitySystems;
using Content.Shared._Nuclear14.AutodocSirius;
using Content.Shared._Shitmed.Body.Events;
using Content.Shared._Shitmed.Body.Organ;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Part;
using Content.Shared.Chemistry.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using System.Linq;
using static Content.Shared._Nuclear14.AutodocSirius.SharedSiriusAutodocSurgerySystem;

namespace Content.Server._Nuclear14.AutodocSirius;

public sealed partial class SiriusAutodocSystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly SolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    private readonly Dictionary<EntityUid, TimeSpan> _treatmentStartTime = new();
    private readonly Dictionary<EntityUid, (string PartId, string OperationId, TimeSpan StartTime)> _surgeryOperations = new();

    private const string StimulantsReagentId = "HealingMixture";
    private const int StimulantsRequired = 30;
    private bool _isUpdating = false;
    private const float UiUpdateInterval = 0.5f;
    private readonly Dictionary<EntityUid, TimeSpan> _lastUiUpdate = new();

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

    private void OnContainerInserted(Entity<SiriusAutodocComponent> entity, ref EntInsertedIntoContainerMessage args)
    {
        var slotId = args.Container.ID;

        if (slotId == SiriusAutodocComponent.SiriusBeakerSlotId)
        {
            UpdateUiState(entity);
        }
        else if (slotId == "autodoc-body")
        {
            entity.Comp.CurrentPatient = args.Entity;
            _sawmill.Info($"Patient inserted into autodoc: {args.Entity}");
            UpdateUiState(entity);
        }
        else if (IsOrganOrPartSlot(slotId))
        {
            _sawmill.Info($"Item inserted into slot {slotId}: {args.Entity}");

            EnableItemForSurgery(args.Entity);

            if (_surgerySystem != null &&
                TryComp<SiriusAutodocSurgeryComponent>(entity.Comp.SiriusSurgeryComponent, out var surgeryComp))
            {
                _surgerySystem.UpdateAvailableParts(entity, surgeryComp);
                _sawmill.Info($"Updated AvailableParts: {surgeryComp.AvailableParts.Count} parts, {surgeryComp.AvailableOrgans.Count} organs");
            }
            UpdateUiState(entity);
        }
    }
    private void EnableItemForSurgery(EntityUid item)
    {
        if (TryComp<OrganComponent>(item, out var organ))
        {
            var enableEvent = new OrganEnableChangedEvent(true);
            RaiseLocalEvent(item, ref enableEvent);
            _sawmill.Info($"Enabled organ {item}");
        }

        if (TryComp<BodyPartComponent>(item, out var part))
        {
            var enableEvent = new BodyPartEnableChangedEvent(true);
            RaiseLocalEvent(item, ref enableEvent);
            _sawmill.Info($"Enabled body part {item}");
        }
    }
    private void OnContainerRemoved(Entity<SiriusAutodocComponent> entity, ref EntRemovedFromContainerMessage args)
    {
        var slotId = args.Container.ID;

        if (slotId == SiriusAutodocComponent.SiriusBeakerSlotId)
        {
            UpdateUiState(entity);
        }
        else if (slotId == "autodoc-body")
        {
            entity.Comp.CurrentPatient = null;
            UpdateUiState(entity);
        }
        else if (IsOrganOrPartSlot(slotId))
        {
            _sawmill.Info($"Item removed from slot {slotId}: {args.Entity}");
            if (_surgerySystem != null &&
                TryComp<SiriusAutodocSurgeryComponent>(entity.Comp.SiriusSurgeryComponent, out var surgeryComp))
            {
                _surgerySystem.UpdateAvailableParts(entity, surgeryComp);
                _sawmill.Info($"Updated AvailableParts: {surgeryComp.AvailableParts.Count} parts, {surgeryComp.AvailableOrgans.Count} organs");
            }
            UpdateUiState(entity);
        }
    }

    private void OnPowerChanged(Entity<SiriusAutodocComponent> entity, ref PowerChangedEvent args)
    {
        entity.Comp.Powered = args.Powered;
        UpdateAppearance(entity.Owner, entity.Comp);
        UpdateUiState(entity);

        if (!args.Powered && entity.Comp.IsTreating)
        {
            entity.Comp.IsTreating = false;
            UpdateUiState(entity);
        }
    }

    private void OnBoundUIOpened(Entity<SiriusAutodocComponent> entity, ref BoundUIOpenedEvent args)
    {
        _lastUiUpdate[entity.Owner] = _gameTiming.CurTime;

        if (_surgerySystem != null &&
            TryComp<SiriusAutodocSurgeryComponent>(entity.Comp.SiriusSurgeryComponent, out var surgeryComp))
        {
            _surgerySystem.UpdateAvailableParts(entity, surgeryComp);
            _sawmill.Info($"UI opened - Updated AvailableParts: {surgeryComp.AvailableParts.Count} parts, {surgeryComp.AvailableOrgans.Count} organs");
        }

        UpdateUiState(entity);
    }

    private void OnBoundUIClosed(Entity<SiriusAutodocComponent> entity, ref BoundUIClosedEvent args)
    {
        _lastUiUpdate.Remove(entity.Owner);
    }

    private void OnEjectBeakerMessage(Entity<SiriusAutodocComponent> entity, EntityUid user)
    {
        if (entity.Comp.IsTreating)
        {
            _popupSystem.PopupEntity(Loc.GetString("autodoc-cant-eject-beaker-treating"), entity, user);
            UpdateUiState(entity);
            return;
        }

        var result = _itemSlots.TryEject(entity.Owner, SiriusAutodocComponent.SiriusBeakerSlotId, user, out var ejected);
        if (result)
        {
            _popupSystem.PopupEntity(Loc.GetString("autodoc-beaker-ejected"), entity, user);
        }

        UpdateUiState(entity);
    }

    private void OnStartTreatmentMessage(Entity<SiriusAutodocComponent> entity, EntityUid user)
    {
        if (!CanStartTreatment(entity))
        {
            UpdateUiState(entity);
            return;
        }

        if (entity.Comp.IsOpen)
        {
            UpdateUiState(entity);
            return;
        }

        entity.Comp.IsTreating = true;
        _treatmentStartTime[entity.Owner] = _gameTiming.CurTime;

        UpdateUiState(entity);
        UpdateAppearance(entity.Owner, entity.Comp);
    }
    internal void OnSurgeryPartSelected(Entity<SiriusAutodocComponent> entity, ref AutodocSurgeryPartSelectedMessage message)
    {
        _sawmill.Info($"SurgeryPartSelected: {message.PartId}");

        if (entity.Comp.IsTreating || entity.Comp.IsOpen)
            return;

        if (_surgerySystem == null)
            return;

        if (entity.Comp.CurrentPatient is not { } patient)
            return;

        var partId = message.PartId;

        if (TryComp<SiriusAutodocSurgeryComponent>(entity.Comp.SiriusSurgeryComponent, out var surgeryComponent))
        {
            surgeryComponent.SelectedPartId = partId;
            _sawmill.Info($"Saved SelectedPartId: {partId}");
        }
        UpdateUiState(entity);
    }

    internal void OnSurgeryOperationSelected(Entity<SiriusAutodocComponent> entity, ref AutodocSurgeryOperationMessage message)
    {
        _sawmill.Info($"SurgeryOperationSelected: Part={message.PartId}, Op={message.OperationId}");

        if (entity.Comp.IsTreating || entity.Comp.IsOpen)
        {
            _popupSystem.PopupEntity(Loc.GetString("autodoc-surgery-cant-during-treatment"), entity, message.Actor);
            return;
        }

        if (_surgerySystem == null)
            return;

        if (entity.Comp.CurrentPatient is not { } patient)
        {
            _popupSystem.PopupEntity(Loc.GetString("autodoc-surgery-no-patient"), entity, message.Actor);
            return;
        }

        if (_surgeryOperations.ContainsKey(entity.Owner))
        {
            _popupSystem.PopupEntity(Loc.GetString("autodoc-surgery-already-in-progress"), entity, message.Actor);
            return;
        }

        var partId = message.PartId;
        var operationId = message.OperationId;

        var operations = _surgerySystem.GetOperationsForPart(patient, partId, entity.Owner);
        var operation = operations.FirstOrDefault(o => o.Id == operationId);

        if (operation == null || !operation.IsAvailable)
        {
            _popupSystem.PopupEntity(Loc.GetString("autodoc-surgery-not-available"), entity, message.Actor);
            return;
        }
        _surgeryOperations[entity.Owner] = (partId, operationId, _gameTiming.CurTime);

        if (TryComp<SiriusAutodocSurgeryComponent>(entity.Comp.SiriusSurgeryComponent, out var surgeryComponent))
        {
            surgeryComponent.IsOperating = true;
            surgeryComponent.OperationProgress = 0f;
            surgeryComponent.CurrentOperationId = operationId;
            surgeryComponent.CurrentPartId = partId;
            surgeryComponent.CurrentOperationName = operation.DisplayName;
        }

        UpdateUiState(entity);
    }
    internal void OnSurgeryOperationDoAfter(AutodocSurgeryOperationDoAfterEvent args)
    {
        _sawmill.Info($"SurgeryOperationDoAfter: Part={args.PartId}, Op={args.OperationId}");

        if (args.Args.Used is not { } usedUid)
        {
            _sawmill.Info("No used Uid in args");
            return;
        }

        if (!TryComp<SiriusAutodocComponent>(usedUid, out var comp))
        {
            _sawmill.Info("No SiriusAutodocComponent on used Uid");
            return;
        }

        var entity = new Entity<SiriusAutodocComponent>(usedUid, comp);
        CompleteSurgeryOperation(entity, args.PartId, args.OperationId);
    }
    private string GetOperationDisplayName(string operationId)
    {
        return operationId switch
        {
            "TendBrute" => Loc.GetString("autodoc-surgery-op-tend-brute"),
            "TendBurn" => Loc.GetString("autodoc-surgery-op-tend-burn"),
            "RemoveBrain" => Loc.GetString("autodoc-surgery-op-remove-brain"),
            "InsertBrain" => Loc.GetString("autodoc-surgery-op-insert-brain"),
            "RemoveHeart" => Loc.GetString("autodoc-surgery-op-remove-heart"),
            "InsertHeart" => Loc.GetString("autodoc-surgery-op-insert-heart"),
            "RemoveLiver" => Loc.GetString("autodoc-surgery-op-remove-liver"),
            "InsertLiver" => Loc.GetString("autodoc-surgery-op-insert-liver"),
            "RemoveLungs" => Loc.GetString("autodoc-surgery-op-remove-lungs"),
            "InsertLungs" => Loc.GetString("autodoc-surgery-op-insert-lungs"),
            "RemoveStomach" => Loc.GetString("autodoc-surgery-op-remove-stomach"),
            "InsertStomach" => Loc.GetString("autodoc-surgery-op-insert-stomach"),
            "RemoveEyes" => Loc.GetString("autodoc-surgery-op-remove-eyes"),
            "InsertEyes" => Loc.GetString("autodoc-surgery-op-insert-eyes"),
            "AttachPart" => Loc.GetString("autodoc-surgery-op-attach-part"),
            _ => operationId
        };
    }

    private void OnUiButtonPressed(Entity<SiriusAutodocComponent> entity, ref AutodocUiButtonPressedMessage message)
    {
        _sawmill.Info($"OnUiButtonPressed: Button = {message.Button}");
        switch (message.Button)
        {
            case AutodocUiButton.OpenDoor:
                if (!entity.Comp.IsTreating && !entity.Comp.IsOpen)
                {
                    entity.Comp.IsOpen = true;
                    UpdateAppearance(entity.Owner, entity.Comp);
                    UpdateUiState(entity);
                }
                break;

            case AutodocUiButton.CloseDoor:
                if (!entity.Comp.IsTreating && entity.Comp.IsOpen)
                {
                    entity.Comp.IsOpen = false;
                    UpdateAppearance(entity.Owner, entity.Comp);
                    UpdateUiState(entity);
                }
                break;

            case AutodocUiButton.EjectBeaker:
                OnEjectBeakerMessage(entity, message.Actor);
                break;

            case AutodocUiButton.EjectPatient:
                if (entity.Comp.IsOpen)
                {
                    if (entity.Comp.IsEjecting)
                    {
                        return;
                    }

                    entity.Comp.IsEjecting = true;
                    try
                    {
                        TryEjectBody(entity.Owner, message.Actor, entity.Comp);
                        UpdateUiState(entity);
                    }
                    finally
                    {
                        entity.Comp.IsEjecting = false;
                    }
                }
                break;

            case AutodocUiButton.StartTreatment:
                OnStartTreatmentMessage(entity, message.Actor);
                break;
        }
    }

    private void OnToggleOpenMessage(Entity<SiriusAutodocComponent> entity, ref AutodocUiToggleOpenMessage message)
    {
        if (entity.Comp.IsTreating)
            return;

        TryToggleOpen(entity.Owner, message.Actor, entity.Comp);
    }

    private bool CanStartTreatment(Entity<SiriusAutodocComponent> entity)
    {
        if (entity.Comp.CurrentPatient == null || entity.Comp.BodyContainer.ContainedEntity == null)
        {
            return false;
        }

        if (entity.Comp.IsOpen)
        {
            return false;
        }

        if (!entity.Comp.Powered)
        {
            return false;
        }

        var beaker = _itemSlots.GetItemOrNull(entity.Owner, SiriusAutodocComponent.SiriusBeakerSlotId);
        if (beaker == null)
        {
            return false;
        }

        if (!_solutionContainer.TryGetFitsInDispenser(beaker.Value, out var soln, out var solution))
        {
            return false;
        }

        var stimulantsAmount = solution.GetReagentQuantity(new(StimulantsReagentId, null));
        if (stimulantsAmount < StimulantsRequired)
        {
            return false;
        }

        return true;
    }

    private void HealPatient(EntityUid patient)
    {
        if (!TryComp<DamageableComponent>(patient, out var damageable))
            return;

        var healSpec = new DamageSpecifier();
        foreach (var damage in damageable.Damage.DamageDict)
        {
            if (damage.Value > 0)
                healSpec.DamageDict[damage.Key] = -damage.Value;
        }

        if (!healSpec.Empty)
            _damageable.TryChangeDamage(patient, healSpec, true);
    }

    private void UpdateUiState(Entity<SiriusAutodocComponent> entity)
    {

        try
        {
            if (!_uiSystem.HasUi(entity.Owner, SiriusAutodocUiKey.Key))
            {
                return;
            }

            var state = GetUiState(entity);
            _uiSystem.SetUiState(entity.Owner, SiriusAutodocUiKey.Key, state);
            _sawmill.Info($"UpdateUiState: Sent state with SelectedPartId={state.SelectedPartId}");
        }
        finally
        {
        }
    }

    private AutodocBoundUserInterfaceState GetUiState(Entity<SiriusAutodocComponent> entity)
    {
        var component = entity.Comp;
        var hasOccupant = component.BodyContainer.ContainedEntity != null;
        var occupantDamage = new Dictionary<string, FixedPoint2>();
        var occupantStatus = OccupantStatus.None;
        var occupantName = string.Empty;
        var hasSurgeryComp = component.SiriusSurgeryComponent != null &&
                             TryComp<SiriusAutodocSurgeryComponent>(component.SiriusSurgeryComponent.Value, out _);
        var canSurgery = hasOccupant &&
                         !component.IsTreating &&
                         component.Powered &&
                         hasSurgeryComp;
        var surgeryMode = false;
        var bodyParts = new List<AutodocBodyPartData>();
        var selectedPartId = "";
        var availableOperations = new List<AutodocOperationData>();
        var isOperating = false;
        var operationProgress = 0f;
        var currentOperationName = "";

        if (hasOccupant && component.CurrentPatient is { } patient)
        {
            if (TryComp<DamageableComponent>(patient, out var damageable))
            {
                foreach (var damage in damageable.Damage.DamageDict)
                {
                    occupantDamage[damage.Key] = damage.Value;
                }
            }

            if (TryComp<MobStateComponent>(patient, out var mobState))
            {
                occupantStatus = mobState.CurrentState switch
                {
                    MobState.Alive => OccupantStatus.Alive,
                    MobState.Critical => OccupantStatus.Critical,
                    MobState.Dead => OccupantStatus.Dead,
                    _ => OccupantStatus.None
                };
            }

            if (TryComp<MetaDataComponent>(patient, out var meta))
                occupantName = meta.EntityName;
            if (canSurgery && _surgerySystem != null)
            {
                surgeryMode = true;

                _sawmill.Info($"Getting body parts for patient: {patient}");
                bodyParts = _surgerySystem.GetBodyPartsData(patient);
                _sawmill.Info($"Found {bodyParts.Count} body parts");

                if (TryComp<SiriusAutodocSurgeryComponent>(component.SiriusSurgeryComponent, out var surgeryComponent))
                {
                    selectedPartId = surgeryComponent.SelectedPartId ?? "";
                    _sawmill.Info($"SelectedPartId from component: '{selectedPartId}'");

                    isOperating = surgeryComponent.IsOperating;
                    operationProgress = surgeryComponent.OperationProgress;
                    currentOperationName = surgeryComponent.CurrentOperationName ?? "";

                    if (!string.IsNullOrEmpty(selectedPartId))
                    {
                        availableOperations = _surgerySystem.GetOperationsForPart(patient, selectedPartId, entity);
                        _sawmill.Info($"Found {availableOperations.Count} operations for part {selectedPartId}");
                    }
                }
            }
        }

        var beakerStimulants = FixedPoint2.Zero;
        var hasBeaker = false;
        var beakerCurrentVolume = FixedPoint2.Zero;
        var beakerMaxVolume = FixedPoint2.Zero;

        var beaker = _itemSlots.GetItemOrNull(entity.Owner, SiriusAutodocComponent.SiriusBeakerSlotId);

        if (beaker != null)
        {
            hasBeaker = true;
            if (_solutionContainer.TryGetFitsInDispenser(beaker.Value, out var soln, out var solution))
            {
                beakerStimulants = solution.GetReagentQuantity(new(StimulantsReagentId, null));
                beakerCurrentVolume = solution.Volume;
                beakerMaxVolume = solution.MaxVolume;
            }
        }

        var treatmentProgress = 0f;
        if (component.IsTreating && _treatmentStartTime.TryGetValue(entity.Owner, out var startTime))
        {
            var elapsed = (float) (_gameTiming.CurTime - startTime).TotalSeconds;
            treatmentProgress = Math.Clamp(elapsed / component.TreatmentDuration, 0, 1);
        }

        var canTreat = CanStartTreatment(entity);
        var treatButtonEnabled = canTreat && !component.IsTreating;

        return new AutodocBoundUserInterfaceState(
            component.IsOpen,
            component.Powered,
            hasOccupant,
            component.IsTreating,
            occupantStatus,
            occupantDamage,
            occupantName,
            hasBeaker,
            beakerCurrentVolume,
            beakerMaxVolume,
            beakerStimulants,
            treatButtonEnabled,
            treatmentProgress,
            canSurgery,
            null,
            surgeryMode,
            bodyParts,
            selectedPartId,
            availableOperations,
            isOperating,
            operationProgress,
            currentOperationName
        );
    }
    private void CompleteSurgeryOperation(Entity<SiriusAutodocComponent> entity, string partId, string operationId)
    {
        _sawmill.Info($"CompleteSurgeryOperation: Part={partId}, Op={operationId}");

        if (_surgerySystem == null)
        {
            _sawmill.Info("_surgerySystem is null");
            return;
        }

        if (entity.Comp.CurrentPatient is not { } patient)
        {
            _sawmill.Info("No patient in autodoc");
            return;
        }
        _sawmill.Info($"Calling ExecuteSurgeryOperation for {operationId} on patient {patient}");
        var success = _surgerySystem.ExecuteSurgeryOperation(entity, patient, partId, operationId);
        _sawmill.Info($"ExecuteSurgeryOperation result: {success}");

        if (success)
        {
            _popupSystem.PopupEntity(Loc.GetString("autodoc-surgery-complete", ("operation", GetOperationDisplayName(operationId))), entity);
        }
        else
        {
            _popupSystem.PopupEntity(Loc.GetString("autodoc-surgery-failed"), entity);
        }
        if (TryComp<SiriusAutodocSurgeryComponent>(entity.Comp.SiriusSurgeryComponent, out var surgeryComponent))
        {
            surgeryComponent.IsOperating = false;
            surgeryComponent.OperationProgress = 0f;
            surgeryComponent.CurrentOperationId = null;
            surgeryComponent.CurrentPartId = null;
            surgeryComponent.CurrentOperationName = null;
        }
        if (_surgerySystem != null && TryComp<SiriusAutodocSurgeryComponent>(entity.Comp.SiriusSurgeryComponent, out var surgeryComponent2))
        {
            _surgerySystem.UpdateAvailableParts(entity, surgeryComponent2);
        }
        UpdateUiState(entity);
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var currentTime = _gameTiming.CurTime;
        var treatmentsToComplete = new List<EntityUid>();

        foreach (var (uid, startTime) in _treatmentStartTime)
        {
            if (!TryComp<SiriusAutodocComponent>(uid, out var comp))
            {
                treatmentsToComplete.Add(uid);
                continue;
            }

            if (!comp.IsTreating)
            {
                treatmentsToComplete.Add(uid);
                continue;
            }

            var elapsed = (currentTime - startTime).TotalSeconds;
            if (elapsed >= comp.TreatmentDuration)
            {
                treatmentsToComplete.Add(uid);
                CompleteTreatment((uid, comp));
            }
        }

        foreach (var uid in treatmentsToComplete)
        {
            _treatmentStartTime.Remove(uid);
        }
        var surgeriesToComplete = new List<EntityUid>();
        foreach (var (uid, surgeryData) in _surgeryOperations)
        {
            if (!TryComp<SiriusAutodocComponent>(uid, out var comp))
            {
                surgeriesToComplete.Add(uid);
                continue;
            }

            var elapsed = (currentTime - surgeryData.StartTime).TotalSeconds;
            var progress = Math.Clamp((float) elapsed / SurgeryOperationDuration, 0f, 1f);

            if (TryComp<SiriusAutodocSurgeryComponent>(comp.SiriusSurgeryComponent, out var surgeryComponent))
            {
                surgeryComponent.OperationProgress = progress;
                _sawmill.Info($"Surgery progress: {progress * 100:F0}% for {surgeryData.OperationId}");
            }
            if (progress >= 1f)
            {
                surgeriesToComplete.Add(uid);
            }
        }
        foreach (var uid in surgeriesToComplete)
        {
            if (_surgeryOperations.TryGetValue(uid, out var data))
            {
                _sawmill.Info($"Surgery operation completed: {data.PartId}, {data.OperationId}");
                _surgeryOperations.Remove(uid);

                if (TryComp<SiriusAutodocComponent>(uid, out var comp))
                {
                    var entity = new Entity<SiriusAutodocComponent>(uid, comp);
                    CompleteSurgeryOperation(entity, data.PartId, data.OperationId);
                }
            }
        }
        var query = EntityQueryEnumerator<SiriusAutodocComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!_uiSystem.IsUiOpen(uid, SiriusAutodocUiKey.Key))
                continue;

            if (_surgeryOperations.ContainsKey(uid))
            {
                if (!_lastUiUpdate.TryGetValue(uid, out var lastUpdate))
                {
                    _lastUiUpdate[uid] = currentTime;
                    UpdateUiState((uid, comp));
                    continue;
                }

                if ((currentTime - lastUpdate).TotalSeconds >= UiUpdateInterval)
                {
                    _lastUiUpdate[uid] = currentTime;
                    UpdateUiState((uid, comp));
                }
            }
        }
    }

    private void CompleteTreatment(Entity<SiriusAutodocComponent> entity)
    {
        if (!entity.Comp.IsTreating)
            return;

        if (!entity.Comp.Powered)
        {
            entity.Comp.IsTreating = false;
            UpdateUiState(entity);
            UpdateAppearance(entity.Owner, entity.Comp);
            return;
        }

        if (entity.Comp.CurrentPatient is { } patient)
        {
            HealPatient(patient);

            var beaker = _itemSlots.GetItemOrNull(entity.Owner, SiriusAutodocComponent.SiriusBeakerSlotId);
            if (beaker != null && _solutionContainer.TryGetFitsInDispenser(beaker.Value, out var soln, out var solution))
            {
                var stimulantsAmount = solution.GetReagentQuantity(new(StimulantsReagentId, null));
                var toRemove = FixedPoint2.Min(StimulantsRequired, stimulantsAmount);
                _solutionContainer.RemoveReagent(soln.Value, new(StimulantsReagentId, null), toRemove);
            }
        }

        entity.Comp.IsTreating = false;
        UpdateUiState(entity);
        UpdateAppearance(entity.Owner, entity.Comp);
    }
}
