// #Misfits Add - Marks belt-fed machine guns. Inserting an ammo belt into the gun_magazine
// slot is handled by a timed do-after (GunBeltInsertDoAfterEvent) instead of the instant
// ItemSlots interact insert, giving reloads an Agility-scaled delay.
using System;

namespace Content.Shared.Weapons.Ranged.Components;

[RegisterComponent]
public sealed partial class GunBeltInsertDelayComponent : Component
{
    // Base delay at 5 Agility. SharedDoAfterSystem divides this by the Agility action-speed
    // multiplier, so the value here is the exact time a neutral Agility character waits.
    [DataField("delay")]
    public TimeSpan Delay = TimeSpan.FromSeconds(4);
}