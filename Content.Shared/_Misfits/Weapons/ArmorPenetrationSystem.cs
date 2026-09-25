using Content.Shared.Examine;

// #Misfits Add - Show the round's armor penetration (AP/JHP/FMJ/Scrap variants)
// when examining the ammo. The value is the fraction of armor ignored, shown as a
// signed percentage. Negative values mean the round is worse at defeating armor.

namespace Content.Shared._Misfits.Weapons;

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
        args.PushMarkup(Loc.GetString("misfits-armor-penetration-examine", ("value", percent)));
    }
}