// #Misfits Add - Talent tree view.
// Renders the talent trees as one menu: a horizontal set of columns, one per category tree.
// Each perk is a node placed in a tier row, and connector lines are drawn from a perk up to
// its prerequisite (solid when the prerequisite is owned, dim when locked).

using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls; // #Cythisiax Add - Tooltip control for rich-text tooltips
using Robust.Shared.Maths;
using Robust.Shared.Utility; // #Cythisiax Add - FormattedMessage for rich-text tooltips

namespace Content.Client.Lobby.UI;

public sealed record TreeBranch(string CategoryName, IReadOnlyList<TreePerk> Perks);

public sealed record TreePerk(string Id, string Name, int Points, int Tier, string? PrerequisiteId, string? Tooltip = null);

public sealed class TalentTreeView : Control
{
    private readonly Dictionary<string, TalentTreeNode> _nodes = new();
    private readonly Dictionary<string, string> _prereq = new();
    private readonly List<BoxContainer> _columns = new();

    private static readonly Color EdgeOwned = Color.FromHex("#7fb06b");
    private static readonly Color EdgeLocked = Color.FromHex("#555555");

    public event Action<string, bool>? NodePressed;

    public void SetTrees(IReadOnlyList<TreeBranch> branches)
    {
        RemoveAllChildren();
        _nodes.Clear();
        _prereq.Clear();
        _columns.Clear();

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        AddChild(root);

        foreach (var branch in branches)
        {
            var column = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                MinWidth = 230,
                Margin = new Thickness(0, 0, 16, 0),
            };

            column.AddChild(new Label
            {
                Text = branch.CategoryName,
                StyleClasses = { StyleBase.StyleClassLabelHeading },
                Margin = new Thickness(0, 0, 0, 6),
            });

            foreach (var perk in branch.Perks)
            {
                var node = new TalentTreeNode(perk.Id, perk.PrerequisiteId, perk.Tier, perk.Name, perk.Points);

                // #Cythisiax Fixed - render the tooltip as rich text. Requirement reasons contain
                // [color=...] markup (e.g. play-time counts), and the plain ToolTip string showed
                // it raw. FormattedMessage.FromMarkupPermissive parses the markup so colors render.
                if (!string.IsNullOrEmpty(perk.Tooltip))
                {
                    var formatted = new Tooltip();
                    formatted.SetMessage(FormattedMessage.FromMarkupPermissive(perk.Tooltip));
                    node.TooltipSupplier = _ => formatted;
                }
                else
                {
                    node.ToolTip = perk.Name;
                }

                node.OnPressed += _ => OnNodeToggled(node);
                _nodes[perk.Id] = node;
                if (perk.PrerequisiteId != null)
                    _prereq[perk.Id] = perk.PrerequisiteId;
                column.AddChild(node);
            }

            _columns.Add(column);
            root.AddChild(column);
        }
    }

    /// <summary>
    ///     Updates every node's Owned / Locked state based on the currently owned traits and
    ///     which traits are selectable for this character/job (usable).
    /// </summary>
    public void RefreshStates(HashSet<string> owned, HashSet<string> usable)
    {
        foreach (var (id, node) in _nodes)
        {
            node.Owned = owned.Contains(id);
            var prereq = node.PrerequisiteTraitId;
            node.Locked = !node.Owned && (!usable.Contains(id) || prereq != null && !owned.Contains(prereq));
        }
    }

    private void OnNodeToggled(TalentTreeNode node)
    {
        NodePressed?.Invoke(node.TraitId, node.Owned);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        foreach (var (childId, prereqId) in _prereq)
        {
            if (!_nodes.TryGetValue(childId, out var child) || !_nodes.TryGetValue(prereqId, out var parent))
                continue;

            var childPos = GetPositionInView(child);
            var parentPos = GetPositionInView(parent);

            var from = new Vector2(parentPos.X + parent.Size.X / 2f, parentPos.Y + parent.Size.Y);
            var to = new Vector2(childPos.X + child.Size.X / 2f, childPos.Y);
            handle.DrawLine(from, to, parent.Owned ? EdgeOwned : EdgeLocked);
        }
    }

    private Vector2 GetPositionInView(Control control)
    {
        var pos = control.Position;
        var parent = control.Parent;
        while (parent != null && parent != this)
        {
            pos += parent.Position;
            parent = parent.Parent;
        }
        return pos;
    }
}
