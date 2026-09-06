// #Misfits Add — Spirit Board server system (séance / Ouija mechanic).
//
// Flow:
//   1. TribalShaman (or TribalElder) right-clicks the board → "Begin Séance" verb.
//   2. Cooldown / session guard checked. If clear: SpiritBoardSessionComponent is added with 120s timeout.
//      Atmospheric popup sent to nearby living tribe members: "The shaman's hands hover over the spirit board..."
//   3. Any GHOST within GhostRange sees a "Commune with the Living" verb on the board.
//   4. Ghost right-clicks → server opens the board's BUI on that ghost's session (response menu).
//   5. Ghost picks from YES/NO/MAYBE/GOODBYE/A-Z → SpiritBoardSelectResponseMessage arrives.
//   6. Server broadcasts an atmospheric popup to all living Tribe-dept players within BroadcastRange.
//   7. "GOODBYE" or session timeout → SpiritBoardSessionComponent removed, board goes quiet.

using Content.Shared._Misfits.SpiritBoard;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Player; // #Misfits Fix - ActorComponent lives in Robust.Shared.Player
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Misfits.SpiritBoard;

/// <summary>
/// Manages Spirit Board séance sessions: activation, ghost communion, response broadcast, and cleanup.
/// </summary>
public sealed class SpiritBoardSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    // Reusable buffer for proximity lookups
    private readonly HashSet<EntityUid> _proximityBuffer = new();

    public override void Initialize()
    {
        base.Initialize();

        // Shaman activation verb
        SubscribeLocalEvent<SpiritBoardComponent, GetVerbsEvent<ActivationVerb>>(OnGetActivationVerb);
        // Ghost communion verb (shown on an active board to nearby ghosts)
        SubscribeLocalEvent<SpiritBoardComponent, GetVerbsEvent<AlternativeVerb>>(OnGetGhostVerb);
        // Ghost response from BUI
        SubscribeLocalEvent<SpiritBoardComponent, SpiritBoardSelectResponseMessage>(OnGhostResponse);
    }

    // ──────────────────────────────────────────────────────────────────────────────────
    //  Update: timeout active sessions
    // ──────────────────────────────────────────────────────────────────────────────────

    // Throttle: only check once per second — sessions are 120s long.
    private float _accumulator;
    private const float UpdateInterval = 1f;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accumulator += frameTime;
        if (_accumulator < UpdateInterval)
            return;
        _accumulator = 0f;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<SpiritBoardSessionComponent, SpiritBoardComponent>();
        while (query.MoveNext(out var uid, out var session, out var board))
        {
            if (now < session.SessionEnd)
                continue;

            // Session timed out — end it with a quiet atmospheric message
            EndSession(uid, board, session, "spirit-board-session-timeout");
        }
    }

    // ──────────────────────────────────────────────────────────────────────────────────
    //  Shaman activation verb: "Begin Séance"
    // ──────────────────────────────────────────────────────────────────────────────────

    private void OnGetActivationVerb(EntityUid uid, SpiritBoardComponent component, GetVerbsEvent<ActivationVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        // Only the configured activator jobs can start a séance; ghosts handled separately
        if (!IsActivator(args.User, component))
            return;

        args.Verbs.Add(new ActivationVerb
        {
            Text = Loc.GetString(HasComp<SpiritBoardSessionComponent>(uid)
                ? "spirit-board-verb-end"
                : "spirit-board-verb-begin"),
            Act = () =>
            {
                if (HasComp<SpiritBoardSessionComponent>(uid))
                {
                    // Shaman manually closes the session
                    if (TryComp<SpiritBoardSessionComponent>(uid, out var existingSession))
                        EndSession(uid, component, existingSession, "spirit-board-session-ended-shaman");
                    return;
                }

                // Check cooldown
                if (component.CooldownEnd.HasValue && _timing.CurTime < component.CooldownEnd.Value)
                {
                    var remaining = (int) Math.Ceiling((component.CooldownEnd.Value - _timing.CurTime).TotalSeconds);
                    _popup.PopupEntity(Loc.GetString("spirit-board-cooldown", ("seconds", remaining)),
                        uid, args.User, PopupType.SmallCaution);
                    return;
                }

                BeginSession(uid, component, args.User);
            },
            Priority = 1,
        });
    }

    // ──────────────────────────────────────────────────────────────────────────────────
    //  Ghost verb: "Commune with the Living" (alternate verb — only when session active)
    // ──────────────────────────────────────────────────────────────────────────────────

    private void OnGetGhostVerb(EntityUid uid, SpiritBoardComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract)
            return;

        // Only show verb if a séance session is active
        if (!HasComp<SpiritBoardSessionComponent>(uid))
            return;

        // Only ghosts see this verb
        if (!HasComp<GhostComponent>(args.User))
            return;

        // Check ghost is within GhostRange of the board
        if (!IsInRange(args.User, uid, component.GhostRange))
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("spirit-board-verb-commune"),
            Act = () =>
            {
                // Open the response selection BUI for this ghost
                _ui.OpenUi(uid, SpiritBoardUiKey.Key, args.User);
            },
            Priority = 1,
        });
    }

    // ──────────────────────────────────────────────────────────────────────────────────
    //  Response handler: ghost picked a token from the BUI
    // ──────────────────────────────────────────────────────────────────────────────────

    private void OnGhostResponse(EntityUid uid, SpiritBoardComponent component, SpiritBoardSelectResponseMessage args)
    {
        // Session must still be active
        if (!TryComp<SpiritBoardSessionComponent>(uid, out var session))
            return;

        var ghostEntity = args.Actor; // #Misfits Fix - .Session removed from BUI messages; use args.Actor
        if (ghostEntity == default)
            return;

        var response = args.Response;

        // GOODBYE ends the session
        if (response == "GOODBYE")
        {
            // Broadcast the farewell first, then close
            BroadcastResponse(uid, component, ghostEntity, "GOODBYE");
            EndSession(uid, component, session, "spirit-board-session-ended-goodbye");
            return;
        }

        // Broadcast the response as an atmospheric popup to nearby living tribe members
        BroadcastResponse(uid, component, ghostEntity, response);
    }

    // ──────────────────────────────────────────────────────────────────────────────────
    //  Session helpers
    // ──────────────────────────────────────────────────────────────────────────────────

    private void BeginSession(EntityUid uid, SpiritBoardComponent component, EntityUid shaman)
    {
        var session = AddComp<SpiritBoardSessionComponent>(uid);
        session.Shaman = shaman;
        session.SessionEnd = _timing.CurTime + component.SessionDuration;

        // Atmospheric broadcast to nearby tribe
        var shamanName = Name(shaman);
        BroadcastArea(uid, component.BroadcastRange, component.TargetDepartment,
            Loc.GetString("spirit-board-session-started", ("shaman", shamanName)));

        Log.Info($"[SpiritBoard] Séance started by {ToPrettyString(shaman)}");
    }

    private void EndSession(EntityUid uid, SpiritBoardComponent component, SpiritBoardSessionComponent session, string locKey)
    {
        // Set cooldown from now
        component.CooldownEnd = _timing.CurTime + component.Cooldown;

        RemComp<SpiritBoardSessionComponent>(uid);

        // Close any open ghost BUIs on the board
        _ui.CloseUi(uid, SpiritBoardUiKey.Key);

        // Atmospheric farewell to nearby tribe
        BroadcastArea(uid, component.BroadcastRange, component.TargetDepartment, Loc.GetString(locKey));

        Log.Info($"[SpiritBoard] Séance ended on {ToPrettyString(uid)}");
    }

    /// <summary>
    /// Builds and sends the atmospheric flavored popup for a ghost response.
    /// Single-letter responses: "A chill hand traces the letter: X"
    /// Word responses (YES/NO/MAYBE): "The planchette drifts to: YES"
    /// </summary>
    private void BroadcastResponse(EntityUid boardUid, SpiritBoardComponent component, EntityUid ghost, string response)
    {
        string locKey;
        if (response.Length == 1 && char.IsLetter(response[0]))
            locKey = "spirit-board-response-letter";
        else
            locKey = "spirit-board-response-word";

        var msgText = Loc.GetString(locKey, ("response", response));

        BroadcastArea(boardUid, component.BroadcastRange, component.TargetDepartment, msgText);
    }

    /// <summary>
    /// Sends a popup to all living Tribe-dept players within range of the board.
    /// </summary>
    private void BroadcastArea(EntityUid boardUid, float range, string department, string message)
    {
        _proximityBuffer.Clear();
        _lookup.GetEntitiesInRange(Transform(boardUid).Coordinates, range, _proximityBuffer);

        foreach (var targetUid in _proximityBuffer)
        {
            if (!HasComp<ActorComponent>(targetUid))
                continue;

            if (_mobState.IsDead(targetUid))
                continue;

            if (!IsInDepartment(targetUid, department))
                continue;

            _popup.PopupEntity(message, targetUid, targetUid, PopupType.Large);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────────────
    //  Guards
    // ──────────────────────────────────────────────────────────────────────────────────

    private bool IsActivator(EntityUid uid, SpiritBoardComponent component)
    {
        if (!_mind.TryGetMind(uid, out var mindId, out _))
            return false;

        if (!_jobs.MindTryGetJob(mindId, out _, out var jobPrototype))
            return false;

        return component.ActivatorJobs.Contains(jobPrototype.ID);
    }

    // #Misfits Fix - resolved against the target department's role list so dual-citizenship tribe jobs
    // (SuperMutantTribal, SyntheticProtectronTribal) receive spirit board broadcasts.
    private bool IsInDepartment(EntityUid uid, string departmentId)
    {
        if (!_mind.TryGetMind(uid, out var mindId, out _))
            return false;

        if (!_jobs.MindTryGetJob(mindId, out _, out var jobPrototype))
            return false;

        return _prototypes.TryIndex<DepartmentPrototype>(departmentId, out var dept)
            && dept.Roles.Contains(jobPrototype.ID);
    }

    private bool IsInRange(EntityUid a, EntityUid b, float range)
    {
        var posA = _transform.GetWorldPosition(a);
        var posB = _transform.GetWorldPosition(b);
        return (posA - posB).LengthSquared() <= range * range;
    }
}
