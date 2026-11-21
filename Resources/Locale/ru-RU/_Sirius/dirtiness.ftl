# #Sirius Add: SiriusDirty system localization.

alerts-sirius-dirty-name = Загрязнение
alerts-sirius-dirty-desc = Чистота вашего персонажа. Мыло, душ и чистая вода помогают, кровотечение и грязная вода мешают.
alerts-sirius-dirty-current-state = Состояние: {$state}.
sirius-dirty-level-animal = Животное
sirius-dirty-level-very-dirty = Очень грязный
sirius-dirty-level-dirty = Грязный
sirius-dirty-level-slightly-smelly = Слегка пахнет
sirius-dirty-level-relatively-clean = Относительно чистый
sirius-dirty-level-clean = Чистый
sirius-dirty-level-fragrant = Благоухает
sirius-dirty-tooltip-ghoul = Гули грязнятся вдвое быстрее.

alerts-sirius-wet-name = Мокрый
alerts-sirius-wet-desc = Вы мокрый

sirius-dirty-examine-animal = [color=red]Вы прямо чувствуете резкий звериный запах.[/color]
sirius-dirty-examine-very-dirty = [color=red]Вы прямо чувствуете сильный запах грязи.[/color]
sirius-dirty-examine-dirty = [color=#c86432]Вы прямо чувствуете запах грязи.[/color]
sirius-dirty-examine-slightly-smelly = [color=#c8b432]Вы прямо чувствуете лёгкий неприятный запах.[/color]
sirius-dirty-examine-relatively-clean = [color=#96c832]Вы почти ничего не чувствуете.[/color]
sirius-dirty-examine-clean = [color=green]Вы чувствуете приятный аромат.[/color]
sirius-dirty-examine-fragrant = [color=#64c8ff]Вы прямо чувствуете приятный аромат.[/color]

# #Sirius Add: descriptions of the two smell mood slots.
mood-effect-SiriusSmellSelfAnimal = От вас ужасно пахнет зверем.
mood-effect-SiriusSmellSelfVeryDirty = От вас очень сильно пахнет грязью.
mood-effect-SiriusSmellSelfDirty = От вас пахнет грязью.
mood-effect-SiriusSmellSelfSlightlySmelly = От вас почти ничего не пахнет.
mood-effect-SiriusSmellSelfRelativelyClean = От вас почти ничего не пахнет.
mood-effect-SiriusSmellSelfClean = Вы приятно пахнете.
mood-effect-SiriusSmellSelfFragrant = От вас исходит восхитительный аромат.

mood-effect-SiriusSmellOthersAnimal = Кто-то рядом ужасно пахнет зверем.
mood-effect-SiriusSmellOthersVeryDirty = Кто-то рядом очень сильно пахнет грязью.
mood-effect-SiriusSmellOthersDirty = Кто-то рядом пахнет грязью.
mood-effect-SiriusSmellOthersSlightlySmelly = Кто-то рядом почти ничего не пахнет.
mood-effect-SiriusSmellOthersRelativelyClean = Кто-то рядом почти ничего не пахнет.
mood-effect-SiriusSmellOthersClean = Кто-то рядом приятно пахнет.
mood-effect-SiriusSmellOthersFragrant = Кто-то рядом восхитительно пахнет.

reagent-name-soap-water = мыльная вода
reagent-desc-soap-water = Вода с растворённым мылом. Намыливает того, на кого попала.

reagent-name-lye = щёлок
reagent-desc-lye = Щелочной раствор, из которого варят мыло.

sirius-reagent-effect-wet = Делает цель мокрой.
sirius-reagent-effect-wash = Намыливает цель.
sirius-reagent-effect-solidify-soap = Превращается в брусок мыла при остывании.

sirius-soap-lather-self = Вы намылились.
sirius-soap-lather-target = Вы намылили {THE($target)}.
sirius-soap-need-wet-self = Сначала намочитесь.
sirius-soap-need-wet-target = Сначала намочите {THE($target)}.
sirius-soap-not-enough = Недостаточно мыла.
sirius-soap-lather-other =
    { $gender ->
        [female] {THE($user)} намылила {THE($target)}.
        [epicene] {THE($user)} намылили {THE($target)}.
        [neuter] {THE($user)} намылило {THE($target)}.
        *[male] {THE($user)} намылил {THE($target)}.
    }
