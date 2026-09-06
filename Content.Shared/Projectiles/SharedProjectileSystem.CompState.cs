


namespace Content.Shared.Projectiles;

public abstract partial class SharedProjectileSystem : EntitySystem
{

    public void InitCompStates()
    {
        SubscribeLocalEvent<ProjectileComponent, ComponentGetState>(OnGetState);
        SubscribeLocalEvent<ProjectileComponent, ComponentHandleState>(OnHandleState);
        SubscribeLocalEvent<ProjectileComponent, ComponentGetStateAttemptEvent>(OnProjGetStateAttempt);
    }

    //TODO: figure out why this happens and rework as needed......Probably mounts
    private void OnProjGetStateAttempt(EntityUid uid, ProjectileComponent comp, ref ComponentGetStateAttemptEvent args)
    {
        if (Deleted(comp.Shooter) || Deleted(comp.Weapon) || GetNetEntity(comp.Shooter) == NetEntity.Invalid ||
        GetNetEntity(comp.Weapon) == NetEntity.Invalid)
        {
            args.Cancelled = true;
            TryQueueDel(uid);
            DebugTools.Assert(DebugProjHandle(comp));
        }
    }
    public bool DebugProjHandle(ProjectileComponent comp)
    {
        Log.Debug($"Tick: {_timing.CurTick} Lifestage: {comp.LifeStage} Shooter: {comp.Shooter} Deleted?: {Deleted(comp.Weapon)} NetEnt:{GetNetEntity(comp.Shooter)} \n Weapon: {comp.Weapon} Deleted?: {Deleted(comp.Weapon)} NetEnt:{GetNetEntity(comp.Weapon)} ");
        return true;
    }
    private void OnGetState(EntityUid uid, ProjectileComponent component, ref ComponentGetState args)
    {
        // Get full state
        args.State = new ProjectileComponentState
        {
            Angle = component.Angle,
            Shooter = Deleted(component.Shooter) ? NetEntity.Invalid : GetNetEntity(component.Shooter),
            Weapon = Deleted(component.Weapon) ? NetEntity.Invalid : GetNetEntity(component.Weapon),
            ExtraIgnoredEntity = Deleted(component.ExtraIgnoredEntity) ? NetEntity.Invalid : GetNetEntity(component.ExtraIgnoredEntity),
            IgnoreShooter = component.IgnoreShooter,
            DamagedEntity = component.DamagedEntity,
        };
    }

    private void OnHandleState(EntityUid uid, ProjectileComponent component, ref ComponentHandleState args)
    {
        if (args.Current is not ProjectileComponentState state)
            return;

        component.Angle = state.Angle;
        component.Shooter = EnsureEntity<ProjectileComponent>(state.Shooter, uid);
        component.Weapon = EnsureEntity<ProjectileComponent>(state.Weapon, uid);
        component.ExtraIgnoredEntity = EnsureEntity<ProjectileComponent>(state.ExtraIgnoredEntity, uid);
        component.IgnoreShooter = state.IgnoreShooter;
        component.DamagedEntity = state.DamagedEntity;
    }

    [Serializable, NetSerializable]
    public sealed class ProjectileComponentState : IComponentState
    {
        public Angle Angle = default;
        public NetEntity? Shooter = default;
        public NetEntity? Weapon = default;
        public NetEntity? ExtraIgnoredEntity = default;
        public bool IgnoreShooter = default;
        public bool DamagedEntity = default;
    }
}
