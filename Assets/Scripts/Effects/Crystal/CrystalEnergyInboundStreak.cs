using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CrystalEnergyInboundStreakController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LineRenderer core;
    [SerializeField] private LineRenderer glow;

    [Header("Path")]
    [SerializeField] private Vector3 startLocalPosition =
        new Vector3(0.22f, 0.08f, 0f);

    [SerializeField] private Vector3 curveOffset =
        new Vector3(0f, 0.05f, 0.025f);

    [Header("Motion")]
    [Min(0.05f)]
    [SerializeField] private float duration = 0.18f;

    [Range(0.05f, 0.6f)]
    [SerializeField] private float streakLength = 0.28f;

    [Min(3)]
    [SerializeField] private int segmentCount = 7;

    [Header("Timing")]
    [Range(1f, 6f)]
    [SerializeField] private float accelerationPower = 2.6f;

    [Header("Shape")]
    [Min(0f)]
    [SerializeField] private float waveAmplitude = 0.006f;

    [Min(0.1f)]
    [SerializeField] private float waveFrequency = 4.5f;

    [Min(0f)]
    [SerializeField] private float wavePhaseTravel = 2.0f;
    
    public bool IsPlaying { get; private set; }

    [Header("Absorption")]
    [Min(0.01f)]
    [SerializeField] private float absorptionDuration = 0.055f;

    [Range(1f, 6f)]
    [SerializeField] private float absorptionPower = 2.5f;

    private Vector3[] positions;

    private void Awake()
    {
        PrepareRenderer(core);
        PrepareRenderer(glow);

        positions = new Vector3[Mathf.Max(3, segmentCount)];

        SetVisible(false);
    }

    public IEnumerator Play()
    {
        if (IsPlaying)
            yield break;

        IsPlaying = true;

        SetVisible(true);

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float normalized =
                Mathf.Clamp01(elapsed / duration);

            // 終盤ほど速くTipへ吸い込まれる
            float headT =
                1f -
                Mathf.Pow(
                    1f - normalized,
                    accelerationPower);

            UpdateStreak(headT);

            yield return null;
        }

        yield return PlayAbsorption();

        SetVisible(false);

        IsPlaying = false;
    }

    private IEnumerator PlayAbsorption()
    {
        float elapsed = 0f;
        while (elapsed < absorptionDuration)
        {
            elapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed / absorptionDuration);
            
            // 最初は形を少し残し
            // 最後に急速にTipへ吸い込ませる
            float absorbT =
                Mathf.Pow(
                    t,
                    absorptionPower);
            
            UpdateAbsorbingStreak(absorbT);

            yield return null;
        }

        UpdateAbsorbingStreak(1f);
    }

    private void UpdateAbsorbingStreak(float absorbT)
    {
        int count = positions.Length;

        float currentLength =
            Mathf.Lerp(
                streakLength,
                0f,
                absorbT);
        
        for (int i = 0; i < count; i++)
        {
            float point01 =
                count <= 1
                ? 1f
                : i / (float)(count -1);

            // HeadはTipに固定
            // Tailだけが急速に追いついてくる。
            float pathT =
                1f -
                currentLength *
                (1f - point01);

            Vector3 basePos =
                EvaluateBezier(pathT);

            Vector3 tangent =
                EvaluateTangent(pathT);

            Vector3 side =
                Vector3.Cross(
                    tangent.normalized,
                    Vector3.forward);
            if (side.sqrMagnitude < 0.0001f)
                side = Vector3.up;
            
            side.Normalize();

            float envelope =
                Mathf.Sin(pathT * Mathf.PI);
            
            // 吸収中は揺らぎも収束させる
            float wave =
                Mathf.Sin(
                    pathT * Mathf.PI * waveFrequency +
                    Mathf.PI * wavePhaseTravel)
                * waveAmplitude
                * envelope
                * (1f - absorbT);
            
            positions[i] =
                basePos +
                side * wave;
        }
        core.SetPositions(positions);
        glow.SetPositions(positions);
    }
    
    private void UpdateStreak(float headT)
    {
        int count = positions.Length;

        for (int i = 0; i < count; i++)
        {
            float point01 =
                count <= 1
                    ? 1f
                    : i / (float)(count - 1);

            // i=0 がTail、最後がHead
            float pathT =
                headT -
                streakLength *
                (1f - point01);

            pathT = Mathf.Clamp01(pathT);

            Vector3 basePos =
                EvaluateBezier(pathT);
            Vector3 tangent =
                EvaluateTangent(pathT);

            Vector3 side =
                Vector3.Cross(
                    tangent.normalized,
                    Vector3.forward);
            if (side.sqrMagnitude < 0.0001f)
                side = Vector3.up;
            
            side.Normalize();

            float envelope =
                Mathf.Sin(pathT * Mathf.PI);
            
            float wave =
                Mathf.Sin(
                    pathT * Mathf.PI * waveFrequency +
                    headT * Mathf.PI * wavePhaseTravel)
                * waveAmplitude
                * envelope;
            
            positions[i] =
                basePos +
                side * wave;
            
        }

        core.SetPositions(positions);
        glow.SetPositions(positions);
    }

    private Vector3 EvaluateTangent(float t)
    {
        Vector3 start = startLocalPosition;
        Vector3 end = Vector3.zero;

        Vector3 midpoint =
            Vector3.Lerp(start, end, 0.5f);
        
        Vector3 control =
            midpoint + curveOffset;
        
        return
            2f * (1f - t) * (control - start) +
            2f * t * (end - control);
    }

    private Vector3 EvaluateBezier(float t)
    {
        Vector3 start = startLocalPosition;
        Vector3 end = Vector3.zero;

        Vector3 midpoint =
            Vector3.Lerp(start, end, 0.5f);

        Vector3 control =
            midpoint + curveOffset;

        float oneMinusT = 1f - t;

        return
            oneMinusT * oneMinusT * start +
            2f * oneMinusT * t * control +
            t * t * end;
    }

    private void PrepareRenderer(LineRenderer lr)
    {
        if (lr == null)
            return;

        lr.useWorldSpace = false;
        lr.positionCount = Mathf.Max(3, segmentCount);
    }

    private void SetVisible(bool visible)
    {
        if (core != null)
            core.enabled = visible;

        if (glow != null)
            glow.enabled = visible;
    }

    [ContextMenu("Test Play")]
    private void TestPlay()
    {
        if (!Application.isPlaying)
            return;

        StartCoroutine(Play());
    }
}