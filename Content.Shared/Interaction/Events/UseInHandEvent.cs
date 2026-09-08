using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Timing;
using JetBrains.Annotations;

namespace Content.Shared.Interaction.Events;

/// <summary>
///     Raised when using the entity in your hands.
/// </summary>
[PublicAPI]
public sealed class UseInHandEvent : HandledEntityEventArgs
{
    /// <summary>
    ///     Entity holding the item in their hand.
    /// </summary>
    public EntityUid User;

    /// <summary>
    ///     Whether or not to apply a UseDelay when used.
    ///     Mostly used by the <see cref="ClothingSystem"/> quick-equip to not apply the delay to entities that have the <see cref="UseDelayComponent"/>.
    /// </summary>
    public bool ApplyDelay = true;
    public UseInHandEvent(EntityUid user)
    {
        User = user;
    }
}

/// Misfit addition: expandability in future
/// <summary>
///  raised before UseInHandEvent.
///  Used to add features onto existing UseInHand events
///  that can happen before or completely overrite (handle set to true)
///  the given UseInHandEvent
/// </summary>
[PublicAPI]
public sealed class BeforeUseInHandEvent : HandledEntityEventArgs
{
    /// <summary>
    ///     Entity holding the item in their hand.
    /// </summary>
    public EntityUid User;

    /// <summary>
    ///     Whether or not to apply a UseDelay when used.
    ///     Mostly used by the <see cref="ClothingSystem"/> quick-equip to not apply the delay to entities that have the <see cref="UseDelayComponent"/>.
    /// </summary>
    public bool ApplyDelay = true;

    public BeforeUseInHandEvent(EntityUid user)
    {
        User = user;
    }


}


/// Misfit addition: expandability in future
/// <summary>
///  raised after UseInHandEvent.
///  Used to add features onto existing UseInHand events
///  that can happen after
///  the given UseInHandEvent
/// </summary>
[PublicAPI]
public sealed class AfterUseInHandEvent : HandledEntityEventArgs
{
    /// <summary>
    ///     Entity holding the item in their hand.
    /// </summary>
    public EntityUid User;

    /// <summary>
    ///     Whether or not to apply a UseDelay when used.
    ///     Mostly used by the <see cref="ClothingSystem"/> quick-equip to not apply the delay to entities that have the <see cref="UseDelayComponent"/>.
    /// </summary>
    public bool ApplyDelay = true;

    public AfterUseInHandEvent(EntityUid user)
    {
        User = user;
    }


}
