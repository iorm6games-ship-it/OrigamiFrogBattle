using UnityEngine;

public sealed class CrystalVortexWindController : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private LineRenderer[] lines;

    [Header("Shape")]
    [Min(8)]
    [SerializeField]
    private int segmentCount = 24;

    [SerializeField]
    private float vortexLift = 0.10f;

    [SerializeField]
    private float baseRadius = 0.22f;

    [SerializeField]
    private float baseHeight = 0.45f;

    [SerializeField]
    private float arcDegrees = 150f;

    [Header("Rotation")]
    [SerializeField]
    private float angularSpeed = 250f;

    [Header("Organic Motion")]
    [SerializeField]
    private float radiusNoise = 0.012f;

    [SerializeField]
    private float heightNoise = 0.018f;

    [SerializeField]
    private float noiseSpeed = 1.4f;

    [Header("Wind Motion")]
    [SerializeField]
    private float angularWobbleDegrees = 8f;

    [SerializeField]
    private float angularWobbleFrequency = 1.6f;

    [SerializeField]
    private float angularWobbleSpeed = 2.0f;

    [Header("Visibility")]
    [Range(0f, 1f)]
    [SerializeField]
    private float seedIntensity = 0.08f;

    [SerializeField]
    private float baseWidth = 0.014f;

    [Range(0f, 1f)]
    [SerializeField]
    private float windOpacity = 0.12f;

    [SerializeField]
    private Color lineColor =
        new Color(
            0.55f,
            0.82f,
            1f,
            1f);

    [Header("Peak Visibility")]
    [Min(1f)]
    [SerializeField]
    private float peakOpacityMultiplier = 1.8f;

    [Min(1f)]
    [SerializeField]
    private float peakWidthMultiplier = 1.15f;

    [Header("Condense")]
    [Range(0f, 1f)]
    [SerializeField]
    private float condenseFadeStart = 0.55f;

    private CrystalFormationController formation;
    private Transform center;

    private float rotationAngle;

    //
    // LineRendererのGradientを毎フレームnewしないためのキャッシュ。
    //
    private Gradient[] runtimeGradients;
    private GradientColorKey[][] colorKeyBuffers;
    private GradientAlphaKey[][] alphaKeyBuffers;

    private readonly float[] phaseOffsets =
    {
        0f,
        123f,
        247f
    };

    private readonly float[] radiusMultipliers =
    {
        1f,
        0.78f,
        1.12f
    };

    private readonly float[] heightOffsets =
    {
        0.10f,
        0.38f,
        0.22f
    };

    private readonly float[] heightSpans =
    {
        0.32f,
        0.24f,
        0.34f
    };

    private readonly float[] speedMultipliers =
    {
        1f,
        0.86f,
        1.12f
    };

    private readonly float[] arcMultipliers =
    {
        1f,
        0.82f,
        1.10f
    };

    public void Play(
        CrystalFormationController owner,
        Transform centerPoint)
    {
        formation = owner;
        center = centerPoint;

        rotationAngle =
            Random.Range(
                0f,
                360f);

        SetupLines();
    }

    private void Update()
    {
        if (formation == null ||
            center == null)
        {
            return;
        }

        //
        // Formation全体のSpinに同期。
        //
        rotationAngle +=
            angularSpeed *
            formation.VortexSpinScale *
            Time.deltaTime;

        float intensity =
            CalculateIntensity();

        UpdateLines(
            intensity);
    }

    private void SetupLines()
    {
        if (lines == null)
        {
            return;
        }

        int count =
            Mathf.Min(
                lines.Length,
                phaseOffsets.Length);

        runtimeGradients =
            new Gradient[count];

        colorKeyBuffers =
            new GradientColorKey[count][];

        alphaKeyBuffers =
            new GradientAlphaKey[count][];

        for (int i = 0;
             i < count;
             i++)
        {
            LineRenderer line =
                lines[i];

            if (line == null)
            {
                continue;
            }

            line.useWorldSpace = true;
            line.loop = false;

            line.positionCount =
                segmentCount;

            //
            // 細い尻尾
            //      ↓
            // 徐々に存在感
            //      ↓
            // 後半で最も太く
            //      ↓
            // 先端で消える
            //
            line.widthCurve =
                new AnimationCurve(
                    new Keyframe(
                        0f,
                        0f),

                    new Keyframe(
                        0.18f,
                        0.35f),

                    new Keyframe(
                        0.55f,
                        0.85f),

                    new Keyframe(
                        0.78f,
                        1f),

                    new Keyframe(
                        1f,
                        0f));

            Gradient gradient =
                new Gradient();

            GradientColorKey[] colorKeys =
            {
                new GradientColorKey(
                    lineColor,
                    0f),

                new GradientColorKey(
                    lineColor,
                    1f)
            };

            GradientAlphaKey[] alphaKeys =
            {
                new GradientAlphaKey(
                    0f,
                    0f),

                new GradientAlphaKey(
                    0f,
                    0.18f),

                new GradientAlphaKey(
                    0f,
                    0.62f),

                new GradientAlphaKey(
                    0f,
                    0.86f),

                new GradientAlphaKey(
                    0f,
                    1f)
            };

            gradient.SetKeys(
                colorKeys,
                alphaKeys);

            runtimeGradients[i] =
                gradient;

            colorKeyBuffers[i] =
                colorKeys;

            alphaKeyBuffers[i] =
                alphaKeys;

            line.colorGradient =
                gradient;

            line.enabled = true;
        }
    }

    private void UpdateLines(
        float intensity)
    {
        if (lines == null)
        {
            return;
        }

        Vector3 axis =
            center.up.normalized;

        Vector3 basisX =
            center.right.normalized;

        Vector3 basisY =
            center.forward.normalized;

        float radiusScale =
            formation.VortexRadiusScale;

        float heightScale =
            formation.VortexHeightScale;

        int count =
            Mathf.Min(
                lines.Length,
                phaseOffsets.Length);

        for (int lineIndex = 0;
             lineIndex < count;
             lineIndex++)
        {
            LineRenderer line =
                lines[lineIndex];

            if (line == null)
            {
                continue;
            }

            //
            // 3本とも完全には同期させない。
            //
            float lineRotation =
                rotationAngle *
                speedMultipliers[lineIndex] +
                phaseOffsets[lineIndex];

            float baseLineRadius =
                baseRadius *
                radiusMultipliers[lineIndex];

            float lineArc =
                arcDegrees *
                arcMultipliers[lineIndex];

            for (int i = 0;
                 i < segmentCount;
                 i++)
            {
                float u =
                    segmentCount <= 1
                        ? 0f
                        : i /
                          (float)(
                              segmentCount - 1);

                //
                // 単純な円弧に見せず、
                // 中央付近だけ少しうねらせる。
                //
                float angularWobble =
                    Mathf.Sin(
                        Time.time *
                        angularWobbleSpeed +
                        u *
                        angularWobbleFrequency *
                        Mathf.PI *
                        2f +
                        lineIndex *
                        1.7f
                    ) *
                    angularWobbleDegrees *
                    Mathf.Sin(
                        Mathf.PI * u);

                float angle =
                    lineRotation +
                    lineArc *
                    (u - 0.5f) +
                    angularWobble;

                float angleRad =
                    angle *
                    Mathf.Deg2Rad;

                float noiseTime =
                    Time.time *
                    noiseSpeed;

                //
                // 個体的な半径揺らぎ。
                //
                float radialNoise =
                    Mathf.Sin(
                        noiseTime +
                        u * 5.1f +
                        lineIndex * 1.7f) *
                    radiusNoise;

                //
                // Noiseを含めた形全体へ
                // FormationのRadiusScaleを掛ける。
                //
                // これによりCondenseで完全に中心へ潰れる。
                //
                float radius =
                    Mathf.Max(
                        0f,
                        baseLineRadius +
                        radialNoise);

                radius *=
                    radiusScale;

                float height01 =
                    heightOffsets[lineIndex] +
                    heightSpans[lineIndex] *
                    u;

                float verticalNoise =
                    Mathf.Sin(
                        noiseTime * 0.83f +
                        u * 6.7f +
                        lineIndex * 2.4f) *
                    heightNoise;

                //
                // Lift / Height / Noise全体を
                // FormationのHeightScaleで制御する。
                //
                // Condense完了時は0へ完全収束。
                //
                float height =
                    (
                        vortexLift +
                        baseHeight *
                        height01 +
                        verticalNoise
                    ) *
                    heightScale;

                Vector3 radial =
                    basisX *
                    Mathf.Cos(angleRad) +
                    basisY *
                    Mathf.Sin(angleRad);

                Vector3 position =
                    center.position +
                    radial * radius +
                    axis * height;

                line.SetPosition(
                    i,
                    position);
            }

            ApplyVisual(
                line,
                intensity,
                lineIndex);
        }
    }

    private float CalculateIntensity()
    {
        if (formation == null)
        {
            return 0f;
        }

        //
        // RadiusやSpinから風の強度を
        // 推測しない。
        //
        // Formationが持つ演出エネルギーへ
        // 直接同期する。
        //
        float energy =
            Mathf.Clamp01(
                formation.SparkleEnergy);

        float energyT =
            Mathf.SmoothStep(
                0f,
                1f,
                energy);

        float intensity =
            Mathf.Lerp(
                seedIntensity,
                1f,
                energyT);

        //
        // Condense前半では風を維持。
        // 後半から凝縮光へエネルギーを渡して消える。
        //
        float condense =
            formation.CondenseProgress;

        if (condense > condenseFadeStart)
        {
            float fadeT =
                Mathf.InverseLerp(
                    condenseFadeStart,
                    1f,
                    condense);

            fadeT =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    fadeT);

            intensity *=
                1f - fadeT;
        }

        return Mathf.Clamp01(
            intensity);
    }

    private void ApplyVisual(
        LineRenderer line,
        float intensity,
        int lineIndex)
    {
        float variation =
            1f -
            lineIndex *
            0.10f;

        float finalIntensity =
            Mathf.Clamp01(
                intensity *
                variation);

        //
        // Formation Energyが高いほど、
        // Dustの密度に負けない程度に
        // Windも存在感を増す。
        //
        float energy =
            formation != null
                ? Mathf.Clamp01(
                    formation.SparkleEnergy)
                : 0f;

        float energyT =
            Mathf.SmoothStep(
                0f,
                1f,
                energy);

        float opacityBoost =
            Mathf.Lerp(
                1f,
                peakOpacityMultiplier,
                energyT);

        float alpha =
            finalIntensity *
            windOpacity *
            opacityBoost;

        alpha =
            Mathf.Clamp01(
                alpha);

        //
        // Peak時でも太いリングにはせず、
        // わずかに密度感を上げるだけ。
        //
        float widthBoost =
            Mathf.Lerp(
                1f,
                peakWidthMultiplier,
                energyT);

        line.widthMultiplier =
            baseWidth *
            Mathf.Lerp(
                0.55f,
                1f,
                finalIntensity) *
            widthBoost;

        //
        // 一様な線ではなく、
        // 尾 → 明るい流れ → 先端Fade
        // という方向性を作る。
        //
        if (runtimeGradients == null ||
            lineIndex >= runtimeGradients.Length ||
            runtimeGradients[lineIndex] == null)
        {
            return;
        }

        GradientAlphaKey[] alphaKeys =
            alphaKeyBuffers[lineIndex];

        alphaKeys[0] =
            new GradientAlphaKey(
                0f,
                0f);

        alphaKeys[1] =
            new GradientAlphaKey(
                alpha * 0.20f,
                0.18f);

        alphaKeys[2] =
            new GradientAlphaKey(
                alpha,
                0.62f);

        alphaKeys[3] =
            new GradientAlphaKey(
                alpha * 0.45f,
                0.86f);

        alphaKeys[4] =
            new GradientAlphaKey(
                0f,
                1f);

        runtimeGradients[lineIndex].SetKeys(
            colorKeyBuffers[lineIndex],
            alphaKeys);

        line.colorGradient =
            runtimeGradients[lineIndex];
    }

    public void CompleteFormation()
    {
        Destroy(
            gameObject);
    }
}