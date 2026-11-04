using System;
using System.Collections.Generic;

[Serializable]
public sealed class CliCheckResult
{
    public string Name { get; set; }
    public bool Passed { get; set; }
    public string Detail { get; set; }
}

[Serializable]
public sealed class CliProjectSnapshot
{
    public string ProjectName { get; set; }
    public string UnityVersion { get; set; }
    public string ActiveScene { get; set; }
    public bool IsPlaying { get; set; }
    public bool IsCompiling { get; set; }
    public int SceneObjectCount { get; set; }
    public int PrefabCount { get; set; }
    public int ScriptableObjectCount { get; set; }
    public int WarningCount { get; set; }
    public int ErrorCount { get; set; }
    public List<CliCheckResult> Checks { get; set; }
}

[Serializable]
public sealed class CliSceneReport
{
    public string SceneName { get; set; }
    public string ScenePath { get; set; }
    public bool IsDirty { get; set; }
    public int RootCount { get; set; }
    public int ObjectCount { get; set; }
    public int ActiveObjectCount { get; set; }
    public int CameraCount { get; set; }
    public int LightCount { get; set; }
    public int AudioListenerCount { get; set; }
    public int MissingScriptCount { get; set; }
    public List<string> BuildScenes { get; set; }
    public List<string> Problems { get; set; }
}

[Serializable]
public sealed class CliPlayerReport
{
    public int PlayerCount { get; set; }
    public List<CliPlayerItem> Players { get; set; }
    public List<string> Problems { get; set; }
}

[Serializable]
public sealed class CliPlayerItem
{
    public string Path { get; set; }
    public bool IsActive { get; set; }
    public bool HasInput { get; set; }
    public bool HasAction { get; set; }
    public bool HasCharacterController { get; set; }
    public bool HasPlayerData { get; set; }
    public bool HasAnimationController { get; set; }
    public bool HasFirstPersonView { get; set; }
    public bool HasThirdPersonView { get; set; }
    public string WeaponData { get; set; }
}

[Serializable]
public sealed class CliAnimationReport
{
    public int AnimatorCount { get; set; }
    public int ViewCount { get; set; }
    public List<CliAnimatorItem> Animators { get; set; }
    public List<string> Problems { get; set; }
}

[Serializable]
public sealed class CliAnimatorItem
{
    public string Path { get; set; }
    public bool IsActive { get; set; }
    public string Controller { get; set; }
    public string Avatar { get; set; }
    public int ParameterCount { get; set; }
}

[Serializable]
public sealed class CliUiReport
{
    public int CanvasCount { get; set; }
    public int EventSystemCount { get; set; }
    public int SelectableCount { get; set; }
    public List<string> Canvases { get; set; }
    public List<string> Problems { get; set; }
}

[Serializable]
public sealed class CliDataReport
{
    public int ScriptableObjectCount { get; set; }
    public int WeaponDataCount { get; set; }
    public int WeaponPrefabCount { get; set; }
    public int PlayerDataCount { get; set; }
    public int InputCatalogCount { get; set; }
    public List<string> WeaponIds { get; set; }
    public List<string> UnusedWeaponData { get; set; }
    public List<string> Problems { get; set; }
}

[Serializable]
public sealed class CliPrefabReport
{
    public int PrefabCount { get; set; }
    public int MissingScriptCount { get; set; }
    public int MissingMaterialCount { get; set; }
    public List<string> Problems { get; set; }
}

[Serializable]
public sealed class CliAssetReport
{
    public int AssetCount { get; set; }
    public int MissingReferenceCount { get; set; }
    public int CandidateReferenceCount { get; set; }
    public List<string> MissingReferences { get; set; }
    public List<string> CandidateReferences { get; set; }
}

[Serializable]
public sealed class CliVerificationBundle
{
    public DateTime CreatedAtUtc { get; set; }
    public CliProjectSnapshot Project { get; set; }
    public CliSceneReport Scene { get; set; }
    public CliPlayerReport Player { get; set; }
    public CliAnimationReport Animation { get; set; }
    public CliUiReport Ui { get; set; }
    public CliDataReport Data { get; set; }
    public CliPrefabReport Prefabs { get; set; }
    public CliAssetReport Assets { get; set; }
    public int ProblemCount { get; set; }
    public int BlockingProblemCount { get; set; }
    public int WarningCount { get; set; }
    public bool Passed { get; set; }
}
