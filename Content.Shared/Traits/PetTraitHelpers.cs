using Robust.Shared.Prototypes;

namespace Content.Shared.Traits;

// #Cythisiax Added - Pet size helpers for the separate Pets point system.
// Pet traits live under the "Pets" root category in one of three size subcategories,
// and are paid for out of their own pet point pool (see game.pets_* CVars) instead of
// the regular perk/trait points. Both the client editor and the server validators share
// this logic so a pet can never be mistaken for a regular perk.
public static class PetTraitHelpers
{
    public const string PetsRootCategory = "Pets";
    public const string PetsSmall = "PetsSmall";
    public const string PetsMedium = "PetsMedium";
    public const string PetsLarge = "PetsLarge";

    public const string SizeSmall = "Small";
    public const string SizeMedium = "Medium";
    public const string SizeLarge = "Large";

    /// <summary>
    ///     True if this trait is a pet (its category is one of the pet size subcategories).
    /// </summary>
    public static bool IsPet(TraitPrototype trait)
        => GetPetSize(trait) != null;

    /// <summary>
    ///     The pet size tier ("Small", "Medium" or "Large"), or null if the trait is not a pet.
    /// </summary>
    public static string? GetPetSize(TraitPrototype trait)
        => trait.Category.Id switch
        {
            PetsSmall => SizeSmall,
            PetsMedium => SizeMedium,
            PetsLarge => SizeLarge,
            _ => null,
        };
}
