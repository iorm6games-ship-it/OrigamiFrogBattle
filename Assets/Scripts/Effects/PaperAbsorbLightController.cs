using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PaperAbsorbLightController : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private SpriteRenderer absorbGlow;

    [SerializeField]
    private Transform absorbPoint;

    [Header("Animation")]
    [Min(0.01f)]
    [SerializeField]
    private float fadeInDuration = 0.08f;

    [Min(0.01f)]
    [SerializeField]
    private float absorbDuration = 0.28f;

    [SerializeField]
    private Vector3 startScale = new Vector3(0.12f, 0.12f, 1f);

    [SerializeField]
    private Vector3 peakScale = new Vector3(0.24f, 0.24f, 1f);

    [SerializeField]
    private Vector3 endScale = new Vector3(0.36f, 0.36f, 1f);

    [Range(0f, 1f)]
    [SerializeField]
    private float peakAlpha = 0.85f;

    private Coroutine playCoroutine;

    private void Awake()
    {
        HideImmediately();
    }

    public IEnumerator PlayAbsorb()
    {
        if (absorbGlow == null)
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

        HideImmediately();
    }

    private IEnumerator PlayAbsorbRoutine()
    {
        if (absorbPoint != null)
        {
            absorbGlow.transform.position =
                absorbPoint.position;
        }

        absorbGlow.enabled = true;
        absorbGlow.transform.localScale = startScale;
        SetAlpha(0f);

        float time = 0f;

        // 表面に現れる
        while (time < fadeInDuration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / fadeInDuration);
            float eased = Mathf.SmoothStep(0f, 1f, t);

            absorbGlow.transform.localScale =
                Vector3.Lerp(startScale, peakScale, eased);

            SetAlpha(Mathf.Lerp(0f, peakAlpha, eased));

            yield return null;
        }

        time = 0f;

        // 吸い込まれるように薄れながら広がる
        while (time < absorbDuration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / absorbDuration);
            float eased = Mathf.SmoothStep(0f, 1f, t);

            absorbGlow.transform.localScale =
                Vector3.Lerp(peakScale, endScale, eased);

            SetAlpha(Mathf.Lerp(peakAlpha, 0f, eased));

            yield return null;
        }

        HideImmediately();
    }

    private void SetAlpha(float alpha)
    {
        if (absorbGlow == null)
        {
            return;
        }

        Color color = absorbGlow.color;
        color.a = alpha;
        absorbGlow.color = color;
    }

    private void HideImmediately()
    {
        if (absorbGlow == null)
        {
            return;
        }

        SetAlpha(0f);
        absorbGlow.enabled = false;
    }
}