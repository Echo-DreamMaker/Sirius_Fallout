using System.Linq;
using Content.Server._Sirius.Dirtiness;
using Content.Shared._Misfits.Surgery.Contamination;
using Content.Shared._Sirius.Dirtiness;
using Content.Shared._Sirius.Wet;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Shared.Player;

namespace Content.Server._Sirius.Soap;

/// <summary>
/// #Sirius Add: using soap in hand (or on another mob) lathers them for 5 units,
/// and hot soap reagent solidifies into one of the five siriussoap bars once cooled.
/// The vanilla "eat" action stays available through the alternative-verb context menu.
/// </summary>
public sealed class SiriusSoapSystem : EntitySystem
{
    private const string SoapSolution = "soap";
    private const string SoapReagent = "SoapReagent";
    private const float UseUnits = 5f;

    private const string PlainSoap = "N14SoapSirius";

    // #Sirius Add: the plant left in the solution decides which of the five soaps is made.
    private static readonly (string Reagent, string Soap)[] ScentedSoaps =
    {
        ("ExtractTarberries", "N14SoapSiriusWildberrySirius"),
        ("ExtractStarlightBerries", "N14SoapSiriusBluelagoonSirius"),
        ("ExtractGlowpepper", "N14SoapSiriusIsotopSirius"),
        ("ExtractAgaveNectar", "N14SoapSiriusLavandaSirius"),
    };

    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly SiriusDirtySystem _dirty = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TagComponent, UseInHandEvent>(OnUseInHand);
        // #Sirius Fix: lathering a mob must win over the sanitizer's "nothing to clean" popup.
        // Item targets stay with SurgeryCleanSystem, so soap can still disinfect scalpels etc.
        SubscribeLocalEvent<TagComponent, AfterInteractEvent>(OnAfterInteract, before: [typeof(SurgeryCleanSystem)]);
    }

    private void OnUseInHand(Entity<TagComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || !_tags.HasTag(ent.Owner, "Soap") || !HasComp<SiriusDirtyComponent>(args.User))
            return;

        // #Sirius Fix: a soap-in-hand use on a character is lathering, not item sanitization.
        args.Handled = true;
        TryWash(ent, args.User, args.User);
    }

    private void OnAfterInteract(Entity<TagComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || args.Target == null || !args.CanReach || !_tags.HasTag(ent.Owner, "Soap"))
            return;

        var target = args.Target.Value;
        if (!HasComp<SiriusDirtyComponent>(target))
            return;

        // #Sirius Fix: accept soap-on-character before SurgeryCleanSystem, while leaving
        // soap-on-item to the sanitizer. TryWash reports the real reason for failed lathering.
        args.Handled = true;
        TryWash(ent, args.User, target);
    }

    private bool TryWash(EntityUid soap, EntityUid user, EntityUid target)
    {
        if (!HasComp<SiriusDirtyComponent>(target))
            return false;

        // #Sirius Add: only a wet character can be lathered.
        if (!HasComp<WetComponent>(target))
        {
            ShowFailure(user, target, "sirius-soap-need-wet-self", "sirius-soap-need-wet-target");
            return false;
        }

        if (!TryComp<SolutionContainerManagerComponent>(soap, out var manager) ||
            !_solutions.TryGetSolution((soap, manager), SoapSolution, out var solution))
            return false;

        if (solution.Value.Comp.Solution.GetTotalPrototypeQuantity(SoapReagent) < FixedPoint2.New(UseUnits))
        {
            _popup.PopupEntity(Loc.GetString("sirius-soap-not-enough"), target, user);
            return false;
        }

        _solutions.RemoveReagent(solution.Value, SoapReagent, FixedPoint2.New(UseUnits));

        var scented = IsScented(soap);
        var comp = Comp<SiriusDirtyComponent>(target);
        var rate = scented ? comp.ScentedSoapPerSecond : comp.PlainSoapPerSecond;
        var cap = scented ? comp.ScentedSoapCap : comp.PlainSoapCap;
        _dirty.ApplyWashed(target, rate, cap, TimeSpan.FromSeconds(30));

        // #Sirius Add: describe the lathering instead of leaving the sanitizer's "nothing to clean" message.
        if (user == target)
        {
            _popup.PopupEntity(Loc.GetString("sirius-soap-lather-self"), target, user);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("sirius-soap-lather-target", ("target", target)), target, user);

            var gender = TryComp<HumanoidAppearanceComponent>(user, out var appearance)
                ? appearance.Gender.ToString().ToLowerInvariant()
                : "epicene";
            _popup.PopupEntity(
                Loc.GetString("sirius-soap-lather-other",
                    ("user", user),
                    ("target", target),
                    ("gender", gender)),
                target,
                Filter.PvsExcept(user, entityManager: EntityManager),
                true);
        }

        return true;
    }

    private void ShowFailure(EntityUid user, EntityUid target, string selfKey, string targetKey)
    {
        if (user == target)
            _popup.PopupEntity(Loc.GetString(selfKey), target, user);
        else
            _popup.PopupEntity(Loc.GetString(targetKey, ("target", target)), target, user);
    }

    private bool IsScented(EntityUid soap)
    {
        var id = MetaData(soap).EntityPrototype?.ID ?? string.Empty;
        return ScentedSoaps.Any(scent => scent.Soap == id);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SiriusSoapSolidifyingComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            comp.Elapsed += frameTime;

            if (!TryComp<SolutionContainerManagerComponent>(uid, out var manager))
                continue;

            var solidified = false;
            foreach (var (_, solutionEntity) in _solutions.EnumerateSolutions((uid, manager)).ToArray())
            {
                var solution = solutionEntity.Comp.Solution;
                if (solution.GetTotalPrototypeQuantity(SoapReagent) <= FixedPoint2.Zero)
                    continue;

                // #Sirius Add: no engine cooling exists for solutions, so cool it ourselves.
                if (solution.Temperature > 293.15f)
                    _solutions.SetTemperature(solutionEntity,
                        MathF.Max(293.15f, solution.Temperature - comp.CoolingPerSecond * frameTime));

                if (solution.Temperature > comp.SolidifyTemperature && comp.Elapsed < comp.MaxCoolingTime)
                    continue;

                Solidify(uid, solutionEntity, comp);
                solidified = true;
            }

            if (solidified || comp.Elapsed >= comp.MaxCoolingTime)
                RemComp<SiriusSoapSolidifyingComponent>(uid);
        }
    }

    private void Solidify(EntityUid uid, Entity<SolutionComponent> solutionEntity,
        SiriusSoapSolidifyingComponent comp)
    {
        var solution = solutionEntity.Comp.Solution;
        var soapAmount = solution.GetTotalPrototypeQuantity(SoapReagent).Float();
        if (soapAmount <= 0f)
            return;

        var target = PlainSoap;
        foreach (var scent in ScentedSoaps)
        {
            if (solution.GetTotalPrototypeQuantity(scent.Reagent) <= FixedPoint2.Zero)
                continue;

            target = scent.Soap;
            _solutions.RemoveReagent(solutionEntity, scent.Reagent,
                solution.GetTotalPrototypeQuantity(scent.Reagent));
            break;
        }

        _solutions.RemoveReagent(solutionEntity, SoapReagent,
            solution.GetTotalPrototypeQuantity(SoapReagent));

        var coordinates = _transform.GetMapCoordinates(uid);
        var remaining = soapAmount;
        while (remaining > 0f)
        {
            var portion = MathF.Min(remaining, comp.BarVolume);
            var bar = Spawn(target, coordinates);

            if (TryComp<SolutionContainerManagerComponent>(bar, out var barManager) &&
                _solutions.TryGetSolution((bar, barManager), SoapSolution, out var barSolution))
                _solutions.TryAddReagent(barSolution.Value, SoapReagent, FixedPoint2.New(portion));

            remaining -= portion;
        }
    }
}
