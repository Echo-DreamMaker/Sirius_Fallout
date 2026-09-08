// Origin: ColonialMarinesUniverse (AU-14) — Multi Z system
//   PR #1058 "Multi Z addition" & #1119 "Multi z fixes" by TheHellFireo
//   Based on Crystall Edge (crystallpunk-14) Multi-Z system
//   Performance refactors from TTMC (ttmc14)
// Ported to misfits-14 _MultiZ/ — renamed &amp; adapted
// #Cythisiax Ported — Multi-Z level support for misfits-14

using Robust.Shared.GameStates;

namespace Content.Shared._MultiZ.Core.Components;

/// <summary>
/// Temporary marker for entities currently being processed by the server-side Z transition controller.
/// </summary>
[RegisterComponent, NetworkedComponent, UnsavedComponent]
public sealed partial class MZFallingComponent : Component;
