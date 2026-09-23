using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CrystalEnergyBeam : MonoBehaviour
{
    [Header("References")]

    [SerializeField]
    private Transform source;

    [SerializeField]
    private Transform target;

    [SerializeField]
    private LineRenderer coreLine;

    [SerializeField]
    private LineRenderer glowLine;


    [Header("Plasma Branch")]

    [SerializeField]
    private LineRenderer branchCoreLine;

    [SerializeField]
    private LineRenderer branchGlowLine;

    [SerializeField]
    private bool enableBranch = true;


    [Header("Main Filament Shape")]

    [Range(12, 48)]
    [SerializeField]
    private int segmentCount = 24;

    [Tooltip("途中に置く独立した屈曲点数。")]
    [Range(2, 7)]
    [SerializeField]
    private int kinkCount = 4;

    [Tooltip("画面方向の主な曲がり幅。")]
    [Min(0f)]
    [SerializeField]
    private float kinkAmplitude = 0.030f;

    [Tooltip("奥行き方向の曲がり幅。")]
    [Min(0f)]
    [SerializeField]
    private float depthAmplitude = 0.017f;

    [Tooltip("次のプラズマ形状へ移る間隔。")]
    [Min(0.03f)]
    [SerializeField]
    private float shapeChangeInterval = 0.10f;

    [Tooltip("主線表面のごく細かい不安定さ。")]
    [Min(0f)]
    [SerializeField]
    private float microAmplitude = 0.0035f;

    [Min(0.1f)]
    [SerializeField]
    private float microFrequency = 11f;

    [Min(0f)]
    [SerializeField]
    private float microMotionSpeed = 5f;

    [Range(0.5f, 4f)]
    [SerializeField]
    private float endpointStabilityPower = 1.6f;


    [Header("Width")]

    [Min(0.0001f)]
    [SerializeField]
    private float coreWidth = 0.007f;

    [Min(0.0001f)]
    [SerializeField]
    private float glowWidth = 0.032f;

    [Min(0.0001f)]
    [SerializeField]
    private float branchCoreWidth = 0.0045f;

    [Min(0.0001f)]
    [SerializeField]
    private float branchGlowWidth = 0.020f;


    [Header("Branch Behaviour")]

    [Range(4, 12)]
    [SerializeField]
    private int branchPointCount = 7;

    [Min(0.03f)]
    [SerializeField]
    private float branchLifetime = 0.12f;

    [SerializeField]
    private Vector2 branchCooldownRange =
        new Vector2(0.12f, 0.28f);

    [Range(0.05f, 0.5f)]
    [SerializeField]
    private float branchLengthRatio = 0.20f;

    [SerializeField]
    private Vector2 branchStartRange =
        new Vector2(0.25f, 0.75f);


    [Header("Reveal")]

    [Min(0.01f)]
    [SerializeField]
    private float revealDuration = 0.18f;


    private Vector3[] fullPoints;
    private Vector3[] visiblePoints;
    private Vector3[] knotPoints;

    private Vector2[] fromOffsets;
    private Vector2[] toOffsets;
    private Vector2[] currentOffsets;

    private Vector3[] branchPoints;

    private float visibleProgress;
    private float elapsedTime;

    private float shapeTimer;
    private bool shapeInitialized;

    private bool branchActive;
    private float branchElapsed;
    private float branchTimer;

    private float branchStartU;
    private float branchDirectionAngle;
    private float branchLengthScale;
    private float branchNoiseSeed;

    private bool initialized;
    private Coroutine revealCoroutine;


    private void Awake()
    {
        Initialize();

        SetVisibleImmediate(false);
    }


    private void Initialize()
    {
        fullPoints =
            new Vector3[
                segmentCount + 1];

        visiblePoints =
            new Vector3[
                segmentCount + 1];

        knotPoints =
            new Vector3[
                kinkCount + 2];

        fromOffsets =
            new Vector2[
                kinkCount];

        toOffsets =
            new Vector2[
                kinkCount];

        currentOffsets =
            new Vector2[
                kinkCount];

        branchPoints =
            new Vector3[
                branchPointCount];


        ConfigureMainLine(
            coreLine,
            coreWidth);

        ConfigureMainLine(
            glowLine,
            glowWidth);

        ConfigureBranchLine(
            branchCoreLine,
            branchCoreWidth);

        ConfigureBranchLine(
            branchGlowLine,
            branchGlowWidth);


        branchTimer =
            Random.Range(
                branchCooldownRange.x,
                branchCooldownRange.y);

        initialized = true;
    }


    private static void ConfigureMainLine(
        LineRenderer line,
        float width)
    {
        if (line == null)
        {
            return;
        }

        line.useWorldSpace = true;
        line.loop = false;

        line.widthMultiplier =
            width;

        line.numCapVertices = 4;

        //
        // 少しだけ丸める。
        // 完全な角張りにはしない。
        //
        line.numCornerVertices = 2;

        line.textureMode =
            LineTextureMode.Stretch;
    }


    private static void ConfigureBranchLine(
        LineRenderer line,
        float width)
    {
        if (line == null)
        {
            return;
        }

        line.useWorldSpace = true;
        line.loop = false;

        line.widthMultiplier =
            width;

        line.numCapVertices = 3;
        line.numCornerVertices = 2;

        line.textureMode =
            LineTextureMode.Stretch;

        //
        // 枝先へ向かって細く消す。
        //
        line.widthCurve =
            new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.55f, 0.72f),
                new Keyframe(1f, 0f));

        line.enabled = false;
    }


    private void LateUpdate()
    {
        if (!initialized ||
            source == null ||
            target == null)
        {
            return;
        }

        ApplyRuntimeWidths();

        elapsedTime +=
            Time.deltaTime;

        UpdatePlasmaShape();

        BuildMainPath();

        ApplyVisiblePath();

        UpdateBranch();
    }

    private void ApplyRuntimeWidths()
    {
        if (coreLine != null)
        {
            coreLine.widthMultiplier =
                coreWidth;
        }

        if (glowLine != null)
        {
            glowLine.widthMultiplier =
                glowWidth;
        }        
    }

    private void UpdatePlasmaShape()
    {
        if (!shapeInitialized)
        {
            GenerateOffsets(
                currentOffsets);

            CopyOffsets(
                currentOffsets,
                fromOffsets);

            GenerateOffsets(
                toOffsets);

            shapeTimer = 0f;
            shapeInitialized = true;

            return;
        }


        shapeTimer +=
            Time.deltaTime;

        if (shapeTimer >=
            shapeChangeInterval)
        {
            CopyOffsets(
                currentOffsets,
                fromOffsets);

            GenerateOffsets(
                toOffsets);

            shapeTimer -=
                shapeChangeInterval;
        }


        float t =
            Mathf.Clamp01(
                shapeTimer /
                shapeChangeInterval);

        //
        // 次の経路へ滑らかに移るが、
        // 波が流れているわけではない。
        //
        t =
            t * t *
            (3f - 2f * t);


        for (int i = 0;
             i < kinkCount;
             i++)
        {
            currentOffsets[i] =
                Vector2.Lerp(
                    fromOffsets[i],
                    toOffsets[i],
                    t);
        }
    }


    private void GenerateOffsets(
        Vector2[] values)
    {
        for (int i = 0;
             i < values.Length;
             i++)
        {
            values[i] =
                new Vector2(
                    Random.Range(
                        -1f,
                        1f),

                    Random.Range(
                        -1f,
                        1f));
        }
    }


    private static void CopyOffsets(
        Vector2[] sourceValues,
        Vector2[] destination)
    {
        for (int i = 0;
             i < sourceValues.Length;
             i++)
        {
            destination[i] =
                sourceValues[i];
        }
    }


    private void BuildMainPath()
    {
        Vector3 start =
            source.position;

        Vector3 end =
            target.position;


        if (!TryBuildBasis(
                start,
                end,
                out Vector3 forward,
                out Vector3 side,
                out Vector3 depth,
                out float length))
        {
            for (int i = 0;
                 i < fullPoints.Length;
                 i++)
            {
                fullPoints[i] =
                    start;
            }

            return;
        }


        int lastKnot =
            knotPoints.Length - 1;

        knotPoints[0] =
            start;

        knotPoints[lastKnot] =
            end;


        for (int i = 1;
             i < lastKnot;
             i++)
        {
            float u =
                i /
                (float)lastKnot;

            float envelope =
                Mathf.Pow(
                    Mathf.Sin(
                        u *
                        Mathf.PI),
                    endpointStabilityPower);


            Vector2 offset =
                currentOffsets[
                    i - 1];


            knotPoints[i] =
                Vector3.Lerp(
                    start,
                    end,
                    u) +

                side *
                offset.x *
                kinkAmplitude *
                envelope +

                depth *
                offset.y *
                depthAmplitude *
                envelope;
        }


        int lastPoint =
            fullPoints.Length - 1;


        for (int i = 0;
             i <= lastPoint;
             i++)
        {
            float u =
                i /
                (float)lastPoint;


            float knotPosition =
                u *
                lastKnot;

            int knotIndex =
                Mathf.Clamp(
                    Mathf.FloorToInt(
                        knotPosition),
                    0,
                    lastKnot - 1);

            float localT =
                knotPosition -
                knotIndex;


            //
            // CatmullRomではなく、
            // 局所的な屈曲を残す。
            //
            Vector3 point =
                Vector3.Lerp(
                    knotPoints[knotIndex],
                    knotPoints[knotIndex + 1],
                    localT);


            float envelope =
                Mathf.Pow(
                    Mathf.Sin(
                        u *
                        Mathf.PI),
                    endpointStabilityPower);


            float microA =
                SignedNoise(
                    u *
                    microFrequency,
                    elapsedTime *
                    microMotionSpeed,
                    71.2f);

            float microB =
                SignedNoise(
                    u *
                    microFrequency,
                    elapsedTime *
                    microMotionSpeed,
                    136.7f);


            point +=
                (
                    side *
                    microA +
                    depth *
                    microB
                ) *
                microAmplitude *
                envelope;


            fullPoints[i] =
                point;
        }


        //
        // Crystal先端だけは絶対に外さない。
        //
        fullPoints[0] =
            start;

        fullPoints[lastPoint] =
            end;
    }


    private void UpdateBranch()
    {
        if (!enableBranch ||
            branchCoreLine == null ||
            branchGlowLine == null ||
            visibleProgress < 0.98f)
        {
            SetBranchVisible(false);
            return;
        }


        if (branchActive)
        {
            branchElapsed +=
                Time.deltaTime;


            float lifeT =
                branchElapsed /
                Mathf.Max(
                    0.01f,
                    branchLifetime);


            if (lifeT >= 1f)
            {
                branchActive = false;

                SetBranchVisible(false);

                branchTimer =
                    Random.Range(
                        branchCooldownRange.x,
                        branchCooldownRange.y);

                return;
            }


            BuildBranchPath(
                lifeT);

            return;
        }


        branchTimer -=
            Time.deltaTime;


        if (branchTimer <= 0f)
        {
            SpawnBranch();
        }
    }


    private void SpawnBranch()
    {
        branchActive = true;
        branchElapsed = 0f;

        branchStartU =
            Random.Range(
                branchStartRange.x,
                branchStartRange.y);

        branchDirectionAngle =
            Random.Range(
                0f,
                Mathf.PI *
                2f);

        branchLengthScale =
            Random.Range(
                0.75f,
                1.15f);

        branchNoiseSeed =
            Random.Range(
                0f,
                1000f);

        SetBranchVisible(true);
    }


    private void BuildBranchPath(
        float lifeT)
    {
        Vector3 start =
            source.position;

        Vector3 end =
            target.position;


        if (!TryBuildBasis(
                start,
                end,
                out Vector3 forward,
                out Vector3 side,
                out Vector3 depth,
                out float beamLength))
        {
            return;
        }


        Vector3 branchStart =
            SampleMainPath(
                branchStartU);


        Vector3 branchDirection =
            (
                side *
                Mathf.Cos(
                    branchDirectionAngle) +

                depth *
                Mathf.Sin(
                    branchDirectionAngle) +

                forward *
                0.10f
            ).normalized;


        float length =
            beamLength *
            branchLengthRatio *
            branchLengthScale;


        int last =
            branchPoints.Length - 1;


        for (int i = 0;
             i <= last;
             i++)
        {
            float t =
                i /
                (float)last;


            float envelope =
                Mathf.Sin(
                    t *
                    Mathf.PI);


            float noiseA =
                SignedNoise(
                    t * 5f,
                    elapsedTime * 7f,
                    branchNoiseSeed);

            float noiseB =
                SignedNoise(
                    t * 6.3f,
                    elapsedTime * 8f,
                    branchNoiseSeed +
                    73f);


            branchPoints[i] =
                branchStart +

                branchDirection *
                length *
                t +

                (
                    side *
                    noiseA +
                    depth *
                    noiseB
                ) *
                length *
                0.06f *
                envelope;
        }


        //
        // 生えて、短時間だけ存在して、消える。
        //
        float fade =
            Mathf.Sin(
                Mathf.PI *
                Mathf.Clamp01(
                    lifeT));

        branchCoreLine.widthMultiplier =
            branchCoreWidth *
            fade;

        branchGlowLine.widthMultiplier =
            branchGlowWidth *
            fade;


        ApplyBranchLine(
            branchCoreLine);

        ApplyBranchLine(
            branchGlowLine);
    }


    private Vector3 SampleMainPath(
        float u)
    {
        u =
            Mathf.Clamp01(
                u);

        float position =
            u *
            (
                fullPoints.Length -
                1);

        int index =
            Mathf.Clamp(
                Mathf.FloorToInt(
                    position),
                0,
                fullPoints.Length -
                2);

        float t =
            position -
            index;

        return
            Vector3.Lerp(
                fullPoints[index],
                fullPoints[index + 1],
                t);
    }


    private void ApplyBranchLine(
        LineRenderer line)
    {
        line.positionCount =
            branchPoints.Length;

        for (int i = 0;
             i < branchPoints.Length;
             i++)
        {
            line.SetPosition(
                i,
                branchPoints[i]);
        }
    }


    private static bool TryBuildBasis(
        Vector3 start,
        Vector3 end,
        out Vector3 forward,
        out Vector3 side,
        out Vector3 depth,
        out float length)
    {
        Vector3 beam =
            end -
            start;

        length =
            beam.magnitude;


        if (length < 0.0001f)
        {
            forward =
                Vector3.forward;

            side =
                Vector3.right;

            depth =
                Vector3.up;

            return false;
        }


        forward =
            beam /
            length;


        Vector3 referenceAxis =
            Mathf.Abs(
                Vector3.Dot(
                    forward,
                    Vector3.up)) < 0.92f
                ? Vector3.up
                : Vector3.right;


        side =
            Vector3.Cross(
                forward,
                referenceAxis).normalized;

        depth =
            Vector3.Cross(
                forward,
                side).normalized;


        return true;
    }


    private static float SignedNoise(
        float position,
        float time,
        float seed)
    {
        return
            Mathf.PerlinNoise(
                position + seed,
                time + seed * 0.173f) *
            2f -
            1f;
    }


    private void ApplyVisiblePath()
    {
        int lastIndex =
            fullPoints.Length - 1;


        float scaledProgress =
            Mathf.Clamp01(
                visibleProgress) *
            lastIndex;


        int completeSegments =
            Mathf.FloorToInt(
                scaledProgress);


        float partialSegment =
            scaledProgress -
            completeSegments;


        int visibleCount =
            Mathf.Clamp(
                completeSegments + 2,
                2,
                fullPoints.Length);


        for (int i = 0;
             i < visibleCount;
             i++)
        {
            if (i <= completeSegments)
            {
                visiblePoints[i] =
                    fullPoints[i];
            }
            else
            {
                int previous =
                    Mathf.Clamp(
                        completeSegments,
                        0,
                        lastIndex);

                int next =
                    Mathf.Clamp(
                        previous + 1,
                        0,
                        lastIndex);


                visiblePoints[i] =
                    Vector3.Lerp(
                        fullPoints[previous],
                        fullPoints[next],
                        partialSegment);
            }
        }


        ApplyMainLine(
            coreLine,
            visibleCount);

        ApplyMainLine(
            glowLine,
            visibleCount);
    }


    private void ApplyMainLine(
        LineRenderer line,
        int count)
    {
        if (line == null)
        {
            return;
        }


        line.positionCount =
            count;


        for (int i = 0;
             i < count;
             i++)
        {
            line.SetPosition(
                i,
                visiblePoints[i]);
        }
    }


    public void SetEndpoints(
        Transform newSource,
        Transform newTarget)
    {
        source =
            newSource;

        target =
            newTarget;

        shapeInitialized = false;
    }


    public void SetVisibleImmediate(
        bool visible)
    {
        if (revealCoroutine != null)
        {
            StopCoroutine(
                revealCoroutine);

            revealCoroutine = null;
        }


        visibleProgress =
            visible
                ? 1f
                : 0f;


        if (coreLine != null)
        {
            coreLine.enabled =
                visible;
        }

        if (glowLine != null)
        {
            glowLine.enabled =
                visible;
        }


        branchActive = false;

        SetBranchVisible(false);
    }


    public void PlayReveal()
    {
        if (revealCoroutine != null)
        {
            StopCoroutine(
                revealCoroutine);
        }


        revealCoroutine =
            StartCoroutine(
                RevealRoutine());
    }


    private IEnumerator RevealRoutine()
    {
        if (coreLine != null)
        {
            coreLine.enabled = true;
        }

        if (glowLine != null)
        {
            glowLine.enabled = true;
        }


        visibleProgress = 0f;

        float elapsed = 0f;


        while (elapsed <
               revealDuration)
        {
            elapsed +=
                Time.deltaTime;


            float t =
                Mathf.Clamp01(
                    elapsed /
                    revealDuration);


            visibleProgress =
                1f -
                Mathf.Pow(
                    1f - t,
                    3f);


            yield return null;
        }


        visibleProgress = 1f;

        branchTimer =
            Random.Range(
                0.03f,
                0.10f);

        revealCoroutine = null;
    }


    private void SetBranchVisible(
        bool visible)
    {
        if (branchCoreLine != null)
        {
            branchCoreLine.enabled =
                visible;
        }

        if (branchGlowLine != null)
        {
            branchGlowLine.enabled =
                visible;
        }
    }
}