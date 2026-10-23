using Content.Server._Sirius.WeaponAssemblyStation;
using Content.Server.DoAfter;
using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Mind;
using Content.Shared.Power;
using Content.Shared.Roles.Jobs;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Content.Shared._Misfits.Special;
using Content.Shared._Misfits.Special.Components;
using Content.Shared._Sirius.WeaponAssemblyStation;
using Content.Shared._Sirius.Weapons.Durability;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;

namespace Content.Server._Sirius.WeaponAssemblyStation;

public sealed partial class WeaponAssemblyStationSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedSpecialSystem _special = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly PowerReceiverSystem _power = default!;
    [Dependency] private readonly SharedPointLightSystem _light = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private static readonly SoundPathSpecifier BoltSound = new("/Audio/Items/ratchet.ogg");
    private const string AnchoringQuality = "Anchoring";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WeaponAssemblyStationComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<WeaponAssemblyStationComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<WeaponAssemblyStationComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<WeaponAssemblyStationComponent, BoundUIClosedEvent>(OnUiClosed);
        SubscribeLocalEvent<WeaponAssemblyStationComponent, ActivatableUIOpenAttemptEvent>(OnOpenAttempt);
        SubscribeLocalEvent<WeaponAssemblyStationComponent, WeaponAssemblyUiButtonPressedMessage>(OnButtonPressed);
        // #Misfits Fix - these must be component subscriptions. A do-after raises
        // its event as a directed event with Broadcast == false, which only reaches
        // component handlers; a global SubscribeLocalEvent<T> is a broadcast
        // subscription and never fired, leaving IsBusy stuck true and the station
        // clamped shut.
        SubscribeLocalEvent<WeaponAssemblyStationComponent, WeaponAssemblyDoAfterEvent>(OnAssemblyDoAfter);
        SubscribeLocalEvent<WeaponAssemblyStationComponent, WeaponRepairDoAfterEvent>(OnRepairDoAfter);
        SubscribeLocalEvent<WeaponAssemblyStationComponent, GetVerbsEvent<InteractionVerb>>(OnGetVerbs);
        SubscribeLocalEvent<WeaponAssemblyStationComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs);
        SubscribeLocalEvent<WeaponAssemblyStationComponent, EntInsertedIntoContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<WeaponAssemblyStationComponent, EntRemovedFromContainerMessage>(OnContainerChanged);
        // #Misfits Add - refilling or draining the fuel solution has to repaint
        // the UI gauge while the window is open.
        SubscribeLocalEvent<WeaponAssemblyStationComponent, SolutionContainerChangedEvent>(OnFuelChanged);
    }

    private void OnFuelChanged(EntityUid uid, WeaponAssemblyStationComponent comp, ref SolutionContainerChangedEvent args)
    {
        if (args.SolutionId != comp.FuelSolutionName)
            return;

        UpdateUi(uid, comp);
    }

    private void OnInit(EntityUid uid, WeaponAssemblyStationComponent comp, ComponentInit args)
    {
        comp.MainContainer = _container.EnsureContainer<ContainerSlot>(uid, WeaponAssemblyStationComponent.MainSlotId);
        comp.Part1Container = _container.EnsureContainer<ContainerSlot>(uid, WeaponAssemblyStationComponent.Part1SlotId);
        comp.Part2Container = _container.EnsureContainer<ContainerSlot>(uid, WeaponAssemblyStationComponent.Part2SlotId);
        comp.Part3Container = _container.EnsureContainer<ContainerSlot>(uid, WeaponAssemblyStationComponent.Part3SlotId);
        comp.Part4Container = _container.EnsureContainer<ContainerSlot>(uid, WeaponAssemblyStationComponent.Part4SlotId);
        comp.Part6Container = _container.EnsureContainer<ContainerSlot>(uid, WeaponAssemblyStationComponent.Part6SlotId);
    }

    private void OnPowerChanged(EntityUid uid, WeaponAssemblyStationComponent comp, ref PowerChangedEvent args)
    {
        comp.Powered = args.Powered;
        UpdateAppearance(uid, comp);
        UpdateUi(uid, comp);
        if (!args.Powered)
        {
            comp.IsBusy = false;
        }

        UpdatePowerLoad(uid, comp);
    }

    private void UpdatePowerLoad(EntityUid uid, WeaponAssemblyStationComponent comp)
    {
        // #Misfits Add - draw idle power when waiting, active power while operating.
        if (TryComp<ApcPowerReceiverComponent>(uid, out var receiver))
            _power.SetLoad(receiver, comp.IsBusy ? comp.ActivePowerUsage : comp.IdlePowerUsage);
    }

    private void OnUiOpened(EntityUid uid, WeaponAssemblyStationComponent comp, ref BoundUIOpenedEvent args)
    {
        UpdateUi(uid, comp);
    }

    private void OnUiClosed(EntityUid uid, WeaponAssemblyStationComponent comp, ref BoundUIClosedEvent args)
    {
    }

    private void OnOpenAttempt(EntityUid uid, WeaponAssemblyStationComponent comp, ActivatableUIOpenAttemptEvent args)
    {
        if (!comp.Powered)
        {
            args.Cancel();
            return;
        }

        if (CanUseStation(comp, args.User))
            return;

        args.Cancel();
        _popup.PopupEntity(Loc.GetString("construction-system-construct-too-low-intelligence"), uid, args.User);
    }

    private bool CanUseStation(WeaponAssemblyStationComponent comp, EntityUid user)
    {
        // #Misfits Add - Expert jobs (wasteland mechanics, NCR engineers, BoS scribes) bypass the
        // intelligence requirement; everyone else needs enough Intelligence to use the station.
        if (IsExemptJob(comp, user))
            return true;

        // Ghosts (admin ghosts included) never carry SPECIAL, so don't lock them out.
        if (!_special.UsesSpecialStats(user))
            return true;

        return TryComp<SpecialComponent>(user, out var special) &&
               _special.GetEffective(user, SpecialStat.Intelligence, special) > comp.IntelligenceThreshold;
    }

    private bool IsExemptJob(WeaponAssemblyStationComponent comp, EntityUid user)
    {
        if (comp.ExemptJobs.Count == 0)
            return false;

        if (!_mind.TryGetMind(user, out var mindId, out _))
            return false;

        return _jobs.MindTryGetJob(mindId, out _, out var job) && comp.ExemptJobs.Contains(job.ID);
    }

    private void OnButtonPressed(EntityUid uid, WeaponAssemblyStationComponent comp, WeaponAssemblyUiButtonPressedMessage args)
    {
        if (comp.IsBusy)
            return;

        switch (args.Button)
        {
            case WeaponAssemblyUiButton.Bolt:
                ToggleBolt(uid, comp, args.Actor);
                break;
            case WeaponAssemblyUiButton.Disassemble:
                TryDisassemble(uid, comp, args.Actor);
                break;
            case WeaponAssemblyUiButton.Assemble:
                TryAssemble(uid, comp, args.Actor);
                break;
            case WeaponAssemblyUiButton.RepairBody:
                TryRepair(uid, comp, args.Actor, 0);
                break;
            case >= WeaponAssemblyUiButton.RepairPart1 and <= WeaponAssemblyUiButton.RepairPart4:
                TryRepair(uid, comp, args.Actor, (int) args.Button - (int) WeaponAssemblyUiButton.RepairPart1 + 1);
                break;
            case WeaponAssemblyUiButton.Close:
                _ui.CloseUi(uid, WeaponAssemblyStationUiKey.Key);
                break;
        }
    }

    private void OnContainerChanged(EntityUid uid, WeaponAssemblyStationComponent comp, ContainerModifiedMessage args)
    {
        // #Misfits Fix - every slot has to push a fresh UI state, not just the
        // main one: otherwise the client keeps drawing a part that was taken
        // out and keeps showing an empty slot for a part that was put in.
        if (args.Container.ID == WeaponAssemblyStationComponent.MainSlotId && !comp.IsBusy)
        {
            // #Misfits Change - seating a weapon no longer bolts it by itself.
            // The player has to clamp it down (button or verb) before the station
            // will strip or assemble anything, which is what the lock tracks.
            comp.IsBolted = false;
            _itemSlots.SetLock(uid, WeaponAssemblyStationComponent.MainSlotId, false);
        }

        UpdateUi(uid, comp);
    }

    // #Misfits Add - plain context-menu verb so the weapon can be bolted/unbolted
    // without first picking up a wrench.
    private void OnGetVerbs(EntityUid uid, WeaponAssemblyStationComponent comp, GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        if (comp.MainContainer.ContainedEntity == null)
            return;

        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString(comp.IsBolted
                ? "misfits-weapon-station-unbolt"
                : "misfits-weapon-station-bolt"),
            Priority = 1,
            Act = () => ToggleBolt(uid, comp, args.User),
            DoContactInteraction = false,
        });
    }

    // #Misfits Add - holding a wrench keeps the short alt-click shortcut.
    private void OnGetAlternativeVerbs(EntityUid uid, WeaponAssemblyStationComponent comp, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess || args.Hands == null)
            return;

        if (comp.MainContainer.ContainedEntity == null)
            return;

        if (args.Using is not { } used ||
            !TryComp<ToolComponent>(used, out var tool) ||
            !_tool.HasQuality(used, AnchoringQuality, tool))
        {
            return;
        }

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(comp.IsBolted
                ? "misfits-weapon-station-unbolt"
                : "misfits-weapon-station-bolt"),
            Priority = 1,
            Act = () => ToggleBolt(uid, comp, args.User),
        });
    }

    public bool ToggleBolt(EntityUid uid, WeaponAssemblyStationComponent comp, EntityUid? user)
    {
        if (comp.MainContainer.ContainedEntity is not { } weapon)
        {
            if (user is { } noWeaponUser)
                _popup.PopupEntity(Loc.GetString("misfits-weapon-station-need-weapon"), uid, noWeaponUser);

            return false;
        }

        comp.IsBolted = !comp.IsBolted;

        // #Misfits Add - bolting is what physically holds the weapon in the main slot.
        _itemSlots.SetLock(uid, WeaponAssemblyStationComponent.MainSlotId, comp.IsBolted);

        _audio.PlayPvs(BoltSound, uid);

        if (user is { } userUid)
            _popup.PopupEntity(
                Loc.GetString(comp.IsBolted ? "misfits-weapon-station-bolted" : "misfits-weapon-station-unbolted", ("weapon", weapon)),
                uid,
                userUid);

        UpdateUi(uid, comp);
        return true;
    }

    public void UpdateUi(EntityUid uid, WeaponAssemblyStationComponent comp)
    {
        if (!_ui.HasUi(uid, WeaponAssemblyStationUiKey.Key))
            return;

        // #Misfits Add - ship the slot contents so the client can render the formation.
        var parts = new NetEntity?[4];
        parts[0] = comp.Part1Container.ContainedEntity is { } part1 ? GetNetEntity(part1) : null;
        parts[1] = comp.Part2Container.ContainedEntity is { } part2 ? GetNetEntity(part2) : null;
        parts[2] = comp.Part3Container.ContainedEntity is { } part3 ? GetNetEntity(part3) : null;
        parts[3] = comp.Part4Container.ContainedEntity is { } part4 ? GetNetEntity(part4) : null;

        // #Misfits Add - exact durability numbers for the status panels.
        var partCurrent = new float[4];
        var partMax = new float[4];
        var partDeferred = new float[4];

        for (var i = 0; i < 4; i++)
        {
            if (parts[i] is not { } netPart ||
                !TryGetEntity(netPart, out var partUid) ||
                !TryComp<WeaponPartDurabilityComponent>(partUid, out var partDurability))
            {
                continue;
            }

            partCurrent[i] = partDurability.Current;
            partMax[i] = partDurability.Max;
            partDeferred[i] = partDurability.Deferred;
        }

        var weaponCurrent = 0f;
        var weaponMax = 0f;

        if (comp.MainContainer.ContainedEntity is { } weapon &&
            TryComp<WeaponDurabilityComponent>(weapon, out var weaponDurability))
        {
            // A whole weapon reports its cached total, a bare frame its own health.
            weaponCurrent = weaponDurability.Modular ? weaponDurability.FrameCurrent : weaponDurability.Total;
            weaponMax = weaponDurability.Modular ? weaponDurability.FrameMax : weaponDurability.TotalMax;
        }

        // #Misfits Add - the internal fuel reservoir, so the UI can draw its gauge.
        var fuel = 0f;
        var fuelMax = 0f;
        if (_solutions.TryGetSolution(uid, comp.FuelSolutionName, out _, out var fuelSolution))
        {
            fuel = fuelSolution.GetReagentQuantity(new ReagentId(comp.FuelReagent, null)).Float();
            fuelMax = fuelSolution.MaxVolume.Float();
        }

        _ui.SetUiState(uid, WeaponAssemblyStationUiKey.Key,
            new WeaponAssemblyBoundUserInterfaceState(
                comp.Powered,
                comp.IsBusy,
                comp.IsBolted,
                comp.MainContainer.ContainedEntity is { } weaponEntity ? GetNetEntity(weaponEntity) : null,
                comp.Part6Container.ContainedEntity is { } magazine ? GetNetEntity(magazine) : null,
                parts,
                RepairAvailability(comp, out var bodyRepairIsWeapon),
                bodyRepairIsWeapon,
                partCurrent,
                partMax,
                partDeferred,
                weaponCurrent,
                weaponMax,
                fuel,
                fuelMax));
    }

    public void UpdateAppearance(EntityUid uid, WeaponAssemblyStationComponent comp)
    {
        if (!TryComp<AppearanceComponent>(uid, out var appearance))
            return;
        _appearance.SetData(uid, WeaponAssemblyStationComponent.WeaponAssemblyVisuals.IsOn, comp.Powered);
        _light.SetEnabled(uid, comp.Powered);
    }
}