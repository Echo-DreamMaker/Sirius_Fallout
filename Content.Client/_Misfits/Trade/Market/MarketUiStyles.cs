// #Cythisiax Add - Shared visual language for the Wendover market terminal.
using System.Linq;
using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using static Robust.Client.UserInterface.StylesheetHelpers;

namespace Content.Client._Misfits.Trade.Market;

public static class MarketUiStyles
{
    public const string Panel = "MarketPanel";

    private static readonly Color Green = Color.FromHex("#33FF33");
    private static readonly Color DimGreen = Color.FromHex("#5fbf5f");
    private static readonly Color DarkGreen = Color.FromHex("#0f3d0f");
    private static readonly Color Background = Color.FromHex("#0a0a0a");
    private static readonly Color PanelBackground = Color.FromHex("#0d160d");

    public static Stylesheet Create()
    {
        var baseRules = IoCManager.Resolve<IStylesheetManager>().SheetNano.Rules;
        return new Stylesheet(baseRules.Concat(CreateRules()).ToList());
    }

    public static void ApplyActionButton(Button button)
    {
        button.StyleBoxOverride = Box(PanelBackground, Green, new Thickness(2));
        button.MinHeight = System.Math.Max(button.MinHeight, 28);
        button.ModulateSelfOverride = Green;
    }

    private static StyleRule[] CreateRules()
    {
        return
        [
            Element<PanelContainer>()
                .Class(Panel)
                .Prop(PanelContainer.StylePropertyPanel, Box(PanelBackground, DarkGreen, new Thickness(1))),

            Element<PanelContainer>()
                .Class(DefaultWindow.StyleClassWindowPanel)
                .Prop(PanelContainer.StylePropertyPanel, Box(Background, DimGreen, new Thickness(2))),

            Element<PanelContainer>()
                .Class(DefaultWindow.StyleClassWindowHeader)
                .Prop(PanelContainer.StylePropertyPanel, Box(DarkGreen, DimGreen, new Thickness(0, 0, 0, 2))),

            Element<Label>()
                .Class(DefaultWindow.StyleClassWindowTitle)
                .Prop(Label.StylePropertyFontColor, Green),
        ];
    }

    private static StyleBoxFlat Box(Color background, Color border, Thickness thickness)
    {
        return new StyleBoxFlat
        {
            BackgroundColor = background,
            BorderColor = border,
            BorderThickness = thickness,
        };
    }
}
