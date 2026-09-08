using Robust.Shared.Prototypes;

namespace Content.Shared._Misfits.Special.Prototypes;

[Prototype("specialTuning")]
public sealed partial class SpecialTuningPrototype : IPrototype
{
    // Code defaults keep the system usable in tests or when the tuning prototype
    // is not loaded. Production values should live in YAML.
    public static readonly SpecialTuningPrototype Fallback = new()
    {
        ID = "Fallback",
    };

    [IdDataField]
    public string ID { get; private set; } = default!;

    // Strength: melee output and carry handling.
    [DataField("strengthMeleeDamageMultiplierPerPoint")]
    public float StrengthMeleeDamageMultiplierPerPoint = 0.02f;

    [DataField("strengthUnarmedDamageMultiplierPerPoint")]
    public float StrengthUnarmedDamageMultiplierPerPoint = 0.03333333f;

    [DataField("strengthCarryPullSpeedMultiplierPerPoint")]
    public float StrengthCarryPullSpeedMultiplierPerPoint = 0.04f;

    [DataField("strengthThrowSpeedMultiplierPerPoint")]
    public float StrengthThrowSpeedMultiplierPerPoint = 0.06666667f;

    // Perception: ranged accuracy, heavy gun handling, mining speed, and fire delay.
    [DataField("perceptionSpreadMultiplierPerPoint")]
    public float PerceptionSpreadMultiplierPerPoint = 0.03333333f;

    [DataField("perceptionHeavyGunMultiplierPerPoint")]
    public float PerceptionHeavyGunMultiplierPerPoint = 0.01333333f;

    [DataField("perceptionMineDelayMultiplierPerPoint")]
    public float PerceptionMineDelayMultiplierPerPoint = 0.03333333f;

    [DataField("perceptionFireDelayMultiplierPerPoint")]
    public float PerceptionFireDelayMultiplierPerPoint = 0.01333333f;

    // Endurance: survivability, needs, stamina, and toxin resistance.
    [DataField("enduranceStaminaCritThresholdPerPoint")]
    public float EnduranceStaminaCritThresholdPerPoint = 4f;

    [DataField("enduranceHealthModifierPerPoint")]
    public float EnduranceHealthModifierPerPoint = 2.6666667f;

    [DataField("enduranceNeedDecayMultiplierPerPoint")]
    public float EnduranceNeedDecayMultiplierPerPoint = 0.016f;

    [DataField("enduranceStaminaRecoveryMultiplierPerPoint")]
    public float EnduranceStaminaRecoveryMultiplierPerPoint = 0.02666667f;

    [DataField("enduranceToxinDamageMultiplierPerPoint")]
    public float EnduranceToxinDamageMultiplierPerPoint = 0.02f;

    // Charisma: economy, loadout points, presentation, and leadership hooks.
    [DataField("charismaTradeMultiplierPerPoint")]
    public float CharismaTradeMultiplierPerPoint = 0.01333333f;

    [DataField("charismaWarcryRangeMultiplierPerPoint")]
    public float CharismaWarcryRangeMultiplierPerPoint = 0.006667f;

    [DataField("charismaWarcryDurationMultiplierPerPoint")]
    public float CharismaWarcryDurationMultiplierPerPoint = 0.006667f;

    [DataField("charismaWarcrySpeedMultiplierPerPoint")]
    public float CharismaWarcrySpeedMultiplierPerPoint = 0.006667f;

    [DataField("charismaNeutralFollowerMinimum")]
    public int CharismaNeutralFollowerMinimum = 8;

    // Intelligence: crafting/medical quality-of-life gates.
    [DataField("intelligenceLatheTimeMultiplierPerPoint")]
    public float IntelligenceLatheTimeMultiplierPerPoint = 0.06666667f;

    [DataField("intelligenceLatheMaterialUseMultiplierPerPoint")]
    public float IntelligenceLatheMaterialUseMultiplierPerPoint = 0.03333333f;

    // Agility: movement and general action speed.
    [DataField("agilityMovementSpeedMultiplierPerPoint")]
    public float AgilityMovementSpeedMultiplierPerPoint = 0.01f;

