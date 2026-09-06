using Content.Shared._Misfits.Special;
using Content.Shared._Misfits.Special.Components;
using Content.Shared.Random.Helpers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Misfits.SpecialStats;

/// <summary>
/// Grants a small bonus item chance when a lucky player completes a junk-pile search.
/// </summary>
public sealed class SpecialLuckSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedSpecialSystem _special = default!;

    private static readonly Dictionary<LuckyLootRarity, float> RarityWeights = new()
    {
        [LuckyLootRarity.Common] = 100f,
        [LuckyLootRarity.Uncommon] = 45f,
        [LuckyLootRarity.Rare] = 18f,
        [LuckyLootRarity.VeryRare] = 6f,
        [LuckyLootRarity.Legendary] = 1f,
    };

    /// <summary>
    /// Rolls the Luck S.P.E.C.I.A.L. bonus for one completed junk-pile search.
    /// </summary>
    public void TryGrantJunkBonus(Entity<LuckJunkBonusComponent> ent, EntityUid actor)
    {
        if (!TryComp<SpecialComponent>(actor, out var special))
            return;

        var rollChance = _special.GetLuckRollChance(actor, 0f, ent.Comp.ChancePerLuckPoint, special);
        if (!_random.Prob(rollChance))
            return;

        if (ent.Comp.LuckyItems.Count == 0)
            return;

        if (!TryPickLuckyItem(ent.Comp, out var chosenProto))
            return;

        Spawn(chosenProto, Transform(ent.Owner).Coordinates);
    }

    private bool TryPickLuckyItem(LuckJunkBonusComponent component, out EntProtoId chosenProto)
    {
        var weights = new Dictionary<EntProtoId, float>();

        foreach (var entry in component.LuckyItems)
        {
            if (!RarityWeights.TryGetValue(entry.Rarity, out var weight) || weight <= 0f)
                continue;

            weights[entry.Id] = weight;
        }

        if (weights.Count == 0)
        {
            chosenProto = default;
            return false;
        }

        chosenProto = _random.Pick(weights);
        return true;
    }
}
