using Content.Shared.Examine;

// #N14 Add - Show the round's armor penetration (AP/JHP/FMJ/Scrap variants)
// when examining the ammo. The value is the fraction of armor ignored, shown as a
// signed percentage. Negative values mean the round is worse at defeating armor.

namespace Content.Shared._N14.Weapons;

public sealed class ArmorPenetrationSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ArmorPenetrationComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(EntityUid uid, ArmorPenetrationComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || component.Penetration == 0f)
            return;

        var percent = (int) MathF.Round(component.Penetration * 100f);
        args.PushMarkup(Loc.GetString("n14-armor-penetration-examine", ("value", percent)));
    }
}