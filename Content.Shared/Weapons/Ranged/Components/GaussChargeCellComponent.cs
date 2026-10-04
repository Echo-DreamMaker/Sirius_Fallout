namespace Content.Shared.Weapons.Ranged.Components;

/// <summary>
/// Marks a ballistic magazine that doubles as a gauss charge cell, so it reports its own remaining
/// shots when examined on its own rather than only while it is inside a gun.
///
/// This lives on the magazine, not the gun, because the cell is the thing that actually holds the
/// charge. The gun's <see cref="HybridAmmoProviderComponent"/> mirrors the same numbers onto itself
/// for its own examine line and pre-fire gate; the two are sized from the same
/// <c>capacity * fireCost</c> rule so they always agree.
/// </summary>
/// <remarks>
/// Reading the charge needs <c>BatteryComponent</c>, which is server-only and not networked, so the
/// examine line is produced server-side by <c>GaussChargeCellSystem</c> rather than in shared code.
/// </remarks>
[RegisterComponent]
public sealed partial class GaussChargeCellComponent : Component
{
    /// <summary>
    /// Jouels one shot costs, i.e. how many joules make up a single "charge" for display purposes.
    /// Kept in step with the gun's <see cref="HybridAmmoProviderComponent.FireCost"/>; gauss
    /// prototypes set both to the same value so the magazine and the gun count shots identically.
    /// </summary>
    [DataField("chargeCost")]
    public float ChargeCost = 50f;
}