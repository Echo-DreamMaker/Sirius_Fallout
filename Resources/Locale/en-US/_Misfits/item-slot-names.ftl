# #Misfits Fix: ItemSlotsSystem calls Loc.GetString(slot.Name) for every ItemSlot whose
# name field is non-empty.  None of the bare capitalised slot names used across YAML
# prototypes were ever defined as FTL keys, so the engine fires [WARN] loc: Unknown
# messageId for each verb interaction.  Names that contain spaces cannot be valid
# Fluent identifiers and are omitted here (they fall back to the raw string which is
# already the intended display text).

# ── Ranged weapons ──────────────────────────────────────────────────────────────
Magazine = Magazine
Chamber = Chamber
Projectiles = Projectiles
Canister = Canister
Tank = Tank
Flare = Flare
Shotgun = Shotgun

# ── Melee / misc weapons ────────────────────────────────────────────────────────
Knife = Knife
Katana = Katana
Machete = Machete
Sabre = Sabre
Vector = Vector
Incinerator = Incinerator
CaneBlade = Cane blade
HollowCane = Hollow cane

# ── General items ───────────────────────────────────────────────────────────────
Disk = Disk
Board = Board
Implant = Implant
Vial = Vial
Keys = Keys
Mail = Mail
SoulCrystal = Soul crystal

# ── Weapon attachment slots (#Misfits Add) ───────────────────────────────────
weapon-attachment-slot-top = Top slot
weapon-attachment-slot-muzzle = Muzzle slot
weapon-attachment-slot-bottom = Bottom slot
weapon-attachment-slot-handle = Handle slot
