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
        out Vector3 bestPosition,
        out Vector3 bestPeekPosition)
    {
        bestPosition = origin;
        bestPeekPosition = origin;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < CandidateCount; i++)
        {
            float angle = i * 360f / CandidateCount;
            float distance = Mathf.Lerp(radius * 0.35f, radius, (i % 4) / 3f);
            Vector3 raw = origin + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * distance;
            if (!NavMesh.SamplePosition(raw, out NavMeshHit sample, 2.5f, NavMesh.AllAreas) ||
                !NavMesh.CalculatePath(origin, sample.position, NavMesh.AllAreas, _path) ||
                _path.status != NavMeshPathStatus.PathComplete)
            {
                continue;
            }
            Vector3 eye = sample.position + Vector3.up * 1.15f;
            Vector3 toThreat = threat + Vector3.up * 1.15f - eye;
            bool blocked = HasBlockingHit(eye, toThreat, mask, threatActor);
            if (!blocked)
            {
                continue;
            }
            Vector3 peekPosition = FindPeekPosition(sample.position, threat, mask, threatActor);
            float peek = Vector3.SqrMagnitude(peekPosition - sample.position) > 0.05f
                ? 1f
                : 0f;
            float protect = blocked ? 1f : 0f;
            float range = 1f - Mathf.Clamp01(
                Mathf.Abs(Vector3.Distance(sample.position, threat) - preferredRange) /
                Mathf.Max(1f, preferredRange));
            float pathLength = CalculatePathLength(_path);
            float travel = 1f - Mathf.Clamp01(pathLength / Mathf.Max(1f, radius * 1.6f));
            float score = protect * 1.3f + peek * 0.35f + range * 0.45f + travel * 0.35f;
            if (score > bestScore)
            {
                bestScore = score;
                bestPosition = sample.position;
                bestPeekPosition = peekPosition;
            }
        }
        return bestScore > 0.9f;
    }

    private Vector3 FindPeekPosition(
        Vector3 coverPosition,
        Vector3 threat,
        LayerMask mask,
        CharacterHealth threatActor)
    {
        Vector3 away = coverPosition - threat;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f)
        {
            return coverPosition;
        }

        Vector3 side = Vector3.Cross(Vector3.up, away.normalized);
        for (int direction = -1; direction <= 1; direction += 2)
        {
            Vector3 raw = coverPosition + side * direction * 0.85f;
            if (!NavMesh.SamplePosition(raw, out NavMeshHit sample, 0.8f, NavMesh.AllAreas))
            {
                continue;
            }

            Vector3 eye = sample.position + Vector3.up * 1.35f;
            Vector3 toThreat = threat + Vector3.up * 1.15f - eye;
            if (!HasBlockingHit(eye, toThreat, mask, threatActor))
            {
                return sample.position;
            }
        }

        return coverPosition;
    }

    private bool HasBlockingHit(
        Vector3 origin,
        Vector3 direction,
        LayerMask mask,
        CharacterHealth threatActor)
    {
        int count = Physics.RaycastNonAlloc(
            origin,
            direction.normalized,
            _hits,
            direction.magnitude,
            mask,
            QueryTriggerInteraction.Ignore);
        float nearestDistance = float.PositiveInfinity;
        Transform nearest = null;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = _hits[i];
            if (!hit.transform || hit.distance >= nearestDistance)
            {
                continue;
            }
            nearestDistance = hit.distance;
            nearest = hit.transform;
        }

        return nearest && (!threatActor || !nearest.IsChildOf(threatActor.transform));
    }

    private static float CalculatePathLength(NavMeshPath path)
    {
        float length = 0f;
        Vector3[] corners = path.corners;
        for (int i = 1; i < corners.Length; i++)
        {
            length += Vector3.Distance(corners[i - 1], corners[i]);
        }
        return length;
    }
}
