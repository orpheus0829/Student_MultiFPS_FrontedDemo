using UnityEngine;

[DisallowMultipleComponent]
public sealed class BodyHitbox : MonoBehaviour
{
    [Header("所属角色")]
    [SerializeField, InspectorName("生命组件")] private CharacterHealth _characterHealth;

    [Header("命中部位")]
    [SerializeField, InspectorName("身体部位")] private BodyPart _bodyPart = BodyPart.UpperBody;
    [SerializeField, Min(0f), InspectorName("伤害倍率")] private float _damageFactor = 1f;
    [SerializeField, Min(0f), InspectorName("得分倍率")] private float _scoreFactor = 1f;

    public CharacterHealth CharacterHealth
    {
        get
        {
            return _characterHealth;
        }
    }

    public BodyPart BodyPart
    {
        get
        {
            return _bodyPart;
        }
    }

    public float DamageFactor
    {
        get
        {
            return _damageFactor;
        }
    }

    public float ScoreFactor
    {
        get
        {
            return _scoreFactor;
        }
    }

    private void Awake()
    {
        if (!_characterHealth)
        {
            _characterHealth = GetComponentInParent<CharacterHealth>();
        }

        if (!_characterHealth)
        {
            Debug.LogError($"{name} 找不到所属角色的生命组件。", this);
            enabled = false;
        }
    }

    public DamageResult ReceiveHit(DamageRequest request)
    {
        DamageRequest bodyRequest = new DamageRequest(request);
        bodyRequest.Target = _characterHealth;
        bodyRequest.BodyPart = _bodyPart;
        bodyRequest.BodyDamageFactor = _damageFactor;
        bodyRequest.BodyScoreFactor = _scoreFactor;
        return CombatDamage.Apply(bodyRequest);
    }

    private void OnValidate()
    {
        _damageFactor = Mathf.Max(0f, _damageFactor);
        _scoreFactor = Mathf.Max(0f, _scoreFactor);

        if (!_characterHealth)
        {
            _characterHealth = GetComponentInParent<CharacterHealth>();
        }
    }
}
