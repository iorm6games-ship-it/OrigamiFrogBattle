using System.Collections;
using UnityEngine;

public sealed class FoldLineProgressController : MonoBehaviour
{
    private static readonly int ProgressId =
        Shader.PropertyToID("_Progress");

    private static readonly int FoldLineFadeId =
        Shader.PropertyToID("_FoldLineFade");

    [Header("Animation")]
    [Min(0.1f)]
    [SerializeField]
    private float duration = 0.9f;

    [Min(0.1f)]
    [SerializeField]
    private float fadeOutDuration = 0.65f;

    [Tooltip("折り畳み完了後に残す、紙の折り目の濃さ")]
    [Range(0f, 1f)]
    [SerializeField]
    private float completedCreaseVisibility = 0.22f;

    private Coroutine animationCoroutine;
    private Renderer currentRenderer;
    private MaterialPropertyBlock propertyBlock;

    private void Awake()
    {
        propertyBlock =
            new MaterialPropertyBlock();
    }

    public void ResetFoldLine(
        Renderer targetRenderer
    )
    {
        if (targetRenderer == null)
        {
            return;
        }

        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
            animationCoroutine = null;
        }

        SetProgress(
            targetRenderer,
            0f
        );

        SetFoldLineFade(
            targetRenderer,
            1f
        );
    }

    public IEnumerator PlayFoldLine(
        Renderer targetRenderer
    )
    {
        if (targetRenderer == null)
        {
            Debug.LogError(
                $"{nameof(FoldLineProgressController)}: " +
                "Target Renderer がありません",
                this
            );

            yield break;
        }

        if (!HasProgressProperty(targetRenderer))
        {
            Debug.LogError(
                $"{nameof(FoldLineProgressController)}: " +
                $"{targetRenderer.name} のShaderに " +
                "_Progress がありません",
                targetRenderer
            );

            yield break;
        }

        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
            animationCoroutine = null;
        }

        currentRenderer = targetRenderer;

        yield return AnimateProgress(
            targetRenderer
        );

        currentRenderer = null;
    }

    public IEnumerator FadeOutFoldLine(
        Renderer targetRenderer
    )
    {
        if (targetRenderer == null)
        {
            yield break;
        }

        if (!HasProperty(
                targetRenderer,
                FoldLineFadeId
            ))
        {
            Debug.LogError(
                $"{nameof(FoldLineProgressController)}: " +
                $"{targetRenderer.name} のShaderに " +
                "_FoldLineFade がありません",
                targetRenderer
            );

            yield break;
        }

        float elapsedTime = 0f;
        float safeDuration =
            Mathf.Max(
                0.01f,
                fadeOutDuration
            );

        SetFoldLineFade(
            targetRenderer,
            1f
        );

        while (elapsedTime < safeDuration)
        {
            elapsedTime += Time.deltaTime;

            float progress =
                Mathf.Clamp01(
                    elapsedTime / safeDuration
                );

            float easedProgress =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    progress
                );

            SetFoldLineFade(
                targetRenderer,
                Mathf.Lerp(
                    1f,
                    completedCreaseVisibility,
                    easedProgress
                )
            );

            yield return null;
        }

        SetFoldLineFade(
            targetRenderer,
            completedCreaseVisibility
        );
    }

    private IEnumerator AnimateProgress(
        Renderer targetRenderer
    )
    {
        SetProgress(
            targetRenderer,
            0f
        );

        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;

            float progress =
                Mathf.Clamp01(
                    time / duration
                );

            SetProgress(
                targetRenderer,
                progress
            );

            yield return null;
        }

        SetProgress(
            targetRenderer,
            1.25f
        );

        animationCoroutine = null;
    }

    private void SetProgress(
        Renderer targetRenderer,
        float progress
    )
    {
        SetFloat(
            targetRenderer,
            ProgressId,
            progress
        );
    }

    private void SetFoldLineFade(
        Renderer targetRenderer,
        float fade
    )
    {
        SetFloat(
            targetRenderer,
            FoldLineFadeId,
            fade
        );
    }

    private void SetFloat(
        Renderer targetRenderer,
        int propertyId,
        float value
    )
    {
        targetRenderer.GetPropertyBlock(
            propertyBlock
        );

        propertyBlock.SetFloat(
            propertyId,
            value
        );

        targetRenderer.SetPropertyBlock(
            propertyBlock
        );
    }

    private bool HasProgressProperty(
        Renderer targetRenderer
    )
    {
        return HasProperty(
            targetRenderer,
            ProgressId
        );
    }

    private bool HasProperty(
        Renderer targetRenderer,
        int propertyId
    )
    {
        Material material =
            targetRenderer.sharedMaterial;

        return
            material != null &&
            material.HasProperty(propertyId);
    }
}
