// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Speech;
using Content.Shared._Misfits.Genetics.Mutations;

namespace Content.Server._Misfits.Genetics;

/// <summary>
/// Relays <see cref="AccentGetEvent"/> from a mutated mob to its mutation entities, so accent
/// mutations (Elvis, Swedish, Chav, Medieval, Pig Latin, Heckacious Larincks, etc.) that keep
/// their accent components on the mutation entity actually transform the mob's speech.
/// This can't live in the shared <see cref="MutationRelaySystem"/> because AccentGetEvent
/// is defined in Content.Server.
/// </summary>
public sealed class MutationAccentRelaySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MutatableComponent, AccentGetEvent>(OnAccentGet);
    }

    private void OnAccentGet(EntityUid uid, MutatableComponent component, AccentGetEvent args)
    {
        foreach (var mutation in component.Mutations.Values)
        {
            RaiseLocalEvent(mutation, args);
        }
    }
}
