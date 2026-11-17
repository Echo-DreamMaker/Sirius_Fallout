// Misfits Add - Extended SmileyToEmote dictionary with new keyboard emote entries (O_o, >_<, etc.)
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Content.Server.Administration.Notes;
using Content.Shared.Administration.Notes;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Server.Chat.Managers;

public sealed class ChatSanitizationManager : IChatSanitizationManager
{
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;
    [Dependency] private readonly IAdminNotesManager _adminNotes = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;

    private const string HiFallback = "Hi.";
    private const string UnintelligibleAction = "attempted to say something unintelligible.";

    // #Misfits Tweak - Removed LGBTQ+ identity terms (lesbian, bisexual, queer, trans, transgender,
    // nonbinary, non-binary, pansexual, asexual, intersex) from blocked terms; these are identity labels,
    // not slurs, and players should be free to use them naturally. Slurs remain blocked.
    private static readonly string[] BlockedTerms =
    [
        "homo",
        "dyke",
        "fag",
        "faggot",
        "tranny",
        "nigger",
        "nigga",
        "kike",
        "spic",
        "chink",
        "gook",
        "wetback",
    ];

    private static readonly Regex[] BlockedTermRegexes = BuildBlockedTermRegexes();

    private static readonly Dictionary<string, string> SmileyToEmote = new()
    {
        // I could've done this with regex, but felt it wasn't the right idea.
        { ":)", "chatsan-smiles" },
        { ":]", "chatsan-smiles" },
        { "=)", "chatsan-smiles" },
        { "=]", "chatsan-smiles" },
        { "(:", "chatsan-smiles" },
        { "[:", "chatsan-smiles" },
        { "(=", "chatsan-smiles" },
        { "[=", "chatsan-smiles" },
        { "^^", "chatsan-smiles" },
        { "^-^", "chatsan-smiles" },
        { ":(", "chatsan-frowns" },
        { ":[", "chatsan-frowns" },
        { "=(", "chatsan-frowns" },
        { "=[", "chatsan-frowns" },
        { "):", "chatsan-frowns" },
        { ")=", "chatsan-frowns" },
        { "]:", "chatsan-frowns" },
        { "]=", "chatsan-frowns" },
        { ":D", "chatsan-smiles-widely" },
        { "D:", "chatsan-frowns-deeply" },
        { ":O", "chatsan-surprised" },
        { ":3", "chatsan-smiles" }, //nope
        { ":S", "chatsan-uncertain" },
        { ":>", "chatsan-grins" },
        { ":<", "chatsan-pouts" },
        { "xD", "chatsan-laughs" },
        { ":'(", "chatsan-cries" },
        { ":'[", "chatsan-cries" },
        { "='(", "chatsan-cries" },
        { "='[", "chatsan-cries" },
        { ")':", "chatsan-cries" },
        { "]':", "chatsan-cries" },
        { ")'=", "chatsan-cries" },
        { "]'=", "chatsan-cries" },
        { ";-;", "chatsan-cries" },
        { ";_;", "chatsan-cries" },
        { ":u", "chatsan-smiles-smugly" },
        { ":v", "chatsan-smiles-smugly" },
        { ">:i", "chatsan-annoyed" },
        { ":i", "chatsan-sighs" },
        { ":|", "chatsan-sighs" },
        { ":p", "chatsan-stick-out-tongue" },
        { ";p", "chatsan-stick-out-tongue" },
        { ":b", "chatsan-stick-out-tongue" },
        { "0-0", "chatsan-wide-eyed" },
        { "o-o", "chatsan-wide-eyed" },
        { "o.o", "chatsan-wide-eyed" },
        { "._.", "chatsan-surprised" },
        { ".-.", "chatsan-mumbles" },
        { "-_-", "chatsan-unimpressed" },
        { "o/", "chatsan-waves" },
        { "^^/", "chatsan-waves" },
        { ":/", "chatsan-uncertain" },
        { ":\\", "chatsan-uncertain" },
        { "o7", "chatsan-salutes" },
        { ";_;7", "chatsan-tearfully-salutes"},
        { ";)", "chatsan-winks" },
        { ";]", "chatsan-winks" },
        { "(;", "chatsan-winks" },
        { "[;", "chatsan-winks" },
        { ":')", "chatsan-tearfully-smiles" },
        { ":']", "chatsan-tearfully-smiles" },
        { "=')", "chatsan-tearfully-smiles" },
        { "=']", "chatsan-tearfully-smiles" },
        { "(':", "chatsan-tearfully-smiles" },
        { "[':", "chatsan-tearfully-smiles" },
        { "('=", "chatsan-tearfully-smiles" },
        { "['=", "chatsan-tearfully-smiles" },
        // Corvax-Localization-Start
        { "0_o", "chatsan-wide-eyed" },
        { ":0", "chatsan-surprised" },
        { "T_T", "chatsan-cries" },
        { "=_(", "chatsan-cries" },
        // Corvax-Localization-End

        // Misfits Add - Extended keyboard emote list (symbol-only emoticons)
        // Boggle / wide-eyed confusion variants
        { "O_o", "chatsan-boggles" },
        { "o_O", "chatsan-boggles" },
        { "O_O", "chatsan-boggles" },
        { "o_o", "chatsan-boggles" },
        // Frustration / grimace
        { ">_<", "chatsan-frustrated" },
        { ">.<", "chatsan-frustrated" },
        // Shifty / side-eye
        { ">_>", "chatsan-shifty" },
        { "<_<", "chatsan-shifty" },
        { ">.>", "chatsan-shifty" },
        { "<.<", "chatsan-shifty" },
        // Happy beam
        { "^_^", "chatsan-beams" },
        { "^.^", "chatsan-beams" },
        { "n_n", "chatsan-beams" },
        // Wicked / mischievous grin
        { ">:)", "chatsan-grins-wickedly" },
        { ">:D", "chatsan-grins-wickedly" },
        { ">;)", "chatsan-grins-wickedly" },
        // Scowl / angry frown
        { ">:(", "chatsan-scowls" },
        { ">:[", "chatsan-scowls" },
        // Dejected / resigned
        { "u_u", "chatsan-dejected" },
        { "v_v", "chatsan-dejected" },
        // Starstruck / amazed
        { "*_*", "chatsan-starstruck" },
        { "★_★", "chatsan-starstruck" },
        // Dizzy / disoriented
        { "@_@", "chatsan-dizzy" },
        // Dazed / knocked out
        { "x_x", "chatsan-dazed" },
        { "X_X", "chatsan-dazed" },
        { "x.x", "chatsan-dazed" },
        // Zipping lips
        { ":x", "chatsan-zips-lips" },
        { ":X", "chatsan-zips-lips" },
        { ":-x", "chatsan-zips-lips" },
        { ":-X", "chatsan-zips-lips" },
        // Blowing a kiss
        { ":*", "chatsan-blows-kiss" },
        { ":-*", "chatsan-blows-kiss" },
        { ";*", "chatsan-blows-kiss" },
        // Cheering / throwing arms up
        { "\\o/", "chatsan-cheers" },
        // Nervous sweat
        { "^^;", "chatsan-nervous" },
        { "^_^;", "chatsan-nervous" },
        { "^^;;", "chatsan-nervous" },
        // Facepalm
        { ">_<;", "chatsan-facepalms" },
        // End Misfits Add

        // #Sirius Add: расширенный набор смайлов. Порядок в словаре не важен —
        // совпадения перебираются от самого длинного к короткому (см. SmileyByLength).
        // Только эмоции, у которых уже есть ключ chatsan-* в en-US и ru-RU.
        // Улыбки
        { ":-)", "chatsan-smiles" },
        { ":-]", "chatsan-smiles" },
        { ":^)", "chatsan-smiles" },
        { "8)", "chatsan-smiles" },
        { ":з", "chatsan-smiles" },
        { ":))", "chatsan-smiles-widely" },
        { ":-D", "chatsan-smiles-widely" },
        { "=D", "chatsan-smiles-widely" },
        // Грусть и злость
        { ":-(", "chatsan-frowns" },
        { ":-[", "chatsan-frowns" },
        { ":^(", "chatsan-frowns" },
        { ":c", "chatsan-frowns" },
        { ":((", "chatsan-frowns-deeply" },
        { ">:|", "chatsan-annoyed" },
        // Язык и подмигивание
        { ":-P", "chatsan-stick-out-tongue" },
        { "=P", "chatsan-stick-out-tongue" },
        { ";-]", "chatsan-winks" },
        // Слёзы и сердце
        { ";w;", "chatsan-cries" },
        { "T.T", "chatsan-cries" },
        { "</3", "chatsan-cries" },
        { "<3", "chatsan-beams" },
        // Широко раскрытые глаза и головокружение
        { "0_0", "chatsan-wide-eyed" },
        { "0.0", "chatsan-wide-eyed" },
        { "D8", "chatsan-wide-eyed" },
        { "@.@", "chatsan-dizzy" },
        // Уныние и жесты
        { "u.u", "chatsan-dejected" },
        { "\\o", "chatsan-waves" },
        { "^^7", "chatsan-salutes" },
        // Умиление
        { "OwO", "chatsan-looks-flustered" },
        { "UwU", "chatsan-looks-flustered" },
        { ">w<", "chatsan-looks-flustered" },
        { "^w^", "chatsan-beams" },
        // #Sirius End
    };

