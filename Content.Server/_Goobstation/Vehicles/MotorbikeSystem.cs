using Content.Server.Administration;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Repairable;
using Content.Shared.Audio;
using Content.Shared.Buckle;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Movement.Components;
using Content.Shared.Popups;
using Content.Shared.Tools.Systems;
using Content.Shared.Vehicles;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Vehicles;

public sealed class MotorbikeSystem : EntitySystem
{
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly MetaDataSystem _metaSystem = default!;
    [Dependency] private readonly QuickDialogSystem _quickDialog = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MotorbikeComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<MotorbikeComponent, EntInsertedIntoContainerMessage>(OnKeyInserted, after: [typeof(VehicleSystem)]);
        SubscribeLocalEvent<MotorbikeComponent, InteractUsingEvent>(OnInteractUsing, before: [typeof(FlammableSystem), typeof(RepairableSystem)]);
        SubscribeLocalEvent<MotorbikeComponent, SolutionTransferAttemptEvent>(OnSolutionTransferAttempt);
        SubscribeLocalEvent<MotorbikeComponent, MotorbikeRefuelDoAfterEvent>(OnRefuelDoAfter);
        SubscribeLocalEvent<MotorbikeComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<MotorbikeComponent, ExaminedEvent>(OnExamined);

        SubscribeLocalEvent<MotorbikeComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    // Adds a Rename verb to motorbikes, allowing players to give them a custom name.
    private void OnGetVerbs(Entity<MotorbikeComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!TryComp<ActorComponent>(args.User, out var actor))
            return;

        var player = actor.PlayerSession;

        var rename = new Verb
        {
            Text = "Rename",
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/rename.svg.192dpi.png")),
            Act = () =>
            {
                _quickDialog.OpenDialog(player, "Rename", "Name", (string newName) =>
                {
                    _metaSystem.SetEntityName(ent.Owner, newName);
                });
            },
            Impact = LogImpact.Low,
        };

