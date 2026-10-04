using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.Weapons.Ranged.Components;

/// <summary>
/// The hybrid half of a gauss weapon: the gun feeds from an ordinary ballistic magazine that
/// happens to also be a battery cell, so every shot spends a cartridge <b>and</b> some charge.
///
/// This component deliberately does <b>not</b> implement ammo. Rounds come from the magazine via
/// the normal <see cref="MagazineAmmoProviderComponent"/> →
/// <see cref="BallisticAmmoProviderComponent"/> → <see cref="CartridgeAmmoComponent"/> chain, which
/// is shared, prediction-correct and already handles the ammo counter, examine, ejection, transfer
/// and the Gauss muzzle flash. All this component adds is the charge requirement and the drain.
///
/// That split is what makes gun prediction work: ammo taking happens in shared code on both sides
/// of the wire, so the shooter spawns the cartridge locally on the same tick they pull the trigger.
/// </summary>
/// <remarks>
/// A weapon using this needs <see cref="MagazineAmmoProviderComponent"/> alongside it, otherwise
/// nothing will feed it ammo.
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HybridAmmoProviderComponent : Component
{
    /// <summary>
    /// Jouels drawn from the magazine's battery per shot. Checked before firing so a shot is never
    /// wasted on an empty cell, and multiplied by the shot count for burst/full-auto catch-up.
    /// </summary>
    [DataField("fireCost")]
    public float FireCost = 50f;

    /// <summary>
    /// Charge left in the magazine, mirrored from its <c>BatteryComponent</c>.
    /// <c>BatteryComponent</c> is a server-only, non-networked component, so the client cannot read
    /// the real value; the server republishes it here after every shot and magazine change so the
    /// pre-fire check and the examine line work on the client too.
    /// </summary>
    [AutoNetworkedField]
    public float Charge;

    /// <summary>
    /// Capacity of the magazine's battery, mirrored alongside <see cref="Charge"/>.
    /// </summary>
    [AutoNetworkedField]
    public float MaxCharge;

    /// <summary>
    /// Whether the last mirrored refresh actually found a battery magazine. Lets the examine and
    /// pre-fire check tell "no magazine" apart from "flat cell".
    /// </summary>
    [AutoNetworkedField]
    public bool HasCell;

    /// <summary>
    /// Dry-fire click played when a shot is refused because the cell is flat. Separate from the
    /// gun's own <c>soundEmpty</c>, which fires when the <i>magazine</i> runs dry: this one answers
    /// "the cell is dead", that one answers "no rounds".
    /// </summary>
    [DataField("soundNoCharge")]
    public SoundSpecifier? SoundNoCharge = new SoundPathSpecifier("/Audio/DeltaV/Weapons/Guns/Empty/dry_fire.ogg");

    /// <summary>
    /// Whole shots the mirrored cell can still pay for. Designers size cells as
    /// <c>capacity * fireCost</c> (20 rounds x 50 J = 1000 J), so this lines up with the round count
    /// on the ammo counter and is far more meaningful to a player than a joule figure.
    /// </summary>
    public int ChargesLeft => CountCharges(Charge);

    /// <summary>Whole shots a full mirrored cell would pay for.</summary>
    public int ChargesMax => CountCharges(MaxCharge);

    /// <summary>
    /// Converts a joule figure into whole shots. Floors rather than rounds so a half-drained cell
    /// never claims a shot the pre-fire gate would then refuse.
    /// </summary>
    private int CountCharges(float joules)
    {
        if (!HasCell || FireCost <= 0f || joules <= 0f)
            return 0;

        return (int) MathF.Floor(joules / FireCost);
    }
}