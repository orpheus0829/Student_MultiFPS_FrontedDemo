using System;
using System.Collections.Generic;
using UnityEngine;

public enum MatchState
{
    Idle = 0,
    Preparing = 1,
    InRound = 2,
    RoundOver = 3,
    MatchOver = 4
}

public sealed class MatchManager : BaseMgr<MatchManager>
{
    private static readonly IReadOnlyList<MatchParticipant> EmptyParticipants =
        new List<MatchParticipant>();

    private readonly List<MatchParticipant> _roster = new List<MatchParticipant>();
    private readonly Dictionary<int, MatchParticipant> _participantsBySlot =
        new Dictionary<int, MatchParticipant>();
    private readonly Dictionary<PlayerInfo, MatchParticipant> _participantsByInfo =
        new Dictionary<PlayerInfo, MatchParticipant>();
    private readonly Dictionary<int, List<MatchParticipant>> _participantsByTeam =
        new Dictionary<int, List<MatchParticipant>>();
    private readonly Dictionary<MatchParticipant, PlayerLifeCtrl> _boundLifeCtrls =
        new Dictionary<MatchParticipant, PlayerLifeCtrl>();
    private readonly Dictionary<int, int> _teamScores = new Dictionary<int, int>();
    private readonly Dictionary<int, int> _teamRoundWins = new Dictionary<int, int>();
    private readonly AssistTracker _assistTracker = new AssistTracker();
    private readonly List<PlayerSpawnZone> _spawnZones = new List<PlayerSpawnZone>();

    private MatchConfig _config;
    private MatchState _state = MatchState.Idle;
    private IModeRules _rules;
    private bool _isEndingRound;
    private BombRules _bombRuntime;
    private BombEconomyService _bombEconomy;
    private float _nextRoundStartTime = -1f;

    public static MatchManager Instance
    {
        get { return instance; }
    }

    public IReadOnlyList<MatchParticipant> Roster { get { return _roster; } }
    public MatchState State { get { return _state; } }
    public MatchConfig Config { get { return _config; } }
    public IModeRules Rules { get { return _rules; } }
    public PlayerInfo BombCarrier
    {
        get { return _bombRuntime ? _bombRuntime.BombCarrier : null; }
    }
    public bool IsBombPlanted
    {
        get
        {
            return _bombRuntime && _bombRuntime.Bomb &&
                _bombRuntime.Bomb.State == BombEntityState.Planted;
        }
    }

    public event Action RosterChanged;
    public event Action<MatchParticipant> ParticipantStatChanged;
    public event Action<AssistAwardedEvent> AssistAwarded;
    public event Action<KillAwardedEvent> KillAwarded;
    public event Action<MatchState> RoundStateChanged;
    public event Action<int> MatchFinished;
    public event Action<PlayerInfo> BombCarrierChanged;

    protected override bool Ddol
    {
        get { return true; }
    }

    protected override void Awake()
    {
        base.Awake();
        _state = MatchState.Idle;
    }

    private void OnEnable()
    {
        CombatDamage.DamageResolved -= HandleDamageResolved;
        CombatDamage.DamageResolved += HandleDamageResolved;
        SubscribeBoundRespawns();
    }

    private void Update()
    {
        if (_state == MatchState.RoundOver && _nextRoundStartTime >= 0f)
        {
            if (Time.unscaledTime >= _nextRoundStartTime)
            {
                _nextRoundStartTime = -1f;
                BeginRound();
            }
            return;
        }

        if (_state != MatchState.InRound || _rules == null)
        {
            return;
        }

        _rules.Tick(Time.deltaTime);
        TryFinishRoundFromRules();
    }

    private void OnDisable()
    {
        CombatDamage.DamageResolved -= HandleDamageResolved;
        DetachRespawnSubscriptions();
    }

    public bool StartMatch(MatchConfig config)
    {
        if (config == null || !config.Mode)
        {
            Debug.LogError("无法开始对局：缺少有效的模式配置。", this);
            return false;
        }

        ResetRuntimeData();
        _config = config;
        SetState(MatchState.Preparing);
        BuildRoster(config.Slots);
        BindSpawnZones();
        _rules = CreateRules(config.Mode.Kind, config);
        _rules.OnMatchStart();
        SetupModeRuntime();
        RosterChanged?.Invoke();
        BeginRound();
        return true;
    }

