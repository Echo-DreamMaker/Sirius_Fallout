// #Misfits Change
using System.Globalization;
using System.Text;
using Content.Server.Chat.Managers;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Ghost;
using Content.Shared.IdentityManagement;
using Content.Shared.Players;
using Content.Shared.Players.RateLimiting;
using Robust.Shared.Console;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server.Chat.Systems;

public sealed partial class ChatSystem
{
    private const string RoleplayQuoteColor = "#f0c674";

    public void SendPrivateDoMessage(ICommonSession session, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        var wrappedMessage = BuildDoWrappedMessage(message);

        _chatManager.ChatMessageToOne(
            ChatChannel.Emotes,
            message,
            wrappedMessage,
            EntityUid.Invalid,
            false,
            session.Channel);
    }

    public void TrySendInGameDoMessage(
        EntityUid source,
        string message,
        ChatTransmitRange range,
        bool hideLog = false,
        IConsoleShell? shell = null,
        ICommonSession? player = null,
        bool ignoreActionBlocker = false)
    {
        if (HasComp<GhostComponent>(source))
        {
            TrySendInGameOOCMessage(source, message, InGameOOCChatType.Dead, range == ChatTransmitRange.HideChat, shell, player);
            return;
        }

        if (player != null && _chatManager.HandleRateLimit(player) != RateLimitStatus.Allowed)
            return;

        if (player?.AttachedEntity is { Valid: true } entity && source != entity)
            return;

        if (!CanSendInGame(message, shell, player))
            return;

        ignoreActionBlocker = CheckIgnoreSpeechBlocker(source, ignoreActionBlocker);

        if (player != null)
            _chatManager.EnsurePlayer(player.UserId).AddEntity(GetNetEntity(source));

        if (player != null && _sanitizer.TryGetBlockedChatResult(message, ChatSanitizationChannel.InCharacter, out var doModeration))
        {
            _sanitizer.ReportBlockedChat(player, message, ".do");
            SendEntityEmote(source, doModeration.ReplacementText, range, null, _language.GetLanguage(source), ignoreActionBlocker: ignoreActionBlocker, author: player.UserId);
            return;
        }

        var shouldPunctuate = _configurationManager.GetCVar(CCVars.ChatPunctuation);
        var shouldCapitalizeTheWordI = (!CultureInfo.CurrentCulture.IsNeutralCulture && CultureInfo.CurrentCulture.Parent.Name == "en")
            || (CultureInfo.CurrentCulture.IsNeutralCulture && CultureInfo.CurrentCulture.Name == "en");

        message = SanitizeInGameICMessage(source, message, out _, capitalize: false, punctuate: shouldPunctuate, capitalizeTheWordI: shouldCapitalizeTheWordI);

        if (string.IsNullOrEmpty(message))
            return;

        SendEntityDo(source, message, range, hideLog, ignoreActionBlocker, player?.UserId);
    }

    private void SendEntityDo(
        EntityUid source,
        string action,
        ChatTransmitRange range,
        bool hideLog = false,
        bool ignoreActionBlocker = false,
        NetUserId? author = null)
    {
        if (!_actionBlocker.CanEmote(source) && !ignoreActionBlocker)
            return;

        var wrappedMessage = BuildDoWrappedMessage(action);

        // Loop over recipients manually and pass EntityUid.Invalid so no speech bubble
        // appears over the sender's head — matching /aemote (anonymous local-area RP) behavior.
        foreach (var (session, data) in GetRecipients(source, Transform(source).GridUid == null ? 0.3f : VoiceRange))
        {
            if (session.AttachedEntity != null
                && Transform(session.AttachedEntity.Value).GridUid != Transform(source).GridUid
                && !CheckAttachedGrids(source, session.AttachedEntity.Value))
                continue;

            var entRange = MessageRangeCheck(session, data, range);
            if (entRange == MessageRangeCheckResult.Disallowed)
                continue;

            var entHideChat = entRange == MessageRangeCheckResult.HideChat;

            _chatManager.ChatMessageToOne(
                ChatChannel.Emotes,
                action,
                wrappedMessage,
                EntityUid.Invalid,   // no entity → no speech bubble over sender
                entHideChat,
                session.Channel,
                author: author);
        }

        _replay.RecordServerMessage(
            new ChatMessage(ChatChannel.Emotes, action, wrappedMessage, default, null, MessageRangeHideChatForReplay(range)));

        if (!hideLog)
            _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Do from {ToPrettyString(source):user}: {action}");
    }

