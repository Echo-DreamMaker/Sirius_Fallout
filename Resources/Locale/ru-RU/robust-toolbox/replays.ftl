#Sirius Add: полный перевод команд воспроизведения записей движка.
# Playback Commands

cmd-replay-play-desc = Возобновить воспроизведение записи.
cmd-replay-play-help = replay_play

cmd-replay-pause-desc = Приостановить воспроизведение записи.
cmd-replay-pause-help = replay_pause

cmd-replay-toggle-desc = Возобновить или приостановить воспроизведение записи.
cmd-replay-toggle-help = replay_toggle

#Sirius Add: ключ отсутствовал в ru-RU и давал WARN Unknown messageId.
cmd-replay-toggle-screenshot-mode-desc = Включает или выключает режим скриншота для записей, скрывая панель управления воспроизведением.
cmd-replay-toggle-screenshot-mode-help = replay_toggle_screenshot_mode

cmd-replay-stop-desc = Остановить и выгрузить запись.
cmd-replay-stop-help = replay_stop

cmd-replay-load-desc = Загрузить и начать воспроизведение записи.
cmd-replay-load-help = replay_load <папка записи>
cmd-replay-load-hint = Папка записи

cmd-replay-skip-desc = Перемотать вперёд или назад по времени.
cmd-replay-skip-help = replay_skip <тик или интервал>
cmd-replay-skip-hint = Тики или интервал (ЧЧ:ММ:СС).

cmd-replay-set-time-desc = Перейти вперёд или назад на определённое время.
cmd-replay-set-time-help = replay_set <тик или время>
cmd-replay-set-time-hint = Тик или интервал (ЧЧ:ММ:СС) начиная с

cmd-replay-error-time = "{$time}" не является целым числом или интервалом.
cmd-replay-error-args = Неверное число аргументов.
cmd-replay-error-no-replay = Сейчас запись не воспроизводится.
cmd-replay-error-already-loaded = Запись уже загружена.
cmd-replay-error-run-level = Нельзя загрузить запись, пока вы подключены к серверу.

# Recording commands

cmd-replay-recording-start-desc = Начинает запись, при необходимости с ограничением времени.
cmd-replay-recording-start-help = Использование: replay_recording_start [имя] [перезапись] [лимит времени]
cmd-replay-recording-start-success = Запись начата.
cmd-replay-recording-start-already-recording = Запись уже идёт.
cmd-replay-recording-start-error = При запуске записи произошла ошибка.
cmd-replay-recording-start-hint-time = [лимит времени (минуты)]
cmd-replay-recording-start-hint-name = [имя]
cmd-replay-recording-start-hint-overwrite = [перезапись (bool)]

cmd-replay-recording-stop-desc = Останавливает запись.
cmd-replay-recording-stop-help = Использование: replay_recording_stop
cmd-replay-recording-stop-success = Запись остановлена.
cmd-replay-recording-stop-not-recording = Запись сейчас не ведётся.

cmd-replay-recording-stats-desc = Показывает сведения о текущей записи.
cmd-replay-recording-stats-help = Использование: replay_recording_stats
cmd-replay-recording-stats-result = Длительность: {$time} мин, тиков: {$ticks}, размер: {$size} МБ, скорость: {$rate} МБ/мин.

# Time Control UI
replay-time-box-scrubbing-label = Динамическая прокрутка
replay-time-box-replay-time-label = Время записи: {$current} / {$end}  ({$percentage}%)
replay-time-box-server-time-label = Время сервера: {$current} / {$end}
replay-time-box-index-label = Индекс: {$current} / {$total}
replay-time-box-tick-label = Тик: {$current} / {$total}
