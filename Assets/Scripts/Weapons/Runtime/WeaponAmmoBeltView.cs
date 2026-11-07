using System.Collections.Generic;
using KINEMATION.FPSAnimationPack.Scripts.Weapon;
using KINEMATION.KAnimationCore.Runtime.Core;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class WeaponAmmoBeltView : MonoBehaviour
{
    private const float HiddenScaleFactor = 100f;

    [Header("弹链表现")]
    [SerializeField, InspectorName("弹药读取组件")] private MonoBehaviour _ammoProviderSource;
    [SerializeField, InspectorName("弹链节点")] private List<Transform> _ammoBelt = new List<Transform>();

    private readonly List<KTransform> _defaultLocalTransforms = new List<KTransform>();

    private IAmmoProvider _ammoProvider;
    private int _displayedAmmo = int.MinValue;

    private void Awake()
    {
        _ammoProvider = _ammoProviderSource as IAmmoProvider;
        if (_ammoProvider == null)
        {
            _ammoProvider = GetComponentInParent<IAmmoProvider>();
        }

        CacheDefaultTransforms();
        if (_ammoProvider == null || _ammoBelt.Count == 0)
        {
            Debug.LogError($"{name} 的弹链表现引用未配置完整。", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        _displayedAmmo = int.MinValue;
    }

    private void LateUpdate()
    {
        int activeAmmo = _ammoProvider.GetActiveAmmo();
        if (activeAmmo == _displayedAmmo)
        {
            return;
        }

        RefreshBelt(activeAmmo);
        _displayedAmmo = activeAmmo;
    }

    private void CacheDefaultTransforms()
    {
        _defaultLocalTransforms.Clear();
        for (int i = 0; i < _ammoBelt.Count; i++)
        {
            Transform beltNode = _ammoBelt[i];
            _defaultLocalTransforms.Add(beltNode
                ? new KTransform(beltNode, true)
                : KTransform.Identity);
        }
    }

    private void RefreshBelt(int activeAmmo)
    {
        RestoreDefaultTransforms();

        int count = _ammoBelt.Count;
        if (activeAmmo > count)
        {
            return;
        }

        int lastHiddenIndex = Mathf.Clamp(count - activeAmmo, 0, count - 1);
        for (int i = 0; i <= lastHiddenIndex; i++)
        {
            Transform current = _ammoBelt[i];
            if (!current)
            {
                continue;
            }

            KTransform nextWorldTransform = KTransform.Identity;
            if (i < count - 1 && _ammoBelt[i + 1])
            {
                nextWorldTransform = new KTransform(_ammoBelt[i + 1]);
            }

            current.localScale /= HiddenScaleFactor;
            if (i >= count - 1 || !_ammoBelt[i + 1])
            {
                continue;
            }

            Transform next = _ammoBelt[i + 1];
            next.localScale *= HiddenScaleFactor;
            next.position = nextWorldTransform.position;
            next.rotation = nextWorldTransform.rotation;
        }
    }

    private void RestoreDefaultTransforms()
    {
        int count = Mathf.Min(_ammoBelt.Count, _defaultLocalTransforms.Count);
        for (int i = 0; i < count; i++)
        {
            Transform beltNode = _ammoBelt[i];
            if (!beltNode)
            {
                continue;
            }

            KTransform localTransform = _defaultLocalTransforms[i];
            beltNode.localPosition = localTransform.position;
            beltNode.localRotation = localTransform.rotation;
            beltNode.localScale = localTransform.scale;
        }
    }
}
