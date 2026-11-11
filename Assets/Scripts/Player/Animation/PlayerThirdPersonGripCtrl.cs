using System.Collections.Generic;
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

    private static readonly HumanBodyBones[] FingerBones =
    {
        HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftThumbIntermediate,
        HumanBodyBones.LeftThumbDistal, HumanBodyBones.LeftIndexProximal,
        HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal,
        HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate,
        HumanBodyBones.LeftMiddleDistal, HumanBodyBones.LeftRingProximal,
        HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal,
        HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate,
        HumanBodyBones.LeftLittleDistal, HumanBodyBones.RightThumbProximal,
        HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal,
        HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate,
        HumanBodyBones.RightIndexDistal, HumanBodyBones.RightMiddleProximal,
        HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal,
        HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate,
        HumanBodyBones.RightRingDistal, HumanBodyBones.RightLittleProximal,
        HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal
    };

    private static readonly float[] MaxFingerJointAngles = { 45f, 55f, 40f };

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
    private Transform _rightHand;
    private Quaternion _handFrame;
    private HumanPoseHandler _poseHandler;
    private HumanPose _humanPose = new HumanPose();
    private readonly Transform[] _fingerBones = new Transform[30];
    private readonly Transform[] _poseFingerBones = new Transform[30];
    private readonly Quaternion[] _fingerNeutralLocalRotations = new Quaternion[30];
    private readonly Quaternion[] _fingerLocalRotations = new Quaternion[30];
    private readonly Quaternion[] _fingerFireLocalRotations = new Quaternion[30];
    private readonly float[] _savedFingerMuscles = new float[FingerMusclePerHand * 2];
    private readonly List<Transform> _rightGripChain = new List<Transform>();
    private readonly List<Quaternion> _rightGripNeutralLocalRotations =
        new List<Quaternion>();
    private GameObject _poseSamplerRoot;
    private ThirdPersonFingerPoseData _cachedFingerPoseData;
    private bool _hasCachedFingerRotations;
    private bool _leftFingersAvailable;
    private bool _rightFingersAvailable;
    private bool _leftFingerErrorLogged;
    private bool _rightFingerErrorLogged;
    private bool _resumeFromAnimationTakeover;
    private bool _needsNeutralGripCalibration;
    private bool _hasRightHandNeutralRotation;
    private int _firePoseThroughFrame = -1;
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

    private void OnEnable()
    {
        if (_action)
        {
            _action.ActionStarted += HandleActionStarted;
            _action.ActionCanceled += HandleActionCanceled;
        }
    }

    private void OnDisable()
    {
        if (_action)
        {
            _action.ActionStarted -= HandleActionStarted;
            _action.ActionCanceled -= HandleActionCanceled;
        }
        _firePoseThroughFrame = -1;
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
            _needsNeutralGripCalibration = true;
        }
        if (!_hasGripTarget)
        {
            return;
        }

        // 握持只补基础姿势；装弹、收枪、检视需要自由活动左手，不能被 IK 拉回护木。
        if (_action.CurrentAction != null &&
            _action.CurrentAction.ActionType != WeaponActionType.Fire)
        {
            _resumeFromAnimationTakeover = true;
            return;
        }

        if (_resumeFromAnimationTakeover || _needsNeutralGripCalibration)
        {
            // 装弹、收枪、检视等动作会完全接管左臂，动作结束时先清掉残留的
            // Humanoid 求值并回到 Idle 第 0 帧，再恢复程序握持。开火不会走这里。
            _animationCtrl.ThirdPersonView.RestoreNeutralGripPose();
            CacheRightHandNeutralRotation();
            _resumeFromAnimationTakeover = false;
            _needsNeutralGripCalibration = false;
        }

        StabilizeRightHandGrip();

        // 锚点挂在随枪运动的节点下，武器每帧都在动，因此由缓存的武器局部值
        // 重新算出本帧世界目标；这里不重新 Find，查找只在切枪/换锚点数据时发生。
        bool usesExplicitAnchor = _gripSource == ThirdPersonGripTargetSource.ExplicitAnchor;
        _gripTarget = new ThirdPersonGripTarget(
            targetGun.TransformPoint(_gripLocalPosition),
            targetGun.rotation * _gripLocalRotation,
            usesExplicitAnchor,
            _gripSource);

        bool isFiring = (_action.CurrentAction != null &&
            _action.CurrentAction.ActionType == WeaponActionType.Fire) ||
            Time.frameCount <= _firePoseThroughFrame;
        // 顺序固定：Animator 已求值 → 应用缓存的指骨局部旋转 → 解左臂 IK。
        ApplyFingerPose(ikData, isFiring);
        SolveLeftArm(usesExplicitAnchor);
    }

    private void HandleActionStarted(
        WeaponActionDefinition action, bool startedWhileAiming)
    {
        if (action != null && action.ActionType == WeaponActionType.Fire)
        {
            // 极短 Fire 可能在下一次 Update 就完成；至少把开火指姿保留到下一
            // 渲染帧，保证动画首帧和枪口事件使用的是扣扳机姿势。
            _firePoseThroughFrame = Mathf.Max(_firePoseThroughFrame, Time.frameCount + 1);
        }
    }

    private void HandleActionCanceled(
        WeaponActionType actionType, WeaponActionCancelReason reason)
    {
        if (actionType == WeaponActionType.Fire)
        {
            _firePoseThroughFrame = -1;
        }
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
        _rightHand = null;
        _handFrame = Quaternion.identity;
        _hasRightHandNeutralRotation = false;
        _rightGripChain.Clear();
        _rightGripNeutralLocalRotations.Clear();
        _leftFingersAvailable = false;
        _rightFingersAvailable = false;
        _leftFingerErrorLogged = false;
        _rightFingerErrorLogged = false;
        _cachedFingerPoseData = null;
        _hasCachedFingerRotations = false;
        _resumeFromAnimationTakeover = false;
        _needsNeutralGripCalibration = true;
        if (!animator || !animator.isHuman || !animator.avatar)
        {
            return;
        }

        _upperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        _forearm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        _hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        _rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        Transform rightGripStop = animator.GetBoneTransform(HumanBodyBones.UpperChest);
        if (!rightGripStop)
        {
            rightGripStop = animator.GetBoneTransform(HumanBodyBones.Chest);
        }
        Transform rightGripBone = _rightHand;
        while (rightGripBone && rightGripBone != rightGripStop)
        {
            _rightGripChain.Add(rightGripBone);
            rightGripBone = rightGripBone.parent;
        }
        _handFrame = GetHandFrame(_hand,
            animator.GetBoneTransform(HumanBodyBones.LeftMiddleProximal),
            animator.GetBoneTransform(HumanBodyBones.LeftIndexProximal),
            animator.GetBoneTransform(HumanBodyBones.LeftLittleProximal));

        EnsureFingerMuscleIndices();
        _leftFingersAvailable = AreFingerMusclesValid(0);
        _rightFingersAvailable = AreFingerMusclesValid(FingerMusclePerHand);
        for (int i = 0; i < FingerBones.Length; i++)
        {
            _fingerBones[i] = animator.GetBoneTransform(FingerBones[i]);
        }

        try
        {
            Dictionary<Transform, Transform> poseBoneMap =
                CreatePoseSampler(animator, out _poseSamplerRoot);
            _poseHandler = new HumanPoseHandler(
                animator.avatar, _poseSamplerRoot.transform);
            for (int i = 0; i < _poseFingerBones.Length; i++)
            {
                Transform sourceBone = _fingerBones[i];
                _poseFingerBones[i] = sourceBone && poseBoneMap.TryGetValue(
                    sourceBone, out Transform poseBone) ? poseBone : null;
            }
            CacheNeutralFingerRotations();
        }
        catch (System.Exception exception)
        {
            _poseHandler = null;
            if (_poseSamplerRoot)
            {
                Destroy(_poseSamplerRoot);
                _poseSamplerRoot = null;
            }
            Debug.LogError($"{name} 无法建立 HumanPoseHandler：{exception.Message}", this);
            return;
        }
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
        if (_poseSamplerRoot)
        {
            Destroy(_poseSamplerRoot);
            _poseSamplerRoot = null;
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

        bool rebuildRotations = !_hasCachedFingerRotations ||
            _cachedFingerPoseData != fingers;
        if (rebuildRotations)
        {
            _poseHandler.GetHumanPose(ref _humanPose);
            for (int i = 0; i < _savedFingerMuscles.Length; i++)
            {
                int muscleIndex = _fingerMuscleIndices[i];
                if (muscleIndex >= 0)
                {
                    _savedFingerMuscles[i] = _humanPose.muscles[muscleIndex];
                }
            }
            bool changed = false;
            if (fingers.UseLeftGripPose && _leftFingersAvailable)
            {
                changed |= WriteHandPose(fingers.LeftGripPose, 0);
            }
            if (fingers.UseRightGripPose && _rightFingersAvailable)
            {
                changed |= WriteHandPose(fingers.RightIdlePose, FingerMusclePerHand);
            }
            if (!changed)
            {
                return;
            }

            _poseHandler.SetHumanPose(ref _humanPose);
            for (int i = 0; i < _poseFingerBones.Length; i++)
            {
                if (_poseFingerBones[i])
                {
                    _fingerLocalRotations[i] = _poseFingerBones[i].localRotation;
                }
            }

            for (int i = 0; i < _savedFingerMuscles.Length; i++)
            {
                int muscleIndex = _fingerMuscleIndices[i];
                if (muscleIndex >= 0)
                {
                    _humanPose.muscles[muscleIndex] = _savedFingerMuscles[i];
                }
            }
            _poseHandler.SetHumanPose(ref _humanPose);

            if (fingers.UseLeftGripPose && _leftFingersAvailable)
            {
                WriteHandPose(fingers.LeftGripPose, 0);
            }
            if (fingers.UseRightGripPose && _rightFingersAvailable)
            {
                WriteHandPose(fingers.RightFirePose, FingerMusclePerHand);
            }
            _poseHandler.SetHumanPose(ref _humanPose);
            for (int i = 0; i < _poseFingerBones.Length; i++)
            {
                if (_poseFingerBones[i])
                {
                    _fingerFireLocalRotations[i] = _poseFingerBones[i].localRotation;
                }
            }

            ConstrainFingerRotations(_fingerLocalRotations);
            ConstrainFingerRotations(_fingerFireLocalRotations);

            // HumanPoseHandler 只操作隐藏的人形采样骨架，真实角色 Animator 从不
            // 接收 SetHumanPose，因此不会把临时求指姿的完整人体姿势带进下一帧。
            _cachedFingerPoseData = fingers;
            _hasCachedFingerRotations = true;
        }

        // 第三人称连续射击时不再让右手每发在 Idle/Fire 间硬切。扣扳机的
        // 微小精确度不值得后握把附近出现高频指骨残影；保持自然待机握姿。
        Quaternion[] rotations = _fingerLocalRotations;
        int firstBone = fingers.UseLeftGripPose ? 0 : 15;
        int lastBone = fingers.UseRightGripPose ? _fingerBones.Length : 15;
        for (int i = firstBone; i < lastBone; i++)
        {
            if (_fingerBones[i])
            {
                _fingerBones[i].localRotation = rotations[i];
            }
        }
    }

    private void CacheRightHandNeutralRotation()
    {
        if (_rightGripChain.Count == 0)
        {
            _hasRightHandNeutralRotation = false;
            return;
        }

        // RestoreNeutralGripPose 已把 Animator 放到 Idle 第 0 帧；缓存整条持枪臂
        // 的自然局部角度，不改骨长、缩放或关节位置。
        _rightGripNeutralLocalRotations.Clear();
        for (int i = 0; i < _rightGripChain.Count; i++)
        {
            _rightGripNeutralLocalRotations.Add(_rightGripChain[i].localRotation);
        }
        _hasRightHandNeutralRotation = true;
    }

    private void StabilizeRightHandGrip()
    {
        if (!_hasRightHandNeutralRotation ||
            _rightGripChain.Count != _rightGripNeutralLocalRotations.Count)
        {
            return;
        }

        // 商店 Idle 动画的持枪臂循环接缝可单帧跳十几度，而枪根正挂在右腕下。
        // 普通持枪/开火期间锁定整条右臂局部角；装弹、切枪、检视会在上方提前
        // return，因而仍由动作动画完整接管，结束后再从 Idle 首帧重新校准。
        for (int i = 0; i < _rightGripChain.Count; i++)
        {
            if (_rightGripChain[i])
            {
                _rightGripChain[i].localRotation = _rightGripNeutralLocalRotations[i];
            }
        }
    }

    private void CacheNeutralFingerRotations()
    {
        _poseHandler.GetHumanPose(ref _humanPose);
        for (int i = 0; i < _savedFingerMuscles.Length; i++)
        {
            int muscleIndex = _fingerMuscleIndices[i];
            if (muscleIndex >= 0)
            {
                _humanPose.muscles[muscleIndex] = 0f;
            }
        }
        _poseHandler.SetHumanPose(ref _humanPose);
        for (int i = 0; i < _poseFingerBones.Length; i++)
        {
            if (_poseFingerBones[i])
            {
                _fingerNeutralLocalRotations[i] = _poseFingerBones[i].localRotation;
            }
        }
    }

    private void ConstrainFingerRotations(Quaternion[] rotations)
    {
        for (int i = 0; i < rotations.Length; i++)
        {
            if (!_poseFingerBones[i])
            {
                continue;
            }
            int jointIndex = (i % 15) % 3;
            rotations[i] = Quaternion.RotateTowards(
                _fingerNeutralLocalRotations[i],
                rotations[i],
                MaxFingerJointAngles[jointIndex]);
        }
    }

    private static Dictionary<Transform, Transform> CreatePoseSampler(
        Animator animator, out GameObject samplerRoot)
    {
        HashSet<Transform> required = new HashSet<Transform>();
        required.Add(animator.transform);
        HashSet<string> avatarBoneNames = new HashSet<string>();
        SkeletonBone[] avatarSkeleton = animator.avatar.humanDescription.skeleton;
        for (int i = 0; i < avatarSkeleton.Length; i++)
        {
            avatarBoneNames.Add(avatarSkeleton[i].name);
        }
        Transform[] hierarchy = animator.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < hierarchy.Length; i++)
        {
            if (!avatarBoneNames.Contains(hierarchy[i].name))
            {
                continue;
            }
            Transform bone = hierarchy[i];
            while (bone && !required.Contains(bone))
            {
                required.Add(bone);
                if (bone == animator.transform)
                {
                    break;
                }
                bone = bone.parent;
            }
        }
        for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
        {
            Transform bone = animator.GetBoneTransform((HumanBodyBones)i);
            while (bone && !required.Contains(bone))
            {
                required.Add(bone);
                if (bone == animator.transform)
                {
                    break;
                }
                bone = bone.parent;
            }
        }

        Dictionary<Transform, Transform> map =
            new Dictionary<Transform, Transform>(required.Count);
        Transform root = ClonePoseHierarchy(animator.transform, null, required, map);
        samplerRoot = root.gameObject;
        samplerRoot.name = animator.name + "_FingerPoseSampler";
        samplerRoot.SetActive(false);
        return map;
    }

    private static Transform ClonePoseHierarchy(
        Transform source,
        Transform parent,
        HashSet<Transform> required,
        Dictionary<Transform, Transform> map)
    {
        GameObject cloneObject = new GameObject(source.name);
        cloneObject.hideFlags = HideFlags.HideAndDontSave;
        Transform clone = cloneObject.transform;
        if (parent)
        {
            clone.SetParent(parent, false);
        }
        clone.localPosition = source.localPosition;
        clone.localRotation = source.localRotation;
        clone.localScale = source.localScale;
        map.Add(source, clone);

        for (int i = 0; i < source.childCount; i++)
        {
            Transform child = source.GetChild(i);
            if (required.Contains(child))
            {
                ClonePoseHierarchy(child, clone, required, map);
            }
        }
        return clone;
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
