// #Misfits Change - Willower Tree delivery, cooldown, and default regression coverage.
using System.Linq;
using Content.Server._Misfits.SmokeSignal;
using Content.Server._Misfits.WastelandMap;
using Content.Shared.Access.Components;
using Content.Shared._Misfits.SmokeSignal;
using Content.Shared._Misfits.TribalHunt;
using Content.Shared._Misfits.WastelandMap;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Content.Shared.Tag;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._Misfits;

[TestFixture]
public sealed class WillowerTreeCommunicationsTest
{
    // #Misfits Add - Tree tactical feed only follows tagged Willower identification items.
    [Test]
    public async Task TribeFeedTracksOnlyWillowerIdentificationItems()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.ResolveDependency<IEntityManager>();
        var tags = entities.System<TagSystem>();
        var maps = entities.System<WastelandMapSystem>();

        await server.WaitAssertion(() =>
        {
            var pendants = new[]
            {
                entities.SpawnEntity("N14IDTribeBossPendant", new EntityCoordinates(map.Grid, 1f, 1f)),
                entities.SpawnEntity("N14IDTribeSawbonePendant", new EntityCoordinates(map.Grid, 2f, 1f)),
                entities.SpawnEntity("N14IDTribeEnforcerPendant", new EntityCoordinates(map.Grid, 3f, 1f)),
                entities.SpawnEntity("N14IDTribeBulletsPendant", new EntityCoordinates(map.Grid, 4f, 1f)),
            };
            var tribalCard = entities.SpawnEntity("MisfitsRobotNavCardTribal", new EntityCoordinates(map.Grid, 5f, 1f));
            var ordinaryCard = entities.SpawnEntity("MisfitsRobotNavCardRobCo", new EntityCoordinates(map.Grid, 6f, 1f));
            var huntTarget = entities.SpawnEntity(null, new EntityCoordinates(map.Grid, 7f, 1f));
            var legendary = entities.EnsureComponent<LegendaryCreatureComponent>(huntTarget);
            legendary.CreatureName = "Test Hunt";
            var names = new[] { "Chieftan", "Shaman", "Farmer", "Tribal" };
            for (var i = 0; i < pendants.Length; i++)
                entities.GetComponent<IdCardComponent>(pendants[i]).FullName = names[i];
            entities.GetComponent<IdCardComponent>(tribalCard).FullName = "Spirit-Tender";

            var component = new WastelandMapComponent
            {
                TacticalFeed = WastelandMapTacticalFeedKind.Tribe,
                MapTexturePath = new ResPath("_Misfits/Maps/wendover_map.png"),
                WorldBounds = new Box2(-10f, -10f, 10f, 10f),
                ActivatorJobs = ["TribalShaman", "TribalElder"],
            };
            var state = maps.BuildState(component, map.MapId);
            component.TacticalFeed = WastelandMapTacticalFeedKind.Brotherhood;
            var existingFeedState = maps.BuildState(component, map.MapId);

            Assert.Multiple(() =>
            {
                Assert.That(pendants.All(pendant => tags.HasTag(pendant, "IdCardTribe")), Is.True);
                Assert.That(tags.HasTag(tribalCard, "IdCardTribe"), Is.True);
                Assert.That(tags.HasTag(ordinaryCard, "IdCardTribe"), Is.False);
                Assert.That(maps.AllowsSharedOverlays(WastelandMapTacticalFeedKind.Tribe), Is.False);
                Assert.That(maps.AllowsSharedOverlays(WastelandMapTacticalFeedKind.Brotherhood), Is.True);
                Assert.That(state.TrackedBlips.Any(x => x.Kind == WastelandMapTrackedBlipKind.TribalHuntTarget), Is.False);
                Assert.That(existingFeedState.TrackedBlips.Any(x => x.Kind == WastelandMapTrackedBlipKind.TribalHuntTarget), Is.True);
                Assert.That(state.TrackedBlips.Select(x => x.Label), Is.EquivalentTo(new[]
                {
                    "Chieftan (Willowers Chieftan)",
                    "Shaman (Willowers Shaman)",
                    "Farmer (Willowers Farmer)",
                    "Tribal (Willowers Tribal)",
                    "Spirit-Tender (Protectron Spirit-Tender)",
                }));
                Assert.That(state.TrackedBlips.All(x => x.Kind == WastelandMapTrackedBlipKind.Willower), Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }

    // #Misfits Add - Tree TacMap authorizes leaders while leaving maps without allowlists unrestricted.
    [Test]
    public async Task TreeTacMapAllowsOnlyWillowerLeaders()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.ResolveDependency<IEntityManager>();
        var minds = entities.System<SharedMindSystem>();
        var roles = entities.System<SharedRoleSystem>();
        var maps = entities.System<WastelandMapSystem>();

        await server.WaitAssertion(() =>
        {
            EntityUid SpawnWithJob(string job)
            {
                var body = entities.SpawnEntity(null, map.GridCoords);
                entities.EnsureComponent<MindContainerComponent>(body);
                var mind = minds.CreateMind(null).Owner;
                minds.TransferTo(mind, body);
                roles.MindAddRole(mind, new JobComponent { Prototype = job });
                return body;
            }

            var shaman = SpawnWithJob("TribalShaman");
            var elder = SpawnWithJob("TribalElder");
            var tribal = SpawnWithJob("Tribal");
            var tree = entities.SpawnEntity("TribalTree", map.GridCoords);
            var treeMap = entities.GetComponent<WastelandMapComponent>(tree);

            Assert.Multiple(() =>
            {
                Assert.That(treeMap.ActivatorJobs, Is.EquivalentTo(["TribalShaman", "TribalElder"]));
                Assert.That(maps.CanOpenMap(shaman, treeMap), Is.True);
                Assert.That(maps.CanOpenMap(elder, treeMap), Is.True);
                Assert.That(maps.CanOpenMap(tribal, treeMap), Is.False);
                Assert.That(maps.CanOpenMap(tribal, new WastelandMapComponent()), Is.True);
                Assert.That(maps.CanOpenMap(tribal, new WastelandMapComponent { ActivatorJobs = [] }), Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TreeAnnouncementUsesTreeConfigAndKeepsDefaultSignalSettings()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.ResolveDependency<IEntityManager>();
        var locale = server.ResolveDependency<ILocalizationManager>();

        await server.WaitAssertion(() =>
        {
            var tree = entities.SpawnEntity("TribalTree", map.GridCoords);
            var treeSignal = entities.GetComponent<SmokeSignalComponent>(tree);
            var signalFire = entities.SpawnEntity("MisfitsTribalSignalFire", map.GridCoords);
            var defaultSignal = entities.GetComponent<SmokeSignalComponent>(signalFire);
            var bonfire = entities.SpawnEntity("N14Bonfire", map.GridCoords);
            var bonfireSignal = entities.GetComponent<SmokeSignalComponent>(bonfire);

            Assert.Multiple(() =>
            {
                Assert.That(treeSignal.Cooldown, Is.EqualTo(TimeSpan.Zero));
                Assert.That(treeSignal.MaxMessageLength, Is.EqualTo(128));
                Assert.That(treeSignal.TargetDepartment, Is.EqualTo("Tribe"));
                Assert.That(treeSignal.NearbyRange, Is.Zero);
                Assert.That(treeSignal.OpenOnActivate, Is.False);
                Assert.That(treeSignal.ActivatorJobs, Is.EquivalentTo(["TribalShaman", "TribalElder"]));
                Assert.That(treeSignal.Verb, Is.EqualTo("willower-tree-announce-verb"));
                Assert.That(treeSignal.BroadcastMessage, Is.EqualTo("willower-tree-announcement"));
                Assert.That(treeSignal.CooldownMessage, Is.EqualTo("willower-tree-announcement-cooldown"));
                Assert.That(defaultSignal.ActivatorJobs, Is.Null);
                Assert.That(defaultSignal.OpenOnActivate, Is.True);
                Assert.That(defaultSignal.Verb, Is.EqualTo("smoke-signal-verb"));
                Assert.That(defaultSignal.BroadcastMessage, Is.EqualTo("smoke-signal-broadcast"));
                Assert.That(defaultSignal.CooldownMessage, Is.EqualTo("smoke-signal-cooldown"));
                Assert.That(defaultSignal.NearbyRange, Is.EqualTo(18f));
                Assert.That(defaultSignal.Cooldown, Is.EqualTo(TimeSpan.FromMinutes(10)));
                Assert.That(bonfireSignal.Cooldown, Is.EqualTo(TimeSpan.FromMinutes(10)));
                Assert.That(locale.GetString(treeSignal.BroadcastMessage,
                        ("sender", "Willow"),
                        ("message", "Return to the village.")),
                    Is.EqualTo("Willow speaks through the Tree of Life: Return to the village."));
                Assert.That(locale.GetString(defaultSignal.BroadcastMessage,
                        ("sender", "Willow"),
                        ("message", "Raiders approaching.")),
                    Is.EqualTo("Willow sends a smoke signal: Raiders approaching."));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TreeAnnouncementDeliversToLivingWillowersWithoutCooldown()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.ResolveDependency<IEntityManager>();
        var minds = entities.System<SharedMindSystem>();
        var roles = entities.System<SharedRoleSystem>();
        var signals = entities.System<SmokeSignalSystem>();
        var timing = server.ResolveDependency<IGameTiming>();

        await server.WaitAssertion(() =>
        {
            EntityUid SpawnWithJob(string job, bool dead = false)
            {
                var body = entities.SpawnEntity(null, map.GridCoords);
                entities.EnsureComponent<MindContainerComponent>(body);
                var mind = minds.CreateMind(null).Owner;
                minds.TransferTo(mind, body);
                roles.MindAddRole(mind, new JobComponent { Prototype = job });
                entities.EnsureComponent<ActorComponent>(body);
                if (dead)
                    entities.EnsureComponent<MobStateComponent>(body).CurrentState = MobState.Dead;
                return body;
            }

            var shaman = SpawnWithJob("TribalShaman");
            var elder = SpawnWithJob("TribalElder");
            var tribal = SpawnWithJob("Tribal");
            var superMutant = SpawnWithJob("SuperMutantTribal");
            var protectron = SpawnWithJob("SyntheticProtectronTribal");
            var outsider = SpawnWithJob("Wastelander");
            var deadTribal = SpawnWithJob("Tribal", dead: true);
            var tree = entities.SpawnEntity("TribalTree", map.GridCoords);
            var component = entities.GetComponent<SmokeSignalComponent>(tree);

            Assert.Multiple(() =>
            {
                Assert.That(signals.CanUse(shaman, component), Is.True);
                Assert.That(signals.CanUse(elder, component), Is.True);
                Assert.That(signals.CanUse(tribal, component), Is.False);
                Assert.That(signals.GetRecipients(component),
                    Is.EquivalentTo(new[] { shaman, elder, tribal, superMutant, protectron }));
            });

            var defaultSignal = entities.GetComponent<SmokeSignalComponent>(
                entities.SpawnEntity("MisfitsTribalSignalFire", map.GridCoords));
            Assert.Multiple(() =>
            {
                // #Misfits Fix - dual-citizenship tribe jobs (SuperMutantTribal, SyntheticProtectronTribal)
                // now count as full tribe members for signal fires.
                Assert.That(signals.CanUse(superMutant, defaultSignal), Is.True);
                Assert.That(signals.CanUse(protectron, defaultSignal), Is.True);
                Assert.That(signals.GetRecipients(defaultSignal),
                    Is.EquivalentTo(new[] { shaman, elder, tribal, superMutant, protectron }));
            });

            entities.EventBus.RaiseLocalEvent(tree, new SmokeSignalSendMessage("   ") { Actor = shaman });
            entities.EventBus.RaiseLocalEvent(tree, new SmokeSignalSendMessage("unauthorized") { Actor = tribal });
            Assert.That(component.CooldownEnd, Is.Null);

            entities.RemoveComponent<ActorComponent>(shaman);
            entities.RemoveComponent<ActorComponent>(elder);
            entities.RemoveComponent<ActorComponent>(tribal);
            entities.RemoveComponent<ActorComponent>(superMutant);
            entities.RemoveComponent<ActorComponent>(protectron);
            entities.RemoveComponent<ActorComponent>(outsider);
            entities.RemoveComponent<ActorComponent>(deadTribal);

            var longMessage = new SmokeSignalSendMessage(new string('x', 129)) { Actor = shaman };
            entities.EventBus.RaiseLocalEvent(tree, longMessage);
            var cooldownEnd = component.CooldownEnd;

            Assert.That(cooldownEnd, Is.EqualTo(timing.CurTime));

            entities.EventBus.RaiseLocalEvent(tree, new SmokeSignalSendMessage("second") { Actor = elder });
            Assert.That(component.CooldownEnd, Is.EqualTo(cooldownEnd));
        });

        await pair.CleanReturnAsync();
    }
}
