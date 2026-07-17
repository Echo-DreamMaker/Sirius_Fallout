using System.Diagnostics.CodeAnalysis;

namespace Content.Shared._Horizon;

public sealed class StarlightEntitySystem : EntitySystem
{
    public bool TryEntity<T>(EntityUid uid, out Entity<T> entity) where T : IComponent
    {
        if (TryComp(uid, out T? comp))
        {
            entity = new Entity<T>(uid, comp);
            return true;
        }

        entity = default!;
        return false;
    }

    public bool TryEntity<T1, T2>(EntityUid uid, out Entity<T1, T2> entity)
        where T1 : IComponent
        where T2 : IComponent
    {
        if (TryComp(uid, out T1? comp1) && TryComp(uid, out T2? comp2))
        {
            entity = new Entity<T1, T2>(uid, comp1, comp2);
            return true;
        }

        entity = default!;
        return false;
    }

    public bool TryEntity<T1, T2, T3>(EntityUid uid, out Entity<T1, T2, T3> entity)
        where T1 : IComponent
        where T2 : IComponent
        where T3 : IComponent
    {
        if (TryComp(uid, out T1? comp1) && TryComp(uid, out T2? comp2) && TryComp(uid, out T3? comp3))
        {
            entity = new Entity<T1, T2, T3>(uid, comp1, comp2, comp3);
            return true;
        }

        entity = default!;
        return false;
    }
}
