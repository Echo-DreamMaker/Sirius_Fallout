// #Sirius Add: событие после загрузки профиля — нужно для подробного осмотра персонажа.
namespace Content.Shared._Sirius.Preferences;

/// <summary>
/// Поднимается SharedHumanoidAppearanceSystem после применения профиля к мобу.
/// Свой event нужен, чтобы не конфликтовать с уже занятыми directed-подписками
/// на ProfileLoadFinishedEvent / RoundstartProfileLoadedEvent.
/// </summary>
public sealed class SiriusEroProfileLoadedEvent : EntityEventArgs
{
    public SiriusEroMode Ero { get; }

    public SiriusEroProfileLoadedEvent(SiriusEroMode ero)
    {
        Ero = ero;
    }
}
