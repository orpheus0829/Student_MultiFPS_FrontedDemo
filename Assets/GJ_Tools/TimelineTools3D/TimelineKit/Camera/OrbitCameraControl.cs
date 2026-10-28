#if ENABLE_INPUT_SYSTEM
using UnityEngine;
using UnityEngine.InputSystem;

[AddComponentMenu("TimelineKit/OrbitCameraControl")]
[RequireComponent(typeof(TimelineCamRig))]
public class OrbitCameraControl : MonoBehaviour
{
    [Header("视角控制")]
    [Tooltip("勾选后需要按住鼠标右键才旋转视角；不勾则鼠标移动即转")]
    public bool requireMouseButton = false;
    [Tooltip("水平旋转灵敏度")]
    public float lookSpeedX = 4f;
    [Tooltip("垂直旋转灵敏度")]
    public float lookSpeedY = 2.5f;
    [Tooltip("垂直视角方向是否反转")]
    public bool invertY = false;
    [Tooltip("滚轮缩放速度")]
    public float scrollSpeed = 3f;
    [Tooltip("俯仰角限制(度)")]
    public Vector2 rotXClamp = new Vector2(-85f, 85f);
    [Tooltip("相机距离限制")]
    public Vector2 distanceRange = new Vector2(2f, 25f);

    private TimelineCamRig _rig;

    private void Awake()
    {
        _rig = GetComponent<TimelineCamRig>();
    }

    private void Update()
    {
        if (_rig == null)
        {
            return;
        }
        _rig.userDriving = false;
        if (_rig.isPlayingAnim && !_rig.userManualAllowed)
        {
            return;
        }
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }
        bool moved = false;
        if (!requireMouseButton || mouse.rightButton.isPressed)
        {
            Vector2 delta = mouse.delta.ReadValue();
            if (delta.sqrMagnitude > 0.0001f)
            {
                _rig.rotY += delta.x * lookSpeedX;
                float dir = invertY ? -1f : 1f;
                _rig.rotX = Mathf.Clamp(_rig.rotX + delta.y * lookSpeedY * dir, rotXClamp.x, rotXClamp.y);
                moved = true;
            }
        }
        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.0001f)
        {
            _rig.distance = Mathf.Clamp(_rig.distance - scroll * scrollSpeed, distanceRange.x, distanceRange.y);
            moved = true;
        }
        if (moved && _rig.isPlayingAnim)
        {
            _rig.userDriving = true;
            if (_rig.target != null)
            {
                Vector3 lookPoint = _rig.target.position + Vector3.up * _rig.lookHeight;
                transform.position = _rig.OrbitPosition(_rig.target);
                transform.rotation = Quaternion.LookRotation(lookPoint - transform.position);
            }
        }
    }
}
#endif
