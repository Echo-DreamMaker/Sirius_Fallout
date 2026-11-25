lathe-menu-title = Lathe Menu
lathe-menu-queue = Queue
lathe-menu-server-list = Server list
lathe-menu-sync = Sync
lathe-menu-search-designs = Search designs
lathe-menu-category-all = All
lathe-menu-search-filter = Filter:
lathe-menu-amount = Amount:
lathe-menu-reagent-slot-examine = It has a slot for a beaker on the side.
lathe-reagent-dispense-no-container = Liquid pours out of {THE($name)} onto the floor!
lathe-menu-result-reagent-display = {$reagent} ({$amount}u)
lathe-menu-material-display = {$material} ({$amount})
lathe-menu-tooltip-display = {$amount} of {$material}
lathe-menu-description-display = [italic]{$description}[/italic]
lathe-menu-material-amount = { $amount ->
    [1] {NATURALFIXED($amount, 2)} {$unit}
    *[other] {NATURALFIXED($amount, 2)} {MAKEPLURAL($unit)}
}
lathe-menu-material-amount-missing = { $amount ->
    [1] {NATURALFIXED($amount, 2)} {$unit} of {$material} ([color=red]{NATURALFIXED($missingAmount, 2)} {$unit} missing[/color])
    *[other] {NATURALFIXED($amount, 2)} {MAKEPLURAL($unit)} of {$material} ([color=red]{NATURALFIXED($missingAmount, 2)} {MAKEPLURAL($unit)} missing[/color])
}
lathe-menu-material-raw-amount = {$amount} {$material}
lathe-menu-material-raw-amount-missing = {$amount} {$material} ([color=red]{$missingAmount} missing[/color])
#Sirius Add: item costs (whole entities) in lathe recipes
lathe-menu-sirius-item-amount = {$amount} × {$item}
lathe-menu-sirius-item-amount-missing = {$amount} × {$item} ([color=red]{$missing} missing[/color])
lathe-menu-sirius-one-of = One of: {$costs}
lathe-menu-no-materials-message = No materials loaded.
lathe-menu-connected-to-silo-message = Connected to material silo.
lathe-menu-fabricating-message = Fabricating...
lathe-menu-materials-title = Materials
lathe-menu-queue-title = Build Queue
# Misfits Change Add: Section header strings for the 3-panel lathe menu layout
lathe-menu-all-items-title = All Items
lathe-menu-craftable-title = Craftable Now
lathe-menu-blueprints-title = Blueprints
lathe-menu-search-blueprints = Search blueprints
