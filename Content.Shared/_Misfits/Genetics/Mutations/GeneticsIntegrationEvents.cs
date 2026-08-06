// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Misfits.Genetics.Mutations;

/// <summary>Raised by the target polymorph system after a replacement entity is created.</summary>
public sealed class PolymorphedEvent(EntityUid oldEntity, EntityUid newEntity) : EntityEventArgs
{
    public EntityUid OldEntity { get; } = oldEntity;
    public EntityUid NewEntity { get; } = newEntity;
}

/// <summary>Raised when another system randomizes a mob's identity/DNA.</summary>
public sealed class DnaScrambledEvent : EntityEventArgs;
