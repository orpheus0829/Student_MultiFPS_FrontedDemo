using System;
using UnityEngine;

[Serializable]
public sealed class AIDifficultyProfile
{
    [SerializeField, InspectorName("难度")] private AIDifficulty _difficulty;
    [SerializeField, Min(0.05f), InspectorName("反应时间")] private float _reactionTime = 0.4f;
    [SerializeField, Min(1f), InspectorName("感知距离")] private float _sightRange = 45f;
    [SerializeField, Range(0f, 20f), InspectorName("瞄准误差（度）")] private float _aimError = 4f;
    [SerializeField, Range(0f, 1f), InspectorName("开镜概率")] private float _aimChance = 0.65f;
    [SerializeField, Range(0f, 1f), InspectorName("掩体偏好")] private float _coverPreference = 0.65f;
    [SerializeField, Range(0.05f, 1f), InspectorName("撤退生命比例")] private float _retreatHealthRatio = 0.35f;
    [SerializeField, Min(0.1f), InspectorName("决策间隔")] private float _decisionInterval = 0.35f;
    [SerializeField, Min(0.1f), InspectorName("单次射击持续")] private float _fireDuration = 0.65f;
    [SerializeField, Min(0f), InspectorName("射击停顿")] private float _firePause = 0.45f;
    [SerializeField, Min(2f), InspectorName("理想交战距离")] private float _preferredRange = 15f;
    [SerializeField, InspectorName("允许战斗跳跃")] private bool _canCombatJump;
    [SerializeField, InspectorName("允许战术滑铲")] private bool _canTacticalSlide;

    public AIDifficulty Difficulty { get { return _difficulty; } }
    public float ReactionTime { get { return _reactionTime; } }
    public float SightRange { get { return _sightRange; } }
    public float AimError { get { return _aimError; } }
    public float AimChance { get { return _aimChance; } }
    public float CoverPreference { get { return _coverPreference; } }
    public float RetreatHealthRatio { get { return _retreatHealthRatio; } }
    public float DecisionInterval { get { return _decisionInterval; } }
    public float FireDuration { get { return _fireDuration; } }
    public float FirePause { get { return _firePause; } }
    public float PreferredRange { get { return _preferredRange; } }
    public bool CanCombatJump { get { return _canCombatJump; } }
    public bool CanTacticalSlide { get { return _canTacticalSlide; } }

    public static AIDifficultyProfile Create(AIDifficulty difficulty)
    {
        AIDifficultyProfile profile = new AIDifficultyProfile();
        profile._difficulty = difficulty;
        switch (difficulty)
        {
            case AIDifficulty.Easy:
                profile._reactionTime = 0.8f;
                profile._sightRange = 34f;
                profile._aimError = 9f;
                profile._aimChance = 0f;
                profile._coverPreference = 0.15f;
                profile._retreatHealthRatio = 0.2f;
                profile._decisionInterval = 0.75f;
                profile._fireDuration = 0.55f;
                profile._firePause = 0.8f;
                profile._preferredRange = 10f;
                break;
            case AIDifficulty.Hard:
                profile._reactionTime = 0.12f;
                profile._sightRange = 65f;
                profile._aimError = 1.1f;
                profile._aimChance = 1f;
                profile._coverPreference = 0.95f;
                profile._retreatHealthRatio = 0.55f;
                profile._decisionInterval = 0.16f;
                profile._fireDuration = 1.15f;
                profile._firePause = 0.18f;
                profile._preferredRange = 20f;
                profile._canCombatJump = true;
                profile._canTacticalSlide = true;
                break;
            default:
                profile._difficulty = AIDifficulty.Normal;
                profile._reactionTime = 0.35f;
                profile._sightRange = 48f;
                profile._aimError = 3.5f;
                profile._aimChance = 0.8f;
                profile._coverPreference = 0.75f;
                profile._retreatHealthRatio = 0.38f;
                profile._decisionInterval = 0.35f;
                profile._fireDuration = 0.85f;
                profile._firePause = 0.4f;
                profile._preferredRange = 16f;
                profile._canCombatJump = true;
                break;
        }
        return profile;
    }
}
