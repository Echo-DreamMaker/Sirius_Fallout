#Sirius Fix: полная переработка машинного перевода системы тикетов (единая терминология, естественные формулировки)
# Misfits Add — Ticket system localization for admin help & mentor help

ticket-system-claim-button = Взять
ticket-system-unclaim-button = Вернуть в очередь
ticket-system-resolve-button = Решить
ticket-system-reopen-button = Открыть заново
ticket-system-status-open = Открыто — не взято
ticket-system-status-claimed = Взял {$admin}
ticket-system-status-resolved = Решил {$admin}
ticket-system-created = [{$type} ТИКЕТ #{$id}] {$player} создал новый тикет.
ticket-system-claimed = [{$type} ТИКЕТ #{$id}] Взял {$admin}.
ticket-system-unclaimed = [{$type} ТИКЕТ #{$id}] {$admin} вернул тикет в очередь — тикет снова открыт.
ticket-system-resolved = [{$type} ТИКЕТ #{$id}] Решил {$admin}.
ticket-system-reopened = [{$type} ТИКЕТ #{$id}] Открыл заново {$admin}.
ticket-system-auto-claimed = [{$type} ТИКЕТ #{$id}] {$admin} автоматически взял тикет при первом ответе.
ticket-system-auto-resolved-disconnect = [{$type} ТИКЕТ #{$id}] Автоматически решён — игрок отключился.
# #Misfits Add - Player-facing resolve notification (sent only to the player, not admin panel)
ticket-system-resolved-with-cooldown = Ваш тикет решён. Если нужна дополнительная помощь, подождите 1 минуту, прежде чем открывать новый.
# #Misfits Add - Player-facing cooldown block message (sent only to the player when they attempt to re-ticket too quickly)
ticket-system-cooldown-blocked = Пожалуйста, подождите минуту после решения, прежде чем создавать новый тикет.
ticket-system-toast-new-title = Новый тикет
ticket-system-toast-new-body = Тикет #{$id} от {$player}
ticket-system-toast-claimed-title = Тикет взят
ticket-system-toast-claimed-body = Тикет #{$id} взял {$admin}
ticket-system-toast-resolved-title = Тикет решён
ticket-system-toast-resolved-body = Тикет #{$id} решил {$admin}
ticket-system-toast-reopened-title = Тикет открыт заново
ticket-system-toast-reopened-body = Тикет #{$id} от {$player} открыт заново
ticket-system-must-claim-first = Прежде чем отвечать, нужно взять этот тикет. Нажмите кнопку «Взять» выше.
ticket-system-claim-to-reply = Взять тикет, чтобы ответить…
ticket-system-reminder = [ТИКЕТЫ] Ожидают невзятые тикеты: {$count}. Откройте панель «Помощь», чтобы взять их.
ticket-system-reminder-ahelp = [AHELP ТИКЕТ] Ожидают невзятые тикеты: {$count}. Откройте панель «Помощь», чтобы взять их.
ticket-system-reminder-mhelp = [MHELP ТИКЕТ] Ожидают невзятые тикеты: {$count}. Откройте панель «Помощь», чтобы взять их.

# Quick Reply window (opened from player info panel Ahelp button)
ticket-system-quick-reply-title = AHelp — {$player}
ticket-system-quick-reply-target = Сообщение для {$player}
ticket-system-quick-reply-placeholder = Введите сообщение для отправки…

# Audit Log window
ticket-audit-log-button = Журнал аудита
ticket-audit-log-window-title = Журнал аудита тикетов помощи

# #Misfits Add - expanded filter panel locale keys
ticket-audit-filter-player-label = Игрок (имя или ID):
ticket-audit-filter-player-placeholder = Введите имя или GUID…
ticket-audit-filter-admin-label = Имя администратора:
ticket-audit-filter-admin-placeholder = Поиск по имени администратора…
ticket-audit-filter-date-from = С даты:
ticket-audit-filter-date-to = По дату:
ticket-audit-filter-date-placeholder = ГГГГ-ММ-ДД
ticket-audit-filter-search = Поиск
ticket-audit-filter-clear = Сбросить фильтры
ticket-audit-filter-month-this = Этот месяц
ticket-audit-filter-month-last = Прошлый месяц
ticket-audit-filter-month-two-ago = Позапрошлый месяц

# Tab titles
ticket-audit-tab-events = События аудита
ticket-audit-tab-stats = Статистика администраторов
ticket-audit-tab-actions = Действия администрации
ticket-audit-tab-player-logs = Логи игрока
ticket-audit-actions-requires-logs = Для просмотра этих записей требуется право на админ-логи.
ticket-audit-player-logs-select = Введите точное имя игрока или GUID и нажмите «Поиск», чтобы просмотреть его логи.

# Events list headers
ticket-audit-header-time = Время
ticket-audit-header-id = Тикет
ticket-audit-header-event = Событие
ticket-audit-header-player = Игрок
ticket-audit-header-admin = Админ

# Pagination
ticket-audit-pagination-prev = ← Назад
ticket-audit-pagination-next = Вперёд →
ticket-audit-pagination-label = Страница {$page} из {$total} (всего событий: {$count})
ticket-audit-pagination-placeholder = Загрузка…

# Statistics tab
ticket-audit-stats-label = Статистика обработки тикетов администраторами (за выбранный период)
ticket-audit-stats-admin = Администратор
ticket-audit-stats-resolved = Решено
ticket-audit-stats-claimed = Взято
ticket-audit-stats-ratio = Доля
ticket-audit-stats-empty = За выбранный период статистики нет.
ticket-audit-stats-ahelp-section = Помощь администратора (AHELP)
ticket-audit-stats-mhelp-section = Помощь наставника (MHELP)
ticket-audit-stats-summary-ahelp = AHELP: создано {$created} — отвечено {$answered} ({$pct})
ticket-audit-stats-summary-mhelp = MHELP: создано {$created} — отвечено {$answered} ({$pct})

# General
ticket-audit-log-empty = Событий по тикетам не найдено.

# Chat history window
ticket-chat-history-window-title = Тикет #{$id} — {$player}
ticket-chat-history-loading = Загрузка истории чата…
ticket-chat-history-empty = Сообщений по этому тикету не найдено.

# Audit event type display names
ticket-audit-event-created = Создан
ticket-audit-event-claimed = Взят
ticket-audit-event-unclaimed = Возвращён в очередь
ticket-audit-event-resolved = Решён
ticket-audit-event-reopened = Открыт заново
ticket-audit-event-auto-resolved = Автоматически решён

# #Misfits Add - Player search bar placeholder and open ticket count label
ticket-search-player-placeholder = Написать игроку…
ticket-open-count = Открыто: { $count }
