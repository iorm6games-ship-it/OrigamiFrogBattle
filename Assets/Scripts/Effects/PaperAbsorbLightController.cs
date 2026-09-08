using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PaperAbsorbLightController : MonoBehaviour
{
    private static readonly int AbsorbCenterId =
        Shader.PropertyToID("_AbsorbCenter");

    private static readonly int AbsorbProgressId =
        Shader.PropertyToID("_AbsorbProgress");

    private static readonly int AbsorbFadeId =
        Shader.PropertyToID("_AbsorbFade");

    private static readonly int FlashStepId =
    Shader.PropertyToID("_FlashStep");

    private static readonly int FoldStepFlashId =
        Shader.PropertyToID("_FoldStepFlash");

    [SerializeField]
    private float fadeOutDuration = 0.5f;

    [Header("References")]
    [SerializeField]
    private Renderer targetRenderer;

    [Header("Animation")]
    [SerializeField]
    private Vector2 absorbCenter =
        new Vector2(0.5f, 0.5f);

    [SerializeField]
    private float startProgress = 0f;

    [SerializeField]
    private float endProgress = 0.8f;

    private Material runtimeMaterial;
    public Renderer TargetRenderer =>
        targetRenderer;

    private static readonly int PreFoldFlashId =
        Shader.PropertyToID("_PreFoldFlash");

    private static readonly int AbsorbBranchStrengthId =
        Shader.PropertyToID("_AbsorbBranchStrength");
    private static readonly int AbsorbBranchSpreadId =
        Shader.PropertyToID("_AbsorbBranchSpread");
    
    [Header("Branch")]
    [SerializeField, Range(0f, 1f)]
    private float branchStartNormalizedProgress = 0.33f;
    [SerializeField, Range(0f, 1f)]
    private float branchEndNormalizedProgress = 0.9f;
    [SerializeField, Range(0f, 1f)]
    private float branchMaxStrength = 0.1f;
    private static readonly int AbsorbTrailFadeId =
        Shader.PropertyToID("_AbsorbTrailFade");
    [SerializeField]
    private float trailFadeDuration = 0.35f;
    private static readonly int CrystalCenterChargeId =
        Shader.PropertyToID(
            "_CrystalCenterCharge"
        );

    private static readonly int CrystalShotProgressId =
        Shader.PropertyToID(
            "_CrystalShotProgress"
        );

    private static readonly int CrystalShotStrengthId =
        Shader.PropertyToID(
            "_CrystalShotStrength"
        );

    private static readonly int CrystalCornerChargeId =
        Shader.PropertyToID(
            "_CrystalCornerCharge"
        );

    private static readonly int CrystalCornerPulseId =
        Shader.PropertyToID(
            "_CrystalCornerPulse"
        );
    [Header("Crystal Ignition")]

    [Min(0.01f)]
    [SerializeField]
    private float centerChargeDuration = 0.10f;

    [Min(0.01f)]
    [SerializeField]
    private float crystalShotDuration = 0.13f;

    [Min(0.01f)]
    [SerializeField]
    private float cornerChargeDuration = 0.16f;

    [Min(0f)]
    [SerializeField]
    private float preCrystalHoldDuration = 0.08f;

    [Min(0f)]
    [SerializeField]
    private float crystalChargeFadeDelay = 0.08f;

    [Min(0.01f)]
    [SerializeField]
    private float crystalChargeFadeDuration = 0.22f;

    private void Awake()
    {
        if (targetRenderer == null)
        {
            Debug.LogError(
                $"{nameof(PaperAbsorbLightController)}: " +
                "Target Renderer が未設定です",
                this
            );
            enabled = false;
            return;
        }

        runtimeMaterial = targetRenderer.material;

        ResetEffect();
    }

    public IEnumerator PlayAbsorb(
        Action<float> onProgress,
        float firstPhaseDuration,
        float secondPhaseDuration,
        float firstPhaseEndProgress
    )
    {

        if (!enabled || runtimeMaterial == null)
        {
            yield break;
        }

        yield return PlayAbsorbRoutine(
            onProgress,
            firstPhaseDuration,
            secondPhaseDuration,
            firstPhaseEndProgress
        );
    }

    public void SetPreFoldFlash(
        float amount
    )
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        runtimeMaterial.SetFloat(
            PreFoldFlashId,
            Mathf.Clamp01(amount)
        );
    }

    public void SetFoldStepFlash(
        float stepValue,
        float amount
    )
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        runtimeMaterial.SetFloat(
            FlashStepId,
            Mathf.Clamp01(stepValue)
        );

        runtimeMaterial.SetFloat(
            FoldStepFlashId,
            Mathf.Clamp01(amount)
        );

        // 一時的な確認ログ
        if (amount >= 0.9f)
        {
            Debug.Log(
                $"[FoldFlashValue] " +
                $"renderer={targetRenderer.name}, " +
                $"sameMaterial={runtimeMaterial == targetRenderer.sharedMaterial}, " +
                $"hasStep={runtimeMaterial.HasProperty(FlashStepId)}, " +
                $"hasFlash={runtimeMaterial.HasProperty(FoldStepFlashId)}, " +
                $"step={runtimeMaterial.GetFloat(FlashStepId):F3}, " +
                $"amount={runtimeMaterial.GetFloat(FoldStepFlashId):F3}",
                this
            );
        }
    }

    public void ClearFoldStepFlash()
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        runtimeMaterial.SetFloat(
            FoldStepFlashId,
            0f
        );
    }

    public void ClearPreFoldFlash()
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        runtimeMaterial.SetFloat(
            PreFoldFlashId,
            0f
        );
    }

    public void ResetEffect()
    {
        
        if (runtimeMaterial == null)
        {
            return;
        }

        runtimeMaterial.SetVector(
            AbsorbCenterId,
            absorbCenter
        );

        runtimeMaterial.SetFloat(
            AbsorbProgressId,
            startProgress
        );

        runtimeMaterial.SetFloat(
            AbsorbFadeId,
            0f
        );

        runtimeMaterial.SetFloat(
            PreFoldFlashId,
            0f
        );
        runtimeMaterial.SetFloat(
            FlashStepId,
            0f
        );

        runtimeMaterial.SetFloat(
            FoldStepFlashId,
            0f
        );

        runtimeMaterial.SetFloat(
            AbsorbBranchSpreadId,
            0f
        );
        runtimeMaterial.SetFloat(
            AbsorbBranchStrengthId,
            0f
        );
        runtimeMaterial.SetFloat(
            AbsorbTrailFadeId,
            0f
        );
        runtimeMaterial.SetFloat(
            CrystalCenterChargeId,
            0f
        );

        runtimeMaterial.SetFloat(
            CrystalShotProgressId,
            0f
        );

        runtimeMaterial.SetFloat(
            CrystalShotStrengthId,
            0f
        );

        runtimeMaterial.SetFloat(
            CrystalCornerChargeId,
            0f
        );

        runtimeMaterial.SetFloat(
            CrystalCornerPulseId,
            0f
        );
    }
    private IEnumerator PlayCrystalIgnitionRoutine()
    {
        // -------------------------
        // 初期状態
        // -------------------------

        runtimeMaterial.SetFloat(
            CrystalCenterChargeId,
            0f
        );

        runtimeMaterial.SetFloat(
            CrystalShotProgressId,
            0f
        );

        runtimeMaterial.SetFloat(
            CrystalShotStrengthId,
            0f
        );

        runtimeMaterial.SetFloat(
            CrystalCornerChargeId,
            0f
        );

        runtimeMaterial.SetFloat(
            CrystalCornerPulseId,
            0f
        );

        // -------------------------
        // 1. 中心に一度ギュッと溜める
        // -------------------------

        float time = 0f;

        while (time < centerChargeDuration)
        {
            time += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    time /
                    centerChargeDuration
                );

            float eased =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    t
                );

            runtimeMaterial.SetFloat(
                CrystalCenterChargeId,
                eased
            );

            yield return null;
        }

        runtimeMaterial.SetFloat(
            CrystalCenterChargeId,
            1f
        );

        // -------------------------
        // 2. 中心 → 四隅へシュッ
        // -------------------------

        time = 0f;

        runtimeMaterial.SetFloat(
            CrystalShotStrengthId,
            1f
        );

        while (time < crystalShotDuration)
        {
            time += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    time /
                    crystalShotDuration
                );

            // 最初に強く飛び出して、
            // 四隅へ近づくほど少し減速
            float shotT =
                1f -
                Mathf.Pow(
                    1f - t,
                    3f
                );

            runtimeMaterial.SetFloat(
                CrystalShotProgressId,
                shotT
            );

            yield return null;
        }

        runtimeMaterial.SetFloat(
            CrystalShotProgressId,
            1f
        );

        runtimeMaterial.SetFloat(
            CrystalShotStrengthId,
            0f
        );

        // -------------------------
        // 3. 到達点で光が膨らむ
        // -------------------------

        time = 0f;

        while (time < cornerChargeDuration)
        {
            time += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    time /
                    cornerChargeDuration
                );

            float charge =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    t
                );

            // 到達直後だけ軽く「ポン」と光る
            float pulse =
                Mathf.Sin(
                    t *
                    Mathf.PI
                );

            runtimeMaterial.SetFloat(
                CrystalCornerChargeId,
                charge
            );

            runtimeMaterial.SetFloat(
                CrystalCornerPulseId,
                pulse
            );

            yield return null;
        }

        runtimeMaterial.SetFloat(
            CrystalCornerChargeId,
            1f
        );

        runtimeMaterial.SetFloat(
            CrystalCornerPulseId,
            0f
        );

        // 氷晶が出る直前の一瞬
        if (preCrystalHoldDuration > 0f)
        {
            yield return new WaitForSeconds(
                preCrystalHoldDuration
            );
        }
    }

    private IEnumerator PlayAbsorbRoutine(
        Action<float> onProgress,
        float firstPhaseDuration,
        float secondPhaseDuration,
        float firstPhaseEndProgress
    )
    {
        
        firstPhaseDuration =
            Mathf.Max(
                0.01f,
                firstPhaseDuration
            );
        
        secondPhaseDuration =
            Mathf.Max(
                0.01f,
                secondPhaseDuration
            );

        firstPhaseEndProgress =
            Mathf.Clamp01(
                firstPhaseEndProgress
            );
        // UpdateBranch(0f);
        
        // まず内部状態を初期化
        runtimeMaterial.SetVector(
            AbsorbCenterId,
            absorbCenter
        );

        runtimeMaterial.SetFloat(
            AbsorbProgressId,
            startProgress
        );

        // 初期化してから表示開始
        runtimeMaterial.SetFloat(
            AbsorbFadeId,
            1f
        );

        // -------------------------
        // 1. 中心から外へ浸透
        // -------------------------

        float time = 0f;
        
        onProgress?.Invoke(0f);

        while (time < firstPhaseDuration)
        {
            time += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    time / firstPhaseDuration
                );

            float eased =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    t
                );
            float normalizedProgress =
                Mathf.Lerp(
                    0f,
                    firstPhaseEndProgress,
                    eased
                );
            // UpdateBranch(normalizedProgress);

            float shaderProgress =
                Mathf.Lerp(
                    startProgress,
                    endProgress,
                    normalizedProgress
                );

            runtimeMaterial.SetFloat(
                AbsorbProgressId,
                shaderProgress
            );

            onProgress?.Invoke(
                normalizedProgress
            );

            yield return null;
        }
        // UpdateBranch(firstPhaseEndProgress);
        // 境界を正確に固定
        float phase1ShaderProgress =
            Mathf.Lerp(
                startProgress,
                endProgress,
                firstPhaseEndProgress
            );
        
        runtimeMaterial.SetFloat(
            AbsorbProgressId,
            phase1ShaderProgress
        );

        onProgress?.Invoke(
            firstPhaseEndProgress
        );

        // -------------------------
        // 2. 33% → 100％
        //    魂が消えた後、ゆっくり紙全体へ広がる
        // -------------------------

        time = 0f;

        while (time < secondPhaseDuration)
        {
            time += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    time /
                    secondPhaseDuration
                );

            float eased =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    t
                );
            float normalizedProgress =
                Mathf.Lerp(
                    firstPhaseEndProgress,
                    1f,
                    eased
                );
            // UpdateBranch(normalizedProgress);
            float shaderProgress =
                Mathf.Lerp(
                    startProgress,
                    endProgress,
                    normalizedProgress
                );

            runtimeMaterial.SetFloat(
                AbsorbProgressId,
                shaderProgress
            );

            onProgress?.Invoke(normalizedProgress);

            yield return null;
        }

        // 完全に元の色
        runtimeMaterial.SetFloat(
            AbsorbProgressId,
            endProgress
        );
        onProgress?.Invoke(1f);
        
        yield return PlayCrystalIgnitionRoutine();

        // 尾を中心から四隅へ向けて消す
        // yield return PlayTrailFadeRoutine();
        
        // 浸透範囲を固定したまま
        // 元の紙色へ戻す
        float fadeTime = 0f;

        while (fadeTime < fadeOutDuration)
        {
            fadeTime += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    fadeTime /
                    fadeOutDuration
                );
            
            float eased =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    t
                );

            runtimeMaterial.SetFloat(
                AbsorbFadeId,
                Mathf.Lerp(
                    1f,
                    0f,
                    eased
                )
            );
            yield return null;
        }
        Debug.Log($"Absorb finished. endProgress={endProgress}");
        runtimeMaterial.SetFloat(
            AbsorbFadeId,
            0f
        );

        runtimeMaterial.SetFloat(
            AbsorbProgressId,
            startProgress
        );
        // UpdateBranch(1f);

    }
    public IEnumerator FadeCrystalIgnition()
    {
        if (runtimeMaterial == null)
        {
            yield break;
        }

        if (crystalChargeFadeDelay > 0f)
        {
            yield return new WaitForSeconds(
                crystalChargeFadeDelay
            );
        }

        float time = 0f;

        while (time <
            crystalChargeFadeDuration)
        {
            time += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    time /
                    crystalChargeFadeDuration
                );

            float eased =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    t
                );

            float strength =
                1f - eased;

            runtimeMaterial.SetFloat(
                CrystalCenterChargeId,
                strength
            );

            runtimeMaterial.SetFloat(
                CrystalCornerChargeId,
                strength
            );

            yield return null;
        }

        runtimeMaterial.SetFloat(
            CrystalCenterChargeId,
            0f
        );

        runtimeMaterial.SetFloat(
            CrystalCornerChargeId,
            0f
        );

        runtimeMaterial.SetFloat(
            CrystalCornerPulseId,
            0f
        );
    }

    private void OnDestroy()
    {
        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
        }
    }

    private void UpdateBranch(
        float normalizedProgress
    )
    {
        float start =
            Mathf.Clamp(
                branchStartNormalizedProgress,
                0f,
                0.99f
            );
        
        float end =
            Mathf.Clamp(
                branchEndNormalizedProgress,
                start + 0.01f,
                1f
            );
        float t =
            Mathf.InverseLerp(
                start,
                end,
                Mathf.Clamp01(normalizedProgress)
            );
        float spread =
            Mathf.SmoothStep(
                0f,
                1f,
                t
            );
        
        // 移動が始まる頃には光が見えるように
        // Strength は Spread より早く最大へ到達させる
        float strengthT =
            Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(t * 2f)
            );
        
        runtimeMaterial.SetFloat(
            AbsorbBranchSpreadId,
            spread
        );

        runtimeMaterial.SetFloat(
            AbsorbBranchStrengthId,
            branchMaxStrength * strengthT
        );

    }

    private IEnumerator PlayTrailFadeRoutine()
    {
        float duration =
            Mathf.Max(
                0.01f,
                trailFadeDuration
            );
        float time = 0f;

        runtimeMaterial.SetFloat(
            AbsorbTrailFadeId,
            0f
        );

        while (time < duration)
        {
            time += Time.deltaTime;
            float t =
                Mathf.Clamp01(
                    time / duration
                );
            float eased =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    t
                );
            runtimeMaterial.SetFloat(
                AbsorbTrailFadeId,
                eased
            );

            yield return null;
        }

        runtimeMaterial.SetFloat(
            AbsorbTrailFadeId,
            1f
        );
    }
#if UNITY_EDITOR
    [ContextMenu("Test Crystal Shot Progress 0.5")]
    private void TestCrystalShotProgressHalf()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "Crystal Shot test は Play Mode で実行してください。",
                this
            );
            return;
        }

        if (runtimeMaterial == null)
        {
            Debug.LogWarning(
                "runtimeMaterial が初期化されていません。",
                this
            );
            return;
        }

        runtimeMaterial.SetFloat(
            CrystalShotProgressId,
            0.5f
        );

        Debug.Log(
            $"Crystal Shot direct material test: " +
            $"progress={runtimeMaterial.GetFloat(CrystalShotProgressId):F2}",
            this
        );
    }
#endif
}