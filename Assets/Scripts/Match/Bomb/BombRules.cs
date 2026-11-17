using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BombRules : MonoBehaviour
{
    public const string DefaultBombResourceId = "prop.bomb.prefab";
    public const int DefaultAttackerTeam = 0;
    public const int DefaultDefenderTeam = 1;

    private const string ExplosionSourceId = "objective.c4.explosion";
    private const string ExplosionSourceName = "C4 炸弹";
    private const float GroundProbeHeight = 2f;
    private const float GroundProbeDistance = 6f;
    private const float GroundContactOffset = 0.002f;
    private const float BombPlayerClearance = 1.4f;
    private const float SpawnSearchStep = 0.8f;
    private const int SpawnSearchRings = 4;

    [Header("炸弹生成")]
    [SerializeField, ResourceId(typeof(GameObject)), InspectorName("炸弹资源 ID")]
    private string _bombResourceId = DefaultBombResourceId;
    [SerializeField, InspectorName("匪方出生点")] private Transform _attackerSpawn;
    [SerializeField, Min(0.1f), InspectorName("自动拾取半径")] private float _pickupRadius = 0.9f;
    [SerializeField, Min(0.5f), InspectorName("拆除判定半径")] private float _siteRadius = 3f;
    [SerializeField, InspectorName("本回合目标包点 ID")] private string _targetSiteId = string.Empty;

    [Header("队伍")]
    [SerializeField, InspectorName("匪方队伍编号")] private int _attackerTeam = DefaultAttackerTeam;
    [SerializeField, InspectorName("警方队伍编号")] private int _defenderTeam = DefaultDefenderTeam;

    [Header("时长")]
    [SerializeField, Min(0.1f), InspectorName("安放时长")] private float _plantDuration = 3f;
    [SerializeField, Min(0.1f), InspectorName("拆除时长")] private float _defuseDuration = 5f;
    [SerializeField, Min(1f), InspectorName("爆炸倒计时")] private float _fuseDuration = 40f;

    [Header("爆炸")]
    [SerializeField, Min(1f), InspectorName("致死半径")] private float _explosionKillRadius = 18f;
    [SerializeField, Min(1f), InspectorName("特效尺寸")] private float _explosionVisualScale = 1f;

    private readonly List<BombInteractCtrl> _interactionCtrls = new List<BombInteractCtrl>();
    private readonly RaycastHit[] _groundHits = new RaycastHit[24];
    private Func<PlayerInfo, int> _teamResolver;
    private BombEntity _bomb;
    private PlayerInfo _bombPlanter;
    private PlayerMoveCtrl _slowedCarrierMoveCtrl;
    private float _carrierSpeedScale = 0.92f;
    private bool _roundActive;
    private bool _roundPrepared;
    private bool _bombSpawnPending;
    private int _roundGeneration;
    private float _nextBindingRefreshTime;

    public BombEntity Bomb { get { return _bomb; } }
    public PlayerInfo BombCarrier { get { return _bomb ? _bomb.Carrier : null; } }
    public bool IsRoundActive { get { return _roundActive; } }
    public float PlantDuration { get { return _plantDuration; } }
    public float DefuseDuration { get { return _defuseDuration; } }
    public float FuseDuration { get { return _fuseDuration; } }
    public int AttackerTeam { get { return _attackerTeam; } }
    public int DefenderTeam { get { return _defenderTeam; } }

    public event Action<BombEntity> BombSpawned;
    public event Action<PlayerInfo> BombCarrierChanged;
    public event Action<PlayerInfo, BombSite> BombPlanted;
    public event Action<PlayerInfo> BombDefused;
    public event Action BombExploded;
    public event Action<int> RoundWon;

    private void Update()
    {
        if (!_roundPrepared)
        {
            return;
        }

        if (Time.unscaledTime >= _nextBindingRefreshTime)
        {
            _nextBindingRefreshTime = Time.unscaledTime + 0.5f;
            RefreshInteractionBindings();
        }

        if (_bomb && _bomb.State == BombEntityState.OnGround)
        {
            TryAutoPickup();
        }
    }

    private void OnDisable()
    {
        _roundGeneration++;
        _roundActive = false;
        _roundPrepared = false;
        SetInteractionRules(null);
        ReleaseBomb();
    }

    public void Configure(
        ModeConfigRegistry registry,
        Func<PlayerInfo, int> teamResolver,
        Transform attackerSpawn = null)
    {
        _teamResolver = teamResolver;
        if (attackerSpawn)
        {
            _attackerSpawn = attackerSpawn;
        }
        if (registry == null)
        {
            return;
        }

        _pickupRadius = Mathf.Max(0.1f, registry.GetFloat("bomb.pickupRadius", _pickupRadius));
        _siteRadius = Mathf.Max(0.5f, registry.GetFloat("bomb.siteRadius", _siteRadius));
        _plantDuration = Mathf.Max(0.1f, registry.GetFloat("bomb.plantDuration", _plantDuration));
        _defuseDuration = Mathf.Max(0.1f, registry.GetFloat("bomb.defuseDuration", _defuseDuration));
        _fuseDuration = Mathf.Max(1f, registry.GetFloat("bomb.fuseDuration", _fuseDuration));
        _explosionKillRadius = Mathf.Max(
            1f,
            registry.GetFloat("bomb.explosionKillRadius", _explosionKillRadius));
        _explosionVisualScale = Mathf.Max(
            1f,
            registry.GetFloat("bomb.explosionVisualScale", _explosionVisualScale));
        _carrierSpeedScale = Mathf.Clamp(
            registry.GetFloat("bomb.carrierSpeedScale", _carrierSpeedScale),
            0.1f,
            1f);
        _bombResourceId = registry.GetString("bomb.resourceId", _bombResourceId);
        _attackerTeam = registry.GetInt("bomb.attackerTeam", _attackerTeam);
        _defenderTeam = registry.GetInt("bomb.defenderTeam", _defenderTeam);
        _targetSiteId = registry.GetString("bomb.targetSiteId", _targetSiteId);
    }

    public async void OnRoundCountdownStarted()
    {
        int generation = ++_roundGeneration;
        _roundActive = false;
        _roundPrepared = true;
        _bombSpawnPending = true;
        ReleaseBomb();
        RefreshInteractionBindings();
        SetInteractionRules(null);

        Vector3 spawnOrigin = _attackerSpawn ? _attackerSpawn.position : transform.position;
        Vector3 spawnPosition = FindAvailableBombSpawn(spawnOrigin);
        BombEntity spawned = await SpawnBomb(spawnPosition);
        if (generation == _roundGeneration)
        {
            _bombSpawnPending = false;
        }
        if (generation != _roundGeneration || !this || !_roundPrepared)
        {
            ReleaseSpawned(spawned);
            return;
        }

        SetBomb(spawned);
    }

    public void OnRoundStart()
    {
        _roundPrepared = true;
        _roundActive = true;
        RefreshInteractionBindings();
        SetInteractionRules(this);
        if (!_bomb && !_bombSpawnPending)
        {
            SpawnMissingBomb();
        }
    }

    public void OnRoundEnd()
    {
        _roundActive = false;
        _roundPrepared = false;
        _roundGeneration++;
        if (_bomb && _bomb.State == BombEntityState.Carried)
        {
            _bomb.TryDrop(_bomb.Carrier ? _bomb.Carrier.transform.position : transform.position);
        }
        if (_bomb)
        {
            _bomb.PauseFuse(true);
        }
        SetInteractionRules(null);
    }

    public bool CanPlant(PlayerInfo player, out BombSite site)
    {
        site = null;
        if (!_roundActive || !_bomb || _bomb.State != BombEntityState.Carried ||
            _bomb.Carrier != player || !IsAlive(player) || GetTeam(player) != _attackerTeam)
        {
            return false;
        }

        IReadOnlyList<BombSite> sites = BombSite.Sites;
        for (int i = 0; i < sites.Count; i++)
        {
            BombSite candidate = sites[i];
            if (candidate && candidate.isActiveAndEnabled && IsTargetSite(candidate) &&
                candidate.Contains(player.transform.position))
            {
                site = candidate;
                return true;
            }
        }

        return false;
    }

    public bool CanDefuse(PlayerInfo player)
    {
        if (!_roundActive || !_bomb || _bomb.State != BombEntityState.Planted ||
            !IsAlive(player) || GetTeam(player) != _defenderTeam)
        {
            return false;
        }

        Vector3 delta = player.transform.position - _bomb.transform.position;
        delta.y = 0f;
        return delta.sqrMagnitude <= _siteRadius * _siteRadius;
    }

    public bool TryPlant(PlayerInfo planter, BombSite site)
    {
        if (!CanPlant(planter, out BombSite currentSite) || currentSite != site)
        {
            return false;
        }

        ResolveGroundPose(planter, out Vector3 position, out Quaternion rotation);
        if (!_bomb.TryPlant(planter, site, position, rotation, _fuseDuration))
        {
            return false;
        }

        _bombPlanter = planter;
        RestoreCarrierSpeed();
        BombCarrierChanged?.Invoke(null);
        BombPlanted?.Invoke(planter, site);
        return true;
    }

    public bool TryDefuse(PlayerInfo defuser)
    {
        return CanDefuse(defuser) && _bomb.TryDefuse(defuser);
    }

    public bool TryDrop(PlayerInfo carrier)
    {
        if (!_roundActive || !_bomb || _bomb.State != BombEntityState.Carried ||
            _bomb.Carrier != carrier || !IsAlive(carrier))
        {
            return false;
        }

        Vector3 forward = carrier.transform.forward;
        Vector3 position = carrier.transform.position + Vector3.up * 1.05f +
            forward * 0.7f;
        Vector3 impulse = forward * 2.25f + Vector3.up * 0.7f;
        return _bomb.TryDrop(position, impulse);
    }

    private async Task<BombEntity> SpawnBomb(Vector3 position)
    {
        GameObject instance = null;
        try
        {
            instance = await ResourceMgr.instance.InstantiateAsync(
                _bombResourceId,
                position,
                Quaternion.identity);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"炸弹资源 {_bombResourceId} 加载失败，将使用运行时占位物：{exception.Message}", this);
        }

        if (instance && instance.TryGetComponent(out BombEntity entity))
        {
            return entity;
        }

        if (instance)
        {
            ResourceMgr.instance.ReleaseInstance(instance);
        }

        return CreateFallbackBomb(position);
    }

    private async void SpawnMissingBomb()
    {
        int generation = _roundGeneration;
        _bombSpawnPending = true;
        Vector3 spawnOrigin = _attackerSpawn ? _attackerSpawn.position : transform.position;
        BombEntity spawned = await SpawnBomb(FindAvailableBombSpawn(spawnOrigin));
        if (generation == _roundGeneration)
        {
            _bombSpawnPending = false;
        }
        if (generation != _roundGeneration || !this || !_roundPrepared || _bomb)
        {
            ReleaseSpawned(spawned);
            return;
        }

        SetBomb(spawned);
    }

    private BombEntity CreateFallbackBomb(Vector3 position)
    {
        GameObject placeholder = GameObject.CreatePrimitive(PrimitiveType.Cube);
        placeholder.name = "Bomb_RuntimePlaceholder";
        placeholder.transform.position = position;
        placeholder.transform.localScale = new Vector3(0.35f, 0.2f, 0.5f);
        return placeholder.AddComponent<BombEntity>();
    }

    private Vector3 FindAvailableBombSpawn(Vector3 origin)
    {
        if (IsBombSpawnClear(origin))
        {
            return origin;
        }

        for (int ring = 1; ring <= SpawnSearchRings; ring++)
        {
            float distance = ring * SpawnSearchStep;
            int samples = ring * 8;
            for (int sample = 0; sample < samples; sample++)
            {
                float angle = sample * Mathf.PI * 2f / samples;
                Vector3 candidate = origin + new Vector3(
                    Mathf.Cos(angle) * distance,
                    0f,
                    Mathf.Sin(angle) * distance);
                if (IsBombSpawnClear(candidate))
                {
                    return candidate;
                }
            }
        }

        return origin + Vector3.forward * BombPlayerClearance;
    }

    private static bool IsBombSpawnClear(Vector3 position)
    {
        float clearanceSquared = BombPlayerClearance * BombPlayerClearance;
        IReadOnlyList<CharacterHealth> actors = CharacterHealth.Actors;
        for (int i = 0; i < actors.Count; i++)
        {
            CharacterHealth health = actors[i];
            if (!health || health.IsDead)
            {
                continue;
            }

            Vector3 delta = health.transform.position - position;
            delta.y = 0f;
            if (delta.sqrMagnitude < clearanceSquared)
            {
                return false;
            }
        }

        return true;
    }

    private void SetBomb(BombEntity bomb)
    {
        _bomb = bomb;
        if (!_bomb)
        {
            Debug.LogError("爆破回合未能创建炸弹。", this);
            return;
        }

        _bomb.PickedUp += HandlePickedUp;
        _bomb.Dropped += HandleDropped;
        _bomb.Defused += HandleDefused;
        _bomb.Exploded += HandleExploded;
        _bomb.ConfigurePickupRadius(_pickupRadius);
        BombSpawned?.Invoke(_bomb);
    }

    private void UnsubscribeBomb()
    {
        if (!_bomb)
        {
            return;
        }

        _bomb.PickedUp -= HandlePickedUp;
        _bomb.Dropped -= HandleDropped;
        _bomb.Defused -= HandleDefused;
        _bomb.Exploded -= HandleExploded;
    }

    private void TryAutoPickup()
    {
        IReadOnlyList<CharacterHealth> actors = CharacterHealth.Actors;
        float radius = Mathf.Max(_pickupRadius, _bomb.PickupRadius);
        float radiusSquared = radius * radius;
        for (int i = 0; i < actors.Count; i++)
        {
            CharacterHealth health = actors[i];
            PlayerInfo player = health ? health.PlayerInfo : null;
            if (!player || health.IsDead || GetTeam(player) != _attackerTeam ||
                (player.transform.position - _bomb.transform.position).sqrMagnitude > radiusSquared)
            {
                continue;
            }

            if (_bomb.TryPickUp(player))
            {
                return;
            }
        }
    }

    public void RegisterPlayer(PlayerInfo player)
    {
        if (!player)
        {
            return;
        }

        for (int i = 0; i < _interactionCtrls.Count; i++)
        {
            BombInteractCtrl registered = _interactionCtrls[i];
            if (registered && registered.gameObject == player.gameObject)
            {
                return;
            }
        }

        BombInteractCtrl interactCtrl;
        if (!player.TryGetComponent(out interactCtrl))
        {
            interactCtrl = player.gameObject.AddComponent<BombInteractCtrl>();
        }
        if (!_interactionCtrls.Contains(interactCtrl))
        {
            _interactionCtrls.Add(interactCtrl);
        }
        interactCtrl.Configure(_roundActive ? this : null);
    }

    private void RefreshInteractionBindings()
    {
        for (int i = _interactionCtrls.Count - 1; i >= 0; i--)
        {
            if (!_interactionCtrls[i])
            {
                _interactionCtrls.RemoveAt(i);
            }
        }

        IReadOnlyList<CharacterHealth> actors = CharacterHealth.Actors;
        for (int i = 0; i < actors.Count; i++)
        {
            CharacterHealth health = actors[i];
            if (health && health.PlayerInfo)
            {
                RegisterPlayer(health.PlayerInfo);
            }
        }
    }

    private void SetInteractionRules(BombRules rules)
    {
        for (int i = _interactionCtrls.Count - 1; i >= 0; i--)
        {
            BombInteractCtrl interactCtrl = _interactionCtrls[i];
            if (!interactCtrl)
            {
                _interactionCtrls.RemoveAt(i);
                continue;
            }

            interactCtrl.Configure(rules);
        }
    }

    private bool IsTargetSite(BombSite site)
    {
        return string.IsNullOrWhiteSpace(_targetSiteId) ||
            string.Equals(site.SiteId, _targetSiteId, StringComparison.Ordinal);
    }

    private int GetTeam(PlayerInfo player)
    {
        return _teamResolver != null && player ? _teamResolver(player) : int.MinValue;
    }

    private bool IsAlive(PlayerInfo player)
    {
        return player && player.TryGetComponent(out CharacterHealth health) && !health.IsDead;
    }

    private void ResolveGroundPose(
        PlayerInfo planter,
        out Vector3 position,
        out Quaternion rotation)
    {
        Vector3 origin = planter ? planter.transform.position : transform.position;
        float yaw = planter ? planter.transform.eulerAngles.y : 0f;
        position = origin;
        rotation = Quaternion.Euler(0f, yaw, 0f);
        Vector3 rayOrigin = origin + Vector3.up * GroundProbeHeight;
        int hitCount = Physics.RaycastNonAlloc(
            rayOrigin,
            Vector3.down,
            _groundHits,
            GroundProbeDistance,
            ~0,
            QueryTriggerInteraction.Ignore);
        float nearestDistance = float.PositiveInfinity;
        RaycastHit groundHit = default;
        bool hasGround = false;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit candidate = _groundHits[i];
            if (!candidate.collider || candidate.distance >= nearestDistance ||
                candidate.normal.y <= 0.05f)
            {
                continue;
            }

            Transform hitTransform = candidate.collider.transform;
            if ((planter && hitTransform.IsChildOf(planter.transform)) ||
                (_bomb && hitTransform.IsChildOf(_bomb.transform)))
            {
                continue;
            }

            nearestDistance = candidate.distance;
            groundHit = candidate;
            hasGround = true;
        }

        if (!hasGround)
        {
            return;
        }

        position = groundHit.point + groundHit.normal * GroundContactOffset;
        Quaternion surfaceAlignment = Quaternion.FromToRotation(Vector3.up, groundHit.normal);
        rotation = surfaceAlignment * Quaternion.Euler(0f, yaw, 0f);
    }

    private void HandlePickedUp(BombEntity bomb, PlayerInfo carrier)
    {
        RestoreCarrierSpeed();
        if (carrier && carrier.TryGetComponent(out _slowedCarrierMoveCtrl))
        {
            _slowedCarrierMoveCtrl.SetGameplaySpeedMultiplier(_carrierSpeedScale);
        }
        BombCarrierChanged?.Invoke(carrier);
    }

    private void HandleDropped(BombEntity bomb, PlayerInfo previousCarrier)
    {
        RestoreCarrierSpeed();
        BombCarrierChanged?.Invoke(null);
    }

    private void HandleDefused(BombEntity bomb, PlayerInfo defuser)
    {
        BombDefused?.Invoke(defuser);
        CompleteRound(_defenderTeam);
    }

    private void HandleExploded(BombEntity bomb)
    {
        Vector3 explosionPosition = bomb ? bomb.transform.position : transform.position;
        BombExplosionEffect.Spawn(explosionPosition, _explosionVisualScale);
        KillActorsInExplosion(explosionPosition);
        BombExploded?.Invoke();
        CompleteRound(_attackerTeam);
    }

    private void KillActorsInExplosion(Vector3 explosionPosition)
    {
        IReadOnlyList<CharacterHealth> actors = CharacterHealth.Actors;
        float radiusSquared = _explosionKillRadius * _explosionKillRadius;
        for (int i = actors.Count - 1; i >= 0; i--)
        {
            CharacterHealth health = actors[i];
            if (!health || health.IsDead)
            {
                continue;
            }

            Vector3 delta = health.transform.position - explosionPosition;
            if (delta.sqrMagnitude > radiusSquared)
            {
                continue;
            }

            PlayerInfo victim = health.PlayerInfo;
            DamageRequest request = new DamageRequest(health, health.CurrentHealth);
            request.Instigator = ResolveExplosionInstigator(victim);
            request.SourceId = ExplosionSourceId;
            request.SourceName = ExplosionSourceName;
            request.HitDistance = delta.magnitude;
            request.HitPoint = explosionPosition;
            request.HitDirection = delta.sqrMagnitude > 0.0001f
                ? delta.normalized
                : Vector3.up;
            request.BypassArmor = true;
            CombatDamage.Kill(request);
        }
    }

    private PlayerInfo ResolveExplosionInstigator(PlayerInfo victim)
    {
        if (!_bombPlanter || !victim || victim == _bombPlanter)
        {
            return null;
        }

        return GetTeam(victim) == GetTeam(_bombPlanter) ? null : _bombPlanter;
    }

    private void CompleteRound(int winningTeam)
    {
        if (!_roundActive)
        {
            return;
        }

        _roundActive = false;
        RoundWon?.Invoke(winningTeam);
    }

    private void ReleaseBomb()
    {
        RestoreCarrierSpeed();
        _bombPlanter = null;
        if (!_bomb)
        {
            return;
        }

        BombEntity oldBomb = _bomb;
        UnsubscribeBomb();
        _bomb = null;
        ReleaseSpawned(oldBomb);
    }

    private void RestoreCarrierSpeed()
    {
        if (_slowedCarrierMoveCtrl)
        {
            _slowedCarrierMoveCtrl.SetGameplaySpeedMultiplier(1f);
        }

        _slowedCarrierMoveCtrl = null;
    }

    private void ReleaseSpawned(BombEntity entity)
    {
        if (!entity)
        {
            return;
        }

        if (!ResourceMgr.instance.ReleaseInstance(entity.gameObject))
        {
            Destroy(entity.gameObject);
        }
    }

    private void OnValidate()
    {
        _bombResourceId = string.IsNullOrWhiteSpace(_bombResourceId)
            ? DefaultBombResourceId
            : _bombResourceId.Trim();
        _pickupRadius = Mathf.Max(0.1f, _pickupRadius);
        _siteRadius = Mathf.Max(0.5f, _siteRadius);
        _plantDuration = Mathf.Max(0.1f, _plantDuration);
        _defuseDuration = Mathf.Max(0.1f, _defuseDuration);
        _fuseDuration = Mathf.Max(1f, _fuseDuration);
        _explosionKillRadius = Mathf.Max(1f, _explosionKillRadius);
        _explosionVisualScale = Mathf.Max(1f, _explosionVisualScale);
        _carrierSpeedScale = Mathf.Clamp(_carrierSpeedScale, 0.1f, 1f);
        _targetSiteId = _targetSiteId == null ? string.Empty : _targetSiteId.Trim();
    }
}
