using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class CrystalAssemblyController : MonoBehaviour
{
    private static readonly int FormProgressId =
        Shader.PropertyToID("_FormProgress");

    [Header("Fragment Source")]
    [SerializeField]
    private Transform fragmentPrefab;

    [Min(3)]
    [SerializeField]
    private int fragmentCount = 9;

    [Tooltip(
        "任意。子Transformを欠片の完成位置として使う。\n" +
        "設定されていない場合はFinal CrystalのBoundsから自動生成する。")]
    [SerializeField]
    private Transform fragmentTargetsRoot;

    [Header("Scatter")]
    [SerializeField]
    private float startRadiusMultiplier = 2.2f;

    [SerializeField]
    private float startHeightMultiplier = 0.85f;

    [SerializeField]
    private Vector2 fragmentScaleRange =
        new Vector2(0.70f, 1.10f);

    [SerializeField]
    private float curveAmountMultiplier = 0.55f;

    [Header("Bottom-Up Assembly")]
    [Range(0f, 0.6f)]
    [SerializeField]
    private float stackStart = 0.05f;

    [Range(0.3f, 1f)]
    [SerializeField]
    private float stackEnd = 0.72f;

    [Range(0.05f, 0.7f)]
    [SerializeField]
    private float fragmentTravelWindow = 0.30f;

    [Range(0f, 0.25f)]
    [SerializeField]
    private float orderJitter = 0.025f;

    [Range(0f, 0.25f)]
    [SerializeField]
    private float settlePortion = 0.22f;

    [Range(0f, 0.20f)]
    [SerializeField]
    private float settleScaleOvershoot = 0.06f;

    [Header("Final Crystal")]
    [Range(0f, 1f)]
    [SerializeField]
    private float finalCrystalStart = 0.82f;

    [Range(0f, 1f)]
    [SerializeField]
    private float fragmentMergeStart = 0.86f;

    [Range(0.01f, 0.5f)]
    [SerializeField]
    private float fragmentEndScale = 0.10f;

    [Range(0f, 0.3f)]
    [SerializeField]
    private float mergeHeightStagger = 0.04f;

    [Header("Completed Float")]
    [SerializeField]
    private float floatAmplitude = 0.025f;

    [SerializeField]
    private float floatFrequency = 0.72f;

    [SerializeField]
    private float swayDegrees = 1.25f;

    [SerializeField]
    private float phaseOffsetRange = 0.12f;

    public bool IsComplete { get; private set; }
    public float FloatSignal { get; private set; }
    public float CurrentFloatOffset { get; private set; }

    private Transform center;
    private Transform finalCrystal;

    private Renderer[] finalRenderers;
    private MaterialPropertyBlock finalPropertyBlock;

    private Vector3 finalBasePosition;
    private Quaternion finalBaseRotation;
    private Vector3 finalBaseScale;

    private float floatPhaseOffset;
    private bool floating;

    private readonly List<FragmentState> fragments =
        new List<FragmentState>();

    private sealed class FragmentState
    {
        public Transform Transform;

        public Vector3 StartPosition;
        public Quaternion StartRotation;
        public Vector3 StartScale;

        public Vector3 TargetPosition;
        public Quaternion TargetRotation;
        public Vector3 TargetScale;

        public Vector3 CurveDirection;

        // 0 = 最下段 / 1 = 最上段
        public float HeightOrder;

        // この欠片が組み上がり始めるFormation Progress
        public float StartProgress;

        // この欠片が定位置へ収まるFormation Progress
        public float EndProgress;
    }

    private struct TargetPose
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
        public float Height;
    }

    public void Begin(
        Transform centerPoint,
        Transform finalCrystalTransform)
    {
        center = centerPoint;
        finalCrystal = finalCrystalTransform;

        IsComplete = false;
        floating = false;

        FloatSignal = 0f;
        CurrentFloatOffset = 0f;

        if (center == null ||
            finalCrystal == null)
        {
            return;
        }

        finalBasePosition =
            finalCrystal.position;

        finalBaseRotation =
            finalCrystal.rotation;

        finalBaseScale =
            finalCrystal.localScale;

        finalRenderers =
            finalCrystal.GetComponentsInChildren<Renderer>(
                true);

        finalPropertyBlock =
            new MaterialPropertyBlock();

        SetFinalFormProgress(0f);

        //
        // 欠片が下から組み上がる間は、
        // 完成クリスタル本体を完全に非表示にする。
        //
        // 先に薄い完成形が見えると、
        // 「欠片が組み立てている」読みが消えるため。
        //
        finalCrystal.gameObject.SetActive(false);

        finalCrystal.position =
            finalBasePosition;

        finalCrystal.rotation =
            finalBaseRotation;

        finalCrystal.localScale =
            finalBaseScale;

        floatPhaseOffset =
            UnityEngine.Random.Range(
                -phaseOffsetRange,
                phaseOffsetRange);

        ClearFragments();
        CreateFragments();

        SetProgress(0f);
    }

    public void SetProgress(
        float progress)
    {
        if (center == null ||
            finalCrystal == null)
        {
            return;
        }

        float t =
            Mathf.Clamp01(
                progress);

        UpdateFragments(
            t);

        //
        // 欠片の積み上げを先に最後まで読ませる。
        //
        // この時点では完成クリスタル本体を
        // RendererごとではなくGameObjectごと非表示。
        // ShaderのFormProgress=0でも薄いシルエットが
        // 見える可能性を完全に排除する。
        //
        if (t < finalCrystalStart)
        {
            if (finalCrystal.gameObject.activeSelf)
            {
                finalCrystal.gameObject.SetActive(false);
            }

            SetFinalFormProgress(0f);
            return;
        }

        if (!finalCrystal.gameObject.activeSelf)
        {
            SetFinalFormProgress(0f);
            finalCrystal.gameObject.SetActive(true);
        }

        //
        // 全体の形がある程度「欠片として完成」した後に、
        // Final Crystalを一体化させる。
        //
        float formT =
            Mathf.InverseLerp(
                finalCrystalStart,
                1f,
                t);

        formT =
            Mathf.SmoothStep(
                0f,
                1f,
                formT);

        SetFinalFormProgress(
            formT);
    }

    public void CompleteAssembly()
    {
        if (finalCrystal == null)
        {
            IsComplete = true;
            return;
        }

        if (!finalCrystal.gameObject.activeSelf)
        {
            finalCrystal.gameObject.SetActive(true);
        }

        SetFinalFormProgress(
            1f);

        finalCrystal.position =
            finalBasePosition;

        finalCrystal.rotation =
            finalBaseRotation;

        finalCrystal.localScale =
            finalBaseScale;

        ClearFragments();

        IsComplete = true;
        floating = true;
    }

    private void LateUpdate()
    {
        if (!floating ||
            finalCrystal == null ||
            center == null)
        {
            return;
        }

        float time =
            Time.time *
            floatFrequency *
            Mathf.PI *
            2f +
            floatPhaseOffset;

        FloatSignal =
            Mathf.Sin(
                time);

        CurrentFloatOffset =
            FloatSignal *
            floatAmplitude;

        Vector3 axis =
            center.up.normalized;

        Vector3 swayAxis =
            center.forward.normalized;

        finalCrystal.position =
            finalBasePosition +
            axis *
            CurrentFloatOffset;

        finalCrystal.rotation =
            Quaternion.AngleAxis(
                FloatSignal *
                swayDegrees,
                swayAxis) *
            finalBaseRotation;
    }

    private void CreateFragments()
    {
        if (fragmentPrefab == null ||
            center == null ||
            finalCrystal == null)
        {
            return;
        }

        Bounds bounds =
            CalculateFinalBounds();

        List<TargetPose> targets =
            BuildTargets(
                bounds);

        if (targets.Count <= 0)
        {
            return;
        }

        //
        // center.up基準の高さでSort。
        // これにより最下段から上へ順番に組み立てる。
        //
        targets.Sort(
            (a, b) =>
                a.Height.CompareTo(
                    b.Height));

        Vector3 axis =
            center.up.normalized;

        Vector3 basisX =
            center.right.normalized;

        Vector3 basisZ =
            center.forward.normalized;

        float verticalExtent =
            Mathf.Max(
                0.08f,
                Vector3.Dot(
                    bounds.extents,
                    AbsVector(axis)));

        if (verticalExtent < 0.08f)
        {
            verticalExtent =
                Mathf.Max(
                    0.08f,
                    bounds.extents.magnitude *
                    0.52f);
        }

        float horizontalExtent =
            Mathf.Max(
                0.04f,
                Mathf.Max(
                    bounds.extents.x,
                    bounds.extents.z));

        float startRadius =
            horizontalExtent *
            startRadiusMultiplier;

        float startHeightSpread =
            Mathf.Max(
                0.06f,
                verticalExtent *
                startHeightMultiplier);

        int count =
            targets.Count;

        for (int i = 0;
             i < count;
             i++)
        {
            TargetPose target =
                targets[i];

            Transform fragment =
                Instantiate(
                    fragmentPrefab,
                    finalBasePosition,
                    finalBaseRotation,
                    transform);

            DisableFragmentMotion(
                fragment);

            float order =
                count <= 1
                    ? 0f
                    : i /
                      (float)(count - 1);

            float angle =
                i *
                137.507764f +
                UnityEngine.Random.Range(
                    -22f,
                    22f);

            float angleRad =
                angle *
                Mathf.Deg2Rad;

            Vector3 startRadial =
                basisX *
                Mathf.Cos(
                    angleRad) +
                basisZ *
                Mathf.Sin(
                    angleRad);

            //
            // 上段ほど少しだけ高い位置から寄せる。
            // ただし開始位置はバラけさせて
            // 「積み木が待機している」ようには見せない。
            //
            Vector3 startPosition =
                finalBasePosition +
                startRadial *
                UnityEngine.Random.Range(
                    startRadius * 0.72f,
                    startRadius * 1.18f) +
                axis *
                (
                    Mathf.Lerp(
                        -startHeightSpread * 0.30f,
                        startHeightSpread * 0.65f,
                        order) +
                    UnityEngine.Random.Range(
                        -startHeightSpread * 0.35f,
                        startHeightSpread * 0.35f)
                );

            Quaternion startRotation =
                UnityEngine.Random.rotation;

            float scaleMultiplier =
                UnityEngine.Random.Range(
                    fragmentScaleRange.x,
                    fragmentScaleRange.y);

            Vector3 startScale =
                Vector3.Scale(
                    fragment.localScale,
                    Vector3.one *
                    scaleMultiplier);

            Vector3 targetScale =
                Vector3.Scale(
                    target.Scale,
                    Vector3.one *
                    UnityEngine.Random.Range(
                        0.92f,
                        1.06f));

            Vector3 curveDirection =
                Vector3.Cross(
                    axis,
                    startPosition -
                    target.Position);

            if (curveDirection.sqrMagnitude <
                0.0001f)
            {
                curveDirection =
                    basisX;
            }

            curveDirection.Normalize();

            //
            // 下から上へ。
            //
            // 最後の欠片でもstackEndまでには収まるよう、
            // TravelWindowを考慮して開始位置を分配する。
            //
            float availableStartSpan =
                Mathf.Max(
                    0f,
                    stackEnd -
                    stackStart -
                    fragmentTravelWindow);

            float jitter =
                UnityEngine.Random.Range(
                    -orderJitter,
                    orderJitter);

            float startProgress =
                stackStart +
                availableStartSpan *
                order +
                jitter;

            startProgress =
                Mathf.Clamp(
                    startProgress,
                    0f,
                    0.98f);

            float endProgress =
                Mathf.Min(
                    1f,
                    startProgress +
                    fragmentTravelWindow);

            fragment.position =
                startPosition;

            fragment.rotation =
                startRotation;

            fragment.localScale =
                startScale;

            fragments.Add(
                new FragmentState
                {
                    Transform =
                        fragment,

                    StartPosition =
                        startPosition,

                    StartRotation =
                        startRotation,

                    StartScale =
                        startScale,

                    TargetPosition =
                        target.Position,

                    TargetRotation =
                        target.Rotation,

                    TargetScale =
                        targetScale,

                    CurveDirection =
                        curveDirection,

                    HeightOrder =
                        order,

                    StartProgress =
                        startProgress,

                    EndProgress =
                        endProgress
                });
        }
    }

    private List<TargetPose> BuildTargets(
        Bounds bounds)
    {
        if (fragmentTargetsRoot != null &&
            fragmentTargetsRoot.childCount > 0)
        {
            return BuildManualTargets();
        }

        return BuildAutomaticTargets(
            bounds);
    }

    private List<TargetPose> BuildManualTargets()
    {
        List<TargetPose> result =
            new List<TargetPose>();

        Vector3 axis =
            center.up.normalized;

        for (int i = 0;
             i < fragmentTargetsRoot.childCount;
             i++)
        {
            Transform target =
                fragmentTargetsRoot.GetChild(
                    i);

            if (target == null)
            {
                continue;
            }

            float height =
                Vector3.Dot(
                    target.position -
                    finalBasePosition,
                    axis);

            result.Add(
                new TargetPose
                {
                    Position =
                        target.position,

                    Rotation =
                        target.rotation,

                    Scale =
                        target.localScale,

                    Height =
                        height
                });
        }

        return result;
    }

    private List<TargetPose> BuildAutomaticTargets(
        Bounds bounds)
    {
        List<TargetPose> result =
            new List<TargetPose>();

        Vector3 axis =
            center.up.normalized;

        Vector3 basisX =
            center.right.normalized;

        Vector3 basisZ =
            center.forward.normalized;

        float verticalExtent =
            Mathf.Max(
                0.08f,
                bounds.extents.magnitude *
                0.52f);

        float horizontalExtent =
            Mathf.Max(
                0.04f,
                Mathf.Max(
                    bounds.extents.x,
                    bounds.extents.z));

        for (int i = 0;
             i < fragmentCount;
             i++)
        {
            float order =
                fragmentCount <= 1
                    ? 0f
                    : i /
                      (float)(fragmentCount - 1);

            //
            // 下部から先端へ。
            //
            float height01 =
                Mathf.Lerp(
                    -0.82f,
                    0.88f,
                    order);

            //
            // 中央部を少し太く、
            // 先端へ行くほど半径を絞る。
            //
            float profile =
                Mathf.Sin(
                    Mathf.Clamp01(
                        order) *
                    Mathf.PI);

            profile =
                Mathf.Lerp(
                    0.25f,
                    1f,
                    profile);

            if (order > 0.78f)
            {
                float tipT =
                    Mathf.InverseLerp(
                        0.78f,
                        1f,
                        order);

                profile *=
                    Mathf.Lerp(
                        1f,
                        0.18f,
                        tipT);
            }

            float angle =
                i *
                137.507764f;

            float angleRad =
                angle *
                Mathf.Deg2Rad;

            Vector3 radial =
                basisX *
                Mathf.Cos(
                    angleRad) +
                basisZ *
                Mathf.Sin(
                    angleRad);

            float targetRadius =
                horizontalExtent *
                0.48f *
                profile;

            Vector3 position =
                finalBasePosition +
                axis *
                (
                    height01 *
                    verticalExtent *
                    0.82f
                ) +
                radial *
                targetRadius;

            Quaternion rotation =
                finalBaseRotation *
                Quaternion.Euler(
                    UnityEngine.Random.Range(
                        -12f,
                        12f),
                    UnityEngine.Random.Range(
                        -20f,
                        20f),
                    UnityEngine.Random.Range(
                        -12f,
                        12f));

            //
            // 上段ほど少し細くして
            // 先端形成の印象を出す。
            //
            float targetScaleMultiplier =
                Mathf.Lerp(
                    1.0f,
                    0.72f,
                    Mathf.InverseLerp(
                        0.55f,
                        1f,
                        order));

            result.Add(
                new TargetPose
                {
                    Position =
                        position,

                    Rotation =
                        rotation,

                    Scale =
                        fragmentPrefab.localScale *
                        targetScaleMultiplier,

                    Height =
                        Vector3.Dot(
                            position -
                            finalBasePosition,
                            axis)
                });
        }

        return result;
    }

    private void UpdateFragments(
        float progress)
    {
        foreach (
            FragmentState state
            in fragments)
        {
            if (state.Transform == null)
            {
                continue;
            }

            float localT =
                Mathf.InverseLerp(
                    state.StartProgress,
                    state.EndProgress,
                    progress);

            localT =
                Mathf.Clamp01(
                    localT);

            //
            // 0～(1-settlePortion):
            // 渦から引かれて定位置へ近づく。
            //
            // 終盤:
            // 「カチッ」ではなく、少し柔らかく吸着して収まる。
            //
            float approachEnd =
                Mathf.Clamp01(
                    1f -
                    settlePortion);

            float moveT;

            if (localT < approachEnd)
            {
                float approachT =
                    approachEnd <= 0f
                        ? 1f
                        : localT /
                          approachEnd;

                //
                // 最初は少し勢いを残し、
                // ターゲット近くで減速。
                //
                moveT =
                    1f -
                    Mathf.Pow(
                        1f -
                        approachT,
                        3f);

                moveT *=
                    0.93f;
            }
            else
            {
                float settleT =
                    Mathf.InverseLerp(
                        approachEnd,
                        1f,
                        localT);

                settleT =
                    Mathf.SmoothStep(
                        0f,
                        1f,
                        settleT);

                moveT =
                    Mathf.Lerp(
                        0.93f,
                        1f,
                        settleT);
            }

            Vector3 position =
                Vector3.Lerp(
                    state.StartPosition,
                    state.TargetPosition,
                    moveT);

            float arc =
                Mathf.Sin(
                    moveT *
                    Mathf.PI) *
                curveAmountMultiplier *
                Vector3.Distance(
                    state.StartPosition,
                    state.TargetPosition) *
                0.20f;

            position +=
                state.CurveDirection *
                arc;

            state.Transform.position =
                position;

            state.Transform.rotation =
                Quaternion.Slerp(
                    state.StartRotation,
                    state.TargetRotation,
                    moveT);

            //
            // 収まる瞬間にほんの少しだけ膨らみ、
            // その後TargetScaleへ定着。
            //
            Vector3 scale =
                Vector3.Lerp(
                    state.StartScale,
                    state.TargetScale,
                    moveT);

            if (localT >= approachEnd)
            {
                float settleT =
                    Mathf.InverseLerp(
                        approachEnd,
                        1f,
                        localT);

                float pulse =
                    Mathf.Sin(
                        settleT *
                        Mathf.PI);

                scale =
                    state.TargetScale *
                    (
                        1f +
                        settleScaleOvershoot *
                        pulse
                    );
            }

            //
            // Final Crystalが形成されるにつれ、
            // 下段から順にFragmentを一体化させる。
            //
            //
            // 重要:
            // 欠片は「積み上がった姿」を一度見せてから
            // Final Crystalへ一体化する。
            //
            // stackEndより前には絶対に縮めない。
            //
            float mergeStart =
                Mathf.Max(
                    fragmentMergeStart,
                    stackEnd) +
                state.HeightOrder *
                mergeHeightStagger;

            mergeStart =
                Mathf.Clamp(
                    mergeStart,
                    0f,
                    0.96f);

            if (progress >= mergeStart)
            {
                float mergeT =
                    Mathf.InverseLerp(
                        mergeStart,
                        1f,
                        progress);

                mergeT =
                    Mathf.SmoothStep(
                        0f,
                        1f,
                        mergeT);

                scale =
                    Vector3.Lerp(
                        scale,
                        state.TargetScale *
                        fragmentEndScale,
                        mergeT);
            }

            state.Transform.localScale =
                scale;

            bool visible =
                progress <
                0.999f &&
                scale.sqrMagnitude >
                0.000001f;

            state.Transform.gameObject.SetActive(
                visible);
        }
    }

    private Bounds CalculateFinalBounds()
    {
        Renderer[] renderers =
            finalCrystal.GetComponentsInChildren<Renderer>(
                true);

        bool hasBounds = false;

        Bounds bounds =
            new Bounds(
                finalCrystal.position,
                Vector3.one *
                0.1f);

        foreach (
            Renderer renderer
            in renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds =
                    renderer.bounds;

                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(
                    renderer.bounds);
            }
        }

        return bounds;
    }

    private void SetFinalFormProgress(
        float progress)
    {
        if (finalRenderers == null ||
            finalPropertyBlock == null)
        {
            return;
        }

        float value =
            Mathf.Clamp01(
                progress);

        foreach (
            Renderer renderer
            in finalRenderers)
        {
            if (renderer == null)
            {
                continue;
            }

            renderer.GetPropertyBlock(
                finalPropertyBlock);

            finalPropertyBlock.SetFloat(
                FormProgressId,
                value);

            renderer.SetPropertyBlock(
                finalPropertyBlock);
        }
    }

    private void DisableFragmentMotion(
        Transform fragment)
    {
        if (fragment == null)
        {
            return;
        }

        CrystalDustHeroMotion[] motions =
            fragment.GetComponentsInChildren<CrystalDustHeroMotion>(
                true);

        foreach (
            CrystalDustHeroMotion motion
            in motions)
        {
            if (motion != null)
            {
                motion.enabled = false;
            }
        }
    }

    private static Vector3 AbsVector(
        Vector3 value)
    {
        return new Vector3(
            Mathf.Abs(value.x),
            Mathf.Abs(value.y),
            Mathf.Abs(value.z));
    }

    private void ClearFragments()
    {
        foreach (
            FragmentState state
            in fragments)
        {
            if (state.Transform != null)
            {
                Destroy(
                    state.Transform.gameObject);
            }
        }

        fragments.Clear();
    }

    private void OnDestroy()
    {
        ClearFragments();
    }
}