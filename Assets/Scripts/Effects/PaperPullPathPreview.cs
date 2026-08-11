using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class PaperPullPathPreview : MonoBehaviour
{
    [Header("Bone Chain")]

    [Tooltip("PullBone_00を設定")]
    [SerializeField]
    private Transform firstBone;

    [Min(2)]
    [SerializeField]
    private int boneCount = 10;

    [Header("Path Points")]

    [Tooltip(
        "PullBone_09が通過する中継地点。"
        + "前半の高さと奥行きを調整する"
    )]
    [SerializeField]
    private Transform middlePoint;

    [Tooltip(
        "PullBone_09が最終的に到達する地点。"
        + "現在はSelectedPaperCenterPoint"
    )]
    [SerializeField]
    private Transform targetPoint;

    [Header("Curve Shape")]

    [Range(0.05f, 0.8f)]
    [SerializeField]
    private float firstHandleRatio = 0.33f;

    [Range(0.05f, 0.8f)]
    [SerializeField]
    private float middleInHandleRatio = 0.25f;

    [Range(0.05f, 0.8f)]
    [SerializeField]
    private float middleOutHandleRatio = 0.30f;

    [Range(0.05f, 0.8f)]
    [SerializeField]
    private float targetHandleRatio = 0.35f;

    [Header("Preview")]

    [Range(16, 200)]
    [SerializeField]
    private int curveSegmentCount = 80;

    [Tooltip(
        "0ではBoneが初期位置、"
        + "1ではPullBone_09がTarget Pointへ到達"
    )]
    [Range(0f, 1f)]
    [SerializeField]
    private float previewProgress;

    [Min(0.001f)]
    [SerializeField]
    private float pathPointRadius = 0.06f;

    [Min(0.001f)]
    [SerializeField]
    private float bonePointRadius = 0.09f;

    [Min(0f)]
    [SerializeField]
    private float tangentLength = 0.6f;

    [SerializeField]
    private bool drawControlPoints = true;

    [SerializeField]
    private bool drawInitialBoneChain = true;

    [SerializeField]
    private bool drawFollowingBones = true;

    private readonly List<Transform> bones =
        new List<Transform>();

    private readonly List<Vector3> sampledPath =
        new List<Vector3>();

    private readonly List<float> cumulativeLengths =
        new List<float>();

    private readonly List<float> initialBoneDistances =
        new List<float>();

    private bool runtimePathPrepared;

    private bool TryBuildPreview()
    {
        if (
            firstBone == null ||
            middlePoint == null ||
            targetPoint == null
        )
        {
            return false;
        }

        if (!CollectBones())
        {
            return false;
        }

        BuildSampledPath();

        return sampledPath.Count >= 2;
    }

    private bool CollectBones()
    {
        bones.Clear();

        Transform currentBone =
            firstBone;

        for (
            int i = 0;
            i < boneCount;
            i++
        )
        {
            if (currentBone == null)
            {
                bones.Clear();
                return false;
            }

            bones.Add(currentBone);

            if (i >= boneCount - 1)
            {
                continue;
            }

            if (currentBone.childCount == 0)
            {
                bones.Clear();
                return false;
            }

            currentBone =
                currentBone.GetChild(0);
        }

        return bones.Count == boneCount;
    }

    private void BuildSampledPath()
    {
        sampledPath.Clear();
        cumulativeLengths.Clear();
        initialBoneDistances.Clear();

        /*
         * 経路前半は、現在のBone列そのもの。
         *
         * PullBone_00
         * → 01
         * → …
         * → 09
         */
        for (
            int i = 0;
            i < bones.Count;
            i++
        )
        {
            AddSamplePoint(
                bones[i].position
            );

            initialBoneDistances.Add(
                cumulativeLengths[
                    cumulativeLengths.Count - 1
                ]
            );
        }

        Vector3 start =
            bones[bones.Count - 1].position;

        BuildControlPoints(
            start,
            out Vector3 firstControl1,
            out Vector3 firstControl2,
            out Vector3 secondControl1,
            out Vector3 secondControl2
        );

        int safeSegmentCount =
            Mathf.Max(
                4,
                curveSegmentCount
            );

        /*
         * 経路後半は、
         * PullBone_09からMiddle Pointまで。
         */
        for (
            int i = 1;
            i <= safeSegmentCount;
            i++
        )
        {
            float t =
                i /
                (float)safeSegmentCount;

            Vector3 position =
                EvaluateCubicBezier(
                    start,
                    firstControl1,
                    firstControl2,
                    middlePoint.position,
                    t
                );

            AddSamplePoint(position);
        }

        /*
         * Middle PointからTarget Pointまで。
         */
        for (
            int i = 1;
            i <= safeSegmentCount;
            i++
        )
        {
            float t =
                i /
                (float)safeSegmentCount;

            Vector3 position =
                EvaluateCubicBezier(
                    middlePoint.position,
                    secondControl1,
                    secondControl2,
                    targetPoint.position,
                    t
                );

            AddSamplePoint(position);
        }
    }

    private void AddSamplePoint(
        Vector3 position
    )
    {
        if (sampledPath.Count == 0)
        {
            sampledPath.Add(position);
            cumulativeLengths.Add(0f);
            return;
        }

        Vector3 previousPosition =
            sampledPath[
                sampledPath.Count - 1
            ];

        float previousLength =
            cumulativeLengths[
                cumulativeLengths.Count - 1
            ];

        float additionalLength =
            Vector3.Distance(
                previousPosition,
                position
            );

        sampledPath.Add(position);

        cumulativeLengths.Add(
            previousLength +
            additionalLength
        );
    }

    private void BuildControlPoints(
        Vector3 start,
        out Vector3 firstControl1,
        out Vector3 firstControl2,
        out Vector3 secondControl1,
        out Vector3 secondControl2
    )
    {
        Vector3 middle =
            middlePoint.position;

        Vector3 target =
            targetPoint.position;

        Vector3 firstVector =
            middle - start;

        Vector3 secondVector =
            target - middle;

        float firstDistance =
            Mathf.Max(
                0.001f,
                firstVector.magnitude
            );

        float secondDistance =
            Mathf.Max(
                0.001f,
                secondVector.magnitude
            );

        Vector3 firstDirection =
            firstVector.normalized;

        Vector3 secondDirection =
            secondVector.normalized;

        /*
        * MiddlePoint前後で共通して使用する方向。
        *
        * 入る曲線と出る曲線の両方で同じ方向を使うことで、
        * MiddlePointに「くの字」の角ができるのを防ぐ。
        */
        Vector3 middleDirection =
            firstDirection +
            secondDirection;

        if (
            middleDirection.sqrMagnitude <
            0.000001f
        )
        {
            middleDirection =
                secondDirection;
        }
        else
        {
            middleDirection.Normalize();
        }

        /*
        * Bone列から最初の曲線へ滑らかにつなぐ。
        *
        * 現在のコードではstartDirectionを計算しているのに、
        * firstDirectionを使っていたので、ここも修正する。
        */
        firstControl1 =
            start +
            firstDirection *
            firstDistance *
            firstHandleRatio;

        /*
        * MiddlePointへmiddleDirectionで入る。
        */
        firstControl2 =
            middle -
            middleDirection *
            firstDistance *
            middleInHandleRatio;

        /*
        * MiddlePointからも同じmiddleDirectionで出る。
        *
        * ここで別方向を使うと、MiddlePointに角ができる。
        */
        secondControl1 =
            middle +
            middleDirection *
            secondDistance *
            middleOutHandleRatio;

        /*
        * TargetPointへ向かって滑らかに収束する。
        */
        secondControl2 =
            target -
            secondDirection *
            secondDistance *
            targetHandleRatio;
    }

    public Vector3 EvaluateBonePosition(
        int boneIndex,
        float progress
    )
    {
        if (
            sampledPath.Count < 2 ||
            initialBoneDistances.Count != bones.Count
        )
        {
            return transform.position;
        }

        boneIndex =
            Mathf.Clamp(
                boneIndex,
                0,
                bones.Count - 1
            );

        progress =
            Mathf.Clamp01(progress);

        /*
         * 初期状態で09が存在する、
         * 経路上の距離。
         */
        float initialHeadDistance =
            initialBoneDistances[
                initialBoneDistances.Count - 1
            ];

        float totalPathDistance =
            cumulativeLengths[
                cumulativeLengths.Count - 1
            ];

        /*
         * 09が初期位置からTargetまで進む。
         */
        float headDistance =
            Mathf.Lerp(
                initialHeadDistance,
                totalPathDistance,
                progress
            );

        /*
         * 各Boneと09との初期距離を維持する。
         *
         * これにより、
         * 08は09の後ろ、
         * 07は08の後ろへ配置される。
         */
        float distanceBehindHead =
            initialHeadDistance -
            initialBoneDistances[boneIndex];

        float bonePathDistance =
            headDistance -
            distanceBehindHead;

        return EvaluatePositionByDistance(
            bonePathDistance
        );
    }

    public Vector3 EvaluateBoneTangent(
        int boneIndex,
        float progress
    )
    {
        if (sampledPath.Count < 2)
        {
            return Vector3.forward;
        }

        boneIndex =
            Mathf.Clamp(
                boneIndex,
                0,
                bones.Count - 1
            );

        progress =
            Mathf.Clamp01(progress);

        float initialHeadDistance =
            initialBoneDistances[
                initialBoneDistances.Count - 1
            ];

        float totalPathDistance =
            cumulativeLengths[
                cumulativeLengths.Count - 1
            ];

        float headDistance =
            Mathf.Lerp(
                initialHeadDistance,
                totalPathDistance,
                progress
            );

        float distanceBehindHead =
            initialHeadDistance -
            initialBoneDistances[boneIndex];

        float bonePathDistance =
            headDistance -
            distanceBehindHead;

        return EvaluateTangentByDistance(
            bonePathDistance
        );
    }

    private Vector3 EvaluatePositionByDistance(
        float distance
    )
    {
        if (sampledPath.Count == 0)
        {
            return transform.position;
        }

        distance =
            Mathf.Clamp(
                distance,
                0f,
                cumulativeLengths[
                    cumulativeLengths.Count - 1
                ]
            );

        int upperIndex =
            FindUpperSampleIndex(
                distance
            );

        if (upperIndex <= 0)
        {
            return sampledPath[0];
        }

        float lowerDistance =
            cumulativeLengths[
                upperIndex - 1
            ];

        float upperDistance =
            cumulativeLengths[
                upperIndex
            ];

        float segmentProgress =
            Mathf.InverseLerp(
                lowerDistance,
                upperDistance,
                distance
            );

        return Vector3.Lerp(
            sampledPath[upperIndex - 1],
            sampledPath[upperIndex],
            segmentProgress
        );
    }

    private Vector3 EvaluateTangentByDistance(
        float distance
    )
    {
        if (sampledPath.Count < 2)
        {
            return Vector3.forward;
        }

        distance =
            Mathf.Clamp(
                distance,
                0f,
                cumulativeLengths[
                    cumulativeLengths.Count - 1
                ]
            );

        int upperIndex =
            FindUpperSampleIndex(
                distance
            );

        int lowerIndex =
            Mathf.Max(
                0,
                upperIndex - 1
            );

        int nextIndex =
            Mathf.Min(
                sampledPath.Count - 1,
                upperIndex + 1
            );

        Vector3 tangent =
            sampledPath[nextIndex] -
            sampledPath[lowerIndex];

        if (
            tangent.sqrMagnitude <
            0.000001f
        )
        {
            return Vector3.forward;
        }

        return tangent.normalized;
    }

    private int FindUpperSampleIndex(
        float distance
    )
    {
        for (
            int i = 1;
            i < cumulativeLengths.Count;
            i++
        )
        {
            if (
                cumulativeLengths[i] >=
                distance
            )
            {
                return i;
            }
        }

        return cumulativeLengths.Count - 1;
    }

    private void OnDrawGizmos()
    {
        if (
            !Application.isPlaying ||
            !runtimePathPrepared
        )
        {
            if (!TryBuildPreview())
            {
                return;
            }            
        }

        DrawCompletePath();

        if (drawInitialBoneChain)
        {
            DrawInitialBones();
        }

        if (drawFollowingBones)
        {
            DrawFollowingBonePreview();
        }

        DrawPathPointsAndControls();
    }

    private void DrawCompletePath()
    {
        Gizmos.color =
            Color.cyan;

        for (
            int i = 1;
            i < sampledPath.Count;
            i++
        )
        {
            Gizmos.DrawLine(
                sampledPath[i - 1],
                sampledPath[i]
            );
        }
    }

    private void DrawInitialBones()
    {
        Gizmos.color =
            new Color(
                0.45f,
                0.45f,
                0.45f,
                1f
            );

        for (
            int i = 0;
            i < bones.Count;
            i++
        )
        {
            Gizmos.DrawWireSphere(
                bones[i].position,
                bonePointRadius * 0.75f
            );
        }
    }

    private void DrawFollowingBonePreview()
    {
        Vector3 previousPosition =
            Vector3.zero;

        for (
            int i = 0;
            i < bones.Count;
            i++
        )
        {
            Vector3 position =
                EvaluateBonePosition(
                    i,
                    previewProgress
                );

            /*
             * 00側は暗め、09側は明るめ。
             */
            float chainProgress =
                bones.Count > 1
                    ? i /
                      (float)(bones.Count - 1)
                    : 0f;

            Gizmos.color =
                Color.Lerp(
                    new Color(
                        0.2f,
                        0.5f,
                        1f,
                        1f
                    ),
                    Color.white,
                    chainProgress
                );

            Gizmos.DrawSphere(
                position,
                bonePointRadius
            );

            if (i > 0)
            {
                Gizmos.DrawLine(
                    previousPosition,
                    position
                );
            }

            previousPosition =
                position;
        }

        Vector3 headPosition =
            EvaluateBonePosition(
                bones.Count - 1,
                previewProgress
            );

        Vector3 headTangent =
            EvaluateBoneTangent(
                bones.Count - 1,
                previewProgress
            );

        Gizmos.color =
            Color.white;

        Gizmos.DrawLine(
            headPosition,
            headPosition +
            headTangent *
            tangentLength
        );
    }

    private void DrawPathPointsAndControls()
    {
        Vector3 start =
            bones[bones.Count - 1].position;

        BuildControlPoints(
            start,
            out Vector3 firstControl1,
            out Vector3 firstControl2,
            out Vector3 secondControl1,
            out Vector3 secondControl2
        );

        Gizmos.color =
            Color.red;

        Gizmos.DrawSphere(
            start,
            pathPointRadius
        );

        Gizmos.color =
            Color.yellow;

        Gizmos.DrawSphere(
            middlePoint.position,
            pathPointRadius
        );

        Gizmos.color =
            Color.green;

        Gizmos.DrawSphere(
            targetPoint.position,
            pathPointRadius
        );

        if (!drawControlPoints)
        {
            return;
        }

        Gizmos.color =
            Color.gray;

        Gizmos.DrawLine(
            start,
            firstControl1
        );

        Gizmos.DrawLine(
            firstControl1,
            firstControl2
        );

        Gizmos.DrawLine(
            firstControl2,
            middlePoint.position
        );

        Gizmos.DrawLine(
            middlePoint.position,
            secondControl1
        );

        Gizmos.DrawLine(
            secondControl1,
            secondControl2
        );

        Gizmos.DrawLine(
            secondControl2,
            targetPoint.position
        );

        Gizmos.DrawWireSphere(
            firstControl1,
            pathPointRadius * 0.6f
        );

        Gizmos.DrawWireSphere(
            firstControl2,
            pathPointRadius * 0.6f
        );

        Gizmos.DrawWireSphere(
            secondControl1,
            pathPointRadius * 0.6f
        );

        Gizmos.DrawWireSphere(
            secondControl2,
            pathPointRadius * 0.6f
        );
    }

    private static Vector3 EvaluateCubicBezier(
        Vector3 point0,
        Vector3 point1,
        Vector3 point2,
        Vector3 point3,
        float progress
    )
    {
        float inverse =
            1f - progress;

        return
            inverse *
            inverse *
            inverse *
            point0 +

            3f *
            inverse *
            inverse *
            progress *
            point1 +

            3f *
            inverse *
            progress *
            progress *
            point2 +

            progress *
            progress *
            progress *
            point3;
    }
    /// <summary>
    /// 実行時に、現在のBone位置を基準として
    /// 経路とBone間隔を構築する。
    /// </summary>
    public bool PrepareRuntimePath()
    {
        
        runtimePathPrepared =
            TryBuildPreview();
        
        return runtimePathPrepared;

    }

    public void ReleaseRuntimePath()
    {
        runtimePathPrepared = false;
    }
}