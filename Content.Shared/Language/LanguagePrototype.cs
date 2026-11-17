using Content.Shared.Chat;
using Content.Shared.Speech;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.Shared.Language;

[Prototype("language")]
public sealed partial class LanguagePrototype : IPrototype
{
    // #Misfits Change Fix: avoid surfacing raw localization ids when a language entry is missing.
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    ///     Whether this language will display its name in chat behind a player's name.
    /// </summary>
    [DataField]
    public bool IsVisibleLanguage { get; set; }

    /// <summary>
    ///     Obfuscation method used by this language. By default, uses <see cref="ObfuscationMethod.Default"/>
    /// </summary>
    [DataField("obfuscation")]
    public ObfuscationMethod Obfuscation = ObfuscationMethod.Default;

    /// <summary>
    ///     Speech overrides used for messages sent in this language.
    /// </summary>
    [DataField("speech")]
    public SpeechOverrideInfo SpeechOverride = new();

    #region utility
    /// <summary>
    ///     The in-world name of this language, localized.
    /// </summary>
    public string Name => GetLocalizedOrFallback($"language-{ID}-name", ID);

    /// <summary>
    ///     The in-world chat abbreviation of this language, localized.
    /// </summary>
    public string ChatName => GetLocalizedOrFallback($"chat-language-{ID}-name", Name);

    /// <summary>
    ///     The in-world description of this language, localized.
    /// </summary>
    public string Description => GetLocalizedOrFallback($"language-{ID}-description", string.Empty);

    private static string GetLocalizedOrFallback(string key, string fallback)
    {
        var localization = IoCManager.Resolve<ILocalizationManager>();
        return localization.TryGetString(key, out var value) ? value : fallback;
    }
    #endregion utility
}

[DataDefinition]
public sealed partial class SpeechOverrideInfo
{
    /// <summary>
    ///     Color which text in this language will be blended with.
    ///     Alpha blending is used, which means the alpha component of the color controls the intensity of this color.
    /// </summary>
    [DataField]
    public Color? Color = null;

    [DataField]
    public string? FontId;

    [DataField]
    public int? FontSize;

    [DataField]
    public bool AllowRadio = true;

    /// <summary>
    ///     If true, the message will be relayed to the Empathy Chat and
    ///     anyone with that language will also hear Empathy Chat. (Unless user has ShadowkinBlackeyeComponent)
    ///     This is mostly only use for "Marish" but... fuckit modularity :p
    /// </summary>
    [DataField]
    public bool EmpathySpeech = false;

    /// <summary>
    ///     If false, the entity can use this language even when it's unable to speak (i.e. muffled or muted),
    ///     and accents are not applied to messages in this language.
    /// </summary>
    [DataField]
    public bool RequireSpeech = true;

    /// <summary>
    ///     #Sirius Add: a replacement accent that is always applied to messages in this language,
    ///     shaping how the language "sounds" (or, for non-verbal languages like sign language, how it looks).
    ///     Unlike the <see cref="RequireSpeech"/> gate for entity accents, this is applied even when
    ///     <see cref="RequireSpeech"/> is false, because it is part of the language itself.
    /// </summary>
    [DataField]
    public ProtoId<ReplacementAccentPrototype>? Accent;

    /// <summary>
    ///     If not null, all messages in this language will be forced to be spoken in this chat type.
    /// </summary>
    [DataField]
    public InGameICChatType? ChatTypeOverride;

    /// <summary>
    ///     Speech verb overrides. If not provided, the default ones for the entity are used.
    /// </summary>
    [DataField]
    public List<LocId>? SpeechVerbOverrides;

    /// <summary>
    ///     Overrides for different kinds chat message wraps. If not provided, the default ones are used.
    /// </summary>
    /// <remarks>
    ///     Currently, only local chat and whispers support this. Radio and emotes are unaffected.
    ///     This is horrible.
    /// </remarks>
    [DataField]
    public Dictionary<InGameICChatType, LocId> MessageWrapOverrides = new();
}
