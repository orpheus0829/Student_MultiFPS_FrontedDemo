using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class WaitHook : MonoBehaviour
{
    private readonly List<SmartWait.WaitTask> _waits = new List<SmartWait.WaitTask>();
    private bool _destroying;
    private bool _pendingDestroy;
    private Coroutine _autoDestroyCo;

    internal void Add(SmartWait.WaitTask task)
    {
        _waits.Add(task);
        task.HookList = _waits;
        task.Hook = this;
    }

    internal void TryAutoDestroy()
    {
        if (_destroying || _waits.Count > 0 || _pendingDestroy)
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
        foreach (SmartWait.WaitTask task in _waits.ToArray())
        {
            if (task.AutoPaused)
            {
                task.AutoPaused = false;
                task.IsPaused = false;
            }
        }
        TryAutoDestroy();
    }

    private void OnDisable()
    {
        foreach (SmartWait.WaitTask task in _waits.ToArray())
        {
            if (task.PauseOnDisable)
            {
                if (!task.IsPaused)
                {
                    task.AutoPaused = true;
                    task.IsPaused = true;
                }
            }
            else
            {
                SmartWait.CancelTask(task);
            }
        }
    }

    private void OnDestroy()
    {
        _destroying = true;
        foreach (SmartWait.WaitTask task in _waits.ToArray())
        {
            SmartWait.CancelTask(task);
        }
        _waits.Clear();
    }

    private IEnumerator DestroySelfNextFrame()
    {
        yield return null;
        _pendingDestroy = false;
        if (!this)
        {
            yield break;
        }
        if (_waits.Count == 0)
        {
            Destroy(this);
        }
    }
}
