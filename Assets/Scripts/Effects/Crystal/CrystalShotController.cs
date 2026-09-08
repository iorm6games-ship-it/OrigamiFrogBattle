using System.Collections;
using UnityEngine;

public sealed class CrystalShotController : MonoBehaviour
{
    private static readonly int CrystalShotProgressId =
        Shader.PropertyToID("_CrystalShotProgress");
    
    [Header("Target")]
    [SerializeField]
    private Renderer targetRenderer;

    [Header("Timing")]
    [Min(0.01f)]
    [SerializeField]
    private float duration = 0.22f;

    [SerializeField]
    private AnimationCurve progressCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    private MaterialPropertyBlock propertyBlock;
    private Coroutine playCoroutine;

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
        SetProgress(0f);
    }

    public void Play()
    {
        if (targetRenderer == null)
        {
            Debug.LogWarning(
                $"{nameof(CrystalShotController)}: Target Renderer is not assigned.",
                this
            );
            return;
        }

        if (playCoroutine != null)
        {
            StopCoroutine(playCoroutine);
        }
        playCoroutine = StartCoroutine(PlayRoutine());
    }

    private void ResetEffect()
    {
        if (playCoroutine != null)
        {
            StopCoroutine(playCoroutine);
            playCoroutine = null;
        }

        SetProgress(0f);
    }

#if UNITY_EDITOR
    [ContextMenu("Test Crystal Shot")]
    private void TestCrystalShot()
    {
        Play();
    }

    [ContextMenu("Reset Crystal Shot")]
    private void TestResetCrystalShot()
    {
        ResetEffect();
    }
    [ContextMenu("Test Progress 0.5")]
    private void TestProgressHalf()
    {
        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        SetProgress(0.5f);

        // 書き込んだ直後のMPBを読み直す
        var checkBlock = new MaterialPropertyBlock();
        targetRenderer.GetPropertyBlock(checkBlock);

        float actualProgress =
            checkBlock.GetFloat(CrystalShotProgressId);

        bool hasProperty =
            targetRenderer != null &&
            targetRenderer.sharedMaterial != null &&
            targetRenderer.sharedMaterial.HasProperty(CrystalShotProgressId);

        Debug.Log(
            $"Crystal Shot Test: " +
            $"renderer={targetRenderer.name}, " +
            $"hasProperty={hasProperty}, " +
            $"MPB Progress={actualProgress}",
            this);
    }
#endif

    private IEnumerator PlayRoutine()
    {
        float elapsed = 0f;

        SetProgress(0f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float normalizedTime =
                Mathf.Clamp01(elapsed / duration);

            float progress =
                progressCurve.Evaluate(normalizedTime);
            SetProgress(progress);

            yield return null;
        }
        SetProgress(1f);

        playCoroutine = null;
    }

    private void SetProgress(float value)
    {
        if (targetRenderer == null)
        {
            return;
        }

        targetRenderer.GetPropertyBlock(propertyBlock);

        propertyBlock.SetFloat(
            CrystalShotProgressId,
            Mathf.Clamp01(value)
        );

        targetRenderer.SetPropertyBlock(propertyBlock);
    }
    
}