using System;
using System.Collections.Generic;
using UnityEngine;

public class SmartWait : BaseMgr<SmartWait>
{
    protected override bool Ddol => true;

    private readonly List<WaitTask> _waits = new List<WaitTask>();

    public WaitTask WaitUntil(MonoBehaviour owner, Func<bool> condition, Action onDone)
    {
        if (condition == null)
        {
            Debug.LogWarning("[SmartWait] 条件不能为空");
            return null;
        }
        return Register(owner, new[] { condition }, onDone);
    }

    public WaitTask WaitUntilAll(MonoBehaviour owner, params Func<bool>[] conditions)
    {
        if (conditions == null || conditions.Length == 0)
        {
            Debug.LogWarning("[SmartWait] 至少要一个条件");
            return null;
        }
        return Register(owner, conditions, null);
    }

    private WaitTask Register(MonoBehaviour owner, Func<bool>[] conditions, Action onDone)
    {
        if (!owner || !owner.gameObject)
        {
            Debug.LogWarning("[SmartWait] owner 无效，跳过等待");
            return null;
        }
        WaitHook hook = owner.GetComponent<WaitHook>();
        if (!hook)
        {
            hook = owner.gameObject.AddComponent<WaitHook>();
        }
        WaitTask task = new WaitTask
        {
            OwnerList = _waits,
            Conditions = conditions
        };
        if (onDone != null)
        {
            task.OnDone += onDone;
        }
        hook.Add(task);
        _waits.Add(task);
        if (!owner.gameObject.activeInHierarchy)
        {
            if (task.PauseOnDisable)
            {
                task.AutoPaused = true;
                task.IsPaused = true;
            }
            else
            {
                CancelTask(task);
                Debug.LogWarning("[SmartWait] owner 处于失活状态，等待已被取消");
                return null;
            }
        }
        return task;
    }

    private void Update()
    {
        if (_waits.Count == 0)
        {
            return;
        }
        foreach (WaitTask task in _waits.ToArray())
        {
            if (task.IsDead || task.IsPaused)
            {
                continue;
            }
            if (task.CheckInterval > 0)
            {
                task.CheckTimer += Time.deltaTime;
                if (task.CheckTimer < task.CheckInterval)
                {
                    continue;
                }
                task.CheckTimer = 0;
            }
            if (IsAllTrue(task))
            {
                Complete(task);
            }
        }
    }

    private static bool IsAllTrue(WaitTask task)
    {
        foreach (Func<bool> c in task.Conditions)
        {
            bool ok;
            try
            {
                ok = c();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                CancelTask(task);
                return false;
            }
            if (!ok)
            {
                return false;
            }
        }
        return true;
    }

    private static void Complete(WaitTask task)
    {
        if (task == null || task.IsDead)
        {
            return;
        }
        task.IsDead = true;
        task.OwnerList?.Remove(task);
        task.HookList?.Remove(task);
        task.Hook?.TryAutoDestroy();
        SafeCall(task.OnDone);
    }

    internal static void CancelTask(WaitTask task)
    {
        if (task == null || task.IsDead)
        {
            return;
        }
        task.IsDead = true;
        task.OwnerList?.Remove(task);
        task.HookList?.Remove(task);
        task.Hook?.TryAutoDestroy();
    }

    private static void SafeCall(Action action)
    {
        try
        {
            action?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    public class WaitTask
    {
        public bool PauseOnDisable;
        public float CheckInterval;
        internal float CheckTimer;
        internal List<WaitTask> OwnerList;
        internal List<WaitTask> HookList;
        internal WaitHook Hook;
        internal Func<bool>[] Conditions;
        internal Action OnDone;
        internal bool IsPaused;
        internal bool AutoPaused;
        internal bool IsDead;

        public void Pause()
        {
            IsPaused = true;
        }

        public void Resume()
        {
            IsPaused = false;
        }

        public void Cancel()
        {
            CancelTask(this);
        }

        public WaitTask Then(Action onDone)
        {
            if (onDone != null)
            {
                OnDone += onDone;
            }
            return this;
        }

        public WaitTask PauseWhenInactive()
        {
            PauseOnDisable = true;
            return this;
        }

        public WaitTask CheckEvery(float interval)
        {
            CheckInterval = Mathf.Max(0f, interval);
            return this;
        }
    }
}
