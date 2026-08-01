# #Misfits Change
# Ghoul Reversal (De-Ghoulification) Syringe Localization

# Private message shown only to the player being reversed.
ghoul-reversal-self = Вы чувствуете странное тепло, распространяющееся по вашим венам, когда соединение начинает действовать. Радиационное повреждение начинает восстанавливаться... вы снова становитесь человеком!
# Emote broadcast to bystanders (no $target — emote system prefixes the entity name).
ghoul-reversal-others = начинает трансформироваться — их омерзительные черты тускнеют, когда кожа возвращается к человеческому облику.
ghoul-reversal-not-ghoul = Они не похожи на гулей. Эта сыворотка на них не действует.

# Reagent (Promethine) strings
# Private feedback to the affected player.
ghoul-reversal-reagent-self = Прометин наполняет ваши клетки — радиационные маркеры начинают растворяться! Вы чувствуете, что возвращаетесь к нормальной жизни...
# Emote broadcast to bystanders.
ghoul-reversal-reagent-others = вздрагивает, когда Прометин начинает действовать — их омерзительный вид медленно исчезает
ghoul-reversal-reagent-too-old = Прометин не имеет никакого эффекта. Маркеры гулификации расположены слишком глубоко, чтобы их можно было устранить с помощью химии.

# Radiation death ghoulification
# Keys used by GhoulifyOnRadiationDeathSystem (note: ghoul-on-death-* kept below for reference).
# Private message shown only to the newly-ghoulified player.
ghoulify-on-death-self = Смертельная доза радиации пронзает ваше тело, но вместо того, чтобы убить вас, она трансформирует вас. Ты теперь гуль.
# Emote broadcast to bystanders (no $target — emote system prefixes the entity name).
ghoulify-on-death-others = падает от радиации... затем снова поднимается, кожа искривлена, глаза ввалились - теперь гуль

# Legacy keys (ghoul-on-death-*) — kept for reference; system now uses ghoulify-on-death-* keys above.
# ghoul-on-death-self = The fatal dose of radiation tears through your body — but instead of killing you, it transforms you. You are a ghoul now.
# ghoul-on-death-others = {THE($target)} collapses from the radiation... but rises again, skin twisted and eyes hollow. They've become a ghoul!

# Reagent guidebook
reagent-effect-guidebook-ghoul-reversal = обратное гулирование, если оно применено в течение 12 часов после воздействия ({ $chance ->
  [1] always
  *[other] { $chance } chance
})

# Promethine reagent strings
reagent-name-promethine = Прометин
reagent-desc-promethine = Чрезвычайно редкое соединение, синтезированное из RadAway, RadX и клеточных катализаторов. Клинические исследования показывают, что он может подавлять и обращать вспять каскад излучения ВРЭ, ответственный за гулификацию, — но только в пределах узкого окна после первоначального воздействия. Через 12 часов клеточная мутация становится постоянной.
reagent-physical-desc-luminous = светящийся золотой
