using System.Threading.Tasks;
using Content.Server.Chat.Systems;
using Content.Server.Language;
using Content.Server.Radio.Components;
using Content.Shared._NC.CorvaxVars;
using Content.Shared._NC.TTS;
using Content.Shared.GameTicking;
using Content.Shared.Language;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Players.RateLimiting;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;


namespace Content.Server._NC.TTS;

// ReSharper disable once InconsistentNaming
public sealed partial class TTSSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly INetConfigurationManager _netCfg = default!;
    [Dependency] private readonly LanguageSystem _language = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly TTSManager _ttsManager = default!;
    [Dependency] private readonly SharedTransformSystem _xforms = default!;
    [Dependency] private readonly IRobustRandom _rng = default!;

    private readonly List<string> _sampleText =
        new()
        {
            "War... War never changes.",
            "Scum! Get off my land!",
            "Another settler needs your help. I'll mark it on your map.",
            "A door trap is a lone stalker's best friend.",
            "Smells like something's burning... and not soup.",
            "Who is shooting? I've got enough rounds for everyone!",
            "Find a bottlecap and the day was not wasted.",
            "That robot has clearly seen better days... and more intact gears.",
            "Radiation is just an unpleasant tingling on your skin.",
            "They say there are giant ants in the wasteland... I hope it's just rumors.",
            "My psi-knife needs sharpening... and fresh brains.",
            "Do you see a gopher? It is there! Or maybe it's just radiation hallucinations...",
            "Hey, smoothskin! Got anything to trade for caps?",
            "My brahmin looks thoughtful today... Probably swallowed the wrong lunch.",
            "Those super mutants have gotten out of hand! Someone needs to set things right.",
            "Traveler, greetings. Want to play Caravan?",
            "My energy pistol needs a recharge... and a couple of fresh cells.",
            "They say there are ghosts in these ruins... Probably just ghouls joking.",
            "Another day in the wasteland, another story for the tavern.",
            "These security bots keep getting more pushy... Just like my ex.",
            "Found a new gun? Show me! Just don't point it my way.",
            "The wasteland is beautiful... if you forget about radiation, mutants, and raiders.",
            "My Geiger counter crackles more than Diamond City Radio!",
            "Seen that guy in the blue jumpsuit? Says he's from a vault... Weird fellow.",
            "Hey, stalker! Want to join our caravan? We'll split it fairly... probably."
        };

    private const int MaxMessageChars = 100 * 2; // same as SingleBubbleCharLimit * 2
    private bool _isEnabled = false;

    public override void Initialize()
    {
        _cfg.OnValueChanged(CorvaxVars.TTSEnabled, v => _isEnabled = v, true);

        SubscribeLocalEvent<TransformSpeechEvent>(OnTransformSpeech);
        SubscribeLocalEvent<TTSComponent, EntitySpokeEvent>(OnEntitySpoke);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);

        SubscribeNetworkEvent<RequestPreviewTTSEvent>(OnRequestPreviewTTS);

        RegisterRateLimits();
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _ttsManager.ResetCache();
    }

    private async void OnRequestPreviewTTS(RequestPreviewTTSEvent ev, EntitySessionEventArgs args)
    {
        if (!_isEnabled ||
            !_prototypeManager.TryIndex<TTSVoicePrototype>(ev.VoiceId, out var protoVoice))
            return;

        if (HandleRateLimit(args.SenderSession) != RateLimitStatus.Allowed)
            return;

        var previewText = _rng.Pick(_sampleText);
        var soundData = await GenerateTTS(previewText, protoVoice.Speaker);
        if (soundData is null)
            return;

        RaiseNetworkEvent(new PlayTTSEvent(soundData), Filter.SinglePlayer(args.SenderSession));
    }

    private async void OnEntitySpoke(EntityUid uid, TTSComponent component, EntitySpokeEvent args) 
    { 
        if (TryComp<MindContainerComponent>(uid, out var mindCon) 
            && TryComp<MindComponent>(mindCon.Mind, out var mind) && mind.Session != null) 
        { 
            var channel = mind.Session.Channel; 
            if (!_netCfg.GetClientCVar(channel, CorvaxVars.LocalTTSEnabled)) 
                return; 
        }

        if (HasComp<ActiveRadioComponent>(uid))
            await Task.Delay(1000);
        
        var voiceId = component.VoicePrototypeId; 
        if (!_isEnabled || 
            args.Message.Length > MaxMessageChars || 
            voiceId == null) 
            return;
        
        var voiceEv = new TransformSpeakerVoiceEvent(uid, voiceId); 
        RaiseLocalEvent(uid, voiceEv); 
        voiceId = voiceEv.VoiceId;
        
        if (!_prototypeManager.TryIndex<TTSVoicePrototype>(voiceId, out var protoVoice)) 
            return;
        
        var obfuscatedMessage = _language.ObfuscateSpeech(args.Message, args.Language);
        
        await Handle(uid, args.Message, protoVoice.Speaker, args.IsWhisper, obfuscatedMessage, args.Language);
    }
    
    private async Task Handle(
        EntityUid uid,
        string message,
        string speaker,
        bool isWhisper,
        string obfuscatedMessage,
        LanguagePrototype language
        )
    { 
        var fullSoundData = await GenerateTTS(message, speaker, isWhisper); 
        if (fullSoundData is null) return;
        await Task.Delay(70);
        
        var obfSoundData = await GenerateTTS(obfuscatedMessage, speaker, isWhisper); 
        if (obfSoundData is null) return;
        
        var fullTtsEvent = new PlayTTSEvent(fullSoundData, GetNetEntity(uid), isWhisper);
        var obfTtsEvent = new PlayTTSEvent(obfSoundData, GetNetEntity(uid), isWhisper);
        
        var xformQuery = GetEntityQuery<TransformComponent>(); 
        var sourcePos = _xforms.GetWorldPosition(xformQuery.GetComponent(uid), xformQuery); 
        var recipients = Filter.Pvs(uid).Recipients;
        
        foreach (var session in recipients) 
        { 
            if (!session.AttachedEntity.HasValue) continue;
            
            var listener = session.AttachedEntity.Value; 
            var xform = xformQuery.GetComponent(listener); 
            var distance = (sourcePos - _xforms.GetWorldPosition(xform, xformQuery)).Length();
            
            if (distance > ChatSystem.VoiceRange) continue;
            var canUnderstand = _language.CanUnderstand(listener, language);
            
            RaiseNetworkEvent(canUnderstand ? fullTtsEvent : obfTtsEvent, session); 
        } 
    }
    
    // ReSharper disable once InconsistentNaming
    private async Task<byte[]?> GenerateTTS(string text, string speaker, bool isWhisper = false)
    {
        var textSanitized = Sanitize(text);
        if (textSanitized == "") return null;
        if (char.IsLetter(textSanitized[^1]))
            textSanitized += ".";

        var ssmlTraits = SoundTraits.RateFast;
        if (isWhisper)
            ssmlTraits = SoundTraits.PitchVerylow;
        var textSsml = ToSsmlText(textSanitized, ssmlTraits);

        return await _ttsManager.ConvertTextToSpeech(speaker, textSsml);
    }
}
