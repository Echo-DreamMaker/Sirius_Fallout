using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Weapons.Ranged.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HybridAmmoProviderComponent : Component
{
    [DataField("proto", required: true, customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string Prototype = default!;

    [DataField("capacity"), AutoNetworkedField]
    public int Capacity = 10;

    [DataField("count"), AutoNetworkedField]
    public int Count = 10;

    [DataField("fireCost")]
    public float FireCost = 100f;

    // Ссылка на батарею (если null, используем свой uid)
    [DataField("battery")]
    public EntityUid? BatteryEntity;

    // Ссылка на слот магазина (для автоматической загрузки патронов)
    [DataField("magazineSlot")]
    public string? MagazineSlot;
}
