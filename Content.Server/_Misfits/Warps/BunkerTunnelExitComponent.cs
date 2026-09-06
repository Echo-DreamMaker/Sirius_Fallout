// #Misfits Add - Marker entity the bunker hatch can drop players at.
namespace Content.Server._Misfits.Warps;

/// <summary>
/// A place the surface bunker hatch can drop you. These are placed around the tunnels in the map
/// editor; each hatch picks one at random the first time it is used and then sticks with it.
/// Adding another way out of the maze means placing another marker, not changing code.
/// </summary>
[RegisterComponent]
public sealed partial class BunkerTunnelExitComponent : Component
{
    /// <summary>
    /// Which tunnel network this exit belongs to. Only hatches on the same channel can arrive here.
    /// </summary>
    [DataField]
    public string Channel = "bunker_tunnel";
}
