// Misfits Add - YAML-driven ghost color presets. List usernames and they get a custom ghost tint.
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Shared._Misfits.Ghost;

/// <summary>
/// A named ghost-color preset that applies to listed usernames.
/// Servers define these in YAML; the server-side GhostColorSystem reads them
/// and sets GhostComponent.color on spawn.
/// </summary>
[Prototype("misfitsGhostColor")]
public sealed partial class MisfitsGhostColorPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    /// <summary>The ghost tint color (hex like "#ffff00cc").</summary>
    [DataField("color", required: true)]
    public Color Color = Color.White;

    /// <summary>Usernames (case-insensitive match against session name).</summary>
    [DataField("users")]
    public List<string> Users { get; private set; } = new();
}
