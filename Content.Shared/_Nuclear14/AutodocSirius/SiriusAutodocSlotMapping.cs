using Content.Shared.Body.Part;
using Robust.Shared.Serialization;

namespace Content.Shared._Nuclear14.AutodocSirius;

[Serializable, NetSerializable]
public sealed class SiriusAutodocSlotMapping
{
    public static readonly Dictionary<string, string> OrganSlotMap = new()
    {
        { "Brain", "brainSlot" },
        { "Eyes", "eyesSlot" },
        { "Heart", "heartSlot" },
        { "Liver", "liverSlot" },
        { "Lungs", "lungsSlot" },
        { "Stomach", "stomachSlot" },
        { "Kidneys", "kidneysSlot" },
        { "Appendix", "appendixSlot" },
        { "Tongue", "tongueSlot" },
        { "Ears", "earsSlot" }
    };
    public static readonly Dictionary<BodyPartType, string> BodyPartSlotMap = new()
    {
        { BodyPartType.Head, "headSlot" },
        { BodyPartType.Torso, "torsoSlot" },
        { BodyPartType.Arm, "armSlot" },
        { BodyPartType.Hand, "handSlot" },
        { BodyPartType.Leg, "legSlot" },
        { BodyPartType.Foot, "footSlot" }
    };
    public static string? GetSlotForOrgan(string organType)
    {
        if (OrganSlotMap.TryGetValue(organType, out var slot))
            return slot;
        return null;
    }
    public static string? GetSlotForBodyPart(BodyPartType partType, BodyPartSymmetry symmetry)
    {
        var baseSlot = partType switch
        {
            BodyPartType.Head => "headSlot",
            BodyPartType.Torso => "torsoSlot",
            BodyPartType.Arm => symmetry == BodyPartSymmetry.Left ? "leftArmSlot" : "rightArmSlot",
            BodyPartType.Hand => symmetry == BodyPartSymmetry.Left ? "leftHandSlot" : "rightHandSlot",
            BodyPartType.Leg => symmetry == BodyPartSymmetry.Left ? "leftLegSlot" : "rightLegSlot",
            BodyPartType.Foot => symmetry == BodyPartSymmetry.Left ? "leftFootSlot" : "rightFootSlot",
            _ => null
        };
        return baseSlot;
    }
}
