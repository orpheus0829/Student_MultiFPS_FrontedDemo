using UnityEngine;

[AddComponentMenu("TimelineKit/TimelineCamRig")]
public class TimelineCamRig : MonoBehaviour
{
    [Header("跟随目标")]
    [Tooltip("第三人称跟随的目标（也是运镜轨的参照锚点）")]
    public Transform target;
    [Tooltip("空闲时是否自动环绕跟随并看向目标")]
    public bool autoFollow = true;

    [Header("环绕机位")]
    [Tooltip("俯仰角(度)，正数向下看")]
    public float rotX = 30f;
    [Tooltip("水平环绕角(度)")]
    public float rotY = 0f;
    [Tooltip("相机离目标水平距离")]
    public float distance = 5f;
    [Tooltip("相机高度(相对目标脚底)")]
    public float height = 1.5f;
    [Tooltip("看向目标时在目标上的抬高量")]
    public float lookHeight = 1.2f;

    [Header("状态")]
    [Tooltip("运镜轨接管中(自动置位)，此时不自动跟随")]
    public bool isPlayingAnim;
    [HideInInspector]
    public bool userManualAllowed;
    [HideInInspector]
    public bool userDriving;

    private bool _cacheValid;
    private float _cacheRotX;
    private float _cacheRotY;
    private float _cacheDistance;
    private float _cacheHeight;
    private Vector3 _cachePos;
    private Quaternion _cacheRot;
    private int _activeClips;

    public bool HasNormalState => _cacheValid;

    public Vector3 NormalPosition => _cachePos;

    public Vector3 OrbitPosition(Transform anchor)
    {
        return anchor.position + (Quaternion.Euler(rotX, rotY, 0f) * Vector3.back) * distance + Vector3.up * height;
    }

    public Vector3 GetNormalOrbitPosition(Transform anchor)
    {
        if (!_cacheValid)
        {
            return OrbitPosition(anchor);
        }
        return anchor.position + (Quaternion.Euler(_cacheRotX, _cacheRotY, 0f) * Vector3.back) * _cacheDistance + Vector3.up * _cacheHeight;
    }

    public void Acquire()
    {
        _activeClips++;
        CameraControlAuthority.Acquire(this);
        isPlayingAnim = true;
        if (_activeClips == 1)
        {
            _cacheValid = true;
            _cacheRotX = rotX;
            _cacheRotY = rotY;
            _cacheDistance = distance;
            _cacheHeight = height;
            _cachePos = transform.position;
            _cacheRot = transform.rotation;
        }
    }

    public void Release(bool restore)
    {
        if (_activeClips <= 0)
        {
            return;
        }
        _activeClips--;
        CameraControlAuthority.Release(this);
        if (_activeClips <= 0)
        {
            _activeClips = 0;
            isPlayingAnim = false;
            if (_cacheValid && restore)
            {
                rotX = _cacheRotX;
                rotY = _cacheRotY;
                distance = _cacheDistance;
                height = _cacheHeight;
                transform.position = _cachePos;
                transform.rotation = _cacheRot;
            }
            else if (!restore)
            {
                SyncParamsFromCamera();
            }
            _cacheValid = false;
        }
    }

    public void SyncParamsFromCamera()
    {
        if (target == null)
        {
            return;
        }
        Vector3 rel = transform.position - target.position;
        float flatR = Mathf.Sqrt(rel.x * rel.x + rel.z * rel.z);
        float dy = rel.y - height;
        float d = Mathf.Sqrt(dy * dy + flatR * flatR);
        if (d < 0.001f)
        {
            return;
        }
        distance = d;
        rotX = Mathf.Asin(Mathf.Clamp(dy / d, -1f, 1f)) * Mathf.Rad2Deg;
        rotY = Mathf.Atan2(-rel.x, -rel.z) * Mathf.Rad2Deg;
    }

    private void LateUpdate()
    {
        if (isPlayingAnim || !autoFollow || target == null)
        {
            return;
        }
        Vector3 lookTarget = target.position + Vector3.up * lookHeight;
        transform.position = OrbitPosition(target);
        transform.rotation = Quaternion.LookRotation(lookTarget - transform.position);
    }
}
