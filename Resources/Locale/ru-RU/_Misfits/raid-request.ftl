# #Misfits Add - Localization for the /raid request system (player window + admin tab).

raid-request-window-title = Запрос на рейд

# Header / eligibility
raid-request-your-faction = Ваша фракция: { $faction }
raid-request-no-faction = В настоящее время вы не принадлежите к признанной фракции.
raid-request-individual-suffix = (индивидуальный запрос)
raid-request-individual-banner = Будучи жителем Пустоши, вы можете подавать индивидуальные запросы на рейды. О решении администратора будете уведомлены только вы.
raid-request-not-eligible = Ваша фракция ({ $faction }) не имеет права отправлять запросы на рейды.

# Submission form
raid-request-submit-header = Отправьте заявку на рейд
raid-request-target-label = Целевая фракция:
raid-request-location-label = Местоположение (необязательно):
raid-request-location-placeholder = например Аванпост НКР Браво, южные дюны...
raid-request-reason-label = Причина/план:
raid-request-reason-placeholder = Объясните, на кого вы собираетесь совершить набег и почему. Минимум пять слов.
raid-request-submit-button = Отправить запрос
raid-request-submit-confirm = Нажмите еще раз, чтобы подтвердить
raid-request-confirm-prompt = Нажмите еще раз, чтобы отправить этот запрос администраторам.
raid-request-reason-too-short = Ваша причина должна содержать не менее { $min } слов.

# My-requests history (player side)
raid-request-my-requests-header = Ваши запросы в этом раунде
raid-request-no-my-requests = В этом раунде вы не отправляли ни одного запроса на рейд.

# Admin embedded panel (bwoink Raid tab)
raid-request-filter-pending = В ожидании
raid-request-filter-decided = Решенный
raid-request-filter-all = Все
raid-request-list-empty = (нет запросов, соответствующих этому фильтру)
raid-request-no-selection = Выберите запрос, чтобы просмотреть подробности.
raid-request-admin-comment-label = Замечания (отправлены во фракцию):
raid-request-admin-comment-placeholder = Необходимый. Это сообщение доставляется каждому уведомленному игроку.
raid-request-approve-button = Утвердить
raid-request-deny-button = Отрицать
raid-request-end-button = Завершить рейд
raid-request-comment-required = Прежде чем утвердить или отклонить, необходим комментарий.
raid-request-pending-count = { $count } ожидающие запросы на рейд
raid-request-no-pending = Нет ожидающих запросов на рейд.

# Decision popups (client-side)
raid-request-popup-approved = Рейд УТВЕРЖДЕН: { $from } → { $to }
raid-request-popup-denied = Рейд ЗАПРЕЩЕН: { $from } → { $to }
raid-request-popup-target-warning = Грядущая угроза рейда против { $faction } УТВЕРЖДЕНА!

# #Misfits Add - Peer-faction approval popup (target faction leader bypass).
raid-request-peer-window-title = Рейдовая угроза — решите
raid-request-peer-header = Входящий запрос на рейд от { $faction } против { $target }.
raid-request-peer-from-label = От:
raid-request-peer-location-label = Заявленное место:
raid-request-peer-location-unset = (не указан)
raid-request-peer-reason-label = Причина/план:
raid-request-peer-comment-label = Ваши замечания (отправлены во фракцию запросившего):
raid-request-peer-comment-placeholder = Необязательный. Будет дословно показано другой фракции.
raid-request-peer-approve-button = Разрешить рейд
raid-request-peer-deny-button = Мусор

raid-concluded-window-title = Рейд окончен
raid-concluded-header = РЕЙД №{ $id } ЗАВЕРШЁН
raid-concluded-parties = { $from } против { $to }
raid-concluded-warning = Боевое разрешение аннулировано. Немедленно прекратите все враждебные действия в рамках рейда.
raid-concluded-acknowledge = Я подтверждаю