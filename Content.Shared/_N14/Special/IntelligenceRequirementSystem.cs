using Content.Shared._Misfits.Special;
using Content.Shared._Misfits.Special.Prototypes;
using Content.Shared._N14.Special.Components;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Network;
using Robust.Shared.Random;

namespace Content.Shared._N14.Special;

/// <summary>
/// Server-side gate for energy weapons requiring minimum Intelligence.
/// On each shot attempt, rolls a drop-from-hands or misfire chance when the
/// wielder's effective Intelligence is below the weapon's requirement.
/// Also provides an examine tooltip showing the requirement.
/// </summary>
public sealed class IntelligenceRequirementSystem : EntitySystem
{
    [Dependency] private readonly SharedSpecialSystem _special = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IntelligenceRequirementComponent, ShotAttemptedEvent>(OnShotAttempted);
        SubscribeLocalEvent<IntelligenceRequirementComponent, ExaminedEvent>(OnExamined);
    }

    private void OnShotAttempted(EntityUid uid, IntelligenceRequirementComponent component, ref ShotAttemptedEvent args)
    {
        if (!_net.IsServer)
            return;

        if (!_special.UsesSpecialStats(args.User))
            return;

        var intelligence = _special.GetEffective(args.User, SpecialStat.Intelligence);

        if (intelligence >= component.RequiredIntelligence)
            return;

        var deficit = component.RequiredIntelligence - intelligence;
        var tuning = _special.GetTuning();
        var dropChance   = deficit * tuning.IntelligenceWeaponDropChancePerPoint;
        var misfireChance = deficit * tuning.IntelligenceWeaponMisfireChancePerPoint;

        if (_random.Prob(dropChance))
        {
            _hands.TryDrop(args.User, uid, checkActionBlocker: false);
            _popup.PopupEntity(
                Loc.GetString("intelligence-requirement-drop", ("item", uid)),
                uid,
                args.User);
            args.Cancel();
            return;
        }

        if (_random.Prob(misfireChance))
        {
            _popup.PopupEntity(
                Loc.GetString("intelligence-requirement-misfire", ("item", uid)),
                uid,
                args.User);
            args.Cancel();
        }
    }

    private void OnExamined(EntityUid uid, IntelligenceRequirementComponent component, ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("intelligence-requirement-examine",
            ("required", component.RequiredIntelligence)));
    }
}
