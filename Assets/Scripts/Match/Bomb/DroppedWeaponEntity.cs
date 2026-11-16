using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DroppedWeaponEntity : MonoBehaviour, IWorldInteraction
{
    private static readonly List<DroppedWeaponEntity> All =
        new List<DroppedWeaponEntity>();
    [SerializeField] private string _weaponResourceId;
    [SerializeField] private int _magazineAmmo;
    [SerializeField] private int _reserveAmmo;
    private bool _isBeingPickedUp;

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

    public bool CanInteract(GameObject interactor)
    {
        return !_isBeingPickedUp && interactor &&
            interactor.TryGetComponent(out PlayerWeaponWorldInteractionCtrl interactionCtrl) &&
            !interactionCtrl.IsBusy;
    }

    public async void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor) ||
            !interactor.TryGetComponent(out PlayerWeaponWorldInteractionCtrl interactionCtrl))
        {
            return;
        }

        _isBeingPickedUp = true;
        bool acquired = await interactionCtrl.TryAcquireWeaponAsync(
            _weaponResourceId,
            _magazineAmmo,
            _reserveAmmo);
        if (!this)
        {
            return;
        }

        if (acquired)
        {
            Destroy(gameObject);
            return;
        }

        _isBeingPickedUp = false;
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
        Bounds bounds = default;
        bool hasModelBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (!IsWeaponModelRenderer(renderer))
            {
                continue;
            }

            if (!hasModelBounds)
            {
                bounds = renderer.bounds;
                hasModelBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasModelBounds)
        {
            Destroy(dropped);
            return null;
        }

        BoxCollider collider = dropped.AddComponent<BoxCollider>();
        collider.center = dropped.transform.InverseTransformPoint(bounds.center);
        Vector3 scale = dropped.transform.lossyScale;
        Vector3 colliderSize = new Vector3(
            bounds.size.x / Mathf.Max(0.01f, Mathf.Abs(scale.x)),
            bounds.size.y / Mathf.Max(0.01f, Mathf.Abs(scale.y)),
            bounds.size.z / Mathf.Max(0.01f, Mathf.Abs(scale.z)));
        collider.size = new Vector3(
            Mathf.Max(0.08f, colliderSize.x),
            Mathf.Max(0.08f, colliderSize.y),
            Mathf.Max(0.08f, colliderSize.z));
        Rigidbody body = dropped.AddComponent<Rigidbody>();
        body.mass = 2f;
        body.useGravity = true;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.AddForce(impulse, ForceMode.VelocityChange);

        DroppedWeaponEntity entity = dropped.AddComponent<DroppedWeaponEntity>();
        entity._weaponResourceId = weaponResourceId;
        entity._magazineAmmo = Mathf.Max(0, magazineAmmo);
        entity._reserveAmmo = Mathf.Max(0, reserveAmmo);
        WorldInteractable interactable = dropped.AddComponent<WorldInteractable>();
        interactable.Configure(
            "拾取",
            GJInputAction.PickupWeapon,
            entity,
            3.2f);
        IgnorePlayerCollisions(dropped);
        return entity;
    }

    public static void IgnorePlayerCollisions(GameObject droppedWeapon)
    {
        if (!droppedWeapon)
        {
            return;
        }

        Collider[] weaponColliders = droppedWeapon.GetComponentsInChildren<Collider>(true);
        IReadOnlyList<CharacterHealth> actors = CharacterHealth.Actors;
        for (int actorIndex = 0; actorIndex < actors.Count; actorIndex++)
        {
            CharacterHealth health = actors[actorIndex];
            if (!health)
            {
                continue;
            }

            Collider[] characterColliders = health.GetComponentsInChildren<Collider>(true);
            for (int weaponIndex = 0; weaponIndex < weaponColliders.Length; weaponIndex++)
            {
                Collider weaponCollider = weaponColliders[weaponIndex];
                if (!weaponCollider)
                {
                    continue;
                }

                for (int characterIndex = 0;
                    characterIndex < characterColliders.Length;
                    characterIndex++)
                {
                    Collider characterCollider = characterColliders[characterIndex];
                    if (characterCollider)
                    {
                        Physics.IgnoreCollision(weaponCollider, characterCollider, true);
                    }
                }
            }
        }
    }

    private static bool IsWeaponModelRenderer(Renderer renderer)
    {
        if (!renderer || !renderer.gameObject.activeInHierarchy || !renderer.enabled)
        {
            return false;
        }

        return renderer is MeshRenderer || renderer is SkinnedMeshRenderer;
    }

    public static async Task<DroppedWeaponEntity> CreateFromResourceAsync(
        string weaponResourceId,
        int magazineAmmo,
        int reserveAmmo,
        Vector3 position,
        Quaternion rotation,
        Vector3 impulse)
    {
        if (string.IsNullOrWhiteSpace(weaponResourceId))
        {
            return null;
        }

        ResourceMgr resources = ResourceMgr.instance;
        WeaponDefinitionSO weaponData = null;
        GameObject modelPrefab = null;
        string modelResourceId = string.Empty;
        try
        {
            weaponData = await resources.LoadAsync<WeaponDefinitionSO>(weaponResourceId);
            if (!weaponData)
            {
                return null;
            }

            modelResourceId = weaponData.Resources.WorldModelPrefabResourceId;
            modelPrefab = await resources.LoadAsync<GameObject>(modelResourceId);
            if (!modelPrefab)
            {
                return null;
            }

            return CreateFromModel(
                modelPrefab,
                weaponResourceId,
                magazineAmmo,
                reserveAmmo,
                position,
                rotation,
                impulse);
        }
        finally
        {
            if (modelPrefab && !string.IsNullOrWhiteSpace(modelResourceId))
            {
                resources.Release<GameObject>(modelResourceId);
            }
            if (weaponData)
            {
                resources.Release<WeaponDefinitionSO>(weaponResourceId);
            }
        }
    }
}
