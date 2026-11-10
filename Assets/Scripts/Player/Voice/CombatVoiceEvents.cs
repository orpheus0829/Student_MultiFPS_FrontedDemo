using System;

public static class CombatVoiceEvents
{
    // 已按本地听者距离筛选；由 UI 自行决定同屏字幕的排队表现。
    public static event Action<PlayerInfo, string, float, bool> LineStarted;

    public static void RaiseLineStarted(PlayerInfo speaker, string subtitle,
        float duration, bool isLocal)
    {
        LineStarted?.Invoke(speaker, subtitle, duration, isLocal);
    }
}