    public void StopMatch()
    {
        ResetRuntimeData();
        _config = null;
        _rules = null;
        SetState(MatchState.Idle);
        RosterChanged?.Invoke();
    }

    public bool BeginRound()
    {
        if (_config == null || _rules == null ||
            (_state != MatchState.Preparing && _state != MatchState.RoundOver))
        {
            return false;
        }

        _assistTracker.Clear();
        _nextRoundStartTime = -1f;
        ResetParticipantsForRound();
        SetRoundRespawnPolicy(false);
        RefreshAliveStates();
        _rules.OnRoundStart();
        if (_bombRuntime)
        {
            _bombRuntime.OnRoundStart();
        }
        NotifyAiRoundStart();
        SetState(MatchState.InRound);
        return true;
    }

    public bool EndRound(int winningTeam)
    {
        if (_state != MatchState.InRound || _isEndingRound)
        {
            return false;
        }

        _isEndingRound = true;
        _assistTracker.Clear();
        AddRoundWin(winningTeam);
        int attackerTeam = _config.Registry.GetInt("bomb.attackerTeam", 0);
        _rules?.OnRoundEnd(winningTeam == attackerTeam);
        if (_bombRuntime)
        {
            _bombRuntime.OnRoundEnd();
        }
        SetRoundRespawnPolicy(false);
        NotifyAiRoundEnd(winningTeam);
        _bombEconomy?.AwardRound(winningTeam);
        SetState(MatchState.RoundOver);

        int roundWinScore = _config.Registry.GetInt("score.roundWin", 0);
        if (roundWinScore > 0)
        {
            AwardTeamScoreToParticipants(winningTeam, roundWinScore);
        }

        bool usesRounds = _config.Registry.GetBool(
            "match.usesRounds",
            _config.Mode.WinCondition == WinConditionType.RoundWins);
        int roundsToWin = _config.Registry.GetInt(
            "match.roundsToWin",
            _config.Registry.GetInt("bomb.roundsToWin",
                _config.Mode.ScoreToWin));
        if (!usesRounds || GetTeamRoundWins(winningTeam) >= Mathf.Max(1, roundsToWin))
        {
            FinishMatch(winningTeam);
        }
        else
        {
            float delay = Mathf.Max(0f,
                _config.Registry.GetFloat("match.roundRestartDelay",
                    _config.Registry.GetFloat("bomb.roundRestartDelay", 4f)));
            _nextRoundStartTime = Time.unscaledTime + delay;
        }

        _isEndingRound = false;
        return true;
    }

    public void FinishMatch(int winningTeam)
    {
        if (_state == MatchState.Idle || _state == MatchState.MatchOver)
        {
            return;
        }

        int matchWinScore = _config != null
            ? _config.Registry.GetInt("score.matchWin", 0)
            : 0;
        if (matchWinScore > 0)
        {
            AwardTeamScoreToParticipants(winningTeam, matchWinScore);
        }

        SetState(MatchState.MatchOver);
        _nextRoundStartTime = -1f;
        MatchFinished?.Invoke(winningTeam);
    }

    public bool BindParticipant(int slot, CharacterHealth actor)
    {
        if (!actor || !actor.PlayerInfo)
        {
            return false;
        }

        return BindParticipantInternal(slot, actor.PlayerInfo, actor);
    }

    public bool BindParticipant(int slot, PlayerInfo info)
    {
        if (!info || !info.TryGetComponent(out CharacterHealth health))
        {
            return false;
        }

        return BindParticipantInternal(slot, info, health);
    }

    public bool BindLocalPlayer(PlayerInfo info)
    {
        if (!info)
        {
            return false;
        }

        for (int i = 0; i < _roster.Count; i++)
        {
            MatchParticipant participant = _roster[i];
            if (participant.IsLocalPlayer && !participant.IsBound)
            {
                return BindParticipant(participant.Slot, info);
            }
        }

        for (int i = 0; i < _roster.Count; i++)
        {
            MatchParticipant participant = _roster[i];
            if (!participant.IsAI && !participant.IsBound)
            {
                return BindParticipant(participant.Slot, info);
            }
        }

        return false;
    }

    public MatchParticipant GetParticipant(PlayerInfo info)
    {
        if (!info)
        {
            return null;
        }

        _participantsByInfo.TryGetValue(info, out MatchParticipant participant);
        return participant;
    }

