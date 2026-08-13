using System.Collections;
using UnityEngine;

public sealed class FoldLineProgressController : MonoBehaviour
{
    private static readonly int ProgressId =
        Shader.PropertyToID("_Progress");

    [Header("Animation")]
    [Min(0.1f)]
    [SerializeField]
    private float duration = 0.9f;

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
            1f
        );

        animationCoroutine = null;
    }

    private void SetProgress(
        Renderer targetRenderer,
        float progress
    )
    {
        targetRenderer.GetPropertyBlock(
            propertyBlock
        );

        propertyBlock.SetFloat(
            ProgressId,
            progress
        );

        targetRenderer.SetPropertyBlock(
            propertyBlock
        );
    }

    private bool HasProgressProperty(
        Renderer targetRenderer
    )
    {
        Material material =
            targetRenderer.sharedMaterial;

        return
            material != null &&
            material.HasProperty(ProgressId);
    }
}