    public void SendEntityNamelessEmote(
        EntityUid source,
        string action,
        ChatTransmitRange range,
        bool hideLog = false,
        bool ignoreActionBlocker = false,
        NetUserId? author = null,
        float? recipientRange = null)
    {
        if (!_actionBlocker.CanEmote(source) && !ignoreActionBlocker)
            return;

        var wrappedMessage = BuildDoWrappedMessage(action);
        var voiceRange = recipientRange ?? (Transform(source).GridUid == null ? 0.3f : VoiceRange);

        foreach (var (session, data) in GetRecipients(source, voiceRange))
        {
            if (session.AttachedEntity != null
                && Transform(session.AttachedEntity.Value).GridUid != Transform(source).GridUid
                && !CheckAttachedGrids(source, session.AttachedEntity.Value))
                continue;

            var entRange = MessageRangeCheck(session, data, range);
            if (entRange == MessageRangeCheckResult.Disallowed)
                continue;

            var entHideChat = entRange == MessageRangeCheckResult.HideChat;

            _chatManager.ChatMessageToOne(
                ChatChannel.Emotes,
                action,
                wrappedMessage,
                source,
                entHideChat,
                session.Channel,
                author: author);
        }

        _replay.RecordServerMessage(
            new ChatMessage(ChatChannel.Emotes, action, wrappedMessage, GetNetEntity(source), null, MessageRangeHideChatForReplay(range)));

        if (!hideLog)
            _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Nameless emote from {ToPrettyString(source):user}: {action}");
    }

    private string BuildEmoteWrappedMessage(EntityUid source, string escapedName, string action)
    {
        var ent = Identity.Entity(source, EntityManager);
        return Loc.GetString("chat-manager-entity-me-wrap-message",
            ("entityName", escapedName),
            ("entity", ent),
            ("message", FormatRoleplayActionMarkup(action)));
    }

    private string BuildDoWrappedMessage(string action)
    {
        return Loc.GetString("chat-manager-entity-do-wrap-message",
            ("message", FormatRoleplayActionMarkup(action)));
    }

    // #Misfits Change /Modify/ — made non-static to access sanitizer helpers; now sanitizes each quoted speech segment independently.
    private string FormatRoleplayActionMarkup(string action)
    {
        if (string.IsNullOrEmpty(action))
            return string.Empty;

        var builder = new StringBuilder(action.Length + 32);
        var index = 0;

        while (index < action.Length)
        {
            var openQuote = action.IndexOf('"', index);
            if (openQuote == -1)
            {
                builder.Append(FormattedMessage.EscapeText(action[index..]));
                break;
            }

            var closeQuote = action.IndexOf('"', openQuote + 1);
            if (closeQuote == -1)
            {
                builder.Append(FormattedMessage.EscapeText(action[index..]));
                break;
            }

            if (openQuote > index)
                builder.Append(FormattedMessage.EscapeText(action[index..openQuote]));

            // #Misfits Change /Add/ — sanitize the speech content inside each quote pair independently.
            var speech = action[(openQuote + 1)..closeQuote];
            speech = SanitizeMessageCapital(speech);
            speech = SanitizeMessageCapitalizeTheWordI(speech, "i");
            if (!string.IsNullOrEmpty(speech) && char.IsLetter(speech[^1]))
                speech += ".";

            builder.Append($"[color={RoleplayQuoteColor}]");
            builder.Append('"');
            builder.Append(FormattedMessage.EscapeText(speech));
            builder.Append('"');
            builder.Append("[/color]");
            index = closeQuote + 1;
        }

        return builder.ToString();
    }
}
