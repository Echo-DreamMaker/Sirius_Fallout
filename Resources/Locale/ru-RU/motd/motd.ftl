cmd-motd-desc = Показывает или устанавливает Сообщение дня.
cmd-motd-help = motd [ message... ]
cmd-get-motd-desc = Показывает Сообщение дня и открывает его окно.
cmd-get-motd-help = get-motd
cmd-set-motd-desc = Устанавливает или очищает Сообщение дня.
cmd-set-motd-help = set-motd [ message... ]
cmd-set-motd-hint-head = [ message... ]
cmd-set-motd-hint-cont = [ ...message... ]
cmd-set-motd-cleared-motd-message = Сообщение дня очищено.
cmd-set-motd-set-motd-message = Установлено следующее Сообщение дня: "{ $motd }".
motd-wrap-message = Сообщение дня: { $motd }

# Sirius Add - Картинки и опрос в окне Сообщения дня
motd-none = Сообщение дня не задано.

cmd-set-motd-image-desc = Задаёт картинки, отображаемые в окне Сообщения дня.
cmd-set-motd-image-help = set-motd-image [ путь... ]
cmd-set-motd-image-hint = [ /Textures/путь/к/картинке.png ... ]
cmd-set-motd-image-set = Картинки Сообщения дня установлены: {$images}.
cmd-set-motd-image-cleared = Картинки Сообщения дня очищены.
cmd-set-motd-image-too-many = В Сообщении дня можно показать не более {$max} картинок.
cmd-set-motd-image-invalid = "{$path}" не является допустимым путём к ресурсу.

cmd-set-motd-poll-desc = Задаёт опрос, отображаемый рядом с Сообщением дня.
cmd-set-motd-poll-help = set-motd-poll <вопрос> | <вариант> | <вариант> [| ...]
cmd-set-motd-poll-hint = <вопрос> | <вариант> | <вариант> [| ...]
cmd-set-motd-poll-usage = Использование: set-motd-poll <вопрос> | <вариант> | <вариант> [| ...]
cmd-set-motd-poll-empty = Вопрос и каждый вариант должны быть непустыми.
cmd-set-motd-poll-too-many-options = В опросе может быть не более {$max} вариантов.
cmd-set-motd-poll-set = Опрос Сообщения дня установлен: "{$question}", вариантов — {$count}.
cmd-clear-motd-poll-desc = Убирает опрос, отображаемый рядом с Сообщением дня.
cmd-clear-motd-poll-help = clear-motd-poll
cmd-clear-motd-poll-cleared = Опрос Сообщения дня очищен.
cmd-clear-motd-poll-no-poll = Опрос в Сообщении дня отсутствует.
cmd-motd-vote-desc = Голосует в опросе Сообщения дня либо выводит его результаты.
cmd-motd-vote-help = motd-vote [номер варианта]
cmd-motd-vote-no-poll = Сейчас нет активного опроса в Сообщении дня.
cmd-motd-vote-not-a-number = "{$option}" не является числом.
cmd-motd-vote-invalid-option = "{$option}" не является допустимым вариантом. Выберите число от 1 до {$max}.
cmd-motd-vote-cast = Вы проголосовали за {$number}. {$option}.
cmd-motd-vote-changed = Вы изменили голос на {$number}. {$option}.

motd-poll-header = Опрос: {$question}
motd-poll-option = {$number}. {$option} — {$votes} голосов ({$percent}%)
motd-poll-total = Всего голосов: {$total}
motd-poll-hint = Голосуйте командой "motd-vote <число>" или щелчком по варианту в окне.

motd-ui-title = Сообщение дня
motd-ui-no-message = Сообщение дня не задано.
motd-ui-poll-question = Опрос: {$question}
motd-ui-poll-option = {$number}. {$option}
motd-ui-poll-yours = (ваш голос)
motd-ui-poll-votes = {$percent}% ({$votes})
motd-ui-poll-total = Всего голосов: {$total}
motd-ui-poll-hint = Щёлкните по варианту, чтобы проголосовать. Голос можно изменить в любой момент.

misfits-server-tab-motd-button = Изменить Сообщение дня
misfits-server-tab-motd-desc = Изменить текст, картинки и опрос. После применения их увидит каждый.

motd-ui-edit-header = Изменение Сообщения дня
motd-ui-edit-text = Текст сообщения (допускается разметка вроде [bold] или [color=red])
motd-ui-edit-images = Картинки (пути через ";", не более 4, относительно /Textures)
motd-ui-edit-question = Вопрос опроса (оставьте пустым, чтобы убрать опрос)
motd-ui-edit-options = Варианты опроса (через "|", не менее 2)
motd-ui-edit-save = Сохранить и показать всем
motd-ui-edit-clear-poll = Убрать опрос
motd-ui-edit-hint = Картинки должны существовать в Resources/Textures сервера. Неверные пути, более 4 картинок или менее 2 вариантов отклоняются, и тогда ничего не меняется.
motd-ui-edit-toggle = Изменить Сообщение дня
motd-ui-edit-toggle-close = Закрыть редактор
