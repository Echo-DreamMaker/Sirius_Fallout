// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Slippery;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio;
using Robust.Shared.Network;

namespace Content.Shared._Misfits.Genetics.Abilities;

[RegisterComponent, NetworkedComponent]
public sealed partial class SpecialLowTempImmunityComponent : Component
{
    public override bool SessionSpecific => true;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class SpecialHighTempImmunityComponent : Component
{
    public override bool SessionSpecific => true;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class FireImmunityComponent : Component
{
    public override bool SessionSpecific => true;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class WeatherImmuneComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class DeafComponent : Component;

/// <summary>
/// Genetics-compatible subset of Trauma's hulk state. It preserves the mutation's stamina and slip immunity
/// and its large unarmed/melee damage multiplier without importing Trauma's wizard and laser-eye subsystems.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HulkComponent : Component
{
    [DataField] public float? Duration;
    [DataField] public bool LaserEyes = true;
    [DataField] public SoundSpecifier? SoundGunshot;
    [DataField] public EntProtoId? ShotProto;
    [DataField] public float FistDamageMultiplier = 7f;
    [DataField] public float MaxBonusFistDamage = 50f;
    [DataField] public Color SkinColor = Color.FromHex("#4EDB53");
    [DataField] public Color EyeColor = Color.FromHex("#910C17");

    /// <summary>
    /// The mob's skin and eye colour from before the hulk look was applied, saved so they can be
    /// restored on removal. Only ever assigned with ??=, otherwise a save/reload would re-capture
    /// the already-green values and "restoring" would leave you green.
    /// </summary>
    [DataField] public Color? OriginalSkinColor;

    [DataField] public Color? OriginalEyeColor;
}

public sealed class HulkSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedHumanoidAppearanceSystem _humanoid = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HulkComponent, BeforeStaminaDamageEvent>(OnBeforeStaminaDamage);
        SubscribeLocalEvent<HulkComponent, SlipAttemptEvent>(OnSlipAttempt);
        SubscribeLocalEvent<HulkComponent, GetMeleeDamageEvent>(OnGetMeleeDamage);
        SubscribeLocalEvent<HulkComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<HulkComponent, ComponentShutdown>(OnShutdown);
    }

    // Apply the hulk skin color when the mutation is added. Server-only: it owns humanoid
    // appearance and networks the change to clients, avoiding client mispredicts.
    private void OnStartup(Entity<HulkComponent> ent, ref ComponentStartup args)
    {
        if (_net.IsClient || !TryComp<HumanoidAppearanceComponent>(ent, out var humanoid))
            return;

        // ??= so a save/reload can't re-capture the already-hulked colours as the "originals"
        ent.Comp.OriginalSkinColor ??= humanoid.SkinColor;
        ent.Comp.OriginalEyeColor ??= humanoid.EyeColor;

        // verify:false so the green isn't clamped back to a valid (human) skin tone
        _humanoid.SetSkinColor(ent, ent.Comp.SkinColor, verify: false, humanoid: humanoid);
        _humanoid.SetBaseLayerColor(ent, HumanoidVisualLayers.Eyes, ent.Comp.EyeColor, humanoid: humanoid);
    }

    // Restore the original skin and eye colour when the mutation is removed.
    private void OnShutdown(Entity<HulkComponent> ent, ref ComponentShutdown args)
    {
        if (_net.IsClient || !HasComp<HumanoidAppearanceComponent>(ent))
            return;

        if (ent.Comp.OriginalSkinColor is {} skin)
            _humanoid.SetSkinColor(ent, skin, verify: false);

        _humanoid.SetBaseLayerColor(ent, HumanoidVisualLayers.Eyes, ent.Comp.OriginalEyeColor);
    }

    private static void OnBeforeStaminaDamage(Entity<HulkComponent> ent, ref BeforeStaminaDamageEvent args)
        => args.Cancelled = true;

    private static void OnSlipAttempt(Entity<HulkComponent> ent, ref SlipAttemptEvent args)
        => args.Cancel();

    private static void OnGetMeleeDamage(Entity<HulkComponent> ent, ref GetMeleeDamageEvent args)
    {
        var bonus = new Content.Shared.Damage.DamageSpecifier(args.Damage);
        bonus *= ent.Comp.FistDamageMultiplier;
        var total = bonus.GetTotal().Float();
        if (total > ent.Comp.MaxBonusFistDamage)
            bonus *= ent.Comp.MaxBonusFistDamage / total;
        args.Damage += bonus;
    }
}
