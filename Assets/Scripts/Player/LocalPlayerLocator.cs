using UnityEngine;

public static class LocalPlayerLocator
{
    public static bool TryGet(out GameObject player)
    {
        PlayerLifeCtrl[] candidates = Object.FindObjectsByType<PlayerLifeCtrl>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < candidates.Length; i++)
        {
            PlayerLifeCtrl candidate = candidates[i];
            if (!candidate || candidate.GetComponent<AIPlayerCtrl>())
            {
                continue;
            }

            player = candidate.gameObject;
            return true;
        }

        player = null;
        return false;
    }
}
