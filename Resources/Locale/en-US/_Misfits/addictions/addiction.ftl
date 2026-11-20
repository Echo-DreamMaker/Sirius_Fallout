# Misfits Change — Addiction withdrawal popup messages and guidebook text

# Withdrawal effect popups (1-indexed for localizedDataset prefix+count)
# Sirius Add: index 0 can also come from LocalizedDatasetValues[0]; generic text for any substance.
addiction-effect-0 = You feel your body insisting on another dose.
addiction-effect-1 = Your hands are shaking uncontrollably.
addiction-effect-2 = You feel a desperate craving gnawing at your insides.
addiction-effect-3 = A cold sweat breaks out across your skin.
addiction-effect-4 = Your muscles ache and twitch painfully.
addiction-effect-5 = You feel nauseous and lightheaded.
addiction-effect-6 = An unbearable itch crawls beneath your skin.
addiction-effect-7 = Your head pounds with a splitting headache.
addiction-effect-8 = You can't stop thinking about your next fix.
addiction-effect-9 = Your stomach churns violently.
addiction-effect-10 = You feel weak and irritable.

# Misfits Change /Add:/ Drug-specific addiction chat messages
# Only the first-addiction message is sent; grows/deepens/severe were removed to prevent per-dose spam.
addiction-drug-first = You feel your body beginning to crave { $drug }.

# Misfits Change /Add:/ Fading messages — sent once each time the remaining addiction tier drops.
# Tier 3 (severe) > 120s, tier 2 (moderate) 60-120s, tier 1 (mild) 15-60s, tier 0 (nearly gone) <15s.
addiction-drug-fading-moderate = The worst of your { $drug } craving is beginning to pass.
addiction-drug-fading-mild     = Your need for { $drug } is weakening.
addiction-drug-fading-nearly   = Your craving for { $drug } is almost gone.
addiction-drug-clean           = The last of your { $drug } craving fades away.

# Guidebook descriptions
reagent-effect-guidebook-overdose-toxic = { $chance ->
    [1] Causes
    *[other] cause
} toxic poisoning when taken in excess
reagent-effect-guidebook-addicted = { $chance ->
    [1] Causes
    *[other] cause
} an addiction
reagent-effect-guidebook-addiction-suppression = { $chance ->
    [1] Suppresses
    *[other] suppress
} active addictions
