ent-PrizeTicket = билет на караван
   .desc = Билет, используемый для обмена в специальном «торговом автомате». Позволяет получить довольно мощное оружие, если у вас достаточно билетов.
ent-PrizeTicket1 = { ent-PrizeTicket }
   .suffix = 1
    .desc = призовой билет, который можно обменять на стойке призов.
ent-PrizeTicket10 = { ent-PrizeTicket }
   .suffix = 10
   .desc = призовой билет, который можно обменять на стойке призов.
ent-PrizeTicket30 = { ent-PrizeTicket }
   .suffix = 30
   .desc = призовой билет, который можно обменять на стойке призов.
ent-PrizeTicket60 = { ent-PrizeTicket }
   .suffix = 60
   .desc = призовой билет, который можно обменять на стойке призов.
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
# Sirius Add - Недостающие ранги сложности магазина
nc-store-difficulty-servus = Серв
nc-store-difficulty-plebeian = Плебей
nc-store-difficulty-auxiliary = Ауксиляр
nc-store-difficulty-legionary = Легионер
nc-store-difficulty-decanus = Декан
nc-store-difficulty-centurion = Центурион
nc-store-difficulty-tribal = Племенной
nc-store-difficulty-settler = Поселенец
nc-store-difficulty-citizen = Гражданин
nc-store-difficulty-caravaneer = Караванщик
nc-store-difficulty-caravan-master = Мастер караванов
nc-store-difficulty-brahmin-baron = Брамин-барон

