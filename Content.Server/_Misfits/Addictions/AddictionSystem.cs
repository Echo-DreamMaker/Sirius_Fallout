// #Misfits Change - Ported from Delta-V addiction system
using Content.Server.Chat.Managers;
using Content.Shared._Misfits.Addictions;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Dataset;
using Content.Shared.Mood;
using Content.Shared.Movement.Systems;
using Content.Shared.StatusEffect;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Misfits.Addictions;

/// <summary>
///     Server-side addiction system. Handles withdrawal popup effects
///     at random intervals while the entity is addicted and not suppressed.
///     Also sends drug-specific chat messages when addiction is first applied,
///     deepens by dose count, and fades as the status effect timer winds down.
///     Applies per-drug withdrawal gameplay effects (mood, movement slow, damage,
///     stamina drain) while the entity is in active un-suppressed withdrawal.
/// </summary>
public sealed class AddictionSystem : SharedAddictionSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    // Chat messages for addiction growing/fading
    [Dependency] private readonly IChatManager _chat = default!;

    // StatusEffects — needed to read remaining addiction time for fading tiers
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;

    // #Misfits Change /Add:/ Withdrawal gameplay effect dependencies
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly StaminaSystem _stamina = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;

    private const float MinEffectInterval = 120f;
    private const float MaxEffectInterval = 300f;
    private const float SuppressionDuration = 10f;

    // Remaining-time thresholds (seconds) for fading tier detection.
    private const double TierSevereThreshold   = 120.0;
    private const double TierModerateThreshold =  60.0;
    private const double TierMildThreshold     =  15.0;

    // #Misfits Change /Add:/ Interval between periodic withdrawal damage/stamina ticks.
    private const float WithdrawalTickInterval = 15.0f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AddictedComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<AddictedComponent, ComponentRemove>(OnAddictionRemoved);

        // #Misfits Change /Add:/ Hook into movement speed refresh to apply withdrawal speed penalty
        SubscribeLocalEvent<AddictedComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMoveSpeed);
    }

    private void OnInit(EntityUid uid, AddictedComponent component, ComponentInit args)
    {
        var curTime = _timing.CurTime;
        component.NextEffectTime = curTime + TimeSpan.FromSeconds(_random.NextFloat(MinEffectInterval, MaxEffectInterval));

        // #Misfits Change /Add:/ Stagger the first withdrawal tick so it doesn't fire at round start
        component.WithdrawalNextTick = curTime + TimeSpan.FromSeconds(WithdrawalTickInterval);
    }

    // Misfits Fix: CheckAddictionFading calls _statusEffects.TryGetTime (a dictionary lookup) every tick
    // for every addicted entity. Gate it to once per 5 s — fading messages are cosmetic only.
    private float _fadeCheckAccum;
    private const float FadeCheckInterval = 5f;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;

        _fadeCheckAccum += frameTime;
        var doFadeCheck = _fadeCheckAccum >= FadeCheckInterval;
        if (doFadeCheck)
            _fadeCheckAccum -= FadeCheckInterval;

        var query = EntityQueryEnumerator<AddictedComponent>();

        while (query.MoveNext(out var uid, out var addicted))
        {
            // Detect suppression state transitions (fires mood events, speed refresh)
            UpdateSuppressed(uid, addicted, curTime);

            // Misfits Fix: fading-tier check gated to 5 s to avoid per-tick TryGetTime dictionary lookups.
            if (doFadeCheck)
                CheckAddictionFading(uid, addicted, curTime);

            if (addicted.Suppressed)
                continue;

            // Random withdrawal popup flavour text
            if (addicted.NextEffectTime != null && curTime >= addicted.NextEffectTime)
            {
                DoAddictionEffect(uid);
                addicted.NextEffectTime = curTime + TimeSpan.FromSeconds(_random.NextFloat(MinEffectInterval, MaxEffectInterval));
            }

            // #Misfits Change /Add:/ Periodic withdrawal damage + stamina drain
            if (curTime >= addicted.WithdrawalNextTick)
            {
                DoWithdrawalTick(uid, addicted);
                addicted.WithdrawalNextTick = curTime + TimeSpan.FromSeconds(WithdrawalTickInterval);
            }
        }
    }

    // #Misfits Change /Add:/ Detects suppression transitions and fires mood + speed modifier events.
    /// <summary>
    ///     Evaluates whether suppression state changed this tick.
    ///     On a <c>suppressed → unsuppressed</c> transition: applies withdrawal mood effect and refreshes
    ///     movement speed modifiers. On <c>unsuppressed → suppressed</c>: removes mood and restores speed.
    /// </summary>
    private void UpdateSuppressed(EntityUid uid, AddictedComponent component, TimeSpan curTime)
    {
        var nowSuppressed = component.SuppressionEndTime != null && curTime < component.SuppressionEndTime;
        component.Suppressed = nowSuppressed;

        if (nowSuppressed == component.PreviousSuppressed)
            return;

        component.PreviousSuppressed = nowSuppressed;

        if (!nowSuppressed)
            OnWithdrawalBegan(uid, component);
        else
            OnWithdrawalRelieved(uid, component);
    }

    /// <summary>
    ///     Called once when suppression ends and the entity enters active withdrawal.
    ///     Applies the withdrawal mood effect and triggers a movement speed recalculation.
    /// </summary>
    private void OnWithdrawalBegan(EntityUid uid, AddictedComponent component)
    {
        if (!string.IsNullOrEmpty(component.WithdrawalMoodEffect))
            RaiseLocalEvent(uid, new MoodEffectEvent(component.WithdrawalMoodEffect));

        if (component.WithdrawalSpeedPenalty < 1.0f)
            _movement.RefreshMovementSpeedModifiers(uid);
    }

    /// <summary>
    ///     Called once when suppression begins (drug taken / Fixer used).
    ///     Removes the withdrawal mood effect and restores normal movement speed.
    /// </summary>
    private void OnWithdrawalRelieved(EntityUid uid, AddictedComponent component)
    {
        if (!string.IsNullOrEmpty(component.WithdrawalMoodEffect))
            RaiseLocalEvent(uid, new MoodRemoveEffectEvent(component.WithdrawalMoodEffect));

        if (component.WithdrawalSpeedPenalty < 1.0f)
            _movement.RefreshMovementSpeedModifiers(uid);
    }

    // #Misfits Change /Add:/ Applies the movement speed penalty while in active withdrawal.
    private void OnRefreshMoveSpeed(EntityUid uid, AddictedComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        if (!component.Suppressed && component.WithdrawalSpeedPenalty < 1.0f)
            args.ModifySpeed(component.WithdrawalSpeedPenalty, component.WithdrawalSpeedPenalty);
    }

    // #Misfits Change /Add:/ Applies a single periodic tick of withdrawal damage + stamina drain.
    /// <summary>
    ///     Fires every <see cref="WithdrawalTickInterval"/> seconds while in active un-suppressed withdrawal.
    ///     Applies harm that scales thematically with the drug type:
    ///     healers → poison damage, stimulants → stamina drain, STR drugs → brute damage, etc.
    /// </summary>
    private void DoWithdrawalTick(EntityUid uid, AddictedComponent component)
    {
        if (component.WithdrawalDamage != null)
            _damageable.TryChangeDamage(uid, component.WithdrawalDamage, interruptsDoAfters: false);

        if (component.WithdrawalStaminaDrain > 0.0f)
            _stamina.TakeStaminaDamage(uid, component.WithdrawalStaminaDrain, visual: false);
    }

    protected override void UpdateTime(EntityUid uid)
    {
        if (!TryComp<AddictedComponent>(uid, out var addicted))
            return;

        var curTime = _timing.CurTime;
        addicted.LastMetabolismTime = curTime;
        addicted.SuppressionEndTime = curTime + TimeSpan.FromSeconds(SuppressionDuration);
    }

    // #Misfits Change /Add:/ Drug-specific chat messages when addiction is first applied.
    // Grows/deepens/severe messages are intentionally suppressed — they fire on every
    // metabolism tick and flood chat. The fading messages (CheckAddictionFading) already
    // tell the player when the addiction state is changing.
    protected override void OnAddictionApplied(EntityUid uid, bool isNew)
    {
        if (!isNew)
            return; // only report the very first time to avoid per-dose spam

        if (!TryComp<AddictedComponent>(uid, out var addicted))
            return;

        if (string.IsNullOrEmpty(addicted.DrugName))
            return;

        if (!TryGetSession(uid, out var session))
            return;

        SendAddictionChat(session, "addiction-drug-first", addicted.DrugName);

        // Keep LastReportedTier current so fading messages fire correctly
        if (_statusEffects.TryGetTime(uid, StatusEffectKey, out var times))
        {
            var remaining = times.Value.Item2 - _timing.CurTime;
            var tier = GetAddictionTier(remaining.TotalSeconds);
            if (tier > addicted.LastReportedTier)
                addicted.LastReportedTier = tier;
        }
    }

    // #Misfits Change /Add:/ Checks for downward tier transitions and sends fading messages.
    private void CheckAddictionFading(EntityUid uid, AddictedComponent addicted, TimeSpan curTime)
    {
        if (string.IsNullOrEmpty(addicted.DrugName))
            return;

        if (!_statusEffects.TryGetTime(uid, StatusEffectKey, out var times))
            return;

        var remaining = times.Value.Item2 - curTime;
        if (remaining <= TimeSpan.Zero)
            return;

        var tier = GetAddictionTier(remaining.TotalSeconds);

        if (addicted.LastReportedTier == -1)
        {
            addicted.LastReportedTier = tier;
            return;
        }

        // Silent upward adjustment when a new dose raises the tier
        if (tier >= addicted.LastReportedTier)
        {
            addicted.LastReportedTier = tier;
            return;
        }

        if (!TryGetSession(uid, out var session))
        {
            addicted.LastReportedTier = tier;
            return;
        }

        SendAddictionChat(session, GetFadingMessageKey(tier), addicted.DrugName);
        addicted.LastReportedTier = tier;
    }

    // #Misfits Change /Add:/ Fires when the addiction expires or is cured — cleans up effects.
    private void OnAddictionRemoved(EntityUid uid, AddictedComponent component, ComponentRemove args)
    {
        // Clean up mood effect regardless of player session
        if (!string.IsNullOrEmpty(component.WithdrawalMoodEffect))
            RaiseLocalEvent(uid, new MoodRemoveEffectEvent(component.WithdrawalMoodEffect));

        // Restore movement speed
        if (component.WithdrawalSpeedPenalty < 1.0f)
            _movement.RefreshMovementSpeedModifiers(uid);

        // Send clean chat message if the addiction was tracked
        if (string.IsNullOrEmpty(component.DrugName) || component.LastReportedTier == -1)
            return;

        if (!TryGetSession(uid, out var session))
            return;

        SendAddictionChat(session, "addiction-drug-clean", component.DrugName);
    }

    private static int GetAddictionTier(double remainingSeconds)
    {
        if (remainingSeconds >= TierSevereThreshold)   return 3;
        if (remainingSeconds >= TierModerateThreshold) return 2;
        if (remainingSeconds >= TierMildThreshold)     return 1;
        return 0;
    }

    private static string GetFadingMessageKey(int tier)
    {
        return tier switch
        {
            2 => "addiction-drug-fading-moderate",
            1 => "addiction-drug-fading-mild",
            _ => "addiction-drug-fading-nearly",
        };
    }

    private bool TryGetSession(EntityUid uid, out ICommonSession? session)
    {
        if (TryComp<ActorComponent>(uid, out var actor))
        {
            session = actor.PlayerSession;
            return true;
        }

        session = null;
        return false;
    }

    private void SendAddictionChat(Robust.Shared.Player.ICommonSession? session, string locKey, string drugName)
    {
        if (session == null)
            return;

        var text = Loc.GetString(locKey, ("drug", drugName));
        _chat.ChatMessageToOne(
            ChatChannel.Local,
            text,
            text,
            EntityUid.Invalid,
            false,
            session.Channel);
    }

    private void DoAddictionEffect(EntityUid uid)
    {
        // Private withdrawal flavour text — only the addicted player sees it.
        var msg = GetRandomPopup();
        if (msg != null && TryGetSession(uid, out var session) && session != null)
            _chat.ChatMessageToOne(ChatChannel.Local, msg, msg, EntityUid.Invalid, false, session.Channel);
    }

    private string? GetRandomPopup()
    {
        if (!_proto.TryIndex<LocalizedDatasetPrototype>("AddictionEffects", out var dataset))
            return null;

        // #Misfits Fix - Localize the picked key; Pick() returns a raw LocId, not the translated string
        // #Sirius Fix: IReadOnlyList.Pick() uses index 0..Count-1, but LocalizedDatasetValues[0]
        // is "addiction-effect-0" (no such key) while the enumerator is 1-based. Pick a 1-based
        // index so every one of the 10 translations is reachable and no raw LocId is shown.
        return Loc.GetString(dataset.Values[_random.Next(1, dataset.Values.Count + 1)]);
    }
}
