using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerArmorCtrl : MonoBehaviour
{
    [SerializeField, Min(0f)] private float _armor;
    [SerializeField, Min(1f)] private float _maximumArmor = 100f;
    [SerializeField, Range(0f, 1f)] private float _bodyDamageAbsorption = 0.5f;
    [SerializeField, Range(0f, 1f)] private float _headDamageAbsorption = 0.5f;
    [SerializeField] private bool _hasHelmet;

    public event Action<float, float> ArmorChanged;

    public float Armor { get { return _armor; } }
    public float MaximumArmor { get { return _maximumArmor; } }
    public bool HasHelmet { get { return _hasHelmet; } }

    public void Refill(float amount)
    {
        _armor = Mathf.Clamp(Mathf.Max(_armor, amount), 0f, _maximumArmor);
        ArmorChanged?.Invoke(_armor, _maximumArmor);
    }

    public void EquipHelmet()
    {
        _hasHelmet = true;
        ArmorChanged?.Invoke(_armor, _maximumArmor);
    }

    public void EquipArmor(float capacity, float bodyAbsorption)
    {
        _maximumArmor = Mathf.Max(1f, capacity);
        _bodyDamageAbsorption = Mathf.Clamp01(bodyAbsorption);
        _armor = _maximumArmor;
        ArmorChanged?.Invoke(_armor, _maximumArmor);
    }

    public void EquipHelmet(float headAbsorption)
    {
        _headDamageAbsorption = Mathf.Clamp01(headAbsorption);
        EquipHelmet();
    }

    public void ClearEquipment()
    {
        _armor = 0f;
        _hasHelmet = false;
        ArmorChanged?.Invoke(_armor, _maximumArmor);
    }

    public float Mitigate(float incomingDamage, BodyPart bodyPart)
    {
        incomingDamage = Mathf.Max(0f, incomingDamage);
        bool isHead = bodyPart == BodyPart.Head;
        if (isHead && !_hasHelmet)
        {
            return incomingDamage;
        }
        if (_armor <= 0f || incomingDamage <= 0f)
        {
            return incomingDamage;
        }

        float absorption = isHead
            ? _headDamageAbsorption
            : _bodyDamageAbsorption;
        float absorbed = Mathf.Min(_armor, incomingDamage * absorption);
        _armor -= absorbed;
        ArmorChanged?.Invoke(_armor, _maximumArmor);
        return incomingDamage - absorbed;
    }
}
