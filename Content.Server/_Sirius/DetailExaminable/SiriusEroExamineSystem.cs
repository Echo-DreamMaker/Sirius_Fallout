// #Sirius Add: ЕРП-статус в подробном осмотре персонажа.
using System;
using Content.Server.DetailExaminable;
using Content.Shared.Humanoid;
using Content.Shared._Sirius.Preferences;
using Robust.Shared.Localization;

namespace Content.Server._Sirius.DetailExaminable;

/// <summary>
/// Добавляет цветную ЕРП-строку в описание, которое показывается командой «Осмотреть».
/// Общий examine уже показывает её через SharedHumanoidAppearanceSystem, но подробный
/// осмотр использует отдельный DetailExaminableComponent.Content.
/// </summary>
public sealed partial class SiriusEroExamineSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HumanoidAppearanceComponent, SiriusEroProfileLoadedEvent>(OnProfileLoaded);
    }

    private void OnProfileLoaded(
        EntityUid uid,
        HumanoidAppearanceComponent appearance,
        SiriusEroProfileLoadedEvent args)
    {
        var details = EnsureComp<DetailExaminableComponent>(uid);
        var content = details.Content;

        // Убираем старые строки после смены режима профиля.
        foreach (var mode in new[] { SiriusEroMode.Light, SiriusEroMode.Hard })
        {
            var oldLine = GetLine(mode);
            content = content.Replace(oldLine + "\n", string.Empty, StringComparison.Ordinal)
                             .Replace(oldLine, string.Empty, StringComparison.Ordinal);
        }

        content = content.TrimEnd('\n');

        if (args.Ero != SiriusEroMode.None)
        {
            var line = GetLine(args.Ero);
            content = string.IsNullOrEmpty(content) ? line : content + "\n" + line;
        }

        details.Content = content;
    }

    private static string GetLine(SiriusEroMode mode)
    {
        return Robust.Shared.Localization.Loc.GetString($"sirius-ero-examine-{mode.ToString().ToLowerInvariant()}");
    }
}
