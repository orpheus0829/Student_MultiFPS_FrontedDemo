using UnityEngine;

public readonly struct PlayerAnimationFrame
{
    public Vector2 Move { get; }
    public float Speed { get; }
    public bool IsGrounded { get; }
    public float VerticalSpeed { get; }
    public bool IsSprinting { get; }
    public float AimProgress { get; }
    public bool IsVaulting { get; }
    public bool IsSliding { get; }

    public PlayerAnimationFrame(
        Vector2 move,
        float speed,
        bool isGrounded,
        float verticalSpeed,
        bool isSprinting,
        float aimProgress,
        bool isVaulting,
        bool isSliding)
    {
        Move = move;
        Speed = speed;
        IsGrounded = isGrounded;
        VerticalSpeed = verticalSpeed;
        IsSprinting = isSprinting;
        AimProgress = aimProgress;
        IsVaulting = isVaulting;
        IsSliding = isSliding;
    }
}

public static class PlayerAnimatorContract
{
    public const string GaitName = "Gait";
    public const string TacSprintWeightName = "TacSprintWeight";
    public const string RightHandWeightName = "RightHandWeight";
    public const string TacSprintLayerName = "TacSprint";
    public const string TriggerDisciplineLayerName = "TriggerDiscipline";
    public const string RightHandLayerName = "RightHand";
    public const string MoveXName = "MoveX";
    public const string MoveYName = "MoveY";
    public const string SpeedName = "Speed";
    public const string GroundedName = "Grounded";
    public const string VerticalSpeedName = "VerticalSpeed";
    public const string SprintingName = "Sprinting";
    public const string AimProgressName = "AimProgress";
    public const string VaultName = "Vault";
    public const string VaultingName = "Vaulting";
    public const string SlideName = "Slide";
    public const string SlidingName = "Sliding";
    public const string WeaponActionName = "WeaponAction";
    public const string WeaponActionStartedName = "WeaponActionStarted";
    public const string WeaponActionCompletedName = "WeaponActionCompleted";
    public const string WeaponActionCancelledName = "WeaponActionCancelled";

    public static readonly int Gait = Animator.StringToHash(GaitName);
    public static readonly int TacSprintWeight = Animator.StringToHash(TacSprintWeightName);
    public static readonly int RightHandWeight = Animator.StringToHash(RightHandWeightName);
    public static readonly int MoveX = Animator.StringToHash(MoveXName);
    public static readonly int MoveY = Animator.StringToHash(MoveYName);
    public static readonly int Speed = Animator.StringToHash(SpeedName);
    public static readonly int Grounded = Animator.StringToHash(GroundedName);
    public static readonly int VerticalSpeed = Animator.StringToHash(VerticalSpeedName);
    public static readonly int Sprinting = Animator.StringToHash(SprintingName);
    public static readonly int AimProgress = Animator.StringToHash(AimProgressName);
    public static readonly int Vault = Animator.StringToHash(VaultName);
    public static readonly int Vaulting = Animator.StringToHash(VaultingName);
    public static readonly int Slide = Animator.StringToHash(SlideName);
    public static readonly int Sliding = Animator.StringToHash(SlidingName);
    public static readonly int WeaponAction = Animator.StringToHash(WeaponActionName);
    public static readonly int WeaponActionStarted = Animator.StringToHash(WeaponActionStartedName);
    public static readonly int WeaponActionCompleted = Animator.StringToHash(WeaponActionCompletedName);
    public static readonly int WeaponActionCancelled = Animator.StringToHash(WeaponActionCancelledName);
}
