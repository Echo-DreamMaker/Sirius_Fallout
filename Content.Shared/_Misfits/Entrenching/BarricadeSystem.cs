// #Misfits Add - BarricadeSystem: handles entrenching tool interactions and sandbag barricade construction.
// Port of RMC-14 BarricadeSystem — removed: RMCConstructionSystem, SharedWeaponMountSystem, ContentTileDefinition.CanDig.
// Replaced tile check with standard SS14 IsSpace()/Sturdy checks.
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;

namespace Content.Shared._Misfits.Entrenching;

public abstract class SharedBarricadeSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedMapSystem _mapManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Entrenching tool digs ground tiles for empty bags
        SubscribeLocalEvent<EntrenchingToolComponent, AfterInteractEvent>(OnEntrenchingAfterInteract);
        SubscribeLocalEvent<EntrenchingToolComponent, EntrenchingToolDigDoAfterEvent>(OnDigDoAfter);

        // Entrenching tool fills empty bags at a dirt tile
        SubscribeLocalEvent<EmptySandbagComponent, AfterInteractEvent>(OnEmptySandbagInteract);
        SubscribeLocalEvent<EmptySandbagComponent, SandbagFillDoAfterEvent>(OnFillDoAfter);

        // Full sandbag: UseInHand or AfterInteract on tile to build barricade
        SubscribeLocalEvent<FullSandbagComponent, AfterInteractEvent>(OnFullSandbagInteract);
        SubscribeLocalEvent<FullSandbagComponent, UseInHandEvent>(OnFullSandbagUseInHand);
        SubscribeLocalEvent<FullSandbagComponent, SandbagBuildDoAfterEvent>(OnBuildDoAfter);

        // Dismantle barricade with entrenching tool
        SubscribeLocalEvent<BarricadeSandbagComponent, AfterInteractEvent>(OnBarricadeDismantle);
        SubscribeLocalEvent<BarricadeSandbagComponent, SandbagDismantleDoAfterEvent>(OnDismantleDoAfter);
    }

    // ---------- Dig ----------

    private void OnEntrenchingAfterInteract(Entity<EntrenchingToolComponent> tool, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        // Clicking on a barricade sandbag → dismantle, not dig
        if (args.Target != null && HasComp<BarricadeSandbagComponent>(args.Target.Value))
            return;

        // Must click on a valid ground tile
        var coords = args.ClickLocation.ToMap(EntityManager, _xform);
        if (!TryGetSurdyTile(args.ClickLocation, out _))
        {
            _popup.PopupClient(Loc.GetString("entrenching-dig-novacancy"), args.User, args.User);
            return;
        }

        _popup.PopupClient(Loc.GetString("entrenching-dig-start"), args.User, args.User);

        var ev = new EntrenchingToolDigDoAfterEvent();
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, tool.Comp.DigDelay, ev, tool, target: null, used: tool)
        {
            BreakOnMove = true,
            CancelDuplicate = true,
            NeedHand = true
        });

        args.Handled = true;
    }

    private void OnDigDoAfter(Entity<EntrenchingToolComponent> tool, ref EntrenchingToolDigDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;
        args.Handled = true;

        SpawnDigResult(tool, args.User, tool.Comp.LayersPerDig);
        _popup.PopupClient(Loc.GetString("entrenching-dig-finish", ("count", tool.Comp.LayersPerDig)), args.User, args.User);
    }

    // ---------- Fill empty bag ----------

    private void OnEmptySandbagInteract(Entity<EmptySandbagComponent> bag, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        // Must have entrenching tool in hand
        if (!TryComp<EntrenchingToolComponent>(args.Used, out var toolComp))
            return;

        // Must be at a valid diggable tile
        if (!TryGetSurdyTile(args.ClickLocation, out _))
        {
            _popup.PopupClient(Loc.GetString("entrenching-fill-novacancy"), args.User, args.User);
            return;
        }

        _popup.PopupClient(Loc.GetString("entrenching-fill-start"), args.User, args.User);

        var ev = new SandbagFillDoAfterEvent();
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, toolComp.FillDelay, ev, bag, target: bag, used: args.Used)
        {
            BreakOnMove = true,
            CancelDuplicate = true,
            NeedHand = true
        });

        args.Handled = true;
    }

    private void OnFillDoAfter(Entity<EmptySandbagComponent> bag, ref SandbagFillDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;
        args.Handled = true;

        SpawnFilledBag(bag, args.User);
    }

    // ---------- Build barricade ----------

    private void OnFullSandbagUseInHand(Entity<FullSandbagComponent> bag, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;
        StartBuild(bag, args.User, Transform(args.User).Coordinates);
        args.Handled = true;
    }

    private void OnFullSandbagInteract(Entity<FullSandbagComponent> bag, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        StartBuild(bag, args.User, args.ClickLocation);
        args.Handled = true;
    }

    private void StartBuild(Entity<FullSandbagComponent> bag, EntityUid user, EntityCoordinates location)
    {
        if (!TryGetSurdyTile(location, out _))
        {
            _popup.PopupClient(Loc.GetString("entrenching-build-novacancy"), user, user);
            return;
        }

        // Check we have enough bags in the stack (or just 1 if not stackable)
        var stackComp = CompOrNull<StackComponent>(bag);
        var available = stackComp?.Count ?? 1;
        if (available < bag.Comp.StackRequired)
        {
            _popup.PopupClient(Loc.GetString("entrenching-build-notenough", ("required", bag.Comp.StackRequired)), user, user);
            return;
        }

        _popup.PopupClient(Loc.GetString("entrenching-build-start"), user, user);

        var ev = new SandbagBuildDoAfterEvent();
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, bag.Comp.BuildDelay, ev, bag, used: bag)
        {
            BreakOnMove = true,
            CancelDuplicate = true,
            NeedHand = true
        });
    }

    private void OnBuildDoAfter(Entity<FullSandbagComponent> bag, ref SandbagBuildDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;
        args.Handled = true;

        var coords = Transform(args.User).Coordinates;
        SpawnBarricade(bag, args.User, coords);
    }

    // ---------- Dismantle barricade ----------

    private void OnBarricadeDismantle(Entity<BarricadeSandbagComponent> barricade, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        if (!HasComp<EntrenchingToolComponent>(args.Used))
            return;

        _popup.PopupClient(Loc.GetString("entrenching-dismantle-start"), args.User, args.User);

        var ev = new SandbagDismantleDoAfterEvent();
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(3), ev, barricade, target: barricade, used: args.Used)
        {
            BreakOnMove = true,
            CancelDuplicate = true,
            NeedHand = true
        });

        args.Handled = true;
    }

    private void OnDismantleDoAfter(Entity<BarricadeSandbagComponent> barricade, ref SandbagDismantleDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;
        args.Handled = true;

        DismantleBarricade(barricade, args.User);
        _popup.PopupClient(Loc.GetString("entrenching-dismantle-finish"), args.User, args.User);
    }

    // ---------- Tile validity ----------

    private bool TryGetSurdyTile(EntityCoordinates coordinates, out TileRef tile)
    {
        var tileRef = coordinates.GetTileRef(EntityManager, _mapManager);
        if (tileRef == null || tileRef.Value.IsSpace() || !tileRef.Value.Tile.GetContentTileDefinition().Sturdy)
        {
            tile = default;
            return false;
        }
        tile = tileRef.Value;
        return true;
    }

    // ---------- Spawn helpers (abstract — server only) ----------

    protected abstract void SpawnDigResult(Entity<EntrenchingToolComponent> tool, EntityUid user, int count);
    protected abstract void SpawnFilledBag(Entity<EmptySandbagComponent> bag, EntityUid user);
    protected abstract void SpawnBarricade(Entity<FullSandbagComponent> bag, EntityUid user, EntityCoordinates coords);
    protected abstract void DismantleBarricade(Entity<BarricadeSandbagComponent> barricade, EntityUid user);
}
