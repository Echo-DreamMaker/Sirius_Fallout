using System;
using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server._Misfits.Holotape;
using Content.Server._Misfits.WastelandMap;
using Content.Shared.Access.Components;
using Content.Shared.DeltaV.NanoChat;
using Content.Shared.Damage;
using Content.Shared._Misfits.Holotape;
using Content.Shared._Misfits.WastelandMap;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.PDA;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Tag;
using Content.Shared.UserInterface;
using Content.Shared._Misfits.Overwatch;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Misfits.Overwatch;

public sealed class OverwatchConsoleSystem : EntitySystem
{
    [Dependency] private readonly HolotapeSystem _holotape = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly ViewSubscriberSystem _viewSubscriber = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private static readonly ProtoId<OverwatchCategoryPrototype> GeneralCategoryId = "OverwatchGeneral";
    private static readonly ProtoId<OverwatchCategoryPrototype> UnassignedCategoryId = "OverwatchUnassigned";

    private const float UpdateInterval = 0.5f;
    private float _accumulator;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ExpandICChatRecipientsEvent>(OnExpandRecipients);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<OverwatchConsoleComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<OverwatchConsoleComponent, BoundUIOpenedEvent>(OnOpened);
        SubscribeLocalEvent<OverwatchConsoleComponent, BoundUIClosedEvent>(OnClosed);
        SubscribeLocalEvent<OverwatchConsoleComponent, OverwatchConsoleMessage>(OnMessage);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accumulator += frameTime;
        if (_accumulator < UpdateInterval)
            return;

        _accumulator = 0f;

        var query = EntityQueryEnumerator<OverwatchConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            var ent = (uid, comp);

            // Validate each operator's session independently so any number of
            // operators can watch through the same console at once.
            foreach (var actor in comp.WatchSessions.Keys.ToList())
            {
                if (!comp.WatchSessions.TryGetValue(actor, out var session))
                    continue;

                ValidateWatch(ent, actor, session);
            }

