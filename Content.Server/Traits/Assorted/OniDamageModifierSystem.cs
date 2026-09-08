using Content.Shared._N14.SuperMutant;
using Content.Shared.Damage;

namespace Content.Server.Traits.Assorted;

public sealed class OniDamageModifierSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OniDamageModifierComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(EntityUid uid, OniDamageModifierComponent component, ComponentStartup args)
    {
        if (!TryComp<SuperMutantComponent>(uid, out var sm))
            return;

        foreach (var (key, value) in component.MeleeModifierReplacers.Coefficients)
        {
            sm.MeleeModifiers.Coefficients[key] = value;

        }

        foreach (var (key, value) in component.MeleeModifierReplacers.FlatReduction)
        {
            sm.MeleeModifiers.FlatReduction[key] = value;

        }
    }
}
