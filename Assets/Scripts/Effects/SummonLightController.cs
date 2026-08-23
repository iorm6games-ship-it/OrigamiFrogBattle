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
    [Header("Paper Absorb Timing")]

    [Tooltip("浸透開始から魂が完全に吸収されるまで")]
    [Min(0.01f)]
    [SerializeField]
    private float soulAbsorbDuration = 2f;

    [Tooltip("魂消失後、紙全体へ浸透し終わるまで")]
    [Min(0.01f)]
    [SerializeField]
    private float remainingAbsorbDuration = 5f;

    [Tooltip("紙の浸透の何割で魂を消すか")]
    [Range(0.05f, 0.95f)]
    [SerializeField]
    private float soulAbsorbEndProgress = 0.33f;    

    [Header("Paper Penetration")]

    [Tooltip("光が折り紙の中に入っていく時間")]
    [Min(0.01f)]
    [SerializeField]
    private float penetrateDuration = 0.8f;

    [Tooltip("紙へ浸透するときの光の大きさ最大値")]
    [Min(1f)]
    [SerializeField]
    private float penetrateScaleMultiplier = 1.25f;

    [Header("Paper Reflection Glow")]
    [SerializeField]
    private SpriteRenderer paperReflectionGlow;

    [SerializeField]
    private float reflectionFadeStart = 0.4f;

    [SerializeField]
    private float reflectionMaxAlpha = 0.25f;

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

    [Header("Pre Fold Lift")]

    [Tooltip("折れ線完成後も浮遊を見せる時間")]
    [Min(0f)]
    [SerializeField]
    private float afterFoldLineFloatDuration = 0.4f;

    [Tooltip("折り畳み直前に紙を浮き上がらせる距離")]
    [Min(0f)]
    [SerializeField]
    private float preFoldLiftDistance = 0.12f;

    [Tooltip("折り畳み直前の浮き上がり時間")]
    [Min(0.01f)]
    [SerializeField]
    private float preFoldLiftDuration = 0.45f;

    [Header("Pre Fold Flash")]

    [Tooltip("折り始める直前の発光時間")]
    [Min(0.01f)]
    [SerializeField]
    private float preFoldFlashDuration = 0.22f;

    [Tooltip("紙本体の最大発光量")]
    [Range(0f, 1f)]
    [SerializeField]
    private float preFoldFlashStrength = 0.7f;

    [Tooltip("発光時のReflection Glowの最大Alpha")]
    [Range(0f, 1f)]
    [SerializeField]
    private float preFoldReflectionAlpha = 0.3f;
    
    private void Awake()
    {
        
        BuildRuntimeCache();
        HideImmediately();
        if (paperReflectionGlow != null)
        {
            paperReflectionGlow.enabled = false;
        }
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

    private IEnumerator PenetrateIntoPaper (
        Transform target
    )
    {
        Vector3 startPosition = transform.position;
        Vector3 startScale = transform.localScale;

        Vector3 endScale = baseLocalScale * penetrateScaleMultiplier;

        float elapsedTime = 0;

        while (elapsedTime < penetrateDuration)
        {
            elapsedTime += GetDeltaTime();

            float t = 
                Mathf.Clamp01(
                    elapsedTime /
                    penetrateDuration
                );

            // 最初はゆっくり、徐々に紙に吸い込まれていく
            float eased =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    t
                );
            
            transform.position =
                Vector3.Lerp(
                    startPosition,
                    target.position,
                    eased
                );
            
            transform.localScale =
                Vector3.Lerp(
                    startScale,
                    endScale,
                    eased
                );
            yield return null;
        }

        transform.position = target.position;
        transform.localScale = endScale;
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

       // 折れ線を見せるためにカメラを少し寄せる
        if (summonCameraController != null)
        {
            yield return summonCameraController.ZoomIn();    
        }

        // 降りたら少し停止してフワフワ浮遊する
        yield return HoverBeforeContact(target);

        // 浸透開始
        yield return PlayPenetrationAndAbsorb(
            selectedPaper,
            target
        );
        // 浮上と同時に、折り畳み用カメラへ移動開始
        if (summonCameraController != null)
        {
            StartCoroutine(
                summonCameraController.MoveForFold()
            );
        }
        // 一瞬停止させる予定（仮でWaitForDurationを入れている）
        yield return WaitForDuration(0.5f);

        // 選択した紙に折れ線を走らせる
        if (foldLineProgressController != null)
        {
            yield return foldLineProgressController.PlayFoldLine(
                selectedPaper.TargetRenderer
            );
        }

        // 折れ線完成後も少し浮遊したまま見せる
        yield return WaitForDuration(
            afterFoldLineFloatDuration
        );

        // 今の浮遊位置から少し上へ
        yield return selectedPaper.PlayPreFoldLift(
            preFoldLiftDistance,
            preFoldLiftDuration
        );

        // 折り始める直前に一瞬発光
        yield return PlayPreFoldFlash(
            selectedPaper
        );

        // 変形後の見た目の中心を追従開始
        if (summonCameraController != null)
        {
            summonCameraController.StartFoldCenterTracking(
                selectedPaper.TargetRenderer
            );
        }

        // 折り畳み開始
        selectedPaper.PlayFoldAnimation();

        // STEP03付近からSTEP09完了までズーム
        if (summonCameraController != null)
        {
            StartCoroutine(
                summonCameraController.ZoomLateFold()
            );
        }

        yield return selectedPaper.WaitForFoldAnimation();

        // 折り終えたら、Shader Graph の折れ線を滑らかに消す
        if (foldLineProgressController != null)
        {
            yield return foldLineProgressController.FadeOutFoldLine(
                selectedPaper.TargetRenderer
            );
        }

        introCoroutine = null;
    }

    private IEnumerator PlayPenetrationAndAbsorb(
        PaperPullSelectable selectedPaper,
        Transform target
    )
    {
        float absorbProgress = 0f;
        bool absorbFinished = false;

        Vector3 startPosition =
            transform.position;

        Vector3 startScale =
            transform.localScale;

        // 紙に入る直前に少し膨らむ最大サイズ
        Vector3 peakScale =
            baseLocalScale *
            penetrateScaleMultiplier;

        // 完全に吸い込まれた時のサイズ
        Vector3 absorbedScale =
            baseLocalScale *
            0.08f;

        // Reflection Glow の現在の明るさを保持
        float reflectionStartAlpha = 0f;

        if (paperReflectionGlow != null)
        {
            reflectionStartAlpha =
                paperReflectionGlow.color.a;
        }

        // --------------------------
        // 紙の浸透を開始
        // --------------------------

        StartCoroutine(
            RunAbsorbAndWait(
                selectedPaper,
                soulAbsorbDuration,
                remainingAbsorbDuration,
                soulAbsorbEndProgress,
                progress =>
                {
                    absorbProgress = progress;
                },
                () =>
                {
                    absorbFinished = true;
                }
            )
        );

        // --------------------------
        // 紙の浸透が約1/3進むまでに
        // 魂を完全に吸収する
        // --------------------------

        float soulAbsorbEnd =
            soulAbsorbEndProgress;

        // 魂側の進行の60%地点から
        // 急速に縮み始める
        const float shrinkStart = 0.60f;

        while (
            absorbProgress < soulAbsorbEnd &&
            !absorbFinished
        )
        {
            // 紙の浸透 0 ～ 0.33 を
            // 魂側の 0 ～ 1 に変換
            float soulT =
                Mathf.InverseLerp(
                    0f,
                    soulAbsorbEnd,
                    absorbProgress
                );

            // --------------------------
            // 位置
            // 紙の中心へ吸い込まれる
            // --------------------------

            float moveT =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    soulT
                );

            transform.position =
                Vector3.Lerp(
                    startPosition,
                    target.position,
                    moveT
                );

            // --------------------------
            // サイズ
            //
            // 前半：
            // 少し膨らむ
            //
            // 後半：
            // 急速に縮んで吸い込まれる
            // --------------------------

            if (soulT < shrinkStart)
            {
                float growT =
                    Mathf.InverseLerp(
                        0f,
                        shrinkStart,
                        soulT
                    );

                growT =
                    Mathf.SmoothStep(
                        0f,
                        1f,
                        growT
                    );

                transform.localScale =
                    Vector3.Lerp(
                        startScale,
                        peakScale,
                        growT
                    );
            }
            else
            {
                float shrinkT =
                    Mathf.InverseLerp(
                        shrinkStart,
                        1f,
                        soulT
                    );

                shrinkT =
                    Mathf.SmoothStep(
                        0f,
                        1f,
                        shrinkT
                    );

                transform.localScale =
                    Vector3.Lerp(
                        peakScale,
                        absorbedScale,
                        shrinkT
                    );
            }

            // --------------------------
            // 吸収後半で光を消す
            // --------------------------

            float fadeT =
                Mathf.InverseLerp(
                    shrinkStart,
                    1f,
                    soulT
                );

            fadeT =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    fadeT
                );

            float visibility =
                1f - fadeT;

            SetRendererIntensity(
                lightCore,
                0.6f * visibility
            );

            SetRendererIntensity(
                lightGlow,
                0.25f * visibility
            );

            // --------------------------
            // Reflection Glow も
            // 魂が吸収される後半で消す
            // --------------------------

            if (paperReflectionGlow != null)
            {
                Color color =
                    paperReflectionGlow.color;

                color.a =
                    Mathf.Lerp(
                        reflectionStartAlpha,
                        0f,
                        fadeT
                    );

                paperReflectionGlow.color =
                    color;
            }

            yield return null;
        }

        // --------------------------
        // 魂の吸収完了
        // --------------------------

        transform.position =
            target.position;

        transform.localScale =
            absorbedScale;

        SetRendererIntensity(
            lightCore,
            0f
        );

        SetRendererIntensity(
            lightGlow,
            0f
        );

        if (lightCore != null)
        {
            lightCore.enabled = false;
        }

        if (lightGlow != null)
        {
            lightGlow.enabled = false;
        }

        // Reflection Glow も終了
        if (paperReflectionGlow != null)
        {
            Color color =
                paperReflectionGlow.color;

            color.a = 0f;

            paperReflectionGlow.color =
                color;

            paperReflectionGlow.enabled =
                false;
        }

        // 非表示になったので
        // 次回のために Scale は戻しておく
        transform.localScale =
            baseLocalScale;

        // --------------------------
        // 残りの浸透が終わるまで待つ
        // --------------------------

        while (!absorbFinished)
        {
            yield return null;
        }
    }

    private IEnumerator RunAbsorbAndWait(
        PaperPullSelectable selectedPaper,
        float firstPhaseDuration,
        float secondPhaseDuration,
        float firstPhaseEndProgress,
        System.Action<float> onProgress,
        System.Action onFinished
    )
    {
        yield return selectedPaper.PlayAbsorbLightOnly(
            onProgress,
            firstPhaseDuration,
            secondPhaseDuration,
            firstPhaseEndProgress
        );
        onFinished?.Invoke();
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

        if (paperReflectionGlow != null)
        {
            Vector3 cameraDirection =
                Camera.main != null
                    ? -Camera.main.transform.forward
                    : Vector3.back;

            paperReflectionGlow.transform.position =
                target.position +
                cameraDirection * 0.01f;

            Color color =
                paperReflectionGlow.color;

            color.a = 0f;

            paperReflectionGlow.color = color;
            paperReflectionGlow.enabled = true;
        }

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

            if (paperReflectionGlow != null)
            {
                Vector3 cameraDirection =
                    Camera.main != null
                        ? -Camera.main.transform.forward
                        : Vector3.back;
                paperReflectionGlow.transform.position =
                    target.position +
                    cameraDirection * 0.01f;
                float reflectionT =
                    Mathf.InverseLerp(
                        reflectionFadeStart,
                        1f,
                        normalizedTime
                    );
                reflectionT =
                    Mathf.SmoothStep(
                        0f,
                        1f,
                        reflectionT
                    );
                reflectionT *= reflectionT;

                Color color =
                    paperReflectionGlow.color;
                color.a =
                    reflectionMaxAlpha *
                    reflectionT;
                
                paperReflectionGlow.color = color;
            }

            
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
    private IEnumerator HoverBeforeContact(
        Transform target
    )
    {
        if (preContactHoldDuration <= 0f)
        {
            yield break;
        }
        Vector3 cameraDirection =
            Camera.main != null
                ? -Camera.main.transform.forward
                : Vector3.back;
        paperReflectionGlow.transform.position =
            target.position +
            cameraDirection * 0.01f;

        Vector3 reflectionBaseScale =
            paperReflectionGlow != null
                ? paperReflectionGlow.transform.localScale
                : Vector3.one;
        Vector3 centerPosition =
            transform.position;
        Vector3 hoverBaseScale =
            transform.localScale;

        float elapsedTime = 0f;

        while (elapsedTime < preContactHoldDuration)
        {
            elapsedTime += GetDeltaTime();
            float scaleWave =
                Mathf.Sin(
                    elapsedTime * 5f
                ) * 0.05f;

            transform.localScale =
                hoverBaseScale *
                (1f + scaleWave);

            float wave =
                Mathf.Sin(
                    elapsedTime * 4.5f
                );

            float sideWave =
                Mathf.Sin(
                    elapsedTime * 4.5f
                );
            if (paperReflectionGlow != null)
            {
                // 魂が下に来た時ほど、反射を少し強く・小さく見せる
                float proximity =
                    (1f - wave) * 0.5f;

                float alphaMultiplier =
                    Mathf.Lerp(
                        0.75f,
                        1f,
                        proximity
                    );
                Color color = paperReflectionGlow.color;
                color.a = reflectionMaxAlpha * alphaMultiplier;
                paperReflectionGlow.color = color;

                float reflectionScale =
                    Mathf.Lerp(
                        1.12f,
                        0.92f,
                        proximity
                    );


                paperReflectionGlow.transform.localScale =
                    reflectionBaseScale *
                    reflectionScale;
            }

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
                + upDirection * wave * 0.018f
                + sideDirection * sideWave * 0.006f;

            yield return null;
        }
        if (paperReflectionGlow != null)
        {
            paperReflectionGlow.transform.localScale =
                reflectionBaseScale;
        }    
        transform.position = centerPosition;
        transform.localScale = hoverBaseScale;
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
    private IEnumerator PlayPreFoldFlash(
        PaperPullSelectable selectedPaper
    )
    {
        if (selectedPaper == null)
        {
            yield break;
        }

        Transform target =
            selectedPaper.LightTarget;

        if (paperReflectionGlow != null)
        {
            paperReflectionGlow.enabled =
                true;

            Color color =
                paperReflectionGlow.color;

            color.a = 0f;

            paperReflectionGlow.color =
                color;
        }

        float elapsedTime = 0f;

        while (
            elapsedTime <
            preFoldFlashDuration
        )
        {
            elapsedTime +=
                GetDeltaTime();

            float t =
                Mathf.Clamp01(
                    elapsedTime /
                    preFoldFlashDuration
                );

            // 前半で光り、
            // 後半で元へ戻る
            float pulse;

            if (t < 0.5f)
            {
                pulse =
                    Mathf.SmoothStep(
                        0f,
                        1f,
                        t * 2f
                    );
            }
            else
            {
                pulse =
                    Mathf.SmoothStep(
                        1f,
                        0f,
                        (t - 0.5f) * 2f
                    );
            }

            selectedPaper.SetPreFoldFlash(
                preFoldFlashStrength *
                pulse
            );

            if (paperReflectionGlow != null)
            {
                // 紙はまだ浮遊中なので
                // Reflectionも紙を追従させる
                if (target != null)
                {
                    Vector3 cameraDirection =
                        Camera.main != null
                            ? -Camera.main.transform.forward
                            : Vector3.back;

                    paperReflectionGlow.transform.position =
                        target.position +
                        cameraDirection * 0.01f;
                }

                Color color =
                    paperReflectionGlow.color;

                color.a =
                    preFoldReflectionAlpha *
                    pulse;

                paperReflectionGlow.color =
                    color;
            }

            yield return null;
        }

        selectedPaper.ClearPreFoldFlash();

        if (paperReflectionGlow != null)
        {
            Color color =
                paperReflectionGlow.color;

            color.a = 0f;

            paperReflectionGlow.color =
                color;

            paperReflectionGlow.enabled =
                false;
        }
    }
}
