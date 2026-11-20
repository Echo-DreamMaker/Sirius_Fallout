// #Sirius Add - hostile AI attacks the car instead of the mob sitting inside it.
// Faction checks still run on the occupant (the car has no faction), so only the
// final utility target is remapped to the enclosing vehicle.
using System.Diagnostics.CodeAnalysis;
using Content.Shared._N14.Vehicles;
using Content.Shared.Damage;
using Content.Shared.Mech.Components;
using Robust.Server.Containers;

namespace Content.Server._Sirius.NPC.Systems;

public sealed class SiriusVehicleTargetSystem : EntitySystem
{
    [Dependency] private readonly ContainerSystem _container = default!;

    /// <summary>
    /// #Sirius Add - true when the candidate is a pilot/passenger of a car that can be damaged.
    /// Used to keep such hostiles selectable by TargetAccessibleCon.
    /// </summary>
    public bool IsVehicleOccupant(EntityUid candidate)
    {
        return TryGetVehicle(candidate, out _);
    }

    /// <summary>
    /// #Sirius Add - resolves the car a candidate is riding in, if any.
    /// </summary>
    public bool TryGetVehicleTarget(EntityUid candidate, [NotNullWhen(true)] out EntityUid? vehicle)
    {
        return TryGetVehicle(candidate, out vehicle);
    }

    /// <summary>
    /// #Sirius Add - everyone riding in a car: the pilot plus all passenger seats.
    /// Needed because GetNearbyHostiles only scans containers of entities that already have
    /// a faction component, and cars have none, so the occupant is otherwise invisible.
    /// </summary>
    public IEnumerable<EntityUid> GetOccupants(EntityUid vehicle)
    {
        if (!TryComp<MechComponent>(vehicle, out var mech))
            yield break;

        if (mech.PilotSlot.ContainedEntity is { } pilot)
            yield return pilot;
        if (mech.PassengerSlot1.ContainedEntity is { } p1)
            yield return p1;
        if (mech.PassengerSlot2.ContainedEntity is { } p2)
            yield return p2;
        if (mech.PassengerSlot3.ContainedEntity is { } p3)
            yield return p3;
        if (mech.PassengerSlot4.ContainedEntity is { } p4)
            yield return p4;
    }

    private bool TryGetVehicle(EntityUid candidate, [NotNullWhen(true)] out EntityUid? vehicle)
    {
        vehicle = null;
        if (!Exists(candidate))
            return false;

        EntityUid candidateVehicle = default;

        if (TryComp<MechPilotComponent>(candidate, out var pilot) && Exists(pilot.Mech))
            candidateVehicle = pilot.Mech;
        else if (TryComp<MechPassengerComponent>(candidate, out var passenger) && Exists(passenger.Mech))
            candidateVehicle = passenger.Mech;
        else if (_container.TryGetContainingContainer(candidate, out var container))
            candidateVehicle = container.Owner;

        if (!Exists(candidateVehicle) ||
            !HasComp<CarComponent>(candidateVehicle) ||
            !HasComp<DamageableComponent>(candidateVehicle))
            return false;

        vehicle = candidateVehicle;
        return true;
    }
}
