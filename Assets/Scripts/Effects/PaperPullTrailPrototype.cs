using System.Collections.Generic;
using UnityEngine;

public class PaperPullTrailPrototype : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private Transform frontPoint;

    [SerializeField]
    private Transform rearPoint;

    [Header("Trail Settings")]
    [SerializeField]
    [Min(2)]
    private int pointCount = 10;

    [Min(0.01f)]
    [SerializeField]
    private float pointSpacing = 0.25f;

    [Min(0.001f)]
    [SerializeField]
    private float recordInterval = 0.03f;

    [Header("Debug")]
    [Min(0.01f)]
    [SerializeField]
    private float gizmoRadius = 0.15f;

    private readonly List<Vector3> pathHistory = new();
    private readonly List<Vector3> trailPoints = new();

    private float recordTimer;

    private bool isRecording;

    private void Awake()
    {
        InitializePoints();
    }

    private void Update()
    {
        if (!isRecording || frontPoint == null)
        {
            return;
        }

        recordTimer += Time.deltaTime;

        if (recordTimer < recordInterval)
        {
            return;
        }

        recordTimer = 0f;

        RecordFrontPosition();
        UpdateTrailPoints();
    }

    public void BeginRecording()
    {
        if (frontPoint == null)
        {
            return;
        }

        isRecording = true;
        recordTimer = 0f;
        InitializePoints();

        if (
            frontPoint != null &&
            rearPoint != null &&
            pointCount > 1
        )
        {
            pointSpacing =
                Vector3.Distance(
                    frontPoint.position,
                    rearPoint.position
                )
                / (pointCount - 1);
        }
        UpdateTrailPoints();
    }

    public void EndRecording()
    {
        isRecording = false;
    }
    private void InitializePoints()
    {
        pathHistory.Clear();
        trailPoints.Clear();
        Vector3 frontPosition =
            frontPoint.position;
        Vector3 rearPosition =
            rearPoint != null
                ? rearPoint.position
                : frontPosition;

        for (int i = 0; i < pointCount; i++)
        {
            float t =
                pointCount > 1
                    ? (float)i / (pointCount -1)
                    : 0f;
            Vector3 pointPosition =
                Vector3.Lerp(
                    frontPosition,
                    rearPosition,
                    t
                );

            trailPoints.Add(pointPosition);
        }

        InitializePathHistory(
            frontPosition,
            rearPosition
        );
    }

    private void InitializePathHistory(
        Vector3 frontPosition,
        Vector3 rearPosition
    )
    {
        pathHistory.Clear();

        for (int i = pointCount - 1; i >= 0; i--)
        {
            float t =
                pointCount > 1
                    ? (float)i / (pointCount - 1)
                    : 0f;

            Vector3 pointPosition =
                Vector3.Lerp(
                    frontPosition,
                    rearPosition,
                    t
                );
            pathHistory.Add(pointPosition);
        }
    }
    private void RecordFrontPosition()
    {
        Vector3 currentPosition = frontPoint.position;

        if (pathHistory.Count == 0)
        {
            pathHistory.Add(currentPosition);
            return;
        }

        Vector3 lastPosition =
            pathHistory[pathHistory.Count -1];

        float distance =
            Vector3.Distance(
                lastPosition,
                currentPosition
            );
        
        if (distance < 0.001f)
        {
            return;
        }

        pathHistory.Add(currentPosition);

        TrimHistory();
    }

    private void UpdateTrailPoints()
    {
        if (trailPoints.Count != pointCount)
        {
            InitializePoints();
        }

        trailPoints[0] =
            frontPoint.position;

        for (int i = 1; i < pointCount; i++)
        {
            float targetDistance =
                pointSpacing * i;
            trailPoints[i] =
                GetPointBehindFront(
                    targetDistance
                );
        }
    }

    private Vector3 GetPointBehindFront(
        float targetDistance
    )
    {
        if (pathHistory.Count == 0)
        {
            return frontPoint.position;
        }

        float accumulatedDistance = 0f;

        Vector3 previousPosition =
            frontPoint.position;
        
        for (int i = pathHistory.Count -1; i >= 0; i--)
        {
            Vector3 currentPosition =
                pathHistory[i];
            
            float segmentDistance =
                Vector3.Distance(
                    previousPosition,
                    currentPosition
                );
            if (accumulatedDistance + segmentDistance >= targetDistance)
            {
                float remainingDistance =
                    targetDistance - accumulatedDistance;
                float t = segmentDistance > 0f
                    ? remainingDistance / segmentDistance
                    : 0f;
                return Vector3.Lerp(
                    previousPosition,
                    currentPosition,
                    t
                );
            }

            accumulatedDistance += segmentDistance;
            previousPosition = currentPosition;
        }
        return pathHistory[0];
    }

    private void TrimHistory()
    {
        float requiredLength =
            pointSpacing
            * (pointCount -1)
            + pointSpacing;

        float accumulatedDistance = 0f;

        for (
            int i= pathHistory.Count -1;
            i > 0;
            i--
        )
        {
            accumulatedDistance +=
                Vector3.Distance(
                    pathHistory[i],
                    pathHistory[i - 1]
                );
            if (accumulatedDistance >= requiredLength)
            {
                if (i > 0)
                {
                    pathHistory.RemoveRange(
                        0,
                        i
                    );
                }
                break;
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (frontPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(
                frontPoint.position,
                0.15f
            );
        }
        
        if (trailPoints == null ||
            trailPoints.Count == 0)
        {
            return;
        }

        for (
            int i = 0;
            i < trailPoints.Count;
            i++
        )
        {
            Gizmos.color =
                i == 0
                    ? Color.yellow
                    : Color.magenta;

            Gizmos.DrawSphere(
                trailPoints[i],
                gizmoRadius
            );

            if (i == 0)
            {
                continue;
            }

            Gizmos.color = Color.white;

            Gizmos.DrawLine(
                trailPoints[i - 1],
                trailPoints[i]
            );
        }
    }

}