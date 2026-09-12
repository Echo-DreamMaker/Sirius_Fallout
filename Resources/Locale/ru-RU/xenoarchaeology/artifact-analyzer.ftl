analysis-console-menu-title = Аналитическая консоль широкого спектра модель 3
analysis-console-server-list-button = Сервер
analysis-console-extract-button = Извлечь очки

analysis-console-info-no-scanner = Анализатор не подключён! Пожалуйста, подключите его с помощью мультитула.
analysis-console-info-no-artifact = Артефакт не найден! Поместите артефакт на платформу  для получения данных о узлах.
analysis-console-info-ready = Все системы запущены. Сканирование готово.

analysis-console-no-node = Выберите узел для просмотра
analysis-console-info-id = [font="Monospace" size=11]ID:[/font]
analysis-console-info-id-value = [font="Monospace" size=11][color=yellow]{ $id }[/color][/font]
analysis-console-info-class = [font="Monospace" size=11]Класс:[/font]
analysis-console-info-class-value = [font="Monospace" size=11]{ $class }[/font]
analysis-console-info-locked = [font="Monospace" size=11]Статус:[/font]
analysis-console-info-locked-value = [font="Monospace" size=11][color={ $state ->
    [0] red]Заблокирован
    [1] lime]Разблокирован
    *[2] plum]Активен
}[/color][/font]
analysis-console-info-durability = [font="Monospace" size=11]Прочность:[/font]
analysis-console-info-durability-value = [font="Monospace" size=11][color={ $color }]{ $current }/{ $max }[/color][/font]
analysis-console-info-effect = [font="Monospace" size=11]Эффект:[/font]
analysis-console-info-effect-value = [font="Monospace" size=11][color=gray]{ $state ->
    [true] { $info }
    *[false] Разблокируйте узлы для получения информации
}[/color][/font]
analysis-console-info-trigger = [font="Monospace" size=11]Стимуляторы:[/font]
analysis-console-info-triggered-value = [font="Monospace" size=11][color=gray]{ $triggers }[/color][/font]
analysis-console-info-scanner = Сканирование...
analysis-console-info-scanner-paused = Пауза.
analysis-console-progress-text = { $seconds ->
    [one] T-{ $seconds } секунда
    [few] T-{ $seconds } секунды
    *[other] T-{ $seconds } секунд
}

analysis-console-extract-value = [font="Monospace" size=11][color=orange]Узел { $id } (+{ $value })[/color][/font]
analysis-console-extract-none = [font="Monospace" size=11][color=orange] У разблокированых узлов не осталось очков для извлечения [/color][/font]
analysis-console-extract-sum = [font="Monospace" size=11][color=orange]Всего изучено: { $value }[/color][/font]

analyzer-artifact-extract-popup = Поверхность артефакта мерцает энергией!

analysis-console-scan-button = Сканировать

analysis-console-scan-tooltip-info = Сканируйте артефакты, чтобы получить информацию об их структуре.

analysis-console-print-button = Печать

analysis-console-print-tooltip-info = Распечатать текущую информацию об артефакте.

analysis-console-extract-button-info = Извлечь очки из артефакта на основе вновь изученных узлов.

analysis-console-bias-up = Вверх

analysis-console-bias-down = Вниз

analysis-console-bias-button-info-up = Переключает предпочтение артефакта при перемещении между узлами. Вверх — к узлам с меньшей глубиной.

analysis-console-bias-button-info-down = Переключает предпочтение артефакта при перемещении между узлами. Вниз — к узлам с большей глубиной.

analysis-console-info-depth = ГЛУБИНА: { $depth }

analysis-console-info-triggered-false = АКТИВИРОВАН: НЕТ

analysis-console-info-triggered-true = АКТИВИРОВАН: ДА

analysis-console-info-edges = СВЯЗИ: { $edges }

analysis-console-info-value = НЕИЗВЛЕЧЁННОЕ_ЗНАЧЕНИЕ: { $value }

analysis-console-no-artifact-placed = На анализаторе нет артефакта.

analysis-console-no-points-to-extract = Нет очков для извлечения.

analysis-console-no-server-connected = Невозможно извлечь. Сервер не подключён.

analysis-console-print-popup = Консоль распечатала отчёт.

analysis-report-title = Отчёт об артефакте: Узел { $id }

analyzer-artifact-component-upgrade-analysis = длительность анализа