        args.Verbs.Add(rename);
    }


    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<MotorbikeComponent, VehicleComponent>();
        while (query.MoveNext(out var uid, out var motorbike, out var vehicle))
        {
            if (motorbike.Burning)
            {
                if (motorbike.ExplodeAt <= _timing.CurTime)
                    Explode(uid, motorbike);

                continue;
            }

            if (!vehicle.EngineRunning || vehicle.Driver == null)
                continue;

            if (!HasUsableFuel(uid, motorbike))
            {
                StopEngine(uid, vehicle, popup: true);
                continue;
            }

            var fuelToUse = motorbike.FuelUsePerSecond.Float() * frameTime + motorbike.FuelAccumulator;
            var wholeFuel = FixedPoint2.New(MathF.Floor(fuelToUse));
            motorbike.FuelAccumulator = fuelToUse - wholeFuel.Float();

            if (wholeFuel <= FixedPoint2.Zero)
                continue;

            if (!TryConsumeFuel(uid, motorbike, wholeFuel))
                StopEngine(uid, vehicle, popup: true);
        }
    }

    private void OnInit(Entity<MotorbikeComponent> ent, ref ComponentInit args)
    {
        _appearance.SetData(ent, MotorbikeVisuals.Burning, ent.Comp.Burning);
    }

    private void OnKeyInserted(Entity<MotorbikeComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (!TryComp(ent, out VehicleComponent? vehicle) ||
            args.Container.ID != vehicle.KeySlot ||
            !vehicle.EngineRunning)
        {
            return;
        }

        if (ent.Comp.Burning || !HasUsableFuel(ent, ent.Comp))
            StopEngine(ent, vehicle, popup: !ent.Comp.Burning);
    }

    private void OnInteractUsing(Entity<MotorbikeComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || ent.Comp.Burning)
            return;

        if (IsWeldingTool(args.Used))
        {
            if (TryComp<VehicleComponent>(ent, out var vehicle) && vehicle.Driver == args.User)
            {
                _popup.PopupEntity("You can't repair the motorbike while riding it.", ent, args.User);
                args.Handled = true;
                return;
            }

            if (TryComp<DamageableComponent>(ent, out var damageable) &&
                damageable.TotalDamage == 0)
            {
                _popup.PopupEntity("The motorbike is already fully repaired.", ent, args.User);
                args.Handled = true;
            }

            return;
        }

        if (!TryStartRefuelDoAfter(ent, args.Used, args.User))
            return;

        args.Handled = true;
    }

    private void OnSolutionTransferAttempt(Entity<MotorbikeComponent> ent, ref SolutionTransferAttemptEvent args)
    {
        if (args.To != ent.Owner)
            return;

        args.Cancel("Use the container on the motorbike directly to refuel it.");
    }

    private void OnRefuelDoAfter(Entity<MotorbikeComponent> ent, ref MotorbikeRefuelDoAfterEvent args)
    {
        ent.Comp.RefuelDoAfter = null;

        if (args.Handled || args.Cancelled || args.Used is not { } source)
            return;

        if (!IsReadyForRefuel(ent, args.User, popup: true))
            return;

        args.Handled = TryRefuel(ent, source, args.User, ent.Comp.RefillAmount);
    }

    private void OnDamageChanged(Entity<MotorbikeComponent> ent, ref DamageChangedEvent args)
    {
        if (ent.Comp.Burning ||
            args.Damageable.TotalDamage < ent.Comp.MaxIntegrity)
        {
            return;
        }

        StartBurning(ent);
    }

    private void OnExamined(Entity<MotorbikeComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange ||
            !_solution.TryGetSolution(ent.Owner, ent.Comp.FuelSolution, out _, out var fuelSolution))
        {
            return;
        }

        var current = fuelSolution.GetTotalPrototypeQuantity(ent.Comp.FuelReagent);
        args.PushMarkup(Loc.GetString("motorbike-examine-fuel", ("current", current), ("max", fuelSolution.MaxVolume)));
    }

    private bool TryRefuel(Entity<MotorbikeComponent> motorbike, EntityUid source, EntityUid user, FixedPoint2 maxAmount)
    {
        if (!_solution.TryGetDrainableSolution(source, out var sourceSoln, out var sourceSolution) ||
            !_solution.TryGetSolution(motorbike.Owner, motorbike.Comp.FuelSolution, out var fuelSoln, out var fuelSolution))
        {
            return false;
        }

        var availableFuel = sourceSolution.GetTotalPrototypeQuantity(motorbike.Comp.FuelReagent);
        if (availableFuel <= FixedPoint2.Zero)
        {
            _popup.PopupEntity("The container has no welding fuel.", motorbike, user);
            return true;
        }

        if (sourceSolution.Volume != availableFuel)
        {
            _popup.PopupEntity("The motorbike fuel tank rejects contaminated fuel.", motorbike, user);
            return true;
        }

        if (fuelSolution.AvailableVolume <= FixedPoint2.Zero)
        {
            _popup.PopupEntity("The motorbike fuel tank is already full.", motorbike, user);
            return true;
        }

        var amount = FixedPoint2.Min(maxAmount, FixedPoint2.Min(availableFuel, fuelSolution.AvailableVolume));
        if (amount <= FixedPoint2.Zero)
            return false;

        if (!_solution.RemoveReagent(sourceSoln.Value, motorbike.Comp.FuelReagent, amount))
            return false;

        _solution.TryAddReagent(fuelSoln.Value, motorbike.Comp.FuelReagent, amount, out var accepted);
        if (accepted < amount)
            _solution.TryAddReagent(sourceSoln.Value, motorbike.Comp.FuelReagent, amount - accepted, out _);

        if (accepted <= FixedPoint2.Zero)
            return false;

        _audio.PlayPvs(motorbike.Comp.RefillSound, motorbike.Owner);
        _popup.PopupEntity($"You add {accepted} units of welding fuel to the motorbike.", motorbike, user);
        return true;
    }

    private bool TryStartRefuelDoAfter(Entity<MotorbikeComponent> motorbike, EntityUid source, EntityUid user)
    {
        if (!_solution.TryGetDrainableSolution(source, out _, out var sourceSolution) ||
            !_solution.TryGetSolution(motorbike.Owner, motorbike.Comp.FuelSolution, out _, out var fuelSolution))
        {
            return false;
        }

        var availableFuel = sourceSolution.GetTotalPrototypeQuantity(motorbike.Comp.FuelReagent);
        if (availableFuel <= FixedPoint2.Zero)
        {
            _popup.PopupEntity("The container has no welding fuel.", motorbike, user);
            return true;
        }

        if (sourceSolution.Volume != availableFuel)
        {
            _popup.PopupEntity("The motorbike fuel tank rejects contaminated fuel.", motorbike, user);
            return true;
        }

        if (fuelSolution.AvailableVolume <= FixedPoint2.Zero)
        {
            _popup.PopupEntity("The motorbike fuel tank is already full.", motorbike, user);
            return true;
        }

        if (!IsReadyForRefuel(motorbike, user, popup: true))
            return true;

        if (motorbike.Comp.RefuelDoAfter != null)
        {
            _popup.PopupEntity("The motorbike is already being refueled.", motorbike, user);
            return true;
        }

        var doAfterArgs = new DoAfterArgs(EntityManager,
            user,
            motorbike.Comp.RefillDelay,
            new MotorbikeRefuelDoAfterEvent(),
            motorbike.Owner,
            target: motorbike.Owner,
            used: source)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs, out var doAfterId))
            return false;

        motorbike.Comp.RefuelDoAfter = doAfterId;
        _popup.PopupEntity("You begin refueling the motorbike.", motorbike, user);
        return true;
    }

    private bool IsReadyForRefuel(Entity<MotorbikeComponent> motorbike, EntityUid user, bool popup)
    {
        if (!TryComp(motorbike, out VehicleComponent? vehicle))
            return false;

        if (!vehicle.EngineRunning &&
            _itemSlots.GetItemOrNull(motorbike, vehicle.KeySlot) == null)
        {
            return true;
        }

        if (popup)
            _popup.PopupEntity("Remove the key and turn the motorbike off before refueling it.", motorbike, user);

        return false;
    }

    private bool TryConsumeFuel(EntityUid uid, MotorbikeComponent motorbike, FixedPoint2 amount)
    {
        if (!_solution.TryGetSolution(uid, motorbike.FuelSolution, out var fuelSoln, out var fuelSolution))
            return false;

        var available = fuelSolution.GetTotalPrototypeQuantity(motorbike.FuelReagent);
        if (available <= FixedPoint2.Zero)
            return false;

        var consumed = FixedPoint2.Min(amount, available);
        if (!_solution.RemoveReagent(fuelSoln.Value, motorbike.FuelReagent, consumed))
            return false;

        return available > amount;
    }

    private bool HasUsableFuel(EntityUid uid, MotorbikeComponent motorbike)
    {
        return _solution.TryGetSolution(uid, motorbike.FuelSolution, out _, out var fuelSolution) &&
            fuelSolution.GetTotalPrototypeQuantity(motorbike.FuelReagent) > FixedPoint2.Zero;
    }

    private bool IsWeldingTool(EntityUid uid)
    {
        return _tool.HasQuality(uid, "Welding");
    }

    private void StopEngine(EntityUid uid, VehicleComponent vehicle, bool popup = false)
    {
        if (!vehicle.EngineRunning)
            return;

        vehicle.EngineRunning = false;
        Dirty(uid, vehicle);

        _appearance.SetData(uid, VehicleState.Animated, false);
        _ambient.SetAmbience(uid, false);

        if (vehicle.Driver is not { } driver)
            return;

        RemComp<RelayInputMoverComponent>(driver);

        if (popup)
            _popup.PopupEntity("The motorbike sputters out of fuel.", uid, driver);
    }

    private void StartBurning(Entity<MotorbikeComponent> ent)
    {
        ent.Comp.Burning = true;
        ent.Comp.ExplodeAt = _timing.CurTime + ent.Comp.ExplosionDelay;
        Dirty(ent);

        _appearance.SetData(ent, MotorbikeVisuals.Burning, true);
        _audio.PlayPvs(ent.Comp.FuseSound, ent);

        if (TryComp(ent, out VehicleComponent? vehicle))
        {
            vehicle.IsBroken = true;
            StopEngine(ent, vehicle);
        }

        if (TryComp(ent, out FlammableComponent? flammable))
            _flammable.Ignite(ent, ent, flammable, ignoreFireProtection: true);
    }

    private void Explode(EntityUid uid, MotorbikeComponent motorbike)
    {
        if (TryComp<VehicleComponent>(uid, out var vehicle) &&
            vehicle.Driver is { } driver)
        {
            _buckle.Unbuckle((driver, null), null);
        }

        _explosion.QueueExplosion(
            uid,
            motorbike.ExplosionType,
            motorbike.ExplosionTotalIntensity,
            motorbike.ExplosionSlope,
            motorbike.ExplosionMaxTileIntensity);

        QueueDel(uid);
    }
}
