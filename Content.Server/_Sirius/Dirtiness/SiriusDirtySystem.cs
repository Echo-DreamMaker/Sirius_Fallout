using Content.Server.Body.Components;
using Content.Server.Mood;
using Content.Shared._Misfits.Special;
using Content.Shared._Sirius.Dirtiness;
using Content.Shared._Sirius.Wet;
using Content.Shared.Alert;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Examine;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Mood;
using Content.Shared.Showers;
using Content.Shared.StatusEffect;
using Content.Shared.FixedPoint;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Sirius.Dirtiness;

// #Sirius Add: drives the cleanliness scale, its HUD icon, examine text and SPECIAL modifier.
public sealed class SiriusDirtySystem : EntitySystem
{
    public const string WetKey = "Wet";
    public const string WashedKey = "SiriusWashed";
    private const string CharismaSource = "SiriusDirty";

    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly SharedSpecialSystem _special = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

    private enum WaterKind : byte
    {
        None,
        Clean,
        Dirty,
        Sewage,
        Radioactive,
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SiriusDirtyComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<SiriusDirtyComponent, ExaminedEvent>(OnExamined);
    }

    private void OnStartup(Entity<SiriusDirtyComponent> ent, ref ComponentStartup args)
    {
        // #Sirius Add: every spawn (roundstart or late join) starts at 30 "clean" points.
        if (ent.Comp.Cleanliness <= 0)
            ent.Comp.Cleanliness = ent.Comp.StartingCleanliness;

        ent.Comp.State = GetState(ent.Comp.Cleanliness);
        _alerts.ShowAlert(ent, ent.Comp.DirtyAlert, (short) ent.Comp.State);
        ApplyCharisma(ent);
        ApplySmellMood(ent, self: true, ent.Comp.State);
        Dirty(ent.Owner, ent.Comp);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SiriusDirtyComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Accumulator < 1f)
            {
                comp.Accumulator += frameTime;
                continue;
            }

