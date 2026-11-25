// #Sirius Add: ЕРП-статус должен быть виден и в общем, и в подробном осмотре.
using Content.Client.Examine;
using Content.Client.Lobby;
using Content.Server.DetailExaminable;
using Content.Server.Players.PlayTimeTracking;
using Content.Server.Preferences.Managers;
using Content.Server.Station.Systems;
using Content.Shared._Sirius.Preferences;
using Content.Shared.Examine;
using Content.Shared.Humanoid;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Preferences;
using Robust.Client.State;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Network;

namespace Content.IntegrationTests.Tests._Sirius;

[TestFixture]
public sealed class SiriusEroExamineTest
{
    [Test]
    public async Task EroLineIsVisibleOnExamine()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        NetEntity targetNet = default;

        await server.WaitAssertion(() =>
        {
            var spawning = server.EntMan.System<StationSpawningSystem>();
            var profile = HumanoidCharacterProfile.DefaultWithSpecies()
                .WithName("Ero Examine")
                .WithFlavorText("Описание персонажа")
                .WithSiriusEro(SiriusEroMode.Hard);

            var target = spawning.SpawnPlayerMob(map.GridCoords, null, profile, null);
            targetNet = server.EntMan.GetNetEntity(target);
            var examiner = spawning.SpawnPlayerMob(
                map.GridCoords,
                null,
                HumanoidCharacterProfile.DefaultWithSpecies().WithName("Examiner"),
                null);

            var line = Loc.GetString("sirius-ero-examine-hard");

            Assert.That(server.EntMan.TryGetComponent<DetailExaminableComponent>(target, out var details), Is.True);
            Assert.That(details!.Content, Does.Contain(line));
            Assert.That(details.Content, Does.Contain("Описание персонажа"));

            var examine = server.EntMan.System<ExamineSystemShared>();
            Assert.That(examine.GetExamineText(target, examiner).ToMarkup(), Does.Contain(line));
        });

        await pair.RunTicksSync(10);

        await client.WaitAssertion(() =>
        {
            var clientTarget = client.EntMan.GetEntity(targetNet);
            var appearance = client.EntMan.GetComponent<HumanoidAppearanceComponent>(clientTarget);
            Assert.That(appearance.Ero, Is.EqualTo(SiriusEroMode.Hard));

            var examine = client.EntMan.System<Content.Client.Examine.ExamineSystem>();
            Assert.That(examine.GetExamineText(clientTarget, clientTarget).ToMarkup(),
                Does.Contain(Loc.GetString("sirius-ero-examine-hard")));
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Проверяет полный путь из лобби: клиент сохраняет профиль с ЕРП, сервер не должен
    /// сбрасывать его при сохранении, если общее время игры разблокировало режим.
    /// </summary>
    [Test]
    public async Task EroSurvivesClientProfileSave()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true });
        var server = pair.Server;
        var client = pair.Client;

        await pair.RunTicksSync(1);
        await PoolManager.WaitUntil(client, () => client.ResolveDependency<IStateManager>().CurrentState is LobbyState, 600);

        var clientNet = client.ResolveDependency<IClientNetManager>();
        var clientPrefs = client.ResolveDependency<IClientPreferencesManager>();
        var serverPrefs = server.ResolveDependency<IServerPreferencesManager>();
        var players = server.ResolveDependency<IPlayerManager>();
        var playTime = server.ResolveDependency<PlayTimeTrackingManager>();
        var userId = clientNet.ServerChannel!.UserId;

        await server.WaitAssertion(() =>
        {
            var session = players.GetSessionById(userId);
            playTime.AddTimeToOverallPlaytime(session, TimeSpan.FromMinutes(1300));
            Assert.That(
                playTime.TryGetTrackerTime(session, PlayTimeTrackingShared.TrackerOverall, out var overall),
                Is.True);
            Assert.That(overall, Is.GreaterThanOrEqualTo(SiriusEroModeExtensions.HardUnlock));
        });

        HumanoidCharacterProfile saved = null!;
        await client.WaitAssertion(() =>
        {
            saved = HumanoidCharacterProfile.DefaultWithSpecies()
                .WithName("Saved Ero")
                .WithSiriusEro(SiriusEroMode.Hard);
            clientPrefs.CreateCharacter(saved);
        });

        await PoolManager.WaitUntil(server, () =>
            serverPrefs.GetPreferences(userId).Characters.Values.OfType<HumanoidCharacterProfile>()
                .Any(p => p.Name == "Saved Ero"), 60);

        await server.WaitAssertion(() =>
        {
            var profile = serverPrefs.GetPreferences(userId).Characters.Values
                .OfType<HumanoidCharacterProfile>()
                .Single(p => p.Name == "Saved Ero");
            Assert.That(profile.Ero, Is.EqualTo(SiriusEroMode.Hard),
                "Сервер сбросил ЕРП при сохранении профиля из лобби.");
        });

        await pair.CleanReturnAsync();
    }
}
