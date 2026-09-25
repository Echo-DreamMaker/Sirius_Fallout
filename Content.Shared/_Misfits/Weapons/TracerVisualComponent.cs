// #Misfits Add - Marks a projectile as a tracer round. While the round flies,
// the client renders a glowing comet tail behind it (see Content.Client ProjectileSystem).
// The NextEmit field is client-side only and is intentionally not networked.

namespace Content.Shared._Misfits.Weapons;

[RegisterComponent]
public sealed partial class TracerVisualComponent : Component
{
    /// <summary>
    /// Next client-side emit time for the trail effect.
    /// </summary>
    public TimeSpan NextEmit;
}