using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.GameTicking;
using Content.Shared._Misfits.Enclave;
using Content.Shared.IdentityManagement;
using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Misfits.Enclave;

/// <summary>
/// Implants Enclave personnel, handles the local-speech failsafe, and services
/// the Enclave remote detonator UI.
/// </summary>
public sealed class EnclaveMicroBombSystem : EntitySystem
{
    private const string EnclaveDepartmentId = "Enclave";
    private const string ImplantPrototypeId = "EnclaveMicroBombImplant";

    private static readonly HashSet<string> ImplantExemptJobs =
    [
        "EnclaveCommander",
        "EnclaveReformer",
    ];

    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly SharedSubdermalImplantSystem _implants = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly TriggerSystem _trigger = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<ImplantedComponent, EntitySpokeEvent>(OnEntitySpoke);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<EnclaveMicroBombComponent, ActivateImplantEvent>(OnImplantActivated);

        Subs.BuiEvents<EnclaveDetonatorComponent>(EnclaveDetonatorUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnDetonatorOpened);
            subs.Event<EnclaveDetonatorRefreshMessage>(OnDetonatorRefresh);
            subs.Event<EnclaveDetonatorActivateMessage>(OnDetonatorActivate);
        });
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        if (args.JobId == null ||
            ImplantExemptJobs.Contains(args.JobId) ||
            !_prototypes.TryIndex<DepartmentPrototype>(EnclaveDepartmentId, out var department))
        {
            return;
        }

        if (!department.Roles.Any(role => role.Id == args.JobId))
            return;

        Implant(args.Mob);
    }

    private void OnEntitySpoke(EntityUid uid, ImplantedComponent component, EntitySpokeEvent args)
    {
        // EntitySpokeEvent is also raised for whispers and radio speech. The
        // failsafe is deliberately limited to ordinary local IC speech.
        if (args.IsWhisper || args.Channel != null)
            return;

        if (!TryGetImplant(uid, out var implant, out var enclaveBomb) ||
            !ContainsWord(args.Message, enclaveBomb.Keyword))
        {
            return;
        }

        BeginCountdown(implant, enclaveBomb, uid);
        UpdateAllDetonators();
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead && TryGetImplant(args.Target, out _, out _))
            UpdateAllDetonators();
    }

    private void OnImplantActivated(EntityUid uid, EnclaveMicroBombComponent component, ActivateImplantEvent args)
    {
        BeginCountdown(uid, component, args.Performer);
        args.Handled = true;
    }

    private void OnDetonatorOpened(Entity<EnclaveDetonatorComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateDetonator(ent.Owner);
    }

    private void OnDetonatorRefresh(Entity<EnclaveDetonatorComponent> ent, ref EnclaveDetonatorRefreshMessage args)
    {
        UpdateDetonator(ent.Owner);
    }

    private void OnDetonatorActivate(Entity<EnclaveDetonatorComponent> ent, ref EnclaveDetonatorActivateMessage args)
    {
        if (!TryGetEntity(args.Target, out var target) ||
            !TryGetImplant(target.Value, out var implant, out _))
        {
            UpdateDetonator(ent.Owner);
            return;
        }

        if (TryComp<EnclaveMicroBombComponent>(implant, out var component))
            BeginCountdown(implant, component, args.Actor);

        UpdateAllDetonators();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<EnclaveMicroBombComponent, SubdermalImplantComponent>();
        while (query.MoveNext(out var uid, out var enclaveBomb, out var subdermal))
        {
            if (!enclaveBomb.CountdownActive)
                continue;

            if (subdermal.ImplantedEntity is not { } body || TerminatingOrDeleted(body))
            {
                enclaveBomb.CountdownActive = false;
                continue;
            }

            if (enclaveBomb.NextNumber > 0 && now >= enclaveBomb.NextAnnouncement)
            {
                _popup.PopupEntity(enclaveBomb.NextNumber.ToString(), body, PopupType.LargeCaution);
                _audio.PlayPvs(enclaveBomb.BeepSound, body);
                enclaveBomb.NextNumber--;
                enclaveBomb.NextAnnouncement += TimeSpan.FromSeconds(1);
            }

            if (now < enclaveBomb.DetonationTime)
                continue;

            enclaveBomb.CountdownActive = false;
            _trigger.Trigger(uid, enclaveBomb.Initiator);
        }
    }

    private void BeginCountdown(EntityUid implant, EnclaveMicroBombComponent component, EntityUid? initiator)
    {
        if (component.CountdownActive ||
            !TryComp<SubdermalImplantComponent>(implant, out var subdermal) ||
            subdermal.ImplantedEntity is not { } body ||
            TerminatingOrDeleted(body))
        {
            return;
        }

        component.CountdownActive = true;
        component.NextNumber = 5;
        component.Initiator = initiator;

        // Give the breach warning a brief moment on screen before beginning a
        // true five-second numeric countdown.
        component.NextAnnouncement = _timing.CurTime + TimeSpan.FromSeconds(0.25);
        component.DetonationTime = component.NextAnnouncement + TimeSpan.FromSeconds(5);

        _popup.PopupEntity(
            Loc.GetString("enclave-microbomb-breach-warning"),
            body,
            PopupType.LargeCaution);
    }

    /// <summary>
    /// Adds a networked Enclave microbomb unless the target already has one.
    /// Used both for spawned Enclave jobs and accepted recruits.
    /// </summary>
    public void Implant(EntityUid target)
    {
        if (TryGetImplant(target, out _, out _))
            return;

        _implants.AddImplant(target, ImplantPrototypeId);
        UpdateAllDetonators();
    }

    private bool TryGetImplant(
        EntityUid target,
        out EntityUid implant,
        out EnclaveMicroBombComponent component)
    {
        if (TerminatingOrDeleted(target))
        {
            implant = default;
            component = default!;
            return false;
        }

        var query = EntityQueryEnumerator<EnclaveMicroBombComponent, SubdermalImplantComponent>();
        while (query.MoveNext(out var uid, out var enclaveBomb, out var subdermal))
        {
            if (subdermal.ImplantedEntity != target)
                continue;

            implant = uid;
            component = enclaveBomb;
            return true;
        }

        implant = default;
        component = default!;
        return false;
    }

    private EnclaveDetonatorBoundUserInterfaceState BuildState()
    {
        var personnel = new List<EnclaveMicroBombEntry>();
        var listed = new HashSet<EntityUid>();
        var query = EntityQueryEnumerator<EnclaveMicroBombComponent, SubdermalImplantComponent>();

        while (query.MoveNext(out _, out _, out var subdermal))
        {
            if (subdermal.ImplantedEntity is not { } body ||
                TerminatingOrDeleted(body) ||
                !listed.Add(body) ||
                TryComp<MobStateComponent>(body, out var mobState) && mobState.CurrentState == MobState.Dead)
            {
                continue;
            }

            personnel.Add(new EnclaveMicroBombEntry(
                GetNetEntity(body),
                Identity.Name(body, EntityManager),
                GetEnclaveRole(body)));
        }

        return new EnclaveDetonatorBoundUserInterfaceState(personnel
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray());
    }

    private string GetEnclaveRole(EntityUid body)
    {
        if (!TryComp<MindContainerComponent>(body, out var mindContainer) || !mindContainer.HasMind)
            return Loc.GetString("enclave-detonator-role-unknown");

        var mind = mindContainer.Mind.Value;
        if (HasComp<EnclaveRecruitMindComponent>(mind))
            return Loc.GetString("job-name-enclave-recruit");

        if (_jobs.MindTryGetJob(mind, out _, out var jobPrototype))
            return Loc.GetString(jobPrototype.Name);

        return Loc.GetString("enclave-detonator-role-unknown");
    }

    private void UpdateDetonator(EntityUid uid)
    {
        _ui.SetUiState(uid, EnclaveDetonatorUiKey.Key, BuildState());
    }

    private void UpdateAllDetonators()
    {
        var query = EntityQueryEnumerator<EnclaveDetonatorComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            UpdateDetonator(uid);
        }
    }

    private static bool ContainsWord(string message, string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return false;

        var index = 0;
        while ((index = message.IndexOf(keyword, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            var beforeIsWord = index > 0 && char.IsLetterOrDigit(message[index - 1]);
            var afterIndex = index + keyword.Length;
            var afterIsWord = afterIndex < message.Length && char.IsLetterOrDigit(message[afterIndex]);

            if (!beforeIsWord && !afterIsWord)
                return true;

            index = afterIndex;
        }

        return false;
    }
}
