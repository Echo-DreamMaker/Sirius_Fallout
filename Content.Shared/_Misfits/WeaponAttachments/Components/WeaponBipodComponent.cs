// #Misfits Add - Folding bipod attachment behaviour.
using Robust.Shared.GameStates;
using Robust.Shared.Maths;

namespace Content.Shared._Misfits.WeaponAttachments.Components;

/// <summary>
/// A folding bipod. While deployed it hurts accuracy when standing and improves it while prone.
/// While folded it does not affect the weapon at all.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WeaponBipodComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Deployed;

    [DataField]
    public Angle StandingMinAnglePenalty = Angle.FromDegrees(30);

    [DataField]
    public Angle StandingMaxAnglePenalty = Angle.FromDegrees(60);

    [DataField]
    public Angle StandingAngleIncreasePenalty = Angle.FromDegrees(4);

    [DataField]
    public Angle ProneMinAngleBonus = Angle.FromDegrees(-15);

    [DataField]
    public Angle ProneMaxAngleBonus = Angle.FromDegrees(-30);

    [DataField]
    public Angle ProneAngleIncreaseBonus = Angle.FromDegrees(-3);

    [DataField]
    public float ProneCameraRecoilModifier = 0.5f;
}
