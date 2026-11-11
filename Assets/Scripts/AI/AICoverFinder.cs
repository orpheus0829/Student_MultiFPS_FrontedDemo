using UnityEngine;
using UnityEngine.AI;

public sealed class AICoverFinder
{
    private const int CandidateCount = 20;
    private readonly RaycastHit[] _hits = new RaycastHit[16];

    public bool TryFind(
        Vector3 origin,
        Vector3 threat,
        float searchRadius,
        LayerMask obstacleMask,
        out Vector3 coverPosition)
    {
        coverPosition = origin;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < CandidateCount; i++)
        {
            float angle = i * (360f / CandidateCount);
            float radius = Mathf.Lerp(searchRadius * 0.35f, searchRadius, (i % 4) / 3f);
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
            if (!NavMesh.SamplePosition(origin + offset, out NavMeshHit sample, 2.5f,
                NavMesh.AllAreas))
            {
                continue;
            }

            Vector3 eye = sample.position + Vector3.up * 1.15f;
            Vector3 threatEye = threat + Vector3.up * 1.2f;
            Vector3 direction = threatEye - eye;
            float distance = direction.magnitude;
            int hitCount = Physics.RaycastNonAlloc(
                eye, direction.normalized, _hits, distance, obstacleMask,
                QueryTriggerInteraction.Ignore);
            if (hitCount <= 0)
            {
                continue;
            }

            float pathDistance = Vector3.Distance(origin, sample.position);
            float threatDistance = Vector3.Distance(sample.position, threat);
            float score = threatDistance * 0.15f - pathDistance * 0.35f;
            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            coverPosition = sample.position;
        }

        return bestScore > float.NegativeInfinity;
    }
}
