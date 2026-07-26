using System.Collections;
using UnityEngine;

public sealed class PaperSelectionAppearController : MonoBehaviour
{
    [System.Serializable]
    public sealed class PaperEntry
    {
        [Header("References")]
        public Transform paper;
        public SkinnedMeshRenderer skinnedMeshRenderer;
        public Transform lightTarget;

        [Header("Timing")]
        [Min(0f)]
        public float delay;

        [Header("Start Position")]
        [Tooltip("降下開始位置の左右差")]
        public float startXOffset;
        
        [Tooltip("着地時の上下順。0が最も低い紙")]
        public float landingOrder;

        [Header("Variation")]
        [Range(-1f, 1f)]
        public float swayDirection = 1f;

        [Min(0.1f)]
        public float swaySpeedMultiplier = 1f;

        [Min(0f)]
        public float bendMultiplier = 1f;
    }

    [Header("Papers")]
    [SerializeField]
    private PaperEntry[] papers;

    [Header("Appear Motion")]
    [SerializeField]
    private float startYOffset = 5f;

    [Min(0.01f)]
    [SerializeField]
    private float duration = 4.5f;

    [Range(0.01f, 1f)]
    [SerializeField]
    private float startScaleMultiplier = 0.92f;

    [SerializeField]
    private AnimationCurve moveCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField]
    private float startYSpacing = 0.45f;

    [Header("Floating")]
    [Tooltip("紙全体の左右への漂い")]
    private float swayAmount = 0.08f;
    [Header("Flutter Rotation")]
    [SerializeField]
    private float pitchAmount = 4.5f;
    [SerializeField]
    private float yawAmount = 2.0f;

    [Tooltip("漂う速さ")]
    [SerializeField]
    private float swaySpeed = 2f;

    [Tooltip("紙全体が左右へ傾く角度")]
    [SerializeField]
    private float rollAmount = 3f;

    [Header("Blend Shapes")]
    [Range(0f, 100f)]
    [SerializeField]
    private float bendWeight = 80f;

    [Range(0f, 100f)]
    [SerializeField]
    private float bowWeight = 45f;

    [Tooltip("紙の変形速度。左右移動とは少し周期を変える")]
    [SerializeField]
    private float bendSpeedMultiplier = 1.35f;

    [Header("Settling")]
    [Range(0f, 1f)]
    [Tooltip("この進行率から揺れと変形を弱め始める")]
    [SerializeField]
    private float settleStart = 0.7f;

    [Header("Options")]
    [SerializeField]
    private bool playOnEnable = true;

    private Vector3[] targetPositions;
    private Quaternion[] targetRotations;
    private Vector3[] targetScales;

    private int[] bendLeftIndices;
    private int[] bendRightIndices;
    private int[] bowIndices;

    private Coroutine appearCoroutine;
    private bool initialized;

    private void Awake()
    {
        Initialize();
        ResetToStartPose();
    }

    private void OnEnable()
    {
        if (playOnEnable && initialized)
        {
            PlayAppearAnimation();
        }
    }

    private void Initialize()
    {
        if (papers == null || papers.Length == 0)
        {
            Debug.LogError(
                $"{nameof(PaperSelectionAppearController)}: " +
                "Papersが設定されていません。",
                this
            );

            enabled = false;
            return;
        }

        CacheTargets();
        CacheBlendShapeIndices();

        initialized = true;
    }

    public void PlayAppearAnimation()
    {
        if (!initialized)
        {
            Initialize();

            if (!initialized)
            {
                return;
            }
        }

        if (appearCoroutine != null)
        {
            StopCoroutine(appearCoroutine);
        }

        appearCoroutine = StartCoroutine(AppearSequence());
    }

    public void ResetToStartPose()
    {
        if (!initialized)
        {
            return;
        }

        for (int i = 0; i < papers.Length; i++)
        {
            PaperEntry entry = papers[i];

            if (entry.paper == null)
            {
                continue;
            }

            entry.paper.localPosition = GetStartPosition(i);
            entry.paper.localRotation = targetRotations[i];
            entry.paper.localScale =
                targetScales[i] * startScaleMultiplier;

            ResetBlendShapes(i);
        }
    }

    private void CacheTargets()
    {
        targetPositions = new Vector3[papers.Length];
        targetRotations = new Quaternion[papers.Length];
        targetScales = new Vector3[papers.Length];

        for (int i = 0; i < papers.Length; i++)
        {
            PaperEntry entry = papers[i];

            if (entry.paper == null)
            {
                Debug.LogWarning(
                    $"{nameof(PaperSelectionAppearController)}: " +
                    $"Papers[{i}]のPaperが設定されていません。",
                    this
                );

                continue;
            }

            targetPositions[i] = entry.paper.localPosition;
            targetRotations[i] = entry.paper.localRotation;
            targetScales[i] = entry.paper.localScale;
        }
    }

    private void CacheBlendShapeIndices()
    {
        bendLeftIndices = new int[papers.Length];
        bendRightIndices = new int[papers.Length];
        bowIndices = new int[papers.Length];

        for (int i = 0; i < papers.Length; i++)
        {
            bendLeftIndices[i] = -1;
            bendRightIndices[i] = -1;
            bowIndices[i] = -1;

            SkinnedMeshRenderer renderer =
                papers[i].skinnedMeshRenderer;

            if (renderer == null || renderer.sharedMesh == null)
            {
                Debug.LogWarning(
                    $"{nameof(PaperSelectionAppearController)}: " +
                    $"Papers[{i}]のSkinnedMeshRendererが設定されていません。",
                    this
                );

                continue;
            }

            Mesh mesh = renderer.sharedMesh;

            bendLeftIndices[i] =
                mesh.GetBlendShapeIndex("Float_Bend_Left");

            bendRightIndices[i] =
                mesh.GetBlendShapeIndex("Float_Bend_Right");

            bowIndices[i] =
                mesh.GetBlendShapeIndex("Float_Bow");
        }
    }

    private IEnumerator AppearSequence()
    {
        ResetToStartPose();

        float totalDuration = duration;

        for (int i = 0; i < papers.Length; i++)
        {
            totalDuration = Mathf.Max(
                totalDuration,
                papers[i].delay + duration
            );

            StartCoroutine(AnimatePaper(i));
        }

        yield return new WaitForSeconds(totalDuration);

        appearCoroutine = null;
    }

    private IEnumerator AnimatePaper(int index)
    {
        PaperEntry entry = papers[index];

        if (entry.paper == null)
        {
            yield break;
        }

        if (entry.delay > 0f)
        {
            yield return new WaitForSeconds(entry.delay);
        }
        Vector3 startPosition = GetStartPosition(index);

        Vector3 startScale =
            targetScales[index] * startScaleMultiplier;

        float elapsedTime = 0f;
        float phase = index * 1.37f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;

            float normalizedTime =
                Mathf.Clamp01(elapsedTime / duration);

            float easedTime =
                moveCurve.Evaluate(normalizedTime);
            float settleT =
                Mathf.InverseLerp(
                    settleStart,
                    1f,
                    normalizedTime
                );
            float settleAmount =
                1f - Mathf.SmoothStep(
                    0f,
                    1f,
                    settleT
                );

            float swayWave =
                Mathf.Sin(
                    elapsedTime *
                    swaySpeed *
                    entry.swaySpeedMultiplier +
                    phase
                );

            float bendWave =
                Mathf.Sin(
                    elapsedTime *
                    swaySpeed *
                    bendSpeedMultiplier *
                    entry.swaySpeedMultiplier +
                    phase * 0.8f
                );

            float bowWave =
                0.5f +
                0.5f *
                Mathf.Sin(
                    elapsedTime *
                    swaySpeed *
                    0.9f *
                    entry.swaySpeedMultiplier +
                    phase * 1.2f
                );

            UpdatePosition(
                index,
                startPosition,
                easedTime,
                swayWave,
                settleAmount
            );

            UpdateRotation(
                index,
                elapsedTime,
                phase,
                settleAmount
            );

            UpdateScale(
                index,
                startScale,
                easedTime
            );

            UpdateBlendShapes(
                index,
                bendWave,
                bowWave,
                settleAmount
            );

            yield return null;
        }

        SetFinalPose(index);
    }

    private Vector3 GetStartPosition(int index)
    {
        PaperEntry entry = papers[index];
        float centerOrder =
            (papers.Length -1) * 0.5f;
        float orderedYOffset =
            (entry.landingOrder -centerOrder)
            * startYSpacing;

        return targetPositions[index]
            + Vector3.up * 
                (startYOffset + orderedYOffset);
    }

    private void UpdatePosition(
        int index,
        Vector3 startPosition,
        float easedTime,
        float swayWave,
        float settleAmount
    )
    {
        PaperEntry entry = papers[index];

        // 縦方向の降下と最終位置への基本移動
        Vector3 position =
            Vector3.Lerp(
                startPosition,
                targetPositions[index],
                easedTime
            );
        
        // 紙ごとの左右への漂い
        float sidewaysOffset =
            swayWave *
            swayAmount *
            entry.swayDirection *
            settleAmount;

        position += Vector3.right * sidewaysOffset;

        entry.paper.localPosition = position;
    }

    private void UpdateRotation(
        int index,
        float elapsedTime,
        float phase,
        float settleAmount
    )
    {
        PaperEntry entry = papers[index];
        float speed = swaySpeed * entry.swaySpeedMultiplier;
        float pitch = 
            Mathf.Sin(elapsedTime * speed * 1.2f + phase) *
            pitchAmount *
            settleAmount;
        float yaw = 
            Mathf.Sin(elapsedTime * speed * 0.85f + phase * 1.35f) *
            yawAmount *
            settleAmount;
        float roll = 
            Mathf.Sin(elapsedTime * speed + phase * 0.7f) *
            rollAmount *
            entry.swayDirection *
            settleAmount;

        Quaternion floatingRotation =
            Quaternion.Euler(pitch, yaw, roll);

        entry.paper.localRotation =
            targetRotations[index] *
            floatingRotation;
    }

    private void UpdateScale(
        int index,
        Vector3 startScale,
        float easedTime
    )
    {
        papers[index].paper.localScale =
            Vector3.Lerp(
                startScale,
                targetScales[index],
                easedTime
            );
    }

    private void UpdateBlendShapes(
        int index,
        float bendWave,
        float bowWave,
        float settleAmount
    )
    {
        PaperEntry entry = papers[index];

        SkinnedMeshRenderer renderer =
            entry.skinnedMeshRenderer;

        if (renderer == null)
        {
            return;
        }

        float multiplier = entry.bendMultiplier;

        float leftWeight =
            Mathf.Max(0f, bendWave) *
            bendWeight *
            multiplier *
            settleAmount;

        float rightWeight =
            Mathf.Max(0f, -bendWave) *
            bendWeight *
            multiplier *
            settleAmount;

        float centerWeight =
            bowWave *
            bowWeight *
            multiplier *
            settleAmount;

        SetBlendShapeWeight(
            renderer,
            bendLeftIndices[index],
            leftWeight
        );

        SetBlendShapeWeight(
            renderer,
            bendRightIndices[index],
            rightWeight
        );

        SetBlendShapeWeight(
            renderer,
            bowIndices[index],
            centerWeight
        );
    }

    private void SetFinalPose(int index)
    {
        PaperEntry entry = papers[index];

        entry.paper.localPosition =
            targetPositions[index];

        entry.paper.localRotation =
            targetRotations[index];

        entry.paper.localScale =
            targetScales[index];

        ResetBlendShapes(index);
    }

    private void ResetBlendShapes(int index)
    {
        SkinnedMeshRenderer renderer =
            papers[index].skinnedMeshRenderer;

        if (renderer == null)
        {
            return;
        }

        SetBlendShapeWeight(
            renderer,
            bendLeftIndices[index],
            0f
        );

        SetBlendShapeWeight(
            renderer,
            bendRightIndices[index],
            0f
        );

        SetBlendShapeWeight(
            renderer,
            bowIndices[index],
            0f
        );
    }

    private static void SetBlendShapeWeight(
        SkinnedMeshRenderer renderer,
        int blendShapeIndex,
        float weight
    )
    {
        if (blendShapeIndex < 0)
        {
            return;
        }

        renderer.SetBlendShapeWeight(
            blendShapeIndex,
            weight
        );
    }
}