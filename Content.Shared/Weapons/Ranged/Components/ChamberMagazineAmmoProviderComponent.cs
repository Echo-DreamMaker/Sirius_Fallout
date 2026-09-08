using Robust.Shared.Audio;

namespace Content.Shared.Weapons.Ranged.Components;

/// <summary>
/// Chamber + mags in one package. If you need just magazine then use <see cref="MagazineAmmoProviderComponent"/>
/// </summary>
[RegisterComponent, AutoGenerateComponentState]
public sealed partial class ChamberMagazineAmmoProviderComponent : MagazineAmmoProviderComponent
{
    /// <summary>
    /// If the gun has a bolt and whether that bolt is closed. Firing is impossible
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("boltClosed"), AutoNetworkedField]
    public bool? BoltClosed = false;

    /// <summary>
    /// Does the gun automatically open and close the bolt upon shooting.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("autoCycle"), AutoNetworkedField]
    public bool AutoCycle = true;

    /// <summary>
    /// Can the gun be racked, which opens and then instantly closes the bolt to cycle a round.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("canRack"), AutoNetworkedField]
    public bool CanRack = true;

    // #Misfits Fixed - M1 Garand en-bloc clip must stay in while a round is chambered (bolt holds
    // the clip in until the chamber runs dry). Previously the clip auto-ejected the moment its last
    // round was chambered, which with a +1 round loaded forced a double bolt on reload.
    /// <summary>
    /// If true, an empty magazine/clip is retained while a round is still in the chamber
    /// (e.g. the M1 Garand en-bloc clip is held by the bolt). Auto-eject (and its sound)
    /// only fires once the chamber is empty too.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("retainEmptyWhenChambered"), AutoNetworkedField]
    public bool RetainEmptyWhenChambered = false;

    [ViewVariables(VVAccess.ReadWrite), DataField("soundBoltClosed"), AutoNetworkedField]
    public SoundSpecifier? BoltClosedSound = new SoundPathSpecifier("/Audio/Weapons/Guns/Bolt/rifle_bolt_closed.ogg");

    [ViewVariables(VVAccess.ReadWrite), DataField("soundBoltOpened"), AutoNetworkedField]
    public SoundSpecifier? BoltOpenedSound = new SoundPathSpecifier("/Audio/Weapons/Guns/Bolt/rifle_bolt_open.ogg");

    [ViewVariables(VVAccess.ReadWrite), DataField("soundRack"), AutoNetworkedField]
    public SoundSpecifier? RackSound = new SoundPathSpecifier("/Audio/Weapons/Guns/Cock/ltrifle_cock.ogg");
}
