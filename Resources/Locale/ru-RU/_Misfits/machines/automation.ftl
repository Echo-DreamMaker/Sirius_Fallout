# Sirius Add - Порты устройств для машин автоматизации
signal-port-name-powered = Питание
signal-port-description-powered = Этот порт задействуется высоким или низким уровнем сигнала в зависимости от того, включено питание машины или нет.

# Робот-манипулятор

signal-port-name-input-machine = Предмет: входная машина
signal-port-description-input-machine = Слот автоматизации машины, из которого забираются предметы вместо подбора с пола.

signal-port-name-output-machine = Предмет: выходная машина
signal-port-description-output-machine = Слот автоматизации машины, в который вставляются предметы вместо укладки на пол.

signal-port-name-item-moved = Предмет перемещён
signal-port-description-item-moved = Порт сигнала, который задействуется после того, как манипулятор переместил предмет.

signal-port-name-automation-slot-filter = Предмет: слот фильтра
signal-port-description-automation-slot-filter = Слот автоматизации для фильтра машины автоматизации.

# Контейнер хранения

signal-port-name-automation-slot-storage = Предмет: хранилище
signal-port-description-automation-slot-storage = Слот автоматизации для содержимого контейнера хранения.

signal-port-name-storage-inserted = Вложено
signal-port-description-storage-inserted = Порт сигнала, который задействуется после того, как предмет вложен в контейнер хранения.

signal-port-name-storage-removed = Извлечено
signal-port-description-storage-removed = Порт сигнала, который задействуется после того, как предмет извлечён из контейнера хранения.

# Конструктор / интерактор

signal-port-name-machine-start = Запуск
signal-port-description-machine-start = Порт сигнала для однократного запуска машины.

signal-port-name-machine-repeating = Повтор
signal-port-description-machine-repeating = Порт сигнала для управления автоматическим запуском после завершения работы.

signal-port-name-machine-started = Запущено
signal-port-description-machine-started = Порт сигнала, который задействуется после запуска машины.

signal-port-name-machine-completed = Завершено
signal-port-description-machine-completed = Порт сигнала, который задействуется после завершения работы машиной.

signal-port-name-machine-failed = Ошибка
signal-port-description-machine-failed = Порт сигнала, который задействуется, если машине не удалось запуститься.

# Интерактор

signal-port-name-automation-slot-tool = Предмет: инструмент
signal-port-description-automation-slot-tool = Слот автоматизации для инструмента в руке интерактора.

signal-port-name-alt-interact = Режим альтернативного взаимодействия
signal-port-description-alt-interact = Порт сигнала для переключения режима альтернативного взаимодействия или установки ему высокого/низкого уровня.

signal-port-name-use-in-hand = Режим использования в руке
signal-port-description-use-in-hand = Порт сигнала для переключения режима использования в руке или установки ему высокого/низкого уровня. Цели игнорируются, а к инструменту в руке применяется Z или Alt+Z.

signal-port-name-harm-mode = Режим нанесения урона
signal-port-description-harm-mode = Порт сигнала для переключения режима нанесения урона или установки ему высокого/низкого уровня. Инструмент в руке будет бить цель так, будто интерактор находится в режиме нанесения урона.

signal-port-name-tool-locked = Инструмент заблокирован
signal-port-description-tool-locked = Порт сигнала для переключения блокировки инструмента или установки ей высокого/низкого уровня. Запрещает ронять или подбирать инструмент любым способом.

# Сантехнический насос

signal-port-name-plumbing-input = Сантехника: вход
signal-port-description-plumbing-input = Слот автоматизации сантехники, в который закачиваются жидкости.

signal-port-name-plumbing-output = Сантехника: выход
signal-port-description-plumbing-output = Слот автоматизации сантехники, из которого откачиваются жидкости.

# Автоматизация растворов

signal-port-name-automation-slot-solution = Раствор: реагенты
signal-port-description-automation-slot-solution = Слот автоматизации для раствора реагентов машины, доступный жидкостным насосам.

# Станок

signal-port-name-lathe-print = Напечатать последний рецепт
signal-port-description-lathe-print = Порт сигнала, который при срабатывании печатает последний заданный рецепт.

signal-port-name-lathe-set-recipe = Задать рецепт станка
signal-port-description-lathe-set-recipe = Порт схемы для задания текущего рецепта станка по строковому ID прототипа рецепта.

signal-port-name-lathe-current-recipe = Рецепт станка
signal-port-description-lathe-current-recipe = Порт схемы, передающий строковый ID текущего прототипа рецепта, который будет использован автоматизацией.

signal-port-name-lathe-quantity = Количество предметов
signal-port-description-lathe-quantity = Порт схемы для задания количества следующей партии целым числом. Изначально равно 1, меньшие значения игнорируются.
