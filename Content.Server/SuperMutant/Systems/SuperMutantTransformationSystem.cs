using System.Threading.Tasks;
using Content.Server.Body.Systems;
using Content.Server.Chemistry.Containers.EntitySystems;
using Content.Server.Humanoid;
using Content.Server.Mind;
using Content.Server.Players.JobWhitelist;
using Content.Server.Preferences.Managers;
using Content.Server.SuperMutant.Components;
using Content.Shared.CCVar;
using Content.Shared.Chemistry.Components;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared._N14.SuperMutant;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.SuperMutant.Systems;

/// <summary>
/// Handles the transformation of characters into Super Mutants when injected with
/// transformation serum. Updates both in-game entity and database character profiles.
/// </summary>
public sealed class SuperMutantTransformationSystem : EntitySystem
{
    [Dependency] private readonly IServerPreferencesManager _prefs = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly HumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SolutionContainerSystem _solutions = default!;
    [Dependency] private readonly JobWhitelistManager _jobWhitelist = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IServerNetManager _netManager = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private int MaxCharacterSlots => _cfg.GetCVar(CCVars.GameMaxCharacterSlots);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SuperMutantTransformationComponent, InjectorDoAfterEvent>(OnInjectorDoAfter);
    }

    private void OnInjectorDoAfter(EntityUid uid, SuperMutantTransformationComponent component, InjectorDoAfterEvent args)
    {
        // Ensure injection completed successfully
        if (args.Cancelled || args.Args.Target == null)
            return;

        var target = args.Args.Target.Value;
        var user = args.Args.User;

        // Only work on living humanoids
        if (!HasComp<HumanoidAppearanceComponent>(target) || !HasComp<MobStateComponent>(target))
            return;

        // Perform the transformation after successful injection
        TransformToSuperMutant(target, user, component);
    }

    private async void TransformToSuperMutant(EntityUid target, EntityUid user, SuperMutantTransformationComponent component)
    {
        // Verify the target is still valid
        if (!Exists(target) || Deleted(target))
            return;

        // Get humanoid appearance component
        if (!TryComp<HumanoidAppearanceComponent>(target, out var appearance))
            return;

        // Check if already a super mutant
        if (appearance.Species == component.TargetSpecies)
        {
            _popup.PopupEntity(Loc.GetString("supermutant-transform-already"), target, user);
            return;
        }

        // Show popup messages
        _popup.PopupEntity(Loc.GetString(component.TransformationMessage), target, target, PopupType.LargeCaution);
        _popup.PopupEntity(Loc.GetString(component.TransformationOthersMessage, ("target", target)),
            target, Filter.PvsExcept(target), true, PopupType.MediumCaution);

        // Transform the entity's species in-game
        if (_prototype.TryIndex<SpeciesPrototype>(component.TargetSpecies, out var species))
        {
            _humanoid.SetSpecies(target, component.TargetSpecies);
            EnsureComp<SuperMutantComponent>(target);
        }

        // Update the database profile if this is a player character
        if (component.UpdateDatabaseProfile && _mind.TryGetMind(target, out var mindId, out var mind))
        {
            if (mind.Session != null)
            {
                var prefs = _prefs.GetPreferences(mind.Session.UserId);
                await UpdateCharacterProfile(mind.Session, prefs.SelectedCharacterIndex, component);
            }
        }
    }

    private async Task UpdateCharacterProfile(ICommonSession session, int slot, SuperMutantTransformationComponent component)
    {
        try
        {
            var userId = session.UserId;
            var prefs = _prefs.GetPreferences(userId);
            if (prefs == null || !prefs.Characters.TryGetValue(slot, out var profile))
                return;

            if (profile is not HumanoidCharacterProfile humanoidProfile)
                return;

            // Create updated profile with Super Mutant species
            var newProfile = humanoidProfile
                .WithSpecies(component.TargetSpecies);

            // Update job priorities to include Super Mutant job if configured
            if (component.UpdateJob)
            {
                newProfile = newProfile.WithJobPriority(component.TargetJob, JobPriority.High);
            }

            // Save the updated profile
            await _prefs.SetProfile(userId, slot, newProfile);

            // Add user to SuperMutant job whitelist
            _jobWhitelist.AddWhitelist(userId, new ProtoId<JobPrototype>(component.TargetJob));

            // Send updated preferences to client to refresh lobby screen
            SendPreferencesToClient(session);
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to update character profile for Super Mutant transformation: {ex}");
        }
    }

    private void SendPreferencesToClient(ICommonSession session)
    {
        try
        {
            var prefs = _prefs.GetPreferences(session.UserId);
            var msg = new MsgPreferencesAndSettings
            {
                Preferences = prefs,
                Settings = new GameSettings
                {
                    MaxCharacterSlots = MaxCharacterSlots
                }
            };
            _netManager.ServerSendMessage(msg, session.Channel);
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to send preferences to client: {ex}");
        }
    }
}
