// #Misfits Add - Guided assembly direction for modular weapon frames.
// Vanilla already ships the "begin deconstructing" verb
// (Content.Server/Construction/ConstructionSystem.Guided.cs), but nothing points
// the player forwards by default: a fresh frame has no pathfinding target, and
// with no target an examined frame prints no next step, so the first "use a
// screwdriver" step would be invisible. The graphs are reversible, so one verb
// can always aim at the finished weapon again, even after the player deviates
// from the suggested path and the engine drops the target no matter how the
// last step was taken.
//
// "assemblyTarget" names the graph node the frame should be built into; the
// verb shows while there is a path to it, and pressing it inverts the direction
// a "begin deconstructing" press set. The verb also names the resulting weapon,
// because the vanilla header prints the raw graph node id instead of a readable
// name.
//
// The same component also owns the detachable-magazine rules of a build: when a
// weapon is finished by hand the magazine the player installed (staged in the
// "craft_magazine" container) replaces the free magazine the prototype spawns,
// and the inherited chambered round is cleared. While a build is coming apart a
// magazine or a chambered round cannot be re-inserted, which is what keeps the
// reverse edge's ContainerEmpty conditions satisfiable.
//
// Handlers are subscribed against WeaponFrameComponent, not
// ConstructionComponent - see the comment on that component.
using Content.Server.Construction;
using Content.Server.Construction.Components;
using Content.Shared.Construction;
using Content.Shared.Construction.NodeEntities;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Misfits.Weapons.Parts;

public sealed class WeaponFrameSystem : EntitySystem
{
    private const string MagazineSlot = "gun_magazine";
    private const string ChamberSlot = "gun_chamber";
    private const string CraftMagazine = "craft_magazine";

    [Dependency] private readonly ConstructionSystem _construction = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<WeaponFrameComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<WeaponFrameComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<WeaponFrameComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<WeaponFrameComponent, ItemSlotInsertAttemptEvent>(OnInsertAttempt);
    }

    private void OnMapInit(EntityUid uid, WeaponFrameComponent frame, ref MapInitEvent args)
    {
        if (!_container.TryGetContainer(uid, CraftMagazine, out var staged) ||
            staged.ContainedEntities.Count == 0)
        {
            // Nothing was staged: an admin-spawned weapon or an internal-magazine
            // rifle keeps the magazine and chambered round its prototype spawns.
            return;
        }

        // #Misfits Add - ItemSlotsSystem.OnMapInit is what spawns the inherited
        // startingItem magazine and chambered round, and the order of the two
        // MapInit handlers is not guaranteed. Defer the swap by one tick so both
        // have run, otherwise a chambered round spawned afterwards would survive.
        Timer.Spawn(0, () =>
        {
            if (TerminatingOrDeleted(uid) ||
                !TryComp(uid, out ItemSlotsComponent? slots) ||
                !_container.TryGetContainer(uid, CraftMagazine, out var craft) ||
                craft.ContainedEntities.Count == 0)
                return;

            var magazine = craft.ContainedEntities[0];

            // The finished weapon keeps the magazine the player installed (with
            // its exact ammunition), not the free one the prototype spawns.
            if (_itemSlots.TryGetSlot(uid, MagazineSlot, out var magazineSlot, slots))
            {
                if (magazineSlot.Item is { } inherited)
                    EntityManager.DeleteEntity(inherited);

                _container.Remove(magazine, craft, reparent: false);
                _itemSlots.TryInsert(uid, magazineSlot, magazine, null);
            }

            if (_itemSlots.TryGetSlot(uid, ChamberSlot, out var chamberSlot, slots) &&
                chamberSlot.Item is { } chambered)
            {
                EntityManager.DeleteEntity(chambered);
            }
        });
    }

    private void OnInsertAttempt(EntityUid uid, WeaponFrameComponent frame, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.Slot.ID != MagazineSlot && args.Slot.ID != ChamberSlot)
            return;

        if (!TryComp(uid, out ConstructionComponent? construction))
            return;

        // Only a finished, idle weapon accepts ammunition. While a build is
        // coming apart (the player pressed "begin deconstructing" or already
        // walked off the finished node) neither a magazine nor a chambered
        // round may go back in: the reverse edge needs both empty to stay
        // reachable.
        if (construction.TargetNode != construction.DeconstructionNode &&
            construction.Node == frame.AssemblyTarget)
            return;

        args.Cancelled = true;

        // If there is an active deconstruction target, drop it: the build stops
        // coming apart and only resumes after "begin deconstructing" is pressed
        // again with the magazine and chamber back out.
        if (construction.TargetNode == construction.DeconstructionNode)
            _construction.SetPathfindingTarget(uid, null, construction);
    }

    private void OnGetVerbs(EntityUid uid, WeaponFrameComponent frame, GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        if (string.IsNullOrEmpty(frame.AssemblyTarget))
            return;

        if (!TryComp(uid, out ConstructionComponent? construction))
            return;

        if (_construction.GetCurrentGraph(uid, construction) is not { } graph)
            return;

        if (_construction.GetNodeFromGraph(graph, frame.AssemblyTarget) is not { } target)
            return;

        // Already aimed at the finished weapon, or already standing on it.
        if (construction.TargetNode == frame.AssemblyTarget ||
            construction.Node == frame.AssemblyTarget)
            return;

        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("misfits-weapon-assembly-verb"),
            Icon = new SpriteSpecifier.Texture(
                new("/Textures/Interface/hammer_scaled.svg.192dpi.png")),
            Act = () =>
            {
                _construction.SetPathfindingTarget(uid, frame.AssemblyTarget, construction);
                _popup.PopupEntity(Loc.GetString("misfits-weapon-assembly-verb-activate"),
                    args.User, args.User);
            },
        });
    }

    private void OnExamine(EntityUid uid, WeaponFrameComponent frame, ExaminedEvent args)
    {
        if (string.IsNullOrEmpty(frame.AssemblyTarget))
            return;

        if (!TryComp(uid, out ConstructionComponent? construction) ||
            construction.TargetNode == null ||
            construction.TargetNode == construction.DeconstructionNode ||
            construction.TargetNode != frame.AssemblyTarget)
            return;

        if (_construction.GetCurrentGraph(uid, construction) is not { } graph)
            return;

        if (_construction.GetNodeFromGraph(graph, construction.TargetNode) is not { } node)
            return;

        if (node.Entity is not StaticNodeEntity staticNode || string.IsNullOrEmpty(staticNode.Id))
            return;

        if (!_prototypeManager.TryIndex(staticNode.Id, out EntityPrototype? prototype))
            return;

        args.PushMarkup(
            Loc.GetString("misfits-weapon-assembly-examine-target", ("name", Loc.GetString(prototype.Name))) +
            "\n");
    }
}