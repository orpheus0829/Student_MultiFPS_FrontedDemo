using System;

[Serializable]
public sealed class AIUtilityWeights
{
    public float PatrolNoTarget { get; private set; }
    public float PatrolQuiet { get; private set; }
    public float InvestigateConfidence { get; private set; }
    public float InvestigateNoise { get; private set; }
    public float EngageVisibility { get; private set; }
    public float EngageRange { get; private set; }
    public float EngageHealth { get; private set; }
    public float EngageAggression { get; private set; }
    public float CoverVisibility { get; private set; }
    public float CoverDanger { get; private set; }
    public float CoverAvailable { get; private set; }
    public float CoverPreference { get; private set; }
    public float CoverReload { get; private set; }
    public float RepositionDistance { get; private set; }
    public float RepositionStrafe { get; private set; }
    public float RepositionFlank { get; private set; }
    public float RetreatDanger { get; private set; }
    public float RetreatCover { get; private set; }
    public float RetreatPreference { get; private set; }

    public AIUtilityWeights(
        float tacticalScale,
        float aggressionScale,
        float retreatScale)
    {
        PatrolNoTarget = 0.55f;
        PatrolQuiet = 0.25f;
        InvestigateConfidence = 0.46f;
        InvestigateNoise = 0.42f;
        EngageVisibility = 0.3f;
        EngageRange = 0.25f;
        EngageHealth = 0.12f;
        EngageAggression = 0.28f * aggressionScale;
        CoverVisibility = 0.12f;
        CoverDanger = 0.46f * tacticalScale;
        CoverAvailable = 0.16f * tacticalScale;
        CoverPreference = 0.42f * tacticalScale;
        CoverReload = 0.24f;
        RepositionDistance = 0.48f;
        RepositionStrafe = 0.22f * tacticalScale;
        RepositionFlank = 0.35f * tacticalScale;
        RetreatDanger = 0.72f * retreatScale;
        RetreatCover = 0.18f * retreatScale;
        RetreatPreference = 0.3f * retreatScale;
    }
}

[Serializable]
public sealed class AIDifficultyProfile
{
    public AIDifficulty Difficulty { get; private set; }
    public float SightRange { get; private set; }
    public float FovDegrees { get; private set; }
    public float HearingRadius { get; private set; }
    public float SenseInterval { get; private set; }
    public float ReactionTime { get; private set; }
    public float MemoryDuration { get; private set; }
    public float DecisionInterval { get; private set; }
    public float MinimumCommitTime { get; private set; }
    public float Hysteresis { get; private set; }
    public float TurnRate { get; private set; }
    public float AimErrorInitial { get; private set; }
    public float AimErrorSettled { get; private set; }
    public float AimSettleTime { get; private set; }
    public float LeadFactor { get; private set; }
    public float HeadshotChance { get; private set; }
    public float RecoilCompensation { get; private set; }
    public float AimChance { get; private set; }
    public float FireSpreadGate { get; private set; }
    public float FireDuration { get; private set; }
    public float FirePause { get; private set; }
    public float TacticalReloadRatio { get; private set; }
    public float PreferredRange { get; private set; }
    public float RangeBand { get; private set; }
    public float StrafeScale { get; private set; }
    public float CoverPreference { get; private set; }
    public float RetreatHealthRatio { get; private set; }
    public float ReturnHealthRatio { get; private set; }
    public float FlankChance { get; private set; }
    public bool CanSwitchWeapon { get; private set; }
    public bool CanMelee { get; private set; }
    public bool CanCombatJump { get; private set; }
    public bool CanTacticalSlide { get; private set; }
    public float DistanceWeight { get; private set; }
    public float AngleWeight { get; private set; }
    public float ThreatWeight { get; private set; }
    public float VulnerabilityWeight { get; private set; }
    public float StickyTargetWeight { get; private set; }
    public float Aggression { get; private set; }
    public float RetreatPreference { get; private set; }
    public AIUtilityWeights Utility { get; private set; }

