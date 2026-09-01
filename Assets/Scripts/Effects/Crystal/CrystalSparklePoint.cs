using System.Collections;
using UnityEngine;

public sealed class CrystalSparklePoint : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private Transform visual;

    [Header("Position")]
    [SerializeField]
    private Vector3 randomRange = new Vector3(0.15f, 0.15f, 0.05f);

    [Header("Timing")]
    [SerializeField]
    private float minWait = 0.3f;

    [SerializeField]
    private float maxWait = 1.2f;

    [SerializeField]
    private float fadeInDuration = 0.08f;

    [SerializeField]
    private float holdDuration = 0.05f;

    [SerializeField]
    private float fadeOutDuration = 0.2f;

    private Vector3 basePosition;
    private Vector3 baseScale;

    private void Awake()
    {
        basePosition = transform.localPosition;

        if (visual != null)
        {
            baseScale = visual.localScale;
            visual.localScale = Vector3.zero;
        }
    }

    private void OnEnable()
    {
        StartCoroutine(SparkleLoop());
    }

    private IEnumerator SparkleLoop()
    {
        while (true)
        {
            float wait = UnityEngine.Random.Range(minWait, maxWait);
            yield return new WaitForSeconds(wait);

            MoveToRandomPosition();

            yield return ScaleVisual(Vector3.zero, baseScale, fadeInDuration);
            yield return new WaitForSeconds(holdDuration);
            yield return ScaleVisual(baseScale, Vector3.zero, fadeOutDuration);
        }
    }

    private void MoveToRandomPosition()
    {
        transform.localPosition =
            basePosition +
            new Vector3(
                UnityEngine.Random.Range(-randomRange.x, randomRange.x),
                UnityEngine.Random.Range(-randomRange.y, randomRange.y),
                UnityEngine.Random.Range(-randomRange.z, randomRange.z)
            );
    }

    private IEnumerator ScaleVisual(
        Vector3 from,
        Vector3 to,
        float duration
    )
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            visual.localScale = Vector3.Lerp(from, to, t);

            yield return null;
        }

        visual.localScale = to;
    }

}