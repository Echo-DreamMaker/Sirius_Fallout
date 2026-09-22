# #Misfits Fix: ItemSlotsSystem calls Loc.GetString(slot.Name) for every ItemSlot whose
# name field is non-empty.  None of the bare capitalised slot names used across YAML
# prototypes were ever defined as FTL keys, so the engine fires [WARN] loc: Unknown
# messageId for each verb interaction.  Names that contain spaces cannot be valid
# Fluent identifiers and are omitted here (they fall back to the raw string which is
# already the intended display text).

# ── Ranged weapons ──────────────────────────────────────────────────────────────
Magazine = Журнал
Chamber = Камера
Projectiles = Снаряды
Canister = Канистра
Tank = Танк
Flare = Вспышка
Shotgun = Дробовик

# ── Melee / misc weapons ────────────────────────────────────────────────────────
Knife = Нож
Katana = Катана
Machete = Мачете
Sabre = Сабля
Vector = Вектор
Incinerator = Мусоросжигательный завод
CaneBlade = Лезвие трости
HollowCane = Полая трость

# ── General items ───────────────────────────────────────────────────────────────
Disk = Диск
Board = Доска
Implant = Имплантат
Vial = флакон
Keys = Ключи
Mail = Почта
SoulCrystal = Кристалл души

# ── Weapon attachment slots (#Misfits Add) ───────────────────────────────────
weapon-attachment-slot-top = Верхний слот
weapon-attachment-slot-muzzle = Дульный слот
weapon-attachment-slot-bottom = Нижний слот
weapon-attachment-slot-handle = Слот рукояти