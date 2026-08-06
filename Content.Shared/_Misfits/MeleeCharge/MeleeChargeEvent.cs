using Content.Shared.Actions;


namespace Content.Shared._Misfits.MeleeCharge;


public sealed partial class MeleeChargeEvent : WorldTargetActionEvent
{
    [DataField]
    public float Speed = 10f;

    [DataField]
    public float Range = 3.5f;
    
}
