# Misfits Change — Addiction withdrawal popup messages and guidebook text

# Withdrawal effect popups (1-indexed for localizedDataset prefix+count)
# Sirius Add: индекс 0 тоже может прийти из LocalizedDatasetValues[0] — общий текст для любого вещества.
addiction-effect-0 = Вы чувствуете, как организм настаивает на новой дозе.
addiction-effect-1 = Твои руки неудержимо трясутся.
addiction-effect-2 = Вы чувствуете, как отчаянная жажда гложет вас изнутри.
addiction-effect-3 = Холодный пот выступает по коже.
addiction-effect-4 = Мышцы болят и болезненно подергиваются.
addiction-effect-5 = Вы чувствуете тошноту и головокружение.
addiction-effect-6 = Невыносимый зуд ползет под кожей.
addiction-effect-7 = Голова раскалывается от раскалывающейся головной боли.
addiction-effect-8 = Вы не можете перестать думать о следующем исправлении.
addiction-effect-9 = Желудок сильно урчит.
addiction-effect-10 = Вы чувствуете себя слабым и раздражительным.

# Misfits Change /Add:/ Drug-specific addiction chat messages
# Only the first-addiction message is sent; grows/deepens/severe were removed to prevent per-dose spam.
addiction-drug-first = Вы чувствуете, что ваше тело начинает жаждать {$drug}.

# Misfits Change /Add:/ Fading messages — sent once each time the remaining addiction tier drops.
# Tier 3 (severe) > 120s, tier 2 (moderate) 60-120s, tier 1 (mild) 15-60s, tier 0 (nearly gone) <15s.
addiction-drug-fading-moderate = Самая сильная тяга к {$drug} начинает проходить.
addiction-drug-fading-mild = Ваша потребность в {$drug} ослабевает.
addiction-drug-fading-nearly = Ваша тяга к {$drug } почти ушла.
addiction-drug-clean = Последняя тяга к {$drug } угаснет.

# Guidebook descriptions
reagent-effect-guidebook-overdose-toxic = {$chance ->
    [1] Causes
    *[other] cause
} toxic poisoning when taken in excess
reagent-effect-guidebook-addicted = {$chance ->
    [1] Causes
    *[other] cause
} an addiction
reagent-effect-guidebook-addiction-suppression = {$chance ->
    [1] Suppresses
    *[other] suppress
} active addictions