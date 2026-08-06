// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.CombatMode;
using Content.Shared.IdentityManagement;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Random;

namespace Content.Shared._Misfits.Genetics.Abilities;

public sealed partial class MindReadActionSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MobStateSystem _mob = default!;
    [Dependency] private SharedCombatModeSystem _combatMode = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MindReadActionComponent, MindReadActionEvent>(OnMindRead);
    }

    private void OnMindRead(Entity<MindReadActionComponent> ent, ref MindReadActionEvent args)
    {
        var user = args.Performer;
        var target = args.Target;

        args.Handled = true;

        // check if they are valid to begin with
        var identity = Identity.Name(target, EntityManager);
        if (!_mind.TryGetMind(target, out var mindId, out var mind))
        {
            _popup.PopupEntity(Loc.GetString("MutationMindReader-popup-target-mindless", ("target", identity)), user, user);
            return;
        }

        if (_mob.IsDead(target))
        {
            _popup.PopupEntity(Loc.GetString("MutationMindReader-popup-target-dead", ("target", identity)), user, user);
            return;
        }

        if (user == target)
        {
            _popup.PopupEntity(Loc.GetString("MutationMindReader-popup-self"), user, user);
            return;
        }

        _popup.PopupEntity(Loc.GetString("MutationMindReader-popup-plunge", ("target", identity)), user, user);

        // you don't know details about other players' minds.
        // also it's using chatcode anyway
        if (_net.IsClient) return;

        // chance to alert the target
        if (_random.Prob(ent.Comp.AlertProb))
            _popup.PopupEntity(Loc.GetString("MutationMindReader-popup-alert"), target, target, PopupType.MediumCaution);

        // doesn't matter much because of combat mode spinning but parity
        var combat = _combatMode.IsInCombatMode(target);
        Tell(user, Loc.GetString("MutationMindReader-popup-combat-mode", ("target", target), ("combat", combat)));

        // reveal mindswaps or whatever
        if (mind.CharacterName is {} name && name != identity)
            Tell(user, Loc.GetString("MutationMindReader-popup-true-identity", ("target", target), ("name", name)), Color.Red);

        // dredge up what they've been saying, each line only surfacing half the time
        if (TryComp<RecentSpeechComponent>(target, out var speech) && speech.Messages.Count > 0)
        {
            var heard = false;
            foreach (var message in speech.Messages)
            {
                if (!_random.Prob(0.5f))
                    continue;

                heard = true;
                // escape it: this is player-authored text going into popup rich text
                Tell(user, Loc.GetString("MutationMindReader-popup-thought", ("message", FormattedMessage.EscapeText(message))));
            }

            if (!heard)
                Tell(user, Loc.GetString("MutationMindReader-popup-no-thoughts", ("target", identity)));
        }
        else
        {
            Tell(user, Loc.GetString("MutationMindReader-popup-no-thoughts", ("target", identity)));
        }
    }

    private void Tell(EntityUid user, string message, Color? color = null)
    {
        _popup.PopupEntity(message, user, user);
    }
}
