// #Misfits Add вЂ” Ug-Qualtoth tribal worship and gradual transformation system.
//
// Mechanic overview:
//   вЂў When a tribal player walks within ProximityRange of the idol they receive a private ambient
//     message ("You feel an ominous presence...") вЂ” once per ProximityCooldown.
//   вЂў Right-clicking the idol within that same range reveals "Pray to Ug-Qualtoth".
//   вЂў Praying starts a 20-second DoAfter ritual. Three private flavor messages are delivered at
//     ~5 s / ~10 s / ~15 s so only the praying player sees the idol's "voice".
//   вЂў Moving or taking damage cancels the ritual.
//   вЂў Each completed prayer + kills near the idol build devotion, advancing stages 0в†’4:
//       Stage 1 вЂ” left arm transformed; +10% speed, +5 slash
//       Stage 2 вЂ” arm + both legs; +20% speed, more damage, minor resistance
//       Stage 3 вЂ” both arms + legs + chest (full body); +30% speed, heavy resistance
//       Stage 4 вЂ” full body polymorph into UgQualtothAbomination

using Content.Server.Chat.Systems;
using Content.Server.Chat.Managers;
using Content.Server.DoAfter;
using Content.Server.Humanoid;
using Content.Shared._Misfits.UgQualtoth;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Melee;
using Robust.Server.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Content.Server.Polymorph.Systems;
using Content.Shared.Polymorph;

namespace Content.Server._Misfits.UgQualtoth;

/// <summary>
/// Drives the Ug-Qualtoth proximity detection, worship ritual, and body-horror transformation.
/// </summary>
public sealed class UgQualtothSystem : EntitySystem
{
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private DoAfterSystem _doAfter = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private HumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private PolymorphSystem _polymorph = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private MovementSpeedModifierSystem _speedMod = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private NpcFactionSystem _faction = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    // Marking prototype IDs вЂ” one overlay per stage, each replaces the previous.
    private const string MarkingStage1 = "UgQualtothStage1";
    private const string MarkingStage2 = "UgQualtothStage2";
    private const string MarkingStage3 = "UgQualtothStage3";

    // Damage modifier set IDs.
    private const string Stage2DamageModifier = "UgQualtothStage2";
    private const string Stage3DamageModifier = "UgQualtothStage3";
    private const string AbominationDamageModifier = "UgQualtothAbomination";

    // DoAfter ritual duration in seconds.
    private const float PrayDoAfterTime = 20f;

    // How many timed flavor texts to send during a prayer and at what intervals.
    private static readonly TimeSpan[] FlavourTextTimings =
    {
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(15),
    };

    // Update throttle вЂ” proximity + flavour checks run once per second.
    private float _updateAccum;
    private const float UpdateInterval = 1f;

    // Reusable buffer for proximity lookups (avoids per-frame allocation).
    private readonly HashSet<Entity<TransformComponent>> _proximityBuffer = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UgQualtothArtifactComponent, GetVerbsEvent<ActivationVerb>>(OnGetPrayVerb);
        SubscribeLocalEvent<UgQualtothArtifactComponent, UgQualtothPrayDoAfterEvent>(OnPrayDoAfter);

