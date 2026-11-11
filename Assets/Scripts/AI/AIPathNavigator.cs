using UnityEngine;
using UnityEngine.AI;

public sealed class AIPathNavigator
{
    private const float CornerReachedDistance = 0.65f;
    private readonly NavMeshPath _path = new NavMeshPath();
    private int _cornerIndex;

    public bool HasPath
    {
        get
        {
            return _path.status != NavMeshPathStatus.PathInvalid &&
                _path.corners != null && _path.corners.Length > 1;
        }
    }

    public bool Calculate(Vector3 origin, Vector3 destination)
    {
        _path.ClearCorners();
        _cornerIndex = 1;
        if (!NavMesh.SamplePosition(origin, out NavMeshHit start, 3f, NavMesh.AllAreas) ||
            !NavMesh.SamplePosition(destination, out NavMeshHit end, 6f, NavMesh.AllAreas))
        {
            return false;
        }

        return NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, _path) &&
            _path.status != NavMeshPathStatus.PathInvalid && _path.corners.Length > 1;
    }

    public Vector3 GetDirection(Vector3 position)
    {
        if (!HasPath)
        {
            return Vector3.zero;
        }

        while (_cornerIndex < _path.corners.Length - 1 &&
            Vector3.Distance(position, _path.corners[_cornerIndex]) <= CornerReachedDistance)
        {
            _cornerIndex++;
        }

        Vector3 direction = _path.corners[_cornerIndex] - position;
        direction.y = 0f;
        return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.zero;
    }

    public bool HasArrived(Vector3 position, float stoppingDistance)
    {
        if (!HasPath)
        {
            return true;
        }

        Vector3 destination = _path.corners[_path.corners.Length - 1];
        destination.y = position.y;
        return Vector3.Distance(position, destination) <= stoppingDistance;
    }
}
