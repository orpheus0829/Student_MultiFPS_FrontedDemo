using UnityEngine;

public sealed class PlayerPrefsInputBindingStorage : IInputBindingStorage
{
    private readonly string _key;

    public PlayerPrefsInputBindingStorage(string key)
    {
        _key = $"GJTools.Input.{key}.BindingOverrides";
    }

    public bool TryLoad(out string json)
    {
        if (!PlayerPrefs.HasKey(_key))
        {
            json = null;
            return false;
        }

        json = PlayerPrefs.GetString(_key);
        return !string.IsNullOrWhiteSpace(json);
    }

    public void Save(string json)
    {
        PlayerPrefs.SetString(_key, json);
        PlayerPrefs.Save();
    }

    public void Delete()
    {
        PlayerPrefs.DeleteKey(_key);
        PlayerPrefs.Save();
    }
}
