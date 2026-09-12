# Requisitions console UI

n14-requisitions-window-title = Терминал поставок RobCo

n14-requisitions-deposit = В бюджет внесено ${$amount}.

# Link status
n14-requisitions-linked = [color=#6abe30][bold]● Подключено[/bold][/color]
n14-requisitions-unlinked = [color=#cf2f2f][bold]● Не подключено — лифт не найден[/bold][/color]

# Platform
n14-requisitions-platform-raise = Поднять платформу
n14-requisitions-platform-lower = Опустить платформу
n14-requisitions-platform-lower-confirm = Подтвердить продажу?
n14-requisitions-platform-busy = Терминал занят
n14-requisitions-platform-missing = Нет платформы

# Sidebar
n14-requisitions-balance = [bold]Бюджет снабжения: ${$balance}[/bold]
n14-requisitions-capacity = Платформа: {$count}/{$capacity}
n14-requisitions-categories-title = Категории
n14-requisitions-category-all = Все
n14-requisitions-category-apparel = Одежда
n14-requisitions-category-equipment = Снаряжение
n14-requisitions-category-security = Охрана
n14-requisitions-category-maintenance = Обслуживание
n14-requisitions-category-medical = Медицина
n14-requisitions-category-provisions = Провизия
n14-requisitions-category-ammunition = Боеприпасы
n14-requisitions-category-supplies = Припасы
n14-requisitions-search-placeholder = Поиск...

# Tabs
n14-requisitions-tab-products = Каталог
n14-requisitions-tab-cart = Корзина
n14-requisitions-tab-sell = Продажа
n14-requisitions-tab-storage = Хранилище
n14-requisitions-tab-pending = Ожидание
n14-requisitions-tab-history = История
n14-requisitions-tab-bounties = Контракты

# Bounties
n14-requisitions-bounties-empty = Контрактов пока нет.
n14-requisitions-bounty-reward-cash = ${$reward}
n14-requisitions-bounty-row = {$item}  [color=#5fbf5f]({$done}/{$amount})[/color]
n14-requisitions-bounty-row-done = [color=#1a5c1a]{$item} ({$amount}/{$amount}) ✓ ВЫПОЛНЕНО[/color]

# Randomized requests
n14-requisitions-random-requests-title = Запросы
n14-requisitions-random-request-row = {$item}  [color=#5fbf5f]({$done}/{$amount})[/color]
n14-requisitions-random-request-cash-reward = [color=#33FF33]+${$amount} бюджет[/color]

# Request/bounty card sections
n14-requisitions-section-request = ЗАПРОС
n14-requisitions-section-reward = НАГРАДА
n14-requisitions-random-request-reroll = Перебросить
n14-requisitions-random-request-reroll-wait = Перебросить ({$time})
n14-requisitions-random-request-refilling = Новый запрос через {$time}
n14-requisitions-random-request-refilling-soon = Новый запрос уже в пути...

# History
n14-requisitions-history-empty = Заказов пока нет.
n14-requisitions-history-row-bought = {$buyer}: куплено {$amount}x {$item} ([color=#cf2f2f]-${$cost}[/color])
n14-requisitions-history-row-sold = продано {$amount}x {$item} ([color=#33FF33]+${$cost}[/color])
n14-requisitions-history-print = Распечатать журнал

# Sell
n14-requisitions-sell-onplatform = На платформе
n14-requisitions-sell-item = {$item}  [color=#5fbf5f]x{$count}[/color]  [color=#33FF33]${$value}[/color]
n14-requisitions-sell-item-trade = {$item}  [color=#5fbf5f]x{$count}[/color]  [color=#33FF33]обмен[/color]
n14-requisitions-sell-total = [bold]Оценка всего: ${$value}[/bold]
n14-requisitions-sell-refresh = Обновить
n14-requisitions-sell-empty = На платформе нет ничего ценного.
n14-requisitions-sell-catalog-title = Принимаемые товары
n14-requisitions-sell-catalog-empty = Этот терминал не принимает товары обратно.
n14-requisitions-sell-catalog-row = {$item} → {$reward}

# Storage
n14-requisitions-storage-empty = Хранилище пусто. Обменяйте товары на платформе, чтобы заполнить его.
n14-requisitions-storage-item = {$item}  [color=#5fbf5f]x{$count}[/color]
n14-requisitions-storage-bring-up = Поднять
n14-requisitions-storage-withdraw = Поднять всё

# Product cards
n14-requisitions-products-empty = Товары недоступны.
n14-requisitions-card-cost = ${$cost}
n14-requisitions-card-no-description = Описание недоступно.
n14-requisitions-stock-left = (осталось {$left})
n14-requisitions-remove-tooltip = Убрать один из корзины
n14-requisitions-add-tooltip = Добавить один в корзину
n14-requisitions-max = Макс

# Cart
n14-requisitions-cart-empty = Ваша корзина пуста.
n14-requisitions-cart-filter-empty = Ни один товар в корзине не соответствует запросу.
n14-requisitions-cart-category-empty = В этой категории нет товаров в корзине.
n14-requisitions-cart-insufficient-funds = Недостаточно бюджета для этой корзины.
n14-requisitions-cart-insufficient-capacity = На платформе недостаточно места для этой корзины.
n14-requisitions-cart-row-cost = ${$cost}
n14-requisitions-cart-total = [bold]Итого: ${$total}[/bold]
n14-requisitions-cart-clear = Очистить
n14-requisitions-buy = Купить
n14-requisitions-buy-confirm = Подтвердить?

# Pending orders
n14-requisitions-pending-empty = Нет ожидающих заказов.
n14-requisitions-pending-filter-empty = Ни один ожидающий заказ не соответствует запросу.
n14-requisitions-pending-category-empty = В этой категории нет ожидающих заказов.
n14-requisitions-pending-quantity = x{$amount}