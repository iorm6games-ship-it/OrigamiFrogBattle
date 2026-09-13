using UnityEngine;

public sealed class CrystalCondenseGlowController : MonoBehaviour
{
    private static readonly int GlowAlphaId =
        Shader.PropertyToID("_GlowAlpha");

    private static readonly int SoftnessId =
        Shader.PropertyToID("_Softness");

    private static readonly int CoreStrengthId =
        Shader.PropertyToID("_CoreStrength");

    [Header("Reference")]
    [SerializeField]
    private Renderer targetRenderer;

    [Header("Intensity")]
    [SerializeField]
    private float maxGlowAlpha = 0.75f;

    [Header("Shape")]
    [SerializeField]
    private float startSoftness = 0.55f;

    [SerializeField]
    private float endSoftness = 0.26f;

    [SerializeField]
    private float startCoreStrength = 1.4f;

    [SerializeField]
    private float endCoreStrength = 3.2f;

    private MaterialPropertyBlock propertyBlock;

    private void Awake()
    {
        propertyBlock =
            new MaterialPropertyBlock();

        SetProgress(0f);
    }

    public void SetProgress(float progress)
    {
        if (targetRenderer == null)
        {
            return;
        }

        float t =
            Mathf.Clamp01(progress);

        // 前半は淡く、後半ほど強くする
        float intensityT =
            Mathf.SmoothStep(
                0f,
                1f,
                t);

        intensityT *= intensityT;

        float alpha =
            maxGlowAlpha *
            intensityT;

        // 凝縮するほど中心へ締める
        float softness =
            Mathf.Lerp(
                startSoftness,
                endSoftness,
                intensityT);

        float coreStrength =
            Mathf.Lerp(
                startCoreStrength,
                endCoreStrength,
                intensityT);

        targetRenderer.GetPropertyBlock(
            propertyBlock);

        propertyBlock.SetFloat(
            GlowAlphaId,
            alpha);

        propertyBlock.SetFloat(
            SoftnessId,
            softness);

        propertyBlock.SetFloat(
            CoreStrengthId,
            coreStrength);

        targetRenderer.SetPropertyBlock(
            propertyBlock);
    }

    public void HideImmediate()
    {
        SetProgress(0f);
    }
}