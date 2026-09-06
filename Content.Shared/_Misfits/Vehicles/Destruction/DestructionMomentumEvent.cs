using Robust.Shared.GameObjects;

namespace Content.Shared._Misfits.Vehicles.Destruction;

// #Misfits Add - Ported from CMU (AU-14) PR #1816 "Gunship overhaul + vehicle movement
// overhaul". Server-authoritative query for the impact speed required to remove a
// damageable obstruction. Destruction thresholds are server-only, so shared movement
// systems use this event instead of referencing server components.
[ByRefEvent]
public record struct DestructionMomentumQueryEvent(
    EntityUid Target,
    float AvailableSpeed,
    float DamageMultiplier)
{
    public bool HasRemovalThreshold;
    public bool CanDestroy;
    public float RequiredSpeed;
}
