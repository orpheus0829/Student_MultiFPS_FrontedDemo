using UnityEngine;

[CreateAssetMenu(fileName = "Weapon", menuName = "Gun Demo/枪械/枪械数据")]
public sealed class WeaponDefinitionSO : ScriptableObject
{
    [SerializeField] private WeaponBasicData _basic = new WeaponBasicData();
    [SerializeField] private WeaponFireData _fire = new WeaponFireData();
    [SerializeField] private WeaponDamageData _damage = new WeaponDamageData();
    [SerializeField] private WeaponAmmoData _ammo = new WeaponAmmoData();
    [SerializeField] private WeaponAccuracyData _accuracy = new WeaponAccuracyData();
    [SerializeField] private WeaponGameplayRecoilData _gameplayRecoil = new WeaponGameplayRecoilData();
    [SerializeField] private WeaponHandlingData _handling = new WeaponHandlingData();
    [SerializeField] private WeaponResourceData _resources = new WeaponResourceData();
    [SerializeField] private WeaponAnimationData _animation = new WeaponAnimationData();
    [SerializeField] private WeaponIkData _ik = new WeaponIkData();
    [SerializeField] private WeaponAudioData _audio = new WeaponAudioData();
    [SerializeField] private WeaponEffectData _effects = new WeaponEffectData();
    [SerializeField] private WeaponCameraFeedbackData _cameraFeedback = new WeaponCameraFeedbackData();
    [SerializeField] private WeaponOriginalRecoilData _originalRecoil = new WeaponOriginalRecoilData();
    [SerializeField] private WeaponSpecialData _special = new WeaponSpecialData();

    public WeaponBasicData Basic { get { return _basic; } }
    public WeaponFireData Fire { get { return _fire; } }
    public WeaponDamageData Damage { get { return _damage; } }
    public WeaponAmmoData Ammo { get { return _ammo; } }
    public WeaponAccuracyData Accuracy { get { return _accuracy; } }
    public WeaponGameplayRecoilData GameplayRecoil { get { return _gameplayRecoil; } }
    public WeaponHandlingData Handling { get { return _handling; } }
    public WeaponResourceData Resources { get { return _resources; } }
    public WeaponAnimationData Animation { get { return _animation; } }
    public WeaponIkData Ik { get { return _ik; } }
    public WeaponAudioData Audio { get { return _audio; } }
    public WeaponEffectData Effects { get { return _effects; } }
    public WeaponCameraFeedbackData CameraFeedback { get { return _cameraFeedback; } }
    public WeaponOriginalRecoilData OriginalRecoil { get { return _originalRecoil; } }
    public WeaponSpecialData Special { get { return _special; } }

    private void OnValidate()
    {
        if (_basic == null)
        {
            _basic = new WeaponBasicData();
        }

        if (_fire == null)
        {
            _fire = new WeaponFireData();
        }

        if (_damage == null)
        {
            _damage = new WeaponDamageData();
        }

        if (_ammo == null)
        {
            _ammo = new WeaponAmmoData();
        }

        if (_accuracy == null)
        {
            _accuracy = new WeaponAccuracyData();
        }

        if (_gameplayRecoil == null)
        {
            _gameplayRecoil = new WeaponGameplayRecoilData();
        }

        if (_handling == null)
        {
            _handling = new WeaponHandlingData();
        }

        if (_resources == null)
        {
            _resources = new WeaponResourceData();
        }

        if (_animation == null)
        {
            _animation = new WeaponAnimationData();
        }

        if (_ik == null)
        {
            _ik = new WeaponIkData();
        }

        if (_audio == null)
        {
            _audio = new WeaponAudioData();
        }

        if (_effects == null)
        {
            _effects = new WeaponEffectData();
        }

        if (_cameraFeedback == null)
        {
            _cameraFeedback = new WeaponCameraFeedbackData();
        }

        if (_originalRecoil == null)
        {
            _originalRecoil = new WeaponOriginalRecoilData();
        }

        if (_special == null)
        {
            _special = new WeaponSpecialData();
        }
    }
}
