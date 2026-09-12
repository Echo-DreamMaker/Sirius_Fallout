### UI

chat-manager-max-message-length = Ваше сообщение превышает лимит в {$maxMessageLength} символов
chat-manager-ooc-chat-enabled-message = OOC чат был включён.
chat-manager-ooc-chat-disabled-message = OOC чат был отключён.
chat-manager-looc-chat-enabled-message = LOOC чат был включён.
chat-manager-looc-chat-disabled-message = LOOC чат был отключён.
chat-manager-dead-looc-chat-enabled-message = Мёртвые игроки теперь могут использовать LOOC.
chat-manager-dead-looc-chat-disabled-message = Мёртвые игроки больше не могут использовать LOOC.
chat-manager-crit-looc-chat-enabled-message = Игроки в критическом состоянии теперь могут использовать LOOC.
chat-manager-crit-looc-chat-disabled-message = Игроки в критическом состоянии больше не могут использовать LOOC.
chat-manager-admin-ooc-chat-enabled-message = Admin OOC чат был включён.
chat-manager-admin-ooc-chat-disabled-message = Admin OOC чат был отключён.

chat-manager-max-message-length-exceeded-message = Ваше сообщение превысило лимит в {$limit} символов
chat-manager-no-headset-on-message = На вас нет гарнитуры!
chat-manager-no-radio-key = Не указан ключ радиоканала!
chat-manager-no-such-channel = Нет канала с ключом '{$key}'!
chat-manager-whisper-headset-on-message = Нельзя шептать по радио!

chat-manager-language-prefix = ({ $language }){" "}

chat-manager-server-wrap-message = [bold]{$message}[/bold]
chat-manager-sender-announcement-wrap-message = [font size=14][bold]{$sender} объявляет:[/font][font size=12]
                                                {$message}[/bold][/font]

# Форматирование сообщений в чате и речевых пузырях
chat-manager-entity-say-wrap-message = [BubbleHeader][Name][font size=11][color={$color}][bold]{$language}[/bold][/color][/font][bold]{$entityName}[/bold][/Name][/BubbleHeader] {$verb}, [font="{$fontType}" size={$fontSize} ][color={$color}]"[BubbleContent][font="{$fontType}" size={$fontSize}][color={$color}]{$message}[/color][/font][/BubbleContent]"[/color][/font]
chat-manager-entity-say-bold-wrap-message = [BubbleHeader][Name][font size=11][color={$color}][bold]{$language}[/bold][/color][/font][bold]{$entityName}[/bold][/Name][/BubbleHeader] {$verb}, [font="{$fontType}" size={$fontSize} ][color={$color}][bold]"[BubbleContent][font="{$fontType}" size={$fontSize}][color={$color}][bold]{$message}[/bold][/color][/font][/BubbleContent]"[/bold][/color][/font]

chat-manager-entity-whisper-wrap-message = [BubbleHeader][Name][font size=10][color={$color}][bold]{$language}[/bold][/color][/font][font size=11][italic]{$entityName}[/Name][/BubbleHeader] шепчет, [font="{$fontType}"][color={$color}][italic]"[BubbleContent][font="{$fontType}"][color={$color}][italic]{$message}[/italic][/color][/font][/BubbleContent]"[/italic][/color][/font][/italic][/font]
chat-manager-entity-whisper-unknown-wrap-message = [BubbleHeader][font size=10][color={$color}][bold]{$language}[/bold][/color][/font][font size=11][italic]Кто-то[/BubbleHeader] шепчет, [font="{$fontType}"][color={$color}][italic]"[BubbleContent][font="{$fontType}"][color={$color}][italic]{$message}[/italic][/color][/font][/BubbleContent]"[/italic][/color][/font][/italic][/font]

# Форматирование эмоций
# #Misfits Change - wrap name in [Name] tags so client can apply chat name color
# #Misfits Fix - Capitalize "The" for sentence-start emote messages when no ID is present
chat-manager-entity-me-wrap-message = [italic]{ PROPER($entity) ->
    *[false] [Name]{$entityName}[/Name] {$message}[/italic]
     [true] [Name]{$entityName}[/Name] {$message}[/italic]
    }

chat-manager-entity-do-wrap-message = [italic]{$message}[/italic]

chat-manager-entity-looc-wrap-message = LOOC: {$entityName}: {$message}
chat-manager-send-ooc-wrap-message = OOC: {$playerName}: {$message}
chat-manager-send-ooc-patron-wrap-message = OOC: [color={$patronColor}]{$playerName}[/color]: {$message}
# #Misfits - Supporter OOC formats: with and without title prefix
chat-manager-send-ooc-supporter-wrap-message = OOC: [color={$supporterColor}][bold]\[{$supporterTitle}][/bold] {$playerName}[/color]: {$message}
chat-manager-send-ooc-supporter-notitle-wrap-message = OOC: [color={$supporterColor}]{$playerName}[/color]: {$message}

chat-manager-send-dead-chat-wrap-message = {$deadChannelName}: [BubbleHeader]{$playerName}[/BubbleHeader]: [BubbleContent]{$message}[/BubbleContent]
chat-manager-send-admin-dead-chat-wrap-message = {$adminChannelName}: ([BubbleHeader]{$userName}[/BubbleHeader]): [BubbleContent]{$message}[/BubbleContent]
chat-manager-send-admin-chat-wrap-message = {$adminChannelName}: {$playerName}: {$message}
chat-manager-send-admin-announcement-wrap-message = [bold]{$adminChannelName}: {$message}[/bold]

