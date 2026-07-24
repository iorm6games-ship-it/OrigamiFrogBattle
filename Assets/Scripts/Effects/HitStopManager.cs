
using System.Collections;
using UnityEngine;

public class HitStopManager: MonoBehaviour
{
    public static HitStopManager Instance {  get; private set; }

    [Header("Slow Motion")]
    [SerializeField] private float slowScale = 0.15f;
    [SerializeField] private float defaultSlowDuration = 0.08f;
    [SerializeField] private float recoverDuration = 0.18f;

    [Header("Safety")]
    [SerializeField] private float minTimeScale = 0.05f;
    
    private float baseFixedDeltaTime;
    private Coroutine hitStopCoroutine;

    private void Awake()
    {
        
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        baseFixedDeltaTime = Time.fixedDeltaTime;
    }

    public void HitStop(float duration)
    {
        float slowDuration = duration > 0f ? duration : defaultSlowDuration;
        if (hitStopCoroutine != null)
        {
            StopCoroutine(hitStopCoroutine);
        }
        hitStopCoroutine = StartCoroutine(HitStopRoutine(slowDuration));
    }

    private IEnumerator HitStopRoutine(float slowDuration)
    {
     
        float clampedSlowScale = Mathf.Max(slowScale, minTimeScale);
        setTimeScale(clampedSlowScale);

        yield return new WaitForSecondsRealtime(slowDuration);

        float elapsed = 0f;

        while (elapsed < recoverDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / recoverDuration);

            // Smoothstep easing
            t = t * t * (3f - 2f * t); 
            float currentScale = Mathf.Lerp(clampedSlowScale, 1f, t);
            setTimeScale(currentScale);
            yield return null;
        }

        setTimeScale(1f);

        hitStopCoroutine = null;
    }

    private void setTimeScale(float scale)
    {
        Time.timeScale = scale;
        Time.fixedDeltaTime = baseFixedDeltaTime * scale;
    }

    private void OnDisable()
    {
        // Ensure time scale is reset if the manager is disabled
        setTimeScale(1f);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            setTimeScale(1f);
            Instance = null;
        }
    }
}

