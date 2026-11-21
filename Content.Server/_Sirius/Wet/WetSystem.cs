using Content.Server.Atmos.Components;
using Content.Shared._Sirius.Wet;
using Content.Shared.Atmos;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;

namespace Content.Server._Sirius.Wet;

/// <summary>
/// #Sirius Add: gameplay effects of being wet. The status effect itself is defined in
/// Resources/Prototypes/_Sirius/Dirtiness, this system only supplies the mechanics.
/// </summary>
public sealed class WetSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WetComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<WetComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<WetComponent, GetFireProtectionEvent>(OnGetFireProtection);
        SubscribeLocalEvent<WetComponent, BeforeDamageChangedEvent>(OnBeforeDamage);
    }

    private void OnStartup(Entity<WetComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<FlammableComponent>(ent, out var flammable))
            return;

        ent.Comp.SavedFireStackIncreaseMultiplier = flammable.FireStackIncreaseMultiplier;
        ent.Comp.SavedFireStackMultiplier = true;
        // #Sirius Add: 50% protection from ignition - twice as many fire stacks are needed to catch fire.
        flammable.FireStackIncreaseMultiplier *= 0.5f;
    }

    private void OnShutdown(Entity<WetComponent> ent, ref ComponentShutdown args)
    {
        if (!ent.Comp.SavedFireStackMultiplier || !TryComp<FlammableComponent>(ent, out var flammable))
            return;

        flammable.FireStackIncreaseMultiplier = ent.Comp.SavedFireStackIncreaseMultiplier;
        ent.Comp.SavedFireStackMultiplier = false;
    }

    private void OnGetFireProtection(Entity<WetComponent> ent, ref GetFireProtectionEvent args)
    {
        // #Sirius Add: 50% protection from fire damage.
        args.Reduce(0.5f);
    }

    /// <summary>
    /// #Sirius Add: BeforeDamageChangedEvent is raised even when resistances are ignored
    /// (environmental heat/cold damage), unlike DamageModifyEvent.
    /// </summary>
    private void OnBeforeDamage(Entity<WetComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (!args.Damage.AnyPositive())
            return;

        var damage = new DamageSpecifier(args.Damage);
        var changed = false;
        changed |= Scale(damage, "Heat", 0.8f);
        changed |= Scale(damage, "Cold", 1.2f);
        changed |= Scale(damage, "Shock", 2.0f); // "Electrical" is called Shock in this fork.

        if (changed)
            args.Damage = damage;
    }

    private static bool Scale(DamageSpecifier damage, string type, float multiplier)
    {
        if (!damage.DamageDict.TryGetValue(type, out var value) || value <= FixedPoint2.Zero)
            return false;

        damage.DamageDict[type] = value * multiplier;
        return true;
    }
}
