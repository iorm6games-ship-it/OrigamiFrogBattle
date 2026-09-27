using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(ParticleSystem))]
public sealed class CrystalEnergyGatherController : MonoBehaviour
{
    [Header("Gather")]
    [Min(1)]
    [SerializeField] private int particleCount = 14;

    [Min(0.05f)]
    [SerializeField] private float duration = 0.45f;

    [Min(0.001f)]
    [SerializeField] private float outerRadius = 0.20f;

    [Min(0f)]
    [SerializeField] private float innerRadius = 0.003f;

    [Header("Motion")]
    [SerializeField] private float spiralAmount = 0.035f;

    [SerializeField] private float spiralTurns = 0.45f;

    [SerializeField] private float verticalSpread = 0.08f;

    [Range(0f, 0.5f)]
    [SerializeField] private float stagger = 0.22f;

    [Header("Particle")]
    [SerializeField] private float particleLifetime = 1.0f;

    [SerializeField] private float startSize = 0.06f;

    private ParticleSystem ps;
    private ParticleSystem.Particle[] particles;

    private Vector3[] startPositions;
    private float[] startAngles;
    private float[] delays;

    public bool IsPlaying { get; private set; }

    private void Awake()
    {
        ps = GetComponent<ParticleSystem>();

        int capacity = Mathf.Max(32, particleCount);

        particles = new ParticleSystem.Particle[capacity];
        startPositions = new Vector3[capacity];
        startAngles = new float[capacity];
        delays = new float[capacity];

        StopImmediate();
    }

    public void StopImmediate()
    {
        if (ps == null)
            return;

        ps.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear);

        IsPlaying = false;
    }

    [ContextMenu("Test Play Gather")]
    private void TestPlayGather()
    {
        if (!Application.isPlaying)
            return;

        StartCoroutine(PlayGather());
    }

    public IEnumerator PlayGather()
    {
        if (IsPlaying)
            yield break;

        IsPlaying = true;

        ps.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear);

        ps.Play(true);

        for (int i = 0; i < particleCount; i++)
        {
            Vector3 dir = Random.onUnitSphere;

            // 完全な球ではなく、少し横方向を広くして
            // カメラからも広がりが読めるようにする
            dir.x *= 1.15f;
            dir.y *= 0.85f;
            dir.z *= 0.75f;
            dir.Normalize();

            float radius =
                Random.Range(
                    outerRadius * 0.65f,
                    outerRadius);

            Vector3 startPosition =
                dir * radius;

            startPositions[i] = startPosition;

            startAngles[i] =
                Mathf.Atan2(
                    startPosition.z,
                    startPosition.x);

            delays[i] =
                Random.Range(
                    0f,
                    stagger);

            var emitParams =
                new ParticleSystem.EmitParams
                {
                    position = startPosition,
                    velocity = Vector3.zero,
                    startLifetime = particleLifetime,
                    startSize = startSize
                };

            ps.Emit(emitParams, 1);
        }

        float elapsed = 0f;

        while (elapsed < duration + stagger)
        {
            elapsed += Time.deltaTime;

            int count =
                ps.GetParticles(particles);

            for (int i = 0;
                 i < count && i < particleCount;
                 i++)
            {
                float localTime =
                    Mathf.Max(
                        0f,
                        elapsed - delays[i]);

                float t =
                    Mathf.Clamp01(
                        localTime / duration);

                // 最初はゆっくり、終盤で吸引を強める
                float gatherT =
                    t * t * (3f - 2f * t);

                float angle =
                    startAngles[i] +
                    gatherT *
                    spiralTurns *
                    Mathf.PI *
                    2f;

                float startRadius =
                    new Vector2(
                        startPositions[i].x,
                        startPositions[i].z)
                    .magnitude;

                float radius =
                    Mathf.Lerp(
                        startRadius,
                        innerRadius,
                        gatherT);

                float height =
                    Mathf.Lerp(
                        startPositions[i].y,
                        0f,
                        gatherT);

                Vector3 radial =
                    new Vector3(
                        Mathf.Cos(angle) * radius,
                        height,
                        Mathf.Sin(angle) * radius);

                float arc =
                    Mathf.Sin(
                        gatherT * Mathf.PI)
                    * spiralAmount;

                Vector3 start = startPositions[i];

                Vector3 towardCenter =
                    Vector3.Lerp(
                        start,
                        Vector3.zero,
                        gatherT);
                Vector3 tangent =
                    Vector3.Cross(
                        start.normalized,
                        Vector3.up);
                        
                if (tangent.sqrMagnitude < 0.001f)
                    tangent = Vector3.right;

                tangent.Normalize();

                particles[i].position =
                    radial +
                    tangent * arc;

                particles[i].startSize =
                    startSize *
                    Mathf.Lerp(
                        1f,
                        0.35f,
                        gatherT);
            }

            ps.SetParticles(
                particles,
                count);

            yield return null;
        }

        ps.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear);

        IsPlaying = false;
    }
}