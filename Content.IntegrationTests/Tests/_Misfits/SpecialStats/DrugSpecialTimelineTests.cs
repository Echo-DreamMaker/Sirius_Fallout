// #Misfits Add - Integration tests: phased drug SPECIAL timelines (peak -> crash -> recovery)
// applied via SpecialStatTimelineEffect, plus the N14 drug damage-resistance sets and the
// N14AlcoholSPECIAL multi-parent inheritance for alcohol.

#nullable enable

using Content.Server.Body.Systems;
using Content.Shared._Misfits.Special;
using Content.Shared._Misfits.Special.Components;
using Content.Shared._Misfits.SpecialStats;
using Content.Shared.Chemistry.Components;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Misfits.SpecialStats;

[TestFixture]
public sealed class DrugSpecialTimelineTests
{
    [Test]
    public async Task Psycho_TimelineAdvancesThroughCrashToRecovery()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bloodstreamSystem = entityManager.System<BloodstreamSystem>();

        EntityUid treated = default;

        await server.WaitAssertion(() =>
        {
            treated = entityManager.SpawnEntity("MobHuman", map.GridCoords);

            // Drug boosts are mirrored onto SpecialComponent temporary modifiers,
            // so the mob needs a SpecialComponent to observe the real stat increase.
            entityManager.EnsureComponent<SpecialComponent>(treated);

            Assert.That(
                bloodstreamSystem.TryAddToChemicals(treated, new Solution("DamageModifyingMixture", FixedPoint2.New(5))),
                Is.True,
                "DamageModifyingMixture (Psycho) should enter the mob's bloodstream.");
        });

        // First metabolism tick -> phase 0: peak (+3 AGI, -3 INT, 50% resist).
        await pair.RunSeconds(10f);

        await server.WaitAssertion(() =>
        {
            Assert.That(entityManager.HasComponent<DrugSpecialTimelineComponent>(treated), Is.True,
                "Psycho should install a drug timeline once metabolism starts.");

            var timeline = entityManager.GetComponent<DrugSpecialTimelineComponent>(treated);
            var boost = entityManager.HasComponent<DrugSpecialBoostComponent>(treated)
                ? entityManager.GetComponent<DrugSpecialBoostComponent>(treated)
                : null;

            Assert.Multiple(() =>
            {
                Assert.That(timeline.PhaseIndex, Is.EqualTo(0));
                Assert.That(timeline.ActiveResistSet, Is.EqualTo("N14DrugResist50"));
            });

            Assert.That(boost, Is.Not.Null, "Psycho should create a DrugSpecialBoostComponent.");
            Assert.Multiple(() =>
            {
                Assert.That(boost!.AgilityBoost, Is.EqualTo(3));
                Assert.That(boost.IntelligenceBoost, Is.EqualTo(-3));
            });

            // The boost must translate into a real SPECIAL characteristic increase.
            var special = entityManager.GetComponent<SpecialComponent>(treated);
            Assert.Multiple(() =>
            {
                Assert.That(special.TemporaryAgilityModifier, Is.EqualTo(3));
                Assert.That(special.TemporaryIntelligenceModifier, Is.EqualTo(-3));
            });

            var damageable = entityManager.GetComponent<DamageableComponent>(treated);
            Assert.That(damageable.DamageModifierSets, Does.Contain("N14DrugResist50"));
        });

        // Phase 0 -> 1 (crash): fast-forward the anchor past the 240s crash threshold only.
        await server.WaitAssertion(() =>
        {
            var timing = server.ResolveDependency<IGameTiming>();
            var timeline = entityManager.GetComponent<DrugSpecialTimelineComponent>(treated);
            timeline.Anchor = timing.CurTime - TimeSpan.FromSeconds(245);
        });

        // Two ticks: the timeline writes the new phase's boost values, then the boost
        // mirror (DrugSpecialBoostSystem) translates them onto SpecialComponent.
        await pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            var timeline = entityManager.GetComponent<DrugSpecialTimelineComponent>(treated);
            Assert.Multiple(() =>
            {
                Assert.That(timeline.PhaseIndex, Is.EqualTo(1));
                Assert.That(timeline.ActiveResistSet, Is.EqualTo("N14DrugResist25"));
            });

