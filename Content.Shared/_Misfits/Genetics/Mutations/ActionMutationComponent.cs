// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Misfits.Genetics.Mutations;

/// <summary>
/// Grants the mutation user an action.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(ActionMutationSystem))]
[AutoGenerateComponentState]
public sealed partial class ActionMutationComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Action;

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;
}
