// #Misfits Add - Fluff-only "Ad Viktoriya" recruitment verb for the Brotherhood
// of Steel. Tribute to Founder Viktoriya Plum, killed in the schism; Her
// loyalists remain. Pure roleplay prompt: no job, no department, no time gate,
// no whitelist, no binding mechanic. The recruited player takes the oath
// in-character and carries it for as long as they choose.
//
// Flow mirrors the Enclave recruit verb: right-click > "Recruit" on a living
// player opens a themed consent prompt on the target. Accepting/declining only
// produces flavor popups — nothing mechanical changes.
//
// Who gets the verb: any entity carrying the BosRecruiterComponent (added via
// addcomp / View Variables — admins can tag their own character or aghost, no
// job needed), plus any Brotherhood of Steel department member. The component
// bypasses the access/interaction checks so it works as an aghost at any range.

using Content.Server.EUI;
using Content.Server.Mind;
using Content.Shared._Misfits.BrotherhoodOfSteel;
using Content.Shared.IdentityManagement;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Roles.Jobs;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;

namespace Content.Server._Misfits.BrotherhoodOfSteel;

public sealed class BosRecruitSystem : EntitySystem
{
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly SharedMindSystem _minds = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly EuiManager _eui = default!;

    /// <summary>Only members of this department may offer the Ad Viktoriya oath.</summary>
    private const string BrotherhoodDepartmentId = "BrotherhoodOfSteel";

    public override void Initialize()
    {
        base.Initialize();

        // Show "Recruit" verb on living player entities. Subscribed on
        // MobStateComponent rather than MindContainerComponent: the Enclave
        // recruit system already owns the (MindContainerComponent,
        // GetVerbsEvent) pair and this engine throws on duplicate
        // subscriptions. The verb requires a living target anyway.
        SubscribeLocalEvent<MobStateComponent, GetVerbsEvent<InteractionVerb>>(OnGetInteractionVerbs);
    }

    /// <summary>
    /// Add the "Recruit" verb for designated recruiters on living players.
    /// </summary>
    private void OnGetInteractionVerbs(
        EntityUid target,
        MobStateComponent mobState,
        GetVerbsEvent<InteractionVerb> args)
    {
        var user = args.User;

        // Admin bypass: anyone carrying BosRecruiterComponent (addcomp / View
        // Variables) may recruit regardless of job — and even as an aghost at
        // any range, since the normal access/interaction checks are skipped.
        var hasBypass = HasComp<BosRecruiterComponent>(user);

        // Normal path (Brotherhood department members) still requires normal
        // access and interaction.
        if (!hasBypass && (!args.CanAccess || !args.CanInteract))
            return;

        // User must be a designated recruiter (bypass) or hold a Brotherhood job
        if (!hasBypass && !IsBrotherhoodMember(user))
            return;

        // Target must be alive (not dead/ghost)
        if (mobState.CurrentState != MobState.Alive)
            return;

        // Target must be a player with a mind
        if (!TryComp<MindContainerComponent>(target, out var targetMind) || !targetMind.HasMind)
            return;

        // Don't show verb on self
        if (user == target)
            return;

        args.Verbs.Add(new InteractionVerb
        {
            Text = "Recruit",
            Category = VerbCategory.Interaction,
            Act = () => OfferOath(target, targetMind, user),
        });
    }

    /// <summary>
    /// Open the fluff consent prompt on the target. Accepting/declining only
    /// produces flavor popups — no job, bomb, or state is applied.
    /// </summary>
    private void OfferOath(EntityUid target, MindContainerComponent targetMind, EntityUid user)
    {
        if (!targetMind.HasMind)
            return;

        var mindId = targetMind.Mind.Value;

        // Get the target's player session for the prompt
        if (!_minds.TryGetSession(mindId, out var targetSession))
            return;

        var userName = Identity.Name(user, EntityManager);
        var targetName = Identity.Name(target, EntityManager);

        _eui.OpenEui(new BosRecruitEui(
            userName,
            targetName,
            () =>
            {
                _popup.PopupEntity(
                    Loc.GetString("bos-recruit-popup-user-swear", ("target", (object)targetName)),
                    user,
                    user,
                    PopupType.Medium);

                _popup.PopupEntity(
                    Loc.GetString("bos-recruit-popup-target-swear"),
                    target,
                    target,
                    PopupType.Medium);
            },
            () =>
            {
                _popup.PopupEntity(
                    Loc.GetString("bos-recruit-popup-user-declined", ("target", (object)targetName)),
                    user,
                    user,
                    PopupType.MediumCaution);
            }),
            targetSession);
    }

    /// <summary>
    /// Check if a user entity may offer the oath: must hold a job in the
    /// Brotherhood of Steel department.
    /// </summary>
    private bool IsBrotherhoodMember(EntityUid uid)
    {
        if (!TryComp<MindContainerComponent>(uid, out var mindContainer) || !mindContainer.HasMind)
            return false;

        var mindId = mindContainer.Mind.Value;

        if (!_jobs.MindTryGetJob(mindId, out _, out var jobProto))
            return false;

        return _jobs.TryGetDepartment(jobProto.ID, out var department)
               && department.ID == BrotherhoodDepartmentId;
    }
}
