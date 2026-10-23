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
using Robust.Shared.Utility;

namespace Content.Server._Sirius.Weapons.Parts;

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
        SubscribeLocalEvent<WeaponFrameComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<WeaponFrameComponent, ItemSlotInsertAttemptEvent>(OnInsertAttempt);
    }

    /// <summary>
    ///     Seats the magazine the player staged during assembly into the finished
    ///     weapon, before the free magazine the prototype declares gets spawned.
    /// </summary>
    /// <remarks>
    ///     This has to be a startup handler rather than a map-init one. The two are
    ///     raised for the same entity in a fixed order: every component is initialized
    ///     (which is when ItemSlotsSystem creates the slot containers) and started
    ///     first, and only then is MapInitEvent raised (which is when ItemSlotsSystem
    ///     spawns each slot's startingItem). Running here therefore means the slot is
    ///     already usable and, once we have occupied it, ItemSlotsSystem leaves the
    ///     prototype's free magazine alone - there is no second occupant to delete
    ///     and nothing that has to be deferred to the next tick.
    /// </remarks>
    private void OnStartup(EntityUid uid, WeaponFrameComponent frame, ComponentStartup args)
    {
        if (!_container.TryGetContainer(uid, CraftMagazine, out var staged) ||
            staged.ContainedEntities.Count == 0)
        {
            // Nothing was staged: an admin-spawned weapon or an internal-magazine
            // rifle keeps the magazine and chambered round its prototype spawns.
            return;
        }

        if (!TryComp(uid, out ItemSlotsComponent? slots))
            return;

        if (!_itemSlots.TryGetSlot(uid, MagazineSlot, out var magazineSlot, slots))
            return;

        // The finished weapon keeps the magazine the player installed (with its
        // exact ammunition), not the free one the prototype spawns. Insert takes
        // the magazine out of the staging container for us.
        var magazine = staged.ContainedEntities[0];
        if (!_itemSlots.TryInsert(uid, magazineSlot, magazine, null))
        {
            // Leave the magazine where it can still be recovered rather than
            // dropping it at the weapon's feet, and let the prototype's magazine
            // stand in instead.
            Log.Error($"Could not seat the staged magazine into {ToPrettyString(uid)}");
            return;
        }

        // A weapon built by hand starts unchambered, which is also what keeps
        // the reverse edge's "gun_chamber is empty" condition satisfiable.
        if (_itemSlots.TryGetSlot(uid, ChamberSlot, out var chamberSlot, slots))
            chamberSlot.StartingItem = null;
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