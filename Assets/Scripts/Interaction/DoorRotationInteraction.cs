using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DoorRotationInteraction : MonoBehaviour, IWorldInteraction
{
    private sealed class IgnoredPlayer
    {
        public CharacterController Controller;
        public Collider[] Colliders;
        public float PushedDistance;
    }

    private const float MaximumPlayerPushDistance = 0.25f;
    private const float PlayerPushPerFrame = 0.025f;

    [Header("门体旋转")]
    [SerializeField, InspectorName("旋转门体（留空为自身）")] private Transform _doorBody;
    [SerializeField, InspectorName("打开角度")] private float _openAngle = 95f;
    [SerializeField, Min(0.01f), InspectorName("旋转耗时")] private float _rotationDuration = 0.32f;

    private Quaternion _closedRotation;
    private Quaternion _openedRotation;
    private Quaternion _startRotation;
    private float _rotationProgress = 1f;
    private bool _isOpen;
    private Collider[] _doorColliders;
    private readonly List<IgnoredPlayer> _ignoredPlayers = new List<IgnoredPlayer>();

    private void Awake()
    {
        if (!_doorBody)
        {
            _doorBody = transform;
        }

        _closedRotation = _doorBody.localRotation;
        _openedRotation = _closedRotation * Quaternion.Euler(0f, _openAngle, 0f);
        _doorColliders = _doorBody.GetComponentsInChildren<Collider>(true);
    }

    private void Update()
    {
        if (_rotationProgress >= 1f)
        {
            RestoreClearedPlayerCollisions();
            return;
        }

        _rotationProgress = Mathf.Min(
            1f,
            _rotationProgress + Time.deltaTime / Mathf.Max(0.01f, _rotationDuration));
        float easedProgress = Mathf.SmoothStep(0f, 1f, _rotationProgress);
        Quaternion targetRotation = _isOpen ? _openedRotation : _closedRotation;
        _doorBody.localRotation = Quaternion.Slerp(
            _startRotation,
            targetRotation,
            easedProgress);
        PushOverlappingPlayersSlightly();
    }

    public bool CanInteract(GameObject interactor)
    {
        return enabled && _doorBody;
    }

    public void Interact(GameObject interactor)
    {
        IgnorePlayerCollisionsDuringRotation();
        _isOpen = !_isOpen;
        _startRotation = _doorBody.localRotation;
        _rotationProgress = 0f;
    }

    private void OnDisable()
    {
        RestoreAllPlayerCollisions();
    }

    private void IgnorePlayerCollisionsDuringRotation()
    {
        RestoreAllPlayerCollisions();
        IReadOnlyList<CharacterHealth> actors = CharacterHealth.Actors;
        for (int i = 0; i < actors.Count; i++)
        {
            CharacterHealth health = actors[i];
            CharacterController controller = health
                ? health.GetComponent<CharacterController>()
                : null;
            if (!controller)
            {
                continue;
            }

            Collider[] playerColliders = health.GetComponentsInChildren<Collider>(true);
            SetCollisionIgnored(playerColliders, true);
            _ignoredPlayers.Add(new IgnoredPlayer
            {
                Controller = controller,
                Colliders = playerColliders,
                PushedDistance = 0f
            });
        }
    }

    private void PushOverlappingPlayersSlightly()
    {
        for (int i = 0; i < _ignoredPlayers.Count; i++)
        {
            IgnoredPlayer player = _ignoredPlayers[i];
            if (!player.Controller ||
                player.PushedDistance >= MaximumPlayerPushDistance ||
                !IntersectsDoor(player.Controller.bounds))
            {
                continue;
            }

            Vector3 direction = player.Controller.bounds.center - _doorBody.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = _doorBody.right;
            }

            float distance = Mathf.Min(
                PlayerPushPerFrame,
                MaximumPlayerPushDistance - player.PushedDistance);
            player.Controller.Move(direction.normalized * distance);
            player.PushedDistance += distance;
        }
    }

    private void RestoreClearedPlayerCollisions()
    {
        for (int i = _ignoredPlayers.Count - 1; i >= 0; i--)
        {
            IgnoredPlayer player = _ignoredPlayers[i];
            if (player.Controller && IntersectsDoor(player.Controller.bounds))
            {
                continue;
            }

            SetCollisionIgnored(player.Colliders, false);
            _ignoredPlayers.RemoveAt(i);
        }
    }

    private void RestoreAllPlayerCollisions()
    {
        for (int i = 0; i < _ignoredPlayers.Count; i++)
        {
            SetCollisionIgnored(_ignoredPlayers[i].Colliders, false);
        }
        _ignoredPlayers.Clear();
    }

    private bool IntersectsDoor(Bounds bounds)
    {
        for (int i = 0; i < _doorColliders.Length; i++)
        {
            Collider doorCollider = _doorColliders[i];
            if (doorCollider && doorCollider.enabled && doorCollider.bounds.Intersects(bounds))
            {
                return true;
            }
        }

        return false;
    }

    private void SetCollisionIgnored(Collider[] playerColliders, bool ignored)
    {
        if (playerColliders == null)
        {
            return;
        }

        for (int doorIndex = 0; doorIndex < _doorColliders.Length; doorIndex++)
        {
            Collider doorCollider = _doorColliders[doorIndex];
            if (!doorCollider)
            {
                continue;
            }

            for (int playerIndex = 0; playerIndex < playerColliders.Length; playerIndex++)
            {
                Collider playerCollider = playerColliders[playerIndex];
                if (playerCollider)
                {
                    Physics.IgnoreCollision(doorCollider, playerCollider, ignored);
                }
            }
        }
    }
}
