using System;
using UnityEngine;

public sealed class CrystalDustHeroMotion : MonoBehaviour
{
    private enum Phase
    {
        Idle,
        Burst,
        Drift
    }

    [Header("Burst")]
    [SerializeField]
    private float burstDuration = 0.35f;

    [SerializeField]
    private float burstDistance = 0.8f;

    [Header("Drift")]
    [SerializeField]
    private float driftDuration = 1.2f;

    [SerializeField]
    private float driftAmplitude = 0.18f;

    [SerializeField]
    private float driftFrequency = 2.2f;

    [SerializeField]
    private float driftVerticalSpeed = 0.08f;

    [Header("Rotation")]
    [SerializeField]
    private Vector3 rotationSpeed = new Vector3(35f, 70f, 25f);

    private Phase phase = Phase.Idle;

    private Vector3 startPosition;
    private Vector3 burstDirection;
    private Vector3 driftStartPosition;

    private float elapsedTime;
    private float randomPhase;

    public void Play(Vector3 origin, Vector3 direction)
    {
        startPosition = origin;
        burstDirection = direction.normalized;

        transform.position = origin;

        elapsedTime = 0f;
        randomPhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);

        phase = Phase.Burst;

    }

    private void Update()
    {

        if (phase == Phase.Idle)
        {
            return;
        }

        transform.Rotate(
            rotationSpeed * Time.deltaTime,
            Space.Self
        );

        switch (phase)
        {
            case Phase.Burst:
                UpdateBurst();
                break;
            case Phase.Drift:
                UpdateDrift();
                break;
        }
    }

    private void UpdateBurst()
    {
        elapsedTime += Time.deltaTime;

        float t = Mathf.Clamp01(elapsedTime / burstDuration);

        // 強く飛び出して、終盤で減速
        float easedT = 1f - Mathf.Pow(1f - t, 3f);

        transform.position =
            startPosition +
            burstDirection * burstDistance * easedT;

        if (t >= 1f)
        {
            driftStartPosition = transform.position;
            elapsedTime = 0f;
            phase = Phase.Drift;
        }        
    }
    private void UpdateDrift()
    {
        elapsedTime += Time.deltaTime;

        float t = elapsedTime;

        float noiseX =
            Mathf.PerlinNoise(
                randomPhase,
                t * driftFrequency) * 2f - 1f;

        float noiseY =
            Mathf.PerlinNoise(
                randomPhase + 13.7f,
                t * driftFrequency * 0.57f) * 2f - 1f;

        float noiseZ =
            Mathf.PerlinNoise(
                randomPhase + 29.3f,
                t * driftFrequency * 1.31f) * 2f - 1f;

        Vector3 driftOffset = new Vector3(
            noiseX,
            noiseY * 0.55f,
            noiseZ * 0.75f
        );

        driftOffset *= driftAmplitude;

        // 一方向へ流し続けず、ゆっくり変化する流れにする
        float flowX =
            Mathf.PerlinNoise(
                randomPhase + 41.2f,
                t * 0.35f) * 2f - 1f;

        float flowY =
            Mathf.PerlinNoise(
                randomPhase + 57.8f,
                t * 0.28f) * 2f - 1f;

        float flowZ =
            Mathf.PerlinNoise(
                randomPhase + 73.4f,
                t * 0.31f) * 2f - 1f;

        Vector3 slowFlow = new Vector3(
            flowX * 0.05f,
            flowY * driftVerticalSpeed,
            flowZ * 0.04f
        );

        transform.position =
            driftStartPosition +
            driftOffset +
            slowFlow;

        if (elapsedTime >= driftDuration)
        {
            phase = Phase.Idle;
        }
    }
}