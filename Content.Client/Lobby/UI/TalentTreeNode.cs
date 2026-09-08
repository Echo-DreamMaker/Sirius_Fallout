// #Misfits Add - Talent tree node button.
// A single node in the talent tree: a toggle button showing tier, name and point cost, with a
// tree-aware state (Owned / Available / Locked when its prerequisite is not owned).

using Content.Client.Stylesheets;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Lobby.UI;

public sealed class TalentTreeNode : Button
{
    public string TraitId { get; }
    public string? PrerequisiteTraitId { get; }

    public bool Owned
    {
        get => Pressed;
        set
        {
            if (Pressed == value)
                return;
            Pressed = value;
            UpdateStyle();
        }
    }

    private bool _locked;
    public bool Locked
    {
        get => _locked;
        set
        {
            if (_locked == value)
                return;
            _locked = value;
            Disabled = value;
            UpdateStyle();
        }
    }

    public TalentTreeNode(string traitId, string? prerequisiteTraitId, int tier, string name, int points)
    {
        TraitId = traitId;
        PrerequisiteTraitId = prerequisiteTraitId;
        ToggleMode = true;
        HorizontalExpand = true;
        Margin = new Thickness(0, 2, 0, 2);

        AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Children =
            {
                new Label
                {
                    Text = tier > 0 ? tier.ToString() : string.Empty,
                    StyleClasses = { StyleBase.StyleClassLabelSubText },
                    MinWidth = 24,
                    MaxWidth = 24,
                    ClipText = true,
                    VerticalAlignment = VAlignment.Center,
                },
                new Label
                {
                    Text = name,
                    VerticalAlignment = VAlignment.Center,
                    Margin = new Thickness(8, 0, 0, 0),
                },
                new Label
                {
                    Text = points.ToString(),
                    StyleClasses = { StyleBase.StyleClassLabelSubText },
                    HorizontalAlignment = HAlignment.Right,
                    HorizontalExpand = true,
                    VerticalAlignment = VAlignment.Center,
                    Margin = new Thickness(8, 0, 0, 0),
                },
            },
        });
    }

    private void UpdateStyle()
    {
        if (_locked)
            Modulate = Color.Gray.WithAlpha(0.55f);
        else if (Pressed)
            Modulate = new Color(0.55f, 0.9f, 0.6f);
        else
            Modulate = Color.White;
    }
}
