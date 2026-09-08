using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.IntegrationTests.Pair;
using Robust.UnitTesting;
using Robust.Shared.Player;
using Robust.Shared.Input;
using Robust.Client.Input;
using Robust.Shared.Map;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;

namespace Content.IntegrationTests.Tests._Misfits.GunSystem;

[SetUpFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public abstract partial class BallisticAmmoProviderSetUp
{
    public const string BasicAmmoCart = "N14Cartridge308Rifle";
    public const string BasicAmmoUseCaseProto = "BasicAmmo";
    public const string BasicAmmoUseCaseEmptyProto = "BasicAmmoEmpty";
    public const string PlayerPrototype = "InteractionTestMob";
    [TestPrototypes]
    public const string Prototypes = $@"
- type: entity
  parent: BaseItem
  id: {BasicAmmoUseCaseProto}
  components:
  - type: BallisticAmmoProvider
    mayTransfer: true
    whitelist:
      tags:
        - N14Cartridge308Rifle
    proto: N14Cartridge308Rifle
    capacity: 21
  - type: ContainerContainer
    containers:
      ballistic-ammo: !type:Container
  - type: Sprite
    sprite: _Nuclear14/Objects/Weapons/Guns/Ammunition/Boxes/308.rsi
    netsync: false
  - type: MagazineVisuals
    magState: mag
    steps: 2
    zeroVisible: false
  - type: Appearance
  - type: FoldableAmmoBox
- type: entity
  parent: BaseItem
  id: {BasicAmmoUseCaseEmptyProto}
  components:
  - type: BallisticAmmoProvider
    mayTransfer: true
    whitelist:
      tags:
        - N14Cartridge308Rifle
    capacity: 21
  - type: ContainerContainer
    containers:
      ballistic-ammo: !type:Container
  - type: Sprite
    sprite: _Nuclear14/Objects/Weapons/Guns/Ammunition/Boxes/308.rsi
    netsync: false
  - type: MagazineVisuals
    magState: mag
    steps: 2
    zeroVisible: false
  - type: Appearance
  - type: FoldableAmmoBox
";

    // Server
    public IEntityManager sEntMan = default!;
    public IPrototypeManager sProtoMan = default!;
    public IEntitySystemManager sEntSysMan = default!;
    public IGameTiming sTime = default!;
    //
    public EntityLookupSystem sLookup = default!;
    public SharedGunSystem sGunSysShared = default!;
    public SharedMapSystem sMapShared = default!;
    //public SharedHandsSystem sHandSys = default!;
    public TestMapData testMap = default!;
    // client
    public IEntityManager cEntMan = default!;
    public IPrototypeManager cProtoMan = default!;
    public IEntitySystemManager cEntSysMan = default!;
    public IGameTiming cTime = default;
    public IInputManager cInput = default;
    //
    Robust.Client.GameObjects.InputSystem cInputSys = default;
    public SharedGunSystem cGunSysShared = default!;
    public SharedMapSystem cMapShared = default!;

    public NetEntity player = default;
    public ICommonSession cSession = default;
    public ICommonSession sSession = default;
    protected TestPair pair = default!;
    public RobustIntegrationTest.ServerIntegrationInstance server => pair.Server;
    public RobustIntegrationTest.ClientIntegrationInstance client => pair.Client;

    [SetUp]
    public async Task SetUp()
    {
        pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });

        // server
        sEntMan = server.ResolveDependency<IEntityManager>();
        sProtoMan = server.ResolveDependency<IPrototypeManager>();
        sEntSysMan = server.ResolveDependency<IEntitySystemManager>();
        sTime = server.ResolveDependency<IGameTiming>();
        //
        sLookup = sEntMan.System<EntityLookupSystem>();
        sGunSysShared = sEntMan.System<SharedGunSystem>();
        sMapShared = sEntMan.System<SharedMapSystem>();
        //sHandSys = sEntMan.System<SharedHandsSystem>();
        // client
        cEntMan = client.ResolveDependency<IEntityManager>();
        cProtoMan = client.ResolveDependency<IPrototypeManager>();
        cEntSysMan = client.ResolveDependency<IEntitySystemManager>();
        cTime = client.ResolveDependency<IGameTiming>();
        cInput = client.ResolveDependency<IInputManager>();
        //
        cInputSys = cEntMan.System<Robust.Client.GameObjects.InputSystem>();
        // Get player data
        //var sPlayerMan = server.ResolveDependency<Robust.Server.Player.IPlayerManager>();
        //var cPlayerMan = client.ResolveDependency<Robust.Client.Player.IPlayerManager>();
        testMap = await pair.CreateTestMap();
        if (client.Session == null)
            Assert.Fail("No player");

        cSession = client.Session;
        sSession = server.PlayerMan.GetSessionById(cSession.UserId);

        // Spawn player entity & attach

        await server.WaitPost(() =>
        {

            var SPlayer = sEntMan.SpawnEntity(PlayerPrototype, testMap.GridCoords);
            server.PlayerMan.SetAttachedEntity(sSession, SPlayer);
            player = sEntMan.GetNetEntity(SPlayer);
            //server.PlayerMan.JoinGame(sSession);
        });

        await server.WaitPost(() =>
                {
                    var bodySystem = sEntMan.System<SharedBodySystem>();
                    var hands = bodySystem.GetBodyChildrenOfType(sEntMan.GetEntity(player), BodyPartType.Hand).ToArray();

                    for (var i = 1; i < hands.Length; i++)
                    {
                        sEntMan.DeleteEntity(hands[i].Id);
                    }
                });
        await pair.SyncTicks();
        await pair.RunTicksSync(20);

        Assert.Multiple(() =>
        {
            Assert.That(cEntMan.GetNetEntity(client.PlayerMan.LocalEntity), Is.EqualTo(player));
            Assert.That(server.PlayerMan.GetSessionById(cSession.UserId).AttachedEntity, Is.EqualTo(sEntMan.GetEntity(player)));
        });

        await pair.SyncTicks();

    }

    [TearDown]
    public async Task TearDown()
    {
        await server.WaitPost(() => sMapShared.DeleteMap(testMap.MapId));
        await pair.CleanReturnAsync();

    }

    public async Task Interact(BoundKeyFunction key, BoundKeyState state, EntityCoordinates coords, EntityUid uid)
    {

        var funcId = cInput.NetworkBindMap.KeyFunctionID(key);
        var message = new ClientFullInputCmdMessage(cTime.CurTick, cTime.TickFraction, funcId)
        {
            State = state,
            Coordinates = coords,
            ScreenCoordinates = default,
            Uid = uid,
        };

        await client.WaitPost(() => cInputSys.HandleInputCommand(cSession, key, message));
    }

}
