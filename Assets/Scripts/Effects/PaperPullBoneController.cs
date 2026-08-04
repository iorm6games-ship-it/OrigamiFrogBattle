using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PaperPullBoneController : MonoBehaviour
{
    [Serializable]
    private struct PathPose
    {
        public Vector3 position;
        public Quaternion rotation;
        public float distance;

        public PathPose(
            Vector3 position,
            Quaternion rotation,
            float distance
        )
        {
            this.position = position;
            this.rotation = rotation;
            this.distance = distance;
        }
    }

    [Header("Bone Chain")]

    [Tooltip("階層のルートBone。現在はPullBone_00")]
    [SerializeField]
    private Transform firstBone;

    [Min(2)]
    [SerializeField]
    private int boneCount = 10;

    [Header("References")]

    [Tooltip("紙全体のTransform")]
    [SerializeField]
    private Transform paperReference;

    [Header("Complete Path")]

    [Tooltip("開始地点から終点までのカーブ分割数")]
    [Range(16, 256)]
    [SerializeField]
    private int curveSampleCount = 96;

    [Tooltip("カーブのハンドル長。開始点と終点の距離に対する割合")]
    [Range(0.05f, 0.8f)]
    [SerializeField]
    private float curveHandleRatio = 0.3f;

    [Tooltip("経路を紙面から浮かせる距離")]
    [Min(0f)]
    [SerializeField]
    private float curveLiftDistance = 0.35f;

    [Tooltip("浮かせる方向に使う紙のローカル法線")]
    [SerializeField]
    private Vector3 localPaperNormal = Vector3.up;

    [Tooltip("浮かせる方向を反転する")]
    [SerializeField]
    private bool invertLiftDirection;

    [Header("Debug")]

    [SerializeField]
    private bool logBoneInformation;

    private Transform[] pullBones;

    private Vector3[] restWorldPositions;
    private Quaternion[] restWorldRotations;

    private Vector3[] restLocalPositions;
    private Quaternion[] restLocalRotations;

    private Vector3[] targetWorldPositions;
    private Quaternion[] targetWorldRotations;

    private float[] cumulativeBoneDistances;

    private readonly List<PathPose> pathPoses =
        new List<PathPose>();

    private float initialChainLength;
    private float completeTravelLength;

    private Vector3 restPaperPosition;
    private Quaternion restPaperRotation;

    public bool IsInitialized { get; private set; }
    public bool IsPathPrepared { get; private set; }

    public int BoneCount =>
        pullBones != null
            ? pullBones.Length
            : 0;

    private void Awake()
    {
        InitializeBoneChain();
    }

    [ContextMenu("Initialize Bone Chain")]
    public void InitializeBoneChain()
    {
        IsInitialized = false;
        IsPathPrepared = false;

        if (firstBone == null)
        {
            Debug.LogWarning(
                $"{name}: First Boneが未設定です。",
                this
            );

            return;
        }

        if (paperReference == null)
        {
            Debug.LogWarning(
                $"{name}: Paper Referenceが未設定です。",
                this
            );

            return;
        }

        if (!CollectBoneChain())
        {
            return;
        }

        IsInitialized = true;

        if (logBoneInformation)
        {
            LogBoneInformation();
        }
    }

    /// <summary>
    /// 現在の紙の姿勢を開始状態として保存し、
    /// targetPointを紙全体の最終Poseとして完全経路を作る。
    /// </summary>
    public bool PreparePath(
        Transform targetPoint
    )
    {
        if (targetPoint == null)
        {
            Debug.LogWarning(
                $"{name}: 最終地点が未設定です。",
                this
            );

            return false;
        }

        /*
         * ドラッグ開始時のBone姿勢を改めて保存する。
         * 降下演出後の正しい配置を開始姿勢にするため。
         */
        if (!CollectBoneChain())
        {
            IsInitialized = false;
            IsPathPrepared = false;
            return false;
        }

        restPaperPosition =
            paperReference.position;

        restPaperRotation =
            paperReference.rotation;

        CalculateTargetBonePoses(
            targetPoint
        );

        BuildCompletePath();

        IsInitialized = true;
        IsPathPrepared =
            pathPoses.Count >= 2 &&
            completeTravelLength > 0.0001f;

        if (
            IsPathPrepared &&
            logBoneInformation
        )
        {
            Debug.Log(
                $"{name}: 完全経路を作成しました。"
                + $" 経路移動量={completeTravelLength:F4}",
                this
            );
        }

        return IsPathPrepared;
    }

    /// <summary>
    /// 0～1を完全経路の進捗として適用する。
    /// </summary>
    public void ApplyPullAmount(
        float progress
    )
    {
        if (
            !IsInitialized ||
            !IsPathPrepared
        )
        {
            return;
        }

        progress =
            Mathf.Clamp01(progress);

        /*
         * progress=0:
         * PullBone_09が初期位置にいる状態
         *
         * progress=1:
         * PullBone_09が最終Poseの先端位置にいる状態
         */
        float tipDistance =
            initialChainLength +
            completeTravelLength *
            progress;

        /*
         * Transform階層が00→09なので、
         * 必ず親から子の順でWorld Poseを適用する。
         */
        for (
            int i = 0;
            i < pullBones.Length;
            i++
        )
        {
            float distanceBehindTip =
                initialChainLength -
                cumulativeBoneDistances[i];

            float sampleDistance =
                tipDistance -
                distanceBehindTip;

            PathPose pose =
                SamplePathByDistance(
                    sampleDistance
                );

            pullBones[i].SetPositionAndRotation(
                pose.position,
                pose.rotation
            );
        }
    }

    public void ResetBonesImmediately()
    {
        if (!IsInitialized)
        {
            InitializeBoneChain();
        }

        if (!IsInitialized)
        {
            return;
        }

        for (
            int i = 0;
            i < pullBones.Length;
            i++
        )
        {
            pullBones[i].localPosition =
                restLocalPositions[i];

            pullBones[i].localRotation =
                restLocalRotations[i];
        }
    }

    private bool CollectBoneChain()
    {
        pullBones =
            new Transform[boneCount];

        restWorldPositions =
            new Vector3[boneCount];

        restWorldRotations =
            new Quaternion[boneCount];

        restLocalPositions =
            new Vector3[boneCount];

        restLocalRotations =
            new Quaternion[boneCount];

        targetWorldPositions =
            new Vector3[boneCount];

        targetWorldRotations =
            new Quaternion[boneCount];

        cumulativeBoneDistances =
            new float[boneCount];

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
                Debug.LogError(
                    $"{name}: {i}番目のBoneを取得できません。",
                    this
                );

                return false;
            }

            pullBones[i] =
                currentBone;

            restWorldPositions[i] =
                currentBone.position;

            restWorldRotations[i] =
                currentBone.rotation;

            restLocalPositions[i] =
                currentBone.localPosition;

            restLocalRotations[i] =
                currentBone.localRotation;

            if (i > 0)
            {
                float distance =
                    Vector3.Distance(
                        restWorldPositions[i - 1],
                        restWorldPositions[i]
                    );

                cumulativeBoneDistances[i] =
                    cumulativeBoneDistances[i - 1] +
                    distance;
            }

            if (i < boneCount - 1)
            {
                if (currentBone.childCount == 0)
                {
                    Debug.LogError(
                        $"{currentBone.name}に"
                        + "次の子Boneがありません。",
                        currentBone
                    );

                    return false;
                }

                currentBone =
                    currentBone.GetChild(0);
            }
        }

        initialChainLength =
            cumulativeBoneDistances[
                cumulativeBoneDistances.Length - 1
            ];

        return true;
    }

    /// <summary>
    /// 紙全体がtargetPointのPoseになった場合に、
    /// 各Boneが来るべきWorld Poseを計算する。
    /// </summary>
    private void CalculateTargetBonePoses(
        Transform targetPoint
    )
    {
        Quaternion paperRotationInverse =
            Quaternion.Inverse(
                restPaperRotation
            );

        for (
            int i = 0;
            i < pullBones.Length;
            i++
        )
        {
            /*
             * 紙全体に対するBoneの相対位置。
             * Scaleの影響を追加せず、紙の実寸を維持する。
             */
            Vector3 relativePosition =
                paperRotationInverse *
                (
                    restWorldPositions[i] -
                    restPaperPosition
                );

            Quaternion relativeRotation =
                paperRotationInverse *
                restWorldRotations[i];

            targetWorldPositions[i] =
                targetPoint.position +
                targetPoint.rotation *
                relativePosition;

            targetWorldRotations[i] =
                targetPoint.rotation *
                relativeRotation;
        }
    }

    private void BuildCompletePath()
    {
        pathPoses.Clear();

        float cumulativeDistance = 0f;

        /*
         * 初期Boneチェーンを00→09で登録。
         * progress=0で全Boneが初期位置へ並ぶために必要。
         */
        for (
            int i = 0;
            i < pullBones.Length;
            i++
        )
        {
            AddPathPose(
                restWorldPositions[i],
                restWorldRotations[i],
                ref cumulativeDistance
            );
        }

        initialChainLength =
            cumulativeDistance;

        int tipIndex =
            pullBones.Length - 1;

        Vector3 curveStart =
            restWorldPositions[tipIndex];

        /*
         * カーブの終点は最終姿勢のPullBone_00。
         * その後に最終Boneチェーン00→09を接続する。
         */
        Vector3 curveEnd =
            targetWorldPositions[0];

        Quaternion curveStartRotation =
            restWorldRotations[tipIndex];

        Quaternion curveEndRotation =
            targetWorldRotations[0];

        Vector3 startDirection =
            GetInitialTipDirection();

        Vector3 targetDirection =
            GetTargetChainDirection();

        float directDistance =
            Vector3.Distance(
                curveStart,
                curveEnd
            );

        float handleDistance =
            Mathf.Max(
                0.01f,
                directDistance *
                curveHandleRatio
            );

        Vector3 liftDirection =
            paperReference.TransformDirection(
                localPaperNormal.normalized
            );

        if (invertLiftDirection)
        {
            liftDirection =
                -liftDirection;
        }

        Vector3 controlPoint1 =
            curveStart +
            startDirection *
            handleDistance +
            liftDirection *
            curveLiftDistance;

        Vector3 controlPoint2 =
            curveEnd -
            targetDirection *
            handleDistance +
            liftDirection *
            curveLiftDistance;

        for (
            int sample = 1;
            sample <= curveSampleCount;
            sample++
        )
        {
            float t =
                sample /
                (float)curveSampleCount;

            Vector3 position =
                EvaluateCubicBezier(
                    curveStart,
                    controlPoint1,
                    controlPoint2,
                    curveEnd,
                    t
                );

            float easedRotation =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    t
                );

            Quaternion rotation =
                Quaternion.Slerp(
                    curveStartRotation,
                    curveEndRotation,
                    easedRotation
                );

            AddPathPose(
                position,
                rotation,
                ref cumulativeDistance
            );
        }

        /*
         * カーブ終点はtargetBone00なので、
         * targetBone01から09までを追加する。
         *
         * 経路末尾のチェーン形状が、
         * 完成時のBone配置そのものになる。
         */
        for (
            int i = 1;
            i < pullBones.Length;
            i++
        )
        {
            AddPathPose(
                targetWorldPositions[i],
                targetWorldRotations[i],
                ref cumulativeDistance
            );
        }

        completeTravelLength =
            cumulativeDistance -
            initialChainLength;
    }

    private void AddPathPose(
        Vector3 position,
        Quaternion rotation,
        ref float cumulativeDistance
    )
    {
        if (pathPoses.Count > 0)
        {
            PathPose previous =
                pathPoses[
                    pathPoses.Count - 1
                ];

            cumulativeDistance +=
                Vector3.Distance(
                    previous.position,
                    position
                );
        }

        pathPoses.Add(
            new PathPose(
                position,
                rotation,
                cumulativeDistance
            )
        );
    }

    private Vector3 GetInitialTipDirection()
    {
        int tipIndex =
            pullBones.Length - 1;

        Vector3 direction =
            restWorldPositions[tipIndex] -
            restWorldPositions[tipIndex - 1];

        if (direction.sqrMagnitude < 0.000001f)
        {
            return -paperReference.up;
        }

        return direction.normalized;
    }

    private Vector3 GetTargetChainDirection()
    {
        Vector3 direction =
            targetWorldPositions[1] -
            targetWorldPositions[0];

        if (direction.sqrMagnitude < 0.000001f)
        {
            return paperReference.up;
        }

        return direction.normalized;
    }

    private static Vector3 EvaluateCubicBezier(
        Vector3 start,
        Vector3 control1,
        Vector3 control2,
        Vector3 end,
        float t
    )
    {
        float inverseT =
            1f - t;

        return
            inverseT * inverseT * inverseT * start +
            3f * inverseT * inverseT * t * control1 +
            3f * inverseT * t * t * control2 +
            t * t * t * end;
    }

    private PathPose SamplePathByDistance(
        float targetDistance
    )
    {
        if (targetDistance <= 0f)
        {
            return pathPoses[0];
        }

        PathPose lastPose =
            pathPoses[
                pathPoses.Count - 1
            ];

        if (targetDistance >= lastPose.distance)
        {
            return lastPose;
        }

        for (
            int i = 1;
            i < pathPoses.Count;
            i++
        )
        {
            PathPose next =
                pathPoses[i];

            if (next.distance < targetDistance)
            {
                continue;
            }

            PathPose previous =
                pathPoses[i - 1];

            float segmentLength =
                next.distance -
                previous.distance;

            float amount =
                segmentLength > 0.000001f
                    ? (
                        targetDistance -
                        previous.distance
                    ) / segmentLength
                    : 0f;

            return new PathPose(
                Vector3.Lerp(
                    previous.position,
                    next.position,
                    amount
                ),
                Quaternion.Slerp(
                    previous.rotation,
                    next.rotation,
                    amount
                ),
                targetDistance
            );
        }

        return lastPose;
    }

    private void LogBoneInformation()
    {
        Debug.Log(
            $"{name}: Bone数={BoneCount}, "
            + $"Boneチェーン長={initialChainLength:F6}",
            this
        );

        for (
            int i = 1;
            i < pullBones.Length;
            i++
        )
        {
            float segmentDistance =
                cumulativeBoneDistances[i] -
                cumulativeBoneDistances[i - 1];

            Debug.Log(
                $"{pullBones[i - 1].name} → "
                + $"{pullBones[i].name}: "
                + $"{segmentDistance:F6}",
                pullBones[i]
            );
        }
    }
}