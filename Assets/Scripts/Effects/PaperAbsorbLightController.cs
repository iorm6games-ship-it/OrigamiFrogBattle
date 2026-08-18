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

    }
    private void OnDestroy()
    {
        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
        }
    }
}