// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Content.Shared._Misfits.Genetics.Console;

namespace Content.Server._Misfits.Genetics.Console;

public sealed class GeneticsConsoleSpeechSystem : EntitySystem
{
    [Dependency] private ChatSystem _chat = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GeneticsConsoleComponent, GeneticsConsoleSpeakEvent>(OnSpeak);
    }

    private void OnSpeak(Entity<GeneticsConsoleComponent> ent, ref GeneticsConsoleSpeakEvent args)
    {
        _chat.TrySendInGameICMessage(ent, args.Message, InGameICChatType.Speak, hideChat: false, hideLog: true);
    }
}
