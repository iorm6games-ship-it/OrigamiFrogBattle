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

    [Header("Scatter")]
    [SerializeField]
    private float startRadiusMultiplier = 2.4f;

    [SerializeField]
    private float startHeightMultiplier = 0.75f;

    [SerializeField]
    private Vector2 fragmentScaleRange =
        new Vector2(0.65f, 1.05f);

    [SerializeField]
    private float curveAmountMultiplier = 0.65f;

    [Header("Assembly")]
    [Range(0f, 0.8f)]
    [SerializeField]
    private float staggerAmount = 0.18f;

    [Range(0f, 1f)]
    [SerializeField]
    private float finalCrystalStart = 0.46f;

    [Range(0f, 1f)]
    [SerializeField]
    private float fragmentMergeStart = 0.68f;

    [SerializeField]
    private float fragmentEndScale = 0.16f;

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
        public float Delay;
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
        finalCrystal.gameObject.SetActive(true);

        floatPhaseOffset =
            Random.Range(
                -phaseOffsetRange,
                phaseOffsetRange);

        ClearFragments();
        CreateFragments();
        SetProgress(0f);
    }

    public void SetProgress(float progress)
    {
        if (center == null ||
            finalCrystal == null)
        {
            return;
        }

        float t =
            Mathf.Clamp01(progress);

        UpdateFragments(t);

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

        SetFinalFormProgress(formT);
    }

    public void CompleteAssembly()
    {
        if (finalCrystal == null)
        {
            IsComplete = true;
            return;
        }

        SetFinalFormProgress(1f);

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
            Mathf.PI * 2f +
            floatPhaseOffset;

        FloatSignal =
            Mathf.Sin(time);

        CurrentFloatOffset =
            FloatSignal *
            floatAmplitude;

        Vector3 axis =
            center.up.normalized;

        Vector3 swayAxis =
            center.forward.normalized;

        finalCrystal.position =
            finalBasePosition +
            axis * CurrentFloatOffset;

        finalCrystal.rotation =
            Quaternion.AngleAxis(
                FloatSignal * swayDegrees,
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

        Vector3 axis =
            center.up.normalized;

        Vector3 basisX =
            center.right.normalized;

        Vector3 basisZ =
            center.forward.normalized;

        float verticalExtent =
            Mathf.Max(
                0.08f,
                bounds.extents.magnitude * 0.52f);

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

        for (int i = 0;
             i < fragmentCount;
             i++)
        {
            Transform fragment =
                Instantiate(
                    fragmentPrefab,
                    finalBasePosition,
                    finalBaseRotation,
                    transform);

            CrystalDustHeroMotion motion =
                fragment.GetComponent<CrystalDustHeroMotion>();

            if (motion != null)
            {
                motion.enabled = false;
            }

            float normalizedIndex =
                fragmentCount <= 1
                    ? 0.5f
                    : i /
                      (float)(fragmentCount - 1);

            float height01 =
                Mathf.Lerp(
                    -1f,
                    1f,
                    normalizedIndex);

            float targetAngle =
                i *
                (360f /
                 Mathf.Max(1, fragmentCount)) +
                Random.Range(-18f, 18f);

            float targetAngleRad =
                targetAngle *
                Mathf.Deg2Rad;

            float targetRadius =
                horizontalExtent *
                Mathf.Lerp(
                    0.20f,
                    0.52f,
                    1f -
                    Mathf.Abs(height01));

            Vector3 targetRadial =
                basisX *
                Mathf.Cos(targetAngleRad) +
                basisZ *
                Mathf.Sin(targetAngleRad);

            Vector3 targetPosition =
                finalBasePosition +
                axis *
                (height01 * verticalExtent * 0.78f) +
                targetRadial * targetRadius;

            float startAngle =
                targetAngle +
                Random.Range(45f, 145f) *
                (Random.value < 0.5f ? -1f : 1f);

            float startAngleRad =
                startAngle *
                Mathf.Deg2Rad;

            Vector3 startRadial =
                basisX *
                Mathf.Cos(startAngleRad) +
                basisZ *
                Mathf.Sin(startAngleRad);

            Vector3 startPosition =
                finalBasePosition +
                startRadial *
                Random.Range(
                    startRadius * 0.72f,
                    startRadius * 1.20f) +
                axis *
                Random.Range(
                    -startHeightSpread,
                    startHeightSpread);

            Quaternion targetRotation =
                finalBaseRotation *
                Quaternion.Euler(
                    Random.Range(-18f, 18f),
                    Random.Range(-28f, 28f),
                    Random.Range(-18f, 18f));

            Quaternion startRotation =
                Random.rotation;

            float scaleMultiplier =
                Random.Range(
                    fragmentScaleRange.x,
                    fragmentScaleRange.y);

            Vector3 startScale =
                fragment.localScale *
                scaleMultiplier;

            Vector3 targetScale =
                startScale *
                Random.Range(0.82f, 1.08f);

            Vector3 curveDirection =
                Vector3.Cross(
                    axis,
                    startPosition -
                    targetPosition);

            if (curveDirection.sqrMagnitude < 0.0001f)
            {
                curveDirection =
                    basisX;
            }

            curveDirection.Normalize();

            float delay =
                fragmentCount <= 1
                    ? 0f
                    : normalizedIndex *
                      staggerAmount;

            fragment.position =
                startPosition;

            fragment.rotation =
                startRotation;

            fragment.localScale =
                startScale;

            fragments.Add(
                new FragmentState
                {
                    Transform = fragment,
                    StartPosition = startPosition,
                    StartRotation = startRotation,
                    StartScale = startScale,
                    TargetPosition = targetPosition,
                    TargetRotation = targetRotation,
                    TargetScale = targetScale,
                    CurveDirection = curveDirection,
                    Delay = delay
                });
        }
    }

    private void UpdateFragments(float progress)
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
                    state.Delay,
                    0.86f +
                    state.Delay * 0.25f,
                    progress);

            localT =
                Mathf.Clamp01(localT);

            float moveT =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    localT);

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
                0.22f;

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

            Vector3 scale =
                Vector3.Lerp(
                    state.StartScale,
                    state.TargetScale,
                    moveT);

            if (progress >= fragmentMergeStart)
            {
                float mergeT =
                    Mathf.InverseLerp(
                        fragmentMergeStart,
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

            state.Transform.gameObject.SetActive(
                progress < 0.995f);
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
                Vector3.one * 0.1f);

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
            Mathf.Clamp01(progress);

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