    public MatchParticipant GetParticipantBySlot(int slot)
    {
        _participantsBySlot.TryGetValue(slot, out MatchParticipant participant);
        return participant;
    }

    public int GetTeam(PlayerInfo info)
    {
        MatchParticipant participant = GetParticipant(info);
        return participant != null ? participant.Team : -1;
    }

    public bool IsFriendly(PlayerInfo first, PlayerInfo second)
    {
        MatchParticipant firstParticipant = GetParticipant(first);
        MatchParticipant secondParticipant = GetParticipant(second);
        return firstParticipant != null && secondParticipant != null &&
            firstParticipant.Team == secondParticipant.Team;
    }

    public IReadOnlyList<MatchParticipant> GetParticipantsByTeam(int team)
    {
        if (_participantsByTeam.TryGetValue(team, out List<MatchParticipant> participants))
        {
            return participants;
        }

        return EmptyParticipants;
    }

    public int GetTeamScore(int team)
    {
        _teamScores.TryGetValue(team, out int score);
        return score;
    }

    public int GetTeamRoundWins(int team)
    {
        _teamRoundWins.TryGetValue(team, out int wins);
        return wins;
    }

    public void AddTeamScore(int team, int amount)
    {
        _teamScores.TryGetValue(team, out int score);
        _teamScores[team] = score + amount;
    }

    public bool ReportObjectiveWinner(int team)
    {
        if (_state != MatchState.InRound || !(_rules is BombModeRules bombRules))
        {
            return false;
        }

        bombRules.ReportObjectiveWinner(team);
        TryFinishRoundFromRules();
        return true;
    }

    public bool TryGetLeadingTeamAtScore(int minimumScore, out int team)
    {
        team = -1;
        int bestScore = minimumScore - 1;
        foreach (KeyValuePair<int, int> entry in _teamScores)
        {
            if (entry.Value <= bestScore)
            {
                continue;
            }

            bestScore = entry.Value;
            team = entry.Key;
        }

        return team >= 0;
    }

    public bool TryGetLeadingTeam(out int team)
    {
        team = -1;
        int bestScore = int.MinValue;
        bool hasTie = false;
        foreach (KeyValuePair<int, int> entry in _teamScores)
        {
            if (entry.Value > bestScore)
            {
                bestScore = entry.Value;
                team = entry.Key;
                hasTie = false;
            }
            else if (entry.Value == bestScore)
            {
                hasTie = true;
            }
        }

        return team >= 0 && !hasTie;
    }

    public bool TryGetOnlyLivingTeam(out int team)
    {
        team = -1;
        int boundTeamCount = 0;
        foreach (KeyValuePair<int, List<MatchParticipant>> entry in _participantsByTeam)
        {
            bool hasBoundParticipant = false;
            for (int i = 0; i < entry.Value.Count; i++)
            {
                if (entry.Value[i].IsBound)
                {
                    hasBoundParticipant = true;
                    break;
                }
            }

            if (hasBoundParticipant)
            {
                boundTeamCount++;
            }
        }

        // 异步生成尚未把双方绑定齐时不能把先到场的一方误判成全灭胜利。
        if (boundTeamCount < 2)
        {
            return false;
        }

        for (int i = 0; i < _roster.Count; i++)
        {
            MatchParticipant participant = _roster[i];
            if (!participant.IsAlive)
            {
                continue;
            }

            if (team < 0)
            {
                team = participant.Team;
            }
            else if (team != participant.Team)
            {
                team = -1;
                return false;
            }
        }

        return team >= 0;
    }

