using Content.Client._Misfits.Administration.UI;
using Content.Client.Eui;
using Content.Shared._Misfits.Supporter;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Client._Misfits.Administration;

[UsedImplicitly]
public sealed class SupporterManagerEui : BaseEui
{
    private SupporterManagerWindow _window = default!;

    public override void Opened()
    {
        _window = new SupporterManagerWindow();

        _window.OnSetSupporter += (guid, username, title, color, tier) =>
        {
            SendMessage(new SupporterSetMessage
            {
                UserId = guid,
                Username = username,
                Title = title,
                NameColor = color,
                Tier = tier, // #Cythisiax Added - Patreon tier assignment
            });
        };

        _window.OnRemoveSupporter += guid =>
        {
            SendMessage(new SupporterRemoveMessage { UserId = guid });
        };

        _window.OnClose += () => SendMessage(new CloseEuiMessage());
        _window.OpenCentered();
    }

    public override void Closed()
    {
        _window.Close();
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is not SupporterManagerState s)
            return;

        _window.Populate(s.Supporters, s.StatusMessage);
    }
}
