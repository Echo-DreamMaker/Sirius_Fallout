// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Misfits.Genetics.Console;

/// <summary>
/// Relays console speech to the server chat system without coupling shared genetics to server chat APIs.
/// </summary>
public sealed record GeneticsConsoleSpeakEvent(string Message);
