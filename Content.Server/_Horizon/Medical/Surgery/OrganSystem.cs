using Content.Server.Humanoid;
using Content.Shared._Horizon.Medical.Limbs;
using Content.Shared._Horizon.Medical.Surgery.Components;
using Content.Shared._Horizon.Medical.Surgery.Events;
using Content.Shared.Damage;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Speech.Muting;

namespace Content.Server._Horizon.Medical.Surgery;

public sealed class OrganSystem : EntitySystem
{
    [Dependency] private readonly BlindableSystem _blindable = null!;
    [Dependency] private readonly DamageableSystem _damageableSystem = null!;
    [Dependency] private readonly HumanoidAppearanceSystem _humanoidAppearanceSystem = null!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FunctionalOrganComponent, SurgeryOrganImplantationCompleted>(OnFunctionalOrganImplanted);
        SubscribeLocalEvent<FunctionalOrganComponent, SurgeryOrganExtracted>(OnFunctionalOrganExtracted);

        SubscribeLocalEvent<OrganEyesComponent, SurgeryOrganImplantationCompleted>(OnEyeImplanted);
        SubscribeLocalEvent<OrganEyesComponent, SurgeryOrganExtracted>(OnEyeExtracted);

        SubscribeLocalEvent<OrganTongueComponent, SurgeryOrganImplantationCompleted>(OnTongueImplanted);
        SubscribeLocalEvent<OrganTongueComponent, SurgeryOrganExtracted>(OnTongueExtracted);

        SubscribeLocalEvent<DamageableComponent, SurgeryOrganImplantationCompleted>(OnOrganImplanted);
        SubscribeLocalEvent<DamageableComponent, SurgeryOrganExtracted>(OnOrganExtracted);

        SubscribeLocalEvent<OrganVisualizationComponent, SurgeryOrganImplantationCompleted>(OnVisualizationImplanted);
        SubscribeLocalEvent<OrganVisualizationComponent, SurgeryOrganExtracted>(OnVisualizationExtracted);
    }

    private void OnFunctionalOrganImplanted(Entity<FunctionalOrganComponent> ent, ref SurgeryOrganImplantationCompleted args)
    {
        if (ent.Comp.OnAddComponents is not null)
            EntityManager.AddComponents(args.Body, ent.Comp.OnAddComponents, false);
    }

    private void OnFunctionalOrganExtracted(Entity<FunctionalOrganComponent> ent, ref SurgeryOrganExtracted args)
    {
        if (ent.Comp.OnAddComponents is not null)
            EntityManager.RemoveComponents(args.Body, ent.Comp.OnAddComponents);
    }

    private void OnOrganImplanted(Entity<DamageableComponent> ent, ref SurgeryOrganImplantationCompleted args)
    {
        if (!TryComp<OrganDamageComponent>(ent.Owner, out var damageRule)
            || damageRule.InsertDamage is null
            || !TryComp<DamageableComponent>(args.Body, out var bodyDamageable))
            return;

        _damageableSystem.TryChangeDamage(args.Body, damageRule.InsertDamage, true, false, bodyDamageable);
    }

    private void OnOrganExtracted(Entity<DamageableComponent> ent, ref SurgeryOrganExtracted args)
    {
        if (!TryComp<OrganDamageComponent>(ent.Owner, out var damageRule)
         || damageRule.RemoveDamage is null
         || !TryComp<DamageableComponent>(args.Body, out var bodyDamageable))
            return;

        _damageableSystem.TryChangeDamage(args.Body, damageRule.RemoveDamage, true, false, bodyDamageable);
    }

    private void OnTongueImplanted(Entity<OrganTongueComponent> ent, ref SurgeryOrganImplantationCompleted args)
    {
        if (!ent.Comp.IsMuted)
            return;

        ent.Comp.IsMuted = false;
        RemComp<MutedComponent>(args.Body);
    }

    private void OnTongueExtracted(Entity<OrganTongueComponent> ent, ref SurgeryOrganExtracted args)
    {
        ent.Comp.IsMuted = true;
        if (HasComp<MutedComponent>(args.Body))
            return;

        AddComp<MutedComponent>(args.Body);
    }

    private void OnEyeExtracted(Entity<OrganEyesComponent> ent, ref SurgeryOrganExtracted args)
    {
        if (!TryComp<BlindableComponent>(args.Body, out var blindable))
            return;

        ent.Comp.EyeDamage = blindable.EyeDamage;
        ent.Comp.MinDamage = blindable.MinDamage;
        _blindable.UpdateIsBlind((args.Body, blindable));
    }

    private void OnEyeImplanted(Entity<OrganEyesComponent> ent, ref SurgeryOrganImplantationCompleted args)
    {
        if (!TryComp<BlindableComponent>(args.Body, out var blindable))
            return;

        _blindable.SetMinDamage((args.Body, blindable), ent.Comp.MinDamage ?? 0);
        _blindable.AdjustEyeDamage((args.Body, blindable), (ent.Comp.EyeDamage ?? 0) - blindable.MaxDamage);
    }

    private void OnVisualizationExtracted(Entity<OrganVisualizationComponent> ent, ref SurgeryOrganExtracted args)
    {
        _humanoidAppearanceSystem.SetLayersVisibility(args.Body, [ent.Comp.Layer], false);
    }

    private void OnVisualizationImplanted(Entity<OrganVisualizationComponent> ent, ref SurgeryOrganImplantationCompleted args)
    {
        _humanoidAppearanceSystem.SetLayersVisibility(args.Body, [ent.Comp.Layer], true);
        _humanoidAppearanceSystem.SetBaseLayerId(args.Body, ent.Comp.Layer, ent.Comp.Prototype);
    }
}
