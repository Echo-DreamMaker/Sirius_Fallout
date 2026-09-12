objectives-round-end-result = { $count ->
    [one] Был один { $agent }.
    [few] Было { $count } { $agent }.
    *[other] Было { $count } { $agent }.
}

objectives-round-end-result-in-custody = { $custody } из { $count } { $agent } были арестованы.

objectives-player-user-named = [color=White]{ $name }[/color] ([color=gray]{ $user }[/color])
objectives-player-named = [color=White]{ $name }[/color]

objectives-no-objectives = { $custody }{ $title } – { $agent }.
objectives-with-objectives = { $custody }{ $title } – { $agent } со следующими целями:

objectives-objective-success = { $objective } | [color=green]Успех![/color] ({ TOSTRING($progress, "P0") })
objectives-objective-partial-success = { $objective } | [color=yellow]Частичный успех![/color] ({ TOSTRING($progress, "P0") })
objectives-objective-partial-failure = { $objective } | [color=orange]Частичный провал![/color] ({ TOSTRING($progress, "P0") })
objectives-objective-fail = { $objective } | [color=red]Провал![/color] ({ TOSTRING($progress, "P0") })

objectives-in-custody = [bold][color=red]| АРЕСТОВАН | [/color][/bold]

objective-issuer-ncr = [color=#cc2f2f]НКР[/color]
objective-issuer-brotherhoodofsteel = [color=#4f81bd]Братство Стали[/color]
objective-issuer-caesarlegion = [color=#8B0000]Легион Цезаря[/color]
objective-issuer-geometerofblood = [color=#b22222]Кровяной Геометр[/color]
objective-issuer-vault = [color=#FFD700]Убежище[/color]
objective-issuer-townsfolk = [color=#8FBC8F]Город[/color]
objective-issuer-playerrobot = [color=#607d8b]Роботы[/color]
objective-issuer-playersupermutant = [color=#6b8e23]Сверхмутанты[/color]
objective-issuer-playerraider = [color=#c0522a]Рейдеры[/color]
