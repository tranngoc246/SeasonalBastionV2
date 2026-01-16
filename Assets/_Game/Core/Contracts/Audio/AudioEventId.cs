// PATCH v0.1.1 — Missing contract types to unblock compilation (Unity 2022.3)
// This patch only adds placeholder contract DTOs/Ids/States referenced by Part 25 interfaces.
// Keep these as pure data (no UnityEngine, no runtime logic).
using System;
using System.Collections.Generic;

namespace SeasonalBastion.Contracts
{

/// <summary>Strongly typed id for audio events (SFX/Music cues).</summary>
public readonly struct AudioEventId : IEquatable<AudioEventId>
{
    public readonly int Value;
    public AudioEventId(int value) => Value = value;
    public bool Equals(AudioEventId other) => Value == other.Value;
    public override bool Equals(object obj) => obj is AudioEventId other && Equals(other);
    public override int GetHashCode() => Value;
    public override string ToString() => $"AudioEventId({Value})";
}

}
