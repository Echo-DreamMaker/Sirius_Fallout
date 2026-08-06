// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;

namespace Content.Shared._Misfits.Genetics.Abilities;

/// <summary>
/// Action component for use with <see cref="TelepathyActionEvent"/>.
/// PDA messaging but with your mind...
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(TelepathyActionSystem))]
public sealed partial class TelepathyActionComponent : Component
{
    [DataField]
    public int MaxLength = 30; // no essays

    [ViewVariables]
    public EntityUid? Target;
}

public sealed partial class TelepathyActionEvent : EntityTargetActionEvent;

[Serializable, NetSerializable]
public enum TelepathyUiKey : byte
{
    Key,
    Far
}

/// <summary>
/// Message sent by the BUI with the chosen text to send to the target.
/// </summary>
[Serializable, NetSerializable]
public sealed class TelepathyChosenMessage(string message) : BoundUserInterfaceMessage
{
    public readonly string Message = message;
}

/// <summary>
/// One reachable mind in the far-telepathy window.
/// </summary>
[Serializable, NetSerializable]
public record struct TelepathyFarEntry(NetEntity Target, string Name);

/// <summary>
/// State for the far-telepathy window: every online player character you can reach.
/// Opened by using the telepathy action on yourself.
/// </summary>
[Serializable, NetSerializable]
public sealed class TelepathyFarState(List<TelepathyFarEntry> players) : BoundUserInterfaceState
{
    public readonly List<TelepathyFarEntry> Players = players;
}

/// <summary>
/// Message sent by the far-telepathy BUI with the chosen target and text.
/// </summary>
[Serializable, NetSerializable]
public sealed class TelepathyFarChosenMessage(NetEntity target, string message) : BoundUserInterfaceMessage
{
    public readonly NetEntity Target = target;
    public readonly string Message = message;
}
