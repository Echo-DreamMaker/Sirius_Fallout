// #Misfits Add - one step of a weapon assembly station repair operation.
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Sirius.WeaponAssemblyStation;

[Serializable, NetSerializable]
public sealed partial class WeaponRepairDoAfterEvent : SimpleDoAfterEvent
{
    /// <summary>
    ///     Which repair this step belongs to: 0 is the body/frame, 1-4 the
    ///     station's four working slots.
    /// </summary>
    [DataField]
    public int Step;
}
