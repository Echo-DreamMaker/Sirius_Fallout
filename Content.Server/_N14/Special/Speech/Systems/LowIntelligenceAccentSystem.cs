using System.Text;
using System.Text.RegularExpressions;
using Content.Server.Speech;
using Content.Server.Speech.Components;
using Content.Server._N14.Special.Speech.Components;
using Content.Shared._Misfits.Special;
using Content.Shared._Misfits.Special.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._N14.Special.Speech.EntitySystems
{
    /// <summary>
    /// Corrupts speech of low-Intelligence characters. Three layers:
    ///  1. Word-level colloquial replacements (что → што).
    ///  2. Phonetic lone-vowel distortion (а → у when isolated, e.g. "аве" → "уву").
    ///  3. Mumbling: random words replaced with incoherent grunts (гым, умм).
    ///
    /// Severity scales with deficit below the Intelligence requirement (4 − INT).
    /// </summary>
    public sealed class LowIntelligenceAccentSystem : EntitySystem
    {
        [Dependency] private readonly SharedSpecialSystem _special = default!;
        [Dependency] private readonly IRobustRandom _random = default!;

        private static readonly HashSet<char> Vowels = new("аеёиоуыэюяАЕЁИОУЫЭЮЯ");

        private static readonly string[] Grunts = { "гым", "умм", "гмм", "ннн", "ммм" };

        private static readonly (Regex Pattern, string Replacement)[] WordRules = new[]
        {
            (new Regex(@"\bчто\b",         RegexOptions.Compiled | RegexOptions.IgnoreCase), "што"),
            (new Regex(@"\bсейчас\b",      RegexOptions.Compiled | RegexOptions.IgnoreCase), "щас"),
            (new Regex(@"\bвообще\b",       RegexOptions.Compiled | RegexOptions.IgnoreCase), "ваще"),
            (new Regex(@"\bконечно\b",      RegexOptions.Compiled | RegexOptions.IgnoreCase), "канешна"),
            (new Regex(@"\bтебя\b",         RegexOptions.Compiled | RegexOptions.IgnoreCase), "тибя"),
            (new Regex(@"\bтебе\b",         RegexOptions.Compiled | RegexOptions.IgnoreCase), "тибе"),
            (new Regex(@"\bменя\b",         RegexOptions.Compiled | RegexOptions.IgnoreCase), "миня"),
            (new Regex(@"\bсебя\b",         RegexOptions.Compiled | RegexOptions.IgnoreCase), "сибя"),
            (new Regex(@"\bсебе\b",         RegexOptions.Compiled | RegexOptions.IgnoreCase), "сибе"),
            (new Regex(@"\bничего\b",       RegexOptions.Compiled | RegexOptions.IgnoreCase), "ниче"),
            (new Regex(@"\bчтобы\b",        RegexOptions.Compiled | RegexOptions.IgnoreCase), "штобы"),
            (new Regex(@"\bещё\b",          RegexOptions.Compiled | RegexOptions.IgnoreCase), "исчо"),
            (new Regex(@"\bтут\b",          RegexOptions.Compiled | RegexOptions.IgnoreCase), "тук"),
            (new Regex(@"\bкогда\b",        RegexOptions.Compiled | RegexOptions.IgnoreCase), "кагда"),
            (new Regex(@"\bпотому\b",       RegexOptions.Compiled | RegexOptions.IgnoreCase), "патаму"),
            (new Regex(@"\bбыстро\b",       RegexOptions.Compiled | RegexOptions.IgnoreCase), "бистро"),
            (new Regex(@"\bпожалуйста\b",   RegexOptions.Compiled | RegexOptions.IgnoreCase), "пажалста"),
            (new Regex(@"\bспасибо\b",      RegexOptions.Compiled | RegexOptions.IgnoreCase), "пасиба"),
            (new Regex(@"\bздравствуй(?:те)?\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "здрасте"),
            (new Regex(@"\bпочему\b",       RegexOptions.Compiled | RegexOptions.IgnoreCase), "пачему"),
            (new Regex(@"\bсегодня\b",      RegexOptions.Compiled | RegexOptions.IgnoreCase), "седня"),
            (new Regex(@"\bэтот\b",         RegexOptions.Compiled | RegexOptions.IgnoreCase), "эт"),
            (new Regex(@"\bможет\b",        RegexOptions.Compiled | RegexOptions.IgnoreCase), "мож"),
            (new Regex(@"\bнадо\b",         RegexOptions.Compiled | RegexOptions.IgnoreCase), "над"),
            (new Regex(@"\bнормально\b",    RegexOptions.Compiled | RegexOptions.IgnoreCase), "норм"),
            (new Regex(@"\bточно\b",        RegexOptions.Compiled | RegexOptions.IgnoreCase), "точн"),
            (new Regex(@"\bпросто\b",       RegexOptions.Compiled | RegexOptions.IgnoreCase), "прост"),
            (new Regex(@"\bкстати\b",       RegexOptions.Compiled | RegexOptions.IgnoreCase), "кстат"),
            (new Regex(@"ться\b",           RegexOptions.Compiled), "ца"),
            (new Regex(@"тся\b",            RegexOptions.Compiled), "ца"),
        };

        public override void Initialize()
        {
            SubscribeLocalEvent<LowIntelligenceAccentComponent, AccentGetEvent>(OnAccent);
        }

        private void OnAccent(EntityUid uid, LowIntelligenceAccentComponent component, AccentGetEvent args)
        {
            var intelligence = _special.GetEffective(uid, SpecialStat.Intelligence);
            args.Message = Accentuate(args.Message, intelligence);
        }

        public string Accentuate(string message, int intelligence)
        {
            message = message.Trim();
            if (string.IsNullOrEmpty(message))
                return message;

            var deficit = Math.Max(0, 4 - intelligence);
            if (deficit <= 0)
                return message;

            foreach (var (pattern, replacement) in WordRules)
                message = pattern.Replace(message, replacement);

            message = CorruptLoneVowels(message, deficit);
            message = ApplyMumbling(message, deficit);

            return message;
        }

        private string CorruptLoneVowels(string message, int deficit)
        {
            var tuning = _special.GetTuning();
            var chance = Math.Clamp(
                tuning.IntelligenceVowelReplaceBaseChance + deficit * tuning.IntelligenceVowelReplaceChancePerPoint,
                0f, 0.9f);

            var words = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
                return message;

            var sb = new StringBuilder();
            for (var i = 0; i < words.Length; i++)
            {
                if (i > 0)
                    sb.Append(' ');
                sb.Append(ReplaceLoneVowels(words[i], chance));
            }

            return sb.ToString();
        }

        private string ReplaceLoneVowels(string word, float chance)
        {
            if (word.Length < 2)
                return word;

            var chars = word.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (!IsVowel(chars[i]))
                    continue;

                var prevIsVowel = i > 0 && IsVowel(chars[i - 1]);
                var nextIsVowel = i < chars.Length - 1 && IsVowel(chars[i + 1]);

                if (prevIsVowel || nextIsVowel)
                    continue;

                if (!_random.Prob(chance))
                    continue;

                chars[i] = char.IsUpper(chars[i]) ? 'У' : 'у';
            }

            return new string(chars);
        }

        private string ApplyMumbling(string message, int deficit)
        {
            var words = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
                return message;

            var tuning = _special.GetTuning();

            var minWords = Math.Max(2, tuning.IntelligenceMumbleMinWordsBase - deficit);
            if (words.Length < minWords)
                return message;

            var mumbleRate = Math.Clamp(
                tuning.IntelligenceMumbleChanceBase + deficit * tuning.IntelligenceMumbleChancePerPoint,
                0f, 0.8f);

            var numToMumble = Math.Max(1, (int)Math.Round(words.Length * mumbleRate));
            numToMumble = Math.Min(numToMumble, words.Length);

            var candidates = new List<int>(words.Length);
            for (var i = 0; i < words.Length; i++)
                candidates.Add(i);

            for (var n = 0; n < numToMumble && candidates.Count > 0; n++)
            {
                var pick = _random.Next(candidates.Count);
                var wordIdx = candidates[pick];
                candidates.RemoveAt(pick);
                words[wordIdx] = Grunts[wordIdx % Grunts.Length];
            }

            return string.Join(' ', words);
        }

        private static bool IsVowel(char c) => Vowels.Contains(c);
    }
}
