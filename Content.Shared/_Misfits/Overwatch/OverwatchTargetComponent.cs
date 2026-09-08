using System.Collections.Generic;
using Robust.Shared.GameStates;

namespace Content.Shared._Misfits.Overwatch;

/// <summary>
/// Placed on a watched entity while one or more operators are watching it.
/// The client renders a HUD indicator from <see cref="WatcherNames"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OverwatchTargetComponent : Component
{
    /// <summary>Display names of the operators currently watching this entity.</summary>
    [DataField, AutoNetworkedField]
    public List<string> WatcherNames = new();

    /// <summary>Only the watched player needs to see their watcher list.</summary>
    public override bool SendOnlyToOwner => true;
}
