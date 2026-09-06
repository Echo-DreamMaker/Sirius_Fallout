// #Misfits Add — Smoke Signal server system.
// Allows any Tribe-department player to activate a signal fire, type a short message,
// and broadcast it to all online Tribe-department players in faction chat.
//
// Flow:
//   1. Player right-clicks a SmokeSignalComponent entity → verb appears if they are in Tribe department.
//   2. Server opens the BUI (text-input window) on the activator's session.
//   3. Player types message and confirms → SmokeSignalSendMessage arrives.
//   4. Server validates cooldown, clamps message, records cooldown end time.
//   5. Message is broadcast in chat to every living Tribe-dept player.

using Content.Server.Chat.Managers;
using Content.Shared._Misfits.SmokeSignal;
using Content.Shared._Misfits.Deathclaw;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Chat;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Roles; // #Misfits Add - resolve dual-department Willower jobs.
using Content.Shared.Roles.Jobs;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Player; // #Misfits Fix - ActorComponent lives in Robust.Shared.Player
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Misfits.SmokeSignal;

/// <summary>
/// Handles the smoke signal activation verb, BUI messaging, cooldown management, and department broadcast.
/// </summary>
public sealed class SmokeSignalSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IChatManager _chatManager = default!; // #Misfits Change - deliver signals through targeted faction chat.
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!; // #Misfits Add - resolve dual-department Willower jobs

    private readonly HashSet<EntityUid> _nearbyBuffer = new();

    public override void Initialize()
    {
        base.Initialize();

        // Register the activation verb on any entity with SmokeSignalComponent
        SubscribeLocalEvent<SmokeSignalComponent, GetVerbsEvent<ActivationVerb>>(OnGetVerb);

        // E/complex activation should open the signal UI for tribe members before bonfire extinguishing handles it.
        SubscribeLocalEvent<SmokeSignalComponent, ActivateInWorldEvent>(OnActivateInWorld, before: new[] { typeof(FlammableSystem) });

        // Handle message sent from the BUI text input
        SubscribeLocalEvent<SmokeSignalComponent, SmokeSignalSendMessage>(OnSendMessage);
    }

    // ──────────────────────────────────────────────────────────────────────────────────
    //  Verb: "Send Smoke Signal"
    // ──────────────────────────────────────────────────────────────────────────────────

    private void OnGetVerb(EntityUid uid, SmokeSignalComponent component, GetVerbsEvent<ActivationVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        // #Misfits Change - respect optional activator role allowlist.
        if (!CanUse(args.User, component))
            return;

        args.Verbs.Add(new ActivationVerb
        {
            Text = Loc.GetString(component.Verb), // #Misfits Change - allow Tree-specific wording
            Act = () =>
            {
                TryOpenSignalUi(uid, component, args.User);
            },
            Priority = 1,
        });
    }

    private void OnActivateInWorld(EntityUid uid, SmokeSignalComponent component, ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        if (!component.OpenOnActivate || !CanUse(args.User, component)) // #Misfits Change - Tree activation stays verb-only.
            return;

        if (!TryOpenSignalUi(uid, component, args.User))
            return;

        args.Handled = true;
    }

    // ──────────────────────────────────────────────────────────────────────────────────
    //  BUI message handler: validate and broadcast
    // ──────────────────────────────────────────────────────────────────────────────────

    private void OnSendMessage(EntityUid uid, SmokeSignalComponent component, SmokeSignalSendMessage args)
    {
        if (args.Actor is not { Valid: true } sender) // #Misfits Fix - .Session removed from BUI messages; use args.Actor
            return;

        if (!CanUse(sender, component)) // #Misfits Change - enforce optional sender role allowlist.
            return;

        // Re-validate cooldown (race guard)
        if (component.CooldownEnd.HasValue && _timing.CurTime < component.CooldownEnd.Value)
            return;

        // Clamp and sanitize message
        var message = args.Message.Trim();
        if (message.Length == 0)
            return;

        if (message.Length > component.MaxMessageLength)
            message = message[..component.MaxMessageLength];

        // Record cooldown
        component.CooldownEnd = _timing.CurTime + component.Cooldown;

        // Build the broadcast text
        var broadcastText = Loc.GetString(component.BroadcastMessage,
            ("sender", MetaData(sender).EntityName),
            ("message", message)); // #Misfits Change - identify the signal sender.

        var filter = Filter.Empty();
        foreach (var playerUid in GetRecipients(component))
            filter.AddPlayer(Comp<ActorComponent>(playerUid).PlayerSession);

        // #Misfits Change - preserve recipient rules while moving full messages to private faction chat.
        _chatManager.ChatMessageToManyFiltered(
            filter,
            ChatChannel.Radio,
            broadcastText,
            FormattedMessage.EscapeText(broadcastText),
            uid,
            hideChat: false,
            recordReplay: false,
            component.AnnouncementColor);

        // Also send an atmospheric notice to nearby non-tribe bystanders
        // so the signal is observable in-world (and testable without a tribe job)
        if (component.NearbyRange > 0f)
        {
            var nearbyText = Loc.GetString("smoke-signal-nearby");
            _nearbyBuffer.Clear();
            _lookup.GetEntitiesInRange(Transform(uid).Coordinates, component.NearbyRange, _nearbyBuffer);

            foreach (var nearbyUid in _nearbyBuffer)
            {
                if (!HasComp<ActorComponent>(nearbyUid))
                    continue;

                if (_mobState.IsDead(nearbyUid))
                    continue;

                // #Misfits Change - only full-message recipients skip the nearby notice.
                if (IsInDepartment(nearbyUid, component))
                    continue;

                _popup.PopupEntity(nearbyText, nearbyUid, nearbyUid, PopupType.Medium);
            }
        }

        // Log so admins can see in the server log
        Log.Info($"[SmokeSignal] {ToPrettyString(sender)} sent: {message}");
    }

    // ──────────────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ──────────────────────────────────────────────────────────────────────────────────

    private bool TryOpenSignalUi(EntityUid uid, SmokeSignalComponent component, EntityUid user)
    {
        // Check cooldown before opening the UI
        if (component.CooldownEnd.HasValue && _timing.CurTime < component.CooldownEnd.Value)
        {
            var remaining = (int) Math.Ceiling((component.CooldownEnd.Value - _timing.CurTime).TotalSeconds);
            _popup.PopupEntity(
                Loc.GetString(component.CooldownMessage, ("seconds", remaining)), // #Misfits Change - allow Tree-specific wording
                uid, user, PopupType.SmallCaution);
            return false;
        }

        // Open the text input window for the activating player
        _ui.OpenUi(uid, SmokeSignalUiKey.Key, user);
        return true;
    }

    // #Misfits Fix - department membership is resolved against the target department's role list so
    // dual-citizenship tribe jobs (SuperMutantTribal, SyntheticProtectronTribal) authorize correctly.
    internal bool CanUse(EntityUid uid, SmokeSignalComponent component)
    {
        if (IsBwonsamdiTribalSignal(uid, component))
            return true;

        if (!_mind.TryGetMind(uid, out var mindId, out _)
            || !_jobs.MindTryGetJob(mindId, out _, out var job))
            return false;

        if (!_prototypes.TryIndex<DepartmentPrototype>(component.TargetDepartment, out var targetDepartment)
            || !targetDepartment.Roles.Contains(job.ID))
            return false;

        return component.ActivatorJobs is not { Count: > 0 } || component.ActivatorJobs.Contains(job.ID);
    }

    // #Misfits Fix - recipients resolved against the target department's role list so dual-citizenship
    // tribe jobs (SuperMutantTribal, SyntheticProtectronTribal) receive broadcasts.
    internal bool IsInDepartment(EntityUid uid, SmokeSignalComponent component)
    {
        if (IsBwonsamdiTribalSignal(uid, component))
            return true;

        if (!_mind.TryGetMind(uid, out var mindId, out _)
            || !_jobs.MindTryGetJob(mindId, out _, out var job))
            return false;

        return _prototypes.TryIndex<DepartmentPrototype>(component.TargetDepartment, out var targetDepartment)
            && targetDepartment.Roles.Contains(job.ID);
    }

    // Bwonsamdi is a Willower ally rather than a Tribe job. Keep this exception
    // scoped to tribal signal fires so ordinary sentient Deathclaws gain nothing.
    private bool IsBwonsamdiTribalSignal(EntityUid uid, SmokeSignalComponent component)
    {
        return component.TargetDepartment == "Tribe" && HasComp<BwonsamdiComponent>(uid);
    }

    // #Misfits Change - share component-specific living recipient selection between delivery and regression coverage.
    internal IEnumerable<EntityUid> GetRecipients(SmokeSignalComponent component)
    {
        var query = EntityQueryEnumerator<ActorComponent>();
        while (query.MoveNext(out var playerUid, out _))
        {
            if (_mobState.IsDead(playerUid) || !IsInDepartment(playerUid, component))
                continue;

            yield return playerUid;
        }
    }
}
