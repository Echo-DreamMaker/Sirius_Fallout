// #Misfits Add - Assembly and disassembly half of the weapon assembly station.
//
// The modular weapon system's own assembly path is a construction graph whose
// edges the station deliberately does not walk: it matches the seated frame and
// parts against the generated weaponAssemblyRecipe table instead, so a finished
// weapon appears or disappears in a single swap. Both directions keep the
// station's working slots and the weapon's own construction-owned craft_*
// containers in sync through one table, CraftContainerToSlot, which is also what
// makes the laser frames (whose first slot is "optics" and whose frame-level
// part is a "drive") land in the same visible slots as rifles.
using System.Diagnostics.CodeAnalysis;
using Content.Server._Sirius.Weapons.Parts;
using Content.Server.Construction.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared._Sirius.WeaponAssemblyStation;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._Sirius.WeaponAssemblyStation;

public sealed partial class WeaponAssemblyStationSystem
{
    private const string GunMagazineSlot = "gun_magazine";
    private const string GunChamberSlot = "gun_chamber";
    private const string CraftMagazineContainer = "craft_magazine";

    private static readonly SoundPathSpecifier AssemblySound = new("/Audio/Items/welder.ogg");
    private static readonly SoundPathSpecifier DisassemblySound = new("/Audio/Items/ratchet.ogg");

    private static readonly string[] StationPartSlots =
    {
        WeaponAssemblyStationComponent.Part1SlotId,
        WeaponAssemblyStationComponent.Part2SlotId,
        WeaponAssemblyStationComponent.Part3SlotId,
        WeaponAssemblyStationComponent.Part4SlotId,
    };

    /// <summary>
    /// Which station slot a construction-owned craft_* container feeds. A barrel
    /// and a laser's optics are both the first working slot, a grip and a drive
    /// both the third, and the magazine is not a working slot at all.
    /// </summary>
    private static readonly Dictionary<string, string> CraftContainerToSlot = new()
    {
        ["craft_barrel"] = WeaponAssemblyStationComponent.Part1SlotId,
        ["craft_optics"] = WeaponAssemblyStationComponent.Part1SlotId,
        ["craft_bolt"] = WeaponAssemblyStationComponent.Part2SlotId,
        ["craft_grip"] = WeaponAssemblyStationComponent.Part3SlotId,
        ["craft_drive"] = WeaponAssemblyStationComponent.Part3SlotId,
        ["craft_stock"] = WeaponAssemblyStationComponent.Part4SlotId,
        [CraftMagazineContainer] = WeaponAssemblyStationComponent.Part6SlotId,
    };

    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly TagSystem _tags = default!;

    /// <summary>
    /// UI entry point for the "Разобрать" button: strip the bolted weapon down to
    /// its bare frame, spilling its parts into the working slots.
    /// </summary>
    private void TryDisassemble(EntityUid uid, WeaponAssemblyStationComponent comp, EntityUid user)
    {
        if (comp.IsBusy || !comp.Powered)
            return;

        if (!comp.IsBolted)
        {
            _popup.PopupEntity(Loc.GetString("misfits-weapon-station-locked"), uid, user);
            return;
        }

        if (comp.MainContainer.ContainedEntity is not { } weapon)
        {
            _popup.PopupEntity(Loc.GetString("misfits-weapon-station-need-weapon"), uid, user);
            return;
        }

        // Bolted, unsuitable and forbidden weapons cannot be stripped: only the
        // modular receiver has parts to take out and a frame to go back to.
        if (!HasComp<WeaponFrameComponent>(weapon) || !HasComp<ConstructionComponent>(weapon))
        {
            _popup.PopupEntity(Loc.GetString("misfits-weapon-station-not-modular"), uid, user);
            return;
        }

        if (!TryGetRecipe(weapon, out var recipe))
        {
            _popup.PopupEntity(Loc.GetString("misfits-weapon-station-no-recipe"), uid, user);
            return;
        }

        if (!HasRoomForParts(comp, recipe))
        {
            _popup.PopupEntity(Loc.GetString("misfits-weapon-station-workspace-occupied"), uid, user);
            return;
        }

        StartOperation(uid, comp, user, assemble: false);
    }

