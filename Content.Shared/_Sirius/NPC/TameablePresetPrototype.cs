using Robust.Shared.Prototypes;

namespace Content.Shared._Sirius.NPC;

[Prototype("tameablePreset")]
public sealed partial class TameablePresetPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    [DataField]
    public List<string> FavoriteFoods = new();

    [DataField]
    public List<string> LikedFoods = new();

    [DataField]
    public List<string> DislikedFoods = new();

    [DataField]
    public float BaseTameChance = 0.3f;

    [DataField]
    public float FavoriteMultiplier = 2.0f;

    [DataField]
    public float LikedMultiplier = 1.5f;

    [DataField]
    public float DislikedMultiplier = 0.5f;

    [DataField]
    public float MinCharisma = 3;

    [DataField]
    public float TamingTime = 3.0f;
}
