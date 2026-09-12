## Job
character-job-requirement = Вы должны{$inverted ->
    [true]{" "}не
    *[other]{""}
} быть одной из этих ролей: {$jobs}

character-department-requirement = Вы должны{$inverted ->
    [true]{" "}не
    *[other]{""}
} быть в одной из этих фракций: {$departments}

character-timer-department-insufficient = Вам требуется ещё [color=yellow]{TOSTRING($time, "0")}[/color] минут за фракцию [color={$departmentColor}]{$department}[/color]
character-timer-department-too-high = Вам требуется на [color=yellow]{TOSTRING($time, "0")}[/color] минут меньше во фракции [color={$departmentColor}]{$department}[/color]

character-timer-overall-insufficient = Вам требуется ещё [color=yellow]{TOSTRING($time, "0")}[/color] минут игрового времени
character-timer-overall-too-high = Вам требуется на [color=yellow]{TOSTRING($time, "0")}[/color] минут меньше игрового времени

character-timer-role-insufficient = Вам требуется ещё [color=yellow]{TOSTRING($time, "0")}[/color] минут в роли [color={$departmentColor}]{$job}[/color]
character-timer-role-too-high = Вам требуется на [color=yellow]{TOSTRING($time, "0")}[/color] минут меньше в роли [color={$departmentColor}]{$job}[/color]


## Logic
character-logic-and-requirement-listprefix = {""}
    {$indent}[color=gray]&[/color]{" "}
character-logic-and-requirement = Вы должны{$inverted ->
    [true]{" "}не
    *[other]{""}
} соответствовать [color=red]всем[/color] из [color=gray]этих[/color]: {$options}

character-logic-or-requirement-listprefix = {""}
    {$indent}[color=white]O[/color]{" "}
character-logic-or-requirement = Вы должны{$inverted ->
    [true]{" "}не
    *[other]{""}
} соответствовать [color=red]хотя бы одному[/color] из [color=white]этих[/color]: {$options}

character-logic-xor-requirement-listprefix = {""}
    {$indent}[color=white]X[/color]{" "}
character-logic-xor-requirement = Вы должны{$inverted ->
    [true]{" "}не
    *[other]{""}
} соответствовать [color=red]только одному[/color] из [color=white]этих[/color]: {$options}


## Profile
character-age-requirement = Вы должны{$inverted ->
    [true]{" "}не
    *[other]{""}
} быть в возрасте от [color = желтый]{$min}[/color] до [color=yellow]{$max}[/color] лет

character-backpack-type-requirement = Вы должны {$inverted ->
    [true] не использовать
    *[other] использовать
} [color = коричневый]{$type}[/color] в качестве сумки

character-clothing-preference-requirement = Вы должны {$inverted ->
    [true] не носить
    *[other] носить
} [color = белый]{$type}[/color]

character-gender-requirement = Вы должны {$inverted ->
    [true] не использовать
    *[other] использовать
} местоимения [color = белый]{$gender}[/color]

character-sex-requirement = Вы должны{$inverted ->
    [true]{" "}не
    *[other]{""}
} быть [color = белый]{$sex ->
    [None] без пола
    *[other] {$sex}
}[/color]
character-species-requirement = Вы должны{$inverted ->
    [true]{" "}не
    *[other]{""}
} быть представителем вида {$species}

character-species-job-restriction = Недоступно для {$species}

character-height-requirement = Вы должны{$inverted ->
    [true]{" "}не
    *[other]{""}
} быть {$min ->
    [-2147483648]{$max ->
        [2147483648]{""}
        *[other] ниже [color = {$color}]{$max}[/color]см
    }
    *[other]{$max ->
        [2147483648] выше [color = {$color}]{$min}[/color]см
        *[other] ростом от [color = {$color}]{$min}[/color] до [color=__PH0__]{$max}[/color]см
    }
}

character-width-requirement = Вы должны{$inverted ->
    [true]{" "}не
    *[other]{""}
} быть {$min ->
    [-2147483648]{$max ->
        [2147483648]{""}
        *[other] уже [color = {$color}]{$max}[/color]см
    }
    *[other]{$max ->
        [2147483648] шире [color = {$color}]{$min}[/color]см
        *[other] шириной от [color = {$color}]{$min}[/color] до [color=__PH0__]{$max}[/color]см
    }
}

character-weight-requirement = Вы должны{$inverted ->
    [true]{" "}не
    *[other]{""}
} быть {$min ->
    [-2147483648]{$max ->
        [2147483648]{""}
        *[other] легче [color = {$color}]{$max}[/color]кг
    }
    *[other]{$max ->
        [2147483648] тяжелее [color = {$color}]{$min}[/color]кг
        *[other] весом от [color = {$color}]{$min}[/color] до [color=__PH0__]{$max}[/color]кг
    }
}


character-trait-requirement = Вы должны {$inverted ->
    [true] не иметь
    *[other] иметь
} одну из этих особенностей: {$traits}

character-loadout-requirement = Вы должны {$inverted ->
    [true] не иметь
    *[other] иметь
} один из этих наборов снаряжения: {$loadouts}


character-item-group-requirement = Вы должны {$inverted ->
    [true] иметь {$max} или больше
    *[other] иметь {$max} или меньше
} предметов из группы [color = белый]{$group}[/color]


## Whitelist
character-whitelist-requirement = Вы должны{$inverted ->
    [true]{" "}не
    *[other]{""}
} быть в вайтлисте

## CVar

character-cvar-requirement = 
    Сервер должен{$inverted ->
    [true]{" "}не
    *[other]{""}
} иметь [color={$color}]{$cvar}[/color] установленным на [color={$color}]{$value}[/color].