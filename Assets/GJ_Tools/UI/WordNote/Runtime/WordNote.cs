using UnityEngine;

public static class WordNote
{
    public static WordNoteHandle Show(string content)
    {
        return Show(content, WordNoteUI.Instance ? WordNoteUI.Instance.DefaultDuration : 2f);
    }

    public static WordNoteHandle Show(string content, float duration)
    {
        if (!TryGetUI(out WordNoteUI ui))
        {
            return null;
        }

        return ui.Enqueue(content, ui.DefaultStyle, duration);
    }

    public static WordNoteHandle Show(
        string content,
        WordNoteStyle style,
        float duration = 2f)
    {
        if (!TryGetUI(out WordNoteUI ui))
        {
            return null;
        }

        return ui.Enqueue(content, style, duration);
    }

    public static bool Close(WordNoteHandle handle)
    {
        return handle != null && handle.Close();
    }

    public static void Clear()
    {
        if (WordNoteUI.Instance)
        {
            WordNoteUI.Instance.Clear();
        }
    }

    private static bool TryGetUI(out WordNoteUI ui)
    {
        ui = WordNoteUI.Instance;
        if (ui)
        {
            return true;
        }

        Debug.LogWarning("[WordNote] 场景中缺少 WordNote_UI 预制体实例。");
        return false;
    }
}
