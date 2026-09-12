shuttle-pilot-start = Пилотирование начато
shuttle-pilot-end = Пилотирование прекращено

shuttle-console-in-ftl = Уже в БСС
shuttle-console-mass = Слишком большой для БСС
shuttle-console-prevent = Вы не можете пилотировать этот корабль

# NAV

shuttle-console-display-label = Дисплей

shuttle-console-position = Координаты:
shuttle-console-position-value = { $X }, { $Y }
shuttle-console-orientation = Азимут:
shuttle-console-orientation-value  = { $angle }
shuttle-console-linear-velocity = Линейная скорость:
shuttle-console-linear-velocity-value = { $X }, { $Y }
shuttle-console-angular-velocity = Угловая скорость:
shuttle-console-angular-velocity-value = { $angularVelocity }

shuttle-console-unknown = Неизвестно
shuttle-console-iff-label = { $name } ({ $distance } м)
shuttle-console-exclusion = Зона отчуждения

shuttle-console-nav-settings = Настройки
shuttle-console-iff-toggle = Показ системы опознавания
shuttle-console-dock-toggle = Показ стыковочных портов

# MAP

shuttle-console-ftl-label = Статус БСС
shuttle-console-ftl-state-Available = Доступно
shuttle-console-ftl-state-Starting = Запуск
shuttle-console-ftl-state-Travelling = В пути
shuttle-console-ftl-state-Arriving = Прибытие
shuttle-console-ftl-state-Cooldown = Перезарядка
shuttle-console-ftl-state-Invalid = Ошибка

shuttle-console-map-settings = Настройки
shuttle-console-ftl-button = БСС
shuttle-console-map-rebuild = Сканировать на
    наличие объектов
shuttle-console-map-beacons = Показ маяков

shuttle-console-no-signal = Нет сигнала

shuttle-console-map-objects = Объекты в секторе

# DOCK
shuttle-console-docked = Пристыкованные объекты

shuttle-console-view = Выбрать
shuttle-console-undock = Отстыковать
shuttle-console-dock = Пристыковать
shuttle-console-docks-label = Стыковочные порты

shuttle-console-undock-fail = Не удалось отстыковаться
shuttle-console-dock-fail = Не удалось пристыковаться

genetics-console-chat-scanning = СКАНИРОВАНИЕ ОБЪЕКТА
genetics-console-chat-scan-failed = СКАНИРОВАНИЕ ОБЪЕКТА ПРОВАЛЕНО
genetics-console-chat-scanned = НОВЫЙ ОБЪЕКТ ОТСКАНИРОВАН
genetics-console-chat-sequencing = СЕКВЕНИРОВАНИЕ ГЕНОМА
genetics-console-chat-sequence-failed = НЕ УДАЛОСЬ СЕКВЕНИРОВАТЬ ГЕНОМ
genetics-console-chat-genetic-damage = ГЕНОМ ПОВРЕЖДЁН ДО НЕЧИТАЕМОСТИ
genetics-console-chat-sequenced = СЕКВЕНИРОВАНИЕ ГЕНОМА ЗАВЕРШЕНО
genetics-console-chat-combining = КОМБИНИРОВАНИЕ МУТАЦИЙ
genetics-console-chat-combine-failed = КОМБИНАЦИЯ МУТАЦИЙ ПРЕРВАНА
genetics-console-chat-combine-none = КОМБИНАЦИЯ НЕ НАЙДЕНА
genetics-console-chat-combine-present = КОМБИНАЦИЯ УЖЕ СУЩЕСТВУЕТ
genetics-console-chat-combined = КОМБИНАЦИЯ МУТАЦИЙ ЗАВЕРШЕНА
genetics-console-chat-applying-enzymes = НАНЕСЕНИЕ УНИКАЛЬНЫХ ФЕРМЕНТОВ
genetics-console-chat-apply-enzymes-failed = НЕ УДАЛОСЬ НАНЕСТИ УНИКАЛЬНЫЕ ФЕРМЕНТЫ
genetics-console-chat-applied-enzymes = УНИКАЛЬНЫЕ ФЕРМЕНТЫ НАНЕСЕНЫ

