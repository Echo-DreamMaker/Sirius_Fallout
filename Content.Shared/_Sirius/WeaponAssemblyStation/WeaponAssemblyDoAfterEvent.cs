// #Misfits Add - The timed operations of the weapon assembly station.
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Sirius.WeaponAssemblyStation;

/// <summary>
/// Runs while the station strips a weapon down to its frame (Assemble = false)
/// or seats a frame's parts together into a finished weapon (Assemble = true).
/// </summary>
[Serializable, NetSerializable]
public sealed partial class WeaponAssemblyDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public bool Assemble;
}
