using Unity.AI.Navigation;
using UnityEngine;

[DefaultExecutionOrder(-500)]
[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshSurface))]
public sealed class AINavigationSurfaceCtrl : MonoBehaviour
{
    private NavMeshSurface _surface;

    private void Awake()
    {
        TryGetComponent(out _surface);
        if (!_surface)
        {
            Debug.LogError($"{name} 缺少 NavMeshSurface。", this);
            enabled = false;
            return;
        }

        if (!_surface.navMeshData)
        {
            Debug.LogError("AI 复活区没有烘焙 NavMesh 数据，请在场景中重新烘焙导航网格。", this);
            enabled = false;
        }
    }
}
