using Content.Shared.Damage;
using Content.Shared.Humanoid;
using Robust.Shared.GameStates;

namespace Content.Shared._Horizon.Medical.Surgery.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class OrganTongueComponent : Component
{
    [DataField]
    public bool IsMuted;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class OrganEyesComponent : Component
{
    [DataField]
    public int? EyeDamage;

    [DataField]
    public int? MinDamage;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class OrganVisualizationComponent : Component
{
    [DataField]
    public HumanoidVisualLayers Layer;

    [DataField]
    public string? Prototype;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class OrganDamageComponent : Component
{
    [DataField]
    public DamageSpecifier? InsertDamage;

    [DataField]
    public DamageSpecifier? RemoveDamage;
}
