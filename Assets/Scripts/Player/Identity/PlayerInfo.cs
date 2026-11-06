using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerInfo : MonoBehaviour
{
    private const string DefaultPlayerName = "玩家";

    [Header("玩家身份")]
    [SerializeField, InspectorName("玩家名")] private string _playerName = DefaultPlayerName;

    public string PlayerName
    {
        get
        {
            return _playerName;
        }
    }

    private void Awake()
    {
        RefreshGameObjectName();
    }

    public void SetPlayerName(string playerName)
    {
        _playerName = NormalizePlayerName(playerName);
        RefreshGameObjectName();
    }

    private void OnValidate()
    {
        _playerName = NormalizePlayerName(_playerName);
        RefreshGameObjectName();
    }

    private string NormalizePlayerName(string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName))
        {
            return DefaultPlayerName;
        }

        return playerName.Trim();
    }

    private void RefreshGameObjectName()
    {
        string normalizedName = NormalizePlayerName(_playerName);
        _playerName = normalizedName;
        if (gameObject.name != normalizedName)
        {
            gameObject.name = normalizedName;
        }
    }
}
