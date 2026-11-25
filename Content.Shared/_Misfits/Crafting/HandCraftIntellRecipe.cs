using Content.Shared.Materials;
using Robust.Shared.Prototypes;

namespace Content.Shared._Misfits.Crafting;

[Prototype]
public sealed partial class HandCraftIntellRecipePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; set; } = default!;

    [DataField(required: true)]
    public int MinInt;

    [DataField(required: true)]
    public EntProtoId Result;

    [DataField]
    public Dictionary<ProtoId<MaterialPrototype>, int> Materials = new();

    // #Sirius Add: whole items consumed by the recipe (skulls, logs, rebar, ...).
    [DataField]
    public Dictionary<EntProtoId, int> ItemCosts = new();

    // #Sirius Add: tags an item in the player's inventory must have. Tools are checked, never consumed.
    [DataField]
    public List<string> ToolTags = new();

    [DataField]
    public TimeSpan CompleteTime = TimeSpan.FromSeconds(5);
}
