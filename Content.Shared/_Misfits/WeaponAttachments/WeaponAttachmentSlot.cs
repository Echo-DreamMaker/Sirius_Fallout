// #Misfits Add - Weapon attachment categories and slot id helpers.
namespace Content.Shared._Misfits.WeaponAttachments;

/// <summary>
/// The category of a weapon attachment. Every attachment fits exactly one category.
/// </summary>
public enum WeaponAttachmentSlot : byte
{
    /// <summary>
    /// Optics and sights. Improve aiming.
    /// </summary>
    Top,

    /// <summary>
    /// Suppressors (sound suppression) and muzzle brakes / compensators (recoil and accuracy).
    /// </summary>
    Muzzle,

    /// <summary>
    /// Bayonets, flashlights, laser sights, bipods and underbarrel launchers.
    /// </summary>
    Bottom,

    /// <summary>
    /// Grips and stocks. Improve handling, accuracy and how hard the weapon is to disarm.
    /// </summary>
    Handle,
}

public static class WeaponAttachmentSlots
{
    public const string Top = "gun_top";
    public const string Muzzle = "gun_muzzle";
    public const string Bottom = "gun_bottom";
    public const string Handle = "gun_handle";

    public static readonly WeaponAttachmentSlot[] All =
    {
        WeaponAttachmentSlot.Top,
        WeaponAttachmentSlot.Muzzle,
        WeaponAttachmentSlot.Bottom,
        WeaponAttachmentSlot.Handle,
    };

    public static string ToId(WeaponAttachmentSlot slot)
    {
        return slot switch
        {
            WeaponAttachmentSlot.Top => Top,
            WeaponAttachmentSlot.Muzzle => Muzzle,
            WeaponAttachmentSlot.Bottom => Bottom,
            WeaponAttachmentSlot.Handle => Handle,
            _ => Top,
        };
    }
}
