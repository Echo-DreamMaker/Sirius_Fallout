// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.EntityEffects;

namespace Content.Shared._Misfits.EntityEffects;

[Prototype("entityEffect")]
public sealed partial class GeneticsEntityEffectPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    [DataField]
    public EntityEffectCondition[] Conditions = [];

    [DataField]
    public LocId? GuidebookText;

    [DataField(required: true)]
    public EntityEffect[] Effects = [];
}

[Prototype("entityCondition")]
public sealed partial class GeneticsEntityConditionPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    [DataField(required: true)]
    public EntityEffectCondition Condition = default!;
}
