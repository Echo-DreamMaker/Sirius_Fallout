// #Misfits Change - Consumable currency system
using Content.Shared.Stacks;

namespace Content.Shared._Misfits.Currency.Components;

/// <summary>
/// Marks a currency item as consumable, allowing it to be added to a player's persistent balance when used.
/// </summary>
[RegisterComponent]
public sealed partial class ConsumableCurrencyComponent : Component
{
    /// <summary>
    /// The type of currency this represents (Caps, NCRDollar, or LegionDenarius)
    /// </summary>
    [DataField(required: true)]
    public CurrencyType CurrencyType;

    /// <summary>
    /// The value per single item. For stacks, this is multiplied by stack count.
    /// </summary>
    [DataField]
    public int ValuePerUnit = 1;
}

/// <summary>
/// Types of persistent currency supported
/// </summary>
public enum CurrencyType : byte
{
    Bottlecaps,
    NCRDollars,
    LegionDenarii,
    PrewarMoney,
    // #Cythisiax Add - Free market multi-currency
    Silver,
    Gold
}
