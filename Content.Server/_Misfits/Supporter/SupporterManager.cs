using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared._Misfits.Supporter;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._Misfits.Supporter;

public interface ISupporterManager
{
    void Initialize();
    bool TryGetSupporter(NetUserId userId, [NotNullWhen(true)] out SupporterEntry? data);
    Task SetSupporterAsync(Guid userId, string username, string? title, string? nameColor, SupporterTier tier = SupporterTier.None);
    Task RemoveSupporterAsync(Guid userId);
    Task WaitLoadedAsync();
    IReadOnlyList<SupporterEntry> GetAll();
}

public sealed class SupporterManager : ISupporterManager, ISharedSupporterManager
{
    [Dependency] private readonly IServerDbManager _db = default!;
    // #Cythisiax Added - used to push the current player's Patreon tier to the client
    [Dependency] private readonly IServerNetManager _net = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private readonly Dictionary<Guid, SupporterEntry> _cache = new();
    private readonly SemaphoreSlim _writeSemaphore = new(1, 1);
    private ISawmill _sawmill = default!;
    private Task _loadTask = Task.CompletedTask;

    public void Initialize()
    {
        IoCManager.InjectDependencies(this);
        _sawmill = Logger.GetSawmill("supporter");
        // #Cythisiax Added - net message so the client can gate supporter-exclusive features
        _net.RegisterNetMessage<MsgSyncSupporterData>();
        _player.PlayerStatusChanged += OnPlayerStatusChanged;
        _loadTask = Task.Run(LoadAsync);
    }

    // #Cythisiax Added - push tier to clients as soon as a player connects
    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus != SessionStatus.Connected)
            return;

        SendSupporterData(args.Session.UserId);
    }

    private async Task LoadAsync()
    {
        try
        {
            var rows = await _db.GetAllSupportersAsync();
            lock (_cache)
            {
                foreach (var row in rows)
                    _cache[row.UserId] = new SupporterEntry(row.UserId, row.Username, row.Title, row.NameColor, (SupporterTier) row.Tier);
            }
            _sawmill.Info($"Loaded {_cache.Count} supporter(s) from database.");

            // #Cythisiax Added - refresh connected clients once the DB cache is ready
            foreach (var session in _player.Sessions)
                SendSupporterData(session.UserId);
        }
        catch (Exception ex)
        {
            _sawmill.Error($"Failed to load supporters from database: {ex}");
        }
    }

    public bool TryGetSupporter(NetUserId userId, [NotNullWhen(true)] out SupporterEntry? data)
    {
        lock (_cache)
            return _cache.TryGetValue(userId.UserId, out data);
    }

    // #Cythisiax Added - shared interface used by client/server to gate Patreon content
    public bool TryGetSupporterTier(NetUserId userId, [NotNullWhen(true)] out SupporterTier tier)
    {
        lock (_cache)
        {
            if (_cache.TryGetValue(userId.UserId, out var data))
            {
                tier = data.Tier;
                return tier != SupporterTier.None;
            }

            tier = SupporterTier.None;
            return false;
        }
    }

    public async Task SetSupporterAsync(Guid userId, string username, string? title, string? nameColor, SupporterTier tier = SupporterTier.None)
    {
        await _writeSemaphore.WaitAsync();
        try
        {
            await _db.UpsertSupporterAsync(userId, username, title, nameColor, (int) tier);
            lock (_cache)
                _cache[userId] = new SupporterEntry(userId, username, title, nameColor, tier);
            // #Cythisiax Added - keep the affected client's tier in sync
            SendSupporterData(new NetUserId(userId));
        }
        finally
        {
            _writeSemaphore.Release();
        }
    }

    public async Task RemoveSupporterAsync(Guid userId)
    {
        await _writeSemaphore.WaitAsync();
        try
        {
            await _db.RemoveSupporterAsync(userId);
            lock (_cache)
                _cache.Remove(userId);
            // #Cythisiax Added - clear the affected client's tier
            SendSupporterData(new NetUserId(userId));
        }
        finally
        {
            _writeSemaphore.Release();
        }
    }

    // #Cythisiax Added - sends the current supporter tier (or None) to a connected client
    private void SendSupporterData(NetUserId userId)
    {
        if (!_player.TryGetSessionById(userId, out var session))
            return;

        var tier = TryGetSupporterTier(userId, out var data) ? data : SupporterTier.None;
        _net.ServerSendMessage(new MsgSyncSupporterData
        {
            UserId = userId,
            Tier = tier,
        }, session.Channel);
    }

    public Task WaitLoadedAsync()
    {
        return _loadTask;
    }

    public IReadOnlyList<SupporterEntry> GetAll()
    {
        lock (_cache)
            return _cache.Values.ToList();
    }
}