genetics-console-radio-message = {CAPITALIZE($mutation)} был секвенирован, на сервер исследований добавлено {$points} очков!

genetics-console-damages-you = Секвенирование терпит катастрофическую неудачу и повреждает ваш геном!
genetics-console-damages-others = Консоль генетики терпит катастрофическую неудачу и повреждает геном объекта!

genetics-console-linking-you = {CAPITALIZE($user)} начинает синхронизацию вас с {POSS-ADJ($user)} {$scanner}
genetics-console-linking-others = {CAPITALIZE($user)} начинает синхронизацию {$target} с {POSS-ADJ($user)} {$scanner}
genetics-console-linked = Объект успешно синхронизирован

genetics-console-window-title = Генетическая консоль 3000
genetics-scanner-window-title = Генетический сканер 4000

genetics-console-heading-scanner = Медицинский сканер
genetics-console-no-scanner = Медицинский сканер не подключён!
genetics-console-no-subject = В медицинском сканере не обнаружен объект.
genetics-console-name = Имя
genetics-console-status = Статус
genetics-console-integrity = Целостность
genetics-console-instability = Нестабильность
genetics-console-scramble = Перемешать ДНК
genetics-console-scramble-cooldown = Перемешивание на перезарядке ({$cooldown}с)

genetics-console-inserted-disk = Вставленный диск:
genetics-console-loaded-mutation = Загруженная мутация:
genetics-console-no-disk = Диск не вставлен
genetics-console-print-item = Напечатать {$item} ({$cost})

genetics-console-sequencer-no-subject = Объект не обнаружен
genetics-console-sequencer-not-scanned = Отсканируйте объект, чтобы начать.
genetics-console-sequencer-no-sequences = ДНК объекта пуста!
genetics-console-sequences = Последовательности
genetics-console-sequence-info = Информация о последовательности
genetics-console-scan = Сканировать!
genetics-console-print-scan = Распечатать сканирование
genetics-console-genome-sequencer = Секвенатор генома™
genetics-console-sequencer-no-sequence-selected = Выберите последовательность для работы.
genetics-console-sequence-text = [{$rarity}] {$number}
genetics-console-mutation-name = Имя:
genetics-console-mutation-desc = Описание:
genetics-console-mutation-instability = Нестабильность:
genetics-console-mutation-name-placeholder = Мутация {$number}
genetics-console-write-mutation = Сохранить на диск
genetics-console-print-sequence = Напечатать основания
genetics-console-sequencer-tip = Совет: Ctrl+Клик по основанию устанавливает X. Правый клик перебирает в обратном порядке.
genetics-console-begin-sequencing = Начать секвенирование
genetics-console-reset-sequence = Сбросить последовательность

genetics-console-examined = В консоли хранится [bold]{$biomass}[/bold] биомассы.
genetics-console-missing-biomass = В консоли недостаточно биомассы для печати!

genetics-console-combine-button = Комбинировать!
genetics-console-combine-catalyst = Катализаторная мутация: {$mutation}
genetics-console-combine-results = Эту мутацию можно использовать для создания {$results}!
genetics-console-combine-no-results = Эту мутацию нельзя с чем-либо комбинировать.
genetics-console-disk-empty = На диске нет мутации!

genetics-console-scanned-mob = Отсканированный объект
genetics-console-save-enzymes = Сохранить на диск
genetics-console-disk-enzymes = Уникальные ферменты диска
genetics-console-apply-enzymes = Применить ферменты к объекту
genetics-console-color = R{$r}/G{$g}/B{$b}

# Состояние объекта (mob-state-<MobState>) — используется напрямую кодом консоли генетики
mob-state-Alive = Жив
mob-state-SoftCritical = Критическое состояние
mob-state-Critical = Тяжёлое состояние
mob-state-Dead = Мёртв
