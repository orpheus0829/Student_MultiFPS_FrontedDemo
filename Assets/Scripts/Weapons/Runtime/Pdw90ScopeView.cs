using UnityEngine;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public sealed class Pdw90ScopeView : MonoBehaviour
{
    private const int ThirdPersonLayer = 8;
    private const int FirstPersonLayer = 9;
    private static readonly int ScopeTextureId = Shader.PropertyToID("_ScopeTex");
    private static readonly int AimWeightId = Shader.PropertyToID("_AimWeight");

    [SerializeField] private Material _lensMaterial;
    [SerializeField, Min(1f)] private float _magnification = 2.2f;
    [SerializeField, Range(128, 1024)] private int _renderTextureSize = 512;
    [SerializeField, Min(0.005f)] private float _lensDiameter = 0.027f;
    [SerializeField, Range(0f, 1f)] private float _lensVisibleAimProgress = 0.65f;

    private PlayerAimCtrl _aimCtrl;
    private FirstPersonCameraCtrl _cameraCtrl;
    private Camera _worldCamera;
    private Camera _scopeCamera;
    private MeshRenderer _lensRenderer;
    private RenderTexture _scopeTexture;
    private Material _runtimeLensMaterial;
    private bool _initialized;

    public Camera ScopeCamera => _scopeCamera;
    public MeshRenderer LensRenderer => _lensRenderer;
    public RenderTexture ScopeTexture => _scopeTexture;

    public void Initialize(
        PlayerAimCtrl aimCtrl,
        Camera worldCamera,
        FirstPersonCameraCtrl cameraCtrl,
        Vector3 lensCenter)
    {
        if (_initialized || !aimCtrl || !worldCamera || !_lensMaterial)
        {
            return;
        }

        _initialized = true;
        _aimCtrl = aimCtrl;
        _worldCamera = worldCamera;
        _cameraCtrl = cameraCtrl;
        CreateScopeTexture();
        CreateScopeCamera();
        CreateLens(lensCenter);
        RefreshVisibility();
    }

    private void LateUpdate()
    {
        if (!_initialized)
        {
            return;
        }

        RefreshScopeCamera();
        RefreshVisibility();
    }

    private void OnDisable()
    {
        if (_scopeCamera)
        {
            _scopeCamera.enabled = false;
        }
        if (_lensRenderer)
        {
            _lensRenderer.enabled = false;
        }
    }

    private void OnDestroy()
    {
        if (_scopeCamera)
        {
            Destroy(_scopeCamera.gameObject);
        }
        if (_scopeTexture)
        {
            _scopeTexture.Release();
            Destroy(_scopeTexture);
        }
        if (_runtimeLensMaterial)
        {
            Destroy(_runtimeLensMaterial);
        }
    }

    private void CreateScopeTexture()
    {
        int size = Mathf.Clamp(_renderTextureSize, 128, 1024);
        _scopeTexture = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32)
        {
            name = "PDW90 瞄具放大画面",
            antiAliasing = 1,
            filterMode = FilterMode.Bilinear,
            useMipMap = false,
            autoGenerateMips = false
        };
        _scopeTexture.Create();
    }

    private void CreateScopeCamera()
    {
        GameObject cameraObject = new GameObject("PDW90 瞄具相机");
        cameraObject.transform.SetParent(_worldCamera.transform, false);
        _scopeCamera = cameraObject.AddComponent<Camera>();
        _scopeCamera.CopyFrom(_worldCamera);
        _scopeCamera.targetTexture = _scopeTexture;
        _scopeCamera.cullingMask &= ~(
            1 << FirstPersonLayer |
            1 << ThirdPersonLayer);
        _scopeCamera.depth = -100f;
        _scopeCamera.aspect = 1f;
        _scopeCamera.enabled = false;

        UniversalAdditionalCameraData cameraData =
            _scopeCamera.GetUniversalAdditionalCameraData();
        cameraData.renderType = CameraRenderType.Base;
        cameraData.renderPostProcessing = false;
        cameraData.cameraStack.Clear();
    }

    private void CreateLens(Vector3 lensCenter)
    {
        GameObject lensObject = new GameObject("PDW90 真实瞄具镜片");
        lensObject.layer = FirstPersonLayer;
        lensObject.transform.SetParent(transform, false);
        lensObject.transform.localPosition = lensCenter + Vector3.back * 0.0015f;
        lensObject.transform.localRotation = Quaternion.identity;
        lensObject.transform.localScale = Vector3.one * _lensDiameter;

        MeshFilter filter = lensObject.AddComponent<MeshFilter>();
        filter.sharedMesh = CreateQuadMesh();
        _lensRenderer = lensObject.AddComponent<MeshRenderer>();
        _runtimeLensMaterial = new Material(_lensMaterial)
        {
            name = "PDW90 瞄具镜片（运行时）"
        };
        _runtimeLensMaterial.SetTexture(ScopeTextureId, _scopeTexture);
        _lensRenderer.sharedMaterial = _runtimeLensMaterial;
        _lensRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _lensRenderer.receiveShadows = false;
        _lensRenderer.enabled = false;
    }

    private void RefreshScopeCamera()
    {
        if (!_scopeCamera || !_worldCamera)
        {
            return;
        }

        float halfFov = _worldCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float magnifiedHalfFov = Mathf.Atan(
            Mathf.Tan(halfFov) / Mathf.Max(1f, _magnification));
        _scopeCamera.fieldOfView = magnifiedHalfFov * 2f * Mathf.Rad2Deg;
        _scopeCamera.nearClipPlane = _worldCamera.nearClipPlane;
        _scopeCamera.farClipPlane = _worldCamera.farClipPlane;
        _scopeCamera.cullingMask = _worldCamera.cullingMask & ~(
            1 << FirstPersonLayer |
            1 << ThirdPersonLayer);
    }

    private void RefreshVisibility()
    {
        float progress = _aimCtrl ? _aimCtrl.AimProgress : 0f;
        bool firstPerson = !_cameraCtrl || !_cameraCtrl.IsThirdPerson;
        bool canRender = isActiveAndEnabled && firstPerson && progress > 0.02f;
        if (_scopeCamera)
        {
            _scopeCamera.enabled = canRender;
        }
        if (_lensRenderer)
        {
            _lensRenderer.enabled = canRender && progress >= _lensVisibleAimProgress;
        }
        if (_runtimeLensMaterial)
        {
            _runtimeLensMaterial.SetFloat(AimWeightId, progress);
        }
    }

    private static Mesh CreateQuadMesh()
    {
        Mesh mesh = new Mesh { name = "PDW90 瞄具镜片网格" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f)
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