            if (TryGetFirstViewer(uid, out _))
                RefreshUi(ent);
        }
    }

    private void OnOpened(Entity<OverwatchConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (!IsOverwatchUi(args.UiKey))
            return;

        RefreshUi(ent);
    }

    private void OnClosed(Entity<OverwatchConsoleComponent> ent, ref BoundUIClosedEvent args)
    {
        if (!IsOverwatchUi(args.UiKey))
            return;

        StopWatching(ent, args.Actor);
        RefreshUi(ent);
    }

    private void OnShutdown(Entity<OverwatchConsoleComponent> ent, ref ComponentShutdown args)
    {
        foreach (var actor in ent.Comp.WatchSessions.Keys.ToList())
            StopWatching(ent, actor);
    }

    private void OnPlayerDetached(PlayerDetachedEvent args)
    {
        var query = EntityQueryEnumerator<OverwatchConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.WatchSessions.ContainsKey(args.Entity))
                StopWatching((uid, comp), args.Entity);
        }
    }

    private void OnMessage(Entity<OverwatchConsoleComponent> ent, ref OverwatchConsoleMessage args)
    {
        if (!IsOverwatchUi(args.UiKey))
            return;

        switch (args.Type)
        {
            case OverwatchConsoleMessageType.Watch:
                if (args.TargetNumber != null)
                    StartWatching(ent, args.Actor, args.TargetNumber.Value);
                break;
            case OverwatchConsoleMessageType.Unwatch:
                StopWatching(ent, args.Actor);
                break;
        }

        RefreshUi(ent);
    }

    private static bool IsOverwatchUi(Enum uiKey)
    {
        return HolotapeUiKey.Key.Equals(uiKey) ||
               WastelandMapUiKey.Key.Equals(uiKey);
    }

    private void OnExpandRecipients(ExpandICChatRecipientsEvent ev)
    {
        var sourceCoordinates = Transform(ev.Source).Coordinates;

        foreach (var session in _player.Sessions)
        {
            if (session.AttachedEntity is not { } watcher ||
                !TryComp<OverwatchWatchingComponent>(watcher, out var watching) ||
                watching.Watching is not { } watched ||
                Deleted(watched))
            {
                continue;
            }

            var watchedCoordinates = Transform(watched).Coordinates;
            if (!sourceCoordinates.TryDistance(EntityManager, watchedCoordinates, out var distance) ||
                distance > ev.VoiceRange)
            {
                continue;
            }

            ev.Recipients.TryAdd(session, new ChatSystem.ICChatRecipientData(distance, false, true));
        }
    }

    private void StartWatching(Entity<OverwatchConsoleComponent> ent, EntityUid actor, uint targetNumber)
    {
        if (!TryComp<ActorComponent>(actor, out var actorComp) ||
            !TryGetWatchTarget(ent.Comp, targetNumber, out var watchedEntity))
        {
            StopWatching(ent, actor);
            return;
        }

        // Tear down any previous session for this actor on this console.
        if (ent.Comp.WatchSessions.TryGetValue(actor, out var existing))
        {
            RemoveWatchViewSubscription(existing.Subscriber, existing.WatchedEntity);
            if (!Deleted(actor) && existing.WatchedEntity is { } oldWatched && !Deleted(oldWatched))
                RemoveTargetWatcher(oldWatched, actor);
        }

        var watching = EnsureComp<OverwatchWatchingComponent>(actor);
        watching.Watching = watchedEntity;
        watching.WatchedNumber = targetNumber;
        watching.WatchedName = MetaData(watchedEntity).EntityName;
        Dirty(actor, watching);

        AddWatchViewSubscription(actorComp.PlayerSession, watchedEntity);

        ent.Comp.WatchSessions[actor] = new OverwatchWatchSession
        {
            Subscriber = actorComp.PlayerSession,
            WatchedNumber = targetNumber,
            WatchedEntity = watchedEntity,
        };

        UpdateLastKnown(actor, watchedEntity);
        AddTargetWatcher(watchedEntity, actor);
    }

    private void StopWatching(Entity<OverwatchConsoleComponent> ent, EntityUid actor)
    {
        if (ent.Comp.WatchSessions.TryGetValue(actor, out var session))
        {
            RemoveWatchViewSubscription(session.Subscriber, session.WatchedEntity);
            if (!Deleted(actor) && session.WatchedEntity is { } watched && !Deleted(watched))
                RemoveTargetWatcher(watched, actor);
            ent.Comp.WatchSessions.Remove(actor);
        }

        if (!Deleted(actor) && TryComp<OverwatchWatchingComponent>(actor, out _))
            RemComp<OverwatchWatchingComponent>(actor);
    }

    private void ValidateWatch(Entity<OverwatchConsoleComponent> ent, EntityUid actor, OverwatchWatchSession session)
    {
        if (Deleted(actor) || session.Subscriber.AttachedEntity != actor)
        {
            StopWatching(ent, actor);
            return;
        }

        if (!TryGetWatchTarget(ent.Comp, session.WatchedNumber, out var watchedEntity))
        {
            if (!session.Suspended)
                SuspendWatching(actor, session);
            return;
        }

        // #Misfits Add - If the resolved player changed while we had a live watch,
        // the tracked card was picked up by someone else. Stop watching.
        if (!session.Suspended &&
            session.WatchedEntity != null &&
            session.WatchedEntity != watchedEntity)
        {
            StopWatching(ent, actor);
            return;
        }

        if (session.Suspended || session.WatchedEntity != watchedEntity)
            AddWatchViewSubscription(session.Subscriber, watchedEntity);

        session.WatchedEntity = watchedEntity;
        session.Suspended = false;
        UpdateLastKnown(actor, watchedEntity);

        if (!Deleted(actor) && TryComp<OverwatchWatchingComponent>(actor, out var watching))
        {
            if (watching.Watching != watchedEntity || watching.WatchedNumber != session.WatchedNumber)
            {
                watching.Watching = watchedEntity;
                watching.WatchedNumber = session.WatchedNumber;
                Dirty(actor, watching);
            }
        }
    }

    private void SuspendWatching(EntityUid actor, OverwatchWatchSession session)
    {
        RemoveWatchViewSubscription(session.Subscriber, session.WatchedEntity);
        session.Suspended = true;

        // Keep WatchedNumber + last-known telemetry so the client can show "FEED LOST".
        if (!Deleted(actor) && TryComp<OverwatchWatchingComponent>(actor, out var watching))
        {
            watching.Watching = null;
            Dirty(actor, watching);
        }
    }

    private void AddTargetWatcher(EntityUid watched, EntityUid watcher)
    {
        if (Deleted(watched) || Deleted(watcher))
            return;

        var comp = EnsureComp<OverwatchTargetComponent>(watched);
        var name = MetaData(watcher).EntityName;
        if (!comp.WatcherNames.Contains(name))
            comp.WatcherNames.Add(name);
        Dirty(watched, comp);

        _popup.PopupEntity(
            Loc.GetString("overwatch-target-watched", ("name", name)),
            watched, watched, PopupType.Medium);
    }

    private void RemoveTargetWatcher(EntityUid watched, EntityUid watcher)
    {
        if (Deleted(watched) || Deleted(watcher))
            return;

        if (!TryComp<OverwatchTargetComponent>(watched, out var comp))
            return;

        var name = MetaData(watcher).EntityName;
        if (comp.WatcherNames.Remove(name) && comp.WatcherNames.Count == 0)
            RemComp<OverwatchTargetComponent>(watched);
        else
            Dirty(watched, comp);

        _popup.PopupEntity(
            Loc.GetString("overwatch-target-unwatched", ("name", name)),
            watched, watched, PopupType.Medium);
    }

    private void RefreshUi(Entity<OverwatchConsoleComponent> ent)
    {
        if (!TryGetFirstViewer(ent.Owner, out var actor))
            return;

        if (HasComp<WastelandMapComponent>(ent.Owner))
            EntityManager.System<WastelandMapSystem>().RefreshUi(ent.Owner, actor);
        else
            _holotape.RefreshTerminalState(ent.Owner, actor);
    }

    public OverwatchConsoleState? BuildUiState(EntityUid uid, OverwatchConsoleComponent? comp = null)
    {
        if (!Resolve(uid, ref comp, false))
            return null;

        return new OverwatchConsoleState(
            Loc.GetString(comp.MonitorTitle),
            GetViewerNames(uid),
            GetPersonnelEntries(comp));
    }

    private List<string> GetViewerNames(EntityUid uid)
    {
        var names = new List<string>();
        if (!TryComp<UserInterfaceComponent>(uid, out var ui))
            return names;

        Enum key = HasComp<WastelandMapComponent>(uid) ? WastelandMapUiKey.Key : (Enum) HolotapeUiKey.Key;
        foreach (var actor in _uiSystem.GetActors((uid, ui), key))
        {
            if (!Deleted(actor))
                names.Add(MetaData(actor).EntityName);
        }

        return names;
    }

    private bool TryGetFirstViewer(EntityUid uid, out EntityUid actor)
    {
        actor = default;
        if (!TryComp<UserInterfaceComponent>(uid, out var ui))
            return false;

        Enum key = HasComp<WastelandMapComponent>(uid) ? WastelandMapUiKey.Key : (Enum) HolotapeUiKey.Key;
        foreach (var a in _uiSystem.GetActors((uid, ui), key))
        {
            actor = a;
            return true;
        }

        return false;
    }

    private List<OverwatchConsoleEntry> GetPersonnelEntries(OverwatchConsoleComponent comp)
    {
        var entries = new List<OverwatchConsoleEntry>();
        var query = EntityQueryEnumerator<NanoChatCardComponent, IdCardComponent>();

        while (query.MoveNext(out var uid, out var nanoChat, out var idCard))
        {
            if (nanoChat.Number == null ||
                !MatchesTrackedPersonnel(comp, uid))
            {
                continue;
            }

            if (!TryResolvePersonnelTarget(nanoChat.PdaUid ?? uid, out var personnelEntity))
                continue;

            var position = Transform(personnelEntity).WorldPosition;
            var (health, state) = GetPersonnelHealth(personnelEntity);
            var category = ResolveCategory(idCard);
            entries.Add(new OverwatchConsoleEntry(
                nanoChat.Number.Value,
                idCard.FullName ?? "Unknown",
                idCard.LocalizedJobTitle,
                category.Name,
                category.SortOrder,
                health,
                state,
                position.X,
                position.Y));
        }

        entries.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));
        return entries;
    }

    private bool TryGetWatchTarget(OverwatchConsoleComponent comp, uint targetNumber, out EntityUid target)
    {
        target = default;

        var query = EntityQueryEnumerator<NanoChatCardComponent>();
        while (query.MoveNext(out var uid, out var nanoChat))
        {
            if (nanoChat.Number != targetNumber ||
                !MatchesTrackedPersonnel(comp, uid))
            {
                continue;
            }

            return TryResolvePersonnelTarget(nanoChat.PdaUid ?? uid, out target);
        }

        return false;
    }

    private bool MatchesTrackedPersonnel(OverwatchConsoleComponent comp, EntityUid trackedEntity)
    {
        if (comp.TrackedTags.Count == 0)
            return false;

        foreach (var tag in comp.TrackedTags)
        {
            if (_tag.HasTag(trackedEntity, tag))
                return true;
        }

        return false;
    }

    private (string Name, int SortOrder) ResolveCategory(IdCardComponent idCard)
    {
        if (idCard.JobPrototype != null &&
            _prototype.TryIndex(idCard.JobPrototype.Value, out JobPrototype? job) &&
            job.OverwatchCategory != null &&
            _prototype.TryIndex(job.OverwatchCategory.Value, out OverwatchCategoryPrototype? category))
        {
            return (category.LocalizedName, category.SortOrder);
        }

        var fallbackId = idCard.JobPrototype != null ? GeneralCategoryId : UnassignedCategoryId;
        if (_prototype.TryIndex(fallbackId, out OverwatchCategoryPrototype? fallback))
            return (fallback.LocalizedName, fallback.SortOrder);

        return (idCard.JobPrototype != null ? "GENERAL" : "UNASSIGNED", int.MaxValue);
    }

    private (float Health, MobState State) GetPersonnelHealth(EntityUid target)
    {
        var state = TryComp<MobStateComponent>(target, out var mobState)
            ? mobState.CurrentState
            : MobState.Alive;

        if (state == MobState.Dead)
            return (0f, state);

        if (!TryComp<DamageableComponent>(target, out var damageable))
            return (1f, state);

        if (!_mobThreshold.TryGetDeadThreshold(target, out var deadThreshold) &&
            !_mobThreshold.TryGetIncapThreshold(target, out deadThreshold))
        {
            return (1f, state);
        }

        var threshold = deadThreshold.Value.Float();
        if (threshold <= 0f)
            return (1f, state);

        var health = Math.Clamp(1f - damageable.TotalDamage.Float() / threshold, 0f, 1f);
        return (health, state);
    }

    private bool TryResolvePersonnelTarget(EntityUid sourceUid, out EntityUid target)
    {
        target = sourceUid;
        var current = sourceUid;

        while (_container.TryGetContainingContainer((current, null, null), out var container))
        {
            current = container.Owner;
            target = current;
        }

        if (target == sourceUid || Deleted(target))
            return false;

        return HasComp<MobStateComponent>(target) || HasComp<ActorComponent>(target);
    }

    private void AddWatchViewSubscription(ICommonSession subscriber, EntityUid? watched)
    {
        if (watched == null || Deleted(watched.Value))
            return;

        _viewSubscriber.AddViewSubscriber(watched.Value, subscriber);
    }

    private void RemoveWatchViewSubscription(ICommonSession subscriber, EntityUid? watched)
    {
        if (watched == null)
            return;

        _viewSubscriber.RemoveViewSubscriber(watched.Value, subscriber);
    }

    private void UpdateLastKnown(EntityUid actor, EntityUid watchedEntity)
    {
        if (Deleted(actor) || Deleted(watchedEntity) ||
            !TryComp<OverwatchWatchingComponent>(actor, out var watching))
        {
            return;
        }

        var position = Transform(watchedEntity).WorldPosition;
        var name = MetaData(watchedEntity).EntityName;
        watching.WatchedName = name;
        watching.LastKnownName = name;
        watching.LastKnownX = position.X;
        watching.LastKnownY = position.Y;
        watching.LastKnownTimestamp = _timing.CurTime.ToString(@"hh\:mm\:ss");
        Dirty(actor, watching);
    }
}