    private void HandleDamageResolved(DamageResult result)
    {
        if (_state != MatchState.InRound || result == null || !result.WasApplied ||
            !result.Victim)
        {
            return;
        }

        MatchParticipant victim = GetParticipant(result.Victim);
        if (victim == null)
        {
            return;
        }

        MatchParticipant attacker = GetParticipant(result.Instigator);
        if (attacker != null && attacker != victim)
        {
            _assistTracker.RecordDamage(
                result.Instigator,
                result.Victim,
                result.AppliedDamage,
                result.Target ? result.Target.MaxHealth : 0f);
        }

        if (!result.WasKill)
        {
            return;
        }

        victim.AddDeath();
        ParticipantStatChanged?.Invoke(victim);
        bool validEnemyKill = attacker != null && attacker != victim &&
            !IsFriendly(attacker.Info, victim.Info);
        if (validEnemyKill)
        {
            int killScore = _config.Registry.GetInt("score.kill", 100);
            int headshotScore = result.WasHeadshot
                ? _config.Registry.GetInt("score.headshotBonus", 25)
                : 0;
            attacker.AddKill(killScore + headshotScore);
            AddTeamScore(attacker.Team, 1);
            ParticipantStatChanged?.Invoke(attacker);
        }

        IReadOnlyList<PlayerInfo> assisters = _assistTracker.ResolveAssists(
            result.Instigator,
            result.Victim);
        int assistScore = _config.Registry.GetInt("score.assist", 25);
        for (int i = 0; i < assisters.Count; i++)
        {
            MatchParticipant assister = GetParticipant(assisters[i]);
            if (assister == null || IsFriendly(assister.Info, victim.Info))
            {
                continue;
            }

            assister.AddAssist(assistScore);
            ParticipantStatChanged?.Invoke(assister);
            AssistAwarded?.Invoke(new AssistAwardedEvent(
                assister.Info,
                victim.Info,
                result.SourceName));
        }

        KillAwarded?.Invoke(new KillAwardedEvent(
            validEnemyKill ? attacker : null,
            victim,
            result.SourceId,
            result.SourceName,
            result.WasHeadshot));
        TryFinishRoundFromRules();
    }

    private void HandleParticipantRespawned()
    {
        for (int i = 0; i < _roster.Count; i++)
        {
            MatchParticipant participant = _roster[i];
            if (!participant.Health || participant.Health.IsDead || participant.IsAlive)
            {
                continue;
            }

            participant.MarkAlive();
            _assistTracker.ClearVictim(participant.Info);
            ParticipantStatChanged?.Invoke(participant);
        }
    }

    private bool BindParticipantInternal(int slot, PlayerInfo info, CharacterHealth health)
    {
        if (_state == MatchState.Idle ||
            !_participantsBySlot.TryGetValue(slot, out MatchParticipant participant))
        {
            return false;
        }

        if (_participantsByInfo.TryGetValue(info, out MatchParticipant existing) &&
            existing != participant)
        {
            return false;
        }

        UnbindParticipant(participant);
        participant.Bind(info, health);
        _participantsByInfo[info] = participant;
        if (health.TryGetComponent(out PlayerLifeCtrl life))
        {
            life.Respawned += HandleParticipantRespawned;
            _boundLifeCtrls[participant] = life;
            PlayerSpawnZone spawnZone = FindSpawnZoneForParticipant(participant);
            if (spawnZone)
            {
                life.BindSpawnZone(spawnZone);
            }
        }
        ConfigureParticipantForMode(participant);

        RosterChanged?.Invoke();
        ParticipantStatChanged?.Invoke(participant);
        return true;
    }

    private void BuildRoster(IReadOnlyList<MatchSlotConfig> slots)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            MatchSlotConfig slot = slots[i];
            if (slot == null || _participantsBySlot.ContainsKey(slot.Slot))
            {
                continue;
            }

