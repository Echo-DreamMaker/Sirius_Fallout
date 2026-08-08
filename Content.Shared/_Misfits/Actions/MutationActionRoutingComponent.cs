// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Misfits.Actions;

/// <summary>
/// Routes an inherited mutation action on both client and server without relying on runtime component state.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MutationActionRoutingComponent : Component;