    [DataField("agilityActionDelayMultiplierPerPoint")]
    public float AgilityActionDelayMultiplierPerPoint = 0.01333333f;

    // Luck: critical hits and chance-based reward hooks.
    [DataField("luckCriticalChancePerPoint")]
    public float LuckCriticalChancePerPoint = 0.005f;

    [DataField("luckSingleShotCriticalChancePerPoint")]
    public float LuckSingleShotCriticalChancePerPoint = 0.03333333f;

    [DataField("luckCriticalDamageMultiplier")]
    public float LuckCriticalDamageMultiplier = 1.5f;

    [DataField("luckUnluckyDamageMultiplier")]
    public float LuckUnluckyDamageMultiplier = 0.5f;

    [DataField("luckLootChancePerPoint")]
    public float LuckLootChancePerPoint = 0.025f;

    // Luck: chance to dodge incoming bullets and hitscan shots.
    [DataField("luckDodgeChancePerPoint")]
    public float LuckDodgeChancePerPoint = 0.0075f;

    // Endurance: damage resistance to Poison and Radiation per point above average.
    [DataField("endurancePoisonResistancePerPoint")]
    public float EndurancePoisonResistancePerPoint = 0.1f;

    [DataField("enduranceRadiationResistancePerPoint")]
    public float EnduranceRadiationResistancePerPoint = 0.1f;

    // Strength: reduces the chance of the character being disarmed per point above average.
    [DataField("strengthDisarmProtectionPerPoint")]
    public float StrengthDisarmProtectionPerPoint = 0.08f;

    // Intelligence: energy-weapon misfire and drop penalties when INT < requirement.
    [DataField("intelligenceWeaponMisfireChancePerPoint")]
    public float IntelligenceWeaponMisfireChancePerPoint = 0.1f;

    [DataField("intelligenceWeaponDropChancePerPoint")]
    public float IntelligenceWeaponDropChancePerPoint = 0.05f;

    // Intelligence: low-INT speech corruption (pronunciation + mumbling).
    // Severity scales with the deficit below the requirement (4 - INT).
    [DataField("intelligenceVowelReplaceBaseChance")]
    public float IntelligenceVowelReplaceBaseChance = 0.2f;

    [DataField("intelligenceVowelReplaceChancePerPoint")]
    public float IntelligenceVowelReplaceChancePerPoint = 0.22f;

    [DataField("intelligenceMumbleMinWordsBase")]
    public int IntelligenceMumbleMinWordsBase = 6;

    [DataField("intelligenceMumbleChanceBase")]
    public float IntelligenceMumbleChanceBase = 0.1f;

    [DataField("intelligenceMumbleChancePerPoint")]
    public float IntelligenceMumbleChancePerPoint = 0.2f;

    // Perception: aiming mode range and accuracy bonuses.
    [DataField("perceptionAimZoomMultiplierPerPoint")]
    public float PerceptionAimZoomMultiplierPerPoint = 0.08f;

    [DataField("perceptionAimSpreadMultiplierPerPoint")]
    public float PerceptionAimSpreadMultiplierPerPoint = 0.02f;

    // Sniper rifles (tags: Sniper) get a stronger spread-reduction reward while aiming.
    [DataField("perceptionAimSpreadSniperMultiplierPerPoint")]
    public float PerceptionAimSpreadSniperMultiplierPerPoint = 0.05f;

    // Perception: motion-trace "infrared" reveal of living mobs through walls.
    // Requires a Perception of at least PerceptionTraceMinPerception. The instantly
    // lingering afterimage fades out over PerceptionTracePersistenceTime.
    [DataField("perceptionTraceMinPerception")]
    public int PerceptionTraceMinPerception = 8;

    [DataField("perceptionTraceRange")]
    public float PerceptionTraceRange = 13f;

    [DataField("perceptionTracePersistenceTime")]
    public float PerceptionTracePersistenceTime = 3f;

    [DataField("perceptionTraceMinAlpha")]
    public float PerceptionTraceMinAlpha = 0.3f;

    [DataField("perceptionTraceAlphaPerPoint")]
    public float PerceptionTraceAlphaPerPoint = 0.25f;
}
