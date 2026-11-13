using UnityEngine;

[DisallowMultipleComponent]
public sealed class WeaponAnimationEventRelay : MonoBehaviour
{
    private WeaponFeedbackCtrl _feedbackCtrl;

    private void Awake()
    {
        _feedbackCtrl = GetComponentInParent<WeaponFeedbackCtrl>();
    }

    public void PlayEquipSound()
    {
        WeaponFeedbackCtrl feedbackCtrl = GetFeedbackCtrl();
        if (feedbackCtrl)
        {
            feedbackCtrl.PlayEquipSound();
        }
    }

    public void PlayUnEquipSound()
    {
        WeaponFeedbackCtrl feedbackCtrl = GetFeedbackCtrl();
        if (feedbackCtrl)
        {
            feedbackCtrl.PlayUnEquipSound();
        }
    }

    public void PlayDryFireSound()
    {
        WeaponFeedbackCtrl feedbackCtrl = GetFeedbackCtrl();
        if (feedbackCtrl)
        {
            feedbackCtrl.PlayDryFireSound();
        }
    }

    public void PlayWeaponSound(int clipIndex)
    {
        WeaponFeedbackCtrl feedbackCtrl = GetFeedbackCtrl();
        if (feedbackCtrl)
        {
            feedbackCtrl.PlayWeaponSound(clipIndex);
        }
    }

    public void PlayReloadSound(int clipIndex)
    {
        WeaponFeedbackCtrl feedbackCtrl = GetFeedbackCtrl();
        if (feedbackCtrl)
        {
            feedbackCtrl.PlayReloadSound(clipIndex);
        }
    }

    public void PlayMechanicalSound(int clipIndex)
    {
        WeaponFeedbackCtrl feedbackCtrl = GetFeedbackCtrl();
        if (feedbackCtrl)
        {
            feedbackCtrl.PlayMechanicalSound(clipIndex);
        }
    }

    private WeaponFeedbackCtrl GetFeedbackCtrl()
    {
        if (!_feedbackCtrl)
        {
            _feedbackCtrl = GetComponentInParent<WeaponFeedbackCtrl>();
        }

        return _feedbackCtrl;
    }
}