            var boost = entityManager.GetComponent<DrugSpecialBoostComponent>(treated);
            Assert.Multiple(() =>
            {
                Assert.That(boost.AgilityBoost, Is.EqualTo(0));
                Assert.That(boost.IntelligenceBoost, Is.EqualTo(-2));
            });

            var special = entityManager.GetComponent<SpecialComponent>(treated);
            Assert.Multiple(() =>
            {
                Assert.That(special.TemporaryAgilityModifier, Is.EqualTo(0));
                Assert.That(special.TemporaryIntelligenceModifier, Is.EqualTo(-2));
            });

            var damageable = entityManager.GetComponent<DamageableComponent>(treated);
            Assert.Multiple(() =>
            {
                Assert.That(damageable.DamageModifierSets, Does.Not.Contain("N14DrugResist50"));
                Assert.That(damageable.DamageModifierSets, Does.Contain("N14DrugResist25"));
            });
        });

        // Phase 1 -> recovery: jump to the end; timeline, boosts and resist set are cleaned up.
        await server.WaitAssertion(() =>
        {
            var timing = server.ResolveDependency<IGameTiming>();
            var timeline = entityManager.GetComponent<DrugSpecialTimelineComponent>(treated);
            timeline.Anchor = timing.CurTime - TimeSpan.FromSeconds(600);
        });

        await pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(entityManager.HasComponent<DrugSpecialTimelineComponent>(treated), Is.False,
                    "The timeline should be removed once the final phase is reached.");
                Assert.That(entityManager.HasComponent<DrugSpecialBoostComponent>(treated), Is.False,
                    "All stat boosts should be removed on recovery.");
            });

            var special = entityManager.GetComponent<SpecialComponent>(treated);
            Assert.Multiple(() =>
            {
                Assert.That(special.TemporaryAgilityModifier, Is.EqualTo(0));
                Assert.That(special.TemporaryIntelligenceModifier, Is.EqualTo(0));
            });

            var damageable = entityManager.GetComponent<DamageableComponent>(treated);
            Assert.That(damageable.DamageModifierSets, Does.Not.Contain("N14DrugResist25"),
                "The drug resist set should be removed once the timeline ends.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task N14Alcohol_InheritsSPECIALTimeline()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var bloodstreamSystem = entityManager.System<BloodstreamSystem>();

        EntityUid treated = default;

        await server.WaitAssertion(() =>
        {
            treated = entityManager.SpawnEntity("MobHuman", map.GridCoords);

            // Alcohol requires a SpecialComponent to observe the temporary PER drop.
            entityManager.EnsureComponent<SpecialComponent>(treated);

            Assert.That(
                bloodstreamSystem.TryAddToChemicals(treated, new Solution("N14RoentgenRum", FixedPoint2.New(5))),
                Is.True,
                "N14RoentgenRum should enter the mob's bloodstream.");
        });

// N14 alcohol installs a Medicine-group SPECIAL timeline directly on the reagent:
// -1 PER at phase 0, back to normal after 10 minutes.
        await pair.RunSeconds(10f);

        await server.WaitAssertion(() =>
        {
            Assert.That(entityManager.HasComponent<DrugSpecialTimelineComponent>(treated), Is.True,
                "N14 alcohol should install the SPECIAL timeline via its Medicine metabolism.");

            var timeline = entityManager.GetComponent<DrugSpecialTimelineComponent>(treated);
            var boost = entityManager.HasComponent<DrugSpecialBoostComponent>(treated)
                ? entityManager.GetComponent<DrugSpecialBoostComponent>(treated)
                : null;

            Assert.That(timeline.PhaseIndex, Is.EqualTo(0));

            Assert.That(boost, Is.Not.Null, "N14 alcohol should create a DrugSpecialBoostComponent.");
            Assert.That(boost!.PerceptionBoost, Is.EqualTo(-1));

            var special = entityManager.GetComponent<SpecialComponent>(treated);
            Assert.That(special.TemporaryPerceptionModifier, Is.EqualTo(-1));
        });

        await pair.CleanReturnAsync();
    }
}