using UnityEngine;

public enum WordNoteRegion
{
    [InspectorName("屏幕底部")]
    Bottom = 0,

    [InspectorName("屏幕右上角")]
    TopRight = 1,

    [InspectorName("屏幕中央")]
    Center = 2,

    [InspectorName("屏幕顶部中央")]
    TopCenter = 3,

    [InspectorName("屏幕右下角")]
    BottomRight = 4,

    [InspectorName("屏幕底部中央")]
    BottomCenter = 5,

    [InspectorName("自定义区域")]
    Custom = 6
}
