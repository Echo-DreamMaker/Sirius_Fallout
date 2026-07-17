using Content.Shared.Weapons.Ranged;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.Weapons.Ranged.Flamer;

[Serializable, NetSerializable]
public sealed class FlamerShot : IShootable
{
    public EntProtoId FireTilePrototype { get; }
    public EntProtoId? ProjectileProto { get; }
    public int Range { get; }

    public FlamerShot(EntProtoId fireTilePrototype, EntProtoId? projectileProto, int range)
    {
        FireTilePrototype = fireTilePrototype;
        ProjectileProto = projectileProto;
        Range = range;
    }
}
