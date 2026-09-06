using System;
using System.Collections.Generic;
using Robust.Shared.Serialization;
using Content.Shared.Mobs;

namespace Content.Shared._Misfits.Overwatch;

[Serializable, NetSerializable]
public enum OverwatchConsoleUiKey : byte
{
    Key,
}

[Serializable, NetSerializable, DataRecord]
public partial struct OverwatchConsoleEntry
{
    public uint Number;
    public string Name;
    public string? JobTitle;
    public string Category;
    public int CategorySortOrder;
    public float Health;
    public MobState State;
    public float X;
    public float Y;

    public OverwatchConsoleEntry(
        uint number,
        string name,
        string? jobTitle,
        string category,
        int categorySortOrder,
        float health,
        MobState state,
        float x,
        float y)
    {
        Number = number;
        Name = name;
        JobTitle = jobTitle;
        Category = category;
        CategorySortOrder = categorySortOrder;
        Health = health;
        State = state;
        X = x;
        Y = y;
    }
}

[Serializable, NetSerializable]
public sealed class OverwatchConsoleState : BoundUserInterfaceState
{
    public readonly string MonitorTitle;

    /// <summary>Display names of the operators currently viewing this console/feed.</summary>
    public readonly List<string> Viewers;

    public readonly List<OverwatchConsoleEntry> Personnel;

    public OverwatchConsoleState(
        string monitorTitle,
        List<string> viewers,
        List<OverwatchConsoleEntry> personnel)
    {
        MonitorTitle = monitorTitle;
        Viewers = viewers;
        Personnel = personnel;
    }
}

[Serializable, NetSerializable]
public sealed class OverwatchConsoleMessage : BoundUserInterfaceMessage
{
    public readonly OverwatchConsoleMessageType Type;
    public readonly uint? TargetNumber;

    public OverwatchConsoleMessage(OverwatchConsoleMessageType type, uint? targetNumber = null)
    {
        Type = type;
        TargetNumber = targetNumber;
    }
}

[Serializable, NetSerializable]
public enum OverwatchConsoleMessageType : byte
{
    Watch,
    Unwatch,
}
