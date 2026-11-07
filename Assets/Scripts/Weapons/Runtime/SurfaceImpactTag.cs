using UnityEngine;

[DisallowMultipleComponent]
public sealed class SurfaceImpactTag : MonoBehaviour
{
    [Header("命中表面")]
    [SerializeField, InspectorName("材质类型")]
    private ImpactSurfaceType _surfaceType = ImpactSurfaceType.Concrete;

    public ImpactSurfaceType SurfaceType
    {
        get
        {
            return _surfaceType;
        }
    }
}
