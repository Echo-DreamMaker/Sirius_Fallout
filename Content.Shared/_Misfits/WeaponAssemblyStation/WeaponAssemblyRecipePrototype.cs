// #Misfits Add - Assembly validity table for the weapon assembly station.
// The modular weapon system lives entirely in the construction graph, which the
// station deliberately does not walk: it matches the seated frame and parts
// against a flat table instead. The table is generated from the same source of
// truth as the graphs (Tools/n14_weapon_parts_gen.py), so the two cannot drift.
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared._Misfits.WeaponAssemblyStation;

/// <summary>
/// One weapon that the parts system can build, keyed by the weapon it produces.
/// </summary>
[Prototype]
public sealed partial class WeaponAssemblyRecipePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>The weapon this recipe produces.</summary>
    [DataField(required: true)]
    public EntProtoId Result = default!;

    /// <summary>The bare frame that has to sit in the station's main slot.</summary>
    [DataField(required: true)]
    public EntProtoId Frame = default!;

    /// <summary>
    /// The part prototypes the four working slots have to hold, in slot order
    /// (barrel/optics, bolt, grip/drive, stock). A null entry means that slot
    /// stays empty, which is how a stockless build is matched.
    /// </summary>
    [DataField]
    public List<EntProtoId?> Parts = new();

    /// <summary>
    /// Tag the magazine in the sixth slot must carry, or null when the weapon
    /// has no detachable magazine.
    /// </summary>
    [DataField]
    public ProtoId<TagPrototype>? Magazine;
}
