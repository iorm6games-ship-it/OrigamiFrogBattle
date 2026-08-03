using System;
using System.Collections.Generic;
using System.IO;
using Unity.VisualScripting;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.UIElements;

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

    [Tooltip("引き抜く側の先頭Bone")]
    [SerializeField]
    private Transform firstBone;

    [Tooltip("取得するBone数")]
    [Min(2)]
    [SerializeField]
    private int boneCount = 10;

    [Header("Pull Path")]
    [Tooltip("紙面方向を取得する基準Transform")]
    [SerializeField]
    private Transform paperReference;

    [Tooltip("ドラッグ方向を取得するカメラ")]
    [SerializeField]
    private Camera targetCamera;

    [Tooltip("紙面のローカル法線")]
    [SerializeField]
    private Vector3 localPaperNormal = Vector3.up;

    [Tooltip("PullBone_00が移動する最大距離")]
    [Min(0.01f)]
    [SerializeField]
    private float maxPullDistance = 0.55f;

    [Tooltip("最大引っ張り時に先頭Boneへ加える回転")]
    [SerializeField]
    private Vector3 headRotationOffsetEuler =
        new Vector3(0f, 0f, 35f);

    [Tooltip("位置の進み方")]
    [SerializeField]
    private AnimationCurve positionCurve =
        AnimationCurve.EaseInOut(
            0f,
            0f,
            1f,
            1f
        );
    [Tooltip("回転の進み方")]
    [SerializeField]
    private AnimationCurve rotationCurve =
        AnimationCurve.EaseInOut(
            0f,
            0f,
            1f,
            1f
        );
    
    [Tooltip("固定経路の分割数")]
    [Range(16, 256)]
    [SerializeField]
    private int pathSampleCount = 96;

    [Tooltip("引き抜き時に紙面から浮かせる最大距離")]
    [Min(0f)]
    [SerializeField]
    private float maxLiftDistance = 0.18f;

    [Tooltip("引き始めのどの時点で最大の浮きを作るか")]
    [Range(0.05f, 0.8f)]
    [SerializeField]
    private float liftPeakProgress = 0.35f;

    [Tooltip("紙面法線の正負を反転する")]
    [SerializeField]
    private bool invertLiftDirection;

    [Header("Debug")]
    [SerializeField]
    private bool logBoneInformation = true;

    private Transform[] pullBones;
    private Vector3[] restWorldPositions;
    private Quaternion[] restWorldRotations;
    private Vector3[] restLocalPositions;
    private Quaternion[] restLocalRotations;

    private float[] cumulativeDistances;

    private readonly List<PathPose> pathPoses =
        new List<PathPose>();
    
    private float initialChainLength;
    private float forwardPathLength;

    public bool IsInitialized {get; private set;}

    public int BoneCount =>
        pullBones != null
        ? pullBones.Length
        : 0;
    
    public float TotalBoneLength
    {
        get
        {
            if (
                cumulativeDistances == null ||
                cumulativeDistances.Length == 0
            )
            {
                return 0;
            }

            return cumulativeDistances[

                cumulativeDistances.Length -1
            ];
        }
    }

    private void Awake()
    {
        InitializeBoneChain();
    }

    [ContextMenu("Initialize Bone Chain")]
    public void InitializeBoneChain()
    {
        IsInitialized = false;

        if (firstBone == null)
        {
            Debug.LogWarning(
                $"{name}: First Bone が未設定です",
                this
            );
            return;
        }

        if (paperReference == null)
        {
            paperReference = transform;
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (!CollectBoneChain())
        {
            return;
        }

        BuildPullPath();

        IsInitialized = pathPoses.Count >= 2;

        if (
            IsInitialized &&
            logBoneInformation
        )
        {
            LogBoneInformation();
        }
    }
    public void ApplyPullAmount(float pullAmount)
    {
        if (!IsInitialized)
        {
            InitializeBoneChain();
        }

        if (!IsInitialized)
        {
            return;
        }

        pullAmount = Mathf.Clamp01(pullAmount);

        float curvePullAmount =
            positionCurve.Evaluate(pullAmount);
        
        float headDistance =
            initialChainLength +
            forwardPathLength *
            curvePullAmount;
        
        /* 親から子の順番にWorld Poseを設定する。
         * 親を動かすとこも動くが、その後に子自身の
         * World Pose を設定するため、最終位置は補正される。
         */
        for (int i = 0; i < pullBones.Length; i++)
        {
            float sampleDistance =
                headDistance -
                cumulativeDistances[i];
            PathPose sampledPose =
                SamplePathByDistance(sampleDistance);
            
            pullBones[i].SetPositionAndRotation(
                sampledPose.position,
                sampledPose.rotation
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

        /*
         * 親から順番にlocal pose を戻す
         * 親の回転が子へ継承される構造なので、
         * 初期Local Pose へ戻す対応が最も確実
         */
        for (int i = 0; i < pullBones.Length; i++)
        {
            pullBones[i].localPosition =
                restLocalPositions[i];
            pullBones[i].localRotation =
                restLocalRotations[i];
        }
    }

    private bool CollectBoneChain()
    {
        pullBones = new Transform[boneCount];

        restWorldPositions =
            new Vector3[boneCount];
        restWorldRotations =
            new Quaternion[boneCount];
        
        restLocalPositions =
            new Vector3[boneCount];
        
        restLocalRotations =
            new Quaternion[boneCount];
        
        cumulativeDistances =
            new float[boneCount];
        
        Transform currentBone = firstBone;

        for (int i = 0; i < boneCount; i++)
        {
            if (currentBone == null)
            {
                Debug.LogError(
                    $"{name}: {i} 番目のBoneを取得できません。",
                    this
                );

                return false;
            }

            pullBones[i] = currentBone;
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
                float segmentDistance =
                    Vector3.Distance(
                        restWorldPositions[i - 1],
                        restWorldPositions[i]
                    );
                
                cumulativeDistances[i] =
                    cumulativeDistances[i - 1] +
                    segmentDistance;
            }

            if (i < boneCount - 1)
            {
                if (currentBone.childCount == 0)
                {
                    Debug.LogError(
                        $"{currentBone.name} に子Boneがありません。",
                        currentBone
                    );
                    return false;
                }
                currentBone =
                    currentBone.GetChild(0);    
            }
            
        }
        initialChainLength =
            cumulativeDistances[
                cumulativeDistances.Length - 1
            ];
        return true;
    }

    private void BuildPullPath()
    {
        pathPoses.Clear();

        /* PullBone_09 から PullBone_00 までを、
         * 初期状態の経路として登録する。
         * 
         * これにより、pullAmount = 0のとき、
         * 各Boneが自分自身の初期地点を参照できる。
         */
        float cumulativeDistance = 0f;

        for (int i = pullBones.Length - 1; i >= 0; i--)
        {
            if (pathPoses.Count > 0)
            {
                PathPose previousPose =
                    pathPoses[pathPoses.Count - 1];
                cumulativeDistance +=
                    Vector3.Distance(
                        previousPose.position,
                        restWorldPositions[i]
                    );
            }
            pathPoses.Add(
                new PathPose(
                    restWorldPositions[i],
                    restWorldRotations[i],
                    cumulativeDistance
                )
            );
        }
        initialChainLength = cumulativeDistance;

        Vector3 headStartPosition =
            restWorldPositions[0];
        
        Quaternion headStartRotation =
            restWorldRotations[0];
        
        Vector3 slideDirection =
            CalculateSlideDirection();

        Vector3 paperNormalWorld =
            paperReference.TransformDirection(
                localPaperNormal.normalized
            );

        if (invertLiftDirection)
        {
            paperNormalWorld =
                -paperNormalWorld;
        }

        Quaternion headEndRotation =
            headStartRotation *
            Quaternion.Euler(
                headRotationOffsetEuler
            );
        
        for (int sample = 1; sample <= pathSampleCount; sample++)
        {
            float normalizedTime =
                sample /
                (float)pathSampleCount;
            float positionTime =
                positionCurve.Evaluate(
                    normalizedTime
                );
            float rotationTime =
                rotationCurve.Evaluate(
                    normalizedTime
                );
            
            /*
             * 引き始めからliftPeakProgress までは
             * 徐々に紙面から浮かせる。
             * それ以降は浮いた状態を維持する
             */
            float liftTime =
                Mathf.InverseLerp(
                    0f,
                    liftPeakProgress,
                    normalizedTime
                );
            
            liftTime =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    liftTime
                );

            Vector3 position =
                headStartPosition +
                slideDirection *
                maxPullDistance *
                positionTime +
                paperNormalWorld *
                maxLiftDistance *
                liftTime;
            
            Quaternion rotation =
                Quaternion.Slerp(
                    headStartRotation,
                    headEndRotation,
                    rotationTime
                );
            
            PathPose previousPose =
                pathPoses[pathPoses.Count - 1];

            cumulativeDistance +=
                Vector3.Distance(
                    previousPose.position,
                    position
                );

            pathPoses.Add(
                new PathPose(
                    position,
                    rotation,
                    cumulativeDistance
                )
            );
        }

        forwardPathLength =
            cumulativeDistance -
            initialChainLength;
    }

    private Vector3 CalculateSlideDirection()
    {
        if (targetCamera == null)
        {
            return -paperReference.up;
        }
        Vector3 paperNormalWorld =
            paperReference.TransformDirection(
                localPaperNormal.normalized
            );
        
        Vector3 screenDownWorld =
            - targetCamera.transform.up;
        
        Vector3 slideDirection =
            Vector3.ProjectOnPlane(
                screenDownWorld,
                paperNormalWorld
            );
        
        if (slideDirection.sqrMagnitude < 0.0001f)
        {
            slideDirection = screenDownWorld;
        }

        return slideDirection.normalized;
    }

    private PathPose SamplePathByDistance(
        float targetDistance
    )
    {
        if (targetDistance < 0f)
        {
            return pathPoses[0];
        }

        PathPose lastPose =
            pathPoses[pathPoses.Count - 1];
        
        if (targetDistance >= lastPose.distance)
        {
            return lastPose;
        }

        for (int i = 1; i < pathPoses.Count; i++)
        {
            PathPose nextPose = pathPoses[i];

            if (nextPose.distance < targetDistance)
            {
                continue;
            }

            PathPose previousPose =
                pathPoses[i - 1];
            
            float segmentLength =
                nextPose.distance -
                previousPose.distance;

            float interpolationAmount =
                segmentLength > 0.000001f
                    ? (
                        targetDistance -
                        previousPose.distance
                    ) / segmentLength
                    : 0f;
            return new PathPose(
                Vector3.Lerp(
                    previousPose.position,
                    nextPose.position,
                    interpolationAmount
                ),
                Quaternion.Slerp(
                    previousPose.rotation,
                    nextPose.rotation,
                    interpolationAmount
                ),
                targetDistance
            );
        }
        return lastPose;
    }

    private void LogBoneInformation()
    {
        Debug.Log(
            $"{name}: Bone チェーン取得完了。"
            + $" Bone数 = {BoneCount}"
            + $" 全長 = {TotalBoneLength:F6}"
            + $" 引き抜き経路長 = {forwardPathLength:F6}",
            this
        );

        for (int i = 1; i < pullBones.Length; i++)
        {
            float segmentDistnace =
                cumulativeDistances[i] -
                cumulativeDistances[i - 1];
            Debug.Log(
                $"{pullBones[i - 1].name} → " +
                $"{pullBones[i].name}: " +
                $"{segmentDistnace:F6}",
                pullBones[i]
            );

        }
    }

    private void ClearCacheData()
    {
        pullBones = null;
        restWorldPositions = null;
        restWorldRotations = null;
        restLocalPositions = null;
        restLocalRotations = null;
        cumulativeDistances = null;
        IsInitialized = false;
    }
}