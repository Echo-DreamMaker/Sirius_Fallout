using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Sirius.NPC;

[Serializable, NetSerializable]
public sealed partial class TamingDoAfterEvent : DoAfterEvent
{
    [DataField]
    public NetEntity? FoodEntity;

    [DataField]
    public string? FoodId;

    public TamingDoAfterEvent(NetEntity? food, string? foodId)
    {
        FoodEntity = food;
        FoodId = foodId;
    }

    public override DoAfterEvent Clone()
    {
        return new TamingDoAfterEvent(FoodEntity, FoodId);
    }
}
