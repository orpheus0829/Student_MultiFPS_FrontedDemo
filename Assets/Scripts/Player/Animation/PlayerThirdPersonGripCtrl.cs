using KINEMATION.KAnimationCore.Runtime.Core;
using UnityEngine;

[DefaultExecutionOrder(350)]
[DisallowMultipleComponent]
public sealed class PlayerThirdPersonGripCtrl : MonoBehaviour
{
    private const float MaxWristSwingDegrees = 35f;
    private const float MaxWristTwistDegrees = 25f;
    private const int FingerComponentCount = 4;
    private const int FingerCount = 5;
    private const int FingerMusclePerHand = FingerCount * FingerComponentCount;

    private static readonly string[] FingerNames =
    {
        "Thumb", "Index", "Middle", "Ring", "Little"
    };

    private static readonly string[] FingerComponents =
    {
        "1 Stretched", "2 Stretched", "3 Stretched", "Spread"
    };

    private static int[] _fingerMuscleIndices;

    [Header("第三人称握持")]
    [SerializeField, Range(0f, 1f), InspectorName("支撑手贴合程度")]
    private float _gripWeight = 1f;

    private PlayerAnimationCtrl _animationCtrl;
    private PlayerWeaponViewCtrl _weaponView;
    private WeaponActionCtrl _action;
    private CharacterHealth _health;
    private Animator _bodyAnimator;
    private Transform _sourceHand;
    private Transform _sourceMiddle;
    private Transform _sourceIndex;
    private Transform _sourceLittle;
    private Transform _upperArm;
    private Transform _forearm;
    private Transform _hand;
    private Quaternion _handFrame;
    private HumanPoseHandler _poseHandler;
    private HumanPose _humanPose = new HumanPose();
    private bool _leftFingersAvailable;
    private bool _rightFingersAvailable;
    private bool _leftFingerErrorLogged;
    private bool _rightFingerErrorLogged;
    private Transform _resolvedWeapon;
    private WeaponIkData _resolvedIkData;
    private ThirdPersonGripTarget _gripTarget;
    private Vector3 _gripLocalPosition;
    private Quaternion _gripLocalRotation;
    private ThirdPersonGripTargetSource _gripSource;
    private bool _hasGripTarget;
    private string _loggedMissingGripPath;

