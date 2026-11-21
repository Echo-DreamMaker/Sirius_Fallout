using System.Linq;
using Content.Server.GameTicking;
using Content.Shared._Sirius.Dirtiness;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects;
using Content.Shared.Humanoid;
using Robust.Shared.Prototypes;

namespace Content.Server._Sirius.Dirtiness;

/// <summary>
/// #Sirius Add: grants dirtiness only to the four player species (not robots) and wires up
/// reagent reactions: water makes the target wet, soap water washes them.
/// </summary>
public sealed class SiriusSpawnSystem : EntitySystem
{
    // #Sirius Add: GhoulGlowing and Nightkin are variants of the same four species.
    private static readonly HashSet<string> AllowedSpecies = new()
    {
        "Human", "SuperMutant", "Nightkin", "Ghoul", "GhoulGlowing", "Deathclaw",
    };

    private static readonly string[] WaterReagents =
    {
        "Water", "WaterDirty", "WaterSalt", "WaterIrradiated", "WaterFiltered",
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent args)
    {
        if (!TryComp<HumanoidAppearanceComponent>(args.Mob, out var appearance))
            return;

        if (!AllowedSpecies.Contains(appearance.Species))
            return;

        EnsureComp<SiriusDirtyComponent>(args.Mob);
        EnsureReactiveEntries(args.Mob);
    }

    private void EnsureReactiveEntries(EntityUid uid)
    {
        var reactive = EnsureComp<ReactiveComponent>(uid);
        reactive.Reactions ??= new List<Content.Shared.Chemistry.Reaction.ReactiveReagentEffectEntry>();

        // #Sirius Add: any water reagent splashed on the target keeps them wet for 2s per unit (up to a minute).
        reactive.Reactions.Add(new Content.Shared.Chemistry.Reaction.ReactiveReagentEffectEntry
        {
            Methods = new HashSet<ReactionMethod> { ReactionMethod.Touch },
            Reagents = WaterReagents.Select(reagent => new ProtoId<ReagentPrototype>(reagent)).ToHashSet(),
            Effects = new List<EntityEffect> { new SiriusMakeWetEffect() },
        });

        // #Sirius Add: soap water works like lathering with plain soap.
        reactive.Reactions.Add(new Content.Shared.Chemistry.Reaction.ReactiveReagentEffectEntry
        {
            Methods = new HashSet<ReactionMethod> { ReactionMethod.Touch },
            Reagents = new HashSet<ProtoId<ReagentPrototype>> { "SoapWater" },
            Effects = new List<EntityEffect> { new SiriusWashEffect() },
        });
    }
}
