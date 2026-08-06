// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Chat.Systems;
using Content.Server.Electrocution;
using Content.Server.Polymorph.Components;
using Content.Server.Polymorph.Systems;
using Content.Shared._Misfits.EntityEffects;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Popups;
using Content.Shared.Speech;

namespace Content.Server._Misfits.Genetics.EntityEffects;

public sealed class GeneticsEffectEventSystem : EntitySystem
{
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly ElectrocutionSystem _electrocution = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly PolymorphSystem _polymorph = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FlammableComponent, GeneticsFlammableEffectEvent>(OnFlammable);
        SubscribeLocalEvent<SpeechComponent, GeneticsSpeakEffectEvent>(OnSpeak);
        SubscribeLocalEvent<TransformComponent, GeneticsPopupEffectEvent>(OnPopup);
        SubscribeLocalEvent<TransformComponent, GeneticsEmoteEffectEvent>(OnEmote);
        SubscribeLocalEvent<TransformComponent, GeneticsHealthChangeEffectEvent>(OnHealthChange);
        SubscribeLocalEvent<TransformComponent, GeneticsIgniteEffectEvent>(OnIgnite);
        SubscribeLocalEvent<TransformComponent, GeneticsElectrocuteEffectEvent>(OnElectrocute);
        SubscribeLocalEvent<TransformComponent, GeneticsPolymorphEffectEvent>(OnPolymorph);
    }

    private void OnFlammable(Entity<FlammableComponent> ent, ref GeneticsFlammableEffectEvent args)
    {
        var stacks = ent.Comp.FireStacks == 0f ? args.Multiplier : ent.Comp.FireStacks * (args.Multiplier - 1f);
        _flammable.AdjustFireStacks(ent, stacks, ent.Comp);
    }

    private void OnSpeak(Entity<SpeechComponent> ent, ref GeneticsSpeakEffectEvent args)
    {
        _chat.TrySendInGameICMessage(ent, args.Message, InGameICChatType.Speak, hideChat: args.HideChat);
    }

    private void OnPopup(Entity<TransformComponent> ent, ref GeneticsPopupEffectEvent args)
    {
        if (args.Method == "PopupCoordinates")
        {
            _popup.PopupCoordinates(args.Message, ent.Comp.Coordinates, args.VisualType);
            return;
        }

        if (args.Recipients == GeneticsPopupRecipients.Local)
            _popup.PopupEntity(args.Message, ent, ent, args.VisualType);
        else
            _popup.PopupEntity(args.Message, ent, args.VisualType);
    }

    private void OnEmote(Entity<TransformComponent> ent, ref GeneticsEmoteEffectEvent args)
    {
        if (args.ShowInChat)
            _chat.TryEmoteWithChat(ent, args.EmoteId, ChatTransmitRange.GhostRangeLimit, forceEmote: args.Force);
        else
            _chat.TryEmoteWithoutChat(ent, args.EmoteId);
    }

    private void OnHealthChange(Entity<TransformComponent> ent, ref GeneticsHealthChangeEffectEvent args)
    {
        _damageable.TryChangeDamage(ent, args.Damage, args.IgnoreResistances, interruptsDoAfters: false,
            targetPart: args.TargetPart, partMultiplier: 0.5f, canSever: false);
    }

    private void OnIgnite(Entity<TransformComponent> ent, ref GeneticsIgniteEffectEvent args)
    {
        _flammable.Ignite(ent, args.Source);
    }

    private void OnElectrocute(Entity<TransformComponent> ent, ref GeneticsElectrocuteEffectEvent args)
    {
        _electrocution.TryDoElectrocution(ent, null, args.Damage, args.Duration, args.Refresh,
            ignoreInsulation: args.BypassInsulation);
    }

    private void OnPolymorph(Entity<TransformComponent> ent, ref GeneticsPolymorphEffectEvent args)
    {
        EnsureComp<PolymorphableComponent>(ent);
        _polymorph.PolymorphEntity(ent, args.Prototype);
    }
}
