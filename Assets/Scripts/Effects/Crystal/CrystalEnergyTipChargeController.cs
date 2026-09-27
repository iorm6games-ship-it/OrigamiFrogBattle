using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
public sealed class CrystalEnergyTipChargeController : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private ParticleSystem chargeCore;

    [SerializeField]
    private ParticleSystem chargeGlow;

    [SerializeField]
    private ParticleSystem gather;

    [SerializeField]
    private ParticleSystem spark;

    [Header("Timing")]
    [Min(0.05f)]
    [SerializeField]
    private float chargeDuration = 0.32f;

    [Range(0f, 1f)]
    [SerializeField]
    private float coreAppearTime = 0.18f;

    [Range(0f, 1f)]
    [SerializeField]
    private float sparkTime = 0.82f;

    [Header("Core")]
    [SerializeField]
    private float coreStartScale = 0.30f;

    [SerializeField]
    private float corePeakScale = 1.0f;

    [Header("Glow")]
    [SerializeField]
    private float glowStartScale = 0.45f;

    [SerializeField]
    private float glowPeakScale = 1.15f;

    [Header("Gather")]
    [Tooltip("チャージ開始時のGather領域")]
    [SerializeField]
    private float gatherStartScale = 1.20f;

    [Tooltip("エネルギーが先端へ収束したときのGather領域")]
    [SerializeField]
    private float gatherEndScale = 0.45f;

    [Header("Spark")]
    [Min(1)]
    [SerializeField]
    private int sparkCount = 2;

    [Min(0f)]
    [SerializeField]
    private float sparkInterval = 0.018f;

    [SerializeField]
    private CrystalEnergyGatherController gatherController;

    private Vector3 coreBaseScale;
    private Vector3 glowBaseScale;
    private Vector3 gatherBaseScale;

    private Coroutine playCoroutine;

    public bool IsPlaying { get; private set; }

    private void Awake()
    {
        CacheBaseScales();
        HideImmediate();
    }

    private void CacheBaseScales()
    {
        if (chargeCore != null)
        {
            coreBaseScale =
                chargeCore.transform.localScale;
        }
        if (chargeGlow != null)
        {
            glowBaseScale =
                chargeGlow.transform.localScale;
        }
        if (gather != null)
        {
            gatherBaseScale =
                gather.transform.localScale;
        }                
    }

    public void HideImmediate()
    {
        StopSystem(chargeCore);
        StopSystem(chargeGlow);
        StopSystem(gather);
        StopSystem(spark);

        if (chargeCore != null)
        {
            chargeCore.transform.localScale =
                coreBaseScale * coreStartScale;
        }
        if (chargeGlow != null)
        {
            chargeGlow.transform.localScale =
                glowBaseScale * glowStartScale;
        }
        if (gather != null)
        {
            gather.transform.localScale =
                gatherBaseScale * gatherStartScale;
        }

        IsPlaying = false;

    }

    public IEnumerator PlayCharge()
    {
        if (IsPlaying)
        {
            yield break;
        }

        yield return PlayChargeInternal();
    }

    private IEnumerator PlayChargeInternal()
    {
        IsPlaying = true;

        PrepareSystem(chargeCore);
        PrepareSystem(chargeGlow);
        PrepareSystem(gather);
        StopSystem(spark);

        if (chargeCore != null)
        {
            chargeCore.transform.localScale =
                coreBaseScale * coreStartScale;
        }
        if (chargeGlow != null)
        {
            chargeGlow.transform.localScale =
                glowBaseScale * glowStartScale;
        }
        if (gather != null)
        {
            gather.transform.localScale =
                gatherBaseScale * gatherStartScale;
                
            gather.Play(true);
        }

        float elapsed = 0f;
        bool coreStarted = false;
        bool sparkStarted = false;

        while (elapsed < chargeDuration)
        {
            elapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    chargeDuration);
            
            float eased =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    t);
            
            //
            // Gather:
            // 周囲に広がっていたエネルギーが
            // 先端へ圧縮されていく
            //
            if (gather != null)
            {
                gather.transform.localScale =
                    gatherBaseScale *
                    Mathf.Lerp(
                        gatherStartScale,
                        gatherEndScale,
                        eased);
            }

            //
            // Core / Glow:
            // 少し遅れて核が現れ、
            // 集束に合わせて強くなる
            // 
            if (!coreStarted && 
                t >= coreAppearTime)
            {
                coreStarted = true;

                chargeCore?.Play(true);
                chargeGlow?.Play(true);
            }

            if (coreStarted)
            {
                float coreT =
                    Mathf.InverseLerp(
                        coreAppearTime,
                        1f,
                        t);
                coreT =
                    Mathf.SmoothStep(
                        0f,
                        1f,
                        coreT);
                
                if (chargeCore != null)
                {
                    chargeCore.transform.localScale =
                        coreBaseScale *
                        Mathf.Lerp(
                            coreStartScale,
                            corePeakScale,
                            coreT);
                }

                if (chargeGlow != null)
                {
                    chargeGlow.transform.localScale =
                        glowBaseScale *
                        Mathf.Lerp(
                            glowStartScale,
                            glowPeakScale,
                            coreT);
                }
            }

            //
            // Spark:
            // Beam発射直前だけ
            //
            if (!sparkStarted &&
                t >= sparkTime)
            {
                sparkStarted = true;

                StartCoroutine(
                    PlaySparkSequence());
            }

            yield return null;
        }
        IsPlaying = false;
        
    }

    private IEnumerator PlaySparkSequence()
    {
        if (spark == null)
        {
            yield break;
        }

        for (int i = 0;
            i < sparkCount;
            i++)
        {
            //
            // ParticleSystem の Burstではなく
            // Controllerから直接Emitする
            //
            // これで複数Particleの偶発的な重なりによる
            // 巨大な白飛びを防ぐ
            //
            spark.Emit(1);

            if (i < sparkCount - 1 &&
                sparkInterval > 0f)
            {
                yield return 
                    new WaitForSeconds(
                        sparkInterval);
            }

        }
    }

    private static void PrepareSystem(
        ParticleSystem system)
    {
        if (system == null)
        {
            return;
        }
        system.Stop(
            true,
            ParticleSystemStopBehavior
                .StopEmittingAndClear);
    }

    private static void StopSystem(
        ParticleSystem system
    )
    {
        if (system == null)
        {
            return;
        }

        system.Stop(
            true,
            ParticleSystemStopBehavior
                .StopEmittingAndClear);
    }

}