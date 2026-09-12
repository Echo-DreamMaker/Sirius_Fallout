# Displayed as initiator of vote when no user creates the vote
ui-vote-initiator-server = Сервер

## Default.Votes

ui-vote-restart-title = Перезапуск раунда
ui-vote-restart-succeeded = Голосование о перезапуске раунда успешно.
ui-vote-restart-failed = Голосование о перезапуске раунда отклонено (требуется { TOSTRING($ratio, "P0") }).
ui-vote-restart-fail-not-enough-ghost-players = Голосование о перезапуске раунда отклонено: Минимум { $ghostPlayerRequirement }% игроков должно быть призраками чтобы запустить голосование о перезапуске. В данный момент игроков-призраков недостаточно.
ui-vote-restart-yes = Да
ui-vote-restart-no = Нет
ui-vote-restart-abstain = Воздерживаюсь

ui-vote-gamemode-title = Следующий режим игры
ui-vote-gamemode-tie = Ничья в голосовании за игровой режим! Выбирается... { $picked }
ui-vote-gamemode-win = { $winner } победил в голосовании за игровой режим!

ui-vote-map-title = Следующая карта
ui-vote-map-tie = Ничья при голосовании за карту! Выбирается... { $picked }
ui-vote-map-win = { $winner } выиграла голосование о выборе карты!
ui-vote-map-notlobby = Голосование о выборе карты действует только в предраундовом лобби!
ui-vote-map-notlobby-time = Голосование о выборе карты действует только в предраундовом лобби, когда осталось { $time }!


# Votekick votes
ui-vote-votekick-unknown-initiator = Игрок
ui-vote-votekick-unknown-target = Неизвестный игрок
ui-vote-votekick-title = { $initiator } начал голосование за кик пользователя: { $targetEntity }. Причина: { $reason }
ui-vote-votekick-yes = Да
ui-vote-votekick-no = Нет
ui-vote-votekick-abstain = Воздержаться
ui-vote-votekick-success = Голосование за кик { $target } прошло успешно. Причина кика: { $reason }
ui-vote-votekick-failure = Голосование за кик { $target } провалилось. Причина кика: { $reason }
ui-vote-votekick-not-enough-eligible = Недостаточное количество подходящих голосующих онлайн для начала голосования: { $voters }/{ $requirement }
ui-vote-votekick-server-cancelled = Голосование за кик { $target } отменено сервером.

ui-round-countdown-15 = Внимание: До конца раунда остаётся примерно пятнадцать минут. Начинается голосование за судьбу раунда.

ui-round-countdown-30 = Внимание: До конца раунда остаётся примерно тридцать минут.

ui-round-countdown-60 = Внимание: До конца раунда остаётся примерно шестьдесят минут.

ui-round-timer-label = Конец раунда
    { $time }

ui-vote-extend-abstain = Воздержаться

ui-vote-extend-failed = Голосование за продление раунда провалено: { $yes }/{ $total } проголосовали за (необходимо { $needed } для большинства).

ui-vote-extend-no = Нет

ui-vote-extend-succeeded = Голосование за продление раунда прошло — раунд продлён на { $minutes } минут. Поезд отозван.

ui-vote-extend-title = Продлить раунд

ui-vote-extend-yes = Да

ui-vote-restart-failed-majority = Голосование за перезапуск раунда провалено: { $yes }/{ $total } проголосовали за (необходимо { $needed } для большинства).

ui-vote-round-decision-no = Нет

ui-vote-round-decision-no-won = Голосование за продление раунда: { $yesVotes } за продление, { $noVotes } за завершение (всего { $total } подключено). Раунд продолжается!

ui-vote-round-decision-tie = Голосование за продление раунда: ничья: { $yesVotes } за продление, { $noVotes } за завершение (всего { $total } подключено). По умолчанию раунд продлевается.

ui-vote-round-decision-title = Продлить раунд?

ui-vote-round-decision-yes = Да

ui-vote-round-decision-yes-won = Голосование за продление раунда: { $yesVotes } за продление, { $noVotes } за завершение (всего { $total } подключено). Вызывается поезд.
