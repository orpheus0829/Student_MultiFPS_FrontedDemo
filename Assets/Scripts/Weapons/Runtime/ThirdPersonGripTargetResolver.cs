using UnityEngine;

public enum ThirdPersonGripTargetSource
{
    None = 0,
    ExplicitAnchor = 1,
    LegacyFirstPersonIdle = 2
}

public readonly struct ThirdPersonGripTarget
{
    public Vector3 Position { get; }
    public Quaternion Rotation { get; }
    public bool HasRotation { get; }
    public ThirdPersonGripTargetSource Source { get; }

    public ThirdPersonGripTarget(
        Vector3 position,
        Quaternion rotation,
        bool hasRotation,
        ThirdPersonGripTargetSource source)
    {
        Position = position;
        Rotation = rotation;
        HasRotation = hasRotation;
        Source = source;
    }
}

public static class ThirdPersonGripTargetResolver
{
    public static bool TryResolve(
        WeaponIkData ikData,
        Transform sourceWeapon,
        Transform targetWeapon,
        Transform sourceHand,
        out ThirdPersonGripTarget target)
    {
        target = default;
        if (ikData == null || !targetWeapon)
        {
            return false;
        }

        string path = ikData.LeftHandIkPointPath;
        if (!string.IsNullOrWhiteSpace(path))
        {
            // 显式锚点只查一次；找不到就是配置错误，绝不静默回退到旧手骨位置。
            Transform anchor = targetWeapon.Find(path);
            if (!anchor)
            {
                return false;
            }

            target = new ThirdPersonGripTarget(
                anchor.position,
                anchor.rotation,
                true,
                ThirdPersonGripTargetSource.ExplicitAnchor);
            return true;
        }

        if (!sourceWeapon || !sourceHand)
        {
            return false;
        }

        // 旧回退只搬运第一人称 Idle 第 0 帧手骨位置，腕朝向继续交给动画。
        Vector3 localPosition = sourceWeapon.InverseTransformPoint(sourceHand.position);
        target = new ThirdPersonGripTarget(
            targetWeapon.TransformPoint(localPosition),
            Quaternion.identity,
            false,
            ThirdPersonGripTargetSource.LegacyFirstPersonIdle);
        return true;
    }
}
