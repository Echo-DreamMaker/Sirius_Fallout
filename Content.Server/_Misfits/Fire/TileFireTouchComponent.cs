using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Misfits.Fire;

[RegisterComponent]
public sealed partial class TileFireTouchComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextTouchAt;
}
