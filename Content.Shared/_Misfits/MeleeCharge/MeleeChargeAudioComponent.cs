using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Misfits.MeleeCharge;

/// <summary>
/// Optional per-performer audio for the shared melee charge action.
/// Kept separate from MeleeChargeComponent because that component also locks movement while active.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MeleeChargeAudioComponent : Component
{
    [DataField]
    public SoundSpecifier ActivationSound = new SoundPathSpecifier("/Audio/Effects/thudswoosh.ogg");

    [DataField]
    public SoundSpecifier ImpactSound = new SoundPathSpecifier("/Audio/Effects/Footsteps/largethud.ogg");
}
