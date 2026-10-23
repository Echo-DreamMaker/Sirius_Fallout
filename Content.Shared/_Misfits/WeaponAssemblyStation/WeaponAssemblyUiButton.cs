using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.WeaponAssemblyStation;

[Serializable, NetSerializable]
public enum WeaponAssemblyUiButton : byte
{
    Bolt,
    Disassemble,
    Assemble,

    // #Misfits Add - repair operations. The part buttons are in station slot
    // order (barrel/optics, bolt, grip/drive, stock); RepairBody mends the frame
    // the weapon is stripped to, or the body of a weapon that has no parts.
    RepairBody,
    RepairPart1,
    RepairPart2,
    RepairPart3,
    RepairPart4,

    Close,
}