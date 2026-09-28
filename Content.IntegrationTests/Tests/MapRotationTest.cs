using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Server.Voting;
using Content.Server.Voting.Managers;
using Content.Shared.CCVar;
using Content.Shared.Voting;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using System.Diagnostics;

namespace Content.IntegrationTests.Tests
{
    /// <summary>
    /// Regression tests for map votes.
    /// <para>
    /// <c>CreateMapVote</c> used to announce the winning map <em>before</em> asking the ticker
    /// whether the map could still be changed, and it discarded the selection silently when
    /// <c>TrySelectMapIfEligible</c> refused it. Players were told a map had won while the round
    /// kept the previous one. The winner is now applied first, and a vote that cannot be applied is
    /// reported instead of being passed off as a win.
    /// </para>
    /// </summary>
    [TestFixture]
    [TestOf(typeof(GameTicker))]
    public sealed class MapRotationTest
    {
        [Test]
        public async Task MapVoteAppliesTheWinningMap()
        {
            await using var pair = await PoolManager.GetServerClient(new PoolSettings
            {
                InLobby = true,
                Connected = true,
                Dirty = true
            });
            var server = pair.Server;

            var ticker = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<GameTicker>();
            var mapManager = server.ResolveDependency<IGameMapManager>();
            var voteManager = server.ResolveDependency<IVoteManager>();
            var session = server.ResolveDependency<IPlayerManager>().Sessions.FirstOrDefault();
            var cfg = server.ResolveDependency<IConfigurationManager>();

            Assert.That(session, Is.Not.Null, "The test needs a connected player to cast a vote.");
            Assert.That(ticker.CanUpdateMap(), Is.True,
                "The map should be changeable while in the pre-round lobby.");

            // Keep the round start far away, and shorten the vote so this does not wait 90 seconds.
            // The autovote is off so the only map vote in play is the one this test creates.
            await server.WaitPost(() =>
            {
                cfg.SetCVar(CCVars.AutoVoteEnabled, false);
                cfg.SetCVar(CCVars.GameLobbyDuration, 600);
                cfg.SetCVar(CCVars.VoteTimerMap, 2);
            });

            await server.WaitPost(() => voteManager.CreateStandardVote(null, StandardVoteType.Map));

            IVoteHandle handle = null!;
            await server.WaitPost(() => handle = voteManager.ActiveVotes.Last());

            var previous = mapManager.GetSelectedMap()?.ID;
            var votedFor = CastVoteForAnyMap(handle, session!, previous);
            Assert.That(votedFor, Is.Not.Null, "The map vote should offer at least one map to pick.");

            await WaitForVoteEnd(
                () => pair.RunTicksSync(5),
                async () =>
                {
                    var any = true;
                    await server.WaitPost(() => any = voteManager.ActiveVotes.Any());
                    return any;
                },
                TimeSpan.FromSeconds(30));
            await server.WaitPost(() => Assert.That(voteManager.ActiveVotes, Is.Empty,
                "The map vote should have finished."));

            await server.WaitPost(() => Assert.That(mapManager.GetSelectedMap()?.ID, Is.EqualTo(votedFor),
                "A map vote that finished in the lobby must apply the map that won it."));

            await pair.CleanReturnAsync();
        }

        [Test]
        public async Task MapVoteTooLateDoesNotChangeTheMap()
        {
            await using var pair = await PoolManager.GetServerClient(new PoolSettings
            {
                InLobby = true,
                Connected = true,
                Dirty = true
            });
            var server = pair.Server;

            var ticker = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<GameTicker>();
            var mapManager = server.ResolveDependency<IGameMapManager>();
            var voteManager = server.ResolveDependency<IVoteManager>();
            var session = server.ResolveDependency<IPlayerManager>().Sessions.FirstOrDefault();
            var cfg = server.ResolveDependency<IConfigurationManager>();

            Assert.That(session, Is.Not.Null, "The test needs a connected player to cast a vote.");

            // A lobby duration shorter than the preload time puts the round start inside the preload
            // window straight away, which is exactly when a map vote can no longer be honoured.
            // The autovote is off so the only map vote in play is the one this test creates.
            await server.WaitPost(() =>
            {
                cfg.SetCVar(CCVars.AutoVoteEnabled, false);
                cfg.SetCVar(CCVars.GameLobbyDuration, 10);
                cfg.SetCVar(CCVars.VoteTimerMap, 1);
            });

            // Restarting the round recomputes the round start time from the new lobby duration.
            await server.WaitPost(() => ticker.RestartRound());

            Assert.That(ticker.CanUpdateMap(), Is.False,
                "The round start should already be inside the preload window.");

            // Let the preload window pick a map, so there is a real selection to protect.
            await pair.RunTicksSync(30);

            string? previous = null;
            await server.WaitPost(() => previous = mapManager.GetSelectedMap()?.ID);
            Assert.That(previous, Is.Not.Null, "The preloaded round should already have a map selected.");

            await server.WaitPost(() => voteManager.CreateStandardVote(null, StandardVoteType.Map));

            IVoteHandle handle = null!;
            await server.WaitPost(() => handle = voteManager.ActiveVotes.Last());

            Assert.That(CastVoteForAnyMap(handle, session!, previous), Is.Not.Null,
                "The map vote should offer a different map to pick.");

            // The vote has to finish before the round actually starts.
            await WaitForVoteEnd(
                () => pair.RunTicksSync(5),
                async () =>
                {
                    var any = true;
                    await server.WaitPost(() => any = voteManager.ActiveVotes.Any());
                    return any;
                },
                TimeSpan.FromSeconds(30));
            await server.WaitPost(() => Assert.That(voteManager.ActiveVotes, Is.Empty,
                "The map vote should have finished."));

            await server.WaitPost(() => Assert.That(mapManager.GetSelectedMap()?.ID, Is.EqualTo(previous),
                "A map vote that can no longer be applied must not change the selected map."));

            await pair.CleanReturnAsync();
        }

        /// <summary>
        /// Votes end on the server's real-time clock rather than on ticks, so a fixed tick count is
        /// not enough to reach the outcome. Poll until the vote is gone.
        /// </summary>
        private static async Task WaitForVoteEnd(
            Func<Task> tick,
            Func<Task<bool>> activeVotes,
            TimeSpan timeout)
        {
            var clock = Stopwatch.StartNew();
            while (clock.Elapsed < timeout)
            {
                if (!await activeVotes())
                    return;

                await Task.Delay(100);
                await tick();
            }
        }

        /// <summary>
        /// Votes for the first offered map, skipping <paramref name="skip"/>, and returns its id.
        /// </summary>
        private static string? CastVoteForAnyMap(IVoteHandle handle, ICommonSession session, string? skip)
        {
            for (var optionId = 0; optionId < 64 && handle.IsValidOption(optionId); optionId++)
            {
                handle.CastVote(session, optionId);

                foreach (var (option, count) in handle.VotesPerOption)
                {
                    if (count == 1 && option is GameMapPrototype proto && proto.ID != skip)
                        return proto.ID;
                }
            }

            return null;
        }
    }
}
