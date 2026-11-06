using UnityEngine;

public enum BodyPart
{
    [InspectorName("未指定")] None = 0,
    [InspectorName("头部")] Head = 1,
    [InspectorName("上半身")] UpperBody = 2,
    [InspectorName("下半身")] LowerBody = 3
}
