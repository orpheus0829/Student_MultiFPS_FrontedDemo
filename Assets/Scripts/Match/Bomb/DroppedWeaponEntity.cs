using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DroppedWeaponEntity : MonoBehaviour
{
    private static readonly List<DroppedWeaponEntity> All =
        new List<DroppedWeaponEntity>();
    [SerializeField] private string _weaponResourceId;
    [SerializeField] private int _magazineAmmo;
    [SerializeField] private int _reserveAmmo;

    public string WeaponResourceId { get { return _weaponResourceId; } }
    public int MagazineAmmo { get { return _magazineAmmo; } }
    public int ReserveAmmo { get { return _reserveAmmo; } }

    private void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
    }

    private void OnDisable()
    {
        All.Remove(this);
    }

    public static void ClearAll()
    {
        for (int i = All.Count - 1; i >= 0; i--)
        {
            if (All[i]) Destroy(All[i].gameObject);
        }
        All.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        All.Clear();
    }

    public static DroppedWeaponEntity Create(
        PlayerWeaponViewCtrl sourceView,
        string weaponResourceId,
        int magazineAmmo,
        int reserveAmmo,
        Vector3 position,
        Vector3 impulse)
    {
        if (!sourceView || !sourceView.ThirdPersonWeapon ||
            string.IsNullOrWhiteSpace(weaponResourceId))
        {
            return null;
        }

        return CreateFromModel(sourceView.ThirdPersonWeapon, weaponResourceId,
            magazineAmmo, reserveAmmo, position,
            sourceView.ThirdPersonWeapon.transform.rotation, impulse);
    }

    public static DroppedWeaponEntity CreateFromModel(
        GameObject sourceModel,
        string weaponResourceId,
        int magazineAmmo,
        int reserveAmmo,
        Vector3 position,
        Quaternion rotation,
        Vector3 impulse)
    {
        if (!sourceModel || string.IsNullOrWhiteSpace(weaponResourceId)) return null;
        GameObject dropped = Instantiate(sourceModel, position, rotation);
        dropped.name = "掉落武器";
        dropped.transform.SetParent(null, true);

        Animator[] animators = dropped.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++) animators[i].enabled = false;
        MonoBehaviour[] behaviours = dropped.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++) behaviours[i].enabled = false;
        Collider[] oldColliders = dropped.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < oldColliders.Length; i++) oldColliders[i].enabled = false;

        Renderer[] renderers = dropped.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            Destroy(dropped);
            return null;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        BoxCollider collider = dropped.AddComponent<BoxCollider>();
        collider.center = dropped.transform.InverseTransformPoint(bounds.center);
        Vector3 scale = dropped.transform.lossyScale;
        collider.size = new Vector3(
            bounds.size.x / Mathf.Max(0.01f, Mathf.Abs(scale.x)),
            bounds.size.y / Mathf.Max(0.01f, Mathf.Abs(scale.y)),
            bounds.size.z / Mathf.Max(0.01f, Mathf.Abs(scale.z)));
        Rigidbody body = dropped.AddComponent<Rigidbody>();
        body.mass = 2f;
        body.AddForce(impulse, ForceMode.VelocityChange);

        DroppedWeaponEntity entity = dropped.AddComponent<DroppedWeaponEntity>();
        entity._weaponResourceId = weaponResourceId;
        entity._magazineAmmo = Mathf.Max(0, magazineAmmo);
        entity._reserveAmmo = Mathf.Max(0, reserveAmmo);
        return entity;
    }
}
