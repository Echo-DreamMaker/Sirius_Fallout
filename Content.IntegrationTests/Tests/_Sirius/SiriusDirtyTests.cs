using System;
using Content.Server._Sirius.Dirtiness;
using Content.Server.Atmos.Components;
using Content.Server.Mood;
using Content.Shared._Misfits.Surgery.Contamination;
using Content.Shared.Alert;
using Content.Shared._Sirius.Dirtiness;
using Content.Shared._Sirius.Wet;
using Content.Shared.CCVar;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Mood;
using Content.Shared.Traits;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Sirius;

// #Sirius Add: smoke tests for the new dirtiness and wet status mechanics.
[TestFixture]
public sealed class SiriusDirtyTests
{
    [TestPrototypes]
    private const string ClothingPenaltyPrototypes = @"
- type: entity
  id: SiriusTestOuter
  components:
  - type: Item
    size: Tiny
  - type: Clothing
    slots: [outerClothing]

- type: entity
  id: SiriusTestHead
  components:
  - type: Item
    size: Tiny
  - type: Clothing
    slots: [head]
";

    [Test]
    public async Task WetStatusHalvesIgnitionMultiplier()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        var entities = server.ResolveDependency<IEntityManager>();
        var dirty = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SiriusDirtySystem>();

        EntityUid uid = default;
        var savedMultiplier = 0f;

