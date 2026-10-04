// #Misfits Change
using Content.Server._Misfits.GhoulReversal;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Humanoid;
using Content.Server.Mind;
using Content.Server.Preferences.Managers;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.EntityEffects;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Content.Server.Ghoul;
using Content.Server._Misfits.Ghoul;
using Content.Shared._N14.Radiation.Components;
using Content.Shared.Damage;


namespace Content.Server._Misfits.EntityEffects.Effects.GhoulReversal;

/// <summary>
/// Reagent effect that reverses a ghoul back into a human, but only if they were
/// ghoulified within the configured time window (default 12 real hours).
/// Round-start ghouls (no GhoulificationTimeComponent) are permanently blocked.
/// </summary>
[UsedImplicitly]
public sealed partial class GhoulReversalEffect : EntityEffect
{
    /// <summary>
    /// Species IDs this effect can reverse.
    /// </summary>
    [DataField]
    public List<string> GhoulSpecies = new() { "Ghoul", "GhoulGlowing" };

    /// <summary>
    /// Species to revert to.
    /// </summary>
    [DataField]
    public string TargetSpecies = "Human";

    // Do we ever have a need for this? We don't persist ghoulification across rounds...
    /// <summary>
    /// Whether to update the character's database profile so the reversal persists across rounds.
    /// </summary>
    [DataField]
    public bool UpdateDatabaseProfile = false;

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-ghoul-reversal", ("chance", Probability));

    public override void Effect(EntityEffectBaseArgs args)
    {
        var entityManager = args.EntityManager;
        var uid = args.TargetEntity;

        if (!entityManager.TryGetComponent<HumanoidAppearanceComponent>(uid, out var appearance))
            return;

        if (!GhoulSpecies.Contains(appearance.Species))
            return;

        var playerManager = IoCManager.Resolve<IPlayerManager>();
        var chatManager = IoCManager.Resolve<IChatManager>();

        // Round-start ghouls have no timer — permanently blocked
        if (!entityManager.TryGetComponent<GhoulificationTimeComponent>(uid, out var timeComp))
        {
            // Private message to the target only — they are not reversible.
            if (playerManager.TryGetSessionByEntity(uid, out var session1)
                && session1.Status == SessionStatus.InGame)
            {
                var tooOldMsg = Loc.GetString("ghoul-reversal-reagent-too-old");
                chatManager.ChatMessageToOne(ChatChannel.Local, tooOldMsg, tooOldMsg,
                    EntityUid.Invalid, false, session1.Channel);
            }
            return;
        }

        var elapsed = DateTime.UtcNow - timeComp.GhoulifiedAtUtc;
        if (elapsed.TotalHours > timeComp.ReversibleWindowHours)
        {
            // Private message to target — reversal window has expired.
            if (playerManager.TryGetSessionByEntity(uid, out var session2)
                && session2.Status == SessionStatus.InGame)
            {
                var tooOldMsg = Loc.GetString("ghoul-reversal-reagent-too-old");
                chatManager.ChatMessageToOne(ChatChannel.Local, tooOldMsg, tooOldMsg,
                    EntityUid.Invalid, false, session2.Channel);
            }
            return;
        }

        // Validate target species
        if (!IoCManager.Resolve<IPrototypeManager>().TryIndex<SpeciesPrototype>(TargetSpecies, out _))
            return;
        
        // Revert species and damage modifier sets
        var humanoidSys = entityManager.EntitySysManager.GetEntitySystem<HumanoidAppearanceSystem>();
        humanoidSys.SetSpecies(uid, TargetSpecies);
        entityManager.TryGetComponent<DamageableComponent>(uid, out var damageable);
        damageable?.DamageModifierSets.Remove("Ghoul");
        damageable?.DamageModifierSets.Remove("N14GammaShield");

        // Remove the feral tracker so they don't go feral again
        entityManager.RemoveComponentDeferred<FeralGhoulifyComponent>(uid);
        entityManager.RemoveComponentDeferred<FeralGhoulifyOverTimeComponent>(uid);
        entityManager.RemoveComponent<GhoulificationTimeComponent>(uid);
        
        // Remove radiation healing
        entityManager.RemoveComponent<RadiationHealingComponent>(uid);

        // Private transformation message to the target only.
        if (playerManager.TryGetSessionByEntity(uid, out var session)
            && session.Status == SessionStatus.InGame)
        {
            var selfMsg = Loc.GetString("ghoul-reversal-reagent-self");
            chatManager.ChatMessageToOne(ChatChannel.Local, selfMsg, selfMsg,
                EntityUid.Invalid, false, session.Channel);
        }

        // Emote visible to nearby bystanders — emote system prefixes the entity name.
        var chatSys = args.EntityManager.EntitySysManager.GetEntitySystem<ChatSystem>();
        chatSys.TrySendInGameICMessage(uid,
            Loc.GetString("ghoul-reversal-reagent-others"),
            InGameICChatType.Emote, ChatTransmitRange.Normal, ignoreActionBlocker: true);

        // Update the database profile so the reversion persists across rounds.
        if (UpdateDatabaseProfile)
            UpdateProfileAsync(uid, entityManager);
    }

    // Note for future contributors: We do not use this currently
    private async void UpdateProfileAsync(EntityUid uid, IEntityManager entityManager)
    {
        try
        {
            var mindSys = entityManager.EntitySysManager.GetEntitySystem<MindSystem>();
            if (!mindSys.TryGetMind(uid, out _, out var mind) || mind.Session == null)
                return;

            var session = mind.Session;
            var prefs = IoCManager.Resolve<IServerPreferencesManager>();
            var netManager = IoCManager.Resolve<IServerNetManager>();
            var cfg = IoCManager.Resolve<IConfigurationManager>();

            var preferences = prefs.GetPreferences(session.UserId);
            if (!preferences.Characters.TryGetValue(preferences.SelectedCharacterIndex, out var profile))
                return;

            if (profile is not HumanoidCharacterProfile humanoidProfile)
                return;

            var newProfile = humanoidProfile.WithSpecies(TargetSpecies);
            await prefs.SetProfile(session.UserId, preferences.SelectedCharacterIndex, newProfile);

            var updatedPrefs = prefs.GetPreferences(session.UserId);
            var msg = new MsgPreferencesAndSettings
            {
                Preferences = updatedPrefs,
                Settings = new GameSettings
                {
                    MaxCharacterSlots = ServerPreferencesManager.GetMaxCharacterSlots(cfg)
                }
            };
            netManager.ServerSendMessage(msg, session.Channel);
        }
        catch (Exception ex)
        {
            Logger.Error($"GhoulReversalEffect: Failed to update character profile: {ex}");
        }
    }
}
