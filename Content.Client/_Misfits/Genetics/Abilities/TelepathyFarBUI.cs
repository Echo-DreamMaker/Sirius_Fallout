// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Misfits.Genetics.Abilities;
using Robust.Shared.GameObjects;

namespace Content.Client._Misfits.Genetics.Abilities;

public sealed partial class TelepathyFarBUI : BoundUserInterface
{
    private TelepathyFarWindow? _window;

    public TelepathyFarBUI(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        var maxLength = EntMan.GetComponentOrNull<TelepathyActionComponent>(Owner)?.MaxLength ?? 30;

        _window = new TelepathyFarWindow();
        _window.OnClose += Close;
        _window.OnSend += (target, msg) =>
        {
            msg = msg.Substring(0, Math.Min(maxLength, msg.Length));
            SendPredictedMessage(new TelepathyFarChosenMessage(target, msg));
            _window?.Close();
        };
        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is TelepathyFarState farState)
            _window?.SetPlayers(farState.Players);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        _window?.Orphan();
    }
}
