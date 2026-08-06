// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Chat.Systems;
using Content.Shared.Mind.Components;
using Content.Shared._Misfits.Genetics.Abilities;

namespace Content.Server._Misfits.Genetics;

/// <summary>
/// Remembers the last few things minded entities said so Mind Reader can dig them out.
/// Lives on the server because EntitySpokeEvent is server-side.
/// </summary>
public sealed class RecentSpeechSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EntitySpokeEvent>(OnSpoke);
    }

    private void OnSpoke(EntitySpokeEvent args)
    {
        // only things with a mind have thoughts worth reading
        if (!HasComp<MindContainerComponent>(args.Source))
            return;

        // deliberate: whispers stay private. recording them verbatim would mean mind reading
        // hands over things the speaker specifically took care not to say out loud.
        if (args.IsWhisper)
            return;

        var message = args.Message.Trim();
        if (message.Length == 0)
            return;

        var comp = EnsureComp<RecentSpeechComponent>(args.Source);
        comp.Messages.Add(message);
        while (comp.Messages.Count > comp.MaxMessages)
        {
            comp.Messages.RemoveAt(0);
        }
    }
}
