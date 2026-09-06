using Content.Server.Actions;
using Content.Server.Chat.Systems;
using Content.Shared._Misfits.Deathclaw;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Player;

namespace Content.Server._Misfits.Deathclaw;

public sealed class BwonsamdiDreadRoarSystem : EntitySystem
{
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;

    private readonly HashSet<EntityUid> _targets = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BwonsamdiComponent, DreadRoarActionEvent>(OnDreadRoar);
    }

    private void OnDreadRoar(Entity<BwonsamdiComponent> ent, ref DreadRoarActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        _actions.StartUseDelay(args.Action);
        _chat.TryEmoteWithChat(ent.Owner, "MisfitsDeathclawRoar");

        _targets.Clear();
        _lookup.GetEntitiesInRange(Transform(ent).Coordinates, 6f, _targets);
        foreach (var target in _targets)
        {
            if (target == ent.Owner
                || !TryComp<ActorComponent>(target, out _)
                || !TryComp<MobStateComponent>(target, out var mob)
                || mob.CurrentState != MobState.Alive)
            {
                continue;
            }

            // refresh:false prevents repeated roars from extending an existing stun loop.
            if (_stun.TryStun(target, TimeSpan.FromSeconds(1.5), false))
            {
                _popup.PopupEntity(
                    Loc.GetString("bwonsamdi-dread-roar-target"),
                    target,
                    target,
                    PopupType.MediumCaution);
            }
        }
    }
}
