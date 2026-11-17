using System;
using System.Collections.Generic;
using UnityEngine;

[Flags]
public enum AIBodyPointMask
{
    None = 0,
    Feet = 1,
    Chest = 2,
    Head = 4
}

public sealed class AITrackedTarget
{
    public CharacterHealth Actor;
    public float Distance;
    public bool Visible;
    public Vector3 LastKnownPosition;
    public Vector3 LastKnownVelocity;
    public float LastSeenTime;
    public float VisibleSince;
    public float Confidence;
    public AIBodyPointMask VisiblePoints;
    public float ThreatScore;
    public float Vulnerability;
    public float UncertaintyRadius;
}

public sealed class AIWorldModel
{
    private readonly Dictionary<CharacterHealth, AITrackedTarget> _tracks =
        new Dictionary<CharacterHealth, AITrackedTarget>();
    private readonly RaycastHit[] _hits = new RaycastHit[32];
    private readonly CharacterHealth _self;
    private readonly Transform _transform;
    private LayerMask _sightMask;
    private AIDifficultyProfile _profile;
    private float _nextSenseTime;
    private Vector3 _noisePosition;
    private float _noiseTime = float.NegativeInfinity;
    private Func<CharacterHealth, bool> _targetFilter;
    private float _damagePressure;
    private float _lastDamageTime = float.NegativeInfinity;

    public AITrackedTarget CurrentTarget { get; private set; }
    public bool HasRecentNoise
    {
        get { return Time.time - _noiseTime <= _profile.MemoryDuration; }
    }
    public Vector3 SuspiciousPosition { get { return _noisePosition; } }
    public float NoiseConfidence
    {
        get
        {
            if (!HasRecentNoise) return 0f;
            return 1f - Mathf.Clamp01(
                (Time.time - _noiseTime) / Mathf.Max(0.1f, _profile.MemoryDuration));
        }
    }
    public float RecentDamagePressure { get { return _damagePressure; } }
    public float TimeSinceDamaged { get { return Time.time - _lastDamageTime; } }

    public AIWorldModel(
        CharacterHealth self,
        Transform transform,
        LayerMask sightMask,
        AIDifficultyProfile profile)
    {
        _self = self;
        _transform = transform;
        _sightMask = sightMask;
        _profile = profile;
    }

    public void SetProfile(AIDifficultyProfile profile)
    {
        _profile = profile;
        _nextSenseTime = 0f;
    }

    public void SetTargetFilter(Func<CharacterHealth, bool> targetFilter)
    {
        _targetFilter = targetFilter;
        if (CurrentTarget != null && !CanConsiderAsTarget(CurrentTarget.Actor))
        {
            CurrentTarget = null;
        }
    }

    public void Enable()
    {
        CombatDamage.DamageResolved += HandleDamageResolved;
        AINoiseBus.NoiseEmitted += HandleNoise;
    }

    public void Disable()
    {
        CombatDamage.DamageResolved -= HandleDamageResolved;
        AINoiseBus.NoiseEmitted -= HandleNoise;
    }

    public void Sense()
    {
        if (Time.time < _nextSenseTime)
        {
            DecayTracks();
            return;
        }
        _nextSenseTime = Time.time + _profile.SenseInterval;
        IReadOnlyList<CharacterHealth> actors = CharacterHealth.Actors;
        for (int i = 0; i < actors.Count; i++)
        {
            CharacterHealth actor = actors[i];
            if (!CanConsiderAsTarget(actor))
            {
                continue;
            }
            UpdateTrack(actor);
        }
        SelectTarget();
        DecayTracks();
    }

    public bool CanConsiderAsTarget(CharacterHealth actor)
    {
        if (!actor || actor.IsDead || actor == _self ||
            actor.transform.IsChildOf(_transform))
        {
            return false;
        }

        MatchManager match = MatchManager.Current;
        if (match && _self.PlayerInfo && actor.PlayerInfo &&
            match.IsFriendly(_self.PlayerInfo, actor.PlayerInfo))
        {
            return false;
        }

        return _targetFilter == null || _targetFilter(actor);
    }

