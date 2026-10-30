using System;
using System.Collections.Generic;
using UnityEngine;

public static class CameraControlAuthority
{
    private static readonly Dictionary<object, int> _owners = new Dictionary<object, int>();

    public static event Action<bool> Changed;

    public static bool IsControlled => _owners.Count > 0;

    public static void Acquire(object owner)
    {
        if (owner == null)
        {
            return;
        }

        bool wasControlled = IsControlled;
        _owners.TryGetValue(owner, out int count);
        _owners[owner] = count + 1;
        if (wasControlled != IsControlled)
        {
            Changed?.Invoke(IsControlled);
        }
    }

    public static void Release(object owner)
    {
        if (owner == null || !_owners.TryGetValue(owner, out int count))
        {
            return;
        }

        bool wasControlled = IsControlled;
        if (count <= 1)
        {
            _owners.Remove(owner);
        }
        else
        {
            _owners[owner] = count - 1;
        }

        if (wasControlled != IsControlled)
        {
            Changed?.Invoke(IsControlled);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        _owners.Clear();
        Changed = null;
    }
}
