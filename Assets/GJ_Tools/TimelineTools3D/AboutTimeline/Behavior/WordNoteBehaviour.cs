using UnityEngine;
using UnityEngine.Playables;

public sealed class WordNoteBehaviour : PlayableBehaviour
{
    private WordNoteHandle _handle;

    public WordNoteClip Clip { private get; set; }

    public override void OnBehaviourPlay(
        Playable playable,
        FrameData info)
    {
        CloseCurrent();
        if (!Application.isPlaying ||
            !Clip ||
            string.IsNullOrWhiteSpace(Clip.Content))
        {
            return;
        }

        _handle = WordNote.Show(Clip.Content, Clip.Style, 0f);
    }

    public override void OnBehaviourPause(
        Playable playable,
        FrameData info)
    {
        CloseCurrent();
    }

    public override void OnGraphStop(Playable playable)
    {
        CloseCurrent();
    }

    private void CloseCurrent()
    {
        if (_handle == null)
        {
            return;
        }

        _handle.Close();
        _handle = null;
    }
}
