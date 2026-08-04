using UnityEngine;

[DisallowMultipleComponent]
public sealed class PaperPullBoneController : MonoBehaviour
{
    [Header("Bone Chain")]

    [Tooltip("階層ルート。PullBone_00を設定")]
    [SerializeField]
    private Transform firstBone;

    [Min(2)]
    [SerializeField]
    private int boneCount = 10;

    [Header("References")]

    [Tooltip("Paper_Redを設定")]
    [SerializeField]
    private Transform paperReference;

    [Tooltip("Main Cameraを設定")]
    [SerializeField]
    private Camera targetCamera;
    [Header("Single Path")]

    [Tooltip(
        "経路途中の膨らみ方。" +
        "0と1では必ず0になるようにする"
    )]
    [SerializeField]
    private AnimationCurve pathArcCurve =
        new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.5f, 1f),
            new Keyframe(1f, 0f)
        );

    [Tooltip("経路をカメラ手前へ持ち上げる距離")]
    [Min(0f)]
    [SerializeField]
    private float pathFrontOffset = 0.45f;

    [Tooltip("カメラ手前方向を反転する")]
    [SerializeField]
    private bool invertFrontDirection;

    [Tooltip("経路上の進み方")]
    [SerializeField]
    private AnimationCurve pathProgressCurve =
        AnimationCurve.EaseInOut(
            0f,
            0f,
            1f,
            1f
        );

    [Header("Continuous Bone Bend")]

    [Tooltip(
        "Bone列を紙面手前へ曲げる最大距離。"
        + "角度ではなく位置変形"
    )]
    [Min(0f)]
    [SerializeField]
    private float maximumBendOffset = 0.15f;

    [Tooltip(
        "湾曲が影響するBoneの幅。"
        + "小さすぎると裂けたように見える"
    )]
    [Range(2f, 9f)]
    [SerializeField]
    private float bendWaveWidth = 5f;

    [Tooltip(
        "09側から00側へ湾曲が流れる進み方"
    )]
    [SerializeField]
    private AnimationCurve bendEnvelope =
        new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.15f, 0.75f),
            new Keyframe(0.4f, 1f),
            new Keyframe(0.75f, 0.55f),
            new Keyframe(1f, 0f)
        );

    [Tooltip(
        "Boneの長手方向ローカル軸。"
        + "Blender Boneは通常Y軸"
    )]
    [SerializeField]
    private Vector3 localBoneForwardAxis =
        Vector3.up;

    [Header("Debug")]

    [SerializeField]
    private bool logInformation;

    private Transform[] pullBones;

    private Vector3[] bindLocalPositions;
    private Quaternion[] bindLocalRotations;

    private Vector3[] startWorldPositions;
    private Quaternion[] startWorldRotations;

    private Vector3[] targetWorldPositions;
    private Quaternion[] targetWorldRotations;

    private Vector3[] currentWorldPositions;
    private Quaternion[] currentWorldRotations;

    private Vector3 startPaperPosition;
    private Quaternion startPaperRotation;

    private Vector3 targetPaperPosition;
    private Quaternion targetPaperRotation;

    private Vector3 pathArcDirection;
    private Vector3 bendDirection;

    public bool IsInitialized
    {
        get;
        private set;
    }

    public bool IsPathPrepared
    {
        get;
        private set;
    }

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

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (!CollectBoneChain())
        {
            return;
        }

        IsInitialized = true;

        if (logInformation)
        {
            Debug.Log(
                $"{name}: Boneチェーン初期化完了。"
                + $" Bone数={pullBones.Length}",
                this
            );
        }
    }

    public bool PreparePath(
        Transform targetPoint
    )
    {
        if (targetPoint == null)
        {
            Debug.LogWarning(
                $"{name}: SelectedPaperCenterPointが未設定です。",
                this
            );

            IsPathPrepared = false;
            return false;
        }

        if (!IsInitialized)
        {
            InitializeBoneChain();
        }

        if (!IsInitialized)
        {
            return false;
        }

        /*
         * 前回の変形が残っていた場合に備え、
         * Boneを初期Local Poseへ戻す。
         */
        RestoreBindPose();

        startPaperPosition =
            paperReference.position;

        startPaperRotation =
            paperReference.rotation;

        targetPaperPosition =
            targetPoint.position;

        targetPaperRotation =
            targetPoint.rotation;

        CacheStartWorldPoses();
        CalculateTargetWorldPoses();
        BuildPathOffsets();

        IsPathPrepared = true;

        ApplyPullAmount(0f);

        if (logInformation)
        {
            Debug.Log(
                $"{name}: 一本のBone経路を準備しました。"
                + $" Start={startPaperPosition},"
                + $" Target={targetPaperPosition}",
                this
            );
        }

        return true;
    }

    /// <summary>
    /// ドラッグ中と確定後の自動再生で共通使用する。
    /// 0が開始地点、1がSelectedPaperCenterPoint。
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

        progress = Mathf.Clamp01(progress);

        float pathProgress =
            Mathf.Clamp01(
                pathProgressCurve.Evaluate(
                    progress
                )
            );

        CalculateCurrentBonePositions(
            progress,
            pathProgress
        );

        CalculateCurrentBoneRotations(
            pathProgress
        );

        ApplyCurrentWorldPoses();
    }

    /// <summary>
    /// 選択確定後、見た目を変えずに
    /// Paper_Red本体を最終地点へ移し、
    /// Boneを通常のLocal Poseへ戻す。
    /// </summary>
    public void CommitTargetPose()
    {
        if (
            !IsInitialized ||
            !IsPathPrepared
        )
        {
            return;
        }

        paperReference.SetPositionAndRotation(
            targetPaperPosition,
            targetPaperRotation
        );

        RestoreBindPose();

        IsPathPrepared = false;
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

        RestoreBindPose();
        IsPathPrepared = false;
    }

    private bool CollectBoneChain()
    {
        pullBones =
            new Transform[boneCount];

        bindLocalPositions =
            new Vector3[boneCount];

        bindLocalRotations =
            new Quaternion[boneCount];

        startWorldPositions =
            new Vector3[boneCount];

        startWorldRotations =
            new Quaternion[boneCount];

        targetWorldPositions =
            new Vector3[boneCount];

        targetWorldRotations =
            new Quaternion[boneCount];

        currentWorldPositions =
            new Vector3[boneCount];

        currentWorldRotations =
            new Quaternion[boneCount];

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

            pullBones[i] = currentBone;

            bindLocalPositions[i] =
                currentBone.localPosition;

            bindLocalRotations[i] =
                currentBone.localRotation;

            if (i >= boneCount - 1)
            {
                continue;
            }

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

        return true;
    }

    private void CacheStartWorldPoses()
    {
        for (
            int i = 0;
            i < pullBones.Length;
            i++
        )
        {
            startWorldPositions[i] =
                pullBones[i].position;

            startWorldRotations[i] =
                pullBones[i].rotation;
        }
    }

    private void CalculateTargetWorldPoses()
    {
        Quaternion inverseStartRotation =
            Quaternion.Inverse(
                startPaperRotation
            );

        for (
            int i = 0;
            i < pullBones.Length;
            i++
        )
        {
            /*
             * 開始時のPaper_Redに対する
             * Boneの相対Poseを取得する。
             */
            Vector3 relativePosition =
                inverseStartRotation *
                (
                    startWorldPositions[i] -
                    startPaperPosition
                );

            Quaternion relativeRotation =
                inverseStartRotation *
                startWorldRotations[i];

            /*
             * Paper_RedがSelectedPaperCenterPointへ
             * 移動した場合のBone最終Pose。
             */
            targetWorldPositions[i] =
                targetPaperPosition +
                targetPaperRotation *
                relativePosition;

            targetWorldRotations[i] =
                targetPaperRotation *
                relativeRotation;
        }
    }

    private void BuildPathOffsets()
    {
        Vector3 directVector =
            targetPaperPosition -
            startPaperPosition;

        if (
            directVector.sqrMagnitude <
            0.000001f
        )
        {
            pathArcDirection =
                Vector3.zero;

            bendDirection =
                Vector3.zero;

            return;
        }

        Vector3 pathDirection =
            directVector.normalized;

        pathArcDirection =
            CalculatePathArcDirection(
                pathDirection
            );

        bendDirection =
            pathArcDirection;

        if (logInformation)
        {
            Debug.Log(
                $"{name}: 経路を準備しました。"
                + $" Start={startPaperPosition}"
                + $" Target={targetPaperPosition}"
                + $" Difference={directVector}"
                + $" ArcDirection={pathArcDirection}",
                this
            );
        }
    }
    private void CalculateCurrentBonePositions(
        float rawProgress,
        float pathProgress
    )
    {
        float lastIndex =
            pullBones.Length - 1f;

        float waveCenter =
            Mathf.Lerp(
                lastIndex,
                0f,
                rawProgress
            );

        float envelope =
            Mathf.Max(
                0f,
                bendEnvelope.Evaluate(
                    rawProgress
                )
            );

        float safeWidth =
            Mathf.Max(
                0.0001f,
                bendWaveWidth
            );

        float arcAmount =
            Mathf.Max(
                0f,
                pathArcCurve.Evaluate(
                    pathProgress
                )
            );

        Vector3 arcOffset =
            pathArcDirection *
            pathFrontOffset *
            arcAmount;

        for (
            int i = 0;
            i < pullBones.Length;
            i++
        )
        {
            Vector3 start =
                startWorldPositions[i];

            Vector3 target =
                targetWorldPositions[i];

            /*
            * 全Boneの基本位置は、
            * 開始Poseから最終Poseへの直線補間。
            *
            * 下ドラッグ方向はここでは使用しない。
            */
            Vector3 straightPosition =
                Vector3.Lerp(
                    start,
                    target,
                    pathProgress
                );

            /*
            * 全Boneへ同じ手前方向の膨らみを加える。
            * 開始時と終了時には必ず0になる。
            */
            Vector3 basePosition =
                straightPosition +
                arcOffset;

            float distanceFromCenter =
                Mathf.Abs(
                    i - waveCenter
                );

            float normalizedDistance =
                Mathf.Clamp01(
                    distanceFromCenter /
                    safeWidth
                );

            float waveAmount =
                1f -
                Mathf.SmoothStep(
                    0f,
                    1f,
                    normalizedDistance
                );

            Vector3 bendOffset =
                bendDirection *
                maximumBendOffset *
                envelope *
                waveAmount;

            currentWorldPositions[i] =
                basePosition +
                bendOffset;
        }
    }
    private void CalculateCurrentBoneRotations(
        float pathProgress
    )
    {
        Vector3 localForward =
            localBoneForwardAxis.sqrMagnitude > 0.000001f
                ? localBoneForwardAxis.normalized
                : Vector3.up;

        for (
            int i = 0;
            i < pullBones.Length;
            i++
        )
        {
            /*
            * 紙全体が開始姿勢から最終姿勢へ変わる分の
            * 基本回転。
            */
            Quaternion baseRotation =
                Quaternion.Slerp(
                    startWorldRotations[i],
                    targetWorldRotations[i],
                    pathProgress
                );

            /*
            * 現在のBone列の並びから、
            * 各Boneが向くべき方向を求める。
            */
            Vector3 desiredDirection;

            if (i < pullBones.Length - 1)
            {
                desiredDirection =
                    currentWorldPositions[i + 1] -
                    currentWorldPositions[i];
            }
            else
            {
                desiredDirection =
                    currentWorldPositions[i] -
                    currentWorldPositions[i - 1];
            }

            if (
                desiredDirection.sqrMagnitude <
                0.000001f
            )
            {
                currentWorldRotations[i] =
                    baseRotation;

                continue;
            }

            desiredDirection.Normalize();

            Vector3 currentForward =
                baseRotation *
                localForward;

            if (
                currentForward.sqrMagnitude <
                0.000001f
            )
            {
                currentWorldRotations[i] =
                    baseRotation;

                continue;
            }

            Quaternion alignmentRotation =
                Quaternion.FromToRotation(
                    currentForward.normalized,
                    desiredDirection
                );

            currentWorldRotations[i] =
                alignmentRotation *
                baseRotation;
        }
    }
    private void ApplyCurrentWorldPoses()
    {
        /*
         * PullBone_00 → PullBone_09の親子順で適用する。
         */
        for (
            int i = 0;
            i < pullBones.Length;
            i++
        )
        {
            pullBones[i].SetPositionAndRotation(
                currentWorldPositions[i],
                currentWorldRotations[i]
            );
        }
    }

    private void RestoreBindPose()
    {
        if (
            pullBones == null ||
            bindLocalPositions == null ||
            bindLocalRotations == null
        )
        {
            return;
        }

        /*
         * 親から子の順で初期Local Poseへ戻す。
         */
        for (
            int i = 0;
            i < pullBones.Length;
            i++
        )
        {
            pullBones[i].localPosition =
                bindLocalPositions[i];

            pullBones[i].localRotation =
                bindLocalRotations[i];
        }
    }


    private Vector3 CalculatePathArcDirection(
        Vector3 pathDirection
    )
    {
        Vector3 cameraFrontDirection;

        if (targetCamera != null)
        {
            /*
            * カメラ手前方向。
            */
            cameraFrontDirection =
                -targetCamera.transform.forward;
        }
        else
        {
            cameraFrontDirection =
                -paperReference.forward;
        }

        if (invertFrontDirection)
        {
            cameraFrontDirection =
                -cameraFrontDirection;
        }

        /*
        * 開始点から終点へ向かう成分を取り除く。
        *
        * これにより、手前へ膨らませても
        * 終点方向へ過剰に進んだり、
        * 開始地点側へ逆戻りしたりしない。
        */
        Vector3 perpendicularDirection =
            Vector3.ProjectOnPlane(
                cameraFrontDirection,
                pathDirection
            );

        /*
        * 画面上下方向の成分も除去する。
        *
        * カメラが傾いていても、
        * pathFrontOffsetによって紙が池の下へ
        * 沈み込まないようにする。
        */
        perpendicularDirection =
            Vector3.ProjectOnPlane(
                perpendicularDirection,
                Vector3.up
            );

        if (
            perpendicularDirection.sqrMagnitude <
            0.000001f
        )
        {
            return Vector3.zero;
        }

        return perpendicularDirection.normalized;
    }
}