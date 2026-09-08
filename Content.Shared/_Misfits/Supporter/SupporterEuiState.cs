using Robust.Shared.Serialization;
using Content.Shared.Eui;

namespace Content.Shared._Misfits.Supporter;

[Serializable, NetSerializable]
public sealed class SupporterEntry
{
    public Guid UserId;
    public string Username = string.Empty;
    public string? Title;
    public string? NameColor;
    // #Cythisiax Added - Patreon supporter tier, gates supporter-exclusive features (e.g. Patreon loadout tab).
    public SupporterTier Tier;

    public SupporterEntry() { }

    // #Cythisiax Edited - Added Tier parameter
    public SupporterEntry(Guid userId, string username, string? title, string? nameColor, SupporterTier tier = SupporterTier.None)
    {
        UserId = userId;
        Username = username;
        Title = title;
        NameColor = nameColor;
        Tier = tier;
    }
}

[Serializable, NetSerializable]
public sealed class SupporterManagerState : EuiStateBase
{
    public readonly List<SupporterEntry> Supporters;
    public readonly string? StatusMessage;

    public SupporterManagerState(List<SupporterEntry> supporters, string? statusMessage = null)
    {
        Supporters = supporters;
        StatusMessage = statusMessage;
    }
}

/// <summary>
/// Set or update a supporter. If UserId is provided it is used directly; otherwise the server
/// resolves the GUID from Username.
/// </summary>
[Serializable, NetSerializable]
public sealed class SupporterSetMessage : EuiMessageBase
{
    public Guid? UserId;
    public string Username = string.Empty;
    public string? Title;
    public string? NameColor;
    // #Cythisiax Added - Patreon tier to assign (nullable so existing admin clients can update without touching tier).
    public SupporterTier? Tier;
}

[Serializable, NetSerializable]
public sealed class SupporterRemoveMessage : EuiMessageBase
{
    public Guid UserId;
}
