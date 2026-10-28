using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public partial class EventMgr : BaseMgr<EventMgr>
{
    protected override bool Ddol => true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticEvents()
    {
        FieldInfo[] fields = typeof(EventMgr).GetFields(BindingFlags.Public | BindingFlags.Static);
        foreach (FieldInfo f in fields)
        {
            if (typeof(Delegate).IsAssignableFrom(f.FieldType))
            {
                f.SetValue(null, null);
            }
        }
    }

    public static Subscription Bind(MonoBehaviour owner, Action add, Action remove)
    {
        return Register(owner, add, remove, null);
    }

    private static Subscription Register(MonoBehaviour owner, Action add, Action remove, object token)
    {
        if (!owner || !owner.gameObject)
        {
            Debug.LogWarning("[EventMgr] owner 无效，跳过绑定");
            return null;
        }
        EventHook hook = owner.GetComponent<EventHook>();
        if (!hook)
        {
            hook = owner.gameObject.AddComponent<EventHook>();
        }
        return hook.Bind(add, remove, token);
    }

    internal sealed class Registration
    {
        internal List<Registration> OwnerList;
        internal EventHook Hook;
        internal object Token;
        internal Action Add;
        internal Action Remove;
        internal bool IsEnabled;
        internal bool IsDead;
    }

    public sealed class Subscription
    {
        private readonly Registration _reg;
        internal Subscription(Registration reg)
        {
            _reg = reg;
        }

        public bool IsAlive => !_reg.IsDead;

        public void Dispose()
        {
            Detach(_reg);
        }
    }

    internal static void Enable(Registration reg)
    {
        if (reg.IsEnabled || reg.IsDead)
        {
            return;
        }
        reg.IsEnabled = true;
        InvokeSafely(reg.Add);
    }

    internal static void Disable(Registration reg)
    {
        if (!reg.IsEnabled || reg.IsDead)
        {
            return;
        }
        reg.IsEnabled = false;
        InvokeSafely(reg.Remove);
    }

    internal static void Detach(Registration reg)
    {
        if (reg.IsDead)
        {
            return;
        }
        reg.IsDead = true;
        if (reg.IsEnabled)
        {
            reg.IsEnabled = false;
            InvokeSafely(reg.Remove);
        }
        reg.OwnerList?.Remove(reg);
        reg.Hook?.TryAutoDestroy();
    }

    private static void InvokeSafely(Action action)
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
}
