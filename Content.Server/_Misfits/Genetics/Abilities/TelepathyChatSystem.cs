// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Chat.Managers;
using Content.Shared.Chat;
using Content.Shared._Misfits.Genetics.Abilities;
using Robust.Server.Player;
using Robust.Shared.Utility;

namespace Content.Server._Misfits.Genetics.Abilities;

/// <summary>
/// Puts telepathic messages into the target's chat on the Telepathic channel. This is the
/// server half of <see cref="TelepathyActionSystem"/>, split out because the chat manager
/// isn't available to shared code. Chat is used rather than a popup so the message renders
/// markup and stays in the log instead of fading after a couple of seconds.
/// </summary>
public sealed class TelepathyChatSystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TelepathyDeliverEvent>(OnDeliver);
    }

    private void OnDeliver(ref TelepathyDeliverEvent args)
    {
        if (!_player.TryGetSessionByEntity(args.Target, out var session))
            return;

        var wrapped = Loc.GetString("MutationTelepathy-message-wrap",
            ("message", FormattedMessage.EscapeText(args.Message)));

        _chatManager.ChatMessageToOne(ChatChannel.Telepathic,
            args.Message,
            wrapped,
            args.User,
            hideChat: false,
            session.Channel,
            Color.PaleVioletRed);
    }
}
