using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Server.Chemistry.Containers.EntitySystems;
using Content.Server.Medical.Components;
using Content.Server.Temperature.Components;
using Content.Server.Temperature.Systems;
using Content.Shared._Misfits.Medical.Cryogenics;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Medical.Cryogenics;
using Content.Shared.MedicalScanner;
using Content.Shared.Temperature;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Misfits.Medical.Cryogenics;

public sealed class MisfitsCryoPodSystem : EntitySystem
{
    [Dependency] private readonly SolutionContainerSystem _solutionContainerSystem = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstreamSystem = default!;
    [Dependency] private readonly ReactiveSystem _reactiveSystem = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlotsSystem = default!;
    [Dependency] private readonly TemperatureSystem _temperature = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly MetaDataSystem _metaDataSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MisfitsCryoPodComponent, AfterActivatableUIOpenEvent>(OnActivateUI);
        SubscribeLocalEvent<MisfitsCryoPodComponent, EntRemovedFromContainerMessage>(OnEjected);
        SubscribeLocalEvent<MisfitsCryoPodComponent, MisfitsCryoPodDepositReagentMessage>(OnDeposit);
        SubscribeLocalEvent<MisfitsCryoPodComponent, MisfitsCryoPodSetTransferAmountMessage>(OnSetTransferAmount);
        SubscribeLocalEvent<MisfitsCryoPodComponent, EntInsertedIntoContainerMessage>(OnBeakerChanged);
        SubscribeLocalEvent<InsideCryoPodComponent, ModifyChangedTemperatureEvent>(OnModifyTemperature);