            MatchParticipant participant = new MatchParticipant(slot);
            _roster.Add(participant);
            _participantsBySlot.Add(participant.Slot, participant);
            if (!_participantsByTeam.TryGetValue(
                participant.Team,
                out List<MatchParticipant> team))
            {
                team = new List<MatchParticipant>();
                _participantsByTeam.Add(participant.Team, team);
                _teamScores[participant.Team] = 0;
                _teamRoundWins[participant.Team] = 0;
            }
            team.Add(participant);
        }
    }

    private IModeRules CreateRules(ModeKind kind, MatchConfig config)
    {
        string fallback = kind == ModeKind.Bomb ? "bomb" :
            kind == ModeKind.TeamDeathmatch ? "tdm" :
            kind == ModeKind.Solo ? "solo" : "custom";
        switch (config.Registry.GetString("rules.type", fallback).ToLowerInvariant())
        {
            case "solo":
                return new SoloRules(this, config);
            case "tdm":
                return new TdmRules(this, config);
            case "bomb":
                return new BombModeRules(this, config);
            default:
                return new CustomRules(this, config);
        }
    }

    private void TryFinishRoundFromRules()
    {
        if (_state == MatchState.InRound && _rules != null &&
            _rules.IsRoundFinished(out int winningTeam))
        {
            EndRound(winningTeam);
        }
    }

    private void AddRoundWin(int team)
    {
        _teamRoundWins.TryGetValue(team, out int wins);
        _teamRoundWins[team] = wins + 1;
    }

    private void AwardTeamScoreToParticipants(int team, int score)
    {
        IReadOnlyList<MatchParticipant> participants = GetParticipantsByTeam(team);
        for (int i = 0; i < participants.Count; i++)
        {
            participants[i].AddScore(score);
            ParticipantStatChanged?.Invoke(participants[i]);
        }
    }

    private void RefreshAliveStates()
    {
        for (int i = 0; i < _roster.Count; i++)
        {
            _roster[i].MarkAlive();
        }
    }

    private void SetState(MatchState state)
    {
        if (_state == state)
        {
            return;
        }

        _state = state;
        RoundStateChanged?.Invoke(_state);
    }

    private void ResetRuntimeData()
    {
        ResetParticipantsForRound();
        SetRoundRespawnPolicy(true);
        UnbindSpawnZones();
        TeardownModeRuntime();
        for (int i = 0; i < _roster.Count; i++)
        {
            if (_roster[i].Info &&
                _roster[i].Info.TryGetComponent(out PlayerLifeCtrl life))
            {
                life.SetRespawnDelayOverride(-1f);
            }

            if (_roster[i].Info && _roster[i].Info.TryGetComponent(out AIPlayerCtrl ai))
            {
                ai.UninstallModePatch();
            }
        }
        _assistTracker.Clear();
        UnsubscribeAllRespawns();
        _roster.Clear();
        _participantsBySlot.Clear();
        _participantsByInfo.Clear();
        _participantsByTeam.Clear();
        _teamScores.Clear();
        _teamRoundWins.Clear();
        _isEndingRound = false;
        _nextRoundStartTime = -1f;
    }

    private void BindSpawnZones()
    {
        UnbindSpawnZones();
        PlayerSpawnZone[] zones = FindObjectsByType<PlayerSpawnZone>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < zones.Length; i++)
        {
            if (!zones[i]) continue;
            zones[i].Spawned += HandleSpawned;
            _spawnZones.Add(zones[i]);
        }
    }

    private void UnbindSpawnZones()
    {
        for (int i = 0; i < _spawnZones.Count; i++)
        {
            if (_spawnZones[i]) _spawnZones[i].Spawned -= HandleSpawned;
        }
        _spawnZones.Clear();
    }

    private void HandleSpawned(CharacterHealth health, int slot)
    {
        BindParticipant(slot, health);
    }

    private void SetupModeRuntime()
    {
        if (_config == null)
        {
            return;
        }
        if (_config.Registry.GetBool(
            "features.bombObjective", _config.Mode.Kind == ModeKind.Bomb))
        {
            _bombRuntime = gameObject.AddComponent<BombRules>();
            _bombRuntime.Configure(
                _config.Registry,
                GetTeam,
                FindSpawnForTeam(_config.Registry.GetInt(
                    "bomb.attackerTeam",
                    BombRules.DefaultAttackerTeam)));
            _bombRuntime.RoundWon += HandleBombRoundWon;
            _bombRuntime.BombCarrierChanged += HandleBombCarrierChanged;
        }
        if (_config.Registry.GetBool(
            "features.economy", _config.Mode.Kind == ModeKind.Bomb))
        {
            _bombEconomy = gameObject.AddComponent<BombEconomyService>();
            _bombEconomy.Initialize(this, _config.Registry);
        }
    }

    private void TeardownModeRuntime()
    {
        for (int i = 0; i < _roster.Count; i++)
        {
            PlayerInfo info = _roster[i].Info;
            if (!info) continue;
            if (info.TryGetComponent(out BombEconomyPlayerCtrl economyPlayer))
            {
                economyPlayer.Shutdown();
                Destroy(economyPlayer);
            }
            if (info.TryGetComponent(out PlayerArmorCtrl armor)) Destroy(armor);
        }
        if (_bombRuntime)
        {
            _bombRuntime.RoundWon -= HandleBombRoundWon;
            _bombRuntime.BombCarrierChanged -= HandleBombCarrierChanged;
            Destroy(_bombRuntime);
            _bombRuntime = null;
        }
        if (_bombEconomy)
        {
            Destroy(_bombEconomy);
            _bombEconomy = null;
        }
        DroppedWeaponEntity.ClearAll();
    }

    private Transform FindSpawnForTeam(int team)
    {
        for (int i = 0; i < _spawnZones.Count; i++)
        {
            PlayerSpawnZone zone = _spawnZones[i];
            MatchParticipant participant = GetParticipantBySlot(zone.MatchSlotOffset);
            if (participant != null && participant.Team == team)
            {
                return zone.transform;
            }
        }
        return transform;
    }

    private PlayerSpawnZone FindSpawnZoneForParticipant(MatchParticipant participant)
    {
        PlayerSpawnZoneMode expectedMode = participant.IsAI
            ? PlayerSpawnZoneMode.Ai
            : PlayerSpawnZoneMode.Player;
        for (int i = 0; i < _spawnZones.Count; i++)
        {
            PlayerSpawnZone zone = _spawnZones[i];
            if (zone && zone.Mode == expectedMode && zone.OwnsMatchSlot(participant.Slot))
            {
                return zone;
            }
        }

        return null;
    }

    private void HandleBombRoundWon(int team)
    {
        EndRound(team);
    }

    private void HandleBombCarrierChanged(PlayerInfo carrier)
    {
        BombCarrierChanged?.Invoke(carrier);
        if (LocalPlayerLocator.TryGet(out GameObject localPlayer))
        {
            PlayerCombatHudUI hud = FindFirstObjectByType<PlayerCombatHudUI>(
                FindObjectsInactive.Include);
            if (hud)
            {
                hud.SetBombCarried(carrier && carrier.gameObject == localPlayer);
            }
        }
    }

    private void ConfigureParticipantForMode(MatchParticipant participant)
    {
        if (participant == null || !participant.Info)
        {
            return;
        }
        if (participant.Info.TryGetComponent(out PlayerLifeCtrl life))
        {
            life.SetAutoRespawn(GetModeRespawnEnabled());
            float respawnDelay = _config.Registry.GetFloat(
                "match.respawnDelay",
                _config.Registry.GetFloat("tdm.respawnDelay", _config.Mode.RespawnDelay));
            life.SetRespawnDelayOverride(respawnDelay);
        }
        if (_bombEconomy)
        {
            if (!participant.Info.TryGetComponent(out BombEconomyPlayerCtrl economyPlayer))
            {
                economyPlayer = participant.Info.gameObject.AddComponent<BombEconomyPlayerCtrl>();
            }
            economyPlayer.Initialize(
                _bombEconomy,
                participant,
                _config.Registry.GetString(
                    "economy.defaultPistol", "weapon.m1911.data"));
        }
        if (!participant.Info.TryGetComponent(out AIPlayerCtrl ai))
        {
            return;
        }
        IAIModePatch patch = CreateAiPatch(participant.Team);
        AIModeContext context = new AIModeContext
        {
            TargetFilter = actor => actor && actor.PlayerInfo &&
                (string.Equals(_config.Mode.AIPatchId, "solo.self_preservation",
                    StringComparison.OrdinalIgnoreCase) ||
                 !IsFriendly(participant.Info, actor.PlayerInfo)),
            ObjectiveProvider = owner => GetAiObjective(participant)
        };
        ai.InstallModePatch(patch, context);
    }

    private void SetRoundRespawnPolicy(bool roundEnded)
    {
        if (_config == null) return;
        bool enabled = _config.Registry.GetBool(
            "features.eliminationRounds", _config.Mode.Kind == ModeKind.Bomb)
            ? roundEnded
            : GetModeRespawnEnabled();
        for (int i = 0; i < _roster.Count; i++)
        {
            if (_roster[i].Info &&
                _roster[i].Info.TryGetComponent(out PlayerLifeCtrl life))
            {
                life.SetAutoRespawn(enabled);
            }
        }
    }

    private bool GetModeRespawnEnabled()
    {
        if (_config == null || !_config.Mode)
        {
            return true;
        }

        return _config.Registry.GetBool(
            "match.respawnEnabled",
            _config.Registry.GetBool("solo.respawnEnabled", _config.Mode.RespawnEnabled));
    }

    private void ResetParticipantsForRound()
    {
        for (int i = 0; i < _roster.Count; i++)
        {
            MatchParticipant participant = _roster[i];
            if (!participant.Health)
            {
                continue;
            }

            if (participant.Info.TryGetComponent(out PlayerLifeCtrl life))
            {
                life.ResetForRound();
                if (participant.Info.TryGetComponent(
                    out BombEconomyPlayerCtrl economyPlayer))
                {
                    economyPlayer.PrepareForRound();
                }
                continue;
            }

            participant.Health.ResetHealth();
            if (participant.Info.TryGetComponent(out WeaponCombatCtrl combat))
            {
                combat.ResetAllAmmoToFull();
            }
        }
    }

    private IAIModePatch CreateAiPatch(int team)
    {
        string patchId = _config.Mode.AIPatchId ?? string.Empty;
        if (_bombRuntime || patchId.StartsWith("bomb.", StringComparison.OrdinalIgnoreCase))
        {
            int attacker = _config.Registry.GetInt(
                "bomb.attackerTeam",
                BombRules.DefaultAttackerTeam);
            return team == attacker
                ? (IAIModePatch)new BombAttackPatch()
                : new BombDefensePatch();
        }
        if (patchId.StartsWith("tdm.", StringComparison.OrdinalIgnoreCase))
        {
            return new TeamworkPatch();
        }
        return new SoloSelfPreservationPatch();
    }

    private AIObjective? GetAiObjective(MatchParticipant participant)
    {
        if (_bombRuntime)
        {
            if (_bombRuntime && _bombRuntime.Bomb &&
                _bombRuntime.Bomb.State == BombEntityState.Planted)
            {
                return new AIObjective(
                    _bombRuntime.Bomb.transform.position,
                    AIObjectiveKind.Bomb);
            }
            IReadOnlyList<BombSite> sites = BombSite.Sites;
            if (sites.Count > 0 && sites[0])
            {
                int attacker = _config.Registry.GetInt(
                    "bomb.attackerTeam",
                    BombRules.DefaultAttackerTeam);
                return new AIObjective(
                    sites[0].Position,
                    participant.Team == attacker
                        ? AIObjectiveKind.AttackSite
                        : AIObjectiveKind.DefendSite);
            }
        }
        if ((_config.Mode.AIPatchId ?? string.Empty).StartsWith(
            "tdm.", StringComparison.OrdinalIgnoreCase))
        {
            IReadOnlyList<MatchParticipant> team = GetParticipantsByTeam(participant.Team);
            for (int i = 0; i < team.Count; i++)
            {
                if (team[i] != participant && team[i].Health && team[i].IsAlive)
                {
                    return new AIObjective(
                        team[i].Health.transform.position,
                        AIObjectiveKind.Regroup);
                }
            }
        }
        return null;
    }

    private void NotifyAiRoundStart()
    {
        int round = 0;
        foreach (KeyValuePair<int, int> entry in _teamRoundWins) round += entry.Value;
        for (int i = 0; i < _roster.Count; i++)
        {
            if (_roster[i].Info && _roster[i].Info.TryGetComponent(out AIPlayerCtrl ai))
            {
                ai.NotifyRoundStart(round);
            }
        }
    }

    private void NotifyAiRoundEnd(int winningTeam)
    {
        for (int i = 0; i < _roster.Count; i++)
        {
            MatchParticipant participant = _roster[i];
            if (participant.Info && participant.Info.TryGetComponent(out AIPlayerCtrl ai))
            {
                ai.NotifyRoundEnd(participant.Team == winningTeam);
            }
        }
    }

    private void UnbindParticipant(MatchParticipant participant)
    {
        if (participant == null)
        {
            return;
        }

        if (participant.Info)
        {
            _participantsByInfo.Remove(participant.Info);
        }
        if (_boundLifeCtrls.TryGetValue(participant, out PlayerLifeCtrl life) && life)
        {
            life.Respawned -= HandleParticipantRespawned;
        }
        _boundLifeCtrls.Remove(participant);
        participant.Unbind();
    }

    private void UnsubscribeAllRespawns()
    {
        DetachRespawnSubscriptions();
        _boundLifeCtrls.Clear();
    }

    private void DetachRespawnSubscriptions()
    {
        foreach (KeyValuePair<MatchParticipant, PlayerLifeCtrl> entry in _boundLifeCtrls)
        {
            if (entry.Value)
            {
                entry.Value.Respawned -= HandleParticipantRespawned;
            }
        }
    }

    private void SubscribeBoundRespawns()
    {
        foreach (KeyValuePair<MatchParticipant, PlayerLifeCtrl> entry in _boundLifeCtrls)
        {
            if (!entry.Value)
            {
                continue;
            }

            entry.Value.Respawned -= HandleParticipantRespawned;
            entry.Value.Respawned += HandleParticipantRespawned;
        }
    }
}
