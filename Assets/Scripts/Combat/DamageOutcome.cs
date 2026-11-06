using UnityEngine;

public enum DamageOutcome
{
    [InspectorName("未受理")] Ignored = 0,
    [InspectorName("命中")] Hit = 1,
    [InspectorName("击杀")] Kill = 2
}
