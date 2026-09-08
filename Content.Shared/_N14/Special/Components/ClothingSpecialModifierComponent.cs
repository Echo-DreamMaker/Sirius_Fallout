using Robust.Shared.GameStates;

namespace Content.Shared._N14.Special.Components;

/// <summary>
/// Grants flat temporary SPECIAL stat bonuses to the wearer while this clothing is
/// equipped. Applied through <see cref="ClothingSpecialModifierSystem"/> using the
/// misfits temporary modifier API so the bonuses stack with gear and clear on unequip.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ClothingSpecialModifierComponent : Component
{
    [DataField, AutoNetworkedField]
    public int StrengthModifier;

    [DataField, AutoNetworkedField]
    public int PerceptionModifier;

    [DataField, AutoNetworkedField]
    public int EnduranceModifier;

    [DataField, AutoNetworkedField]
    public int CharismaModifier;

    [DataField, AutoNetworkedField]
    public int IntelligenceModifier;

    [DataField, AutoNetworkedField]
    public int AgilityModifier;

    [DataField, AutoNetworkedField]
    public int LuckModifier;

    /// <summary>
    /// Whether this clothing currently grants its bonuses (e.g. toggles or batteries).
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    /// <summary>
    /// The entity currently wearing this item (not networked). Used to clear the
    /// modifiers if the clothing is destroyed while equipped.
    /// </summary>
    public EntityUid? Equipee;
}