using System;
using System.Collections;
using UnityEngine;

public sealed class FoldLineProgressController : MonoBehaviour
{
    private static readonly int ProgressId =
        Shader.PropertyToID("_Progress");

    [Header("References")]
    [SerializeField]
    private Renderer targetRenderer;
    [Header("Animation")]
    [Min(0.1f)]
    [SerializeField]
    private float duration = 0.9f;

    [SerializeField]
    private bool playOnStart = false;
    
    private Material runtimeMaterial;
    private Coroutine animationCoroutine;

    private void Awake()
    {
        
        if (targetRenderer == null)
        {
            Debug.LogError(
                $"{nameof(FoldLineProgressController)}: " +
                "Target Renderer が設定されてません", 
                this
            );
            enabled = false;
            return;
        }
        // このRenderer専用のマテリアルインスタンスを取得
        runtimeMaterial = targetRenderer.material;

        if (!runtimeMaterial.HasProperty(ProgressId))
        {
            Debug.LogError(
                $"Shaderに _Progress プロパティがありません", 
                this
            );
            enabled = false;
            return;
        }

        ResetFoldLine();
    }
    
    // テスト用
    private void Start()
    {
        if (playOnStart)
        {
            PlayFoldLine();
        }
    }

    public void PlayFoldLine()
    {
        if (!enabled || runtimeMaterial == null)
        {
            return;
        }
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
        }
        runtimeMaterial.SetFloat(
            ProgressId,
            0f            
        );
        
        animationCoroutine = StartCoroutine(AnimateProgress());
    }
    public void ResetFoldLine()
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
            animationCoroutine = null;
        }

        runtimeMaterial.SetFloat(ProgressId, 0f);
    }
    private IEnumerator AnimateProgress()
    {
        runtimeMaterial.SetFloat(ProgressId, 0f);
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            float progress = Mathf.Clamp01(time / duration);
            runtimeMaterial.SetFloat(ProgressId, progress);
            yield return null;
        }

        runtimeMaterial.SetFloat(ProgressId, 1f);
        animationCoroutine = null;
    }

    private void OnDestroy()
    {
        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
        }
    }

}