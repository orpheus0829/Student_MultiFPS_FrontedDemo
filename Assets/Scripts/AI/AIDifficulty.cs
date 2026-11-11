using System;
using UnityEngine;

public enum AIDifficulty
{
    [InspectorName("简单")] Easy = 0,
    [InspectorName("普通")] Normal = 1,
    [InspectorName("困难")] Hard = 2
}

public interface IAIDifficultyControl
{
    AIDifficulty Difficulty { get; }
    event Action<AIDifficulty> DifficultyChanged;
    void SetDifficulty(AIDifficulty difficulty);
    bool TrySetDifficulty(string difficulty);
}
