// #Misfits Change - Ported from Delta-V addiction system
using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Timing;

namespace Content.Shared._Misfits.Addictions;

/// <summary>
///     Added to an entity when they are currently addicted to a substance.
///     Managed by <see cref="SharedAddictionSystem"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(SharedAddictionSystem))]
[AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class AddictedComponent : Component
{
    /// <summary>
    ///     Whether the addiction symptoms are currently suppressed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Suppressed;

    /// <summary>
    ///     Whether the addiction is permanent (e.g. Jet). A permanent addiction never
    ///     expires on its own and must be cleared before the entity returns to clean.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Permanent;

    /// <summary>
    ///     When the addictive substance was last metabolized.
    /// </summary>
    [DataField(serverOnly: true, customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan? LastMetabolismTime;

    /// <summary>
    ///     When the next withdrawal effect popup should fire.
    /// </summary>
    [DataField(serverOnly: true, customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan? NextEffectTime;

    /// <summary>
    ///     When the current suppression period ends.
    /// </summary>
    [DataField(serverOnly: true, customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan? SuppressionEndTime;

    // #Misfits Change /Add:/ Drug-specific tracking for addiction chat messages

    /// <summary>
    ///     Localized name of the drug that applied this addiction.
    ///     Used to construct drug-specific chat messages like "Your addiction to Hydra grows."
    /// </summary>
    [DataField(serverOnly: true)]
    public string DrugName = string.Empty;

    /// <summary>
    ///     Number of doses taken while this addiction is active.
    ///     Drives severity-tiered messages: mild (1-3), growing (4-7), severe (8+).
    /// </summary>
    [DataField(serverOnly: true)]
    public int DoseCount = 0;

    /// <summary>
    ///     The last withdrawal tier that was reported to the player via chat.
    ///     Used to detect downward tier transitions for fading messages.
    ///     -1 = unset (first update will initialise without sending a message).
    ///     0 = nearly-gone, 1 = mild, 2 = moderate, 3 = severe.
    /// </summary>
    [DataField(serverOnly: true)]
    public int LastReportedTier = -1;

    // #Misfits Change /Add:/ Per-drug withdrawal gameplay effect parameters.
    // Set by the Addicting reagent effect; applied by AddictionSystem when not suppressed.

    /// <summary>
    ///     Mood prototype ID to apply during active withdrawal (e.g. "HydraWithdrawal").
    ///     Empty string means no mood effect for this drug.
    /// </summary>
    [DataField(serverOnly: true)]
    public string WithdrawalMoodEffect = string.Empty;

    /// <summary>
    ///     Damage applied every <see cref="WithdrawalNextTick"/> interval while in withdrawal.
    ///     Null = no periodic damage.
    /// </summary>
    [DataField(serverOnly: true)]
    public DamageSpecifier? WithdrawalDamage;

    /// <summary>
    ///     Multiplicative movement speed penalty while in active withdrawal.
    ///     1.0 = no penalty (default). 0.8 = 20 % slower.
    /// </summary>
    [DataField(serverOnly: true)]
    public float WithdrawalSpeedPenalty = 1.0f;

    /// <summary>
    ///     Stamina damage applied every <see cref="WithdrawalNextTick"/> interval while in withdrawal.
    ///     0.0 = none (default).
    /// </summary>
    [DataField(serverOnly: true)]
    public float WithdrawalStaminaDrain = 0.0f;

    /// <summary>
    ///     When to next apply the periodic withdrawal damage / stamina drain.
    /// </summary>
    [DataField(serverOnly: true, customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan WithdrawalNextTick;

    /// <summary>
    ///     Tracks the previous Suppressed state so AddictionSystem can detect
    ///     transitions and apply / remove mood effects + speed modifiers exactly once.
    /// </summary>
    [DataField(serverOnly: true)]
    public bool PreviousSuppressed;
}

