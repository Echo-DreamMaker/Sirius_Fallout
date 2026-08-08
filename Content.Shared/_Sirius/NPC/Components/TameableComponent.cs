using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._Sirius.NPC.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class TameableComponent : Component
{
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

    [DataField]
    public bool CanTame = true;

    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<TameablePresetPrototype>))]
    public string? Preset;
}
