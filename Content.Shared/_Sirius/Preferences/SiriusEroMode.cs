// #Sirius Add: ERP-режим персонажа и пороги разблокировки по общему времени игры.
using System;

namespace Content.Shared._Sirius.Preferences;

/// <summary>
/// Настройка ЕРП-режима персонажа. Значение по умолчанию (0) означает «Без ЕРП»,
/// поэтому новые персонажи и мобы не получают дополнительной строки в описании.
/// </summary>
public enum SiriusEroMode
{
    None = 0,
    Light = 1,
    Hard = 2,
}

public static class SiriusEroModeExtensions
{
    // #Sirius Add: пороги заданы в минутах общего времени игры, не в часах.
    public static readonly TimeSpan LightUnlock = TimeSpan.FromMinutes(120);
    public static readonly TimeSpan HardUnlock = TimeSpan.FromMinutes(1200);

    public static bool IsUnlocked(this SiriusEroMode mode, TimeSpan overallPlaytime)
    {
        return mode switch
        {
            SiriusEroMode.None => true,
            SiriusEroMode.Light => overallPlaytime >= LightUnlock,
            SiriusEroMode.Hard => overallPlaytime >= HardUnlock,
            _ => false,
        };
    }
}
