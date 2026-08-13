using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class SummonLightController : MonoBehaviour
{
    private static readonly int IntensityId =
        Shader.PropertyToID("_Intensity");
    
    [Header("Light Renderer")]
    [SerializeField]
    private SpriteRenderer lightGlow;

    [SerializeField]
    private SpriteRenderer lightCore;

    [SerializeField]
    private SpriteRenderer lightStreakHorizontal;

    [SerializeField]
    private SpriteRenderer lightStreakSub1;

    [SerializeField]
    private SpriteRenderer lightStreakSub2;
    
    [SerializeField]
    private SpriteRenderer lightStreakVertical;

    [Header("Sequence References")]
    [SerializeField]
    private Transform introLightPoint;

    [SerializeField]
    private PaperSelectionAppearController paperAppearController;

    [Header("Intro Flash Timing")]
    [Tooltip("光が点灯して最大になるまでの時間")]
    [Min(0.01f)]
    [SerializeField]
    private float fadeInDuration = 0.12f;
    
    [Tooltip("最大発行を維持する時間")]
    [Min(0f)]
    [SerializeField]
    private float peakHoldDuration = 0.06f;

    [Tooltip("折り紙の降下開始後、光が消えるまでの時間")]
    [Min(0.01f)]
    [SerializeField]
    private float fadeOutDuration = 0.27f;

    [Header("Intro Flash Scale")]
    [Tooltip("点灯開始時の光の大きさ")]
    [Min(0f)]
    [SerializeField]
    private float startScaleMultiplier = 0.65f;

    [Tooltip("最大発行時の光の大きさ")]
    [Min(0f)]
    [SerializeField]
    private float peakScaleMultiplier = 1.0f;

    [Tooltip("消える直前の光の大きさ")]
    [Min(0f)]
    [SerializeField]
    private float endScaleMultiplier = 1.18f;

    [Header("Options")]
    [Tooltip("Time.timeScaleの影響を受けずに再生する")]
    [SerializeField]
    private bool useUnscaledTime;

    [Tooltip("光が完全に消えてから折り紙が降下し始めるまでの間")]
    [Min(0f)]
    [SerializeField]
    private float afterFlashDelay = 0.12f;

    [Header("Intro Flash Strength")]
    [Tooltip("一瞬だけ発生する最大発行倍率")]
    [Min(1f)]
    [SerializeField]
    private float peakIntensityMultiplier = 2.6f;

    [Header("Selected Paper Light Motion")]

    [Tooltip("選択された紙へ光が降りる時間")]
    [Min(0.01f)]
    [SerializeField]
    private float selectedLightMoveDuration = 1.6f;

    [Tooltip("紙へ完全に接触する前に停止する距離")]
    [Min(0f)]
    [SerializeField]
    private float preContactDistance = 0.22f;

    [Tooltip("接触手前まで移動する時間")]
    [Min(0.01f)]
    [SerializeField]
    private float approachDuration = 1.3f;

    [Tooltip("接触手前で止まる時間")]
    [Min(0f)]
    [SerializeField]
    private float preContactHoldDuration = 1.2f;

    [Tooltip("降下を開始する前の短い間")]
    [Min(0f)]
    [SerializeField]
    private float beforeSelectedMoveDelay = 0.35f;
    
    [Tooltip("選択確定後、フラッシュ開始までの間")]
    [Min(0f)]
    [SerializeField]
    private float afterSelectionConfirmDelay = 0.3f;
    
    private SpriteRenderer[] lightRenderers;
    private float[] baseIntensities;

    private MaterialPropertyBlock propertyBlock;
    private bool runtimeCacheReady;

    private Vector3 baseLocalScale;
    private Coroutine introCoroutine;

    [Header("Selection Sequence")]
    [SerializeField]
    private PaperSelectionController paperSelectionController;

    [Header("Selection Flash")]
    [Tooltip("選択確定後の発光倍率")]
    [Min(1f)]
    [SerializeField]
    private float selectionPeakIntensityMultiplier = 3.8f;

    [Tooltip("選択確定後の点灯時間")]
    [Min(0.01f)]
    [SerializeField]
    private float selectionFadeInDuration = 0.07f;

    [Tooltip("選択確定後の最大発光維持時間")]
    [Min(0f)]
    [SerializeField]
    private float selectionPeakHoldDuration = 0.08f;

    [SerializeField]
    private FoldLineProgressController foldLineProgressController;

    [Header("Main Camera")]
    [SerializeField]
    private SummonCameraController summonCameraController;

    private void Awake()
    {
        
        BuildRuntimeCache();
        HideImmediately();
    }

    private void BuildRuntimeCache()
    {
        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        lightRenderers = new[]
        {
            lightGlow,
            lightCore,
            lightStreakHorizontal,
            lightStreakSub1,
            lightStreakSub2,
            lightStreakVertical
        };

        baseLocalScale = transform.localScale;
        CacheBaseIntensities();

        runtimeCacheReady = true;
    }

    /// <summary>
    /// 降下前の発光を再生し、
    /// 発行のピーク後に折り紙の降下を開始する
    /// </summary>
    public void PlayIntroSequence()
    {
        if (!runtimeCacheReady)
        {
            BuildRuntimeCache();
        }
        if (!isActiveAndEnabled)
        {
            Debug.LogWarning(
                $"{nameof(SummonLightController)}: " +
                "非アクティブなため再生できません",
                this
            );
            return;
        }

        if (introLightPoint == null)
        {
            Debug.LogError(
                $"{nameof(SummonLightController)}: " +
                "Intro Light Pointが設定されていません",
                this
            );
            return;
        }

        if (introCoroutine != null)
        {
            StopCoroutine(introCoroutine);
        }
        introCoroutine =
            StartCoroutine(PlayIntroSequenceCoroutine());
    }

    public void PlaySelectionSequence(
        PaperPullSelectable selectedPaper
    )
    {
        if (selectedPaper == null)
        {
            Debug.LogWarning(
                $"{nameof(SummonLightController)}: " +
                "Selected Paper がありません",
                this
            );
            return;
        }

        if (introCoroutine != null)
        {
            StopCoroutine(introCoroutine);
        }

        introCoroutine =
            StartCoroutine(
                PlaySelectionSequenceCoroutine(
                    selectedPaper
                )
            );
        
    }

    private IEnumerator PlaySelectionSequenceCoroutine(
        PaperPullSelectable selectedPaper
    )
    {

        Transform target =
            selectedPaper.LightTarget;
        
        if (foldLineProgressController != null)
        {
            foldLineProgressController.ResetFoldLine(
                selectedPaper.TargetRenderer
            );
        }
        yield return WaitForDuration(
            afterSelectionConfirmDelay
        );
        
        transform.position =
            introLightPoint.position;
        
        transform.localScale =
            baseLocalScale *
            startScaleMultiplier;
        
        SetRenderersEnabled(true);

        SetIntensityMultiplier(0f);

        // ピカっと点灯
        yield return AnimateLight(
            fromIntensity: 0f,
            toIntensity: selectionPeakIntensityMultiplier,
            fromScale: startScaleMultiplier,
            toScale: peakScaleMultiplier,
            duration: selectionFadeInDuration
        );

        // 一瞬だけ最大光量
        yield return HoldSelectionPeak();

        yield return AnimateLight(
            selectionPeakIntensityMultiplier,
            0f,
            peakScaleMultiplier,
            peakScaleMultiplier,
            0.08f
        );
        
        lightStreakHorizontal.enabled = false;
        lightStreakVertical.enabled = false;
        lightStreakSub1.enabled = false;
        lightStreakSub2.enabled = false;

        
        // ピカの後の一瞬の間
        yield return WaitForDuration(beforeSelectedMoveDelay);
        
        lightCore.enabled = true;
        lightGlow.enabled = true;

        yield return FadeCoreForDescent();

        // 紙の直前まで下りる
        yield return MoveLightNearTarget(target);

        // 降りたら少し停止してフワフワ浮遊する
        yield return HoverBeforeContact();

        // SummonLight は役目終了
        HideImmediately();

        // 紙の内部へ浸透
        yield return selectedPaper.PlayAbsorbLightOnly();

        // 折れ線を見せるためにカメラを少し寄せる
        if (summonCameraController != null)
        {
            yield return summonCameraController.ZoomIn();    
        }
        
        // 浮遊を止める
       selectedPaper.StopFloating();

       // 選択した紙に折れ線を走らせる
       if (foldLineProgressController != null)
        {
            yield return foldLineProgressController.PlayFoldLine(
                selectedPaper.TargetRenderer
            );
        }

        introCoroutine = null;
    }

    private IEnumerator FadeCoreForDescent()
    {
        float duration = 0.22f;
        float elapsedTime = 0f;

        float targetCoreIntensity = 0.6f;
        float targetGlowIntensity = 0.25f;

        while (elapsedTime < duration)
        {
            elapsedTime += GetDeltaTime();

            float t =
                Mathf.Clamp01(
                    elapsedTime / duration
                );
            
            float eased =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    t
                );
            
            SetRendererIntensity(
                lightCore,
                targetCoreIntensity * eased
            );

            SetRendererIntensity(
                lightGlow,
                targetGlowIntensity * eased
            );
            yield return null;
        }

        SetRendererIntensity(
            lightCore,
            targetCoreIntensity
        );
        SetRendererIntensity(
            lightGlow,
            targetGlowIntensity
        );
    }

    private void SetRendererIntensity(
        SpriteRenderer renderer,
        float multiplier
    )
    {
        if (renderer == null)
        {
            return;
        }

        int index =
            System.Array.IndexOf(
                lightRenderers,
                renderer
            );
        
        if (index < 0)
        {
            return;
        }

        propertyBlock.Clear();

        renderer.GetPropertyBlock(
            propertyBlock
        );

        propertyBlock.SetFloat(
            IntensityId,
            baseIntensities[index] *
            multiplier
        );

        renderer.SetPropertyBlock(
            propertyBlock
        );
    }
    private IEnumerator HoldSelectionPeak()
    {
        if (selectionPeakHoldDuration <= 0f)
        {
            yield break;
        }

        float elapsedTime = 0f;

        while (elapsedTime < selectionPeakHoldDuration)
        {
            elapsedTime += GetDeltaTime();

            SetIntensityMultiplier(
                selectionPeakIntensityMultiplier
            );

            transform.localScale =
                baseLocalScale *
                peakScaleMultiplier;
            yield return null;
        }
    }
    private IEnumerator MoveLightNearTarget(
        Transform target
    )
    {
        Vector3 startPosition =
            transform.position;

        Vector3 direction =
            (startPosition - target.position).normalized;

        Vector3 nearPosition =
            target.position +
            direction * preContactDistance;

        float elapsedTime = 0f;

        while (elapsedTime < approachDuration)
        {
            elapsedTime += GetDeltaTime();

            float normalizedTime =
                Mathf.Clamp01(
                    elapsedTime / approachDuration
                );

            float easedTime =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    normalizedTime
                );

            Vector3 position =
                Vector3.Lerp(
                    startPosition,
                    nearPosition,
                    easedTime
                );

            float arc =
                Mathf.Sin(
                    normalizedTime *
                    Mathf.PI
                );

            Vector3 sideDirection =
                Camera.main != null
                    ? Camera.main.transform.right
                    : Vector3.right;

            position +=
                sideDirection *
                arc *
                0.12f;

            float drift =
                Mathf.Sin(
                    elapsedTime * 12f
                ) * 0.015f;

            position +=
                sideDirection *
                drift *
                arc;

            transform.position = position;

            yield return null;
        }

        transform.position = nearPosition;
    }

    /// <summary>
    /// 折り紙の直前で停止したら
    /// その位置で浮遊する
    /// </summary>
    private IEnumerator HoverBeforeContact()
    {
        if (preContactHoldDuration <= 0f)
        {
            yield break;
        }

        Vector3 centerPosition =
            transform.position;

        float elapsedTime = 0f;

        while (elapsedTime < preContactHoldDuration)
        {
            elapsedTime += GetDeltaTime();

            float wave =
                Mathf.Sin(
                    elapsedTime * 6f
                );

            float sideWave =
                Mathf.Sin(
                    elapsedTime * 4.5f
                );

            Vector3 upDirection =
                Camera.main != null
                    ? Camera.main.transform.up
                    : Vector3.up;

            Vector3 sideDirection =
                Camera.main != null
                    ? Camera.main.transform.right
                    : Vector3.right;

            transform.position =
                centerPosition
                + upDirection * wave * 0.012f
                + sideDirection * sideWave * 0.006f;

            yield return null;
        }

        transform.position = centerPosition;
    }

    /// <summary>
    /// 再生の光を停止し、即座に非表示にする
    /// </summary>
    public void StopAndHide()
    {
        if (introCoroutine != null)
        {
            StopCoroutine(introCoroutine);
            introCoroutine = null;
        }

        HideImmediately();
    }

    [ContextMenu("Test Intro Sequence")]
    private void TestIntroSequence()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "テストはPlay mode中に実行してください",
                this

            );
            return;
        }
        PlayIntroSequence();
    }

    private IEnumerator PlayIntroSequenceCoroutine()
    {
        // IntroLightPointはSummonLightと同じ親でなくても使えるよう、
        // World Position を利用する
        transform.position = introLightPoint.position;

        SetRenderersEnabled(true);
        SetIntensityMultiplier(0f);

        transform.localScale =
            baseLocalScale * startScaleMultiplier;
        
        // 0.00 ~ 0.12秒：光が灯る
        yield return AnimateLight(
            fromIntensity: 0f,
            toIntensity: peakIntensityMultiplier,
            fromScale: startScaleMultiplier,
            toScale: peakScaleMultiplier,
            duration: fadeInDuration
        );

        // 0.12 ~ 0.18秒：最大発行を短く維持
        yield return HoldPeak();

        // 0.18 ~ 0.45秒：光の余韻を残して消える
        yield return AnimateLight(
            fromIntensity: peakIntensityMultiplier,
            toIntensity: 0f,
            fromScale: peakScaleMultiplier,
            toScale: endScaleMultiplier,
            duration: fadeOutDuration
        );

        SetIntensityMultiplier(0f);
        SetRenderersEnabled(false);

        transform.localScale = baseLocalScale;
        // 光が消えた後の短い静止
        yield return WaitForDuration(afterFlashDelay);
        StartPaperAppearance();
        introCoroutine = null;
    }

    private IEnumerator WaitForDuration(float duration)
    {
        if (duration <= 0)
        {
            yield break;
        }

        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += GetDeltaTime();
            yield return null;
        }
    }
    private IEnumerator AnimateLight(
        float fromIntensity,
        float toIntensity,
        float fromScale,
        float toScale,
        float duration
    )
    {
        if (duration <= 0f)
        {
            SetIntensityMultiplier(toIntensity);
            transform.localScale = baseLocalScale * toScale;
            yield break;
        }

        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            elapsedTime += GetDeltaTime();

            float normalizedTime = 
                Mathf.Clamp01(elapsedTime / duration);
            
            float easedTime =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    normalizedTime
                );
            float intensityMultiplier =
                Mathf.Lerp(
                    fromIntensity,
                    toIntensity,
                    easedTime
                );
            float scaleMultiplier =
                Mathf.Lerp(
                    fromScale,
                    toScale,
                    easedTime
                );
            SetIntensityMultiplier(intensityMultiplier);
            transform.localScale =
                baseLocalScale * scaleMultiplier;
            yield return null;
        }
        SetIntensityMultiplier(toIntensity);
        transform.localScale = baseLocalScale * toScale;
    }

    private IEnumerator HoldPeak()
    {
        if (peakHoldDuration <= 0f)
        {
            yield break;
        }
        float elapsedTime = 0f;
        while (elapsedTime < peakHoldDuration)
        {
            elapsedTime += GetDeltaTime();
            SetIntensityMultiplier(
                peakIntensityMultiplier
            );
            transform.localScale = baseLocalScale * peakScaleMultiplier;
            yield return null;
        }
    }

    private void StartPaperAppearance()
    {
        if (paperAppearController == null)
        {
            Debug.LogError(
                $"{nameof(SummonLightController)}: " +
                "Paper Appear Controllerが設定されていません",
                this
            );
            return;
        }
        
        GameObject paperRoot =
            paperAppearController.gameObject;
        
        if (!paperRoot.activeSelf)
        {
            paperRoot.SetActive(true);
        }

        if (!paperRoot.activeInHierarchy)
        {
            Debug.LogWarning(
                $"{nameof(SummonLightController)}: " +
                "PaperSelectionRoot の親が非アクティブです",
                paperRoot
            );
            return;
        }

        paperAppearController.PlayAppearAnimation();

    }

    private void CacheBaseIntensities()
    {
        baseIntensities = new float[lightRenderers.Length];
     
        for (int i = 0; i < lightRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer =
                lightRenderers[i];
            if (spriteRenderer == null)
            {
                baseIntensities[i] = 0f;
                continue;
            }
            Material material = 
                spriteRenderer.sharedMaterial;
            
            if (material != null && material.HasProperty(IntensityId))
            {
                baseIntensities[i] =
                    material.GetFloat(IntensityId);
            }
            else
            {
                baseIntensities[i] = 1f;

                Debug.LogWarning(
                    $"{spriteRenderer.name} のマテリアルに" +
                    "_Intensity プロパティが存在しません。 ",
                    spriteRenderer
                );
            }
            
        }
    }
    private void SetIntensityMultiplier(
        float multiplier
    )
    {
        multiplier = Mathf.Max(0f, multiplier);

        for (int i = 0; i < lightRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer =
                lightRenderers[i];
            if (spriteRenderer == null)
            {
                continue;
            }
            propertyBlock.Clear();
            spriteRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(
                IntensityId,
                baseIntensities[i] * multiplier
            );
            spriteRenderer.SetPropertyBlock(propertyBlock);
        }
    }
    
    private void SetRenderersEnabled(bool isEnabled)
    {
        foreach (SpriteRenderer renderer in lightRenderers)
        {
            if (renderer != null)
            {
                renderer.enabled = isEnabled;
            }
        }
    }

    private void HideImmediately()
    {
        SetIntensityMultiplier(0f);
        SetRenderersEnabled(false);
        transform.localScale = baseLocalScale;
    }

    private float GetDeltaTime()
    {
        return useUnscaledTime
            ? Time.unscaledDeltaTime
            : Time.deltaTime;
    }

    private void OnEnable()
    {
        if (paperSelectionController != null)
        {
            paperSelectionController.SelectionConfirmed +=
                HandleSelectionConfirmed;
        }
        
    }

    private void OnDisable()
    {
        if (paperSelectionController != null)
        {
            paperSelectionController.SelectionConfirmed -=
                HandleSelectionConfirmed;
        }
        
    }
    private void HandleSelectionConfirmed(
        PaperPullSelectable selectedPaper
    )
    {
        PlaySelectionSequence(
            selectedPaper
        );
    }
}