        SubscribeLocalEvent<UgQualtothWorshipperComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<UgQualtothWorshipperComponent, IsEquippingAttemptEvent>(OnEquipAttempt);

        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateDeath);
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    //  Update: proximity ambient messages + in-prayer flavor text
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _updateAccum += frameTime;
        if (_updateAccum < UpdateInterval)
            return;
        _updateAccum = 0f;

        var curTime = _timing.CurTime;

        // в”Ђв”Ђ Proximity: add component + ambient message в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        // Any tribal who walks within ProximityRange of the idol is drawn into the
        // system (component added). Stage 0 has no mechanical effects вЂ” the component
        // just means they can accumulate devotion from blood sacrifices and receive
        // the eerie flavor messages. Actual transformation only starts once devotion
        // thresholds are crossed through prayer or sacrifice.
        var idolQuery = EntityQueryEnumerator<UgQualtothArtifactComponent, TransformComponent>();
        while (idolQuery.MoveNext(out _, out var idolComp, out var idolXform))
        {
            var idolCoords = _transform.GetMapCoordinates(idolXform);

            _proximityBuffer.Clear();
            _lookup.GetEntitiesInRange<TransformComponent>(idolCoords, idolComp.ProximityRange, _proximityBuffer);

            foreach (var nearby in _proximityBuffer)
            {
                if (!IsTribalHumanoid(nearby.Owner))
                    continue;

                var worshipperComp = EnsureComp<UgQualtothWorshipperComponent>(nearby.Owner);

                if (!worshipperComp.NextProximityMessageAt.HasValue ||
                    curTime >= worshipperComp.NextProximityMessageAt.Value)
                {
                    SendProximityMessage(nearby.Owner, worshipperComp, curTime);
                }
            }
        }

        // в”Ђв”Ђ In-prayer flavor text в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        var worshipperQuery = EntityQueryEnumerator<UgQualtothWorshipperComponent>();
        while (worshipperQuery.MoveNext(out var worshipperUid, out var worshipper))
        {
            if (!worshipper.PrayingStartedAt.HasValue ||
                worshipper.PrayFlavoursSent >= FlavourTextTimings.Length)
                continue;

            var elapsed = curTime - worshipper.PrayingStartedAt.Value;
            var nextIndex = worshipper.PrayFlavoursSent;

            if (elapsed >= FlavourTextTimings[nextIndex])
            {
                _popup.PopupEntity(
                    Loc.GetString($"ug-qualtoth-pray-flavour-{nextIndex + 1}"),
                    worshipperUid, worshipperUid, PopupType.MediumCaution);
                worshipper.PrayFlavoursSent++;
            }
        }
    }

    /// <summary>Returns true if the entity is a Tribal faction member with a humanoid body.</summary>
    private bool IsTribalHumanoid(EntityUid uid)
    {
        return _faction.IsMember(uid, "Tribal")
               && HasComp<HumanoidAppearanceComponent>(uid);
    }

    private void SendProximityMessage(EntityUid uid, UgQualtothWorshipperComponent comp, TimeSpan curTime)
    {
        // Pick one of the three proximity strings based on how many times the player has been near.
        // Cycles through 1в†’2в†’3в†’1... so each visit feels slightly different.
        var variant = (int) (curTime.TotalMinutes % 3) + 1;
        _popup.PopupEntity(
            Loc.GetString($"ug-qualtoth-proximity-{variant}"),
            uid, uid, PopupType.SmallCaution);

        comp.NextProximityMessageAt = curTime + comp.ProximityCooldown;
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    //  Verb: right-click "Pray to Ug-Qualtoth" on the idol
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    private void OnGetPrayVerb(Entity<UgQualtothArtifactComponent> idol, ref GetVerbsEvent<ActivationVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;

        if (!IsTribalHumanoid(user))
            return;

        args.Verbs.Add(new ActivationVerb
        {
            Text = Loc.GetString("ug-qualtoth-verb-pray"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/star-regular.svg.192dpi.png")),
            Act = () => StartPraying(idol, user),
        });
    }

    private void StartPraying(Entity<UgQualtothArtifactComponent> idol, EntityUid user)
    {
        var worshipper = EnsureComp<UgQualtothWorshipperComponent>(user);

        // Cooldown check.
        if (worshipper.NextPrayAllowedAt.HasValue && _timing.CurTime < worshipper.NextPrayAllowedAt.Value)
        {
            var remaining = (worshipper.NextPrayAllowedAt.Value - _timing.CurTime).TotalMinutes;
            _popup.PopupEntity(
                Loc.GetString("ug-qualtoth-pray-cooldown", ("minutes", (int) Math.Ceiling(remaining))),
                idol, user, PopupType.SmallCaution);
            return;
        }

        if (worshipper.Stage >= 4)
        {
            _popup.PopupEntity(Loc.GetString("ug-qualtoth-pray-already-ascended"), idol, user, PopupType.Small);
            return;
        }

        // Record prayer start for flavor text delivery in Update().
        worshipper.PrayingStartedAt = _timing.CurTime;
        worshipper.PrayFlavoursSent = 0;

        _popup.PopupEntity(Loc.GetString("ug-qualtoth-pray-begin"), idol, user, PopupType.Medium);

        _doAfter.TryStartDoAfter(new DoAfterArgs(
            EntityManager,
            user,
            PrayDoAfterTime,
            new UgQualtothPrayDoAfterEvent(),
            idol,
            target: idol,
            used: idol
        )
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
        });
    }

    /// <summary>
    /// Finds all other tribal players in proximity of the idol, ensures they have the
    /// worshipper component (draws them into the system), and sends each a private
    /// notification that a ritual has begun nearby.
    /// </summary>
    private void DrawInNearbyTribals(Entity<UgQualtothArtifactComponent> idol, EntityUid prayingPlayer)
    {
        var idolCoords = _transform.GetMapCoordinates(idol);

        _proximityBuffer.Clear();
        _lookup.GetEntitiesInRange<TransformComponent>(idolCoords, idol.Comp.ProximityRange, _proximityBuffer);

        foreach (var nearby in _proximityBuffer)
        {
            if (nearby.Owner == prayingPlayer)
                continue;

            if (!IsTribalHumanoid(nearby.Owner))
                continue;

            // Add component if not already present вЂ” this is the moment they're "drawn in".
            EnsureComp<UgQualtothWorshipperComponent>(nearby.Owner);

            _popup.PopupEntity(
                Loc.GetString("ug-qualtoth-ritual-begun-nearby"),
                nearby.Owner, nearby.Owner, PopupType.MediumCaution);
        }
    }

    private void OnPrayDoAfter(Entity<UgQualtothArtifactComponent> idol, ref UgQualtothPrayDoAfterEvent args)
    {
        var user = args.User;

        if (!TryComp<UgQualtothWorshipperComponent>(user, out var worshipper))
            return;

        // Always clear the in-prayer state regardless of outcome.
        worshipper.PrayingStartedAt = null;
        worshipper.PrayFlavoursSent = 0;

        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        worshipper.NextPrayAllowedAt = _timing.CurTime + worshipper.PrayCooldown;

        AddDevotion(user, worshipper, worshipper.PrayDevotionGain, idol);

        if (idol.Comp.PraySound != null)
            _audio.PlayPvs(idol.Comp.PraySound, idol);

        _popup.PopupEntity(
            Loc.GetString("ug-qualtoth-pray-success", ("devotion", (int) worshipper.Devotion)),
            user, user, PopupType.MediumCaution);

        _chat.TrySendInGameICMessage(user,
            Loc.GetString("ug-qualtoth-pray-emote"),
            InGameICChatType.Emote, ChatTransmitRange.Normal, ignoreActionBlocker: true);

        // Draw in anyone still within range when the ritual completes.
        // Anyone who left before the 20 seconds was up is excluded automatically
        // because GetEntitiesInRange only sees current positions.
        DrawInNearbyTribals(idol, user);
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    //  Blood sacrifice
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    private void OnMobStateDeath(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        var victim = args.Target;

        // Ignore entities that are already mid-deletion вЂ” their transform is unreliable.
        if (TerminatingOrDeleted(victim))
            return;

        var victimPos = _transform.GetWorldPosition(victim);

        var idolQuery = EntityQueryEnumerator<UgQualtothArtifactComponent, TransformComponent>();
        while (idolQuery.MoveNext(out var idolUid, out var idolComp, out var idolXform))
        {
            if (TerminatingOrDeleted(idolUid))
                continue;

            var idolPos = _transform.GetWorldPosition(idolXform);
            if ((victimPos - idolPos).Length() > idolComp.SacrificeRange)
                continue;

            var devotionGain = HasComp<HumanoidAppearanceComponent>(victim)
                ? idolComp.HumanoidSacrificeDevotionGain
                : idolComp.AnimalSacrificeDevotionGain;

            var worshipperQuery = EntityQueryEnumerator<UgQualtothWorshipperComponent, TransformComponent>();
            while (worshipperQuery.MoveNext(out var worshipperUid, out var worshipperComp, out var worshipperXform))
            {
                if (TerminatingOrDeleted(worshipperUid))
                    continue;

                if (worshipperXform.MapID != idolXform.MapID)
                    continue;

                // Don't award devotion for the worshipper's own death.
                if (worshipperUid == victim)
                    continue;

                AddDevotion(worshipperUid, worshipperComp, devotionGain,
                    new Entity<UgQualtothArtifactComponent>(idolUid, idolComp));

                _popup.PopupEntity(
                    Loc.GetString("ug-qualtoth-sacrifice-reward", ("devotion", (int) worshipperComp.Devotion)),
                    worshipperUid, worshipperUid, PopupType.Medium);
            }

            break;
        }
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    //  Devotion + stage transitions
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    private void AddDevotion(EntityUid uid, UgQualtothWorshipperComponent comp, float amount,
        Entity<UgQualtothArtifactComponent> idol)
    {
        comp.Devotion += amount;
        CheckStageAdvance(uid, comp, idol);
    }

    private void CheckStageAdvance(EntityUid uid, UgQualtothWorshipperComponent comp,
        Entity<UgQualtothArtifactComponent> idol)
    {
        while (comp.Stage < 4)
        {
            var threshold = comp.Stage switch
            {
                0 => comp.Stage1Threshold,
                1 => comp.Stage2Threshold,
                2 => comp.Stage3Threshold,
                3 => comp.Stage4Threshold,
                _ => float.MaxValue,
            };

            if (comp.Devotion < threshold)
                break;

            comp.Stage++;
            ApplyStageEffects(uid, comp, idol);
        }
    }

    private void ApplyStageEffects(EntityUid uid, UgQualtothWorshipperComponent comp,
        Entity<UgQualtothArtifactComponent> idol)
    {
        // Apply stat/visual changes first вЂ” audio is non-critical and must not block the transformation.
        switch (comp.Stage)
        {
            case 1: ApplyStage1(uid); break;
            case 2: ApplyStage2(uid); break;
            case 3: ApplyStage3(uid); break;
            case 4: ApplyStage4(uid, comp); break;
        }

        _speedMod.RefreshMovementSpeedModifiers(uid);

        _chat.TrySendInGameICMessage(uid,
            Loc.GetString($"ug-qualtoth-stage{comp.Stage}-emote"),
            InGameICChatType.Emote, ChatTransmitRange.Normal, ignoreActionBlocker: true);

        _popup.PopupEntity(Loc.GetString($"ug-qualtoth-stage{comp.Stage}-private"), uid, uid, PopupType.LargeCaution);

        if (idol.Comp.TransformSound != null)
            _audio.PlayPvs(idol.Comp.TransformSound, idol);
    }

    // в”Ђв”Ђ Stage 1 в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    private void ApplyStage1(EntityUid uid)
    {
        ApplyMarking(uid, MarkingStage1);

        if (TryComp<MeleeWeaponComponent>(uid, out var melee))
        {
            melee.Damage.DamageDict.TryAdd("Slash", 0);
            melee.Damage.DamageDict["Slash"] += 5;
            Dirty(uid, melee);
        }
    }

    private static readonly HumanoidVisualLayers[] Stage2HiddenLayers =
    {
        HumanoidVisualLayers.RLeg,
        HumanoidVisualLayers.LLeg,
        HumanoidVisualLayers.RFoot,
        HumanoidVisualLayers.LFoot,
    };

    // в”Ђв”Ђ Stage 2 в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    private void ApplyStage2(EntityUid uid)
    {
        _humanoid.RemoveMarking(uid, MarkingStage1);
        ApplyMarking(uid, MarkingStage2);
        _humanoid.SetLayersVisibility(uid, Stage2HiddenLayers, visible: false, permanent: true);
        _inventory.TryUnequip(uid, uid, "gloves", out _, silent: false, force: true);
        _inventory.TryUnequip(uid, uid, "shoes", out _, silent: false, force: true);

        if (TryComp<MeleeWeaponComponent>(uid, out var melee))
        {
            melee.Damage.DamageDict.TryAdd("Slash", 0);
            melee.Damage.DamageDict["Slash"] += 8;
            melee.Damage.DamageDict.TryAdd("Blunt", 0);
            melee.Damage.DamageDict["Blunt"] += 5;
            Dirty(uid, melee);
        }

        if (_proto.HasIndex<DamageModifierSetPrototype>(Stage2DamageModifier))
            _damageable.SetDamageModifierSetId(uid, Stage2DamageModifier);
    }

    // в”Ђв”Ђ Stage 3 в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    private static readonly HumanoidVisualLayers[] Stage3HiddenLayers =
    {
        HumanoidVisualLayers.RArm,
        HumanoidVisualLayers.LArm,
        HumanoidVisualLayers.RHand,
        HumanoidVisualLayers.LHand,
        HumanoidVisualLayers.RLeg,
        HumanoidVisualLayers.LLeg,
        HumanoidVisualLayers.RFoot,
        HumanoidVisualLayers.LFoot,
    };

    private void ApplyStage3(EntityUid uid)
    {
        _humanoid.RemoveMarking(uid, MarkingStage2);
        ApplyMarking(uid, MarkingStage3);
        _humanoid.SetLayersVisibility(uid, Stage3HiddenLayers, visible: false, permanent: true);

        if (TryComp<MeleeWeaponComponent>(uid, out var melee))
        {
            melee.Damage.DamageDict.TryAdd("Slash", 0);
            melee.Damage.DamageDict["Slash"] += 12;
            melee.Damage.DamageDict.TryAdd("Blunt", 0);
            melee.Damage.DamageDict["Blunt"] += 8;
            Dirty(uid, melee);
        }

        if (_proto.HasIndex<DamageModifierSetPrototype>(Stage3DamageModifier))
            _damageable.SetDamageModifierSetId(uid, Stage3DamageModifier);
    }

    // в”Ђв”Ђ Stage 4 в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    private void ApplyStage4(EntityUid uid, UgQualtothWorshipperComponent comp)
    {
        _humanoid.RemoveMarking(uid, MarkingStage3);
        _humanoid.SetLayersVisibility(uid, Stage3HiddenLayers, visible: true, permanent: true);

        // Attempt to polymorph the entity into the dedicated NPC abomination body.
        var child = _polymorph.PolymorphEntity(uid, new ProtoId<PolymorphPrototype>("UgQualtothAbomination"));

        if (child != null)
        {
            // Apply final melee buff to the new body.
            if (TryComp<MeleeWeaponComponent>(child.Value, out var meleeChild))
            {
                meleeChild.Damage.DamageDict.TryAdd("Slash", 0);
                meleeChild.Damage.DamageDict["Slash"] += 15;
                meleeChild.Damage.DamageDict.TryAdd("Blunt", 0);
                meleeChild.Damage.DamageDict["Blunt"] += 10;
                Dirty(child.Value, meleeChild);
            }

            // Apply abomination damage resistance to the new body.
            if (_proto.HasIndex<DamageModifierSetPrototype>(AbominationDamageModifier))
                _damageable.SetDamageModifierSetId(child.Value, AbominationDamageModifier);
        }
        else
        {
            // Fallback: if polymorph failed for any reason, use the species-based approach.
            _humanoid.SetSpecies(uid, comp.AbominationSpecies);

            // Deep crimson skin to complete the transformation.
            _humanoid.SetSkinColor(uid, new Color(0.18f, 0.05f, 0.05f), verify: false);

            // Final melee buff applied to existing body as a fallback.
            if (TryComp<MeleeWeaponComponent>(uid, out var melee))
            {
                melee.Damage.DamageDict.TryAdd("Slash", 0);
                melee.Damage.DamageDict["Slash"] += 15;
                melee.Damage.DamageDict.TryAdd("Blunt", 0);
                melee.Damage.DamageDict["Blunt"] += 10;
                Dirty(uid, melee);
            }

            if (_proto.HasIndex<DamageModifierSetPrototype>(AbominationDamageModifier))
                _damageable.SetDamageModifierSetId(uid, AbominationDamageModifier);
        }

        _chatManager.DispatchServerAnnouncement(Loc.GetString("ug-qualtoth-stage4-announcement"));
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    //  Marking helper
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    private void ApplyMarking(EntityUid uid, string markingId)
    {
        _humanoid.AddMarking(uid, markingId, forced: true);
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    //  Speed modifier
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    private void OnEquipAttempt(Entity<UgQualtothWorshipperComponent> ent,
        ref IsEquippingAttemptEvent args)
    {
        if (ent.Comp.Stage < 2)
            return;

        if (args.Slot == "gloves" || args.Slot == "shoes")
            args.Cancel();
    }

    private void OnRefreshSpeed(Entity<UgQualtothWorshipperComponent> ent,
        ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.Stage <= 0)
            return;

        var multiplier = 1f + ent.Comp.SpeedBonusPerStage * ent.Comp.Stage;
        args.ModifySpeed(multiplier, multiplier);
    }
}
