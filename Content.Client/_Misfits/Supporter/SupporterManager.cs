using System.Diagnostics.CodeAnalysis;
using Content.Shared._Misfits.Supporter;
using JetBrains.Annotations;
using Robust.Shared.Network;

namespace Content.Client._Misfits.Supporter;

/// <summary>
///     Client-side cache of each player's Patreon supporter tier, synced from the server via
///     <see cref="MsgSyncSupporterData"/>. Used to gate supporter-exclusive UI (e.g. the Patreon loadout tab).
/// </summary>
[UsedImplicitly]
public sealed class SupporterManager : ISharedSupporterManager
{
    [Dependency] private readonly IClientNetManager _netMgr = default!;
    private readonly Dictionary<NetUserId, SupporterTier> _tiers = new();

    public void Initialize()
    {
        _netMgr.RegisterNetMessage<MsgSyncSupporterData>(OnSupporterDataReceived);
    }

    private void OnSupporterDataReceived(MsgSyncSupporterData message)
    {
        lock (_tiers)
        {
            _tiers[message.UserId] = message.Tier;
        }
    }

    public bool TryGetSupporterTier(NetUserId user, [NotNullWhen(true)] out SupporterTier tier)
    {
        lock (_tiers)
        {
            if (_tiers.TryGetValue(user, out tier))
                return tier != SupporterTier.None;

            tier = SupporterTier.None;
            return false;
        }
    }
}
