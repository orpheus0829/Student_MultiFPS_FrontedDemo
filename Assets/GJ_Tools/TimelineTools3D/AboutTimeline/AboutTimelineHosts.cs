using UnityEngine;

public struct TimelineHitData
{
    public Vector3 boxOffset;
    public float boxRadius;
    public HitBoxShape hitBoxShape;
    public Vector3 hitBoxSize;
    public float sectorAngle;
    public float sectorInnerRadius;
    public float sectorHeight;
    public Vector3 boxEuler;
    public float damage;
    public float hitForce;
}

public interface ITimelineHitHost
{
    void SetHitBox(TimelineHitData data);
    void ClearHitBox();
    void DoHitScan();
}

public interface ITimelineEffectHost
{
    void PlaySound(AudioClip clip);
    GameObject SpawnEffect(GameObject prefab, Vector3 position, Quaternion rotation);
    void RecycleEffect(GameObject effect);
}

public interface IDamageable
{
    void TakeDamage(float amount);
}
