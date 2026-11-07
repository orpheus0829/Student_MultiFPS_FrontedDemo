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

    public void PlayWeaponSound(int clipIndex)
    {
        WeaponFeedbackCtrl feedbackCtrl = GetFeedbackCtrl();
        if (feedbackCtrl)
        {
            feedbackCtrl.PlayWeaponSound(clipIndex);
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
