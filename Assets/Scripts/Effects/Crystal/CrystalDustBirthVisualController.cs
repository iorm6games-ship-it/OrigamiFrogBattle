using UnityEngine;

public sealed class CrystalDustBirthVisualController : MonoBehaviour
{
    private static readonly int AlphaId =
        Shader.PropertyToID("_Alpha");
    private static readonly int EmissionStrengthId =
        Shader.PropertyToID("_EmissionStrength");
    private static readonly int GlowAlphaId =
        Shader.PropertyToID("_GlowAlpha");
    
    [Header("References")]
    [SerializeField]
    private Renderer crystalRenderer;

    [SerializeField]
    private Renderer glowRenderer;

    [SerializeField]
    private Transform glowTransform;

    [Header("Crystal Reveal")]
    [Range(0f, 1f)]
    [SerializeField]
    private float revealStart = 0.18f;

    [Range(0f, 1f)]
    [SerializeField]
    private float revealEnd = 0.68f;

    [Header("Emission Pulse")]
    [SerializeField]
    private float birthEmissionPeak = 0.12f;

    [Range(0f, 1f)]
    [SerializeField]
    private float emissionPeakTime = 0.72f;

    [Header("Glow")]
    [SerializeField]
    private float glowStartAlpha = 0.06f;

    [SerializeField]
    private float glowPeakAlpha = 0.22f;

    [SerializeField]
    private float glowStartScale = 0.45f;

    [SerializeField]
    private float glowPeakScale = 1.15f;

    [Header("Outer Sparkles")]
    [SerializeField]
    private GameObject[] outerSparkles;

    [Range(0f, 1f)]
    [SerializeField]
    private float outerSparkleStart = 0.52f;

    [Range(0f, 0.3f)]
    [SerializeField]
    private float outerSparkleInterval = 0.08f;

    private MaterialPropertyBlock crystalBlock;
    private MaterialPropertyBlock glowBlock;
    private float baseAlpha;
    private float baseEmission;
    private float baseGlowAlpha;

    private Vector3 baseGlowScale;

    private void Awake()
    {
        crystalBlock = new MaterialPropertyBlock();
        glowBlock = new MaterialPropertyBlock();

        if (crystalRenderer != null &&
            crystalRenderer.sharedMaterial != null)
        {
            Material material = crystalRenderer.sharedMaterial;

            if (material.HasProperty(AlphaId))
            {
                baseAlpha = material.GetFloat(AlphaId);
            }

            if (material.HasProperty(EmissionStrengthId))
            {
                baseEmission = material.GetFloat(EmissionStrengthId);
            }
        }

        if (glowRenderer != null &&
            glowRenderer.sharedMaterial != null &&
            glowRenderer.sharedMaterial.HasProperty(GlowAlphaId))
        {
            baseGlowAlpha =
                glowRenderer.sharedMaterial.GetFloat(GlowAlphaId);
        }

        if (glowTransform != null)
        {
            baseGlowScale = glowTransform.localScale;
        }
    }

    public void BeginBirth()
    {
        SetCrystalVisual(0f, 0f);
        SetGlowVisual(
            glowStartAlpha,
            glowStartScale);
        SetOuterSparklesActive(false);
    }

    public void UpdateBirthVisual(float t)
    {
        t = Mathf.Clamp01(t);

        // ---
        // Crystal
        // ---
        float revealT =
            Mathf.InverseLerp(
                revealStart,
                revealEnd,
                t);
        revealT =
            Mathf.SmoothStep(
                0f,
                1f,
                revealT);
        float alpha =
            Mathf.Lerp(
                0f,
                baseAlpha,
                revealT);
        // 0 → Peak → 通常値
        float emissionPulse;

        if (t < emissionPeakTime)
        {
            emissionPulse =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    t / emissionPeakTime);
        }
        else
        {
            emissionPulse =
                1f -
                Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(
                        emissionPeakTime,
                        1f,
                        t));
        }
        float emission =
            baseEmission * revealT +
            birthEmissionPeak * emissionPulse;
        SetCrystalVisual(alpha, emission);

        // ---
        // Glow
        // ---
        float glowRise =
            Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(
                    0f,
                    emissionPeakTime,
                    t));
        float glowSettle =
            Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(
                    emissionPeakTime,
                    1f,
                    t));
        float glowAlpha =
            Mathf.Lerp(
                glowStartAlpha,
                glowPeakAlpha,
                glowRise);
        glowAlpha =
            Mathf.Lerp(
                glowAlpha,
                baseGlowAlpha,
                glowSettle);
        float glowScale =
            Mathf.Lerp(
                glowStartScale,
                glowPeakScale,
                glowRise);
        glowScale =
            Mathf.Lerp(
                glowScale,
                1f,
                glowSettle);
        SetGlowVisual(glowAlpha, glowScale);
        UpdateOuterSparkles(t);
    }

    public void CompleteBirth()
    {
        SetCrystalVisual(baseAlpha, baseEmission);
        SetGlowVisual(baseGlowAlpha, 1f);

        SetOuterSparklesActive(true);
    }

    private void SetCrystalVisual(
        float alpha,
        float emission
    )
    {
        if (crystalRenderer == null)
        {
            return;
        }
        crystalRenderer.GetPropertyBlock(crystalBlock);
        crystalBlock.SetFloat(AlphaId, alpha);

        crystalBlock.SetFloat(EmissionStrengthId, emission);

        crystalRenderer.SetPropertyBlock(crystalBlock);
    }

    private void SetGlowVisual(
        float alpha,
        float scale
    )
    {
        if (glowRenderer != null)
        {
            glowRenderer.GetPropertyBlock(glowBlock);
            glowBlock.SetFloat(GlowAlphaId, alpha);

            glowRenderer.SetPropertyBlock(glowBlock);
        }

        if (glowTransform != null)
        {
            glowTransform.localScale = baseGlowScale * scale;
        }
    }
    private void SetOuterSparklesActive(bool active)
    {
        if (outerSparkles == null)
        {
            return;
        }

        foreach (GameObject sparkle in outerSparkles)
        {
            if (sparkle != null)
            {
                sparkle.SetActive(active);
            }
        }
    }
    private void UpdateOuterSparkles(float t)
    {
        if (outerSparkles == null)
        {
            return;
        }
        
        for (int i = 0; i < outerSparkles.Length; i++)
        {
            GameObject sparkle = outerSparkles[i];

            if (sparkle == null)
            {
                continue;
            }
            float startTime =
                outerSparkleStart +
                i * outerSparkleInterval;
            if (!sparkle.activeSelf && t >= startTime)
            {
                sparkle.SetActive(true);
            }
        }
    }
}