    public Vector3 GetKnownTargetPosition()
    {
        return CurrentTarget != null ? CurrentTarget.LastKnownPosition : _transform.position;
    }

    public void ReceiveSharedTarget(AITrackedTarget source, float confidence)
    {
        if (source == null || !CanConsiderAsTarget(source.Actor))
        {
            return;
        }

        if (!_tracks.TryGetValue(source.Actor, out AITrackedTarget track))
        {
            track = new AITrackedTarget { Actor = source.Actor };
            _tracks.Add(source.Actor, track);
        }

        float sharedConfidence = Mathf.Clamp01(confidence * source.Confidence);
        if (track.Visible || track.Confidence >= sharedConfidence)
        {
            return;
        }

        track.Visible = false;
        track.VisiblePoints = AIBodyPointMask.None;
        track.LastKnownPosition = source.LastKnownPosition;
        track.LastKnownVelocity = source.LastKnownVelocity;
        track.Distance = Vector3.Distance(_transform.position, source.LastKnownPosition);
        track.Confidence = sharedConfidence;
        track.LastSeenTime = Time.time -
            (1f - sharedConfidence) * _profile.MemoryDuration;
        track.ThreatScore = Mathf.Max(track.ThreatScore, source.ThreatScore * 0.75f);
        track.Vulnerability = source.Vulnerability;
        track.UncertaintyRadius = Mathf.Max(1f, source.UncertaintyRadius + 1.5f);
    }

    public bool PrioritizeModeTarget(CharacterHealth actor, float awarenessRange)
    {
        if (!CanConsiderAsTarget(actor))
        {
            return false;
        }

        Vector3 delta = actor.transform.position - _transform.position;
        float distance = delta.magnitude;
        if (distance > awarenessRange)
        {
            return false;
        }

        if (!_tracks.TryGetValue(actor, out AITrackedTarget track))
        {
            track = new AITrackedTarget { Actor = actor };
            _tracks.Add(actor, track);
        }

        bool wasVisible = track.Visible;
        AIBodyPointMask points = CheckVisiblePoints(actor);
        track.Distance = distance;
        track.Visible = points != AIBodyPointMask.None;
        track.VisiblePoints = points;
        if (track.Visible && !wasVisible)
        {
            track.VisibleSince = Time.time;
        }
        track.LastKnownPosition = actor.transform.position;
        track.LastKnownVelocity = Vector3.zero;
        track.LastSeenTime = Time.time;
        track.Confidence = 1f;
        track.ThreatScore = 1f;
        track.Vulnerability = 1f;
        track.UncertaintyRadius = track.Visible ? 0f : 1.5f;
        CurrentTarget = track;
        return track.Visible;
    }

    private void UpdateTrack(CharacterHealth actor)
    {
        if (!_tracks.TryGetValue(actor, out AITrackedTarget track))
        {
            track = new AITrackedTarget { Actor = actor };
            _tracks.Add(actor, track);
        }
        Vector3 delta = actor.transform.position - _transform.position;
        track.Distance = delta.magnitude;
        bool inRange = track.Distance <= _profile.SightRange;
        float angle = Vector3.Angle(_transform.forward, delta);
        float closeAwareness = _profile.Difficulty == AIDifficulty.Hard
            ? 12f
            : _profile.Difficulty == AIDifficulty.Normal ? 8f : 4f;
        bool inFov = angle <= _profile.FovDegrees * 0.5f ||
            track.Distance <= closeAwareness;
        AIBodyPointMask points = inRange && inFov ? CheckVisiblePoints(actor) : AIBodyPointMask.None;
        bool wasVisible = track.Visible;
        track.Visible = points != AIBodyPointMask.None;
        track.VisiblePoints = points;
        if (!track.Visible)
        {
            return;
        }
        float elapsed = Mathf.Max(0.02f, Time.time - track.LastSeenTime);
        if (!wasVisible)
        {
            track.VisibleSince = Time.time;
        }
        Vector3 position = actor.transform.position;
        track.LastKnownVelocity = track.LastSeenTime > 0f
            ? (position - track.LastKnownPosition) / elapsed
            : Vector3.zero;
        track.LastKnownPosition = position;
        track.LastSeenTime = Time.time;
        track.Confidence = 1f;
        WeaponActionCtrl actions = actor.GetComponent<WeaponActionCtrl>();
        PlayerActionCtrl playerActions = actor.GetComponent<PlayerActionCtrl>();
        bool reloading = actions && actions.CurrentAction != null &&
            IsReloadAction(actions.CurrentAction.ActionType);
        float lowHealth = 1f - Mathf.Clamp01(
            actor.CurrentHealth / Mathf.Max(1f, actor.MaxHealth));
        track.Vulnerability = Mathf.Clamp01(
            lowHealth * 0.55f + (reloading ? 0.55f : 0f) +
            (actor.IsRecovering ? 0.45f : 0f));
        Vector3 threatDirection = _transform.position - actor.transform.position;
        threatDirection.y = 0f;
        float facingThreat = threatDirection.sqrMagnitude > 0.01f
            ? Mathf.Clamp01(Vector3.Dot(
                actor.transform.forward,
                threatDirection.normalized))
            : 1f;
        float activeThreat = playerActions && playerActions.IsFiring
            ? 0.75f
            : playerActions && playerActions.IsAiming ? 0.4f : 0f;
        track.ThreatScore = Mathf.Max(
            track.ThreatScore,
            Mathf.Clamp01(activeThreat * Mathf.Lerp(0.45f, 1f, facingThreat)));
    }

