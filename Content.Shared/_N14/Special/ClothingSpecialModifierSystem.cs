using Content.Shared.Examine;
using Content.Shared.Inventory.Events;
using Content.Shared._Misfits.Special;
using Content.Shared._N14.Special.Components;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Shared._N14.Special;

/// <summary>
/// Adapter that lets clothing grant flat SPECIAL bonuses through the misfits temporary
/// modifier API. Each equipped item owns a unique modifier source key so removing one
/// piece of gear never clears another's bonuses. Runs on both server and client: the
/// local effect applies immediately for the wearer while the server stays authoritative.
/// </summary>
public sealed partial class ClothingSpecialModifierSystem : EntitySystem
{
    [Dependency] private readonly SharedSpecialSystem _special = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ClothingSpecialModifierComponent, GotEquippedEvent>(OnEquipmentEquipped);
        SubscribeLocalEvent<ClothingSpecialModifierComponent, GotUnequippedEvent>(OnEquipmentUnequipped);
        SubscribeLocalEvent<ClothingSpecialModifierComponent, ComponentShutdown>(OnEquipmentRemoved);
        SubscribeLocalEvent<ClothingSpecialModifierComponent, GetVerbsEvent<ExamineVerb>>(OnClothingVerbExamine);
    }

    private void OnEquipmentEquipped(EntityUid uid, ClothingSpecialModifierComponent component, GotEquippedEvent args)
    {
        if (!component.Enabled)
            return;

        component.Equipee = args.Equipee;
        ApplyModifiers(args.Equipee, uid, component);
    }

    private void OnEquipmentUnequipped(EntityUid uid, ClothingSpecialModifierComponent component, GotUnequippedEvent args)
    {
        ClearModifiers(args.Equipee, uid);
        component.Equipee = null;
    }

    private void OnEquipmentRemoved(EntityUid uid, ClothingSpecialModifierComponent component, ComponentShutdown args)
    {
        if (component.Equipee is { } equipee)
            ClearModifiers(equipee, uid);
    }

    private void ApplyModifiers(EntityUid equipee, EntityUid item, ClothingSpecialModifierComponent component)
    {
        if (!_special.UsesSpecialStats(equipee))
            return;

        var source = GetSourceKey(item);
        TryModify(equipee, SpecialStat.Strength, component.StrengthModifier, source);
        TryModify(equipee, SpecialStat.Perception, component.PerceptionModifier, source);
        TryModify(equipee, SpecialStat.Endurance, component.EnduranceModifier, source);
        TryModify(equipee, SpecialStat.Charisma, component.CharismaModifier, source);
        TryModify(equipee, SpecialStat.Intelligence, component.IntelligenceModifier, source);
        TryModify(equipee, SpecialStat.Agility, component.AgilityModifier, source);
        TryModify(equipee, SpecialStat.Luck, component.LuckModifier, source);
    }

    private void ClearModifiers(EntityUid equipee, EntityUid item)
    {
        if (_special.UsesSpecialStats(equipee))
            _special.ClearTemporaryModifiers(equipee, GetSourceKey(item));
    }

    private void TryModify(EntityUid equipee, SpecialStat stat, int modifier, string source)
    {
        if (modifier != 0)
            _special.TryModifyTemporary(equipee, stat, modifier, source: source);
    }

    private string GetSourceKey(EntityUid item)
    {
        return $"clothing-special-{GetNetEntity(item)}";
    }

    private static readonly (string Increase, string Decrease, string Variable)[] StatLocKeys =
    {
        ("clothing-strength-increase-equal-examine", "clothing-strength-decrease-equal-examine", "strength"),
        ("clothing-perception-increase-equal-examine", "clothing-perception-decrease-equal-examine", "perception"),
        ("clothing-endurance-increase-equal-examine", "clothing-endurance-decrease-equal-examine", "endurance"),
        ("clothing-charisma-increase-equal-examine", "clothing-charisma-decrease-equal-examine", "charisma"),
        ("clothing-intelligence-increase-equal-examine", "clothing-intelligence-decrease-equal-examine", "intelligence"),
        ("clothing-agility-increase-equal-examine", "clothing-agility-decrease-equal-examine", "agility"),
        ("clothing-luck-increase-equal-examine", "clothing-luck-decrease-equal-examine", "luck"),
    };

    private void OnClothingVerbExamine(EntityUid uid, ClothingSpecialModifierComponent component, GetVerbsEvent<ExamineVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        var msg = new FormattedMessage();
        var stats = new[] { component.StrengthModifier, component.PerceptionModifier, component.EnduranceModifier, component.CharismaModifier, component.IntelligenceModifier, component.AgilityModifier, component.LuckModifier };

        for (var i = 0; i < stats.Length; i++)
        {
            if (stats[i] == 0)
                continue;

            var (inc, dec, variable) = StatLocKeys[i];
            var locKey = stats[i] > 0 ? inc : dec;
            msg.AddMarkup(Loc.GetString(locKey, (variable, stats[i])));
            msg.PushNewline();
        }

        _examine.AddDetailedExamineVerb(args,
            component,
            msg,
            Loc.GetString("clothing-special-examinable-verb-text"),
            "/Textures/Interface/examine-star.png",
            Loc.GetString("clothing-special-examinable-verb-message"));
    }
}