# #Cythisiax Added - Faction roster: the faction lead's roster / recruitment GUI.
# Sirius Change: ru-RU translation of the whole file.

faction-roster-name-rangers = Рейнджеры Новой Калифорнийской Республики
faction-roster-name-enclave = Анклав

faction-roster-track-rangers = Рейнджеры
faction-roster-track-military = Военные
faction-roster-track-science = Наука

faction-roster-offer-rangers = {$recruiter} предлагает вам место среди рейнджеров Новой Калифорнийской Республики.

faction-roster-not-authorized = У вас нет полномочий управлять этим списком.

faction-roster-window-title = Список фракции
faction-roster-refresh = Обновить
faction-roster-tab-roster = Состав
faction-roster-tab-recruit = Набор
faction-roster-tab-oath = Клятва
faction-roster-filter-placeholder = Поиск по имени...
faction-roster-loading = Загрузка списка...
# #Cythisiax Edited - the roster only lists connected members now
faction-roster-summary = {$online} {$online ->
    [one] участник
    [few] участника
    [many] участников
   *[other] участников
} в сети.
faction-roster-empty = В сети нет участников.
# #Cythisiax Added - shown when someone without discharge rights tries to remove a member
faction-roster-status-discharge-denied = Вы можете понижать только до самого низкого звания. Чтобы исключить человека из фракции, нужно более высокое командование.

faction-roster-offline = Не в сети
faction-roster-online = В сети
faction-roster-on-duty = На службе

faction-roster-promote = Повысить
faction-roster-promote-tooltip = Повысить до {$rank}.
faction-roster-demote = Понизить
faction-roster-demote-tooltip = Понизить до {$rank}.
faction-roster-demote-remove-tooltip = Убрать из {$rank}.
faction-roster-specialty-grant = Назначить как {$role}.
faction-roster-specialty-revoke = Снять с {$role}.
faction-roster-discharge = Уволить
faction-roster-discharge-confirm = Подтвердить?

faction-roster-recruit-none = Рядом нет человека, которого можно завербовать.
faction-roster-recruit = Завербовать
faction-roster-recruit-pending = Предложение ожидает ответа

faction-roster-oath-hint = Клятва, которую читают новобранцы перед принятием. Напишите [name] вместо имени новобранца и [recruiter] вместо своего.
faction-roster-oath-counter = {$count} / {$max} символов
faction-roster-oath-save = Сохранить клятву
faction-roster-oath-reset = Вернуть по умолчанию
faction-roster-status-oath-saved = Клятва сохранена. Новобранцы её увидят.
faction-roster-status-oath-reset = Клятва возвращена к стандартной.
faction-roster-status-oath-too-long = Клятва слишком длинная (максимум {$max} символов).
faction-roster-status-oath-denied = Менять клятву может только руководство фракции.

faction-roster-status-not-member = Этого человека больше нет в списке.
faction-roster-status-self = Вы не можете менять собственное звание.
faction-roster-status-locked = {$name} входит в руководство фракции.
faction-roster-status-max-rank = {$name} уже имеет высшее звание, которое вы можете дать.
faction-roster-status-min-rank = Ниже звания у {$name} нет. Чтобы исключить, используйте «Уволить».
faction-roster-status-promoted = {$name} повышен до {$rank}.
faction-roster-status-demoted = {$name} понижен до {$rank}.
faction-roster-status-track-removed = {$name} убран из {$rank}.
faction-roster-status-discharged = {$name} уволен.
faction-roster-status-specialty-granted = {$name} назначен как {$role}.
faction-roster-status-specialty-revoked = {$name} снят с {$role}.
faction-roster-status-cannot-recruit = Этого человека сейчас нельзя завербовать.
faction-roster-status-too-far = Они слишком далеко.
faction-roster-status-pending = Они уже рассматривают предложение.
faction-roster-status-offer-sent = Предложение о вербовке отправлено {$name}.

faction-roster-notify-recruited = Вы вступили в {$faction} в звании {$rank}.
faction-roster-notify-promoted = Вас повысили до {$rank}.
faction-roster-notify-demoted = Вас понизили до {$rank}.
faction-roster-notify-track-removed = Вы больше не {$rank}.
faction-roster-notify-recruited-plain = Вы вступили в {$faction}.
faction-roster-notify-discharged = Вас уволили из {$faction}.
faction-roster-notify-specialty-granted = Вас назначили как {$role}.
faction-roster-notify-specialty-revoked = Вас больше не назначают как {$role}.
faction-roster-recruit-accepted = {CAPITALIZE($name)} принимает предложение и вступает в {$faction}.
faction-roster-recruit-declined = {CAPITALIZE($name)} отклоняет ваше предложение о вербовке.

faction-recruit-offer-title = Предложение о вербовке
faction-recruit-offer-header = {$recruiter} приглашает вас вступить в {$faction}
faction-recruit-offer-note = Если закрыть это окно, предложение будет отклонено.
faction-recruit-offer-accept = Принять
faction-recruit-offer-decline = Отклонить