    private AIBodyPointMask CheckVisiblePoints(CharacterHealth actor)
    {
        AIBodyPointMask result = AIBodyPointMask.None;
        if (CanSee(actor, 0.3f)) result |= AIBodyPointMask.Feet;
        if (CanSee(actor, 1.15f)) result |= AIBodyPointMask.Chest;
        if (CanSee(actor, 1.6f)) result |= AIBodyPointMask.Head;
        return result;
    }

    private bool CanSee(CharacterHealth actor, float height)
    {
        Vector3 origin = _transform.position + Vector3.up * 1.35f;
        Vector3 point = actor.transform.position + Vector3.up * height;
        Vector3 direction = point - origin;
        int count = Physics.RaycastNonAlloc(origin, direction.normalized, _hits,
            direction.magnitude, _sightMask, QueryTriggerInteraction.Collide);
        float nearest = float.PositiveInfinity;
        CharacterHealth seen = null;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = _hits[i];
            if (hit.transform.IsChildOf(_transform))
            {
                continue;
            }
            BodyHitbox hitbox = hit.collider.GetComponent<BodyHitbox>();
            CharacterHealth candidate = hitbox
                ? hitbox.CharacterHealth
                : hit.collider.GetComponentInParent<CharacterHealth>();
            // 包点、交互范围等 Trigger 只表达玩法区域，不能遮住区域内的角色。
            if (hit.collider.isTrigger && !candidate)
            {
                continue;
            }
            if (hit.distance >= nearest)
            {
                continue;
            }
            nearest = hit.distance;
            seen = candidate;
        }
        return seen == actor;
    }

    private void SelectTarget()
    {
        AITrackedTarget best = null;
        float bestScore = float.NegativeInfinity;
        foreach (AITrackedTarget track in _tracks.Values)
        {
            if (!CanConsiderAsTarget(track.Actor) || track.Confidence <= 0f)
            {
                continue;
            }
            Vector3 direction = track.LastKnownPosition - _transform.position;
            float distanceScore = 1f - Mathf.Clamp01(direction.magnitude / _profile.SightRange);
            float angleScore = 1f - Vector3.Angle(_transform.forward, direction) / 180f;
            float score = distanceScore * _profile.DistanceWeight +
                angleScore * _profile.AngleWeight +
                track.ThreatScore * _profile.ThreatWeight +
                track.Vulnerability * _profile.VulnerabilityWeight +
                (track == CurrentTarget ? _profile.StickyTargetWeight : 0f);
            if (score > bestScore)
            {
                bestScore = score;
                best = track;
            }
        }
        CurrentTarget = best;
    }

    private void DecayTracks()
    {
        _damagePressure = Mathf.MoveTowards(
            _damagePressure,
            0f,
            Time.deltaTime * 0.22f);
        foreach (AITrackedTarget track in _tracks.Values)
        {
            if (track.Visible)
            {
                continue;
            }
            float age = Time.time - track.LastSeenTime;
            track.Confidence = 1f - Mathf.Clamp01(age / Mathf.Max(0.1f, _profile.MemoryDuration));
            track.UncertaintyRadius = Mathf.Lerp(0f, 7f, 1f - track.Confidence);
            track.ThreatScore = Mathf.MoveTowards(track.ThreatScore, 0f, Time.deltaTime * 0.12f);
        }
        if (CurrentTarget != null && CurrentTarget.Confidence <= 0f)
        {
            CurrentTarget = null;
        }
    }

    private void HandleDamageResolved(DamageResult result)
    {
        if (result == null || result.Target != _self || !result.WasApplied)
        {
            return;
        }
        CharacterHealth attacker = result.Instigator
            ? result.Instigator.GetComponent<CharacterHealth>()
            : null;
        _lastDamageTime = Time.time;
        _damagePressure = Mathf.Clamp01(
            _damagePressure + result.AppliedDamage / Mathf.Max(1f, _self.MaxHealth));
        if (CanConsiderAsTarget(attacker))
        {
            if (!_tracks.TryGetValue(attacker, out AITrackedTarget track))
            {
                track = new AITrackedTarget { Actor = attacker };
                _tracks.Add(attacker, track);
            }
            track.LastKnownPosition = attacker.transform.position;
            track.LastSeenTime = Time.time;
            track.Confidence = 1f;
            track.ThreatScore = Mathf.Clamp01(
                track.ThreatScore + result.AppliedDamage / Mathf.Max(1f, _self.MaxHealth));
            CurrentTarget = track;
        }
        else
        {
            _noisePosition = result.HitPoint - result.HitDirection * 4f;
            _noiseTime = Time.time;
        }
    }

    private void HandleNoise(AINoise noise)
    {
        if (noise.Source && noise.Source.IsChildOf(_transform))
        {
            return;
        }

        CharacterHealth sourceActor = noise.Source
            ? noise.Source.GetComponentInParent<CharacterHealth>()
            : null;
        if (sourceActor && !CanConsiderAsTarget(sourceActor))
        {
            return;
        }

        float distance = Vector3.Distance(_transform.position, noise.Position);
        if (distance > Mathf.Min(_profile.HearingRadius, noise.Radius))
        {
            return;
        }
        float hearingFraction = distance /
            Mathf.Max(0.1f, Mathf.Min(_profile.HearingRadius, noise.Radius));
        Vector2 error = UnityEngine.Random.insideUnitCircle *
            noise.Uncertainty * Mathf.Lerp(0.45f, 1.35f, hearingFraction);
        _noisePosition = noise.Position + new Vector3(error.x, 0f, error.y);
        _noiseTime = Time.time;

        if (_profile.Difficulty == AIDifficulty.Easy || !noise.Source)
        {
            return;
        }

        if (!CanConsiderAsTarget(sourceActor))
        {
            return;
        }

        if (!_tracks.TryGetValue(sourceActor, out AITrackedTarget track))
        {
            track = new AITrackedTarget { Actor = sourceActor };
            _tracks.Add(sourceActor, track);
        }

        float confidence = _profile.Difficulty == AIDifficulty.Hard ? 0.8f : 0.5f;
        track.Visible = false;
        track.VisiblePoints = AIBodyPointMask.None;
        track.LastKnownPosition = _noisePosition;
        track.Distance = distance;
        track.Confidence = Mathf.Max(track.Confidence, confidence);
        track.LastSeenTime = Time.time - (1f - track.Confidence) * _profile.MemoryDuration;
    }

    private static bool IsReloadAction(WeaponActionType actionType)
    {
        return actionType == WeaponActionType.Reload ||
            actionType == WeaponActionType.EmptyReload ||
            actionType == WeaponActionType.PerRoundStart ||
            actionType == WeaponActionType.PerRoundInsert ||
            actionType == WeaponActionType.PerRoundEnd;
    }
}