chat-manager-send-hook-ooc-wrap-message = OOC: (D){$senderName}: {$message}

chat-manager-dead-channel-name = МЁРТВ
chat-manager-admin-channel-name = АДМИН

chat-manager-rate-limited = Вы отправляете сообщения слишком быстро!
chat-manager-rate-limit-admin-announcement = Игрок { $player } превысил лимит сообщений. Следите за ним, если это происходит регулярно.

chat-manager-send-empathy-chat-wrap-message = {$source}: {$message}

chat-manager-send-cult-chat-wrap-message = [bold]\[{ $channelName }\] [BubbleHeader]{ $player }[/BubbleHeader]:[/bold] [BubbleContent]{ $message }[/BubbleContent]
chat-manager-cult-channel-name = Культ Крови

## Глаголы речи

chat-speech-verb-suffix-exclamation = !
chat-speech-verb-suffix-exclamation-strong = !!
chat-speech-verb-suffix-question = ?
chat-speech-verb-suffix-stutter = -
chat-speech-verb-suffix-mumble = ..

chat-speech-verb-name-none = Нет
chat-speech-verb-name-default = Обычный
chat-speech-verb-default = говорит
chat-speech-verb-name-exclamation = Восклицающий
chat-speech-verb-exclamation = восклицает
chat-speech-verb-name-exclamation-strong = Кричащий
chat-speech-verb-exclamation-strong = кричит
chat-speech-verb-name-question = Спрашивающий
chat-speech-verb-question = спрашивает
chat-speech-verb-name-stutter = Заикающийся
chat-speech-verb-stutter = заикается
chat-speech-verb-name-mumble = Бормочущий
chat-speech-verb-mumble = бормочет

chat-speech-verb-name-arachnid = Арахнид
chat-speech-verb-insect-1 = стрекочет
chat-speech-verb-insect-2 = щебечет
chat-speech-verb-insect-3 = щёлкает

chat-speech-verb-name-moth = Мотылёк
chat-speech-verb-winged-1 = трепещет
chat-speech-verb-winged-2 = хлопает
chat-speech-verb-winged-3 = жужжит

chat-speech-verb-name-slime = Слайм
chat-speech-verb-slime-1 = хлюпает
chat-speech-verb-slime-2 = булькает
chat-speech-verb-slime-3 = сочится

chat-speech-verb-name-plant = Диона
chat-speech-verb-plant-1 = шелестит
chat-speech-verb-plant-2 = колышется
chat-speech-verb-plant-3 = скрипит

chat-speech-verb-name-robotic = Робот
chat-speech-verb-robotic-1 = констатирует
chat-speech-verb-robotic-2 = пищит
chat-speech-verb-robotic-3 = гудит

chat-speech-verb-name-reptilian = Рептилия
chat-speech-verb-reptilian-1 = шипит
chat-speech-verb-reptilian-2 = фыркает
chat-speech-verb-reptilian-3 = пыхтит

chat-speech-verb-name-skeleton = Скелет / Плазмамен
chat-speech-verb-skeleton-1 = гремит
chat-speech-verb-skeleton-2 = постукивает
chat-speech-verb-skeleton-3 = лязгает
chat-speech-verb-skeleton-4 = клацает
chat-speech-verb-skeleton-5 = трещит

chat-speech-verb-name-vox = Вокс
chat-speech-verb-vox-1 = визжит
chat-speech-verb-vox-2 = вопит
chat-speech-verb-vox-3 = каркает

chat-speech-verb-name-oni = Они
chat-speech-verb-oni-1 = ворчит
chat-speech-verb-oni-2 = ревёт
chat-speech-verb-oni-3 = гремит
chat-speech-verb-oni-4 = грохочет

chat-speech-verb-name-canine = Пёс
chat-speech-verb-canine-1 = лает
chat-speech-verb-canine-2 = гавкает
chat-speech-verb-canine-3 = воет

chat-speech-verb-name-small-mob = Мышь
chat-speech-verb-small-mob-1 = пищит
chat-speech-verb-small-mob-2 = попискивает

chat-speech-verb-name-large-mob = Карп
chat-speech-verb-large-mob-1 = рычит
chat-speech-verb-large-mob-2 = growls

chat-speech-verb-name-monkey = Обезьяна
chat-speech-verb-monkey-1 = ухает
chat-speech-verb-monkey-2 = визжит

chat-speech-verb-name-cluwne = Клувн

chat-speech-verb-name-parrot = Попугай
chat-speech-verb-parrot-1 = скрежещет
chat-speech-verb-parrot-2 = чирикает
chat-speech-verb-parrot-3 = щебечет

chat-speech-verb-cluwne-1 = хихикает
chat-speech-verb-cluwne-2 = ржёт
chat-speech-verb-cluwne-3 = хохочет

chat-speech-verb-name-ghost = Призрак
chat-speech-verb-ghost-1 = жалуется
chat-speech-verb-ghost-2 = вздыхает
chat-speech-verb-ghost-3 = напевает
chat-speech-verb-ghost-4 = бормочет

chat-speech-verb-name-electricity = Электричество
chat-speech-verb-electricity-1 = потрескивает
chat-speech-verb-electricity-2 = жужжит
chat-speech-verb-electricity-3 = скрежещет

chat-speech-verb-marish = Марс

chat-speech-verb-name-station-radio = Станционное радио
chat-speech-verb-station-radio-1 = передаёт