# Sirius: caravan contract localization
nc-contract-caravan_contract_N14MaterialThread-name = Протяни руку помощи
nc-contract-caravan_contract_N14MaterialThread-desc = Беженцы из зон конфликта прибыли в лохмотьях. Последователям Апокалипсиса нужны нитки, чтобы починить их одежду.
nc-contract-caravan_contract_N14FloraProduceWildBrocFlower-name = Цветы для нуждающихся
nc-contract-caravan_contract_N14FloraProduceWildBrocFlower-desc = Последователям Апокалипсиса нужно пополнить запасы медикаментов. Доставьте цветы брока для измотанных волонтёров.
nc-contract-caravan_contract_MaterialCloth-name = Портной, солдат, шпион
nc-contract-caravan_contract_MaterialCloth-desc = Штату Дейглоу нужна новая партия формы для рекрутов, но материала не хватает. Срочно поставьте ткань для униформы НКР.
nc-contract-caravan_contract_N14FloraLogTimber-name = Недружелюбный Север
nc-contract-caravan_contract_N14FloraLogTimber-desc = Городу Арройо нужны дрова, чтобы жители пережили приближающиеся холода. Помогите НКР позаботиться о своих людях.
nc-contract-caravan_contract_N14FoodDogfood-name = Спустить псов войны
nc-contract-caravan_contract_N14FoodDogfood-desc = Крупное поселение Джанктаун готовит собак для армии Новой Калифорнийской Республики, но запасы собачьего корма на исходе.
nc-contract-caravan_contract_MaterialWoodPlank1-name = Всегда смотри на светлую сторону
nc-contract-caravan_contract_MaterialWoodPlank1-desc = Легиону Цезаря нужна древесина для новых распятий для врагов — а врагов у них хватает. Скорее всего, это будет постоянная работа.
nc-contract-caravan_contract_N14Scraps-name = Военно-промышленный комплекс? Да всё просто!
nc-contract-caravan_contract_N14Scraps-desc = Братство Стали намерено активнее изымать опасные технологии у всех остальных. Им нужны материалы, чтобы кормить тающие запасы боеприпасов.
nc-contract-caravan_contract_N14Scrap1-name = Переработка ради победы
nc-contract-caravan_contract_N14Scrap1-desc = Одному из оставшихся убежищ Vault-Tec нужен металлолом для ремонта бака водоочистителя. За материал они заплатят щедро.
nc-contract-caravan_contract_N14FloraProduceWildCoyoteTobacco-name = Выкурить улей
nc-contract-caravan_contract_N14FloraProduceWildCoyoteTobacco-desc = Местные рейдеры-Саранча обычно нападают без предупреждения, но готовы поторговать. Доставьте им койотовый табак — и несколько наших караванов пройдут спокойно.
nc-contract-caravan_contract_N14FloraProduceWildCannabis-name = Высокая возможность
nc-contract-caravan_contract_N14FloraProduceWildCannabis-desc = Местный шаман почти исчерпал запасы дикой конопли и утверждает, что без неё не слышит голоса богов. Поддержите его видения — и, возможно, торговля расцветёт.
nc-contract-caravan_contract_MisfitsFloraProduceWildKokoa-name = Иногда всё дело в шоколаде
nc-contract-caravan_contract_MisfitsFloraProduceWildKokoa-desc = Кто не любит шоколад? Поставьте стручки какао — а остальное они сделают сами.
nc-contract-caravan_contract_N14FloraProduceWildCarrot-name = Что нового, док?
nc-contract-caravan_contract_N14FloraProduceWildCarrot-desc = Последователям Апокалипсиса нужно накормить голодных беженцев из ближайших зон конфликта. Они массово закупают дикую морковь.
nc-contract-caravan_contract_N14WeaponSniper556VarmintRifle-name = К оружию, соотечественники!
nc-contract-caravan_contract_N14WeaponSniper556VarmintRifle-desc = Небольшое поселение Сак-Таун в НКР расширяет ранчо кротокрысов, а значит, нужно больше солдат для охраны стада. Пришлите винтовки!
nc-contract-caravan_contract_N14FoodMeatRadRaw-name = И это вы называете едой?
nc-contract-caravan_contract_N14FoodMeatRadRaw-desc = Исправительное учреждение НКР нужно кормить заключённых. Не волнуйтесь, мясо сначала приготовят. Небольшое улучшение.
nc-contract-caravan_contract_N14Weapon9mm-name = Не повезло
nc-contract-caravan_contract_N14Weapon9mm-desc = Вышибала одного казино в Нью-Рино уронил пистолет в унитаз. Лезть за ним он не собирается, поэтому нужна замена — пока босс не узнал.
nc-contract-caravan_contract_N14BrassKnife-name = Ножом по мясу!
nc-contract-caravan_contract_N14BrassKnife-desc = Профессиональному повару в Нью-Вегасе нужны новые ножи для кухни. Довоенные инструменты не годятся — сколотые, тупые и ржавые; ему нужен свежий латунный нож.
nc-contract-caravan_contract_N14FloraProduceWildCaveFungusRad-name = Радикальная наука
nc-contract-caravan_contract_N14FloraProduceWildCaveFungusRad-desc = Учёный-отступник Vault-Tec считает, что исследования облучённых грибов приведут к будущему без радиации. За образцы он готов щедро заплатить.
nc-contract-caravan_contract_N14MaterialLeather1-name = Ваш патриотический долг
nc-contract-caravan_contract_N14MaterialLeather1-desc = Одному из убежищ нужен материал для новых комбинезонов жителей. Обычно караванная сеть с этими фанатиками не работает, но на этот раз у них есть крышки, а не довоенные доллары.
nc-contract-caravan_contract_N14MobGhoulFeral-name = Долгое жестокое прощание
nc-contract-caravan_contract_N14MobGhoulFeral-desc = Последователи Апокалипсиса хотят понять, почему гули дичают и можно ли это остановить или обратить. Отправьте им свежий материал для исследований.
nc-contract-caravan_contract_N14MobRaiderPsycho-name = Охота за головами ради прибыли
nc-contract-caravan_contract_N14MobRaiderPsycho-desc = Рейдеры остаются проблемой, а их нападения на туристов НКР достигли предела. Теперь за местных бандитов назначена награда — пока избиратели не успокоятся.
nc-contract-caravan_contract_N14DrinkNukaCola-name = Кругом вода, а пить нечего!
nc-contract-caravan_contract_N14DrinkNukaCola-desc = Даже спустя два века после прекращения выпуска «Ядер-Колы» у неё всё ещё есть фанатичные поклонники. Эти безумцы заплатят караванной сети абсурдные деньги за одну бутылку.
nc-contract-caravan_contract_N14MobRaiderBoss-name = Калифорнийская хандра
nc-contract-caravan_contract_N14MobRaiderBoss-desc = Местные главари рейдеров перешли от нападений на бродяг и поселенцев НКР к засадам на рейнджеров и солдат. Теперь НКР платит награду за лидеров бандитов.
nc-contract-caravan_contract_N14MobCazador-name = Борьба с вредителями
nc-contract-caravan_contract_N14MobCazador-desc = Гнёзда касадоров продолжают расползаться и уничтожать местную фауну. Проредите их популяцию и принесите караванной сети доказательство. Антидот прихватите.
nc-contract-caravan_contract_N14SuperSledgeHammer-name = Время молота
nc-contract-caravan_contract_N14SuperSledgeHammer-desc = Братство Стали обожает эти молоты. Когда в руках молот, всё вокруг кажется гвоздями. А для больших гвоздей нужны большие молоты. Паладины, естественно, хотят ещё.
nc-contract-caravan_contract_N14MobDeathclaw-name = Расплата с процентами
nc-contract-caravan_contract_N14MobDeathclaw-desc = Когти смерти начали охотиться на караванщиков и поселенцев НКР в регионе. Рейнджеры назначили щедрую награду за доказательство убийства когтей смерти.
nc-contract-caravan_contract_N14MobDeathclawMetal-name = Не подумали, стоит ли
nc-contract-caravan_contract_N14MobDeathclawMetal-desc = Караванная сеть получила сообщения о бронированных когтях смерти в регионе — это серьёзная угроза нашей экспансии. Предоставьте доказательство, что проблема устранена.
nc-contract-caravan_contract_N14FoodCannedFish-name = Готовься к будущему
nc-contract-caravan_contract_N14FoodCannedFish-desc = Некоторые считают, что мир скоро снова закончится — какой-нибудь безумец доберётся до уцелевшей боеголовки и похоронит всех нас. Эти пустошники хотят срочно сделать запасы на случай беды.
nc-contract-caravan_contract_N14FoodEggChicken-name = Яичница, но аккуратно
nc-contract-caravan_contract_N14FoodEggChicken-desc = В крупных индустриальных городах вроде Хаба свежие яйца — редкость. Граждане НКР щедро заплатят за чистую свежую еду с фронтира.
nc-contract-caravan_contract_N14Whip-name = Где есть кнут, там есть путь!
nc-contract-caravan_contract_N14Whip-desc = Легионерские надсмотрщики переломали кнуты от чрезмерного усердия над безоружными женщинами и детьми; пришлите им замену, чтобы они могли продолжать храбро избивать тех, кто не может дать сдачи.
nc-contract-caravan_contract_N14MobRadscorpion-name = Жало скорпиона
nc-contract-caravan_contract_N14MobRadscorpion-desc = Радскорпионы осмелели и теперь нападают даже на разведчиков Легиона Цезаря вдали от гнёзд. Уничтожьте этих вредителей — и получите щедрую оплату.
nc-contract-caravan_contract_N14MobMirelurk-name = Операция «Рыба в бочке»
nc-contract-caravan_contract_N14MobMirelurk-desc = Болотники в регионе стали постоянной проблемой для местного Братства Стали. Союзные отделения, заинтересованные в их успехе, назначили награду за местных болотников.
nc-contract-caravan_contract_N14WeaponRifleAk112-name = Помнить Хелиос Один
nc-contract-caravan_contract_N14WeaponRifleAk112-desc = Группа отступников-рыцарей Братства Стали хочет ослушаться приказов и почтить павших братьев Мохавского отделения, крестив рекрутов НКР огнём тяжёлых пулемётов.
nc-contract-caravan_contract_N14ClothingLegionReconArmor-name = Месть ради благотворительности
nc-contract-caravan_contract_N14ClothingLegionReconArmor-desc = Сбежавшие рабы Легиона Цезаря сложились своими сбережениями, чтобы увидеть смерть хотя бы нескольких мучителей. Вместо ушей беженцам хватит и этого доказательства.
nc-contract-caravan_contract_N14PilumSpear-name = Копья судьбы
nc-contract-caravan_contract_N14PilumSpear-desc = Коллекционер подлинного легионерского оружия в Хабе хочет завершить экспозицию ещё несколькими пилумами. Удовлетворите его запрос — и эксцентрик щедро заплатит караванной сети.
nc-contract-caravan_contract_N14MobRadMirelurk-name = На рыбалку
nc-contract-caravan_contract_N14MobRadMirelurk-desc = Светящиеся болотники стали помехой для Легиона Цезаря: их панцири отражают пули, словно силовая броня. Устраните это препятствие для их разведчиков и получите богатую награду.
nc-contract-caravan_contract_N14Gladius-name = До самой кости
nc-contract-caravan_contract_N14Gladius-desc = В Легионе Цезаря есть разногласия, и некоторые элементы Старой гвардии неплохо платят за доказательства устранения «ложного легиона». Пришлите трофеи — получите оплату.
nc-contract-caravan_contract_N14VeteranLegionArmor-name = Кровь и песок
nc-contract-caravan_contract_N14VeteranLegionArmor-desc = Новая Калифорнийская Республика хорошо платит за доказательства мёртвых легионеров. Снимите комплект ветеранской брони со свежего трупа в качестве доказательства и отправьте за оплатой.
nc-contract-caravan_contract_N14MobDeathclawGrandHunt-name = Главное — результат
nc-contract-caravan_contract_N14MobDeathclawGrandHunt-desc = Когти смерти напали на местных торговцев, нанеся серьёзные потери проверенным караванщикам. Караванная сеть предлагает щедрую награду тем, кто проредит их гнёзда.
nc-contract-caravan_contract_N14WeaponPlasmaRifleDelivery-name = Ad Victorium
nc-contract-caravan_contract_N14WeaponPlasmaRifleDelivery-desc = Братство Стали платит целое состояние за исправную плазменную винтовку в рабочем состоянии. Им всё равно, как вы её достанете, — и караванная сеть с этим согласна.
nc-contract-caravan_contract_N14VaultPDA-name = Корпоративный шпионаж
nc-contract-caravan_contract_N14VaultPDA-desc = Пип-бои из местного убежища содержат ценные данные, и спрос среди клиентов караванной сети огромен. Нам всё равно, придётся ли отпиливать их с запястий — просто добудьте их.
nc-contract-caravan_contract_N14ChineseSword-name = Народно-освободительная армия
nc-contract-caravan_contract_N14ChineseSword-desc = Хотя большинство одичавших остатков НОАК или их потомков ушли от прошлого или вымерли, некоторые всё ещё поднимают знамя. Новой группе нужен символ прошлого для борьбы с бродягами.
nc-contract-caravan_contract_N14HolotapeQuestGECK-name = Вырастить мне рай
nc-contract-caravan_contract_N14HolotapeQuestGECK-desc = Учёные OSI в Городе Дейглоу интересуются данными голодиска ГЭКК и готовы щедро заплатить. Они уверяют, что правительство на этот раз не испортит информацию.
nc-contract-caravan_contract_N14HolotapeEnclave-name = Америка не такая уж прекрасная
nc-contract-caravan_contract_N14HolotapeEnclave-desc = Информация, вырванная из холодных мёртвых рук Анклава, всегда ценна для Братства Стали. Их представители заинтересованы в новых данных, украденных у остатков американской элиты.
nc-contract-caravan_contract_N14CurrencyPrewar-name = Наличные всё ещё король
nc-contract-caravan_contract_N14CurrencyPrewar-desc = Обезумевший смотритель убежища Vault-Tec верит, что довоенная валюта всё ещё имеет ценность, и хочет её накопить. Пока у них есть крышки для обмена с караванной сетью, мы подыграем этой фантазии.
