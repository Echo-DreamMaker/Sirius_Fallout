// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;

namespace Content.Shared._Misfits.Genetics.Abilities;

public sealed partial class ConjureMutationActionEvent : InstantActionEvent
{
    [DataField(required: true)]
    public EntProtoId Prototype;
}
