// #Misfits Change - Ported from Delta-V surgery contamination system
using Content.Shared._Misfits.Special;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Forensics;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Shared._Misfits.Surgery.Contamination;

/// <summary>
///     Handles surgical instrument cleanliness, cross-contamination tracking, and sterilization.
///     Instruments used in surgery accumulate dirtiness and patient DNA, which can cause sepsis
///     if used on another patient without being cleaned first.
/// </summary>
public sealed class SurgeryCleanSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedSpecialSystem _special = default!;
    [Dependency] private readonly TagSystem _tags = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SurgeryCleansDirtComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<SurgeryCleansDirtComponent, SurgeryCleanDirtDoAfterEvent>(FinishCleaning);

        SubscribeLocalEvent<SurgeryDirtinessComponent, ExaminedEvent>(OnDirtyExamined);
        SubscribeLocalEvent<SurgeryCleansDirtComponent, GetVerbsEvent<UtilityVerb>>(OnUtilityVerb);

        SubscribeLocalEvent<SurgeryDirtinessComponent, SurgeryCleanedEvent>(OnCleanDirt);
        SubscribeLocalEvent<SurgeryCrossContaminationComponent, SurgeryCleanedEvent>(OnCleanDNA);
    }

    private void OnUtilityVerb(Entity<SurgeryCleansDirtComponent> ent, ref GetVerbsEvent<UtilityVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        var user = args.User;
        var target = args.Target;

        // #Sirius Fix: sanitization is for objects only. Mobs are washed with soap, not disinfected.
        if (HasComp<MobStateComponent>(target))
            return;

        var verb = new UtilityVerb()
        {
            Act = () => TryStartCleaning(ent, user, target),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/bubbles.svg.192dpi.png")),
            Text = Loc.GetString("sanitization-verb-text"),
            Message = Loc.GetString("sanitization-verb-message"),
            // Daisychained to forensics so we shouldn't leave forensic traces here
            DoContactInteraction = false
        };

        args.Verbs.Add(verb);
    }

    private void OnDirtyExamined(Entity<SurgeryDirtinessComponent> ent, ref ExaminedEvent args)
    {
        // Dirtiness 0-100 maps to cleanliness stages 0-5
        var stage = (int) Math.Ceiling(ent.Comp.Dirtiness.Double() / 20.0);
        args.PushMarkup(Loc.GetString($"surgery-cleanliness-{stage}"));
    }

    public bool RequiresCleaning(EntityUid target)
    {
        // #Sirius Fix: mobs must never be valid sanitizer targets, even if they carry surgery dirt.
        if (HasComp<MobStateComponent>(target))
            return false;

        var isDirty = TryComp<SurgeryDirtinessComponent>(target, out var dirtiness) && dirtiness.Dirtiness > 0;
        var isContaminated = TryComp<SurgeryCrossContaminationComponent>(target, out var contamination) && contamination.DNAs.Count > 0;
        return isDirty || isContaminated;
    }

    private void OnAfterInteract(Entity<SurgeryCleansDirtComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target == null)
            return;

        args.Handled = TryStartCleaning(ent, args.User, args.Target.Value);
    }

    /// <summary>
    /// #Sirius Fix: soap applied to an item is a laundry action with its own feedback, so
    /// "nothing to disinfect" must stay silent. The phrase is only shown when soap is NOT
    /// being applied to an item (e.g. a mob or a structure).
    /// </summary>
    public bool ShouldReportCannotClean(Entity<SurgeryCleansDirtComponent> cleaner, EntityUid target)
    {
        return !(_tags.HasTag(cleaner.Owner, "Soap") && HasComp<ItemComponent>(target));
    }

    public bool TryStartCleaning(Entity<SurgeryCleansDirtComponent> ent, EntityUid user, EntityUid target)
    {
        if (!RequiresCleaning(target))
        {
            // #Misfits TODO: Convert to private chat message — requires server-side event handler
            // since PopupClient is shared/client-predicted and IChatManager is server-only.
            if (ShouldReportCannotClean(ent, target))
                _popup.PopupClient(Loc.GetString("sanitization-cannot-clean", ("target", target)), user, user);
            return false;
        }

        // #Misfits TODO: Same as above — convert progress feedback to private chat message.
        _popup.PopupClient(Loc.GetString("sanitization-cleaning", ("target", target)), user, user);

        var doAfterArgs = new DoAfterArgs(EntityManager, user, _special.GetIntelligenceMedicalActionDelay(user, TimeSpan.FromSeconds(ent.Comp.CleanDelay)), new SurgeryCleanDirtDoAfterEvent(), ent, target: target)
        {
            BreakOnMove = true,
            NeedHand = true,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
        return true;
    }

    private void FinishCleaning(Entity<SurgeryCleansDirtComponent> ent, ref SurgeryCleanDirtDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Args.Target is not { } target)
            return;

        DoClean(ent, target);

        args.Repeat = RequiresCleaning(target);

        // Daisychain to forensics — sterilising something should also scrub DNA/fibers
        var daisyChainEvent = new CleanForensicsDoAfterEvent { DoAfter = args.DoAfter };
        RaiseLocalEvent(ent, daisyChainEvent);
    }

    public void DoClean(Entity<SurgeryCleansDirtComponent> cleaner, EntityUid target)
    {
        var ev = new SurgeryCleanedEvent(cleaner.Comp.DirtAmount, cleaner.Comp.DnaAmount);
        RaiseLocalEvent(target, ref ev);
    }

    private void OnCleanDirt(Entity<SurgeryDirtinessComponent> ent, ref SurgeryCleanedEvent args)
    {
        ent.Comp.Dirtiness = FixedPoint2.Max(FixedPoint2.Zero, ent.Comp.Dirtiness - args.DirtAmount);
        Dirty(ent);
    }

    private void OnCleanDNA(Entity<SurgeryCrossContaminationComponent> ent, ref SurgeryCleanedEvent args)
    {
        var i = 0;
        var count = args.DnaAmount;
        ent.Comp.DNAs.RemoveWhere(_ => i++ < count);
        Dirty(ent);
    }

    #region Public API

    /// <summary>
    ///     Gets the dirtiness level for an entity, adding SurgeryDirtinessComponent if it doesn't exist.
    /// </summary>
    public FixedPoint2 Dirtiness(EntityUid uid)
    {
        return EnsureComp<SurgeryDirtinessComponent>(uid).Dirtiness;
    }

    /// <summary>
    ///     Gets all DNA strings contaminating an entity, adding SurgeryCrossContaminationComponent if needed.
    /// </summary>
    public HashSet<string> CrossContaminants(EntityUid uid)
    {
        return EnsureComp<SurgeryCrossContaminationComponent>(uid).DNAs;
    }

    /// <summary>
    ///     Adds dirt to a tool or user. Amount is scaled down by 10x to keep values manageable.
    /// </summary>
    public void AddDirt(EntityUid uid, FixedPoint2 amount)
    {
        var comp = EnsureComp<SurgeryDirtinessComponent>(uid);
        comp.Dirtiness += amount * 0.1f;
        Dirty(uid, comp);
    }

    /// <summary>
    ///     Contaminates a tool or user with patient DNA.
    /// </summary>
    public void AddDna(EntityUid uid, string? dna)
    {
        if (dna == null)
            return;

        var comp = EnsureComp<SurgeryCrossContaminationComponent>(uid);
        comp.DNAs.Add(dna);
        Dirty(uid, comp);
    }

    #endregion
}
