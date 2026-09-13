using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class CrystalFormationController : MonoBehaviour
{
    private enum FormationPhase
    {
        Idle,
        Seed,
        Build,
        Peak,
        Condense,
        CrystalForm,
        Complete
    }

    [Header("References")]
    [SerializeField]
    private CrystalDustHeroMotion dustPrefab;

    [SerializeField]
    private Transform birthPoint;

    [SerializeField]
    private CrystalVortexWindController vortexWindPrefab;

    [SerializeField]
    private CrystalCondenseGlowController condenseGlowPrefab;

    [SerializeField]
    private Transform crystalVisual;

    public Transform BirthPoint => birthPoint;

    [Header("Dust")]
    [Min(1)]
    [SerializeField]
    private int dustCount = 8;

    [Header("Seed")]
    [Min(1)]
    [SerializeField]
    private int seedDustCount = 2;

    [Min(0f)]
    [SerializeField]
    private float seedSpawnInterval = 0.06f;

    [Header("Build")]
    [Min(0f)]
    [SerializeField]
    private float buildDuration = 0.55f;

    [Header("Peak")]
    [Min(0f)]
    [SerializeField]
    private float peakRiseDuration = 0.22f;

    [Min(0f)]
    [SerializeField]
    private float peakHoldDuration = 0.16f;

    [Range(0f, 2f)]
    [SerializeField]
    private float peakRadiusScale = 1.20f;

    [Range(0f, 2f)]
    [SerializeField]
    private float peakHeightScale = 1.55f;

    [Range(0f, 2f)]
    [SerializeField]
    private float peakSpinScale = 1.75f;

    [Range(0f, 0.2f)]
    [SerializeField]
    private float peakPulseAmount = 0.07f;

    [Range(0f, 0.3f)]
    [SerializeField]
    private float peakPulseSpinBoost = 0.18f;

    [Header("Condense")]
    [Min(0.01f)]
    [SerializeField]
    private float condenseDuration = 0.52f;

    [Range(0.05f, 0.95f)]
    [SerializeField]
    private float condenseTightenPoint = 0.62f;

    [Range(0f, 2f)]
    [SerializeField]
    private float condenseMidRadiusScale = 0.28f;

    [Range(0f, 2f)]
    [SerializeField]
    private float condenseMidHeightScale = 0.22f;

    [Range(0f, 2f)]
    [SerializeField]
    private float condenseSpinPeak = 1.95f;

    [Header("Sparkle")]
    [Range(0f, 1f)]
    [SerializeField]
    private float seedSparkleEnergy = 0.18f;

    [Range(0f, 1f)]
    [SerializeField]
    private float buildSparkleEnergy = 0.65f;

    [Range(0f, 1f)]
    [SerializeField]
    private float peakSparkleEnergy = 1f;

    [Range(0f, 1f)]
    [SerializeField]
    private float condenseSparkleFadeStart = 0.55f;

    [Header("Condense Glow")]
    [Range(0f, 1f)]
    [SerializeField]
    private float condenseGlowStart = 0.15f;

    [SerializeField]
    private float condenseGlowStartScale = 1.30f;

    [SerializeField]
    private float condenseGlowEndScale = 0.24f;

    [Min(0.01f)]
    [SerializeField]
    private float condenseGlowFadeDuration = 0.16f;

    [Header("Crystal Form")]
    [Range(0f, 1f)]
    [SerializeField]
    private float crystalFormStartPoint = 0.82f;

    [Min(0.01f)]
    [SerializeField]
    private float crystalFormDuration = 0.42f;
    [SerializeField]
    private float crystalVisualScale = 0.82f;

    [SerializeField]
    private float crystalVisualLift = 1.20f;

    [Header("Crystal Assembly")]
    [SerializeField]
    private CrystalAssemblyController crystalAssemblyPrefab;

    private CrystalAssemblyController crystalAssemblyInstance;

    private Vector3 crystalInitialScale;

    //
    // birthPointのローカル座標基準。
    // 基本は(0,0,0)。
    // Model Pivotの都合で微調整が必要な場合のみ使う。
    //
    [SerializeField]
    private Vector3 crystalFormOffset = Vector3.zero;

    [Header("Vortex - Seed State")]
    [Range(0f, 2f)]
    [SerializeField]
    private float seedRadiusScale = 0.82f;

    [Range(0f, 2f)]
    [SerializeField]
    private float seedHeightScale = 0.80f;

    [Range(0f, 2f)]
    [SerializeField]
    private float seedSpinScale = 0.95f;
    private static readonly int FormProgressId =
        Shader.PropertyToID("_FormProgress");

    private Renderer[] crystalRenderers;

    private MaterialPropertyBlock crystalPropertyBlock;

    public bool IsPlaying { get; private set; }

    public float VortexRadiusScale { get; private set; } = 1f;
    public float VortexHeightScale { get; private set; } = 1f;
    public float VortexSpinScale { get; private set; } = 1f;
    public float CondenseProgress { get; private set; }
    public float SparkleEnergy { get; private set; }

    private FormationPhase phase =
        FormationPhase.Idle;

    private readonly List<CrystalDustHeroMotion> spawnedDust =
        new List<CrystalDustHeroMotion>();

    private CrystalVortexWindController vortexWindInstance;
    private CrystalCondenseGlowController condenseGlowInstance;

    private Vector3 condenseGlowBaseScale;

    private Vector3 crystalBaseScale;
    private Quaternion crystalBaseRotation;

    private bool crystalFormStarted;
    private bool crystalFormComplete;

    private void Awake()
    {
        crystalPropertyBlock =
            new MaterialPropertyBlock();

        if (crystalVisual != null)
        {
            crystalInitialScale =
                crystalVisual.localScale;

            crystalBaseScale =
                crystalVisual.localScale;

            crystalBaseRotation =
                crystalVisual.rotation;

            crystalRenderers =
                crystalVisual.GetComponentsInChildren<Renderer>(
                    true);

            SetCrystalFormProgress(0f);

            crystalVisual.gameObject.SetActive(false);
        }
    }
    private void SetCrystalFormProgress(
        float progress)
    {
        if (crystalRenderers == null)
        {
            return;
        }

        float value =
            Mathf.Clamp01(
                progress);

        foreach (
            Renderer renderer
            in crystalRenderers)
        {
            if (renderer == null)
            {
                continue;
            }

            renderer.GetPropertyBlock(
                crystalPropertyBlock);

            crystalPropertyBlock.SetFloat(
                FormProgressId,
                value);

            renderer.SetPropertyBlock(
                crystalPropertyBlock);
        }
    }
    public void Play()
    {
        if (IsPlaying)
        {
            return;
        }

        if (birthPoint == null ||
            dustPrefab == null)
        {
            Debug.LogWarning(
                $"{name}: Formationの参照が未設定です。");

            return;
        }

        StartCoroutine(
            PlayFormation());
    }

    private IEnumerator PlayFormation()
    {
        IsPlaying = true;

        ResetVisualState();

        spawnedDust.Clear();

        CreateRuntimeEffects();

        SetVortexState(
            seedRadiusScale,
            seedHeightScale,
            seedSpinScale,
            0f,
            seedSparkleEnergy);

        //
        // Seed
        //
        phase =
            FormationPhase.Seed;

        yield return
            PlaySeed();

        //
        // Build
        //
        phase =
            FormationPhase.Build;

        yield return
            PlayBuild();

        yield return new WaitUntil(
            AreAllDustReadyForPeak);

        //
        // Peak
        //
        phase =
            FormationPhase.Peak;

        yield return
            PlayPeak();

        //
        // Condense
        //
        phase =
            FormationPhase.Condense;

        yield return
            PlayCondense();

        //
        // Crystal Form
        //
        phase =
            FormationPhase.CrystalForm;

        if (!crystalFormStarted)
        {
            StartCrystalFormation();
        }

        if (crystalVisual != null)
        {
            yield return new WaitUntil(
                () => crystalFormComplete);
        }

        //
        // Crystal完成後、
        // 凝縮光だけ短く残してFade Out。
        //
        yield return
            FadeOutCondenseGlow();

        //
        // Complete
        //
        phase =
            FormationPhase.Complete;

        CleanupRuntimeEffects();

        SetVortexState(
            1f,
            1f,
            1f,
            0f,
            0f);

        IsPlaying = false;

        phase =
            FormationPhase.Idle;
    }

    private void ResetVisualState()
    {
        crystalFormStarted = false;

        crystalFormComplete =
            crystalVisual == null;

        if (crystalVisual != null)
        {
            SetCrystalFormProgress(0f);

            crystalVisual.gameObject.SetActive(false);

            AlignCrystalToFormationPoint();

            crystalVisual.localScale =
                crystalInitialScale * crystalVisualScale;

            crystalVisual.rotation =
                crystalBaseRotation;
        }

        if (vortexWindInstance != null)
        {
            Destroy(
                vortexWindInstance.gameObject);

            vortexWindInstance = null;
        }

        if (condenseGlowInstance != null)
        {
            Destroy(
                condenseGlowInstance.gameObject);

            condenseGlowInstance = null;
        }

        if (crystalAssemblyInstance != null)
        {
            Destroy(
                crystalAssemblyInstance.gameObject);

            crystalAssemblyInstance = null;
        }
    }

    private void CreateRuntimeEffects()
    {
        if (condenseGlowPrefab != null)
        {
            condenseGlowInstance =
                Instantiate(
                    condenseGlowPrefab,
                    birthPoint.position,
                    birthPoint.rotation,
                    transform);

            condenseGlowBaseScale =
                condenseGlowInstance.transform.localScale;

            condenseGlowInstance.HideImmediate();

            condenseGlowInstance.gameObject.SetActive(false);
        }

        if (vortexWindPrefab != null)
        {
            vortexWindInstance =
                Instantiate(
                    vortexWindPrefab,
                    birthPoint.position,
                    birthPoint.rotation,
                    transform);

            vortexWindInstance.Play(
                this,
                birthPoint);
        }
    }

    private IEnumerator PlaySeed()
    {
        int actualSeedCount =
            Mathf.Clamp(
                seedDustCount,
                1,
                dustCount);

        for (int i = 0;
             i < actualSeedCount;
             i++)
        {
            SpawnDust(
                CrystalDustHeroMotion.VortexRole.Seed);

            if (i < actualSeedCount - 1 &&
                seedSpawnInterval > 0f)
            {
                yield return
                    new WaitForSeconds(
                        seedSpawnInterval);
            }
        }
    }

    private IEnumerator PlayBuild()
    {
        int actualSeedCount =
            Mathf.Clamp(
                seedDustCount,
                1,
                dustCount);

        int buildDustCount =
            dustCount -
            actualSeedCount;

        if (buildDustCount <= 0)
        {
            SetVortexState(
                1f,
                1f,
                1f,
                0f,
                buildSparkleEnergy);

            yield break;
        }

        float duration =
            Mathf.Max(
                0f,
                buildDuration);

        if (duration <= 0f)
        {
            for (int i = 0;
                 i < buildDustCount;
                 i++)
            {
                SpawnDust(
                    CrystalDustHeroMotion.VortexRole.Build);
            }

            SetVortexState(
                1f,
                1f,
                1f,
                0f,
                buildSparkleEnergy);

            yield break;
        }

        float spawnInterval =
            buildDustCount <= 1
                ? 0f
                : duration /
                  (buildDustCount - 1);

        int spawnedCount = 0;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            while (
                spawnedCount < buildDustCount &&
                elapsed >=
                spawnInterval *
                spawnedCount)
            {
                SpawnDust(
                    CrystalDustHeroMotion.VortexRole.Build);

                spawnedCount++;
            }

            float t =
                Mathf.Clamp01(
                    elapsed /
                    duration);

            float easedT =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    t);

            SetVortexState(
                Mathf.Lerp(
                    seedRadiusScale,
                    1f,
                    easedT),

                Mathf.Lerp(
                    seedHeightScale,
                    1f,
                    easedT),

                Mathf.Lerp(
                    seedSpinScale,
                    1f,
                    easedT),

                0f,

                Mathf.Lerp(
                    seedSparkleEnergy,
                    buildSparkleEnergy,
                    easedT));

            elapsed +=
                Time.deltaTime;

            yield return null;
        }

        while (
            spawnedCount <
            buildDustCount)
        {
            SpawnDust(
                CrystalDustHeroMotion.VortexRole.Build);

            spawnedCount++;
        }

        SetVortexState(
            1f,
            1f,
            1f,
            0f,
            buildSparkleEnergy);
    }

    private IEnumerator PlayPeak()
    {
        //
        // Build状態からPeakへ一気に持ち上げる。
        //
        yield return
            AnimateVortexState(
                peakRiseDuration,
                peakRadiusScale,
                peakHeightScale,
                peakSpinScale,
                0f,
                peakSparkleEnergy);

        float duration =
            Mathf.Max(
                0f,
                peakHoldDuration);

        if (duration <= 0f)
        {
            yield break;
        }

        float elapsed = 0f;

        //
        // 完全静止Holdではなく、
        // 高エネルギー状態で一度だけ脈動させる。
        //
        while (elapsed < duration)
        {
            float t =
                Mathf.Clamp01(
                    elapsed /
                    duration);

            float pulse =
                Mathf.Sin(
                    Mathf.PI *
                    t);

            SetVortexState(
                peakRadiusScale *
                (
                    1f +
                    peakPulseAmount *
                    pulse
                ),

                peakHeightScale *
                (
                    1f +
                    peakPulseAmount *
                    pulse
                ),

                peakSpinScale +
                peakPulseSpinBoost *
                pulse,

                0f,

                peakSparkleEnergy);

            elapsed +=
                Time.deltaTime;

            yield return null;
        }

        SetVortexState(
            peakRadiusScale,
            peakHeightScale,
            peakSpinScale,
            0f,
            peakSparkleEnergy);
    }

    private IEnumerator PlayCondense()
    {
        float duration =
            Mathf.Max(
                0.01f,
                condenseDuration);

        float startRadius =
            VortexRadiusScale;

        float startHeight =
            VortexHeightScale;

        float startSpin =
            VortexSpinScale;

        float startSparkle =
            SparkleEnergy;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t =
                Mathf.Clamp01(
                    elapsed /
                    duration);

            float radiusScale;
            float heightScale;
            float spinScale;

            //
            // Condense前半
            //
            // 半径・高さは強く縮む。
            // 回転だけはさらに加速する。
            //
            if (t < condenseTightenPoint)
            {
                float localT =
                    t /
                    condenseTightenPoint;

                localT =
                    Mathf.SmoothStep(
                        0f,
                        1f,
                        localT);

                radiusScale =
                    Mathf.Lerp(
                        startRadius,
                        condenseMidRadiusScale,
                        localT);

                heightScale =
                    Mathf.Lerp(
                        startHeight,
                        condenseMidHeightScale,
                        localT);

                spinScale =
                    Mathf.Lerp(
                        startSpin,
                        condenseSpinPeak,
                        localT);
            }
            //
            // Condense後半
            //
            // エネルギーを一点へ潰し、
            // 最後に回転も完全停止。
            //
            else
            {
                float localT =
                    Mathf.InverseLerp(
                        condenseTightenPoint,
                        1f,
                        t);

                localT =
                    Mathf.SmoothStep(
                        0f,
                        1f,
                        localT);

                radiusScale =
                    Mathf.Lerp(
                        condenseMidRadiusScale,
                        0f,
                        localT);

                heightScale =
                    Mathf.Lerp(
                        condenseMidHeightScale,
                        0f,
                        localT);

                spinScale =
                    Mathf.Lerp(
                        condenseSpinPeak,
                        0f,
                        localT);
            }

            //
            // Dustの煌めきを
            // 中心Glowへ受け渡す。
            //
            float sparkleFade =
                Mathf.InverseLerp(
                    condenseSparkleFadeStart,
                    1f,
                    t);

            sparkleFade =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    sparkleFade);

            float sparkleEnergy =
                Mathf.Lerp(
                    startSparkle,
                    0f,
                    sparkleFade);

            SetVortexState(
                radiusScale,
                heightScale,
                spinScale,
                t,
                sparkleEnergy);

            UpdateCondenseGlow(
                t);

            //
            // Condenseが十分進み、
            // 強い核ができてからCrystal Form開始。
            //
            if (!crystalFormStarted &&
                t >= crystalFormStartPoint)
            {
                StartCrystalFormation();
            }

            elapsed +=
                Time.deltaTime;

            yield return null;
        }

        SetVortexState(
            0f,
            0f,
            0f,
            1f,
            0f);

        UpdateCondenseGlow(
            1f);

        if (!crystalFormStarted)
        {
            StartCrystalFormation();
        }
    }

    private void UpdateCondenseGlow(
        float condenseT)
    {
        if (condenseGlowInstance == null ||
            condenseT < condenseGlowStart)
        {
            return;
        }

        if (!condenseGlowInstance.gameObject.activeSelf)
        {
            condenseGlowInstance.gameObject.SetActive(true);
        }

        float glowT =
            Mathf.InverseLerp(
                condenseGlowStart,
                1f,
                condenseT);

        glowT =
            Mathf.SmoothStep(
                0f,
                1f,
                glowT);

        //
        // 広く淡いGlowから、
        // 小さく強い核へ。
        //
        float glowScale =
            Mathf.Lerp(
                condenseGlowStartScale,
                condenseGlowEndScale,
                glowT);

        condenseGlowInstance.transform.localScale =
            condenseGlowBaseScale *
            glowScale;

        condenseGlowInstance.SetProgress(
            glowT);
    }

    private void StartCrystalFormation()
    {
        if (crystalFormStarted)
        {
            return;
        }

        crystalFormStarted = true;

        StartCoroutine(
            PlayCrystalFormation());
    }

    private IEnumerator PlayCrystalFormation()
    {
        if (crystalVisual == null)
        {
            crystalFormComplete = true;
            yield break;
        }

        //
        // Final Crystalは最初から完成位置・完成サイズ・完成角度に置く。
        // 「出現」ではなく、Fragmentが集まりながら
        // ShaderのFormProgressで一体化していく。
        //
        AlignCrystalToFormationPoint();

        crystalVisual.localScale =
            crystalInitialScale *
            crystalVisualScale;

        crystalVisual.rotation =
            crystalBaseRotation;

        SetCrystalFormProgress(0f);

        crystalVisual.gameObject.SetActive(true);

        if (crystalAssemblyInstance != null)
        {
            Destroy(
                crystalAssemblyInstance.gameObject);

            crystalAssemblyInstance = null;
        }

        if (crystalAssemblyPrefab != null)
        {
            crystalAssemblyInstance =
                Instantiate(
                    crystalAssemblyPrefab,
                    birthPoint.position,
                    birthPoint.rotation,
                    transform);

            crystalAssemblyInstance.Begin(
                birthPoint,
                crystalVisual);
        }

        float duration =
            Mathf.Max(
                0.01f,
                crystalFormDuration);

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed +=
                Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    duration);

            float formT =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    t);

            if (crystalAssemblyInstance != null)
            {
                crystalAssemblyInstance.SetProgress(
                    formT);
            }
            else
            {
                // Assembly Prefab未設定時のFallback。
                SetCrystalFormProgress(
                    formT);
            }

            yield return null;
        }

        if (crystalAssemblyInstance != null)
        {
            crystalAssemblyInstance.SetProgress(1f);
            crystalAssemblyInstance.CompleteAssembly();
        }
        else
        {
            SetCrystalFormProgress(1f);
        }

        crystalFormComplete = true;
    }

    private void AlignCrystalToFormationPoint()
    {
        if (crystalVisual == null ||
            birthPoint == null)
        {
            return;
        }

        //
        // crystalFormOffsetはbirthPointのローカル座標基準。
        // crystalVisualLiftは渦の軸方向へ持ち上げる量。
        //
        Vector3 basePosition =
            birthPoint.TransformPoint(
                crystalFormOffset);

        crystalVisual.position =
            basePosition +
            birthPoint.up *
            crystalVisualLift;
    }

    private IEnumerator FadeOutCondenseGlow()
    {
        if (condenseGlowInstance == null)
        {
            yield break;
        }

        float duration =
            Mathf.Max(
                0.01f,
                condenseGlowFadeDuration);

        Vector3 startScale =
            condenseGlowInstance.transform.localScale;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed +=
                Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    duration);

            float eased =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    t);

            condenseGlowInstance.SetProgress(
                1f -
                eased);

            condenseGlowInstance.transform.localScale =
                Vector3.Lerp(
                    startScale,
                    condenseGlowBaseScale *
                    0.48f,
                    eased);

            yield return null;
        }

        Destroy(
            condenseGlowInstance.gameObject);

        condenseGlowInstance = null;
    }

    private IEnumerator AnimateVortexState(
        float duration,
        float targetRadiusScale,
        float targetHeightScale,
        float targetSpinScale,
        float targetCondenseProgress,
        float targetSparkleEnergy)
    {
        float actualDuration =
            Mathf.Max(
                0f,
                duration);

        if (actualDuration <= 0f)
        {
            SetVortexState(
                targetRadiusScale,
                targetHeightScale,
                targetSpinScale,
                targetCondenseProgress,
                targetSparkleEnergy);

            yield break;
        }

        float startRadius =
            VortexRadiusScale;

        float startHeight =
            VortexHeightScale;

        float startSpin =
            VortexSpinScale;

        float startCondense =
            CondenseProgress;

        float startSparkle =
            SparkleEnergy;

        float elapsed = 0f;

        while (elapsed < actualDuration)
        {
            float t =
                Mathf.Clamp01(
                    elapsed /
                    actualDuration);

            float easedT =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    t);

            SetVortexState(
                Mathf.Lerp(
                    startRadius,
                    targetRadiusScale,
                    easedT),

                Mathf.Lerp(
                    startHeight,
                    targetHeightScale,
                    easedT),

                Mathf.Lerp(
                    startSpin,
                    targetSpinScale,
                    easedT),

                Mathf.Lerp(
                    startCondense,
                    targetCondenseProgress,
                    easedT),

                Mathf.Lerp(
                    startSparkle,
                    targetSparkleEnergy,
                    easedT));

            elapsed +=
                Time.deltaTime;

            yield return null;
        }

        SetVortexState(
            targetRadiusScale,
            targetHeightScale,
            targetSpinScale,
            targetCondenseProgress,
            targetSparkleEnergy);
    }

    private void SpawnDust(
        CrystalDustHeroMotion.VortexRole role)
    {
        CrystalDustHeroMotion dust =
            Instantiate(
                dustPrefab,
                birthPoint.position,
                birthPoint.rotation,
                transform);

        dust.gameObject.SetActive(true);

        spawnedDust.Add(
            dust);

        dust.Play(
            birthPoint.position,
            birthPoint.up,
            role,
            this);
    }

    private bool AreAllDustReadyForPeak()
    {
        foreach (
            CrystalDustHeroMotion dust
            in spawnedDust)
        {
            if (dust != null &&
                !dust.IsVortexReady)
            {
                return false;
            }
        }

        return true;
    }

    private void SetVortexState(
        float radiusScale,
        float heightScale,
        float spinScale,
        float condenseProgress,
        float sparkleEnergy)
    {
        VortexRadiusScale =
            Mathf.Max(
                0f,
                radiusScale);

        VortexHeightScale =
            Mathf.Max(
                0f,
                heightScale);

        VortexSpinScale =
            Mathf.Max(
                0f,
                spinScale);

        CondenseProgress =
            Mathf.Clamp01(
                condenseProgress);

        SparkleEnergy =
            Mathf.Clamp01(
                sparkleEnergy);
    }

    private void CleanupRuntimeEffects()
    {
        foreach (
            CrystalDustHeroMotion dust
            in spawnedDust)
        {
            if (dust != null)
            {
                dust.CompleteFormation();
            }
        }

        spawnedDust.Clear();

        if (vortexWindInstance != null)
        {
            vortexWindInstance.CompleteFormation();

            vortexWindInstance = null;
        }

        if (condenseGlowInstance != null)
        {
            Destroy(
                condenseGlowInstance.gameObject);

            condenseGlowInstance = null;
        }
    }
}