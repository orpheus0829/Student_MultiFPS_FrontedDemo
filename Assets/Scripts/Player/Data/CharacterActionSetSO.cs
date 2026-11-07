using UnityEngine;

[CreateAssetMenu(fileName = "CharacterActionSet", menuName = "Gun Demo/玩家/角色动作集")]
public sealed class CharacterActionSetSO : ScriptableObject
{
    [Header("翻越动作")]
    [SerializeField, Min(0.01f), InspectorName("持续时间")] private float _vaultDuration = 0.55f;
    [SerializeField, Min(0f), InspectorName("抬升高度")] private float _vaultArcHeight = 0.25f;
    [SerializeField, Range(0.2f, 0.8f), InspectorName("到达障碍顶部的进度")]
    private float _vaultRiseEndProgress = 0.48f;
    [SerializeField, InspectorName("前进进度曲线")] private AnimationCurve _vaultForwardCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [SerializeField, InspectorName("抬升曲线")] private AnimationCurve _vaultHeightCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.5f, 1f),
        new Keyframe(1f, 0f));

    [Header("滑铲动作")]
    [SerializeField, Min(0.01f), InspectorName("持续时间")] private float _slideDuration = 1f;
    [SerializeField, Min(0f), InspectorName("起始速度")] private float _slideStartSpeed = 11f;
    [SerializeField, Min(0f), InspectorName("结束速度")] private float _slideEndSpeed = 4.5f;
    [SerializeField, Range(0.3f, 1f), InspectorName("碰撞体高度比例")] private float _slideHeightScale = 0.55f;
    [SerializeField, InspectorName("速度曲线")] private AnimationCurve _slideSpeedCurve =
        AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    public float VaultDuration
    {
        get
        {
            return _vaultDuration;
        }
    }

    public float SlideDuration
    {
        get
        {
            return _slideDuration;
        }
    }

    public float SlideHeightScale
    {
        get
        {
            return _slideHeightScale;
        }
    }

    public float EvaluateSlideSpeed(float progress)
    {
        float weight = Mathf.Clamp01(_slideSpeedCurve.Evaluate(Mathf.Clamp01(progress)));
        return Mathf.Lerp(_slideEndSpeed, _slideStartSpeed, weight);
    }

    public Vector3 EvaluateVaultPosition(Vector3 startPosition, Vector3 targetPosition, float progress)
    {
        float normalizedProgress = Mathf.Clamp01(progress);
        float forwardProgress = Mathf.Clamp01(_vaultForwardCurve.Evaluate(normalizedProgress));
        float heightProgress = _vaultHeightCurve.Evaluate(normalizedProgress);
        float riseProgress = Mathf.SmoothStep(
            0f,
            1f,
            Mathf.Clamp01(normalizedProgress / _vaultRiseEndProgress));
        Vector3 position = Vector3.Lerp(startPosition, targetPosition, forwardProgress);
        position.y = Mathf.Lerp(startPosition.y, targetPosition.y, riseProgress);
        position += Vector3.up * heightProgress * _vaultArcHeight;
        return position;
    }
}
