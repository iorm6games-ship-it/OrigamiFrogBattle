using Unity.VisualScripting;
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

    [Tooltip("実際に移動させる紙全体。Paper_Redを設定")]
    [SerializeField]
    private Transform paperReference;

    [Tooltip("経路をカメラ手前へ膨らませる方向の計算に使用")]
    [SerializeField]
    private Camera targetCamera;
    
    [Tooltip("PullBone_00～09の追従経路を計算するコンポーネント")]
    [SerializeField]
    private PaperPullPathPreview pathPreview;
    
    [Header("Paper Movement Path")]

    [Tooltip(
        "開始位置から確定位置までの進み方。"
        + "下ドラッグ方向は移動方向には使用しない"
    )]
    [SerializeField]
    private AnimationCurve pathProgressCurve =
        AnimationCurve.EaseInOut(
            0f,
            0f,
            1f,
            1f
        );

    [Tooltip(
        "直線経路からカメラ手前へ膨らむ量。"
        + "開始と終了は0、中間が1"
    )]
    [SerializeField]
    private AnimationCurve pathArcCurve =
        new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.5f, 1f),
            new Keyframe(1f, 0f)
        );

    [Tooltip(
        "経路中央をカメラ手前へ膨らませる距離。"
        + "最初は0で直線確認する"
    )]
    [Min(0f)]
    [SerializeField]
    private float pathFrontOffset = 0f;

    [Tooltip("カメラ手前方向を反転する")]
    [SerializeField]
    private bool invertFrontDirection;

    [Header("Bone Local Bend")]

    [Tooltip(
        "紙全体に加える最大湾曲角度。"
        + "10本のBoneへ分散して適用する"
    )]
    [Range(-60f, 60f)]
    [SerializeField]
    private float maximumTotalBendAngle = 0f;

    [Tooltip(
        "Boneを曲げるローカル軸。"
        + "これまで方向が合っていた軸を設定する"
    )]
    [SerializeField]
    private Vector3 localBendAxis =
        Vector3.forward;

    [Tooltip(
        "進捗に応じた湾曲量。"
        + "開始時と終了時は0"
    )]
    [SerializeField]
    private AnimationCurve bendEnvelope =
        new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.2f, 0.75f),
            new Keyframe(0.45f, 1f),
            new Keyframe(0.75f, 0.55f),
            new Keyframe(1f, 0f)
        );

    [Tooltip(
        "00から09までの湾曲配分。"
        + "隣接Bone間の急激な差を作らない"
    )]
    [SerializeField]
    private AnimationCurve bendDistribution =
        AnimationCurve.EaseInOut(
            0f,
            0f,
            1f,
            1f
        );

    [Header("Bone Rotation Test")]

    [Tooltip(
        "PullBone の長手方向を表すローカル軸" 
        + "まずは（0, 1, 0）で確認"
    )]
    [SerializeField]
    private Vector3 localBoneForwardAxis =
        Vector3.up;

    [SerializeField]
    private Vector3 localPaperNormalAxis =
        Vector3.left;

    private Quaternion[] initialWorldRotations;
    private Vector3[] initialPathTangents;
    private Vector3[] initialPathNormals;

    [Header("Bone Rotation Test")]
    [SerializeField]
    private float tangentRotationStartProgress = 0.18f;

    [Range(0f, 1f)]
    [SerializeField]
    private float tangentRotationWeight =
        0.4f;

    [Tooltip(
        "紙をめくる際、隣の紙と反対方向へ" +
        "Bone中心を逃がす最大距離"
    )]
    [Min(0f)]
    [SerializeField]
    private float sideClearanceOffset =
        0.12f;

    [Tooltip(
        "赤い紙から見て、青い紙と反対側を示すローカル軸"
    )]
    [SerializeField]
    private Vector3 localClearanceDirection =
        Vector3.forward;
    [Tooltip(
        "引き抜き中の紙をカメラ側へ浮かせ、" +
        "隣の紙との奥行き交差を防ぐ"
    )]
    [Min(0f)]
    [SerializeField]
    private float cameraDepthClearance =
        0.08f;

    [Tooltip(
        "開始直後から浮かせ、終了付近で元の経路へ戻す"
    )]
    [SerializeField]
    private AnimationCurve depthClearanceCurve =
        new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.08f, 1f),
            new Keyframe(0.75f, 1f),
            new Keyframe(1f, 0f)
        );

    [Header("Debug")]

    [SerializeField]
    private bool logInformation;

    private Transform[] pullBones;
    private Vector3[] bindLocalPositions;
    private Quaternion[] bindLocalRotations;

    private Vector3 startPaperPosition;
    private Quaternion startPaperRotation;

    private Vector3 targetPaperPosition;
    private Quaternion targetPaperRotation;

    private Vector3 pathArcDirection;

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
            targetCamera =
                Camera.main;
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

    /// <summary>
    /// 現在のPaper_RedのPoseから、
    /// SelectedPaperCenterPointまでの一本の経路を準備する。
    /// </summary>
    public bool PreparePath(
        Transform targetPoint
    )
    {
        if (!IsInitialized)
        {
            InitializeBoneChain();
        }

        if (!IsInitialized)
        {
            return false;
        }

        if (pathPreview == null)
        {
            Debug.LogWarning(
                $"{name}: Path Previewが未設定です。",
                this
            );

            IsPathPrepared = false;
            return false;
        }

        /*
        * 前回の操作によるBone位置のずれを
        * 初期姿勢へ戻してから経路を構築する。
        */
        RestoreBindPose();

        bool prepared =
            pathPreview.PrepareRuntimePath();

        if (!prepared)
        {
            Debug.LogWarning(
                $"{name}: Bone追従経路を構築できませんでした。",
                this
            );

            IsPathPrepared = false;
            return false;
        }

        Vector3 normalAxis =
            localPaperNormalAxis.sqrMagnitude >
            0.000001f
                ? localPaperNormalAxis.normalized
                : Vector3.left;
        for (
            int i = 0;
            i < pullBones.Length;
            i++
        )
        {
            initialWorldRotations[i] =
                pullBones[i].rotation;
            
            initialPathTangents[i] =
                pathPreview.EvaluateBoneTangent(
                    i,
                    0f
                );
            if (
                initialPathTangents[i].sqrMagnitude <
                0.000001f
            )
            {
                initialPathTangents[i] =
                    initialWorldRotations[i] *
                    localBoneForwardAxis.normalized;
            }

            initialPathTangents[i].Normalize();

            Vector3 initialWorldNormal =
                initialWorldRotations[i] *
                normalAxis;
            /*
             * 法線から接線方向の成分を除き
             * 経路に対して直角な紙面法線を作る
             */
            initialPathNormals[i] =
                Vector3.ProjectOnPlane(
                    initialWorldNormal,
                    initialPathTangents[i]
                );
            if (
                initialPathNormals[i].sqrMagnitude <
                0.000001f
            )
            {
                initialPathNormals[i] =
                    Vector3.up;
            }

            initialPathNormals[i].Normalize();
            
        }

        IsPathPrepared = true;

        /*
        * 初期位置を確実に適用する。
        */
        ApplyPullAmount(0f);

        if (logInformation)
        {
            Debug.Log(
                $"{name}: Bone追従経路を準備しました。",
                this
            );
        }

        return true;
    }

    /// <summary>
    /// ドラッグ中、戻り、自動完走のすべてで使用する。
    /// progress=0が開始位置、1が確定位置。
    /// </summary>
    public void ApplyPullAmount(
        float progress
    )
    {
        if (
            !IsInitialized ||
            !IsPathPrepared ||
            pathPreview == null
        )
        {
            return;
        }

        progress =
            Mathf.Clamp01(progress);

        ApplyBonePathPoses(
            progress
        );
    }
    private void ApplyBonePathPoses(
        float progress
    )
    {

        Vector3 normalAxis =
            localPaperNormalAxis.sqrMagnitude >
            0.000001f
                ? localPaperNormalAxis.normalized
                : Vector3.left;

        for (
            int i = 0;
            i < pullBones.Length;
            i++
        )
        {
            Transform bone =
                pullBones[i];

            Vector3 targetPosition =
                pathPreview.EvaluateBonePosition(
                    i,
                    progress
                );
            Vector3 cameraFrontDirection =
                targetCamera != null
                    ? targetCamera.transform.forward
                    : Vector3.zero;

            float depthClearanceAmount =
                Mathf.Max(
                    0f,
                    depthClearanceCurve.Evaluate(
                        progress
                    )
                );

            targetPosition +=
                cameraFrontDirection *
                cameraDepthClearance *
                depthClearanceAmount;
            Vector3 currentTangent =
                pathPreview.EvaluateBoneTangent(
                    i,
                    progress
                );

            if (
                currentTangent.sqrMagnitude <
                0.000001f
            )
            {
                bone.SetPositionAndRotation(
                    targetPosition,
                    initialWorldRotations[i]
                );

                continue;
            }

            currentTangent.Normalize();

            /*
            * 初期の紙面法線を現在の接線に対して
            * 直角になるよう投影する。
            *
            * これにより、Boneの長手方向だけでなく、
            * 紙の表裏方向も維持する。
            */
            Vector3 initialWorldNormal =
                initialWorldRotations[i] *
                normalAxis;

            Vector3 currentNormal =
                Vector3.ProjectOnPlane(
                    initialWorldNormal,
                    currentTangent
                );

            if (
                currentNormal.sqrMagnitude <
                0.000001f
            )
            {
                currentNormal =
                    initialPathNormals[i];
            }

            currentNormal.Normalize();

            /*
            * LookRotationは、
            * 第1引数をZ方向、
            * 第2引数をY方向として回転を作る。
            *
            * ここでは直接Boneへ使わず、
            * 初期フレームから現在フレームへの
            * 回転差を計算するために使用する。
            */
            Quaternion initialFrame =
                Quaternion.LookRotation(
                    initialPathTangents[i],
                    initialPathNormals[i]
                );

            Quaternion currentFrame =
                Quaternion.LookRotation(
                    currentTangent,
                    currentNormal
                );

            Quaternion frameDifference =
                currentFrame *
                Quaternion.Inverse(
                    initialFrame
                );

            Quaternion fullTangentRotation =
                frameDifference *
                initialWorldRotations[i];
            
            float rotationProgress =
                Mathf.InverseLerp(
                    tangentRotationStartProgress,
                    1f,
                    progress
                );

            rotationProgress =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    rotationProgress
                );

            float effectiveRotationWeight =
                tangentRotationWeight *
                rotationProgress;

            Quaternion targetRotation =
                Quaternion.Slerp(
                    initialWorldRotations[i],
                    fullTangentRotation,
                    effectiveRotationWeight
                );
            /*
            * 親から子の順にWorld Poseを確定する。
            */
            Vector3 clearanceAxis =
                localClearanceDirection.sqrMagnitude >
                0.000001f
                    ? localClearanceDirection.normalized
                    : Vector3.forward;

                /*
                * 初期回転から現在回転までの角度。
                * 回転が大きいほど隣の紙と反対側へ逃がす。
                */
                float rotationAngle =
                    Quaternion.Angle(
                        initialWorldRotations[i],
                        targetRotation
                    );

                float clearanceAmount =
                    Mathf.Sin(
                        rotationAngle *
                        Mathf.Deg2Rad
                    ) *
                    sideClearanceOffset;

                /*
                * 初期姿勢を基準に、紙幅方向へ補正する。
                */
                Vector3 clearanceDirectionWorld =
                    initialWorldRotations[i] *
                    clearanceAxis;

                Vector3 correctedPosition =
                    targetPosition +
                    clearanceDirectionWorld *
                    clearanceAmount;

                bone.SetPositionAndRotation(
                    correctedPosition,
                    targetRotation
                );
        }
    }
    /// <summary>
    /// 確定時の最終Poseを保証する。
    /// 紙はすでに同じ経路で到達しているため、
    /// 別の移動は発生しない。
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

        /*
        * 現段階では、Progress=1のBone配置を維持する。
        * Paper_Red本体の移動やBind Poseへの復帰はまだ行わない。
        */
        IsPathPrepared = false;

        if (pathPreview != null)
        {
            pathPreview.ReleaseRuntimePath();
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

        RestoreBindPose();

        IsPathPrepared = false;

        if (pathPreview != null)
        {
            pathPreview.ReleaseRuntimePath();
        }
    }

    private bool CollectBoneChain()
    {
        pullBones =
            new Transform[boneCount];

        bindLocalPositions =
            new Vector3[boneCount];

        bindLocalRotations =
            new Quaternion[boneCount];

        initialWorldRotations =
            new Quaternion[boneCount];
        
        initialPathTangents =
            new Vector3[boneCount];
        initialPathNormals =
            new Vector3[boneCount];

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

    private void ApplyPaperMovement(
        float pathProgress
    )
    {
        /*
         * 基本経路は、開始位置から確定位置への直線。
         */
        Vector3 straightPosition =
            Vector3.Lerp(
                startPaperPosition,
                targetPaperPosition,
                pathProgress
            );

        /*
         * 開始・終了では0。
         * 中間地点だけカメラ手前へ膨らませる。
         */
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

        Vector3 currentPosition =
            straightPosition +
            arcOffset;

        Quaternion currentRotation =
            Quaternion.Slerp(
                startPaperRotation,
                targetPaperRotation,
                pathProgress
            );

        /*
         * BoneではなくPaper_Red本体を移動する。
         * これにより初期位置に紙が残らない。
         */
        paperReference.SetPositionAndRotation(
            currentPosition,
            currentRotation
        );
    }

    private void ApplyLocalBoneBend(
        float progress
    )
    {
        Vector3 bendAxis =
            localBendAxis.sqrMagnitude >
            0.000001f
                ? localBendAxis.normalized
                : Vector3.forward;

        float envelope =
            bendEnvelope.Evaluate(
                progress
            );

        /*
         * 全Boneで合計maximumTotalBendAngle程度に
         * なるよう、各Boneの差分角度を計算する。
         *
         * 09だけを大きく回転させないため、
         * 裂けたような境界を作りにくい。
         */
        float previousDistribution = 0f;

        for (
            int i = 0;
            i < pullBones.Length;
            i++
        )
        {
            float chainProgress =
                pullBones.Length > 1
                    ? i /
                      (float)(pullBones.Length - 1)
                    : 0f;

            float currentDistribution =
                bendDistribution.Evaluate(
                    chainProgress
                );

            float distributionDifference =
                i == 0
                    ? currentDistribution
                    : currentDistribution -
                      previousDistribution;

            float angle =
                maximumTotalBendAngle *
                envelope *
                distributionDifference;

            pullBones[i].localPosition =
                bindLocalPositions[i];

            pullBones[i].localRotation =
                bindLocalRotations[i] *
                Quaternion.AngleAxis(
                    angle,
                    bendAxis
                );

            previousDistribution =
                currentDistribution;
        }

        /*
         * 開始地点と確定地点では
         * 正確に通常のBone姿勢へ戻す。
         */
        if (
            progress <= 0.0001f ||
            progress >= 0.9999f
        )
        {
            RestoreBindPose();
        }
    }

    private void BuildPathArcDirection()
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

            return;
        }

        Vector3 directDirection =
            directVector.normalized;

        Vector3 cameraFront;

        if (targetCamera != null)
        {
            cameraFront =
                -targetCamera.transform.forward;
        }
        else
        {
            cameraFront =
                -paperReference.forward;
        }

        if (invertFrontDirection)
        {
            cameraFront =
                -cameraFront;
        }

        /*
         * 移動方向と平行な成分を除去する。
         * これにより開始側へ逆戻りしない。
         */
        Vector3 projectedDirection =
            Vector3.ProjectOnPlane(
                cameraFront,
                directDirection
            );

        if (
            projectedDirection.sqrMagnitude <
            0.000001f
        )
        {
            pathArcDirection =
                Vector3.zero;

            return;
        }

        pathArcDirection =
            projectedDirection.normalized;
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
}