using System;
using UnityEngine;

public static class WeaponAnimationClipResolver
{
    private const string FirstPersonIdleSlot = "A_FP_AKX_Idle";

    public static AnimationClip GetFirstPersonIdle(
        RuntimeAnimatorController controller)
    {
        if (!controller)
        {
            return null;
        }

        AnimatorOverrideController overrideController =
            controller as AnimatorOverrideController;
        if (overrideController)
        {
            AnimationClip overriddenIdle = overrideController[FirstPersonIdleSlot];
            if (overriddenIdle)
            {
                return overriddenIdle;
            }
        }

        AnimationClip[] clips = controller.animationClips;
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip && clip.name.IndexOf(
                "Idle", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return clip;
            }
        }

        return null;
    }
}
