using UnityEngine;

[DisallowMultipleComponent]
public sealed class AIDebugHud : MonoBehaviour
{
    [SerializeField, InspectorName("显示调试文字")] private bool _showText;
    [SerializeField, InspectorName("显示场景辅助线")] private bool _showGizmos = true;
    private AIPlayerCtrl _ai;

    private void Awake()
    {
        TryGetComponent(out _ai);
    }

    private void OnGUI()
    {
        if (!_showText || !_ai)
        {
            return;
        }
        AITrackedTarget target = _ai.Target;
        string targetName = target != null && target.Actor ? target.Actor.name : "无";
        string text = $"{name} [{_ai.Difficulty}]\n" +
            $"行为: {_ai.CurrentBehavior}  开火: {_ai.FirePhase}\n" +
            $"目标: {targetName}  可见: {target != null && target.Visible}\n" +
            $"信心: {(target != null ? target.Confidence : 0f):F2}\n" +
            $"卡死恢复: {_ai.StuckRecoveryCount}  路径失败: {_ai.PathFailureCount}";
        Vector3 screen = Camera.main
            ? Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 2.2f)
            : Vector3.zero;
        if (screen.z > 0f)
        {
            GUI.Label(new Rect(screen.x, Screen.height - screen.y, 220f, 95f), text);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!_showGizmos || !_ai || _ai.Profile == null)
        {
            return;
        }
        Gizmos.color = Color.yellow;
        float half = _ai.Profile.FovDegrees * 0.5f;
        Gizmos.DrawRay(transform.position + Vector3.up * 1.35f,
            Quaternion.Euler(0f, -half, 0f) * transform.forward * _ai.Profile.SightRange);
        Gizmos.DrawRay(transform.position + Vector3.up * 1.35f,
            Quaternion.Euler(0f, half, 0f) * transform.forward * _ai.Profile.SightRange);
        if (_ai.Target != null)
        {
            Gizmos.color = _ai.Target.Visible ? Color.red : Color.cyan;
            Gizmos.DrawLine(transform.position + Vector3.up, _ai.Target.LastKnownPosition);
            Gizmos.DrawWireSphere(_ai.Target.LastKnownPosition,
                _ai.Target.UncertaintyRadius);
        }
    }
}
