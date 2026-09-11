using UnityEngine;

public sealed class CrystalDustHeroMotion : MonoBehaviour
{
    public enum VortexRole
    {
        Seed,
        Build
    }

    private enum Phase
    {
        Idle,
        Birth,
        Vortex
    }

    [Header("Birth")]
    [SerializeField]
    private float birthDuration = 0.2f;

    [SerializeField]
    private float birthStartScale = 0.08f;

    [SerializeField]
    private float birthOverShoot = 1.12f;

    [SerializeField]
    private CrystalDustBirthVisualController birthVisual;

    [Header("Vortex - Seed")]
    [SerializeField]
    private float seedJoinDuration = 0.28f;

    [SerializeField]
    private float seedRadius = 0.28f;

    [Header("Vortex - Build")]
    [SerializeField]
    private float buildJoinDuration = 0.36f;

    [SerializeField]
    private float buildRadiusMin = 0.32f;

    [SerializeField]
    private float buildRadiusMax = 0.50f;

    [Header("Vortex - Rotation")]
    [SerializeField]
    private float vortexAngularSpeed = 320f;

    [SerializeField]
    private float vortexAngularSpeedVariation = 45f;

    [Header("Vortex - Height")]
    [SerializeField]
    private float vortexMinHeight = 0.07f;

    [SerializeField]
    private float vortexMaxHeight = 0.22f;

    [SerializeField]
    private float vortexHeightFrequency = 0.85f;

    [Header("Vortex - Organic Motion")]
    [SerializeField]
    private float radialWobble = 0.035f;

    [SerializeField]
    private float radialWobbleFrequency = 0.75f;

    [Header("Crystal Rotation")]
    [SerializeField]
    private Vector3 rotationSpeed =
        new Vector3(6f, 10f, 4f);

    private Phase phase = Phase.Idle;

    private Vector3 baseScale;

    private Vector3 vortexCenter;
    private Vector3 vortexAxis;

    private Vector3 vortexBasisX;
    private Vector3 vortexBasisY;

    private float elapsedTime;

    private float startAngle;
    private float currentAngularSpeed;
    private float currentTargetRadius;
    private float currentJoinDuration;

    private float heightPhase;
    private float radialPhase;

    // 旧コードとの互換確認用。
    // 現在は「初期の渦への合流が完了した」という意味。
    public bool IsBurstComplete { get; private set; }

    private void Awake()
    {
        baseScale = transform.localScale;
    }

    public void Play(
        Vector3 origin,
        Vector3 axis,
        VortexRole role)
    {
        IsBurstComplete = false;

        vortexCenter = origin;

        vortexAxis =
            axis.sqrMagnitude > 0.0001f
                ? axis.normalized
                : Vector3.up;

        CreateVortexBasis();

        ConfigureForRole(role);

        startAngle =
            Random.Range(
                0f,
                360f);

        currentAngularSpeed =
            vortexAngularSpeed +
            Random.Range(
                -vortexAngularSpeedVariation,
                vortexAngularSpeedVariation);

        heightPhase =
            Random.Range(
                0f,
                Mathf.PI * 2f);

        radialPhase =
            Random.Range(
                0f,
                Mathf.PI * 2f);

        transform.position = vortexCenter;

        transform.localScale =
            baseScale * birthStartScale;

        elapsedTime = 0f;

        birthVisual?.BeginBirth();

        phase = Phase.Birth;
    }

    private void Update()
    {
        if (phase == Phase.Idle)
        {
            return;
        }

        transform.Rotate(
            rotationSpeed * Time.deltaTime,
            Space.Self);

        switch (phase)
        {
            case Phase.Birth:
                UpdateBirth();
                break;

            case Phase.Vortex:
                UpdateVortex();
                break;
        }
    }

