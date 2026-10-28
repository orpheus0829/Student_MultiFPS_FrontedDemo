using System;
using System.Collections.Generic;
using UnityEngine;

public class SmartTimer : BaseMgr<SmartTimer>
{
    protected override bool Ddol => true;

    public enum Mode { Update, FixedUpdate, Realtime }

    [Serializable]
    public class TimerTask
    {
        public string Name;
        public Mode Mode;
        public float Duration;
        public float Elapsed;
        public float Progress;
        public float Remaining => Mathf.Max(0, Duration - Elapsed);
        public bool IsRunning;
        public bool IsPaused;
        public bool PauseOnDisable;
        internal bool AutoPaused;
        public float TickInterval;
        public float TickTimer;
        public bool IsLoop;
        public int LoopCount;
        public int MaxLoop;

        [NonSerialized] public Action OnStart;
        [NonSerialized] public Action<float> OnTick;
        [NonSerialized] public Action OnEnd;
        [NonSerialized] public Action OnLoop;

        public void Pause()
        {
            IsPaused = true;
        }

        public void Resume()
        {
            IsPaused = false;
        }

        public TimerTask PauseWhenInactive()
        {
            PauseOnDisable = true;
            return this;
        }

        public void Stop()
        {
            if (!IsRunning)
            {
                return;
            }
            IsRunning = false;
            SafeCall(OnEnd);
        }

        public void Restart()
        {
            Elapsed = 0;
            Progress = 0;
            TickTimer = 0;
            IsRunning = true;
            IsPaused = false;
            SafeCall(OnStart);
        }

        public string GetInfo()
        {
            return $"[{Name}] 运行:{IsRunning} 暂停:{IsPaused} 进度:{Progress:P0} 已过:{Elapsed:F2}s 剩余:{Remaining:F2}s 循环:{LoopCount}";
        }
    }

    [SerializeField]
    private List<TimerTask> _tasks = new List<TimerTask>();

    public TimerTask SetTimer(MonoBehaviour owner, float duration, Action onEnd, bool pauseOnDisable = false)
    {
        return SetTimer(owner, Mode.Update, duration, null, onEnd, null, 0.1f, false, null, -1, pauseOnDisable);
    }

    public TimerTask SetLoop(MonoBehaviour owner, float duration, Action onLoop = null, int maxLoop = -1, bool pauseOnDisable = false)
    {
        return SetTimer(owner, Mode.Update, duration, null, null, null, 0.1f, true, onLoop, maxLoop, pauseOnDisable);
    }

    public TimerTask SetTimer(MonoBehaviour owner, Mode mode = Mode.Update, float duration = 1f,
        Action onStart = null, Action onEnd = null, Action<float> onTick = null, float tickInterval = 0.1f,
        bool isLoop = false, Action onLoop = null, int maxLoop = -1, bool pauseOnDisable = false)
    {
        string startName = onStart != null ? onStart.Method.Name : "";
        string tickName = onTick != null ? onTick.Method.Name : "";
        string endName = onEnd != null ? onEnd.Method.Name : "";
        if (startName.StartsWith("<"))
        {
            startName = "";
        }
        if (tickName.StartsWith("<"))
        {
            tickName = "";
        }
        if (endName.StartsWith("<"))
        {
            endName = "";
        }
        string taskName = "";
        if (startName.Length > 0)
        {
            taskName = startName;
        }
        if (tickName.Length > 0)
        {
            taskName += taskName.Length > 0 ? $" → {tickName}" : tickName;
        }
        if (endName.Length > 0)
        {
            taskName += taskName.Length > 0 ? $" → {endName}" : endName;
        }
        if (taskName.Length == 0)
        {
            taskName = "Timer";
        }

        TimerTask task = new TimerTask
        {
            Name = taskName,
            Mode = mode,
            Duration = duration,
            Elapsed = 0,
            Progress = 0,
            IsRunning = true,
            IsPaused = false,
            TickInterval = tickInterval,
            TickTimer = 0,
            IsLoop = isLoop,
            LoopCount = 0,
            MaxLoop = maxLoop,
            PauseOnDisable = pauseOnDisable,
            OnStart = onStart,
            OnTick = onTick,
            OnEnd = onEnd,
            OnLoop = onLoop
        };
        _tasks.Add(task);
        SafeCall(onStart);

        if (owner && owner.gameObject)
        {
            TaskRegistry registry = owner.GetComponent<TaskRegistry>();
            if (!registry)
            {
                registry = owner.gameObject.AddComponent<TaskRegistry>();
            }
            registry.AddTask(task);
            task.OnEnd += () =>
            {
                if (registry)
                {
                    registry.RemoveTask(task);
                }
            };
        }
        return task;
    }

