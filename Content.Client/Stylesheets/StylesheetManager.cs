using Content.Shared.CCVar;
using Content.Shared._Misfits.UI; // #Misfits Add - UI theme palette prototype
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;

namespace Content.Client.Stylesheets
{
    public sealed class StylesheetManager : IStylesheetManager
    {
        [Dependency] private readonly IUserInterfaceManager _userInterfaceManager = default!;
        [Dependency] private readonly IResourceCache _resourceCache = default!;
        [Dependency] private readonly IConfigurationManager _cfg = default!;
        [Dependency] private readonly IPrototypeManager _prototype = default!; // #Misfits Add

        public Stylesheet SheetNano { get; private set; } = default!;
        public Stylesheet SheetSpace { get; private set; } = default!;

        public void Initialize()
        {
            // #Misfits Add - Apply the saved UI theme before building the sheet so its rules pick up the colors.
            ApplySavedPalette();

            SheetNano = new StyleNano(_resourceCache).Stylesheet;
            SheetSpace = new StyleSpace(_resourceCache).Stylesheet;

            _userInterfaceManager.Stylesheet = SheetNano;
        }

        // #Misfits Add - Switch UI theme at runtime: resolve the prototype, rebuild and reassign the sheet.
        public void SetActivePalette(string id)
        {
            if (_prototype.TryIndex<UiThemePrototype>(id, out var theme))
                StyleNano.ApplyPalette(theme);

            SheetNano = new StyleNano(_resourceCache).Stylesheet;
            _userInterfaceManager.Stylesheet = SheetNano;
        }

        // #Misfits Add - Resolve the saved theme id; falls back to StyleNano's defaults if absent on this server.
        private void ApplySavedPalette()
        {
            if (_prototype.TryIndex<UiThemePrototype>(_cfg.GetCVar(CCVars.UiThemePalette), out var theme))
                StyleNano.ApplyPalette(theme);
        }
    }
}
