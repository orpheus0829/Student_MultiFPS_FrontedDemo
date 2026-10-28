using UnityEngine;
using UnityEngine.Timeline;

public enum HitBoxShape
{
    [InspectorName("球")]
    Sphere,
    [InspectorName("盒")]
    Box,
    [InspectorName("扇形")]
    Sector,
}

[TrackBindingType(typeof(Transform))]
[TrackClipType(typeof(HitBoxClip))]
public class HitBoxTrack : TrackAsset
{
}
