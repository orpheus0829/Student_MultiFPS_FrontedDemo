using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerRagdollCtrl : MonoBehaviour
{
    private static readonly List<PlayerRagdollCtrl> ActiveRagdolls = new List<PlayerRagdollCtrl>();

    [Header("布娃娃骨骼")]
    [SerializeField, InspectorName("第三人称 Animator")] private Animator _bodyAnimator;
    [SerializeField, Min(0f), InspectorName("致命一击冲量")] private float _hitImpulse = 21f;

    private readonly List<Rigidbody> _bodies = new List<Rigidbody>();
    private readonly List<Collider> _colliders = new List<Collider>();
    private readonly List<CharacterJoint> _joints = new List<CharacterJoint>();
    private bool _isActive;

    public Vector3 FocusPoint
    {
        get
        {
            Rigidbody hips = _bodies.Count > 0 ? _bodies[0] : null;
            return hips ? hips.position + Vector3.up * 0.5f : transform.position + Vector3.up;
        }
    }

    private void Awake()
    {
        BuildRagdoll();
    }

    public void SetBodyAnimator(Animator bodyAnimator)
    {
        if (_bodyAnimator == bodyAnimator)
        {
            return;
        }

        if (_isActive)
        {
            StandUp();
        }

        ClearRagdoll();
        _bodyAnimator = bodyAnimator;
        BuildRagdoll();
        Collider[] ownColliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < ActiveRagdolls.Count; i++)
        {
            ActiveRagdolls[i].IgnoreCharacterColliders(ownColliders);
        }
    }

    private void BuildRagdoll()
    {
        if (!_bodyAnimator || !_bodyAnimator.isHuman)
        {
            Debug.LogWarning($"{name} 没有可用的人形骨骼，布娃娃未启用。", this);
            return;
        }

        AddBone(HumanBodyBones.Hips, HumanBodyBones.Spine, null, 0.17f, 2.2f);
        Rigidbody hips = _bodies.Count > 0 ? _bodies[0] : null;
        AddBone(HumanBodyBones.Spine, HumanBodyBones.Chest, hips, 0.16f, 1.5f);
        Rigidbody spine = FindBody(HumanBodyBones.Spine);
        AddBone(HumanBodyBones.Chest, HumanBodyBones.Neck, spine, 0.16f, 1.5f);
        Rigidbody chest = FindBody(HumanBodyBones.Chest);
        AddBone(HumanBodyBones.Head, HumanBodyBones.Head, chest, 0.14f, 0.7f);
        AddBone(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, chest, 0.085f, 0.55f);
        AddBone(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
            FindBody(HumanBodyBones.LeftUpperArm), 0.07f, 0.4f);
        AddBone(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, chest, 0.085f, 0.55f);
        AddBone(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
            FindBody(HumanBodyBones.RightUpperArm), 0.07f, 0.4f);
        AddBone(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, hips, 0.11f, 1f);
        AddBone(HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
            FindBody(HumanBodyBones.LeftUpperLeg), 0.09f, 0.75f);
        AddBone(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, hips, 0.11f, 1f);
        AddBone(HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot,
            FindBody(HumanBodyBones.RightUpperLeg), 0.09f, 0.75f);
    }

    private void ClearRagdoll()
    {
        for (int i = 0; i < _colliders.Count; i++)
        {
            if (_colliders[i])
            {
                _colliders[i].enabled = false;
                Destroy(_colliders[i].gameObject);
            }
        }
        for (int i = 0; i < _joints.Count; i++)
        {
            if (_joints[i])
            {
                Destroy(_joints[i]);
            }
        }
        for (int i = 0; i < _bodies.Count; i++)
        {
            if (_bodies[i])
            {
                _bodies[i].isKinematic = true;
                Destroy(_bodies[i]);
            }
        }
        _colliders.Clear();
        _joints.Clear();
        _bodies.Clear();
    }

    private void Start()
    {
        Collider[] ownColliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < ActiveRagdolls.Count; i++)
        {
            ActiveRagdolls[i].IgnoreCharacterColliders(ownColliders);
        }
    }

    private void OnDestroy()
    {
        ActiveRagdolls.Remove(this);
    }

    public void Fall(Vector3 hitPoint, Vector3 hitDirection, float appliedDamage)
    {
        if (_isActive || _bodies.Count == 0)
        {
            return;
        }

        _isActive = true;
        _bodyAnimator.enabled = false;
        for (int i = 0; i < _bodies.Count; i++)
        {
            _bodies[i].isKinematic = false;
            _bodies[i].useGravity = true;
            _colliders[i].enabled = true;
        }
        IgnoreSelfCollisions();
        CharacterHealth[] characters = FindObjectsByType<CharacterHealth>(FindObjectsSortMode.None);
        for (int i = 0; i < characters.Length; i++)
        {
            if (characters[i].transform.IsChildOf(transform))
            {
                continue;
            }
            IgnoreCharacterColliders(characters[i].GetComponentsInChildren<Collider>(true));
        }
        ActiveRagdolls.Add(this);

        Rigidbody closest = _bodies[0];
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < _bodies.Count; i++)
        {
            float distance = (_bodies[i].worldCenterOfMass - hitPoint).sqrMagnitude;
            if (distance < bestDistance)
            {
                closest = _bodies[i];
                bestDistance = distance;
            }
        }

        Vector3 direction = hitDirection.sqrMagnitude > 0.001f
            ? hitDirection.normalized
            : transform.forward;
        float impulse = _hitImpulse * Mathf.Clamp(appliedDamage / 35f, 0.45f, 1.5f);
        Vector3 forcePoint = (hitPoint - closest.worldCenterOfMass).sqrMagnitude <= 0.49f
            ? hitPoint
            : closest.worldCenterOfMass;
        closest.AddForceAtPosition(direction * impulse, forcePoint, ForceMode.Impulse);
    }

    public void StandUp()
    {
        if (!_isActive)
        {
            return;
        }

        for (int i = 0; i < _bodies.Count; i++)
        {
            _bodies[i].linearVelocity = Vector3.zero;
            _bodies[i].angularVelocity = Vector3.zero;
            _bodies[i].isKinematic = true;
            _bodies[i].useGravity = false;
            _colliders[i].enabled = false;
        }
        _bodyAnimator.enabled = true;
        _bodyAnimator.Rebind();
        _bodyAnimator.Update(0f);
        _isActive = false;
        ActiveRagdolls.Remove(this);
    }

    private void IgnoreSelfCollisions()
    {
        for (int i = 0; i < _colliders.Count; i++)
        {
            for (int j = i + 1; j < _colliders.Count; j++)
            {
                Physics.IgnoreCollision(_colliders[i], _colliders[j]);
            }
        }
    }

    private void IgnoreCharacterColliders(Collider[] characterColliders)
    {
        for (int i = 0; i < _colliders.Count; i++)
        {
            for (int j = 0; j < characterColliders.Length; j++)
            {
                if (characterColliders[j] &&
                    !characterColliders[j].transform.IsChildOf(transform))
                {
                    Physics.IgnoreCollision(_colliders[i], characterColliders[j]);
                }
            }
        }
    }

    private void AddBone(
        HumanBodyBones boneId,
        HumanBodyBones nextId,
        Rigidbody connectedBody,
        float radius,
        float mass)
    {
        Transform bone = _bodyAnimator.GetBoneTransform(boneId);
        if (!bone)
        {
            return;
        }

        Rigidbody body = bone.gameObject.AddComponent<Rigidbody>();
        body.mass = mass;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.linearDamping = 0.5f;
        body.angularDamping = 3f;
        body.solverIterations = 12;
        body.isKinematic = true;
        body.useGravity = false;

        GameObject shape = new GameObject("布娃娃碰撞体");
        shape.transform.SetParent(bone, false);
        Vector3 boneScale = bone.lossyScale;
        shape.transform.localScale = new Vector3(
            1f / Mathf.Max(0.0001f, Mathf.Abs(boneScale.x)),
            1f / Mathf.Max(0.0001f, Mathf.Abs(boneScale.y)),
            1f / Mathf.Max(0.0001f, Mathf.Abs(boneScale.z)));
        Collider collider;
        Transform next = _bodyAnimator.GetBoneTransform(nextId);
        Vector3 end = next && next != bone
            ? Quaternion.Inverse(bone.rotation) * (next.position - bone.position)
            : Vector3.up * radius * 2f;
        if (boneId == HumanBodyBones.Head || end.magnitude < radius * 2f)
        {
            SphereCollider sphere = shape.AddComponent<SphereCollider>();
            sphere.radius = radius;
            collider = sphere;
        }
        else
        {
            shape.transform.position = bone.position + bone.rotation * (end * 0.5f);
            shape.transform.localRotation = Quaternion.FromToRotation(Vector3.up, end.normalized);
            CapsuleCollider capsule = shape.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.radius = radius;
            capsule.height = Mathf.Max(radius * 2f, end.magnitude + radius);
            collider = capsule;
        }
        collider.enabled = false;
        _bodies.Add(body);
        _colliders.Add(collider);

        if (connectedBody)
        {
            CharacterJoint joint = bone.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = connectedBody;
            joint.enablePreprocessing = false;
            joint.lowTwistLimit = new SoftJointLimit { limit = -20f };
            joint.highTwistLimit = new SoftJointLimit { limit = 20f };
            joint.swing1Limit = new SoftJointLimit { limit = 35f };
            joint.swing2Limit = new SoftJointLimit { limit = 35f };
            _joints.Add(joint);
        }
    }

    private Rigidbody FindBody(HumanBodyBones boneId)
    {
        Transform bone = _bodyAnimator.GetBoneTransform(boneId);
        return bone ? bone.GetComponent<Rigidbody>() : null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        ActiveRagdolls.Clear();
    }

}
