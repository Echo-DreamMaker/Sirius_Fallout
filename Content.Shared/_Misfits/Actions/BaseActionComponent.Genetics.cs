// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared.Actions;

/// <summary>
/// Trauma compatibility: route an action's event to the action entity itself. Genetics action handlers and
/// components live on that entity rather than on the mutated mob or mutation container.
/// </summary>
public abstract partial class BaseActionComponent
{
    [DataField]
    public bool RaiseOnAction;
}
