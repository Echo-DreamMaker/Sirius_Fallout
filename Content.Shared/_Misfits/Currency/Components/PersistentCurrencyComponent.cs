// #Misfits Change - Persistent currency system
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.Currency.Components;

/// <summary>
/// Tracks a player's persistent Bottle Caps balance that persists across rounds and server restarts.
/// Currency is tied to the character name and stored in the database.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PersistentCurrencyComponent : Component
{
    /// <summary>
    /// Amount of bottlecaps the character has (persisted to the database).
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Bottlecaps;

    // #Cythisiax Add - Multi-currency balances for the free market
    /// <summary>Persistent NCR Dollars balance (3 NCR = 1 cap).</summary>
    [DataField, AutoNetworkedField]
    public int NcrDollars;

    /// <summary>Persistent Silver balance (4 silver = 1 cap).</summary>
    [DataField, AutoNetworkedField]
    public int Silver;

    /// <summary>Persistent Gold balance (2 gold = 1 cap).</summary>
    [DataField, AutoNetworkedField]
    public int Gold;

    // #Nuclear14 Add - Persistent Legion Denarii / pre-war money balances
    /// <summary>Persistent Legion Denarii balance.</summary>
    [DataField, AutoNetworkedField]
    public int LegionDenarii;

    /// <summary>Persistent Pre-War Money balance.</summary>
    [DataField, AutoNetworkedField]
    public int PrewarMoney;

    /// <summary>
    /// The user ID associated with this currency.
    /// </summary>
    [DataField]
    public string? UserId;

    /// <summary>
    /// The character name associated with this currency.
    /// </summary>
    [DataField]
    public string? CharacterName;

    /// <summary>
    /// Whether the currency has been loaded from the database yet.
    /// </summary>
    [DataField]
    public bool Loaded;

    /// <summary>
    /// The action prototype used to open the currency wallet UI.
    /// </summary>
    [DataField]
    public EntProtoId Action = "ActionOpenCurrencyWallet";

    /// <summary>
    /// The entity for the wallet action.
    /// </summary>
    [DataField]
    public EntityUid? ActionEntity;
}
