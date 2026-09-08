// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;

namespace Content.Shared._Misfits.Genetics.Abilities;

/// <summary>
/// Action component for use with <see cref="TelepathyActionEvent"/>.
/// PDA messaging but with your mind...
/// </summary>
// no Access restriction: the handling system lives in Content.Server, since delivering a
// message into someone's chat needs the chat manager
[RegisterComponent, NetworkedComponent]
public sealed partial class TelepathyActionComponent : Component
{
    [DataField]
    public int MaxLength = 30; // no essays

    [ViewVariables]
    public EntityUid? Target;

    /// <summary>
    /// Minds this telepath has made contact with in person. Touching a mind by clicking
    /// someone adds them here, and from then on they can be reached from anywhere.
    /// Other telepaths are always reachable and don't need to be in here.
    /// </summary>
    [DataField]
    public HashSet<EntityUid> KnownMinds = new();
}

public sealed partial class TelepathyActionEvent : EntityTargetActionEvent;

/// <summary>
/// Raised when a telepathic message should be put into the target's chat. Handled on the
/// server, since chat delivery needs the chat manager.
/// </summary>
[ByRefEvent]
public record struct TelepathyDeliverEvent(EntityUid User, EntityUid Target, string Message);

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
/// One reachable mind in the far-telepathy window. <see cref="Telepath"/> marks minds that
/// are reachable because they're telepaths themselves rather than because you've met them.
/// </summary>
[Serializable, NetSerializable]
public record struct TelepathyFarEntry(NetEntity Target, string Name, bool Telepath);

/// <summary>
/// State for the far-telepathy window: the minds this telepath can currently reach.
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
