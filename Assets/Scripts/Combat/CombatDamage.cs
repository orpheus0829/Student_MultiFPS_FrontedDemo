using System;
using System.Collections.Generic;
using UnityEngine;

public static class CombatDamage
{
    private const float DefaultHitScore = 10f;
    private const float DefaultKillBonus = 100f;
    private static readonly HashSet<CharacterHealth> ResolvingTargets =
        new HashSet<CharacterHealth>();

    public static event Action<DamageResult> DamageResolved;

    public static DamageResult Apply(
        CharacterHealth target,
        float damage,
        PlayerInfo instigator = null)
    {
        DamageRequest request = new DamageRequest(target, damage);
        request.Instigator = instigator;
        request.BaseHitScore = DefaultHitScore;
        return Apply(request);
    }

    public static DamageResult Apply(DamageRequest request)
    {
        if (request == null || !request.Target)
        {
            return CreateIgnoredResult(request);
        }

        CharacterHealth target = request.Target;
        if (target.IsDead || !ResolvingTargets.Add(target))
        {
            return CreateIgnoredResult(request);
        }

        try
        {
            return Resolve(request, target);
        }
        finally
        {
            ResolvingTargets.Remove(target);
        }
    }

    public static DamageResult Kill(CharacterHealth target, PlayerInfo instigator = null)
    {
        DamageRequest request = new DamageRequest(target, target ? target.CurrentHealth : 0f);
        request.Instigator = instigator;
        request.BaseHitScore = DefaultHitScore;
        request.KillBonus = DefaultKillBonus;
        return Kill(request);
    }

    public static DamageResult Kill(DamageRequest request)
    {
        if (request == null)
        {
            return CreateIgnoredResult(null);
        }

        DamageRequest killRequest = new DamageRequest(request);
        killRequest.BaseDamage = killRequest.Target ? killRequest.Target.CurrentHealth : 0f;
        killRequest.BodyDamageFactor = 1f;
        return Apply(killRequest);
    }

    private static DamageResult Resolve(DamageRequest request, CharacterHealth target)
    {
        float requestedDamage = Mathf.Max(0f, request.BaseDamage) *
            Mathf.Max(0f, request.BodyDamageFactor);
        if (requestedDamage <= 0f)
        {
            return CreateIgnoredResult(request);
        }

        bool wasKilled;
        float remainingHealth;
        float appliedDamage = target.ApplyDamage(
            requestedDamage,
            out wasKilled,
            out remainingHealth);
        if (appliedDamage <= 0f)
        {
            return CreateIgnoredResult(request);
        }

        int hitScore = 0;
        int killScore = 0;
        if (CanAwardScore(request, target))
        {
            hitScore = CalculateHitScore(request);
            if (wasKilled)
            {
                killScore = Mathf.Max(0, Mathf.RoundToInt(request.KillBonus));
            }
        }

        DamageOutcome outcome = wasKilled ? DamageOutcome.Kill : DamageOutcome.Hit;
        DamageResult result = new DamageResult(
            outcome,
            request,
            target.PlayerInfo,
            requestedDamage,
            appliedDamage,
            remainingHealth,
            hitScore,
            killScore);
        DamageResolved?.Invoke(result);
        return result;
    }

    private static bool CanAwardScore(DamageRequest request, CharacterHealth target)
    {
        if (!request.Instigator)
        {
            return false;
        }

        PlayerInfo victim = target.PlayerInfo;
        return !victim || victim != request.Instigator;
    }

    private static int CalculateHitScore(DamageRequest request)
    {
        float score = Mathf.Max(0f, request.BaseHitScore) *
            Mathf.Max(0f, request.WeaponScoreFactor) *
            Mathf.Max(0f, request.DistanceScoreFactor) *
            Mathf.Max(0f, request.BodyScoreFactor);
        return Mathf.Max(0, Mathf.RoundToInt(score));
    }

    private static DamageResult CreateIgnoredResult(DamageRequest request)
    {
        CharacterHealth target = request != null ? request.Target : null;
        PlayerInfo victim = target ? target.PlayerInfo : null;
        float remainingHealth = target ? target.CurrentHealth : 0f;
        return new DamageResult(
            DamageOutcome.Ignored,
            request,
            victim,
            0f,
            0f,
            remainingHealth,
            0,
            0);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        DamageResolved = null;
        ResolvingTargets.Clear();
    }
}
