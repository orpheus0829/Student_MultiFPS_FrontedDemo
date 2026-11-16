using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class WeaponPresentationConfigurator
{
    private const string WeaponPrefabFolder = "Assets/Prefabs/Weapons";
    private const string WeaponDataFolder = "Assets/SO/Weapons/Data";
    private const string MuzzleName = "枪口";
    private const string GripRootName = "ThirdPersonGrip";
    private const string LeftHandName = "LeftHand";

    private static readonly WeaponTemplate[] Templates =
    {
        new WeaponTemplate("DGL50", "M1911"),
        new WeaponTemplate("Drake-12", "KXG12"),
        new WeaponTemplate("G3", "AK"),
        new WeaponTemplate("Kolibri", "M1911"),
        new WeaponTemplate("L96X", "Kar98k"),
        new WeaponTemplate("MGX5", "AK"),
        new WeaponTemplate("Mk14EBR", "AK"),
        new WeaponTemplate("MX16A4", "AK"),
        new WeaponTemplate("Striker-V", "MPS5"),
        new WeaponTemplate("SVD", "Kar98k"),
        new WeaponTemplate("Viper-357", "M1911"),
        new WeaponTemplate("X18", "M1911")
    };

    [MenuItem("工具/Gun Demo/武器/校准新增枪表现")]
    public static WeaponPresentationConfigureReport Configure()
    {
        List<string> problems = new List<string>();
        int configured = 0;
        for (int i = 0; i < Templates.Length; i++)
        {
            if (ConfigureWeapon(Templates[i], problems))
            {
                configured++;
            }
        }

        ConfigureRevolverReload(problems);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return new WeaponPresentationConfigureReport
        {
            Passed = problems.Count == 0,
            ConfiguredWeaponCount = configured,
            Problems = problems
        };
    }

    private static bool ConfigureWeapon(WeaponTemplate template, List<string> problems)
    {
        string prefabPath = GetPrefabPath(template.WeaponName);
        GameObject templatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            GetPrefabPath(template.TemplateName));
        WeaponDefinitionSO weaponData = AssetDatabase.LoadAssetAtPath<WeaponDefinitionSO>(
            GetDataPath(template.WeaponName));
        WeaponDefinitionSO templateData = AssetDatabase.LoadAssetAtPath<WeaponDefinitionSO>(
            GetDataPath(template.TemplateName));
        if (!templatePrefab || !weaponData || !templateData)
        {
            problems.Add(template.WeaponName + " 缺少 Prefab、数据或握持模板。");
            return false;
        }

        Transform templateGrip = templatePrefab.transform.Find(GripRootName);
        Transform templateLeftHand = templatePrefab.transform.Find(
            GripRootName + "/" + LeftHandName);
        if (!templateGrip || !templateLeftHand)
        {
            problems.Add(template.TemplateName + " 缺少正式第三人称握持锚点。");
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            NormalizeWeaponVisual(template.WeaponName, root.transform);
            Transform grip = GetOrCreateChild(root.transform, GripRootName);
            CopyLocalPose(templateGrip, grip);
            Transform leftHand = GetOrCreateChild(grip, LeftHandName);
            CopyLocalPose(templateLeftHand, leftHand);
            EnsureGripGizmo(leftHand.gameObject);

            Transform muzzle = GetOrCreateChild(root.transform, MuzzleName);
            if (!TryCalculateMuzzlePose(root, out Vector3 muzzlePosition))
            {
                problems.Add(template.WeaponName + " 无法从网格计算枪口位置。");
                return false;
            }

            muzzle.localPosition = muzzlePosition;
            muzzle.localRotation = Quaternion.Euler(0f, 180f, 0f);
            muzzle.localScale = Vector3.one;
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        CopyThirdPersonGripData(templateData, weaponData);
        EditorUtility.SetDirty(weaponData);
        return true;
    }

    private static void CopyThirdPersonGripData(
        WeaponDefinitionSO templateData,
        WeaponDefinitionSO targetData)
    {
        SerializedObject source = new SerializedObject(templateData);
        SerializedObject target = new SerializedObject(targetData);
        CopyProperty(source, target, "_ik._thirdPersonGripPosition");
        CopyProperty(source, target, "_ik._thirdPersonGripRotation");
        CopyProperty(source, target, "_ik._thirdPersonGripScale");
        CopyProperty(source, target, "_ik._leftClavicleOffset");
        CopyProperty(source, target, "_ik._rightClavicleOffset");
        CopyProperty(source, target, "_ik._sprintRightHandRotationOffset");
        CopyProperty(source, target, "_ik._thirdPersonFingerPose");
        target.FindProperty("_ik._useThirdPersonGrip").boolValue = true;
        target.FindProperty("_ik._leftHandIkPointPath").stringValue =
            GripRootName + "/" + LeftHandName;
        target.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CopyProperty(
        SerializedObject source,
        SerializedObject target,
        string propertyPath)
    {
        SerializedProperty sourceProperty = source.FindProperty(propertyPath);
        SerializedProperty targetProperty = target.FindProperty(propertyPath);
        if (sourceProperty != null && targetProperty != null)
        {
            targetProperty.boxedValue = sourceProperty.boxedValue;
        }
    }

    private static void ConfigureRevolverReload(List<string> problems)
    {
        WeaponDefinitionSO weaponData = AssetDatabase.LoadAssetAtPath<WeaponDefinitionSO>(
            GetDataPath("Viper-357"));
        WeaponActionSetSO actionSet = AssetDatabase.LoadAssetAtPath<WeaponActionSetSO>(
            "Assets/SO/Weapons/Actions/Viper-357.asset");
        if (!weaponData || !actionSet)
        {
            problems.Add("Viper-357 缺少武器数据或动作集。");
            return;
        }

        SerializedObject weapon = new SerializedObject(weaponData);
        weapon.FindProperty("_ammo._reloadStyle").enumValueIndex =
            (int)WeaponReloadStyle.Revolver;
        weapon.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(weaponData);

        SerializedObject actions = new SerializedObject(actionSet);
        float reloadDuration = GetAnimationDuration(
            "Assets/Resource/Animations/FirstPerson/KINEMATION/Weapons/" +
            "Viper-357/Character/A_FP_Viper-357_Reload_Empty.FBX");
        actions.FindProperty("_reload._enabled").boolValue = true;
        actions.FindProperty("_reload._duration").floatValue = reloadDuration;
        actions.FindProperty("_emptyReload._enabled").boolValue = true;
        actions.FindProperty("_emptyReload._duration").floatValue = reloadDuration;
        ConfigureRevolverMarkers(actions.FindProperty("_reload"));
        ConfigureRevolverMarkers(actions.FindProperty("_emptyReload"));
        actions.FindProperty("_perRoundStart._enabled").boolValue = false;
        actions.FindProperty("_perRoundInsert._enabled").boolValue = false;
        actions.FindProperty("_perRoundEnd._enabled").boolValue = false;
        actions.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(actionSet);
    }

    private static bool TryCalculateMuzzlePose(
        GameObject sourceRoot,
        out Vector3 muzzlePosition)
    {
        GameObject sampleRoot = Object.Instantiate(sourceRoot);
        sampleRoot.hideFlags = HideFlags.HideAndDontSave;
        Animator animator = sampleRoot.GetComponentInChildren<Animator>(true);
        if (animator && animator.runtimeAnimatorController)
        {
            animator.Rebind();
            animator.Play("Idle", 0, 0f);
            animator.Update(0.01f);
        }

        List<Vector3> vertices = new List<Vector3>();
        Transform root = sampleRoot.transform;
        MeshFilter[] filters = sampleRoot.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < filters.Length; i++)
        {
            Mesh mesh = filters[i].sharedMesh;
            if (mesh)
            {
                AddVertices(root, filters[i].transform, mesh.vertices, vertices);
            }
        }

        SkinnedMeshRenderer[] skinned =
            sampleRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < skinned.Length; i++)
        {
            Mesh bakedMesh = new Mesh();
            skinned[i].BakeMesh(bakedMesh);
            if (bakedMesh.vertexCount > 0)
            {
                AddVertices(root, skinned[i].transform, bakedMesh.vertices, vertices);
            }
            Object.DestroyImmediate(bakedMesh);
        }

        if (vertices.Count == 0)
        {
            Object.DestroyImmediate(sampleRoot);
            muzzlePosition = Vector3.zero;
            return false;
        }

        float minimumZ = float.PositiveInfinity;
        float maximumZ = float.NegativeInfinity;
        for (int i = 0; i < vertices.Count; i++)
        {
            minimumZ = Mathf.Min(minimumZ, vertices[i].z);
            maximumZ = Mathf.Max(maximumZ, vertices[i].z);
        }

        float endDepth = Mathf.Max(0.004f, (maximumZ - minimumZ) * 0.015f);
        Vector2 center = Vector2.zero;
        int endVertexCount = 0;
        for (int i = 0; i < vertices.Count; i++)
        {
            Vector3 vertex = vertices[i];
            if (vertex.z >= maximumZ - endDepth)
            {
                center += new Vector2(vertex.x, vertex.y);
                endVertexCount++;
            }
        }

        if (endVertexCount == 0)
        {
            Object.DestroyImmediate(sampleRoot);
            muzzlePosition = Vector3.zero;
            return false;
        }

        center /= endVertexCount;
        muzzlePosition = new Vector3(center.x, center.y, maximumZ + 0.002f);
        Object.DestroyImmediate(sampleRoot);
        return true;
    }

    private static void NormalizeWeaponVisual(string weaponName, Transform root)
    {
        if (weaponName != "SVD")
        {
            return;
        }

        Transform model = root.Find("Model");
        if (model)
        {
            model.localRotation = Quaternion.identity;
        }
    }

    private static float GetAnimationDuration(string assetPath)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is AnimationClip clip && !clip.name.StartsWith("__preview__"))
            {
                return Mathf.Max(0.01f, clip.length);
            }
        }

        return 7.333333f;
    }

    private static void ConfigureRevolverMarkers(SerializedProperty action)
    {
        float[] progress = { 0.34f, 0.43f, 0.52f, 0.61f, 0.70f, 0.79f };
        SerializedProperty markers = action.FindPropertyRelative("_markers");
        markers.arraySize = progress.Length;
        for (int i = 0; i < progress.Length; i++)
        {
            SerializedProperty marker = markers.GetArrayElementAtIndex(i);
            marker.FindPropertyRelative("_markerType").enumValueIndex =
                (int)WeaponActionMarkerType.InsertRound;
            marker.FindPropertyRelative("_progress").floatValue = progress[i];
        }
    }

    private static void AddVertices(
        Transform root,
        Transform meshTransform,
        Vector3[] meshVertices,
        List<Vector3> vertices)
    {
        for (int i = 0; i < meshVertices.Length; i++)
        {
            vertices.Add(root.InverseTransformPoint(
                meshTransform.TransformPoint(meshVertices[i])));
        }
    }

    private static Transform GetOrCreateChild(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child)
        {
            return child;
        }

        child = new GameObject(childName).transform;
        child.SetParent(parent, false);
        return child;
    }

    private static void CopyLocalPose(Transform source, Transform target)
    {
        target.localPosition = source.localPosition;
        target.localRotation = source.localRotation;
        target.localScale = source.localScale;
    }

    private static void EnsureGripGizmo(GameObject target)
    {
        if (!target.TryGetComponent(out WeaponThirdPersonGripGizmo _))
        {
            target.AddComponent<WeaponThirdPersonGripGizmo>();
        }
    }

    private static string GetPrefabPath(string weaponName)
    {
        return WeaponPrefabFolder + "/" + weaponName + ".prefab";
    }

    private static string GetDataPath(string weaponName)
    {
        return WeaponDataFolder + "/" + weaponName + ".asset";
    }

    private readonly struct WeaponTemplate
    {
        public readonly string WeaponName;
        public readonly string TemplateName;

        public WeaponTemplate(string weaponName, string templateName)
        {
            WeaponName = weaponName;
            TemplateName = templateName;
        }
    }
}

public sealed class WeaponPresentationConfigureReport
{
    public bool Passed;
    public int ConfiguredWeaponCount;
    public List<string> Problems;
}
