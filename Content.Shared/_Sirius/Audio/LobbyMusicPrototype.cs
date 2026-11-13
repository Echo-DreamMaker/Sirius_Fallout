// #Sirius Add - метаданные трека лобби для HUD.
// Название и исполнитель хранятся в прототипах, а не только в Vorbis-тегах .ogg:
// теги легко потерять при перекодировании, а прототип виден в одном месте и
// правится без переупаковки звука.
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Sirius.Audio;

/// <summary>
/// Отображаемое название и исполнитель трека, играющего в лобби.
/// </summary>
[Prototype("lobbyMusic")]
public sealed partial class LobbyMusicPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Путь к файлу трека, как он указан в коллекции LobbyMusic.</summary>
    [DataField(required: true)]
    public ResPath File = default!;

    /// <summary>Название трека. Если пусто — берётся тег TITLE из файла.</summary>
    [DataField]
    public string? Title;

    /// <summary>Исполнитель. Если пусто — берётся тег ARTIST из файла.</summary>
    [DataField]
    public string? Artist;
}