    private void Update()
    {
        if (_tasks.Count > 0)
        {
            TimerTask[] snapshot = _tasks.ToArray();
            foreach (TimerTask task in snapshot)
            {
                if (!task.IsRunning || task.IsPaused || task.Mode == Mode.FixedUpdate)
                {
                    continue;
                }
                Tick(task, task.Mode == Mode.Realtime ? Time.unscaledDeltaTime : Time.deltaTime);
            }
        }
        Cleanup();
    }

    private void FixedUpdate()
    {
        if (_tasks.Count > 0)
        {
            TimerTask[] snapshot = _tasks.ToArray();
            foreach (TimerTask task in snapshot)
            {
                if (!task.IsRunning || task.IsPaused || task.Mode != Mode.FixedUpdate)
                {
                    continue;
                }
                Tick(task, Time.fixedDeltaTime);
            }
        }
        Cleanup();
    }

    private void Cleanup()
    {
        for (int i = _tasks.Count - 1; i >= 0; i--)
        {
            if (!_tasks[i].IsRunning)
            {
                _tasks.RemoveAt(i);
            }
        }
    }

    private void Tick(TimerTask task, float delta)
    {
        task.Elapsed += delta;
        task.TickTimer += delta;
        task.Progress = Mathf.Clamp01(task.Elapsed / task.Duration);

        if (task.OnTick != null && task.TickTimer >= task.TickInterval)
        {
            task.TickTimer = 0;
            SafeCallTick(task.OnTick, task.Progress);
        }

        if (task.Elapsed < task.Duration)
        {
            return;
        }

        if (!task.IsLoop)
        {
            task.IsRunning = false;
            SafeCall(task.OnEnd);
            return;
        }

        int guard = 0;
        while (task.Elapsed >= task.Duration && guard < 128)
        {
            guard++;
            task.Elapsed -= task.Duration;
            task.LoopCount++;
            SafeCall(task.OnLoop);
            if (task.MaxLoop > 0 && task.LoopCount >= task.MaxLoop)
            {
                task.IsRunning = false;
                SafeCall(task.OnEnd);
                return;
            }
            task.TickTimer = 0;
            SafeCall(task.OnStart);
        }
        task.Progress = Mathf.Clamp01(task.Elapsed / task.Duration);
    }

    public void PauseAll()
    {
        foreach (TimerTask task in _tasks)
        {
            task.IsPaused = true;
        }
    }

    public void ResumeAll()
    {
        foreach (TimerTask task in _tasks)
        {
            task.IsPaused = false;
        }
    }

    public void StopAll()
    {
        TimerTask[] toNotify = _tasks.ToArray();
        _tasks.Clear();
        foreach (TimerTask task in toNotify)
        {
            task.IsRunning = false;
            SafeCall(task.OnEnd);
        }
    }

    public void RestartAll()
    {
        TimerTask[] snapshot = _tasks.ToArray();
        foreach (TimerTask task in snapshot)
        {
            task.Elapsed = 0;
            task.Progress = 0;
            task.TickTimer = 0;
            task.IsRunning = true;
            task.IsPaused = false;
            SafeCall(task.OnStart);
        }
    }

    public IReadOnlyList<TimerTask> GetAllTasks()
    {
        return _tasks.AsReadOnly();
    }

    private void OnDestroy()
    {
        TimerTask[] snapshot = _tasks.ToArray();
        _tasks.Clear();
        foreach (TimerTask task in snapshot)
        {
            task.IsRunning = false;
            SafeCall(task.OnEnd);
        }
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

    private static void SafeCallTick(Action<float> action, float progress)
    {
        try
        {
            action?.Invoke(progress);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }
}
