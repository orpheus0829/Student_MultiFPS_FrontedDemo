using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class WeaponBoneVisibilityCtrl : MonoBehaviour
{
    [Header("始终隐藏")]
    [SerializeField, InspectorName("隐藏节点")] private List<Transform> _defaultHiddenBones =
        new List<Transform>();

    [Header("动画事件控制")]
    [SerializeField, InspectorName("切换节点")] private List<Transform> _eventBones =
        new List<Transform>();

    private bool _isVisible = true;

    private void Start()
    {
        ApplyDefaultVisibility();
    }

    private void LateUpdate()
    {
        ApplyDefaultVisibility();
        for (int i = 0; i < _eventBones.Count; i++)
        {
            Transform bone = _eventBones[i];
            if (bone)
            {
                bone.localScale = _isVisible ? Vector3.one : Vector3.zero;
            }
        }
    }

    public void SetBoneVisibility(int value)
    {
        _isVisible = value == 1;
    }

    private void ApplyDefaultVisibility()
    {
        for (int i = 0; i < _defaultHiddenBones.Count; i++)
        {
            Transform bone = _defaultHiddenBones[i];
            if (bone)
            {
                bone.localScale = Vector3.zero;
            }
        }
    }
}
