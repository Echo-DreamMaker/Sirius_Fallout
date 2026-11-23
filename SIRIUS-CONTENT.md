# Реестр контента Sirius Fallout

<!-- #Sirius Add: реестр оригинального контента для полу-закрытой модели. -->

Владелец: **RavenAzala** (игровой ckey `RavenReaper`).
Обновлено: 22.11.2026.

Этот файл — рабочий реестр: что считается нашим, а что унаследовано.
При спорной принадлежности ориентироваться на `meta.json`,
`attributions.yml`, `CREDITS.md` и маркеры `#Sirius` в git-истории.

## 1. Закрытый код — SIRIUS-PL v1.0

`LICENSE-SIRIUS-CODE.txt`. Действует только на оригинальные файлы:

* `Content.Server/_Sirius/**`
* `Content.Shared/_Sirius/**`
* `Content.Client/_Sirius/**`
* `Content.Tests/_Sirius/**`
* `Content.IntegrationTests/Tests/_Sirius/**`
* `Resources/Prototypes/_Sirius/**` и `Resources/Locale/*/_Sirius/**` — если файл не является переделкой апстримного прототипа/строки.

**Не подпадает под SIRIUS-PL:** правки с маркерами `#Sirius Add/Change/Fix/Perf`
в файлах апстрима, N14 и Misfits. Они остаются под лицензией исходного файла
(MIT или AGPLv3), даже если написаны с нуля.

## 2. Спрайты Sirius — CC BY-NC-SA 4.0

`https://creativecommons.org/licenses/by-nc-sa/4.0/`. Лицензия выставлена в
`meta.json` соответствующих RSI.

| RSI | Автор |
| --- | --- |
| `Clothing/Head/FiendHelmet.rsi` | jekuzer (bochenok) |
| `Clothing/Head/moth_fluffy_head.rsi` | Rozy_Cosmic |
| `Clothing/Mask/skullmask.rsi` | jekuzer (bochenok) |
| `Clothing/Neck/moth_fluffy_wings.rsi` | Rozy_Cosmic |
| `Clothing/OuterClothing/Trash_RaiderArmor.rsi` | Dicugg (спрайты), Jekuzer (код) |
| `Clothing/OuterClothing/moth_fluffy.rsi` | Rozy_Cosmic |
| `Interface/Actions/tamesystem.rsi` | RabbitBat, спрайты Rozy_Cosmic |
| `Interface/Misc/job_icons.rsi` | rozycosmic |
| `Objects/Misc/siriusbooks.rsi` | Rozy_Cosmic |
| `Objects/Weapons/Ranged/trashassaultcarbine.rsi` | Jekuzer (bochenok) |
| `Interface/Alerts/sirius_wet.rsi` | Raven |

Все пути выше — от `Resources/Textures/_Sirius/`.

## 3. Требуется согласие автора до смены лицензии

Эти RSI помечены как оригинальные, но сейчас под **CC BY-SA 3.0**. Добавление
ограничения NC к материалу CC-BY-SA невозможно без согласия правообладателя —
поэтому `meta.json` пока не изменён.

| RSI | Автор | Что нужно |
| --- | --- | --- |
| `Interface/Alerts/sirius_dirt.rsi` | Rosy_Cosmic, адаптация Raven | Подпись CLA от Rosy_Cosmic |
| `Objects/Fun/casinobunnyblue.rsi` | Rosie | Подпись CLA от Rosie |
| `Objects/Fun/casinobunnypink.rsi` | Rosie | Подпись CLA от Rosie |
| `Objects/Weapons/Melee/BrassKnuckles.rsi` | @kexit2 | Подпись CLA от kexit2 |
| `Mobs/Animals/moff.rsi` | «Created for SS14», автор не установлен | Выяснить источник; при сомнении оставить CC BY-SA 3.0 |

## 4. Унаследованные спрайты — лицензию не меняем

| RSI | Лицензия | Примечание |
| --- | --- | --- |
| `Clothing/OuterClothing/WinterTribeHeavy.rsi` | CC BY-NC-SA 3.0 | Berserk, N14 |
| `Clothing/OuterClothing/WinterTribeLight.rsi` | CC BY-NC-SA 3.0 | Berserk, N14 |
| `Clothing/OuterClothing/falloutwinterraider.rsi` | CC BY-NC-SA 3.0 | Mojave Sun |

Остальной контент (Misfits, N14, Nuclear14, Corvax, DeltaV, ваниль, SS13-форки)
остаётся под своими лицензиями из `meta.json`, `attributions.yml` и `CREDITS.md`.

## 5. Оформление прав

Каждый автор из разделов 2–3 подписывает `CLA-Sirius.md`: исключительную
лицензию в пользу RavenAzala на свой оригинальный вклад. До подписи автор
сохраняет право использовать работу в других проектах.