        await server.WaitAssertion(() =>
        {
            uid = entities.SpawnEntity("MobHuman", testMap.MapCoords);
            var flammable = entities.EnsureComponent<FlammableComponent>(uid);
            savedMultiplier = flammable.FireStackIncreaseMultiplier;

            dirty.MakeWet(uid, TimeSpan.FromSeconds(30));
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<WetComponent>(uid), Is.True);
                Assert.That(entities.GetComponent<FlammableComponent>(uid).FireStackIncreaseMultiplier,
                    Is.EqualTo(savedMultiplier * 0.5f).Within(0.0001f));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WashingEffectRestoresCleanliness()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        var entities = server.ResolveDependency<IEntityManager>();
        var dirty = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SiriusDirtySystem>();

        EntityUid uid = default;

        await server.WaitAssertion(() =>
        {
            uid = entities.SpawnEntity("MobHuman", testMap.MapCoords);
            var comp = entities.EnsureComponent<SiriusDirtyComponent>(uid);
            comp.Cleanliness = 0f;
            comp.Accumulator = 1f;

            dirty.ApplyWashed(uid, comp.PlainSoapPerSecond, comp.PlainSoapCap, TimeSpan.FromSeconds(30));
        });

        await server.WaitRunTicks(61);

        await server.WaitAssertion(() =>
        {
            var comp = entities.GetComponent<SiriusDirtyComponent>(uid);
            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<SiriusWashedComponent>(uid), Is.True);
                Assert.That(comp.Cleanliness, Is.GreaterThan(3.5f));
                Assert.That(comp.Cleanliness, Is.LessThan(4.5f));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SoapOnCharacterIsHandledBeforeSurgerySanitizer()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        var entities = server.ResolveDependency<IEntityManager>();
        var dirty = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SiriusDirtySystem>();

        EntityUid user = default;
        EntityUid target = default;
        EntityUid soap = default;

        await server.WaitAssertion(() =>
        {
            user = entities.SpawnEntity("MobHuman", testMap.MapCoords);
            target = entities.SpawnEntity("MobHuman", testMap.MapCoords);
            soap = entities.SpawnEntity("Soap", testMap.MapCoords);
            entities.EnsureComponent<SiriusDirtyComponent>(target);
            dirty.MakeWet(target, TimeSpan.FromSeconds(30));
        });

        await server.WaitAssertion(() =>
        {
            var coordinates = entities.GetComponent<TransformComponent>(soap).Coordinates;
            var args = new AfterInteractEvent(user, soap, target, coordinates, true);
            entities.EventBus.RaiseLocalEvent(soap, args);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(entities.HasComponent<SiriusWashedComponent>(target), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SiriusLocalizationAndSoapReactionsLoad()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var prototypes = server.ResolveDependency<IPrototypeManager>();
        var locale = server.ResolveDependency<ILocalizationManager>();

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                string[] reactions =
                {
                    "SiriusSoapWater", "SiriusLye", "SiriusSodiumHydroxide",
                    "SiriusSoapPlain", "SiriusSoapTarberry", "SiriusSoapStarlight",
                    "SiriusSoapGlowpepper", "SiriusSoapAgave",
                };

                foreach (var id in reactions)
                    Assert.That(prototypes.HasIndex<ReactionPrototype>(id), Is.True, $"Reaction {id} did not load");

                string[] perkIds =
                {
                    "SiriusPerkKeenSmell", "SiriusPerkChronicRhinitis",
                    "SiriusPerkCleanFreak", "SiriusPerkStinky",
                };

                foreach (var id in perkIds)
                    Assert.That(prototypes.HasIndex<TraitPrototype>(id), Is.True, $"Trait {id} did not load");

                string[] moodIds =
                {
                    "SiriusSmellSelfAnimal", "SiriusSmellSelfVeryDirty", "SiriusSmellSelfDirty",
                    "SiriusSmellSelfClean", "SiriusSmellSelfFragrant", "SiriusSmellOthersAnimal",
                    "SiriusSmellOthersVeryDirty", "SiriusSmellOthersDirty",
                    "SiriusSmellOthersClean", "SiriusSmellOthersFragrant",
                };

                foreach (var id in moodIds)
                    Assert.That(prototypes.HasIndex<MoodEffectPrototype>(id), Is.True, $"Mood effect {id} did not load");

                string[] keys =
                {
                    "alerts-sirius-dirty-current-state",
                    "sirius-dirty-level-animal",
                    "sirius-dirty-level-fragrant",
                    "sirius-dirty-tooltip-ghoul",
                    "sirius-soap-lather-self",
                    "sirius-soap-lather-target",
                    "sirius-soap-lather-other",
                    "mood-effect-SiriusSmellSelfAnimal",
                    "mood-effect-SiriusSmellOthersFragrant",
                    "trait-name-SiriusPerkKeenSmell",
                    "trait-description-SiriusPerkChronicRhinitis",
                    "trait-name-SiriusPerkCleanFreak",
                    "trait-description-SiriusPerkStinky",
                    "ent-N14GroundTobacco",
                };

                foreach (var key in keys)
                    Assert.That(locale.HasString(key), Is.True, $"Localization key {key} is missing");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OwnSmellMoodFollowsStateChange()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        server.CfgMan.SetCVar(CCVars.MoodEnabled, true);
        var testMap = await pair.CreateTestMap();

        var entities = server.ResolveDependency<IEntityManager>();
        EntityUid uid = default;

        await server.WaitAssertion(() =>
        {
            uid = entities.SpawnEntity("MobHuman", testMap.MapCoords);
            var comp = entities.EnsureComponent<SiriusDirtyComponent>(uid);
            comp.Cleanliness = 5f; // Animal
            comp.Accumulator = 1f;
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var mood = entities.GetComponent<MoodComponent>(uid);
            Assert.Multiple(() =>
            {
                Assert.That(mood.UncategorisedEffects.TryGetValue("SiriusSmellSelfAnimal", out var value), Is.True);
                Assert.That(value, Is.EqualTo(-5f));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task KeenSmellDoublesAndRhinitisBlocksMood()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        server.CfgMan.SetCVar(CCVars.MoodEnabled, true);
        var testMap = await pair.CreateTestMap();

        var entities = server.ResolveDependency<IEntityManager>();
        EntityUid keen = default;
        EntityUid blocked = default;

        await server.WaitAssertion(() =>
        {
            keen = entities.SpawnEntity("MobHuman", testMap.MapCoords);
            blocked = entities.SpawnEntity("MobHuman", testMap.MapCoords);
            entities.EnsureComponent<SiriusDirtyComponent>(keen).Cleanliness = 5f;
            entities.EnsureComponent<SiriusDirtyComponent>(blocked).Cleanliness = 15f; // Dirty
            entities.EnsureComponent<SiriusPerkKeenSmellComponent>(keen);
            entities.EnsureComponent<SiriusPerkChronicRhinitisComponent>(blocked);
            entities.GetComponent<SiriusDirtyComponent>(keen).Accumulator = 1f;
            entities.GetComponent<SiriusDirtyComponent>(blocked).Accumulator = 1f;
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var keenMood = entities.GetComponent<MoodComponent>(keen);
            var blockedMood = entities.GetComponent<MoodComponent>(blocked);
            Assert.Multiple(() =>
            {
                Assert.That(keenMood.UncategorisedEffects["SiriusSmellSelfAnimal"], Is.EqualTo(-10f));
                Assert.That(blockedMood.UncategorisedEffects.ContainsKey("SiriusSmellSelfDirty"), Is.False);
            });
        });

        // Rhinitis still lets the Animal state through.
        await server.WaitAssertion(() =>
        {
            var comp = entities.GetComponent<SiriusDirtyComponent>(blocked);
            comp.Cleanliness = 5f;
            comp.Accumulator = 1f;
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var mood = entities.GetComponent<MoodComponent>(blocked);
            Assert.That(mood.UncategorisedEffects["SiriusSmellSelfAnimal"], Is.EqualTo(-5f));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DecayPerksChangeCleanlinessLoss()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        var entities = server.ResolveDependency<IEntityManager>();
        EntityUid clean = default;
        EntityUid normal = default;
        EntityUid stinky = default;

        await server.WaitAssertion(() =>
        {
            clean = entities.SpawnEntity("MobHuman", testMap.MapCoords);
            normal = entities.SpawnEntity("MobHuman", testMap.MapCoords);
            stinky = entities.SpawnEntity("MobHuman", testMap.MapCoords);

            foreach (var uid in new[] { clean, normal, stinky })
            {
                var comp = entities.EnsureComponent<SiriusDirtyComponent>(uid);
                comp.Cleanliness = 40f;
                comp.Accumulator = 1f;
            }

            entities.EnsureComponent<SiriusPerkCleanFreakComponent>(clean);
            entities.EnsureComponent<SiriusPerkStinkyComponent>(stinky);
        });

        await server.WaitRunTicks(301);

        await server.WaitAssertion(() =>
        {
            var cleanValue = entities.GetComponent<SiriusDirtyComponent>(clean).Cleanliness;
            var normalValue = entities.GetComponent<SiriusDirtyComponent>(normal).Cleanliness;
            var stinkyValue = entities.GetComponent<SiriusDirtyComponent>(stinky).Cleanliness;
            Assert.Multiple(() =>
            {
                Assert.That(cleanValue, Is.GreaterThan(normalValue));
                Assert.That(normalValue, Is.GreaterThan(stinkyValue));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ClothingBlocksWashingRegeneration()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        var entities = server.ResolveDependency<IEntityManager>();
        var inventory = entities.System<InventorySystem>();
        var dirty = entities.System<SiriusDirtySystem>();

        EntityUid naked = default;
        EntityUid dressed = default;

        await server.WaitAssertion(() =>
        {
            naked = entities.SpawnEntity("MobHuman", testMap.MapCoords);
            dressed = entities.SpawnEntity("MobHuman", testMap.MapCoords);

            foreach (var uid in new[] { naked, dressed })
            {
                var comp = entities.EnsureComponent<SiriusDirtyComponent>(uid);
                comp.Cleanliness = 0f;
                comp.Accumulator = 1f;
                dirty.ApplyWashed(uid, comp.PlainSoapPerSecond, comp.PlainSoapCap, TimeSpan.FromSeconds(30));
            }

            var outer = entities.SpawnEntity("SiriusTestOuter", testMap.MapCoords);
            var head = entities.SpawnEntity("SiriusTestHead", testMap.MapCoords);
            Assert.Multiple(() =>
            {
                Assert.That(inventory.TryEquip(dressed, outer, "outerClothing"), Is.True);
                Assert.That(inventory.TryEquip(dressed, head, "head"), Is.True);
            });
        });

        await server.WaitRunTicks(61);

        await server.WaitAssertion(() =>
        {
            var nakedValue = entities.GetComponent<SiriusDirtyComponent>(naked).Cleanliness;
            var dressedValue = entities.GetComponent<SiriusDirtyComponent>(dressed).Cleanliness;
            Assert.Multiple(() =>
            {
                Assert.That(nakedValue, Is.GreaterThan(3.5f));
                // outerClothing (80%) + head (20%) = 100% additive penalty.
                Assert.That(dressedValue, Is.LessThan(0.1f));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OurSoapBarsKeepSurgerySanitizer()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        var entities = server.ResolveDependency<IEntityManager>();
        EntityUid soap = default;

        await server.WaitAssertion(() =>
        {
            soap = entities.SpawnEntity("N14SoapSirius", testMap.MapCoords);
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            Assert.That(entities.HasComponent<SurgeryCleansDirtComponent>(soap), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SurgerySanitizerSkipsMobsButKeepsItems()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        var entities = server.ResolveDependency<IEntityManager>();
        var clean = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SurgeryCleanSystem>();

        await server.WaitAssertion(() =>
        {
            var mob = entities.SpawnEntity("MobHuman", testMap.MapCoords);
            var scalpel = entities.SpawnEntity("Scalpel", testMap.MapCoords);

            entities.EnsureComponent<SurgeryDirtinessComponent>(mob).Dirtiness = FixedPoint2.New(50);
            entities.EnsureComponent<SurgeryDirtinessComponent>(scalpel).Dirtiness = FixedPoint2.New(50);

            Assert.Multiple(() =>
            {
                Assert.That(clean.RequiresCleaning(mob), Is.False);
                Assert.That(clean.RequiresCleaning(scalpel), Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SoapOnItemDoesNotReportNothingToDisinfect()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        var entities = server.ResolveDependency<IEntityManager>();
        var clean = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SurgeryCleanSystem>();

        await server.WaitAssertion(() =>
        {
            var soap = entities.SpawnEntity("N14SoapSirius", testMap.MapCoords);
            var ethanol = entities.SpawnEntity("N14Ethanol", testMap.MapCoords);
            var gloves = entities.SpawnEntity("ClothingHandsGlovesLatex", testMap.MapCoords);
            var scalpel = entities.SpawnEntity("Scalpel", testMap.MapCoords);
            var mob = entities.SpawnEntity("MobHuman", testMap.MapCoords);

            var soapCleaner = entities.GetComponent<SurgeryCleansDirtComponent>(soap);
            var ethanolCleaner = entities.GetComponent<SurgeryCleansDirtComponent>(ethanol);

            Assert.Multiple(() =>
            {
                // #Sirius Fix: soap on an item is a laundry action — no "nothing to disinfect" spam.
                Assert.That(clean.ShouldReportCannotClean((soap, soapCleaner), gloves), Is.False);
                Assert.That(clean.ShouldReportCannotClean((soap, soapCleaner), scalpel), Is.False);
                // Outside an item target the phrase is still relevant.
                Assert.That(clean.ShouldReportCannotClean((soap, soapCleaner), mob), Is.True);
                // Other sanitizers keep their original feedback.
                Assert.That(clean.ShouldReportCannotClean((ethanol, ethanolCleaner), gloves), Is.True);
                Assert.That(clean.TryStartCleaning((soap, soapCleaner), mob, gloves), Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task InternalsAlertAppearsOnlyWithToggleableEquipment()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        var entities = server.ResolveDependency<IEntityManager>();
        var inventory = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<InventorySystem>();
        var alerts = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<AlertsSystem>();

        EntityUid uid = default;

        await server.WaitAssertion(() =>
        {
            uid = entities.SpawnEntity("MobHuman", testMap.MapCoords);
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            Assert.That(alerts.IsShowingAlertCategory(uid, "Internals"), Is.False);
        });

        await server.WaitAssertion(() =>
        {
            var mask = entities.SpawnEntity("ClothingMaskGas", testMap.MapCoords);
            var tank = entities.SpawnEntity("OxygenTank", testMap.MapCoords);

            Assert.Multiple(() =>
            {
                Assert.That(inventory.TryEquip(uid, mask, "mask"), Is.True);
                Assert.That(inventory.TryEquip(uid, tank, "back"), Is.True);
            });
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            Assert.That(alerts.IsShowingAlertCategory(uid, "Internals"), Is.True);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(inventory.TryUnequip(uid, "back"), Is.True);
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            Assert.That(alerts.IsShowingAlertCategory(uid, "Internals"), Is.False);
        });

        await pair.CleanReturnAsync();
    }
}
