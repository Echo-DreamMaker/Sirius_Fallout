using Content.Server._Misfits.Speech.Components;
using Content.Server.Speech;
using Content.Server.Speech.EntitySystems;
using System.Text.RegularExpressions;

namespace Content.Server._Misfits.Speech.EntitySystems;

public sealed class GermanAccentSystem : EntitySystem
{
    [Dependency] private readonly ReplacementAccentSystem _replacement = default!;

    private static readonly Regex RegexThe = new(@"\bthe\b", RegexOptions.IgnoreCase);
    private static readonly Regex RegexThis = new(@"\bthis\b", RegexOptions.IgnoreCase);
    private static readonly Regex RegexThat = new(@"\bthat\b", RegexOptions.IgnoreCase);
    private static readonly Regex RegexThere = new(@"\bthere\b", RegexOptions.IgnoreCase);
    private static readonly Regex RegexThey = new(@"\bthey\b", RegexOptions.IgnoreCase);

    private static readonly Regex RegexThSoft = new(@"th", RegexOptions.IgnoreCase);
    private static readonly Regex RegexWStart = new(@"\bw", RegexOptions.IgnoreCase);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GermanAccentComponent, AccentGetEvent>(OnAccentGet);
    }

    public string Accentuate(string message)
    {
        var words = message.Split(' ');
        var accentuatedWords = new List<string>();

        foreach (var word in words)
        {
            // Apply FTL dictionary replacements first.
            var accentuatedWord = _replacement.ApplyReplacements(word, "n14German");

            // If the word was not replaced by the dictionary, apply German-style phonetics.
            if (accentuatedWord == word)
            {
                accentuatedWord = ApplyRegexReplacements(accentuatedWord);
            }

            accentuatedWords.Add(accentuatedWord);
        }

        return string.Join(" ", accentuatedWords);
    }

    private static string ApplyRegexReplacements(string word)
    {
        // Common German-accent word sounds.
        word = RegexThe.Replace(word, "ze");
        word = RegexThis.Replace(word, "zis");
        word = RegexThat.Replace(word, "zat");
        word = RegexThere.Replace(word, "zere");
        word = RegexThey.Replace(word, "zey");

        // General sound changes.
        word = RegexThSoft.Replace(word, "z");
        word = RegexWStart.Replace(word, "v");

        return word;
    }

    private void OnAccentGet(EntityUid uid, GermanAccentComponent component, AccentGetEvent args)
    {
        args.Message = Accentuate(args.Message);
    }
}
