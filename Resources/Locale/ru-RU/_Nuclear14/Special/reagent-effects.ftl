# guidebook reagent special effects

reagent-effect-guidebook-strength-modifier =
    { $chance ->
        [1] Изменяет
        *[other] изменяет
    } силу на {$strength} по меньшей мере на {NATURALFIXED($time, 3)} {MANY("секунд", $time)}

reagent-effect-guidebook-perception-modifier =
    { $chance ->
        [1] Изменяет
        *[other] изменяет
    } восприятие на {$perception} по меньшей мере на {NATURALFIXED($time, 3)} {MANY("секунд", $time)}

reagent-effect-guidebook-endurance-modifier =
    { $chance ->
        [1] Изменяет
        *[other] изменяет
    } выносливость на {$endurance} по меньшей мере на {NATURALFIXED($time, 3)} {MANY("секунд", $time)}

reagent-effect-guidebook-charisma-modifier =
    { $chance ->
        [1] Изменяет
        *[other] изменяет
    } харизму на {$charisma} по меньшей мере на {NATURALFIXED($time, 3)} {MANY("секунд", $time)}

reagent-effect-guidebook-intelligence-modifier =
    { $chance ->
        [1] Изменяет
        *[other] изменяет
    } интеллект на {$intelligence} по меньшей мере на {NATURALFIXED($time, 3)} {MANY("секунд", $time)}

reagent-effect-guidebook-agility-modifier =
    { $chance ->
        [1] Изменяет
        *[other] изменяет
    } ловкость на {$agility} по меньшей мере на {NATURALFIXED($time, 3)} {MANY("секунд", $time)}

reagent-effect-guidebook-luck-modifier =
    { $chance ->
        [1] Изменяет
        *[other] изменяет
    } удачу на {$luck} по меньшей мере на {NATURALFIXED($time, 3)} {MANY("секунд", $time)}

# Misfits Change
reagent-effect-guidebook-nocturine-night-vision =
    Улучшает зрение в условиях низкой освещённости по меньшей мере на {NATURALFIXED($time, 3)} {MANY("секунд", $time)}