    // #Sirius Add: выбор самого длинного совпадения. Иначе короткий "o/" перехватывает "\o/".
    private static readonly KeyValuePair<string, string>[] SmileyByLength =
        SmileyToEmote.OrderByDescending(pair => pair.Key.Length).ToArray();

    // Misfits Add - Text acronyms that fire emote actions on ALL spoken channels (not just Telepathic).
    // Kept separate from SmileyToEmote so they can be applied unconditionally via TrySanitizeAcronyms.
    // Ordered longest→shortest within each group to prevent prefix swallowing (e.g. lmfao before lmao).
    private static readonly Dictionary<string, string> AcronymToEmote = new()
    {
        // Laughter
        { "lmfao", "chatsan-laughs-heartily" }, // laughing my f***ing ass off
        { "rofl",  "chatsan-rolls-laughing" },   // rolling on the floor laughing
        { "lmao",  "chatsan-laughs-heartily" },  // laughing my ass off
        { "heehee", "chatsan-chuckles" },        // quiet giggle
        { "hehe",  "chatsan-chuckles" },         // quiet laugh
        { "heh",   "chatsan-chuckles" },         // understated chuckle
        { "kek",   "chatsan-laughs" },           // kek (lol variant)
        { "lel",   "chatsan-laughs" },           // lel (lol variant)
        { "lol",   "chatsan-laughs" },           // laugh out loud
        // Expressive / reaction
        { "owo", "chatsan-looks-flustered" },    // flustered/cute reaction
        { "uwu", "chatsan-looks-flustered" },    // flustered/cute reaction
        // Confusion / uncertainty
        { "omfg", "chatsan-surprised" },         // oh my f***ing god
        { "omg",  "chatsan-surprised" },         // oh my god
        { "wtf",  "chatsan-mumbles" },          // what the f***
        { "tbh",  "chatsan-shrugs" },            // to be honest
        { "idk",  "chatsan-shrugs" },            // I don't know
        { "?",    "chatsan-mumbles" },           // Misfits Add - lone question mark; sounds more natural over radio than "looks confused"
        // Physical gestures
        { "smh",  "chatsan-shakes-head" },       // shaking my head
        { "fml",  "chatsan-facepalms" },         // f*** my life
        { "ngl",  "chatsan-shifty" },            // not gonna lie
        // Social
        { "tysm", "chatsan-beams" },             // thank you so much
        { "thx",  "chatsan-nods" },              // thanks
        { "brb",  "chatsan-waves" },             // be right back
        { "afk",  "chatsan-waves" },             // away from keyboard
        { "np",   "chatsan-nods" },              // no problem
        // Gestures / salutes
        { "o7",   "chatsan-salutes" },           // Misfits Add - military salute; fires on all channels
        { ";_;7", "chatsan-tearfully-salutes" }, // Misfits Add - tearful salute; fires on all channels
    };
    // End Misfits Add (AcronymToEmote)

