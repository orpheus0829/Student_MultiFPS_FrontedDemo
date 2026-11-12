using UnityEngine;
using UnityEngine.AI;

public sealed class AICoverEvaluator
{
    private const int CandidateCount = 24;
    private readonly RaycastHit[] _hits = new RaycastHit[16];
    private readonly NavMeshPath _path = new NavMeshPath();

    public bool TryFind(
        Vector3 origin,
        Vector3 threat,
        float radius,
        LayerMask mask,
        float preferredRange,
        CharacterHealth threatActor,
        out Vector3 bestPosition)
    {
        bestPosition = origin;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < CandidateCount; i++)
        {
            float angle = i * 360f / CandidateCount;
            float distance = Mathf.Lerp(radius * 0.35f, radius, (i % 4) / 3f);
            Vector3 raw = origin + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * distance;
            if (!NavMesh.SamplePosition(raw, out NavMeshHit sample, 2.5f, NavMesh.AllAreas) ||
                !NavMesh.CalculatePath(origin, sample.position, NavMesh.AllAreas, _path) ||
                _path.status == NavMeshPathStatus.PathInvalid)
            {
                continue;
            }
            Vector3 eye = sample.position + Vector3.up * 1.15f;
            Vector3 toThreat = threat + Vector3.up * 1.15f - eye;
            int hits = Physics.RaycastNonAlloc(eye, toThreat.normalized, _hits,
                toThreat.magnitude, mask, QueryTriggerInteraction.Ignore);
            bool blocked = false;
            for (int hitIndex = 0; hitIndex < hits; hitIndex++)
            {
                Transform hitTransform = _hits[hitIndex].transform;
                if (!hitTransform || (threatActor &&
                    hitTransform.IsChildOf(threatActor.transform)))
                {
                    continue;
                }
                blocked = true;
                break;
            }
            float protect = blocked ? 1f : 0f;
            float range = 1f - Mathf.Clamp01(
                Mathf.Abs(Vector3.Distance(sample.position, threat) - preferredRange) /
                Mathf.Max(1f, preferredRange));
            float travel = 1f - Mathf.Clamp01(Vector3.Distance(origin, sample.position) / radius);
            float score = protect * 1.3f + range * 0.5f + travel * 0.35f;
            if (score > bestScore)
            {
                bestScore = score;
                bestPosition = sample.position;
            }
        }
        return bestScore > 0.9f;
    }
}
