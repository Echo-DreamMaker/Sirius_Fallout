// Origin: ColonialMarinesUniverse (AU-14) — Multi Z system
//   PR #1058 "Multi Z addition" & #1119 "Multi z fixes" by TheHellFireo
//   Based on Crystall Edge (crystallpunk-14) Multi-Z system
//   Performance refactors from TTMC (ttmc14)
// Ported to misfits-14 _MultiZ/ — renamed &amp; adapted
// #Cythisiax Ported — Multi-Z level support for misfits-14

using Robust.Shared.GameStates;
using Content.Shared.Interaction;

namespace Content.Shared._MultiZ.Core.Components;

/// <summary>
/// Moves a user by a relative Z-level offset when activated.
/// Resolves through the current Z-level network instead of a linked destination entity.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MZLadderComponent : Component
{
    /// <summary>
    /// How long it takes to climb the ladder.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan Delay = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Maximum distance from the ladder before the climb is cancelled.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Range = SharedInteractionSystem.InteractionRange + 0.1f;

    /// <summary>
    /// Relative Z-level offset. Usually 1 for up or -1 for down.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Offset = 1;

    /// <summary>
    /// Local Z position to apply after the move.
    /// A small positive value lets the user rest on a lower-level ladder top.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float LandingLocalPosition = 0.05f;
}
