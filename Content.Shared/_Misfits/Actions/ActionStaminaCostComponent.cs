namespace Content.Shared._Misfits.Actions;

/// <summary>
/// A component added to Actions to restrict their use based on stamina.
/// </summary>
[RegisterComponent]
public sealed partial class ActionStaminaCostComponent : Component
{
    /// <summary>
    /// A flat stamina cost for the action. This is overridden if StaminaPercent is also present
    /// </summary>
    [DataField]
    public float Stamina;

    /// <summary>
    /// If given a value, the component will use this percentage of the user's
    /// stamina crit threshold instead of the flat stamina cost
    /// </summary>
    [DataField]
    public float StaminaPercent;
}
