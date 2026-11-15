using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class TestMatchLauncher : MonoBehaviour
{
    private MatchConfig _pendingConfig;

    public static bool Launch(MatchConfig config)
    {
        if (config == null || !config.Mode || !config.Map ||
            string.IsNullOrWhiteSpace(config.Map.SceneName))
        {
            return false;
        }

        GameObject launcherObject = new GameObject("TestMatchLauncher");
        DontDestroyOnLoad(launcherObject);
        TestMatchLauncher launcher = launcherObject.AddComponent<TestMatchLauncher>();
        launcher._pendingConfig = config;
        SceneManager.sceneLoaded += launcher.HandleSceneLoaded;
        SceneManager.LoadScene(config.Map.SceneName);
        return true;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        StartCoroutine(StartMatchAfterSceneReady());
    }

    private IEnumerator StartMatchAfterSceneReady()
    {
        yield return null;

        MatchConfig config = _pendingConfig;
        _pendingConfig = null;
        bool started = MatchManager.Instance.StartMatch(config);
        if (!started)
        {
            Debug.LogError("测试大厅已加载地图，但对局配置启动失败。", this);
        }

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }
}