    // #Sirius Add: кириллические сокращения и смех. Раньше автозамена понимала только латиницу,
    // поэтому "лол", "кек", "ахаха" в чате оставались обычным текстом.
    private static readonly Dictionary<string, string> CyrillicAcronymToEmote = new()
    {
        // Смех
        { "рофл", "chatsan-rolls-laughing" },   // rolling on the floor laughing
        { "лмао", "chatsan-laughs-heartily" },  // laughing my ass off
        { "кек", "chatsan-laughs" },            // kek
        { "кекв", "chatsan-laughs" },           // kek (вариант)
        { "лол", "chatsan-laughs" },            // laugh out loud
        { "ору", "chatsan-laughs-heartily" },   // сленговое "ору" = хохочу
        { "ржу", "chatsan-laughs-heartily" },   // ржу
        { "хех", "chatsan-chuckles" },          // сдержанный смешок
        { "хд", "chatsan-laughs" },             // xD кириллицей
        // Реакции
        { "омг", "chatsan-surprised" },         // oh my god
        { "омфг", "chatsan-surprised" },        // oh my fucking god
        { "втф", "chatsan-mumbles" },           // what the fuck
        { "ппц", "chatsan-mumbles" },           // пипец
        { "жесть", "chatsan-surprised" },       // жесть
        { "кринж", "chatsan-frustrated" },      // кринж
        { "ура", "chatsan-cheers" },            // ура
        // Жесты и вежливость
        { "спс", "chatsan-nods" },              // спасибо
        { "нз", "chatsan-nods" },               // не за что
        { "брб", "chatsan-waves" },             // be right back
        { "афк", "chatsan-waves" },             // away from keyboard
        { "пж", "chatsan-nods" },               // пожалуйста
    };
    // #Sirius End