            var elapsed = comp.Accumulator;
            comp.Accumulator = 0f;
            var ent = (uid, comp);
            Tick(ent, elapsed);
            UpdateOtherSmellMood(ent);
        }
    }

    private void Tick(Entity<SiriusDirtyComponent> ent, float dt)
    {
        var comp = ent.Comp;
        var cleanliness = comp.Cleanliness;
        var decayMultiplier = 1f;
        var regen = 0f;
        var cap = comp.WaterCleanCap;

        // Bleeding speeds up the loss 2..4 times depending on its severity.
        if (TryComp<BloodstreamComponent>(ent, out var blood) && blood.BleedAmount > 0)
        {
            var severity = Math.Clamp(blood.BleedAmount / blood.MaxBleedAmount, 0f, 1f);
            decayMultiplier = comp.BleedDecayMultiplierMin +
                              (comp.BleedDecayMultiplierMax - comp.BleedDecayMultiplierMin) * severity;
        }

        switch (GetWaterKind(ent))
        {
            case WaterKind.Clean:
                regen += comp.WaterCleanPerMinute / 60f;
                MakeWet(ent, comp.WaterWetDuration);
                break;
            case WaterKind.Dirty:
                decayMultiplier = MathF.Max(decayMultiplier, comp.DirtyWaterDecayMultiplier);
                MakeWet(ent, comp.WaterWetDuration);
                break;
            case WaterKind.Sewage:
            case WaterKind.Radioactive:
                decayMultiplier = MathF.Max(decayMultiplier, comp.SewageDecayMultiplier);
                MakeWet(ent, comp.WaterWetDuration);
                break;
        }

        if (IsInActiveShower(ent, comp))
        {
            regen += comp.ShowerCleanPerSecond;
            cap = MathF.Max(cap, comp.ShowerCleanCap);
            MakeWet(ent, comp.WaterWetDuration);
        }

        if (TryComp<SiriusWashedComponent>(ent, out var washed))
        {
            regen += washed.Rate;
            cap = MathF.Max(cap, washed.Cap);
        }

        // #Sirius Add: ghoul nature doubles ordinary decay (and any bleeding/water penalty on top of it).
        if (TryComp<HumanoidAppearanceComponent>(ent, out var appearance))
        {
            decayMultiplier = ApplySpeciesDecayMultiplier(appearance.Species, decayMultiplier,
                comp.GhoulDecayMultiplier);
        }

        // #Sirius Add: cleanliness perks stack with the other decay modifiers.
        if (HasComp<SiriusPerkCleanFreakComponent>(ent))
            decayMultiplier *= 0.5f;
        if (HasComp<SiriusPerkStinkyComponent>(ent))
            decayMultiplier *= 3f;

        // #Sirius Add: worn clothes make washing harder. Penalties add up across slots.
        if (regen > 0f)
        {
            regen *= 1f - ClothingWashPenalty(ent.Owner);
            cleanliness = MathF.Min(cleanliness + regen * dt, cap);
        }
        else
            cleanliness -= comp.DecayPerMinute / 60f * decayMultiplier * dt;

        comp.Cleanliness = Math.Clamp(cleanliness, 0f, comp.MaxCleanliness);

        UpdateState(ent);
    }

    private void UpdateState(Entity<SiriusDirtyComponent> ent)
    {
        var state = GetState(ent.Comp.Cleanliness);
        if (state == ent.Comp.State)
            return;

        ent.Comp.State = state;
        _alerts.ShowAlert(ent, ent.Comp.DirtyAlert, (short) state);
        ApplyCharisma(ent);
        // #Sirius Add: a status update overwrites the "own smell" mood slot.
        ApplySmellMood(ent, self: true, state);
        Dirty(ent.Owner, ent.Comp);
    }

    private void ApplyCharisma(Entity<SiriusDirtyComponent> ent)
    {
        _special.ClearTemporaryModifiers(ent, source: CharismaSource);
        var modifier = CharismaModifier(ent.Comp.State);
        if (modifier != 0)
            _special.TryModifyTemporary(ent, SpecialStat.Charisma, modifier, source: CharismaSource);

        ent.Comp.PreviousCharismaModifier = modifier;
    }

    /// <summary>
    /// #Sirius Add: mood effect ID for one of the two smell slots and a cleanliness state.
    /// </summary>
    private static string SmellMoodId(bool self, SiriusDirtyState state)
    {
        var slot = self ? "Self" : "Others";
        var name = state switch
        {
            SiriusDirtyState.Animal => "Animal",
            SiriusDirtyState.VeryDirty => "VeryDirty",
            SiriusDirtyState.Dirty => "Dirty",
            SiriusDirtyState.SlightlySmelly => "SlightlySmelly",
            SiriusDirtyState.RelativelyClean => "RelativelyClean",
            SiriusDirtyState.Clean => "Clean",
            _ => "Fragrant",
        };
        return $"SiriusSmell{slot}{name}";
    }

    /// <summary>
    /// #Sirius Add: put a smell effect into one of the two slots. Only a real change is sent, so
    /// MoodSystem's per-effect timers do not pile up and expire the mood early.
    /// </summary>
    private void ApplySmellMood(Entity<SiriusDirtyComponent> ent, bool self, SiriusDirtyState state)
    {
        if (!HasComp<MoodComponent>(ent.Owner))
            return;

        var protoId = SmellMoodId(self, state);
        var lastId = self ? ent.Comp.LastSelfSmellEffect : ent.Comp.LastOthersSmellEffect;
        if (lastId == protoId)
            return;

        if (lastId != null)
            RaiseLocalEvent(ent.Owner, new MoodRemoveEffectEvent(lastId));

        if (self)
            ent.Comp.LastSelfSmellEffect = protoId;
        else
            ent.Comp.LastOthersSmellEffect = protoId;

        // #Sirius Add: chronic rhinitis blocks smell mood, but the Animal state still gets through.
        if (HasComp<SiriusPerkChronicRhinitisComponent>(ent.Owner) && state != SiriusDirtyState.Animal)
            return;

        // #Sirius Add: a keen nose doubles the mood from every smell. EffectModifier only applies
        // to uncategorised mood effects, which is why these prototypes have no category.
        var modifier = HasComp<SiriusPerkKeenSmellComponent>(ent.Owner) ? 2f : 1f;
        RaiseLocalEvent(ent.Owner, new MoodEffectEvent(protoId, effectModifier: modifier));
    }

    private void ClearSmellMood(Entity<SiriusDirtyComponent> ent, bool self)
    {
        var lastId = self ? ent.Comp.LastSelfSmellEffect : ent.Comp.LastOthersSmellEffect;
        if (lastId == null)
            return;

        RaiseLocalEvent(ent.Owner, new MoodRemoveEffectEvent(lastId));
        if (self)
            ent.Comp.LastSelfSmellEffect = null;
        else
            ent.Comp.LastOthersSmellEffect = null;
    }

    /// <summary>
    /// #Sirius Add: the closest other scented character in radius owns the "surrounding smell" slot.
    /// A new source (or a change of their state) overwrites the previous effect.
    /// </summary>
    private void UpdateOtherSmellMood(Entity<SiriusDirtyComponent> ent)
    {
        if (!TryComp<TransformComponent>(ent.Owner, out var xform) || xform.MapID == MapId.Nullspace)
            return;

        EntityUid? source = null;
        var sourceState = SiriusDirtyState.Animal;
        var bestDistance = float.MaxValue;
        var position = _transform.GetWorldPosition(xform);

        var query = EntityQueryEnumerator<SiriusDirtyComponent, TransformComponent>();
        while (query.MoveNext(out var otherUid, out var otherComp, out var otherXform))
        {
            if (otherUid == ent.Owner || otherXform.MapID != xform.MapID)
                continue;

            var distance = (_transform.GetWorldPosition(otherXform) - position).Length();
            if (distance > ent.Comp.SmellRadius || distance >= bestDistance)
                continue;

            bestDistance = distance;
            source = otherUid;
            sourceState = otherComp.State;
        }

        if (source == ent.Comp.LastSmellSource && sourceState == ent.Comp.LastSmellSourceState)
            return;

        ent.Comp.LastSmellSource = source;
        ent.Comp.LastSmellSourceState = sourceState;

        if (source == null)
            ClearSmellMood(ent, self: false);
        else
            ApplySmellMood(ent, self: false, sourceState);
    }

    /// <summary>
    /// #Sirius Add: additive wash penalty from worn clothing, clamped so regeneration never goes negative.
    /// </summary>
    private float ClothingWashPenalty(EntityUid uid)
    {
        var penalty = 0f;

        void Add(string slot, float value)
        {
            if (_inventory.TryGetSlotEntity(uid, slot, out _))
                penalty += value;
        }

        Add("outerClothing", 0.8f); // верхняя одежда
        Add("jumpsuit", 0.6f);      // внутренняя одежда
        Add("head", 0.2f);          // головной убор
        Add("shoes", 0.1f);         // обувь
        Add("back", 0.1f);          // рюкзак
        Add("neck", 0.2f);          // vanity misc: плащ, шарф и т.п.
        Add("gloves", 0.1f);        // перчатки

        return Math.Clamp(penalty, 0f, 1f);
    }

    private void OnExamined(Entity<SiriusDirtyComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString(SiriusDirtyStateLoc.Key(ent.Comp.State)));
    }

    /// <summary>
    /// #Sirius Add: apply or refresh the Wet status effect. Water and showers keep it at one minute.
    /// </summary>
    public void MakeWet(EntityUid uid, TimeSpan duration, bool accumulate = false)
    {
        if (!_status.HasStatusEffect(uid, WetKey))
        {
            _status.TryAddStatusEffect<WetComponent>(uid, WetKey, duration, refresh: true);
            return;
        }

        if (!accumulate)
        {
            _status.TrySetTime(uid, WetKey, duration);
            return;
        }

        if (!_status.TryGetTime(uid, WetKey, out var timer))
            return;

        // #Sirius Add: a splash lasts 2 seconds per unit of water, capped at one minute.
        var total = timer.Value.Item2 - _timing.CurTime + duration;
        var cap = TimeSpan.FromMinutes(1);
        _status.TrySetTime(uid, WetKey, total > cap ? cap : total);
    }

    /// <summary>
    /// #Sirius Add: apply the washing effect. Scented soap cleans faster and reaches 40 instead of 35.
    /// </summary>
    public void ApplyWashed(EntityUid uid, float rate, float cap, TimeSpan duration)
    {
        _status.TryAddStatusEffect(uid, WashedKey, duration, refresh: true, new SiriusWashedComponent());

        if (TryComp<SiriusWashedComponent>(uid, out var washed))
        {
            washed.Rate = rate;
            washed.Cap = cap;
        }
    }

    private bool IsInActiveShower(EntityUid uid, SiriusDirtyComponent comp)
    {
        if (!TryComp<TransformComponent>(uid, out var xform))
            return false;

        var showerQuery = EntityQueryEnumerator<ShowerComponent>();
        while (showerQuery.MoveNext(out var showerUid, out var shower))
        {
            if (!shower.ToggleShower || !TryComp<TransformComponent>(showerUid, out var showerXform))
                continue;

            if (showerXform.MapID != xform.MapID)
                continue;

            if ((_transform.GetWorldPosition(showerXform) - _transform.GetWorldPosition(xform)).Length() <= comp.ShowerRange)
                return true;
        }

        return false;
    }

    private WaterKind GetWaterKind(EntityUid uid)
    {
        if (!TryComp<PhysicsComponent>(uid, out var body))
            return WaterKind.None;

        foreach (var other in _physics.GetContactingEntities(uid, body))
        {
            if (!TryComp<SolutionContainerManagerComponent>(other, out var manager))
                continue;

            var prototypeId = MetaData(other).EntityPrototype?.ID ?? string.Empty;

            foreach (var (_, solutionEntity) in _solutions.EnumerateSolutions((other, manager)))
            {
                var solution = solutionEntity.Comp.Solution;

                if (solution.GetTotalPrototypeQuantity("WaterIrradiated") > FixedPoint2.Zero)
                    return WaterKind.Radioactive;

                if (solution.GetTotalPrototypeQuantity("WaterSalt") > FixedPoint2.Zero)
                    return WaterKind.Dirty;

                if (solution.GetTotalPrototypeQuantity("WaterDirty") > FixedPoint2.Zero)
                    return prototypeId.Contains("Sewer", StringComparison.OrdinalIgnoreCase)
                        ? WaterKind.Sewage
                        : WaterKind.Dirty;

                if (solution.GetTotalPrototypeQuantity("Water") > FixedPoint2.Zero)
                    return WaterKind.Clean;
            }
        }

        return WaterKind.None;
    }

    public static SiriusDirtyState GetState(float cleanliness) => cleanliness switch
    {
        <= 5f => SiriusDirtyState.Animal,
        <= 10f => SiriusDirtyState.VeryDirty,
        <= 15f => SiriusDirtyState.Dirty,
        <= 20f => SiriusDirtyState.SlightlySmelly,
        <= 25f => SiriusDirtyState.RelativelyClean,
        <= 30f => SiriusDirtyState.Clean,
        _ => SiriusDirtyState.Fragrant,
    };

    private static int CharismaModifier(SiriusDirtyState state) => state switch
    {
        SiriusDirtyState.Animal => -3,
        SiriusDirtyState.VeryDirty => -2,
        SiriusDirtyState.Dirty => -1,
        SiriusDirtyState.Clean => 1,
        SiriusDirtyState.Fragrant => 2,
        _ => 0,
    };

    public static float ApplySpeciesDecayMultiplier(string? species, float multiplier, float ghoulMultiplier)
    {
        return SiriusDirtySpecies.IsGhoul(species) ? multiplier * ghoulMultiplier : multiplier;
    }
}
