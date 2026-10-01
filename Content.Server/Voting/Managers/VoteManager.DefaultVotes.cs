using System.Linq;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Server.Maps;
using Content.Server.RoundEnd;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Content.Shared.Ghost;
using Content.Shared.Voting;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server.Voting.Managers
{
    public sealed partial class VoteManager
    {
        private static readonly Dictionary<StandardVoteType, CVarDef<bool>> _voteTypesToEnableCVars = new()
        {
            {StandardVoteType.Restart, CCVars.VoteRestartEnabled},
            {StandardVoteType.Preset, CCVars.VotePresetEnabled},
            {StandardVoteType.Map, CCVars.VoteMapEnabled},
            // #Misfits Change: Extend vote type
            {StandardVoteType.Extend, CCVars.VoteExtendEnabled},
        };

        public void CreateStandardVote(ICommonSession? initiator, StandardVoteType voteType)
        {
            if (initiator != null)
                _adminLogger.Add(LogType.Vote, LogImpact.Medium, $"{initiator} initiated a {voteType.ToString()} vote");
            else
                _adminLogger.Add(LogType.Vote, LogImpact.Medium, $"Initiated a {voteType.ToString()} vote");

            switch (voteType)
            {
                case StandardVoteType.Restart:
                    CreateRestartVote(initiator);
                    break;
                case StandardVoteType.Preset:
                    CreatePresetVote(initiator);
                    break;
                case StandardVoteType.Map:
                    CreateMapVote(initiator);
                    break;
                // #Misfits Change: Extend vote
                case StandardVoteType.Extend:
                    CreateExtendVote(initiator);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(voteType), voteType, null);
            }
            var ticker = _entityManager.EntitySysManager.GetEntitySystem<GameTicker>();
            ticker.UpdateInfoText();
            TimeoutStandardVote(voteType);
        }

        private void CreateRestartVote(ICommonSession? initiator)
        {

            var playerVoteMaximum = _cfg.GetCVar(CCVars.VoteRestartMaxPlayers);
            var totalPlayers = _playerManager.Sessions.Count(session => session.Status != SessionStatus.Disconnected);

            var ghostVotePercentageRequirement = _cfg.GetCVar(CCVars.VoteRestartGhostPercentage);
            var ghostCount = 0;
            
            foreach (var player in _playerManager.Sessions)
            {
                _playerManager.UpdateState(player);
                if (player.Status != SessionStatus.Disconnected && _entityManager.HasComponent<GhostComponent>(player.AttachedEntity))
                {
                    ghostCount++;
                }
            }

            var ghostPercentage = 0.0;
            if (totalPlayers > 0)
            {
                ghostPercentage = ((double)ghostCount / totalPlayers) * 100;
            }

            var roundedGhostPercentage = (int)Math.Round(ghostPercentage);

            if (totalPlayers <= playerVoteMaximum || roundedGhostPercentage >= ghostVotePercentageRequirement)
            {
                StartVote(initiator);
            }
            else
            {
                NotifyNotEnoughGhostPlayers(ghostVotePercentageRequirement, roundedGhostPercentage);
            }
        }

        private void StartVote(ICommonSession? initiator)
        {
            var alone = _playerManager.PlayerCount == 1 && initiator != null;
                var options = new VoteOptions
                {
                    Title = Loc.GetString("ui-vote-restart-title"),
                    Options =
                    {
                        (Loc.GetString("ui-vote-restart-yes"), "yes"),
                        (Loc.GetString("ui-vote-restart-no"), "no"),
                        (Loc.GetString("ui-vote-restart-abstain"), "abstain")
                    },
                    Duration = alone
                        ? TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.VoteTimerAlone))
                        : TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.VoteTimerRestart)),
                    InitiatorTimeout = TimeSpan.FromMinutes(5)
                };

                if (alone)
                    options.InitiatorTimeout = TimeSpan.FromSeconds(10);

                WirePresetVoteInitiator(options, initiator);

                var vote = CreateVote(options);

                vote.OnFinished += (_, _) =>
                {
                    var votesYes = vote.VotesPerOption["yes"];
                    var votesNo = vote.VotesPerOption["no"];

                    // #Misfits Change: Require a majority of ALL currently connected players,
                    // not just a ratio of those who explicitly voted yes/no.
                    var totalConnected = _playerManager.Sessions.Count(s => s.Status != SessionStatus.Disconnected);
                    var majorityNeeded = (totalConnected / 2) + 1;
                    if (votesYes >= majorityNeeded)
                    {
                        // Check if an admin is online, and ignore the passed vote if the cvar is enabled
                        if (_cfg.GetCVar(CCVars.VoteRestartNotAllowedWhenAdminOnline) && _adminMgr.ActiveAdmins.Count() != 0)
                        {
                            _adminLogger.Add(LogType.Vote, LogImpact.Medium, $"Restart vote attempted to pass, but an admin was online. {votesYes}/{votesNo} (connected: {totalConnected})");
                        }
                        else // If the cvar is disabled or there's no admins on, proceed as normal
                        {
                            _adminLogger.Add(LogType.Vote, LogImpact.Medium, $"Restart vote succeeded: {votesYes}/{totalConnected}");
                            _chatManager.DispatchServerAnnouncement(Loc.GetString("ui-vote-restart-succeeded"));
                            var roundEnd = _entityManager.EntitySysManager.GetEntitySystem<RoundEndSystem>();
                            roundEnd.EndRound();
                        }
                    }
                    else
                    {
                        _adminLogger.Add(LogType.Vote, LogImpact.Medium, $"Restart vote failed: {votesYes}/{totalConnected} (needed {majorityNeeded})");
                        _chatManager.DispatchServerAnnouncement(
                            Loc.GetString("ui-vote-restart-failed-majority", ("yes", votesYes), ("needed", majorityNeeded), ("total", totalConnected)));
                    }
                };

                if (initiator != null)
                {
                    // Cast yes vote if created the vote yourself.
                    vote.CastVote(initiator, 0);
                }

                foreach (var player in _playerManager.Sessions)
                {
                    if (player != initiator)
                    {
                        // Everybody else defaults to an abstain vote to say they don't mind.
                        vote.CastVote(player, 2);
                    }
                }
        }

        private void NotifyNotEnoughGhostPlayers(int ghostPercentageRequirement, int roundedGhostPercentage)
        {
            // Logic to notify that there are not enough ghost players to start a vote
            _adminLogger.Add(LogType.Vote, LogImpact.Medium, $"Restart vote failed: Current Ghost player percentage:{roundedGhostPercentage.ToString()}% does not meet {ghostPercentageRequirement.ToString()}%");
            _chatManager.DispatchServerAnnouncement(
                Loc.GetString("ui-vote-restart-fail-not-enough-ghost-players", ("ghostPlayerRequirement", ghostPercentageRequirement)));
        }

        // #Misfits Change: Vote to extend the current round.
        private void CreateExtendVote(ICommonSession? initiator)
        {
            var alone = _playerManager.PlayerCount == 1 && initiator != null;
            var extensionMinutes = _cfg.GetCVar(CCVars.EmergencyShuttleAutoCallExtensionTime);
            var options = new VoteOptions
            {
                Title = Loc.GetString("ui-vote-extend-title"),
                Options =
                {
                    (Loc.GetString("ui-vote-extend-yes"), "yes"),
                    (Loc.GetString("ui-vote-extend-no"), "no"),
                    (Loc.GetString("ui-vote-extend-abstain"), "abstain")
                },
                Duration = alone
                    ? TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.VoteTimerAlone))
                    : TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.VoteTimerRestart)),
                InitiatorTimeout = TimeSpan.FromMinutes(5)
            };

            if (alone)
                options.InitiatorTimeout = TimeSpan.FromSeconds(10);

            WirePresetVoteInitiator(options, initiator);

            var vote = CreateVote(options);

            vote.OnFinished += (_, _) =>
            {
                var votesYes = vote.VotesPerOption["yes"];
                var totalConnected = _playerManager.Sessions.Count(s => s.Status != SessionStatus.Disconnected);
                var majorityNeeded = (totalConnected / 2) + 1;

                if (votesYes >= majorityNeeded)
                {
                    _adminLogger.Add(LogType.Vote, LogImpact.Medium,
                        $"Extend vote passed: {votesYes}/{totalConnected} — extending round by {extensionMinutes} minutes.");
                    _chatManager.DispatchServerAnnouncement(
                        Loc.GetString("ui-vote-extend-succeeded", ("minutes", extensionMinutes)));
                    var roundEnd = _entityManager.EntitySysManager.GetEntitySystem<RoundEndSystem>();
                    roundEnd.ExtendRound();
                }
                else
                {
                    _adminLogger.Add(LogType.Vote, LogImpact.Medium,
                        $"Extend vote failed: {votesYes}/{totalConnected} (needed {majorityNeeded}).");
                    _chatManager.DispatchServerAnnouncement(
                        Loc.GetString("ui-vote-extend-failed", ("yes", votesYes), ("needed", majorityNeeded), ("total", totalConnected)));
                }
            };

            if (initiator != null)
                vote.CastVote(initiator, 0);

            foreach (var player in _playerManager.Sessions)
            {
                if (player != initiator)
                    vote.CastVote(player, 2);
            }
        }

        private void CreatePresetVote(ICommonSession? initiator)
        {
            var presets = GetGamePresets();

            var alone = _playerManager.PlayerCount == 1 && initiator != null;
            var options = new VoteOptions
            {
                Title = Loc.GetString("ui-vote-gamemode-title"),
                Duration = alone
                    ? TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.VoteTimerAlone))
                    : TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.VoteTimerPreset))
            };

            if (alone)
                options.InitiatorTimeout = TimeSpan.FromSeconds(10);

            foreach (var (k, v) in presets)
            {
                options.Options.Add((Loc.GetString(v), k));
            }

            WirePresetVoteInitiator(options, initiator);

            var vote = CreateVote(options);

            vote.OnFinished += (_, args) =>
            {
                string picked;
                if (args.Winner == null)
                {
                    picked = (string) _random.Pick(args.Winners);
                    _chatManager.DispatchServerAnnouncement(
                        Loc.GetString("ui-vote-gamemode-tie", ("picked", Loc.GetString(presets[picked]))));
                }
                else
                {
                    picked = (string) args.Winner;
                    _chatManager.DispatchServerAnnouncement(
                        Loc.GetString("ui-vote-gamemode-win", ("winner", Loc.GetString(presets[picked]))));
                }
                _adminLogger.Add(LogType.Vote, LogImpact.Medium, $"Preset vote finished: {picked}");
                var ticker = _entityManager.EntitySysManager.GetEntitySystem<GameTicker>();
                ticker.SetGamePreset(picked);
            };
        }

        private void CreateMapVote(ICommonSession? initiator)
        {
            var maps = _gameMapManager.CurrentlyEligibleMaps().ToDictionary(map => map, map => map.MapName);

            var alone = _playerManager.PlayerCount == 1 && initiator != null;
            var options = new VoteOptions
            {
                Title = Loc.GetString("ui-vote-map-title"),
                Duration = alone
                    ? TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.VoteTimerAlone))
                    : TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.VoteTimerMap))
            };

            if (alone)
                options.InitiatorTimeout = TimeSpan.FromSeconds(10);

            foreach (var (k, v) in maps)
            {
                options.Options.Add((v, k));
            }

            WirePresetVoteInitiator(options, initiator);

            var vote = CreateVote(options);

            vote.OnFinished += (_, args) =>
            {
                GameMapPrototype picked;
                if (args.Winner == null)
                {
                    picked = (GameMapPrototype) _random.Pick(args.Winners);
                }
                else
                {
                    picked = (GameMapPrototype) args.Winner;
                }

                _adminLogger.Add(LogType.Vote, LogImpact.Medium, $"Map vote finished: {picked.MapName}");
                var ticker = _entityManager.EntitySysManager.GetEntitySystem<GameTicker>();

                // #Misfits Fix: Apply the vote *before* announcing the winner. The old code announced
                // the winner first, so a vote that the ticker then refused to apply (too late in the
                // lobby, or the map stopped being eligible) still looked like it had worked, while the
                // round silently kept the previous map. Players were told a map won and got another one.
                if (!ticker.CanUpdateMap())
                {
                    _log.Warning($"Map vote for {picked.ID} was ignored: round start is too close.");
                    if (ticker.RoundPreloadTime <= TimeSpan.Zero)
                    {
                        _chatManager.DispatchServerAnnouncement(Loc.GetString("ui-vote-map-notlobby"));
                    }
                    else
                    {
                        var timeString = $"{ticker.RoundPreloadTime.Minutes:0}:{ticker.RoundPreloadTime.Seconds:00}";
                        _chatManager.DispatchServerAnnouncement(Loc.GetString("ui-vote-map-notlobby-time", ("time", timeString)));
                    }

                    return;
                }

                if (!_gameMapManager.TrySelectMapIfEligible(picked.ID))
                {
                    // #Misfits Fix: Don't swallow this. Previously the winner was announced and the
                    // selection silently dropped, so the round loaded a map nobody voted for.
                    _log.Warning($"Map vote for {picked.ID} was ignored: the map is no longer eligible.");
                    _adminLogger.Add(LogType.Vote, LogImpact.Medium,
                        $"Map vote for {picked.ID} discarded: map is no longer eligible.");
                    _chatManager.DispatchServerAnnouncement(
                        Loc.GetString("ui-vote-map-ineligible", ("winner", maps[picked])));
                    return;
                }

                if (args.Winner == null)
                {
                    _chatManager.DispatchServerAnnouncement(
                        Loc.GetString("ui-vote-map-tie", ("picked", maps[picked])));
                }
                else
                {
                    _chatManager.DispatchServerAnnouncement(
                        Loc.GetString("ui-vote-map-win", ("winner", maps[picked])));
                }

                // #Misfits Fix: SendStatusToAll also refreshes the map name/author shown in the lobby
                // credit. UpdateInfoText alone leaves that display pointing at the previous map.
                ticker.UpdateInfoText();
                ticker.SendStatusToAll();
            };
        }

        private void TimeoutStandardVote(StandardVoteType type)
        {
            var timeout = TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.VoteSameTypeTimeout));
            _standardVoteTimeout[type] = _timing.RealTime + timeout;
            DirtyCanCallVoteAll();
        }

        private Dictionary<string, string> GetGamePresets()
        {
            var presets = new Dictionary<string, string>();

            foreach (var preset in _prototypeManager.EnumeratePrototypes<GamePresetPrototype>())
            {
                if(!preset.ShowInVote)
                    continue;

                if(_playerManager.PlayerCount < (preset.MinPlayers ?? int.MinValue))
                    continue;

                if(_playerManager.PlayerCount > (preset.MaxPlayers ?? int.MaxValue))
                    continue;

                presets[preset.ID] = preset.ModeTitle;
            }
            return presets;
        }
    }
}