    public static AIDifficultyProfile Create(AIDifficulty difficulty)
    {
        AIDifficultyProfile p = new AIDifficultyProfile();
        p.Difficulty = difficulty;
        p.Hysteresis = 0.16f;
        p.FireSpreadGate = 4f;
        p.DistanceWeight = 0.55f;
        p.AngleWeight = 0.45f;
        p.StickyTargetWeight = 0.2f;
        if (difficulty == AIDifficulty.Easy)
        {
            p.SightRange = 28f; p.FovDegrees = 110f; p.HearingRadius = 10f;
            p.SenseInterval = 0.25f; p.ReactionTime = 0.8f; p.MemoryDuration = 2.5f;
            p.DecisionInterval = 0.9f; p.MinimumCommitTime = 3.2f; p.TurnRate = 180f;
            p.AimErrorInitial = 9f; p.AimErrorSettled = 5.5f; p.AimSettleTime = 1.1f;
            p.AimChance = 0.4f; p.FireSpreadGate = 6.5f; p.FireDuration = 0.42f;
            p.FirePause = 1f; p.PreferredRange = 11f; p.RangeBand = 3f;
            p.StrafeScale = 0.6f; p.CoverPreference = 0.35f;
            p.RetreatHealthRatio = 0.2f; p.ReturnHealthRatio = 0.6f;
            p.Aggression = 1f; p.RetreatPreference = 0.15f;
            p.Utility = new AIUtilityWeights(0.35f, 1.2f, 0.45f);
            return p;
        }
        if (difficulty == AIDifficulty.Hard)
        {
            p.SightRange = 65f; p.FovDegrees = 175f; p.HearingRadius = 30f;
            p.SenseInterval = 0.08f; p.ReactionTime = 0.15f; p.MemoryDuration = 8f;
            p.DecisionInterval = 0.12f; p.MinimumCommitTime = 0.7f; p.Hysteresis = 0.1f;
            p.TurnRate = 520f; p.AimErrorInitial = 1.2f; p.AimErrorSettled = 0.9f;
            p.AimSettleTime = 0.15f; p.LeadFactor = 1f; p.HeadshotChance = 0.4f;
            p.RecoilCompensation = 0.85f; p.AimChance = 1f; p.FireSpreadGate = 2f;
            p.FireDuration = 1.1f; p.FirePause = 0.25f; p.TacticalReloadRatio = 0.65f;
            p.PreferredRange = 21f; p.RangeBand = 3f; p.StrafeScale = 1f;
            p.CoverPreference = 0.9f; p.RetreatHealthRatio = 0.55f;
            p.ReturnHealthRatio = 0.9f; p.FlankChance = 0.6f;
            p.CanSwitchWeapon = true; p.CanMelee = true; p.CanCombatJump = true;
            p.CanTacticalSlide = true; p.ThreatWeight = 0.65f;
            p.VulnerabilityWeight = 0.55f; p.Aggression = 0.82f;
            p.RetreatPreference = 1f;
            p.Utility = new AIUtilityWeights(1.15f, 0.9f, 1.2f); return p;
        }
        p.Difficulty = AIDifficulty.Normal;
        p.SightRange = 45f; p.FovDegrees = 140f; p.HearingRadius = 18f;
        p.SenseInterval = 0.15f; p.ReactionTime = 0.38f; p.MemoryDuration = 4.5f;
        p.DecisionInterval = 0.3f; p.MinimumCommitTime = 1.5f; p.TurnRate = 360f;
        p.AimErrorInitial = 4f; p.AimErrorSettled = 2.2f; p.AimSettleTime = 0.4f;
        p.LeadFactor = 0.6f; p.HeadshotChance = 0.2f; p.RecoilCompensation = 0.5f;
        p.AimChance = 0.85f; p.FireSpreadGate = 3.5f; p.FireDuration = 0.72f;
        p.FirePause = 0.45f; p.TacticalReloadRatio = 0.4f; p.PreferredRange = 17f;
        p.RangeBand = 3f; p.StrafeScale = 0.85f; p.CoverPreference = 0.6f;
        p.RetreatHealthRatio = 0.38f; p.ReturnHealthRatio = 0.75f;
        p.FlankChance = 0.25f; p.CanSwitchWeapon = true; p.CanMelee = true;
        p.CanCombatJump = true; p.ThreatWeight = 0.55f; p.Aggression = 0.9f;
        p.RetreatPreference = 0.62f;
        p.Utility = new AIUtilityWeights(0.8f, 1f, 0.85f); return p;
    }
}
