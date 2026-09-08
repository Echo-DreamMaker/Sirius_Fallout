// #Misfits Added - Broadcast extension point so fork systems (aircraft/vertibird) can add their
// own projectile collision rules without duplicating the base PreventCollideEvent subscription.
using Robust.Shared.GameObjects;

namespace Content.Shared.Projectiles;

/// <summary>
/// Raised by <see cref="SharedProjectileSystem"/> when a projectile is about to collide with another
/// entity, before the collision is allowed. Fork systems (e.g. aircraft/vertibird) use this broadcast
/// event to cancel the collision (friendly-fire protection inside a craft) without subscribing to
/// <see cref="PreventCollideEvent"/> directly — the event bus only permits one directed subscription
/// per component/event pair, which the base projectile system already owns.
/// </summary>
[ByRefEvent]
public struct ProjectilePreventCollideEvent
{
    /// <summary>
    /// The projectile that is about to collide.
    /// </summary>
    public readonly Entity<ProjectileComponent> Projectile;

    /// <summary>
    /// The other entity the projectile is about to collide with.
    /// </summary>
    public readonly EntityUid OtherEntity;

    /// <summary>
    /// Whether the collision should be prevented.
    /// </summary>
    public bool Cancelled;

    public ProjectilePreventCollideEvent(Entity<ProjectileComponent> projectile, EntityUid otherEntity)
    {
        Projectile = projectile;
        OtherEntity = otherEntity;
        Cancelled = false;
    }
}
