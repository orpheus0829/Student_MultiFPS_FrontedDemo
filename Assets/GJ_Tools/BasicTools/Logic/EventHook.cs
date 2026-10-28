using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class EventHook : MonoBehaviour
{
    private readonly List<EventMgr.Registration> _regs = new List<EventMgr.Registration>();
    private bool _destroying;
    private bool _pendingDestroy;
    private Coroutine _autoDestroyCo;

    internal EventMgr.Subscription Bind(Action add, Action remove, object token)
    {
        foreach (EventMgr.Registration old in _regs)
        {
            if (old.IsDead)
            {
                continue;
            }
            if (token != null && Equals(old.Token, token))
            {
                Debug.LogWarning("[EventMgr] 重复绑定，返回已有句柄");
                return new EventMgr.Subscription(old);
            }
            if (old.Add == add && old.Remove == remove)
            {
                Debug.LogWarning("[EventMgr] 重复绑定，返回已有句柄");
                return new EventMgr.Subscription(old);
            }
        }

        EventMgr.Registration reg = new EventMgr.Registration
        {
            OwnerList = _regs,
            Hook = this,
            Token = token,
            Add = add,
            Remove = remove
        };
        _regs.Add(reg);
        if (gameObject.activeInHierarchy)
        {
            EventMgr.Enable(reg);
        }
        return new EventMgr.Subscription(reg);
    }

    internal void TryAutoDestroy()
    {
        if (_destroying || _regs.Count > 0 || _pendingDestroy)
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
        foreach (EventMgr.Registration reg in _regs.ToArray())
        {
            EventMgr.Enable(reg);
        }
        TryAutoDestroy();
    }

    private void OnDisable()
    {
        foreach (EventMgr.Registration reg in _regs.ToArray())
        {
            EventMgr.Disable(reg);
        }
    }

    private void OnDestroy()
    {
        _destroying = true;
        foreach (EventMgr.Registration reg in _regs.ToArray())
        {
            EventMgr.Detach(reg);
        }
        _regs.Clear();
    }

    private IEnumerator DestroySelfNextFrame()
    {
        yield return null;
        _pendingDestroy = false;
        if (!this)
        {
            yield break;
        }
        if (_regs.Count == 0)
        {
            Destroy(this);
        }
    }
}