    // #Sirius Add: русский смех пишется не одним словом, а повтором слогов ("ахаха", "хахаха", "хихи").
    // Точное совпадение такое не ловит, поэтому проверяем хвост сообщения регуляркой.
    private static readonly Regex LaughterRegex = new(
        @"(?<![\p{L}])(?:(?:[аa]?х){2,}[аa]?|(?:хи){2,}[аa]?|(?:хе){2,}х?)\s*[.!?,…]*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // #Sirius Add: скобочные смайлы. "))" — широкая улыбка, "((" — сильная грусть,
    // ")))" и длиннее — смех, "(((" и длиннее — злость. Необязательный префикс ":;=8"
    // позволяет целиком съесть ":)))" и "=))", а не оставлять двоеточие в тексте.
    private static readonly Regex ParenSmileyRegex = new(
        @"(?:[:;=8])?(?<run>\){2,}|\({2,})$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // #Sirius Add: одиночная скобка превращается в эмоцию, только если в тексте нет парной.
    // Так "(за хлебом)" и "(он ушёл)" остаются обычным текстом.
    private static readonly Regex SingleParenSmileyRegex = new(
        @"(?<![:;=8])([()])$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private bool _doSanitize;

    // Anti-Goida
    private static readonly Regex GoydaRegex = new(@"[Gg][Oo]+[Yy][Dd][Aa]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public void Initialize()
    {
        _configurationManager.OnValueChanged(CCVars.ChatSanitizerEnabled, x => _doSanitize = x, true);
    }

    public bool TryGetBlockedChatResult(string input, ChatSanitizationChannel channel, [NotNullWhen(true)] out BlockedChatMessageResult? result)
    {
        result = null;

        if (!_doSanitize || string.IsNullOrWhiteSpace(input))
            return false;

        foreach (var regex in BlockedTermRegexes)
        {
            if (!regex.IsMatch(input))
                continue;

            result = channel == ChatSanitizationChannel.OutOfCharacter
                ? new BlockedChatMessageResult(HiFallback, false)
                : new BlockedChatMessageResult(UnintelligibleAction, true);
            return true;
        }

        return false;
    }

    public void ReportBlockedChat(ICommonSession player, string rawMessage, string contextLabel)
    {
        var trimmed = rawMessage.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return;

        _chatManager.SendAdminAlert($"Auto-moderation blocked {player.Name} ({player.UserId}) in {contextLabel}: \"{trimmed}\"");
        CreateModerationNote(player, trimmed, contextLabel);
    }

    public bool TrySanitizeOutSmilies(string input, EntityUid speaker, out string sanitized, [NotNullWhen(true)] out string? emote)
    {
        if (!_doSanitize)
        {
            sanitized = input;
            emote = null;
            return false;
        }

        input = input.TrimEnd();

        // Apply Anti-Goida filter
        input = GoydaRegex.Replace(input, "I am an idiot");

        // #Sirius Add: сначала словарь смайлов. Перебор от длинного к короткому,
        // чтобы "o/" не перехватывал "\o/" и "^^" не съедал "^^7".
        foreach (var (smiley, replacement) in SmileyByLength)
        {
            if (!input.EndsWith(smiley, StringComparison.OrdinalIgnoreCase))
                continue;

            sanitized = input.Remove(input.Length - smiley.Length).TrimEnd();
            emote = Loc.GetString(replacement, ("ent", speaker));
            return true;
        }

        // #Sirius Add: серии скобок. "))" — широко улыбается, "((" — сильно хмурится,
        // ")))" и длиннее — смеётся, "(((" и длиннее — сердито хмурится.
        var parenSmiley = ParenSmileyRegex.Match(input);
        if (parenSmiley.Success)
        {
            var run = parenSmiley.Groups["run"].Value;
            var emoteId = run[0] == ')'
                ? run.Length == 2 ? "chatsan-smiles-widely" : "chatsan-laughs"
                : run.Length == 2 ? "chatsan-frowns-deeply" : "chatsan-scowls";

            sanitized = input.Remove(parenSmiley.Index).TrimEnd();
            emote = Loc.GetString(emoteId, ("ent", speaker));
            return true;
        }

        // #Sirius Add: одиночная скобка становится эмоцией только без парной скобки в тексте.
        // Так "(за хлебом)" и "(он ушёл)" остаются обычным текстом.
        var singleParen = SingleParenSmileyRegex.Match(input);
        if (singleParen.Success)
        {
            var paren = singleParen.Value[0];
            var opposite = paren == ')' ? '(' : ')';
            if (!input.AsSpan(0, singleParen.Index).Contains(opposite))
            {
                sanitized = input.Remove(singleParen.Index).TrimEnd();
                emote = Loc.GetString(paren == ')' ? "chatsan-smiles" : "chatsan-frowns", ("ent", speaker));
                return true;
            }
        }

        sanitized = input;
        emote = null;
        return false;
    }

    // Misfits Add - Separate acronym check so it fires on ALL spoken channels, not just Telepathic.
    // Called unconditionally from SanitizeInGameICMessage before capitalize/punctuate passes.
    public bool TrySanitizeAcronyms(string input, EntityUid speaker, out string sanitized, [NotNullWhen(true)] out string? emote)
    {
        if (!_doSanitize)
        {
            sanitized = input;
            emote = null;
            return false;
        }

        input = input.TrimEnd();

        // #Sirius Add: повторяющийся смех проверяем первым — "ахаха", "хахаха", "хихи", "хехе".
        // Чем длиннее смех, тем он сердечнее.
        var laughter = LaughterRegex.Match(input);
        if (laughter.Success)
        {
            var laughText = laughter.Value;
            var heartily = laughText.Count(c => c is 'х' or 'Х') >= 4;
            sanitized = input.Remove(laughter.Index).TrimEnd();
            emote = Loc.GetString(heartily ? "chatsan-laughs-heartily" : "chatsan-laughs", ("ent", speaker));
            return true;
        }

        if (TryMatchAcronym(input, AcronymToEmote, speaker, out sanitized, out emote))
            return true;

        // #Sirius Add: теперь и кириллические сокращения.
        if (TryMatchAcronym(input, CyrillicAcronymToEmote, speaker, out sanitized, out emote))
            return true;

        sanitized = input;
        emote = null;
        return false;
    }
    // End Misfits Add (TrySanitizeAcronyms)

    /// <summary>
    /// #Sirius Add: общий разбор сокращений для латинского и кириллического словарей.
    /// Сокращение должно быть отдельным словом в конце сообщения, а не хвостом другого слова.
    /// </summary>
    private bool TryMatchAcronym(
        string input,
        Dictionary<string, string> acronyms,
        EntityUid speaker,
        out string sanitized,
        [NotNullWhen(true)] out string? emote)
    {
        foreach (var (acronym, replacement) in acronyms)
        {
            if (!input.EndsWith(acronym, true, CultureInfo.InvariantCulture))
                continue;

            // Misfits Fix - ensure the acronym is a standalone token, not the tail of a real sentence.
            // Trim trailing whitespace from the portion before the match so that "What ?" and "What?"
            // are both rejected (the real last character before the token would be 't', a letter).
            // A bare "?" or "idk" alone has an empty before-string and always matches.
            var matchStart = input.Length - acronym.Length;
            if (matchStart > 0)
            {
                var beforeTrimmed = input[..matchStart].TrimEnd();
                if (beforeTrimmed.Length > 0 && char.IsLetterOrDigit(beforeTrimmed[^1]))
                    continue;
            }

            sanitized = input.Remove(matchStart).TrimEnd();
            emote = Loc.GetString(replacement, ("ent", speaker));
            return true;
        }

        sanitized = input;
        emote = null;
        return false;
    }

    private async void CreateModerationNote(ICommonSession player, string rawMessage, string contextLabel)
    {
        var noteMessage = $"Automatic chat moderation blocked a message in {contextLabel}. Raw attempted message: \"{rawMessage}\"";
        await _adminNotes.AddSystemRemark(player.UserId, NoteType.Note, noteMessage, NoteSeverity.Medium, true, null);
    }

    private static Regex[] BuildBlockedTermRegexes()
    {
        var regexes = new Regex[BlockedTerms.Length];

        for (var i = 0; i < BlockedTerms.Length; i++)
        {
            regexes[i] = BuildBlockedTermRegex(BlockedTerms[i]);
        }

        return regexes;
    }

    private static Regex BuildBlockedTermRegex(string term)
    {
        var pattern = new StringBuilder(@"(?<![\p{L}\p{N}])");
        var appendSeparator = false;

        foreach (var character in term)
        {
            if (character is ' ' or '-' or '_')
            {
                pattern.Append(@"[\W_]*");
                appendSeparator = false;
                continue;
            }

            if (appendSeparator)
                pattern.Append(@"[\W_]*");

            pattern.Append(GetProtectedCharacterPattern(character));
            appendSeparator = true;
        }

        pattern.Append(@"(?![\p{L}\p{N}])");
        return new Regex(pattern.ToString(), RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string GetProtectedCharacterPattern(char character)
    {
        return char.ToLowerInvariant(character) switch
        {
            'a' => "[a4@]",
            'b' => "[b8]",
            'e' => "[e3]",
            'g' => "[g69]",
            'i' => "[i1!|]",
            'l' => "[l1|]",
            'o' => "[o0]",
            's' => "[s5$]",
            't' => "[t7+]",
            'z' => "[z2]",
            _ => Regex.Escape(character.ToString()),
        };
    }
}
