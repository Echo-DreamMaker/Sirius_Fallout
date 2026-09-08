// Origin: ColonialMarinesUniverse (AU-14) — Multi Z system
//   PR #1058 "Multi Z addition" & #1119 "Multi z fixes" by TheHellFireo
//   Based on Crystall Edge (crystallpunk-14) Multi-Z system
//   Performance refactors from TTMC (ttmc14)
// Ported to misfits-14 _MultiZ/ — renamed &amp; adapted
// #Cythisiax Ported — Multi-Z level support for misfits-14

using Robust.Shared.GameStates;

namespace Content.Shared._MultiZ.Core.Components;

/// <summary>
/// Allows an entity to move up and down Z-levels via gravity or jumping.
/// Tracks vertical velocity and local height within the current Z-level (0..1 range).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class MZPhysicsComponent : Component
{
    /// <summary>
    /// Current speed of vertical Z-level movement.
    /// Positive = upward, negative = downward.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Velocity;

    /// <summary>
    /// Current height within the current Z-level.
    /// Values from 0 to 1. Above 1 → transition to level above. Below 0 → transition to level below.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float LocalPosition;

    /// <summary>
    /// Bounciness factor when hitting a floor/ceiling.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Bounciness = 0.3f;

    /// <summary>
    /// Original NoRot value from SpriteComponent, saved for visual offset.
    /// </summary>
    [DataField]
    public bool NoRotDefault;

    /// <summary>
    /// Original DrawDepth value, saved for visual offset.
    /// </summary>
    [DataField]
    public int DrawDepthDefault;

    /// <summary>
    /// Original sprite offset at MapInit, used for Z-position visual offset.
    /// </summary>
    [DataField]
    public Vector2 SpriteOffsetDefault = Vector2.Zero;

    /// <summary>
    /// Last map checked for fall-through.
    /// </summary>
    public EntityUid LastFallCheckMap = EntityUid.Invalid;

    /// <summary>
    /// Last tile checked for fall-through.
    /// </summary>
    public Vector2i LastFallCheckTile;
}