    private void Awake()
    {
        TryGetComponent(out _animationCtrl);
        TryGetComponent(out _weaponView);
        TryGetComponent(out _action);
        TryGetComponent(out _health);
        if (!_animationCtrl || !_weaponView || !_action)
        {
            enabled = false;
            return;
        }

        Animator firstPersonAnimator = _animationCtrl.FirstPersonView.CharacterAnimator;
        if (!firstPersonAnimator)
        {
            return;
        }
        Transform[] bones = firstPersonAnimator.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < bones.Length; i++)
        {
            switch (bones[i].name)
            {
                case "hand_l": _sourceHand = bones[i]; break;
                case "middle_01_l": _sourceMiddle = bones[i]; break;
                case "index_01_l": _sourceIndex = bones[i]; break;
                case "pinky_01_l": _sourceLittle = bones[i]; break;
            }
        }
    }

    private void OnDisable()
    {
        ReleasePoseHandler();
        _resolvedWeapon = null;
        _resolvedIkData = null;
        _hasGripTarget = false;
    }

    private void LateUpdate()
    {
        Animator animator = _animationCtrl.ThirdPersonView.CharacterAnimator;
        if (animator != _bodyAnimator)
        {
            CacheBody(animator);
        }
        if (!_bodyAnimator || !_bodyAnimator.enabled || !_upperArm || !_forearm || !_hand)
        {
            return;
        }
        if (_health && _health.IsDead)
        {
            return;
        }

        GameObject targetInstance = _weaponView.ThirdPersonWeapon;
        if (!targetInstance)
        {
            _resolvedWeapon = null;
            _resolvedIkData = null;
            _hasGripTarget = false;
            return;
        }

        // 切枪时第三人称武器实例会整体更换；实例或数据一变就重新解析，
        // 保证新枪第一帧起使用的就是自己的锚点，不会沿用上一把枪的缓存。
        Transform targetGun = targetInstance.transform;
        WeaponIkData ikData = _action.CurrentWeaponData ? _action.CurrentWeaponData.Ik : null;
        if (targetGun != _resolvedWeapon || ikData != _resolvedIkData)
        {
            ResolveGripTarget(targetGun, ikData);
        }
        if (!_hasGripTarget)
        {
            return;
        }

        // 握持只补基础姿势；装弹、收枪、检视需要自由活动左手，不能被 IK 拉回护木。
        if (_action.CurrentAction != null &&
            _action.CurrentAction.ActionType != WeaponActionType.Fire)
        {
            return;
        }

        // 锚点挂在随枪运动的节点下，武器每帧都在动，因此由缓存的武器局部值
        // 重新算出本帧世界目标；这里不重新 Find，查找只在切枪/换锚点数据时发生。
        bool usesExplicitAnchor = _gripSource == ThirdPersonGripTargetSource.ExplicitAnchor;
        _gripTarget = new ThirdPersonGripTarget(
            targetGun.TransformPoint(_gripLocalPosition),
            targetGun.rotation * _gripLocalRotation,
            usesExplicitAnchor,
            _gripSource);

        bool isFiring = _action.CurrentAction != null;
        // 顺序固定：Animator 已求值 → 写入本帧指骨 muscle → 解左臂 IK。
        ApplyFingerPose(ikData, isFiring);
        SolveLeftArm(usesExplicitAnchor);
    }

    /// <summary>
    /// 枪械表现切换后调用。显式锚点无需采样第一人称姿势，但空路径的旧枪
    /// 仍要以第一人称 Idle 第 0 帧手骨位置作为回退目标，因此保留采样。
    /// </summary>
    public void CacheNeutralGrip()
    {
        SampleFirstPersonIdle();
        _resolvedWeapon = null;
        _resolvedIkData = null;
        _hasGripTarget = false;
    }

    private void SampleFirstPersonIdle()
    {
        if (!_animationCtrl || !_animationCtrl.FirstPersonView)
        {
            return;
        }

        Animator sourceAnimator = _animationCtrl.FirstPersonView.CharacterAnimator;
        if (sourceAnimator && !sourceAnimator.gameObject.activeInHierarchy &&
            sourceAnimator.runtimeAnimatorController && _weaponView.FirstPersonWeapon)
        {
            // 直接以第三人称进入游戏时，第一人称动画图未启动，KINEMATION
            // 不会调用本方法；从当前枪控制器的 Idle 片段取得同一抓握基准。
            AnimationClip[] clips = sourceAnimator.runtimeAnimatorController.animationClips;
            for (int i = 0; i < clips.Length; i++)
            {
                AnimationClip clip = clips[i];
                if (clip && clip.name.IndexOf("Idle", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    clip.SampleAnimation(sourceAnimator.gameObject, 0f);
                    break;
                }
            }
        }
    }

    private void ResolveGripTarget(Transform targetGun, WeaponIkData ikData)
    {
        _resolvedWeapon = targetGun;
        _resolvedIkData = ikData;
        Transform sourceGun = _weaponView.FirstPersonWeapon
            ? _weaponView.FirstPersonWeapon.transform
            : null;
        ThirdPersonGripTarget resolved;
        if (!ThirdPersonGripTargetResolver.TryResolve(
                ikData, sourceGun, targetGun, _sourceHand, out resolved))
        {
            _hasGripTarget = false;
            ReportGripPathError(targetGun, ikData);
            return;
        }

        _gripSource = resolved.Source;
        _gripLocalPosition = targetGun.InverseTransformPoint(resolved.Position);
        _gripLocalRotation = Quaternion.Inverse(targetGun.rotation) * resolved.Rotation;
        _gripTarget = resolved;
        _hasGripTarget = true;
    }

    private void ReportGripPathError(Transform targetGun, WeaponIkData ikData)
    {
        if (ikData == null || string.IsNullOrWhiteSpace(ikData.LeftHandIkPointPath))
        {
            return;
        }

        string weaponName = _action.CurrentWeaponData
            ? _action.CurrentWeaponData.Basic.WeaponName
            : targetGun.name;
        string key = weaponName + "|" + ikData.LeftHandIkPointPath;
        if (_loggedMissingGripPath == key)
        {
            return;
        }

        _loggedMissingGripPath = key;
        Debug.LogError(
            $"{name} 的第三人称左手锚点解析失败：枪 {weaponName}，路径 " +
            $"{ikData.LeftHandIkPointPath}。本次握持已关闭。", this);
    }

    private void SolveLeftArm(bool usesExplicitAnchor)
    {
        Quaternion animatedHandRotation = _hand.rotation;
        Quaternion targetRotation = animatedHandRotation;
        float rotationWeight = 0f;
        if (usesExplicitAnchor)
        {
            Quaternion targetHandRotation =
                _gripTarget.Rotation * Quaternion.Inverse(_handFrame);
            Quaternion correction =
                targetHandRotation * Quaternion.Inverse(animatedHandRotation);
            Vector3 twistAxisWorld = animatedHandRotation * (_handFrame * Vector3.forward);
            targetRotation = ClampWristCorrection(correction, twistAxisWorld) *
                animatedHandRotation;
            rotationWeight = _gripWeight;
        }

        KTwoBoneIkData data = new KTwoBoneIkData
        {
            root = new KTransform(_upperArm),
            mid = new KTransform(_forearm),
            tip = new KTransform(_hand),
            target = new KTransform(_gripTarget.Position, targetRotation),
            posWeight = _gripWeight,
            rotWeight = rotationWeight,
            hint = new KTransform(
                _upperArm.position - transform.right * 0.35f - transform.up * 0.3f,
                Quaternion.identity),
            hasValidHint = true,
            hintWeight = 1f
        };
        KTwoBoneIK.Solve(ref data);
        _upperArm.rotation = data.root.rotation;
        _forearm.rotation = data.mid.rotation;
        _hand.rotation = data.tip.rotation;
    }

    /// <summary>
    /// 把腕部修正拆成绕动画掌框 +Z 的 twist 与其余 swing，各自限幅后再合成。
    /// </summary>
    private static Quaternion ClampWristCorrection(Quaternion correction, Vector3 twistAxisWorld)
    {
        if (correction.w < 0f)
        {
            correction = new Quaternion(
                -correction.x, -correction.y, -correction.z, -correction.w);
        }

        Vector3 axis = twistAxisWorld.sqrMagnitude > 0.000001f
            ? twistAxisWorld.normalized
            : Vector3.forward;
        float projected = correction.x * axis.x + correction.y * axis.y +
            correction.z * axis.z;
        Quaternion twist = new Quaternion(
            axis.x * projected, axis.y * projected, axis.z * projected, correction.w);
        float twistSqr = twist.x * twist.x + twist.y * twist.y +
            twist.z * twist.z + twist.w * twist.w;
        if (twistSqr < 0.00000001f)
        {
            twist = Quaternion.identity;
        }
        else
        {
            float inverseLength = 1f / Mathf.Sqrt(twistSqr);
            twist = new Quaternion(
                twist.x * inverseLength, twist.y * inverseLength,
                twist.z * inverseLength, twist.w * inverseLength);
        }

        Quaternion swing = correction * Quaternion.Inverse(twist);
        float swingAngle = Quaternion.Angle(Quaternion.identity, swing);
        if (swingAngle > MaxWristSwingDegrees)
        {
            swing = Quaternion.Slerp(
                Quaternion.identity, swing, MaxWristSwingDegrees / swingAngle);
        }

        float twistAngle = Quaternion.Angle(Quaternion.identity, twist);
        if (twistAngle > MaxWristTwistDegrees)
        {
            twist = Quaternion.Slerp(
                Quaternion.identity, twist, MaxWristTwistDegrees / twistAngle);
        }

        return swing * twist;
    }

    private void CacheBody(Animator animator)
    {
        ReleasePoseHandler();
        _bodyAnimator = animator;
        _upperArm = null;
        _forearm = null;
        _hand = null;
        _handFrame = Quaternion.identity;
        _leftFingersAvailable = false;
        _rightFingersAvailable = false;
        _leftFingerErrorLogged = false;
        _rightFingerErrorLogged = false;
        if (!animator || !animator.isHuman || !animator.avatar)
        {
            return;
        }

        _upperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        _forearm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        _hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        _handFrame = GetHandFrame(_hand,
            animator.GetBoneTransform(HumanBodyBones.LeftMiddleProximal),
            animator.GetBoneTransform(HumanBodyBones.LeftIndexProximal),
            animator.GetBoneTransform(HumanBodyBones.LeftLittleProximal));

        try
        {
            _poseHandler = new HumanPoseHandler(animator.avatar, animator.transform);
        }
        catch (System.Exception exception)
        {
            _poseHandler = null;
            Debug.LogError($"{name} 无法建立 HumanPoseHandler：{exception.Message}", this);
            return;
        }

        EnsureFingerMuscleIndices();
        _leftFingersAvailable = AreFingerMusclesValid(0);
        _rightFingersAvailable = AreFingerMusclesValid(FingerMusclePerHand);
        if (!_leftFingersAvailable)
        {
            _leftFingerErrorLogged = true;
            Debug.LogError(
                $"{name} 的第三人称角色缺少左手 Humanoid 指骨映射，左手程序化握持已禁用。", this);
        }
        if (!_rightFingersAvailable)
        {
            _rightFingerErrorLogged = true;
            Debug.LogError(
                $"{name} 的第三人称角色缺少右手 Humanoid 指骨映射，右手程序化握持已禁用。", this);
        }
    }

    private void ReleasePoseHandler()
    {
        if (_poseHandler != null)
        {
            _poseHandler.Dispose();
            _poseHandler = null;
        }
    }

    private void ApplyFingerPose(WeaponIkData ikData, bool isFiring)
    {
        if (_poseHandler == null || ikData == null)
        {
            return;
        }

        ThirdPersonFingerPoseData fingers = ikData.ThirdPersonFingerPose;
        if (fingers == null ||
            (!fingers.UseLeftGripPose && !fingers.UseRightGripPose))
        {
            return;
        }

        _poseHandler.GetHumanPose(ref _humanPose);
        bool changed = false;
        if (fingers.UseLeftGripPose && _leftFingersAvailable)
        {
            changed |= WriteHandPose(fingers.LeftGripPose, 0);
        }
        if (fingers.UseRightGripPose && _rightFingersAvailable)
        {
            changed |= WriteHandPose(
                isFiring ? fingers.RightFirePose : fingers.RightIdlePose,
                FingerMusclePerHand);
        }
        if (changed)
        {
            _poseHandler.SetHumanPose(ref _humanPose);
        }
    }

    private bool WriteHandPose(HumanoidFingerPoseData pose, int handOffset)
    {
        if (pose == null)
        {
            return false;
        }

        WriteFinger(pose.Thumb, handOffset);
        WriteFinger(pose.Index, handOffset + FingerComponentCount);
        WriteFinger(pose.Middle, handOffset + FingerComponentCount * 2);
        WriteFinger(pose.Ring, handOffset + FingerComponentCount * 3);
        WriteFinger(pose.Little, handOffset + FingerComponentCount * 4);
        return true;
    }

    private void WriteFinger(HumanoidFingerData finger, int muscleOffset)
    {
        _humanPose.muscles[_fingerMuscleIndices[muscleOffset]] = finger.Proximal;
        _humanPose.muscles[_fingerMuscleIndices[muscleOffset + 1]] = finger.Intermediate;
        _humanPose.muscles[_fingerMuscleIndices[muscleOffset + 2]] = finger.Distal;
        _humanPose.muscles[_fingerMuscleIndices[muscleOffset + 3]] = finger.Spread;
    }

    private static bool AreFingerMusclesValid(int handOffset)
    {
        for (int i = 0; i < FingerMusclePerHand; i++)
        {
            if (_fingerMuscleIndices[handOffset + i] < 0)
            {
                return false;
            }
        }
        return true;
    }

    private static void EnsureFingerMuscleIndices()
    {
        if (_fingerMuscleIndices != null)
        {
            return;
        }

        string[] muscleNames = HumanTrait.MuscleName;
        int[] indices = new int[FingerMusclePerHand * 2];
        for (int side = 0; side < 2; side++)
        {
            string sideName = side == 0 ? "Left" : "Right";
            for (int finger = 0; finger < FingerCount; finger++)
            {
                for (int component = 0; component < FingerComponentCount; component++)
                {
                    string muscleName = sideName + " " + FingerNames[finger] + " " +
                        FingerComponents[component];
                    indices[side * FingerMusclePerHand +
                        finger * FingerComponentCount + component] =
                        FindMuscleIndex(muscleNames, muscleName);
                }
            }
        }
        _fingerMuscleIndices = indices;
    }

    private static int FindMuscleIndex(string[] muscleNames, string muscleName)
    {
        for (int i = 0; i < muscleNames.Length; i++)
        {
            if (string.Equals(muscleNames[i], muscleName, System.StringComparison.Ordinal))
            {
                return i;
            }
        }
        return -1;
    }

    private static Quaternion GetHandFrame(Transform hand, Transform middle,
        Transform index, Transform little)
    {
        if (!hand || !middle || !index || !little)
        {
            return Quaternion.identity;
        }
        Vector3 forward = middle.position - hand.position;
        Vector3 up = Vector3.Cross(forward, index.position - little.position);
        if (forward.sqrMagnitude < 0.000001f || up.sqrMagnitude < 0.000000001f)
        {
            return Quaternion.identity;
        }
        return Quaternion.Inverse(hand.rotation) * Quaternion.LookRotation(forward, up);
    }
}