        SubscribeLocalEvent<MisfitsCryoPodComponent, ActivatableUIOpenAttemptEvent>(
            OnActivateUIAttempt, after: new[] { typeof(Content.Server.Medical.CryoPodSystem) });
    }

    private void OnActivateUIAttempt(Entity<MisfitsCryoPodComponent> entity, ref ActivatableUIOpenAttemptEvent args)
    {
        if (!args.Cancelled || !TryComp<CryoPodComponent>(entity, out var cryoPod))
            return;

        var contained = cryoPod.BodyContainer.ContainedEntity;
        if (contained == args.User || !HasComp<ActiveCryoPodComponent>(entity))
            return;

        args.Uncancel();
    }

    private void OnModifyTemperature(Entity<InsideCryoPodComponent> entity, ref ModifyChangedTemperatureEvent args)
    {
        if (HasComp<MisfitsCryoPodComponent>(Transform(entity.Owner).ParentUid))
            args.TemperatureDelta = 0f;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _gameTiming.CurTime;
        var metaDataQuery = GetEntityQuery<MetaDataComponent>();
        var query = EntityQueryEnumerator<ActiveCryoPodComponent, MisfitsCryoPodComponent, CryoPodComponent>();

        while (query.MoveNext(out var uid, out _, out var misfits, out var cryoPod))
        {
            metaDataQuery.TryGetComponent(uid, out var metaData);
            if (curTime < misfits.NextUpdateTime + _metaDataSystem.GetPauseTime(uid, metaData))
                continue;
            misfits.NextUpdateTime = curTime + TimeSpan.FromSeconds(misfits.TransferTime);

            ProcessCycle(uid, misfits, cryoPod);
            PushState(uid, misfits, cryoPod);
        }
    }

    private void OnActivateUI(Entity<MisfitsCryoPodComponent> entity, ref AfterActivatableUIOpenEvent args)
    {
        if (TryComp<CryoPodComponent>(entity, out var cryoPod))
            PushState(entity.Owner, entity.Comp, cryoPod);
    }

    private void OnEjected(Entity<MisfitsCryoPodComponent> entity, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != "scanner-body")
            return;

        _uiSystem.CloseUi(entity.Owner, MisfitsCryoPodUiKey.Key);
    }

    private void OnBeakerChanged(Entity<MisfitsCryoPodComponent> entity, ref EntInsertedIntoContainerMessage args)
    {
        if (TryComp<CryoPodComponent>(entity, out var cryoPod))
            PushState(entity.Owner, entity.Comp, cryoPod);
    }

    private void OnDeposit(Entity<MisfitsCryoPodComponent> entity, ref MisfitsCryoPodDepositReagentMessage args)
    {
        var misfits = entity.Comp;

        var beaker = _itemSlotsSystem.GetItemOrNull(entity.Owner, misfits.BeakerSlotId);
        if (beaker is not { } beakerUid ||
            !_solutionContainerSystem.TryGetFitsInDispenser((beakerUid, null, null), out var beakerSoln, out var beakerSolution))
            return;

        if (!_solutionContainerSystem.ResolveSolution(entity.Owner, misfits.SolutionName, ref misfits.ChamberSolution, out var chamberSolution))
            return;

        var amount = FixedPoint2.Min(args.Amount, beakerSolution.GetTotalPrototypeQuantity(args.ReagentId));
        amount = FixedPoint2.Min(amount, chamberSolution.AvailableVolume);
        if (amount <= FixedPoint2.Zero)
            return;

        _solutionContainerSystem.RemoveReagent(beakerSoln.Value, args.ReagentId, amount);
        _solutionContainerSystem.TryAddReagent(misfits.ChamberSolution!.Value, args.ReagentId, amount, out _);

        if (TryComp<CryoPodComponent>(entity.Owner, out var cryoPod))
            PushState(entity.Owner, misfits, cryoPod);
    }

    private void OnSetTransferAmount(Entity<MisfitsCryoPodComponent> entity, ref MisfitsCryoPodSetTransferAmountMessage args)
    {
        var misfits = entity.Comp;
        misfits.TransferAmount = FixedPoint2.Clamp(args.Amount, misfits.MinTransferAmount, misfits.MaxTransferAmount);

        if (TryComp<CryoPodComponent>(entity.Owner, out var cryoPod))
            PushState(entity.Owner, misfits, cryoPod);
    }

    private void ProcessCycle(EntityUid uid, MisfitsCryoPodComponent misfits, CryoPodComponent cryoPod)
    {
        var patient = cryoPod.BodyContainer.ContainedEntity;
        if (patient is not { } patientUid)
            return;

        if (!_solutionContainerSystem.ResolveSolution(uid, misfits.SolutionName, ref misfits.ChamberSolution, out var solution))
            return;

        var chamber = misfits.ChamberSolution!.Value;

        if (solution.Volume > FixedPoint2.Zero && TryComp<BloodstreamComponent>(patientUid, out var bloodstream))
        {
            var toInject = _solutionContainerSystem.SplitSolution(chamber, FixedPoint2.Min(misfits.TransferAmount, solution.Volume));
            if (toInject.Volume > FixedPoint2.Zero)
            {
                _bloodstreamSystem.TryAddToChemicals(patientUid, toInject, bloodstream);
                _reactiveSystem.DoEntityReaction(patientUid, toInject, ReactionMethod.Injection);
            }
        }

        if (TryComp<TemperatureComponent>(patientUid, out var temperature))
        {
            var diff = temperature.CurrentTemperature - misfits.TargetTemperature;
            if (diff > 0.5f)
            {
                var coolantQ = solution.GetTotalPrototypeQuantity(misfits.CoolantReagent).Float();
                var fraction = MathF.Min(misfits.MaxCoolingFraction, misfits.BaseCoolingFraction + coolantQ * misfits.CoolingBoostPerUnit);
                var newTemp = MathF.Max(misfits.TargetTemperature, temperature.CurrentTemperature - diff * fraction);
                _temperature.ForceChangeTemperature(patientUid, newTemp, temperature);
            }
        }
    }

    private void PushState(EntityUid uid, MisfitsCryoPodComponent misfits, CryoPodComponent cryoPod)
    {
        if (!_uiSystem.IsUiOpen(uid, MisfitsCryoPodUiKey.Key))
            return;

        var patient = cryoPod.BodyContainer.ContainedEntity;
        var patientTemp = 0f;

        var healthScan = BuildHealthScan(patient, ref patientTemp);

        var patientReagents = Array.Empty<MisfitsCryoReagentReadout>();
        if (patient is { } patientForReagents &&
            TryComp<BloodstreamComponent>(patientForReagents, out var patientBloodstream) &&
            _solutionContainerSystem.ResolveSolution(patientForReagents, patientBloodstream.ChemicalSolutionName, ref patientBloodstream.ChemicalSolution, out var chemSolution))
        {
            patientReagents = BuildReadouts(chemSolution);
        }

        FixedPoint2 chamberMaxVol = FixedPoint2.Zero;
        var chamberReagents = Array.Empty<MisfitsCryoReagentReadout>();
        if (_solutionContainerSystem.ResolveSolution(uid, misfits.SolutionName, ref misfits.ChamberSolution, out var chamberSolution))
        {
            chamberMaxVol = chamberSolution.MaxVolume;
            chamberReagents = BuildReadouts(chamberSolution);
        }

        var hasBeaker = false;
        var beakerName = string.Empty;
        FixedPoint2 beakerMaxVol = FixedPoint2.Zero;
        var beakerReagents = Array.Empty<MisfitsCryoReagentReadout>();

        var beaker = _itemSlotsSystem.GetItemOrNull(uid, misfits.BeakerSlotId);
        if (beaker is { } beakerUid &&
            _solutionContainerSystem.TryGetFitsInDispenser((beakerUid, null, null), out _, out var beakerSolution))
        {
            hasBeaker = true;
            beakerName = Name(beakerUid);
            beakerMaxVol = beakerSolution.MaxVolume;
            beakerReagents = BuildReadouts(beakerSolution);
        }

        var state = new MisfitsCryoPodBoundUserInterfaceState(
            healthScan,
            patientTemp,
            misfits.TargetTemperature,
            patientReagents,
            chamberMaxVol,
            chamberReagents,
            hasBeaker,
            beakerName,
            beakerMaxVol,
            beakerReagents,
            misfits.TransferAmount,
            misfits.MinTransferAmount,
            misfits.MaxTransferAmount);

        _uiSystem.SetUiState(uid, MisfitsCryoPodUiKey.Key, state);
    }

    private HealthAnalyzerScannedUserMessage BuildHealthScan(EntityUid? patient, ref float patientTemp)
    {
        if (patient is not { } patientUid || !HasComp<DamageableComponent>(patientUid))
            return new HealthAnalyzerScannedUserMessage(null, float.NaN, float.NaN, null, null, null, null);

        var bodyTemperature = float.NaN;
        if (TryComp<TemperatureComponent>(patientUid, out var temperature))
        {
            bodyTemperature = temperature.CurrentTemperature;
            patientTemp = temperature.CurrentTemperature;
        }

        var bloodAmount = float.NaN;
        var bleeding = false;
        if (TryComp<BloodstreamComponent>(patientUid, out var bloodstream) &&
            _solutionContainerSystem.ResolveSolution(patientUid, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution, out var bloodSolution))
        {
            bloodAmount = bloodSolution.MaxVolume != 0 ? bloodSolution.FillFraction : 0;
            bleeding = bloodstream.BleedAmount > 0;
        }

        Dictionary<TargetBodyPart, TargetIntegrity>? body = null;
        if (HasComp<TargetingComponent>(patientUid))
            body = _body.GetBodyPartStatus(patientUid);

        return new HealthAnalyzerScannedUserMessage(
            GetNetEntity(patientUid),
            bodyTemperature,
            bloodAmount,
            true,
            bleeding,
            false,
            body);
    }

    private MisfitsCryoReagentReadout[] BuildReadouts(Solution solution)
    {
        var readouts = new MisfitsCryoReagentReadout[solution.Contents.Count];
        for (var i = 0; i < solution.Contents.Count; i++)
        {
            var (reagentId, quantity) = solution.Contents[i];
            var proto = _prototype.Index<ReagentPrototype>(reagentId.Prototype);
            readouts[i] = new MisfitsCryoReagentReadout(reagentId.Prototype, proto.LocalizedName, proto.SubstanceColor, quantity);
        }

        return readouts;
    }
}
