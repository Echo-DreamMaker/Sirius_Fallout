ent-PrizeTicket = билет на караван
   .desc = Билет, используемый для обмена в специальном «торговом автомате». Позволяет получить довольно мощное оружие, если у вас достаточно билетов.
ent-PrizeTicket1 = { ent-PrizeTicket }
   .suffix = 1
   .desc = { ent-PrizeTicket.desc }
ent-PrizeTicket10 = { ent-PrizeTicket }
   .suffix = 10
   .desc = { ent-PrizeTicket.desc }
ent-PrizeTicket30 = { ent-PrizeTicket }
   .suffix = 30
   .desc = { ent-PrizeTicket.desc }
ent-PrizeTicket60 = { ent-PrizeTicket }
   .suffix = 60
   .desc = { ent-PrizeTicket.desc }
nc-store-window-title = Торговый терминал
nc-store-select-category = Выберите категорию
nc-store-search-placeholder = Поиск предметов...
nc-store-footer-balance = Баланс:
nc-store-tab-buy = Купить
nc-store-tab-sell = Продавать
nc-store-tab-contracts = Контракты
nc-store-cat-ready-short = Готовый
nc-store-cat-crate-short = В ящике
nc-store-cat-ready-full = Готов к продаже
nc-store-cat-crate-full = Готов к продаже (в ящике)
nc-store-category-fallback = Разное
nc-store-mass-sell-button = Продать содержимое ящика
nc-store-mass-sell-tooltip = Возможность быстрой продажи всего содержимого.
    Conditions:
    - The crate must be closed
    - You must be pulling the crate
nc-store-mass-sell-tooltip-with-reward = { nc-store-mass-sell-tooltip }

    Estimated value: { $reward }
nc-store-only-mass-sell = Этот предмет можно продать только оптом в закрытом ящике.
nc-store-show-more = Показать больше ({ $count })
nc-store-prompt-select-category = Пожалуйста, выберите категорию слева.
nc-store-empty-search = По вашему запросу ничего не найдено.
nc-store-empty-category-search = В этой категории нет товаров, соответствующих вашему запросу.
nc-store-search-results-buy = Результаты поиска (Купить): { $count }
nc-store-search-results-sell = Результаты поиска (Продать): { $count }
nc-store-no-stock = Распродано
nc-store-buying-finished = Достигнут лимит
nc-store-remaining = Осталось: { $count }
nc-store-will-buy = Обязательно: { $count }
nc-store-owned = У вас есть: { $count }
nc-store-no-access = Ошибка доступа
nc-store-contracts-empty = Действующих контрактов пока нет. Зайдите позже.
nc-store-difficulty-easy = Легкий
nc-store-difficulty-medium = Середина
nc-store-difficulty-hard = Жесткий
nc-store-difficulty-bronze = Бронза
nc-store-difficulty-iron = Железо
nc-store-difficulty-silver = Серебро
nc-store-difficulty-gold = Золото
nc-store-difficulty-mithril = Мифрил
nc-store-difficulty-diamond = Алмаз
nc-store-contract-title = Контракт ({ $difficulty })
nc-store-contract-badge-single = Один раз
nc-store-contract-badge-single-tooltip = 
    This contract can be completed only once per shift.
    After completion it disappears from the list.
nc-store-contract-goals-header = Цели заказа:
nc-store-contract-reward-header = Награда:
nc-store-contract-items-header = Предметы:
nc-store-contract-action-claim = Полный контракт
nc-store-contract-action-claim-progress = Отправить часть ({ $progress }/{ $required })
nc-store-contract-action-can-claim = Готов сдать
nc-store-contract-action-not-done = Не завершено
nc-store-contract-claim-tooltip-single = Завершите разовый контракт и получите полную награду.
nc-store-contract-claim-tooltip-repeatable = Сдайте текущий прогресс по контракту и получите награду.
nc-store-contract-claim-tooltip-not-done = Условия контракта пока не выполнены. Недостаточно предметов.
nc-store-contract-completed = Контракт успешно завершен!
nc-store-contract-goal-line = { $item }: { $count } шт.
nc-store-contract-goal-progress-line = { $item }: { $progress }/{ $count } шт.
nc-store-contract-progress-line = Ход завершения: { $progress } из { $required }
nc-store-currency-format = {$amount } {$currency }
nc-store-contract-title-pretty = Контракт: { $difficulty } - { $goal }
nc-store-contract-title-pretty-nogoal = Контракт: { $difficulty }

nc-store-contract-desc-default = Выполните условия контракта и получите награду.

# Misfits Add — 6-tier contract system UI strings
nc-contract-tier-first-access = Вы зарегистрировались в сети караванов — бронзовый уровень разблокирован!
nc-contract-tier-unlocked = Уровень { $tier } разблокирован! Продолжайте настаивать.
nc-store-tab-tier-hall-of-fame = зал славы
nc-store-hall-of-fame-empty = В этом раунде еще никто не завершил контракты.
nc-store-hall-of-fame-entry = { $name } — { $tier } (завершено { $count })
nc-contract-tier-locked = Заблокировано — завершите контракты уровня { $prevTier }, чтобы разблокировать.
nc-contract-card-locked-hint = Уровень { $tier } — ещё не разблокирован.
nc-store-contract-desc-generated = Обязательно: { $goals }

nc-store-contract-goal-inline = { $item } x{ $count }

nc-store-unknown-item = ???

nc-store-proto-tooltip-name-only = { $name }
nc-store-proto-tooltip = { $name }
    { $desc }

nc-store-contract-reward-none = Вознаграждение не указано
nc-store-contract-reward-item-line = { $item } x{ $count }

nc-store-contract-badge-completed = ЗАВЕРШЕННЫЙ
nc-store-contract-badge-completed-tooltip = Контракт выполнен – вы можете получить награду.

# #Misfits Add - Store popup messages for UI open feedback
nc-store-popup-no-access = У вас нет доступа к этому терминалу.
nc-store-popup-too-far = Вы находитесь слишком далеко от терминала.
nc-store-popup-in-use = Этот терминал в настоящее время используется.
nc-store-popup-crate-open = Сначала закройте ящик.
nc-store-popup-no-crate = Чтобы продать, вам нужно вытащить закрытый ящик.
nc-store-popup-invalid-listing = Недействительный листинг.
nc-store-popup-transaction-failed = Транзакция не удалась.
nc-store-popup-crate-too-far = Ящик находится слишком далеко.

nc-store-difficulty-road-kill = Труп на дороге
nc-store-difficulty-lazy-lizard = Ленивая ящерица
nc-store-difficulty-junktown-rat = Крыса Джанктауна
nc-store-difficulty-hub-mercenary = Наёмник из Хаба
nc-store-difficulty-bunker-buster = Разрушитель бункеров
nc-store-difficulty-wasteland-legend = Легенда Пустоши