// #Misfits Fix - Take a seated occupant's body out of the physics simulation while they are
// aboard the vertibird.
//
// Occupants are strapped at buckleOffset 0,0, which parks them dead centre inside the craft's
// own hull fixture, and they are hidden while aboard. Leaving a live body there caused two
// separate problems:
//
//   1. The deep fixture overlap made the solver push hard to separate the two. A power armour
//      wearer refuses their share of that, because PowerArmorWornComponent cancels
//      AttemptMobTargetCollideEvent to act as an immovable wall, so the whole impulse landed
//      on the craft and threw it across the pad.
//   2. Suppressing just the contact was not enough. The occupant's body still sat in the
//      simulation parented to a moving non-grid entity, which tethered the craft and dragged
//      it back toward where it lifted off whenever the pilot manoeuvred.
//
// The engine already models "inside a vehicle" this way: SharedContainerSystem disables
// CanCollide on insert and asserts inserted bodies are neither awake nor colliding, which is
// how a mech pilot rides. Occupants are conceptually inside the craft, so they get the same
// treatment and their previous state is restored when they get out.
//
// Server-only on purpose. CanCollide is networked from the server, and Content.Client's
// BuckleSystem already subscribes to BuckleComponent's BuckledEvent, so a shared system here
// would be a duplicate subscription on the client.
using Content.Shared._Misfits.Vehicles.Vertibird;
using Content.Shared.Buckle.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server._Misfits.Vehicles.Vertibird;

/// <summary>
/// Remembers an occupant's physics state so boarding can be undone exactly on the way out.
/// </summary>
[RegisterComponent]
public sealed partial class VertibirdOccupantPhysicsComponent : Component
{
    [DataField]
    public bool PreviousCanCollide = true;
}

public sealed class VertibirdOccupantPhysicsSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Hooked from the occupant's side because VertibirdSystem already owns
        // VertibirdComponent's StrappedEvent, UnstrappedEvent and ComponentShutdown, and the
        // engine rejects a second subscription to the same component and event pair.
        //
        // A destroyed or broken craft needs no extra handling: SharedBuckleSystem unbuckles
        // everyone on strap shutdown, destruction, breakage and deconstruction, and each of
        // those raises UnbuckledEvent, so bodies are always handed back.
        SubscribeLocalEvent<BuckleComponent, BuckledEvent>(OnBuckled);
        SubscribeLocalEvent<BuckleComponent, UnbuckledEvent>(OnUnbuckled);
    }

    private void OnBuckled(Entity<BuckleComponent> ent, ref BuckledEvent args)
    {
        // Only vertibirds seat occupants inside the hull; ordinary chairs are unaffected.
        if (!HasComp<VertibirdComponent>(args.Strap.Owner))
            return;

        if (!TryComp<PhysicsComponent>(ent.Owner, out var physics))
            return;

        var stored = EnsureComp<VertibirdOccupantPhysicsComponent>(ent.Owner);
        stored.PreviousCanCollide = physics.CanCollide;

        _physics.SetCanCollide(ent.Owner, false, body: physics);
    }

    private void OnUnbuckled(Entity<BuckleComponent> ent, ref UnbuckledEvent args)
    {
        if (!TryComp<VertibirdOccupantPhysicsComponent>(ent.Owner, out var stored))
            return;

        if (TryComp<PhysicsComponent>(ent.Owner, out var physics))
            _physics.SetCanCollide(ent.Owner, stored.PreviousCanCollide, body: physics);

        RemComp<VertibirdOccupantPhysicsComponent>(ent.Owner);
    }
}
