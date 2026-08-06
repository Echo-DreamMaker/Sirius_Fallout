// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Humanoid;
using Content.Shared._Misfits.Genetics.Mutations;

namespace Content.Shared._Misfits.Genetics;

/// <summary>
/// Reads and applies the identity data stored by genetics. Nuclear-14 keeps forensic fingerprints server-side,
/// so fingerprint application is handled by the server integration while appearance remains shared.
/// </summary>
public sealed partial class UniqueEnzymesSystem : EntitySystem
{
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private MutationSystem _mutation = default!;
    [Dependency] private SharedHumanoidAppearanceSystem _humanoid = default!;

    public void ChangeEnzymes(EntityUid mob, UniqueEnzymes enzymes)
    {
        if (!_mutation.CanMutate(mob))
            return;

        _meta.SetEntityName(mob, enzymes.Name);
        if (!TryComp<HumanoidAppearanceComponent>(mob, out var humanoid))
            return;

        if (enzymes.EyeColor is {} eyeColor)
            humanoid.EyeColor = eyeColor;
        if (enzymes.SkinColor is {} skinColor)
            _humanoid.SetSkinColor(mob, skinColor, humanoid: humanoid);
        if (enzymes.Sex is {} sex)
            _humanoid.SetSex(mob, sex, humanoid: humanoid);
        if (enzymes.Gender is {} gender)
            humanoid.Gender = gender;

        Dirty(mob, humanoid);
    }

    public UniqueEnzymes GetEnzymes(EntityUid mob)
    {
        var humanoid = CompOrNull<HumanoidAppearanceComponent>(mob);
        return new UniqueEnzymes(
            Name(mob),
            null,
            humanoid?.Sex,
            humanoid?.Gender,
            humanoid?.EyeColor,
            humanoid?.SkinColor);
    }
}
