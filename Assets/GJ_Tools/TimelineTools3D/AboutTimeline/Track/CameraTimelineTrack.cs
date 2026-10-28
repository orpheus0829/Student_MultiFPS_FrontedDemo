using UnityEngine;
using UnityEngine.Timeline;

[TrackBindingType(typeof(TimelineCamRig))]
[TrackClipType(typeof(CameraTimelineClip))]
public class CameraTimelineTrack : TrackAsset
{
}
