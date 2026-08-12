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

    [SerializeField]
    private float fadeOutDuration = 0.3f;

    [Header("References")]
    [SerializeField]
    private Renderer targetRenderer;

    [Header("Animation")]
    [SerializeField]
    private float absorbDuration = 0.45f;

    [SerializeField]
    private Vector2 absorbCenter =
        new Vector2(0.5f, 0.5f);

    [SerializeField]
    private float startProgress = 0f;

    [SerializeField]
    private float endProgress = 0.7f;

    private Material runtimeMaterial;
    private Coroutine playCoroutine;

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

    public IEnumerator PlayAbsorb()
    {

        if (!enabled || runtimeMaterial == null)
        {
            yield break;
        }

        if (playCoroutine != null)
        {
            StopCoroutine(playCoroutine);
        }

        yield return PlayAbsorbRoutine();
    }

    public void ResetEffect()
    {
        if (playCoroutine != null)
        {
            StopCoroutine(playCoroutine);
            playCoroutine = null;
        }

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
            0f
        );

        runtimeMaterial.SetFloat(
            AbsorbFadeId,
            0f
        );
    }

    private IEnumerator PlayAbsorbRoutine()
    {
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

        while (time < absorbDuration)
        {
            time += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    time / absorbDuration
                );

            float eased =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    t
                );

            float progress =
                Mathf.Lerp(
                    startProgress,
                    endProgress,
                    eased
                );

            runtimeMaterial.SetFloat(
                AbsorbProgressId,
                progress
            );

            yield return null;
        }

        // 広がり切った状態で固定
        runtimeMaterial.SetFloat(
            AbsorbProgressId,
            endProgress
        );

        // -------------------------
        // 2. 範囲は固定したまま
        //    元の紙色へ戻す
        // -------------------------

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

        // 完全に元の色
        runtimeMaterial.SetFloat(
            AbsorbFadeId,
            0f
        );

        // Fade=0なので、このリセットは画面には見えない
        runtimeMaterial.SetFloat(
            AbsorbProgressId,
            startProgress
        );

        // 本当に全部終わってからnull
        playCoroutine = null;
    }
    private void OnDestroy()
    {
        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
        }
    }
}