// #Misfits Change /Add/ - Shared server-side resuscitation helper for defibrillators and smelling salts.
using Content.Server.Atmos.Rotting;
using Content.Server.Chat.Systems;
using Content.Server.EUI;
using Content.Server.Ghost;
using Content.Server.Popups;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Rejuvenate;
using Robust.Shared.Player;

namespace Content.Server._Misfits.Medical;

public sealed class ResuscitationSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EuiManager _euiManager = default!;
    [Dependency] private readonly RottingSystem _rotting = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly ChatSystem _chat = default!;

    public void SendAttemptEmote(EntityUid target, EntityUid item)
    {
        _chat.TrySendInGameICMessage(target,
            Loc.GetString("resuscitation-attempt-emote", ("item", Name(item))),
            InGameICChatType.Emote,
            ChatTransmitRange.Normal,
            hideLog: true,
            ignoreActionBlocker: true);
    }

    public bool CanResuscitate(EntityUid target, bool targetCanBeAlive = false, bool canReviveCrit = true, MobStateComponent? mobState = null)
    {
        if (!Resolve(target, ref mobState, false))
            return false;

        if (!targetCanBeAlive && _mobState.IsAlive(target, mobState))
            return false;

        if (!targetCanBeAlive && !canReviveCrit && _mobState.IsCritical(target, mobState))
            return false;

        return true;
    }

    public ResuscitationResult TryResuscitate(
        EntityUid source,
        EntityUid target,
        EntityUid user,
        DamageSpecifier reviveHeal,
        string? reviveDoKey = null,
        MobStateComponent? mobState = null,
        MobThresholdsComponent? thresholds = null)
    {
        if (!Resolve(target, ref mobState, ref thresholds, false))
            return default;

        if (_rotting.IsRotten(target))
            return new ResuscitationResult(false, true, false, false);

        // Only dead targets are subject to the consent flow. If they aren't dead there's
        // nothing to revive here (callers gate on CanResuscitate, but stay defensive).
        if (!_mobState.IsDead(target, mobState))
            return new ResuscitationResult(false, false, false, false);

        // Look up the ghost's session.
        ICommonSession? session = null;
        MindComponent? mindComp = null;
        if (_mind.TryGetMind(target, out _, out var mind) && mind.Session is { } playerSession)
        {
            session = playerSession;
            mindComp = mind;
        }

        // No active session (SSD / disconnected) → cannot consent, do not revive.
        // The caller's "no mind" feedback message still plays via HasMindSession=false.
        if (session == null || mindComp == null)
            return new ResuscitationResult(false, false, false, false);

        // Mind is still attached to the body (player hasn't ghosted yet) → revive directly,
        // no prompt needed since they're already "there".
        if (mindComp.CurrentEntity == target)
        {
            var revivedNow = PerformRevive(source, target, user, reviveHeal, reviveDoKey, mobState, thresholds);
            return new ResuscitationResult(revivedNow, false, true, false);
        }

        // Mind is ghosted → ask first. Heal + state change only run if the player accepts.
        // Capture locals so the closure has stable references.
        var sourceCopy = source;
        var targetCopy = target;
        var userCopy = user;
        var healCopy = reviveHeal;
        var keyCopy = reviveDoKey;
        _euiManager.OpenEui(new ReturnToBodyEui(mindComp, _mind, () =>
        {
            // Re-validate at acceptance time — target may have been deleted, rotted,
            // or already revived by another medic between prompt and acceptance.
            if (Deleted(targetCopy) || _rotting.IsRotten(targetCopy))
                return;
            if (!TryComp<MobStateComponent>(targetCopy, out var ms) ||
                !TryComp<MobThresholdsComponent>(targetCopy, out var th))
                return;
            if (!_mobState.IsDead(targetCopy, ms))
                return;

            PerformRevive(sourceCopy, targetCopy, userCopy, healCopy, keyCopy, ms, th);
        }), session);

        return new ResuscitationResult(false, false, true, true);
    }

    /// <summary>
    /// Consent-aware full rejuvenation for exceptional abilities such as Mercy of the Loa.
    /// Unlike ordinary resuscitation, this deliberately bypasses damage thresholds only after
    /// validating an existing, non-rotten dead body and an active player session.
    /// </summary>
    public ResuscitationResult TryRejuvenateWithConsent(EntityUid target, Action<bool> onCompleted)
    {
        if (!TryComp<MobStateComponent>(target, out var mobState) ||
            !_mobState.IsDead(target, mobState))
            return default;

        if (_rotting.IsRotten(target))
            return new ResuscitationResult(false, true, false, false);

        if (!_mind.TryGetMind(target, out _, out var mind) || mind.Session is not { } session)
            return new ResuscitationResult(false, false, false, false);

        if (mind.CurrentEntity == target)
        {
            var success = PerformRejuvenate(target);
            onCompleted(success);
            return new ResuscitationResult(success, false, true, false);
        }

        var targetCopy = target;
        _euiManager.OpenEui(new ReturnToBodyEui(mind, _mind, () =>
        {
            if (Deleted(targetCopy)
                || _rotting.IsRotten(targetCopy)
                || !TryComp<MobStateComponent>(targetCopy, out var currentState)
                || !_mobState.IsDead(targetCopy, currentState)
                || mind.Session == null)
            {
                onCompleted(false);
                return;
            }

            onCompleted(PerformRejuvenate(targetCopy));
        }), session);

        return new ResuscitationResult(false, false, true, true);
    }

    private bool PerformRejuvenate(EntityUid target)
    {
        RaiseLocalEvent(target, new RejuvenateEvent());
        return !Deleted(target)
            && TryComp<MobStateComponent>(target, out var state)
            && !_mobState.IsDead(target, state);
    }

    // #Misfits Add - Centralised heal + state transition. Called either immediately
    // (mind in body) or from the ReturnToBodyEui accept callback (mind ghosted).
    private bool PerformRevive(
        EntityUid source,
        EntityUid target,
        EntityUid user,
        DamageSpecifier reviveHeal,
        string? reviveDoKey,
        MobStateComponent mobState,
        MobThresholdsComponent thresholds)
    {
        _damageable.TryChangeDamage(target, reviveHeal, true, origin: source);

        if (!_mobThreshold.TryGetThresholdForState(target, MobState.Dead, out var threshold) ||
            !TryComp<DamageableComponent>(target, out var damageableComponent) ||
            damageableComponent.TotalDamage >= threshold)
            return false;

        _mobState.ChangeMobState(target, MobState.Critical, mobState, user);

        if (!string.IsNullOrWhiteSpace(reviveDoKey))
        {
            _chat.TrySendInGameDoMessage(target,
                Loc.GetString(reviveDoKey, ("target", target)),
                ChatTransmitRange.Normal,
                hideLog: true,
                ignoreActionBlocker: true);
        }

        return true;
    }
}

// #Misfits Tweak - Added PromptSent so callers can distinguish "asked the player,
// awaiting decision" from a hard failure and play an appropriate sound/message.
public readonly record struct ResuscitationResult(bool Revived, bool Rotten, bool HasMindSession, bool PromptSent);
