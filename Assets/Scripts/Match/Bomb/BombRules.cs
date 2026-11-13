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

    [Header("炸弹生成")]
    [SerializeField, ResourceId(typeof(GameObject)), InspectorName("炸弹资源 ID")]
    private string _bombResourceId = DefaultBombResourceId;
    [SerializeField, InspectorName("匪方出生点")] private Transform _attackerSpawn;
    [SerializeField, Min(0.1f), InspectorName("自动拾取半径")] private float _pickupRadius = 1.8f;
    [SerializeField, Min(0.5f), InspectorName("拆除判定半径")] private float _siteRadius = 3f;
    [SerializeField, InspectorName("本回合目标包点 ID")] private string _targetSiteId = string.Empty;

    [Header("队伍")]
    [SerializeField, InspectorName("匪方队伍编号")] private int _attackerTeam = DefaultAttackerTeam;
    [SerializeField, InspectorName("警方队伍编号")] private int _defenderTeam = DefaultDefenderTeam;

    [Header("时长")]
    [SerializeField, Min(0.1f), InspectorName("安放时长")] private float _plantDuration = 3f;
    [SerializeField, Min(0.1f), InspectorName("拆除时长")] private float _defuseDuration = 5f;
    [SerializeField, Min(1f), InspectorName("爆炸倒计时")] private float _fuseDuration = 40f;

    private readonly List<BombInteractCtrl> _interactionCtrls = new List<BombInteractCtrl>();
    private Func<PlayerInfo, int> _teamResolver;
    private BombEntity _bomb;
    private PlayerMoveCtrl _slowedCarrierMoveCtrl;
    private float _carrierSpeedScale = 0.92f;
    private bool _roundActive;
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
        if (!_roundActive)
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
        _carrierSpeedScale = Mathf.Clamp(
            registry.GetFloat("bomb.carrierSpeedScale", _carrierSpeedScale),
            0.1f,
            1f);
        _bombResourceId = registry.GetString("bomb.resourceId", _bombResourceId);
        _attackerTeam = registry.GetInt("bomb.attackerTeam", _attackerTeam);
        _defenderTeam = registry.GetInt("bomb.defenderTeam", _defenderTeam);
        _targetSiteId = registry.GetString("bomb.targetSiteId", _targetSiteId);
    }

    public async void OnRoundStart()
    {
        int generation = ++_roundGeneration;
        _roundActive = true;
        ReleaseBomb();
        RefreshInteractionBindings();
        SetInteractionRules(this);

        Vector3 spawnPosition = _attackerSpawn ? _attackerSpawn.position : transform.position;
        BombEntity spawned = await SpawnBomb(spawnPosition);
        if (generation != _roundGeneration || !this || !_roundActive)
        {
            ReleaseSpawned(spawned);
            return;
        }

        SetBomb(spawned);
    }

    public void OnRoundEnd()
    {
        _roundActive = false;
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

        Vector3 position = ResolveGroundPosition(planter.transform.position);
        if (!_bomb.TryPlant(planter, site, position, _fuseDuration))
        {
            return false;
        }

        RestoreCarrierSpeed();
        BombCarrierChanged?.Invoke(null);
        BombPlanted?.Invoke(planter, site);
        return true;
    }

    public bool TryDefuse(PlayerInfo defuser)
    {
        return CanDefuse(defuser) && _bomb.TryDefuse(defuser);
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

    private BombEntity CreateFallbackBomb(Vector3 position)
    {
        GameObject placeholder = GameObject.CreatePrimitive(PrimitiveType.Cube);
        placeholder.name = "Bomb_RuntimePlaceholder";
        placeholder.transform.position = position;
        placeholder.transform.localScale = new Vector3(0.35f, 0.2f, 0.5f);
        return placeholder.AddComponent<BombEntity>();
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

    private Vector3 ResolveGroundPosition(Vector3 origin)
    {
        Vector3 rayOrigin = origin + Vector3.up * 1.5f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 4f, ~0,
            QueryTriggerInteraction.Ignore))
        {
            return hit.point + Vector3.up * 0.05f;
        }
        return origin;
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
        BombExploded?.Invoke();
        CompleteRound(_attackerTeam);
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
        _carrierSpeedScale = Mathf.Clamp(_carrierSpeedScale, 0.1f, 1f);
        _targetSiteId = _targetSiteId == null ? string.Empty : _targetSiteId.Trim();
    }
}
