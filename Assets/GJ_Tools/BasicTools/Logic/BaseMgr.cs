using UnityEngine;

public abstract class BaseMgr<T> : MonoBehaviour where T : BaseMgr<T>
{
    private static T _instance;

    public static T instance
    {
        get
        {
            if (!_instance)
            {
                GameObject go = new GameObject(typeof(T).Name);
                _instance = go.AddComponent<T>();
                if (_instance.Ddol)
                {
                    Object.DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    protected virtual bool Ddol => false;

    protected virtual void Awake()
    {
        if (_instance && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this as T;
        if (Ddol)
        {
            Object.DontDestroyOnLoad(gameObject);
        }
    }
}
