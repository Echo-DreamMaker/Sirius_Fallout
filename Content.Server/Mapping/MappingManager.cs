using System.IO;
using System.Linq;
using Content.Server.Administration.Managers;
using Content.Shared.Administration;
using Content.Shared.Mapping;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Utility;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Content.Server.Mapping;

public sealed class MappingManager : IPostInjectInit
{
    [Dependency] private readonly IAdminManager _admin = default!;
    [Dependency] private readonly ILogManager _log = default!;
    [Dependency] private readonly IServerNetManager _net = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IEntitySystemManager _systems = default!;
    // Sirius edit start
    [Dependency] private readonly ISerializationManager _serialization = default!;
    [Dependency] private readonly IResourceManager _resourceMan = default!;
    [Dependency] private readonly IEntityManager _ent = default!;
    // Sirius edit end

    private ISawmill _sawmill = default!;
    private ZStdCompressionContext _zstd = default!;

    // Sirius edit start
    private const string FavoritesPath = "/mapping_editor_favorites.yml";
    // Sirius edit end

    public void PostInject()
    {
        // Sirius edit start
        _net.RegisterNetMessage<MappingFavoritesSaveMessage>(OnMappingFavoritesSave);
        _net.RegisterNetMessage<MappingFavoritesLoadMessage>(OnMappingFavoritesLoad);
        _net.RegisterNetMessage<MappingFavoritesDataMessage>();
        // Sirius edit end

        _sawmill = _log.GetSawmill("mapping");

#if !FULL_RELEASE
        _net.RegisterNetMessage<MappingSaveMapMessage>(OnMappingSaveMap);
        _net.RegisterNetMessage<MappingSaveMapErrorMessage>();
        _net.RegisterNetMessage<MappingMapDataMessage>();

        _zstd = new ZStdCompressionContext();
#endif
    }

    private void OnMappingSaveMap(MappingSaveMapMessage message)
    {
#if !FULL_RELEASE
        try
        {
            // Sirius edit start
            if (!_players.TryGetSessionByChannel(message.MsgChannel, out var session) ||
                !_admin.IsAdmin(session, true) ||
                !_admin.HasAdminFlag(session, AdminFlags.Host) ||
                !_ent.TryGetComponent(session.AttachedEntity, out TransformComponent? xform) ||
                xform.MapUid is not { } mapUid)
            {
                return;
            }

            var mapId = _systems.GetEntitySystem<TransformSystem>().GetMapCoordinates(session.AttachedEntity.Value).MapId;
            var mapEntity = _systems.GetEntitySystem<SharedMapSystem>().GetMap(mapId);
            var (data, _) = _systems.GetEntitySystem<MapLoaderSystem>().SerializeEntitiesRecursive(new HashSet<EntityUid> { mapEntity });
            var document = new YamlDocument(data.ToYaml());
            var stream = new YamlStream { document };
            var writer = new StringWriter();
            stream.Save(new YamlMappingFix(new Emitter(writer)), false);

            // Sirius edit start
            var msg = new MappingMapDataMessage
            {
                Context = _zstd,
                Yml = writer.ToString()
            };
            // Sirius edit end
            _net.ServerSendMessage(msg, message.MsgChannel);
        }
        catch (Exception e)
        {
            _sawmill.Error($"Error saving map in mapping mode:\n{e}");
            var msg = new MappingSaveMapErrorMessage();
            _net.ServerSendMessage(msg, message.MsgChannel);
        }
#endif
    }

    // Sirius edit start
    private void OnMappingFavoritesSave(MappingFavoritesSaveMessage message)
    {
        var mapping = new MappingDataNode();
        mapping.Add("prototypes", _serialization.WriteValue(message.PrototypeIDs, notNullableOverride: true));

        var path = new ResPath(FavoritesPath);
        using var writer = _resourceMan.UserData.OpenWriteText(path);
        var stream = new YamlStream { new(mapping.ToYaml()) };
        stream.Save(new YamlMappingFix(new Emitter(writer)), false);
    }

    private void OnMappingFavoritesLoad(MappingFavoritesLoadMessage message)
    {
        var path = new ResPath(FavoritesPath);

        if (!_resourceMan.UserData.Exists(path))
            return;

        try
        {
            var reader = _resourceMan.UserData.OpenText(path);
            var documents = DataNodeParser.ParseYamlStream(reader).First();
            var mapping = (MappingDataNode) documents.Root;

            if (!mapping.TryGet("prototypes", out var prototypesNode))
                return;

            var ids = _serialization.Read<string[]>(prototypesNode, notNullableOverride: true).ToList();

            var msg = new MappingFavoritesDataMessage()
            {
                PrototypeIDs = ids,
            };
            _net.ServerSendMessage(msg, message.MsgChannel);
        }
        catch (Exception e)
        {
            _sawmill.Error("Failed to load user favorite objects: " + e);
        }
    }
    // Sirius edit end
}
