

# Fallout 14

**Fallout 14** - это русский форк проекта **isfits: Nuclear Wasteland**, Проект объединяет лучшие наработки в сеттинге Fallout с возможностями движка Space Station 14.

## О проекте

Fallout 14 - это первый русскоязычный Fallout-форк для Space Station 14, использующий:

* Ассеты из различных билдов Fallout13 (F13/SS13), таких как Desert Rose 2, Lone Star и др.
* Уникальные материалы, созданные сообществом.
* Высокомодульную систему из оригинального репозитория Einstein Engines.

Тематика и локации отличаются от классического F13, предлагая игрокам совершенно новый опыт.

Чтобы принять участие в разработке, присоединяйтесь к нашему [Discord](https://discord.gg/uPwxRWBq7r).

## Ссылки

* [Discord](https://discord.gg/uPwxRWBq7r) (официальное сообщество)

## Сборка

### Требования

* Git
* .NET SDK 10.0.101

### Windows

1. Склонируйте репозиторий:

```sh
git clone https://github.com/Misfit-Sanctuary/nuclear-14.git

```

2. Инициализируйте подмодули:

```sh
git submodule update --init --recursive

```

3. Соберите проект:

```sh
Scripts/bat/buildAllDebug.bat

```

4. Запустите клиент и сервер:

```sh
Scripts/bat/runQuickAll.bat

```

5. Подключитесь к `localhost` через клиент.

### Linux

Процесс аналогичен Windows, но используются `.sh` скрипты:

```sh
Scripts/sh/buildAllDebug.sh
Scripts/sh/runQuickAll.sh

```

### MacOS

Теоретически процесс аналогичен Linux, но сборка на данной ОС не тестировалась.

## Лицензия

<!-- #Sirius Change: полу-закрытая модель и исправленная ссылка на LEGAL.md. -->

Подробности — в [LEGAL.md](./LEGAL.md), реестр нашего контента — в [SIRIUS-CONTENT.md](./SIRIUS-CONTENT.md). Соблюдение авторских прав обязательно.

* **Оригинальный код Sirius** — закрытая лицензия **SIRIUS-PL v1.0**, `LICENSE-SIRIUS-CODE.txt`. Использование в других проектах — только с письменного разрешения RavenAzala.
* **Оригинальные спрайты Sirius** — **CC BY-NC-SA 4.0**, лицензия и автор указаны в `meta.json` каждого RSI.
* **Унаследованный код** — MIT (до 17.02.2024) или AGPLv3 (после), если в файле не указано иное. Правки с маркером `#Sirius` внутри AGPL-файлов остаются под AGPLv3.
* **Остальной контент** (Misfits, N14, Nuclear14, Corvax, DeltaV, ваниль, SS13-форки) — под своими лицензиями из `meta.json`, `attributions.yml` и `CREDITS.md`.

Файл `LICENSE-ICLA-EN.txt` относится к проекту WWhiteDreamProject/wwdpublic и **не лицензирует контент Sirius Fallout**. Свои права авторы подтверждают через `CLA-Sirius.md`.
