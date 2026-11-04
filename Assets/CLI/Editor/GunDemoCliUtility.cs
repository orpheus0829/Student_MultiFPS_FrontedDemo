using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GunDemoCliUtility
{
    public static List<GameObject> GetSceneObjects()
    {
        List<GameObject> objects = new List<GameObject>();
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return objects;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        foreach (GameObject root in roots)
        {
            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in children)
            {
                objects.Add(child.gameObject);
            }
        }

        return objects;
    }

    public static string GetPath(GameObject gameObject)
    {
        if (!gameObject)
        {
            return string.Empty;
        }

        string path = gameObject.name;
        Transform parent = gameObject.transform.parent;
        while (parent)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }

        return path;
    }

    public static int CountMissingScripts(GameObject gameObject)
    {
        int count = 0;
        Component[] components = gameObject.GetComponents<Component>();
        foreach (Component component in components)
        {
            if (!component)
            {
                count++;
            }
        }

        return count;
    }

    public static string GetAssetName(Object asset)
    {
        if (!asset)
        {
            return string.Empty;
        }

        string path = AssetDatabase.GetAssetPath(asset);
        return string.IsNullOrEmpty(path) ? asset.name : path;
    }
}
