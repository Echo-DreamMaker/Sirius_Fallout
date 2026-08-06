using Content.Shared.Wieldable;


namespace Content.Shared._Misfits.Wielding;

/// <summary>
/// More functional replacement for <see cref="ItemWieldedEvent"/>,
/// which at time of writing had zero uses in this codebase.
///
/// This was made for <see cref="SharedGrantActionOnWieldSystem"/>,
/// but could be used in other places easily.
/// </summary>
public sealed class MisfitsItemWieldedEvent : EntityEventArgs
{
    public EntityUid User;

    public MisfitsItemWieldedEvent(EntityUid user)
    {
        User = user;
    }
}
