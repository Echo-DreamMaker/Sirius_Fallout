using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.WeaponAssemblyStation;

[Serializable, NetSerializable]
public sealed class WeaponAssemblyBoundUserInterfaceState : BoundUserInterfaceState
{
    public bool Powered;
    public bool IsBusy;
    public bool IsBolted;

    // #Misfits Add - entities shown in the station's slot formation.
    public NetEntity? Weapon;
    public NetEntity? Magazine;
    public NetEntity?[] Parts;

    // #Misfits Add - which repair operations the station can run right now.
    // Index 0 is the body/frame, 1-4 the four working slots.
    public bool[] RepairEnabled;

    /// <summary>
    /// Whether the body repair mends a whole weapon (one that has no parts to
    /// strip) rather than the bare frame the weapon was taken apart into. Only
    /// changes the button's label.
    /// </summary>
    public bool BodyRepairIsWeapon;

    // #Misfits Add - exact durability numbers for the panels: index 0-3 are the
    // four working slots, the weapon values are for the main slot.
    public float[] PartCurrent;
    public float[] PartMax;
    public float[] PartDeferred;
    public float WeaponCurrent;
    public float WeaponMax;

    // #Misfits Add - remaining welding fuel in the station's internal reservoir.
    public float Fuel;
    public float FuelMax;

    public WeaponAssemblyBoundUserInterfaceState(
        bool powered,
        bool isBusy,
        bool isBolted,
        NetEntity? weapon,
        NetEntity? magazine,
        NetEntity?[] parts,
        bool[] repairEnabled,
        bool bodyRepairIsWeapon,
        float[] partCurrent,
        float[] partMax,
        float[] partDeferred,
        float weaponCurrent,
        float weaponMax,
        float fuel,
        float fuelMax)
    {
        Powered = powered;
        IsBusy = isBusy;
        IsBolted = isBolted;
        Weapon = weapon;
        Magazine = magazine;
        Parts = parts;
        RepairEnabled = repairEnabled;
        BodyRepairIsWeapon = bodyRepairIsWeapon;
        PartCurrent = partCurrent;
        PartMax = partMax;
        PartDeferred = partDeferred;
        WeaponCurrent = weaponCurrent;
        WeaponMax = weaponMax;
        Fuel = fuel;
        FuelMax = fuelMax;
    }
}
