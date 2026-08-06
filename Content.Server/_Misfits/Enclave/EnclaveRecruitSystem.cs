// #Misfits Add - Per-round Enclave recruitment system.
// Enclave members get a right-click "Recruit" verb on player entities.
// Recruited players are assigned the EnclaveRecruit job so their time counts
// toward Enclave department role timers. Resets on death or round restart.

using System.Linq;
using Content.Server.EUI;
using Content.Server.Mind;
using Content.Shared._Misfits.Enclave;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Content.Shared.Verbs;
using Content.Shared.IdentityManagement;
using Robust.Shared.Prototypes;

namespace Content.Server._Misfits.Enclave;

public sealed class EnclaveRecruitSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly SharedRoleSystem _roles = default!;
    [Dependency] private readonly SharedMindSystem _minds = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly EuiManager _eui = default!;
    [Dependency] private readonly EnclaveMicroBombSystem _microBombs = default!;

    /// <summary>Enclave department ID from the department prototype.</summary>
    private const string EnclaveDepartmentId = "Enclave";

    /// <summary>The literal job assigned to accepted recruits.</summary>
    private const string EnclaveRecruitJobId = "EnclaveRecruit";

    public override void Initialize()
    {
        base.Initialize();

        // Show "Recruit" verb on living player entities for Enclave members
        SubscribeLocalEvent<MindContainerComponent, GetVerbsEvent<InteractionVerb>>(OnGetInteractionVerbs);

        // Remove recruitment on death
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);

        // Clean up all recruitments on round restart
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    /// <summary>
    /// Add "Recruit" verb for Enclave members on player entities.
    /// </summary>
    private void OnGetInteractionVerbs(
        EntityUid target,
        MindContainerComponent targetMind,
        GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;

        // User must be a living player with an Enclave job
        if (!IsEnclaveMember(user))
            return;

        // Target must be a living player with a mind
        if (!targetMind.HasMind)
            return;

        // Target must not already be recruited
        if (HasComp<EnclaveRecruitMindComponent>(targetMind.Mind))
            return;

        // Target must be alive (not dead/ghost)
        if (!TryComp<MobStateComponent>(target, out var mobState)
            || mobState.CurrentState != MobState.Alive)
            return;

        // Don't show verb on self
        if (user == target)
            return;

        args.Verbs.Add(new InteractionVerb
        {
            Text = "Recruit",
            Category = VerbCategory.Interaction,
            Act = () => RecruitPlayer(target, targetMind, user),
        });
    }

    /// <summary>
    /// Remove recruitment when the player dies.
    /// </summary>
    private void OnMobStateChanged(MobStateChangedEvent ev)
    {
        if (ev.NewMobState != MobState.Dead)
            return;

        RemoveRecruitment(ev.Target);
    }

    /// <summary>
    /// Clean up all EnclaveRecruitMindComponents on round restart.
    /// </summary>
    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        var query = EntityQueryEnumerator<EnclaveRecruitMindComponent>();
        while (query.MoveNext(out var uid, out var recruit))
        {
            RestorePreviousJob(uid, recruit);
            RemComp<EnclaveRecruitMindComponent>(uid);
        }
    }

    /// <summary>
    /// Show a confirmation dialog to the target. Only recruits if they accept.
    /// </summary>
    private void RecruitPlayer(EntityUid target, MindContainerComponent targetMind, EntityUid user)
    {
        if (!targetMind.HasMind)
            return;

        var mindId = targetMind.Mind.Value;

        // Already recruited
        if (HasComp<EnclaveRecruitMindComponent>(mindId))
            return;

        // Get the target's player session for the dialog
        if (!_minds.TryGetSession(mindId, out var targetSession))
            return;

        var userName = Identity.Name(user, EntityManager);
        var targetName = Identity.Name(target, EntityManager);

        _eui.OpenEui(new EnclaveRecruitEui(
            targetName,
            () =>
            {
                // Double-check they're still not recruited (may have been recruited while dialog was open)
                if (!targetMind.HasMind || HasComp<EnclaveRecruitMindComponent>(targetMind.Mind!.Value))
                    return;

                ApplyRecruitment(target, targetMind, user, userName, targetName);
            },
            () =>
            {
                _popup.PopupEntity(
                    Loc.GetString("enclave-recruit-declined", ("target", (object)targetName)),
                    user,
                    user,
                    PopupType.MediumCaution);
            }),
            targetSession);
    }

    /// <summary>
    /// Actually apply the recruitment (called after target confirms).
    /// </summary>
    private void ApplyRecruitment(EntityUid target, MindContainerComponent targetMind, EntityUid user,
        string userName, string targetName)
    {
        if (!targetMind.HasMind)
            return;

        var mindId = targetMind.Mind!.Value;

        if (HasComp<EnclaveRecruitMindComponent>(mindId))
            return;

        ProtoId<JobPrototype>? previousJob = null;
        if (_jobs.MindTryGetJob(mindId, out var currentJob, out _))
        {
            previousJob = currentJob.Prototype;
            _roles.MindRemoveRole<JobComponent>(mindId);
        }

        var recruit = AddComp<EnclaveRecruitMindComponent>(mindId);
        recruit.PreviousJob = previousJob;
        _roles.MindAddRole(mindId, new JobComponent { Prototype = EnclaveRecruitJobId });
        _microBombs.Implant(target);

        _popup.PopupEntity(
            Loc.GetString("enclave-recruit-popup-target", ("user", userName)),
            target,
            target,
            PopupType.Medium);

        _popup.PopupEntity(
            Loc.GetString("enclave-recruit-popup-user", ("target", targetName)),
            user,
            user,
            PopupType.Medium);
    }

    /// <summary>
    /// Remove recruitment and restore the player's former job on death.
    /// </summary>
    private void RemoveRecruitment(EntityUid body)
    {
        if (!TryComp<MindContainerComponent>(body, out var mindContainer) || !mindContainer.HasMind)
            return;

        var mindId = mindContainer.Mind.Value;

        if (!TryComp<EnclaveRecruitMindComponent>(mindId, out var recruit))
            return;

        RestorePreviousJob(mindId, recruit);
        RemComp<EnclaveRecruitMindComponent>(mindId);

        // Notify the player they are no longer recruited
        if (_minds.TryGetSession(mindId, out _))
        {
            _popup.PopupEntity(
                Loc.GetString("enclave-recruit-lost"),
                body,
                body,
                PopupType.MediumCaution);
        }
    }

    private void RestorePreviousJob(EntityUid mindId, EnclaveRecruitMindComponent recruit)
    {
        // Do not overwrite a job that another system deliberately assigned
        // after recruitment.
        if (!_jobs.MindHasJobWithId(mindId, EnclaveRecruitJobId))
            return;

        _roles.MindRemoveRole<JobComponent>(mindId);

        if (recruit.PreviousJob is { } previousJob && _prototypes.HasIndex(previousJob))
            _roles.MindAddRole(mindId, new JobComponent { Prototype = previousJob }, silent: true);
    }

    /// <summary>
    /// Check if a user entity is an Enclave member (has an Enclave department job).
    /// </summary>
    private bool IsEnclaveMember(EntityUid uid)
    {
        // Get the mind ID from the user's entity
        if (!TryComp<MindContainerComponent>(uid, out var mindContainer) || !mindContainer.HasMind)
            return false;

        var mindId = mindContainer.Mind.Value;

        // Check if the user has a job component on their mind
        if (!_jobs.MindTryGetJob(mindId, out _, out var jobProto))
            return false;

        // Check if the job belongs to the Enclave department
        var department = _prototypes.Index<DepartmentPrototype>(EnclaveDepartmentId);
        return department.Roles.Contains(jobProto.ID);
    }
}