    private void UpdateBirth()
    {
        elapsedTime += Time.deltaTime;

        float t =
            Mathf.Clamp01(
                elapsedTime /
                Mathf.Max(0.01f, birthDuration));

        birthVisual?.UpdateBirthVisual(t);

        const float settleStart = 0.78f;

        float scaleMultiplier;

        if (t < settleStart)
        {
            float growT =
                t / settleStart;

            scaleMultiplier =
                Mathf.Lerp(
                    birthStartScale,
                    birthOverShoot,
                    growT);
        }
        else
        {
            float settleT =
                (t - settleStart) /
                (1f - settleStart);

            settleT =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    settleT);

            scaleMultiplier =
                Mathf.Lerp(
                    birthOverShoot,
                    1f,
                    settleT);
        }

        transform.localScale =
            baseScale * scaleMultiplier;

        if (t >= 1f)
        {
            transform.localScale = baseScale;

            birthVisual?.CompleteBirth();

            elapsedTime = 0f;

            phase = Phase.Vortex;
        }
    }

    private void UpdateVortex()
    {
        elapsedTime += Time.deltaTime;

        float joinT =
            Mathf.Clamp01(
                elapsedTime /
                Mathf.Max(
                    0.01f,
                    currentJoinDuration));

        //
        // 中心から最初に勢いよく弾け、
        // 後半で半径方向の動きが落ち着く。
        //
        float outwardT =
            1f -
            Mathf.Pow(
                1f - joinT,
                3f);

        //
        // 生まれた瞬間から回転する。
        //
        float angle =
            startAngle +
            currentAngularSpeed *
            elapsedTime;

        float angleRad =
            angle *
            Mathf.Deg2Rad;

        Vector3 radialDirection =
            vortexBasisX *
            Mathf.Cos(angleRad) +
            vortexBasisY *
            Mathf.Sin(angleRad);

        //
        // 完全な円にせず、
        // 半径をわずかに揺らす。
        //
        float radiusVariation =
            Mathf.Sin(
                elapsedTime *
                radialWobbleFrequency *
                Mathf.PI *
                2f +
                radialPhase) *
            radialWobble;

        float targetRadius =
            Mathf.Max(
                0f,
                currentTargetRadius +
                radiusVariation);

        float radius =
            targetRadius *
            outwardT;

        //
        // 紙面から上側だけに高さを作る。
        // 上下対称に振らないので紙の下へ潜らない。
        //
        float heightWave =
            0.5f +
            0.5f *
            Mathf.Sin(
                elapsedTime *
                vortexHeightFrequency *
                Mathf.PI *
                2f +
                heightPhase);

        float targetHeight =
            Mathf.Lerp(
                vortexMinHeight,
                vortexMaxHeight,
                heightWave);

        float height =
            targetHeight *
            Mathf.SmoothStep(
                0f,
                1f,
                joinT);

        transform.position =
            vortexCenter +
            radialDirection * radius +
            vortexAxis * height;

        if (!IsBurstComplete &&
            joinT >= 1f)
        {
            IsBurstComplete = true;
        }
    }

    private void ConfigureForRole(
        VortexRole role)
    {
        switch (role)
        {
            case VortexRole.Seed:
                currentJoinDuration =
                    seedJoinDuration;

                currentTargetRadius =
                    seedRadius;
                break;

            case VortexRole.Build:
                currentJoinDuration =
                    buildJoinDuration;

                currentTargetRadius =
                    Random.Range(
                        buildRadiusMin,
                        buildRadiusMax);
                break;
        }
    }

    private void CreateVortexBasis()
    {
        //
        // vortexAxisに対して垂直な2本の軸を作る。
        // この2本が渦の回転面になる。
        //
        Vector3 reference =
            Mathf.Abs(
                Vector3.Dot(
                    vortexAxis,
                    Vector3.up)) < 0.95f
                ? Vector3.up
                : Vector3.forward;

        vortexBasisX =
            Vector3.Cross(
                vortexAxis,
                reference).normalized;

        vortexBasisY =
            Vector3.Cross(
                vortexAxis,
                vortexBasisX).normalized;
    }
}