    /// <summary>
    /// UI entry point for the "Собрать" button: seat the frame and the four
    /// working slots together into the weapon the table says they form.
    /// </summary>
    private void TryAssemble(EntityUid uid, WeaponAssemblyStationComponent comp, EntityUid user)
    {
        if (comp.IsBusy || !comp.Powered)
            return;

        if (comp.MainContainer.ContainedEntity is not { } frame)
        {
            _popup.PopupEntity(Loc.GetString("misfits-weapon-station-need-weapon"), uid, user);
            return;
        }

        if (!TryMatchFrameAndParts(frame, comp, out _))
        {
            _popup.PopupEntity(Loc.GetString("misfits-weapon-station-invalid-parts"), uid, user);
            return;
        }

        if (!HasFuel(uid, comp, comp.AssembleFuelCost))
        {
            _popup.PopupEntity(Loc.GetString("misfits-weapon-station-no-fuel"), uid, user);
            return;
        }

        StartOperation(uid, comp, user, assemble: true);
    }

    private void StartOperation(EntityUid uid, WeaponAssemblyStationComponent comp, EntityUid user, bool assemble)
    {
        comp.IsBusy = true;

        // The weapon is clamped for the duration of the operation, whatever the
        // bolt state was beforehand.
        _itemSlots.SetLock(uid, WeaponAssemblyStationComponent.MainSlotId, true);

        UpdatePowerLoad(uid, comp);
        UpdateUi(uid, comp);

        _audio.PlayPvs(assemble ? AssemblySound : DisassemblySound, uid);

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user,
            assemble ? comp.AssembleDelay : comp.DisassembleDelay,
            new WeaponAssemblyDoAfterEvent { Assemble = assemble },
            uid,
            target: uid)
        {
            BlockDuplicate = true,
            BreakOnDamage = false,
            BreakOnMove = false,
            DistanceThreshold = null,
            NeedHand = false,
            RequireCanInteract = false,
        });
    }

    // #Misfits Fix - component scoped handler: the do-after raises its event
    // directly on the station, so the component is handed to us.
    private void OnAssemblyDoAfter(EntityUid uid, WeaponAssemblyStationComponent comp, WeaponAssemblyDoAfterEvent args)
    {
        var assembled = args.Assemble;
        if (!args.Cancelled && comp.Powered)
        {
            if (assembled)
                CompleteAssemble(uid, comp, args.User);
            else
                CompleteDisassemble(uid, comp, args.User);
        }

        comp.IsBusy = false;
        _itemSlots.SetLock(uid, WeaponAssemblyStationComponent.MainSlotId, comp.IsBolted);
        UpdatePowerLoad(uid, comp);
        UpdateUi(uid, comp);
    }

    private void CompleteDisassemble(EntityUid uid, WeaponAssemblyStationComponent comp, EntityUid? user)
    {
        if (comp.MainContainer.ContainedEntity is not { } weapon)
        {
            if (user is { } noWeaponUser)
                _popup.PopupEntity(Loc.GetString("misfits-weapon-station-need-weapon"), uid, noWeaponUser);
            return;
        }

        if (!TryGetRecipe(weapon, out var recipe))
        {
            if (user is { } noRecipeUser)
                _popup.PopupEntity(Loc.GetString("misfits-weapon-station-no-recipe"), uid, noRecipeUser);
            return;
        }

        if (!TryComp<ConstructionComponent>(weapon, out var construction))
        {
            if (user is { } notModUser)
                _popup.PopupEntity(Loc.GetString("misfits-weapon-station-not-modular"), uid, notModUser);
            return;
        }

        // Attachments, the chambered round and loose ammunition leave the weapon
        // and land next to the station rather than disappearing with it.
        DropLooseAccessories(uid, weapon);

        foreach (var name in construction.Containers)
        {
            if (name == CraftMagazineContainer)
                continue;

            if (!CraftContainerToSlot.TryGetValue(name, out var slotId))
                continue;

            if (!_container.TryGetContainer(weapon, name, out var source) ||
                source.ContainedEntities.Count == 0)
                continue;

            MoveToStationSlot(comp, slotId, source, source.ContainedEntities[0]);
        }

        // The detachable magazine is staged in the sixth slot, not dropped.
        if (_itemSlots.TryGetSlot(weapon, GunMagazineSlot, out var magazineSlot) &&
            magazineSlot.ContainerSlot?.ContainedEntity is { } magazine)
        {
            MoveToStationSlot(comp, WeaponAssemblyStationComponent.Part6SlotId, magazineSlot.ContainerSlot, magazine);
        }

        var wasBolted = comp.IsBolted;

        _container.Remove(weapon, comp.MainContainer);
        QueueDel(weapon);

        var frame = Spawn(recipe.Frame, Transform(uid).Coordinates);
        _container.Insert(frame, comp.MainContainer);

        comp.IsBolted = wasBolted;

        if (user is { } popupUser)
            _popup.PopupEntity(Loc.GetString("misfits-weapon-station-disassembled"), uid, popupUser);
    }

    private void CompleteAssemble(EntityUid uid, WeaponAssemblyStationComponent comp, EntityUid? user)
    {
        if (comp.MainContainer.ContainedEntity is not { } frame)
            return;

        if (!TryMatchFrameAndParts(frame, comp, out var recipe))
            return;

        if (recipe.Magazine != null &&
            (comp.Part6Container.ContainedEntity is not { } stagedMagazine ||
             !_tags.HasTag(stagedMagazine, recipe.Magazine.Value)))
        {
            if (user is { } magUser)
                _popup.PopupEntity(Loc.GetString("misfits-weapon-station-magazine-required"), uid, magUser);

            return;
        }

        if (!TryConsumeFuel(uid, comp, comp.AssembleFuelCost))
        {
            if (user is { } fuelUser)
                _popup.PopupEntity(Loc.GetString("misfits-weapon-station-no-fuel"), uid, fuelUser);

            return;
        }

        // The frame declares the construction-owned containers the finished
        // weapon has to accept the very same parts into.
        var craftContainers = TryComp<ConstructionComponent>(frame, out var frameConstruction)
            ? frameConstruction.Containers
            : new HashSet<string>();

        var weapon = Spawn(recipe.Result, Transform(uid).Coordinates);

        // A directly spawned weapon is filled with a fresh set of parts and a
        // chambered round; the build takes the seated parts instead.
        foreach (var name in craftContainers)
        {
            var target = _container.TryGetContainer(weapon, name, out var existing)
                ? existing
                : _container.EnsureContainer<Container>(weapon, name);

            _container.CleanContainer(target);
        }

        if (recipe.Magazine != null && comp.Part6Container.ContainedEntity is { } magazine)
        {
            // The free magazine the prototype spawns is replaced outright, the
            // same way a hand-built weapon keeps only the installed one.
            if (_itemSlots.TryGetSlot(weapon, GunMagazineSlot, out var freeMagazineSlot) &&
                freeMagazineSlot.ContainerSlot is { } freeMagazine)
            {
                _container.CleanContainer(freeMagazine);
            }

            _itemSlots.TryInsert(weapon, GunMagazineSlot, magazine, null);
        }

        // A built weapon starts unchambered.
        if (_itemSlots.TryGetSlot(weapon, GunChamberSlot, out var chamberSlot) &&
            chamberSlot.ContainerSlot is { } chamber)
        {
            _container.CleanContainer(chamber);
        }

        foreach (var slotId in StationPartSlots)
        {
            var source = StationSlotContainer(comp, slotId);
            if (source.ContainedEntity is not { } part)
                continue;

            var name = FindCraftContainer(craftContainers, slotId);
            if (name == null)
                continue;

            var target = _container.TryGetContainer(weapon, name, out var existing)
                ? existing
                : _container.EnsureContainer<Container>(weapon, name);

            _container.Remove(part, source);
            _container.Insert(part, target);
        }

        _container.Remove(frame, comp.MainContainer);
        QueueDel(frame);
        _container.Insert(weapon, comp.MainContainer);

        // #Misfits Add - assembled weapon must be bolted by default.
        comp.IsBolted = true;
        _itemSlots.SetLock(uid, WeaponAssemblyStationComponent.MainSlotId, true);

        if (user is { } popupUser)
            _popup.PopupEntity(Loc.GetString("misfits-weapon-station-assembled"), uid, popupUser);
    }

    /// <summary>
    /// The recipe whose id matches this entity's prototype, i.e. the recipe that
    /// produced the weapon sitting in the main slot.
    /// </summary>
    private bool TryGetRecipe(EntityUid weapon, [NotNullWhen(true)] out WeaponAssemblyRecipePrototype? recipe)
    {
        recipe = null;

        if (MetaData(weapon).EntityPrototype is not { } prototype)
            return false;

        return _prototypes.TryIndex(prototype.ID, out recipe);
    }

    /// <summary>
    /// Looks the seated frame and working slots up in the assembly table. Slots
    /// the recipe leaves null have to be empty for the match to count.
    /// </summary>
    private bool TryMatchFrameAndParts(EntityUid frame, WeaponAssemblyStationComponent comp, [NotNullWhen(true)] out WeaponAssemblyRecipePrototype? recipe)
    {
        recipe = null;

        if (MetaData(frame).EntityPrototype is not { } prototype)
            return false;

        var seated = new string?[StationPartSlots.Length];
        for (var i = 0; i < StationPartSlots.Length; i++)
        {
            seated[i] = StationSlotContainer(comp, StationPartSlots[i]).ContainedEntity is { } part
                ? MetaData(part).EntityPrototype?.ID
                : null;
        }

        foreach (var candidate in _prototypes.EnumeratePrototypes<WeaponAssemblyRecipePrototype>())
        {
            if (candidate.Frame.Id != prototype.ID || candidate.Parts.Count != seated.Length)
                continue;

            var matches = true;
            for (var i = 0; i < seated.Length; i++)
            {
                if (candidate.Parts[i]?.Id != seated[i])
                {
                    matches = false;
                    break;
                }
            }

            if (!matches)
                continue;

            recipe = candidate;
            return true;
        }

        return false;
    }

    private bool HasRoomForParts(WeaponAssemblyStationComponent comp, WeaponAssemblyRecipePrototype recipe)
    {
        foreach (var slotId in StationPartSlots)
        {
            if (StationSlotContainer(comp, slotId).ContainedEntity != null)
                return false;
        }

        return recipe.Magazine == null || comp.Part6Container.ContainedEntity == null;
    }

    private void DropLooseAccessories(EntityUid uid, EntityUid weapon)
    {
        var destination = Transform(uid).Coordinates;

        if (TryComp<ItemSlotsComponent>(weapon, out var slots))
        {
            foreach (var slot in slots.Slots.Values)
            {
                var id = slot.ContainerSlot?.ID;

                // The magazine is staged, and the craft_* containers are the
                // parts themselves.
                if (id == null || id == GunMagazineSlot || id.StartsWith("craft_"))
                    continue;

                if (slot.ContainerSlot?.ContainedEntity is not { } item)
                    continue;

                _container.Remove(item, slot.ContainerSlot, destination: destination);
            }
        }

        // Loose ammunition of an internal magazine is not a part either.
        if (TryComp<BallisticAmmoProviderComponent>(weapon, out var ammo))
            _container.EmptyContainer(ammo.Container, destination: destination);
    }

    private void MoveToStationSlot(WeaponAssemblyStationComponent comp, string slotId, BaseContainer source, EntityUid item)
    {
        _container.Remove(item, source);
        _container.Insert(item, StationSlotContainer(comp, slotId));
    }

    private static string? FindCraftContainer(HashSet<string> craftContainers, string slotId)
    {
        foreach (var name in craftContainers)
        {
            if (CraftContainerToSlot.TryGetValue(name, out var mapped) && mapped == slotId)
                return name;
        }

        return null;
    }

    private static ContainerSlot StationSlotContainer(WeaponAssemblyStationComponent comp, string slotId)
    {
        return slotId switch
        {
            WeaponAssemblyStationComponent.Part1SlotId => comp.Part1Container,
            WeaponAssemblyStationComponent.Part2SlotId => comp.Part2Container,
            WeaponAssemblyStationComponent.Part3SlotId => comp.Part3Container,
            WeaponAssemblyStationComponent.Part4SlotId => comp.Part4Container,
            _ => comp.Part6Container,
        };
    }

    private bool HasFuel(EntityUid uid, WeaponAssemblyStationComponent comp, int amount)
    {
        if (!_solutions.TryGetSolution(uid, comp.FuelSolutionName, out _, out var solution))
            return false;

        return solution.GetReagentQuantity(new ReagentId(comp.FuelReagent, null)) >= FixedPoint2.New(amount);
    }

    private bool TryConsumeFuel(EntityUid uid, WeaponAssemblyStationComponent comp, int amount)
    {
        if (!_solutions.TryGetSolution(uid, comp.FuelSolutionName, out var solutionEntity, out var solution))
            return false;

        var reagent = new ReagentId(comp.FuelReagent, null);
        if (solution.GetReagentQuantity(reagent) < FixedPoint2.New(amount))
            return false;

        _solutions.RemoveReagent(solutionEntity.Value, reagent, FixedPoint2.New(amount));
        return true;
    }
}
