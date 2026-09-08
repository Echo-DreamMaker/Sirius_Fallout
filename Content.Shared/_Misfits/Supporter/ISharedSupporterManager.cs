using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Network;

namespace Content.Shared._Misfits.Supporter;

/// <summary>
///     Client/server common interface to query a player's Patreon supporter tier.
///     Implementations are registered in the client and server IoC containers.
/// </summary>
public interface ISharedSupporterManager
{
    void Initialize();
    bool TryGetSupporterTier(NetUserId user, [NotNullWhen(true)] out SupporterTier tier);
}
