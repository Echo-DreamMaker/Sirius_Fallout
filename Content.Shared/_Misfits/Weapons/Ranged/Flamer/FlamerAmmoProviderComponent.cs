using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._Misfits.Weapons.Ranged.Flamer;

[RegisterComponent]
public sealed partial class FlamerAmmoProviderComponent : AmmoProviderComponent
{
    [DataField]
    public string SolutionId = "chamber";

    [DataField]
    public float FireCost = 5f;

    [DataField]
    public EntProtoId FireTilePrototype = "MisfitsTileFire";

    [DataField]
    public EntProtoId? ProjectileProto;

    [DataField]
    public int Range = 6;
}
