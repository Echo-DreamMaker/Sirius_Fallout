// #Misfits Add - Client half of vertibird cargo loading: makes crates draggable and
// marks the aircraft as a valid drop target for them.
using Content.Client.Buckle;
using Content.Client.Storage.Components;
using Content.Shared._Misfits.Vehicles.Vertibird;
using Content.Shared.DragDrop;

namespace Content.Client._Misfits.Vehicles.Vertibird;

/// <summary>
/// Drag validity lives on the client because <see cref="CanDragEvent"/> and
/// <see cref="CanDropTargetEvent"/> are only ever raised there. The server re-checks
/// the drop it receives; this only decides what the cursor lets you attempt.
/// </summary>
public sealed class VertibirdCargoDragSystem : EntitySystem
{
    [Dependency] private readonly SharedVertibirdSystem _vertibird = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EntityStorageComponent, CanDragEvent>(OnCrateCanDrag);
        // The strap on the aircraft answers this event for every dragged entity and
        // overwrites CanDrop when it does, so this has to land after it and OR in.
        SubscribeLocalEvent<VertibirdComponent, CanDropTargetEvent>(OnCanDropTarget,
            after: [typeof(BuckleSystem)]);
    }

    private void OnCrateCanDrag(Entity<EntityStorageComponent> ent, ref CanDragEvent args)
    {
        args.Handled = true;
    }

    private void OnCanDropTarget(Entity<VertibirdComponent> ent, ref CanDropTargetEvent args)
    {
        if (!_vertibird.CanStoreCargo(ent, args.Dragged))
            return;

        args.CanDrop = true;
        args.Handled = true;
    }
}
