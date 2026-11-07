using UnityEngine;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class FirstPersonWeaponOverlayCtrl : MonoBehaviour
{
    private const int FirstPersonLayer = 9;

    private Camera _worldCamera;
    private Camera _weaponCamera;
    private FirstPersonCameraCtrl _cameraCtrl;
    private UniversalAdditionalCameraData _worldCameraData;

    private void Awake()
    {
        TryGetComponent(out _worldCamera);
        TryGetComponent(out _cameraCtrl);
        TryGetComponent(out _worldCameraData);
        if (!_worldCamera || !_cameraCtrl || !_worldCameraData)
        {
            Debug.LogError($"{name} 缺少第一人称武器叠加相机所需组件。", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (!_worldCamera || !_worldCameraData)
        {
            return;
        }

        CreateWeaponCamera();
    }

    private void LateUpdate()
    {
        if (!_weaponCamera)
        {
            return;
        }

        _worldCamera.cullingMask &= ~(1 << FirstPersonLayer);
        _weaponCamera.fieldOfView = _worldCamera.fieldOfView;
        _weaponCamera.nearClipPlane = _worldCamera.nearClipPlane;
        _weaponCamera.farClipPlane = _worldCamera.farClipPlane;
        _weaponCamera.enabled = !_cameraCtrl.IsThirdPerson;
    }

    private void OnDisable()
    {
        if (_worldCameraData && _weaponCamera)
        {
            _worldCameraData.cameraStack.Remove(_weaponCamera);
        }
        if (_weaponCamera)
        {
            Destroy(_weaponCamera.gameObject);
            _weaponCamera = null;
        }
    }

    private void CreateWeaponCamera()
    {
        if (_weaponCamera)
        {
            return;
        }

        GameObject cameraObject = new GameObject("第一人称手枪叠加相机");
        cameraObject.transform.SetParent(transform, false);
        _weaponCamera = cameraObject.AddComponent<Camera>();
        _weaponCamera.clearFlags = CameraClearFlags.Depth;
        _weaponCamera.cullingMask = 1 << FirstPersonLayer;
        _weaponCamera.depth = _worldCamera.depth + 1f;
        _weaponCamera.allowHDR = _worldCamera.allowHDR;
        _weaponCamera.allowMSAA = _worldCamera.allowMSAA;
        UniversalAdditionalCameraData overlayData =
            cameraObject.GetComponent<UniversalAdditionalCameraData>();
        if (!overlayData)
        {
            overlayData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
        }
        overlayData.renderType = CameraRenderType.Overlay;
        overlayData.renderPostProcessing = false;
        _worldCameraData.cameraStack.Add(_weaponCamera);
        _worldCamera.cullingMask &= ~(1 << FirstPersonLayer);
        _weaponCamera.enabled = !_cameraCtrl.IsThirdPerson;
    }
}
