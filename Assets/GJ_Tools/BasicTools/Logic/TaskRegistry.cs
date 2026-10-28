using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TaskRegistry : MonoBehaviour
{
    [SerializeField] private List<SmartTimer.TimerTask> _tasks = new List<SmartTimer.TimerTask>();

    public IReadOnlyList<SmartTimer.TimerTask> Tasks => _tasks.AsReadOnly();

    public bool HasTasks => _tasks.Count > 0;

    internal void AddTask(SmartTimer.TimerTask task)
    {
        if (task == null || _tasks.Contains(task))
        {
            return;
        }
        _tasks.Add(task);
        if (!gameObject.activeInHierarchy)
        {
            if (task.PauseOnDisable)
            {
                task.AutoPaused = true;
                task.Pause();
            }
            else
            {
                task.Stop();
            }
        }
        if (_pendingDestroy)
        {
            _pendingDestroy = false;
            if (enabled && gameObject.activeInHierarchy)
            {
                StopCoroutine(_autoDestroyCo);
            }
            _autoDestroyCo = null;
        }
    }

    internal void RemoveTask(SmartTimer.TimerTask task)
    {
        if (task == null || !_tasks.Contains(task))
        {
            return;
        }
        _tasks.Remove(task);
        MaybeAutoDestroy();
    }

    internal void Clear()
    {
        foreach (SmartTimer.TimerTask task in _tasks.ToArray())
        {
            task.Stop();
        }
        MaybeAutoDestroy();
    }

    private void OnDestroy()
    {
        _destroying = true;
        foreach (SmartTimer.TimerTask task in _tasks.ToArray())
        {
            task.Stop();
        }
        _tasks.Clear();
    }

    private bool _destroying;
    private bool _pendingDestroy;
    private Coroutine _autoDestroyCo;

    private void MaybeAutoDestroy()
    {
        if (_destroying || _tasks.Count > 0 || _pendingDestroy)
        {
            return;
        }
        if (!enabled || !gameObject.activeInHierarchy)
        {
            return;
        }
        _pendingDestroy = true;
        _autoDestroyCo = StartCoroutine(DestroySelfNextFrame());
    }

    private void OnEnable()
    {
        foreach (SmartTimer.TimerTask task in _tasks)
        {
            if (task.AutoPaused)
            {
                task.AutoPaused = false;
                task.Resume();
            }
        }
        MaybeAutoDestroy();
    }

    private void OnDisable()
    {
        foreach (SmartTimer.TimerTask task in _tasks.ToArray())
        {
            if (task.PauseOnDisable)
            {
                if (!task.IsPaused)
                {
                    task.AutoPaused = true;
                    task.Pause();
                }
            }
            else
            {
                task.Stop();
            }
        }
    }

    private IEnumerator DestroySelfNextFrame()
    {
        yield return null;
        _pendingDestroy = false;
        if (!this)
        {
            yield break;
        }
        if (_tasks.Count == 0)
        {
            Destroy(this);
        }
    }
}
