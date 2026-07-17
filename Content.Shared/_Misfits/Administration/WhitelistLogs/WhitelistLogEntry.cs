using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.Administration.WhitelistLogs;

[Serializable, NetSerializable]
public sealed class WhitelistLogEntry
{
    public string Action;
    public string AdminName;
    public string TargetName;
    public string Roles;
    public string Reason;
    public string DiscordUsername;
    public string ApplicationText;
    public TimeSpan Time;

    public WhitelistLogEntry(
        string action,
        string adminName,
        string targetName,
        string roles,
        string reason,
        string discordUsername,
        string applicationText,
        TimeSpan time)
    {
        Action = action;
        AdminName = adminName;
        TargetName = targetName;
        Roles = roles;
        Reason = reason;
        DiscordUsername = discordUsername;
        ApplicationText = applicationText;
        Time = time;
    }
}
