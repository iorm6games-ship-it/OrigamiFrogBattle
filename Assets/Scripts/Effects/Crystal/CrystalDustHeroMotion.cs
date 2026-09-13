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

    [Header("Condense Visual")]
    [SerializeField]
    [Range(0f, 1f)]
    private float condenseScaleStart = 0.75f;

    [SerializeField]
    [Range(0.01f, 1f)]
    private float condenseEndScale = 0.08f;

    [Header("Crystal Rotation")]
    [SerializeField]
    private Vector3 rotationSpeed =
        new Vector3(6f, 10f, 4f);

    [SerializeField]
    private CrystalInnerSparkleController innerSparkle;

    private Phase phase =
        Phase.Idle;

    private CrystalFormationController formation;

    private Vector3 baseScale;

    private Vector3 vortexCenter;
    private Vector3 vortexAxis;

    private Vector3 vortexBasisX;
    private Vector3 vortexBasisY;

    private float elapsedTime;

    private float startAngle;
    private float currentAngle;

    private float currentAngularSpeed;

    private float currentTargetRadius;
    private float currentJoinDuration;

    private float heightPhase;
    private float radialPhase;

    //
    // 旧コードとの互換確認用。
    // 現在はVortexへの合流完了という意味。
    //
    public bool IsBurstComplete { get; private set; }

    public bool IsVortexReady { get; private set; }

    private void Awake()
    {
        baseScale =
            transform.localScale;
    }

    public void Play(
        Vector3 origin,
        Vector3 axis,
        VortexRole role,
        CrystalFormationController owner)
    {
        formation = owner;

        IsBurstComplete = false;
        IsVortexReady = false;

        vortexCenter = origin;

        vortexAxis =
            axis.sqrMagnitude > 0.0001f
                ? axis.normalized
                : Vector3.up;

        CreateVortexBasis();

        ConfigureForRole(
            role);

        startAngle =
            Random.Range(
                0f,
                360f);

        currentAngle =
            startAngle;

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

        transform.position =
            vortexCenter;

        transform.localScale =
            baseScale *
            birthStartScale;

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

        //
        // 結晶そのものの自転。
        //
        transform.Rotate(
            rotationSpeed *
            Time.deltaTime,
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
        elapsedTime +=
            Time.deltaTime;

        float t =
            Mathf.Clamp01(
                elapsedTime /
                Mathf.Max(
                    0.01f,
                    birthDuration));

        birthVisual?.UpdateBirthVisual(
            t);

        const float settleStart =
            0.78f;

        float scaleMultiplier;

        if (t < settleStart)
        {
            float growT =
                t /
                settleStart;

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
            baseScale *
            scaleMultiplier;

        if (t >= 1f)
        {
            transform.localScale =
                baseScale;

            birthVisual?.CompleteBirth();

            elapsedTime = 0f;

            phase =
                Phase.Vortex;
        }
    }

    private void UpdateVortex()
    {
        elapsedTime +=
            Time.deltaTime;

        float joinT =
            Mathf.Clamp01(
                elapsedTime /
                Mathf.Max(
                    0.01f,
                    currentJoinDuration));

        //
        // 各Dust自身が中心から渦へ合流する進捗。
        //
        float outwardT =
            1f -
            Mathf.Pow(
                1f - joinT,
                3f);

        //
        // Formation全体の回転倍率。
        //
        float spinScale =
            formation != null
                ? formation.VortexSpinScale
                : 1f;

        currentAngle +=
            currentAngularSpeed *
            spinScale *
            Time.deltaTime;

        float angleRad =
            currentAngle *
            Mathf.Deg2Rad;

        Vector3 radialDirection =
            vortexBasisX *
            Mathf.Cos(
                angleRad) +
            vortexBasisY *
            Mathf.Sin(
                angleRad);

        //
        // 個体ごとのわずかな半径揺らぎ。
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

        //
        // Formation全体の半径倍率。
        //
        float radiusScale =
            formation != null
                ? formation.VortexRadiusScale
                : 1f;

        float currentRadius =
            targetRadius *
            outwardT *
            radiusScale;

        //
        // 個体ごとの高さ。
        // 正方向のみなので紙の下へ潜らない。
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

        //
        // Formation全体の高さ倍率。
        //
        float heightScale =
            formation != null
                ? formation.VortexHeightScale
                : 1f;

        float currentHeight =
            targetHeight *
            Mathf.SmoothStep(
                0f,
                1f,
                joinT) *
            heightScale;

        transform.position =
            vortexCenter +
            radialDirection *
            currentRadius +
            vortexAxis *
            currentHeight;

        //
        // Formationの凝縮進捗に合わせて、
        // 終盤だけDust自体も縮小する。
        //
        float condenseProgress =
            formation != null
                ? formation.CondenseProgress
                : 0f;

        float scaleT =
            Mathf.InverseLerp(
                condenseScaleStart,
                1f,
                condenseProgress);

        scaleT =
            Mathf.SmoothStep(
                0f,
                1f,
                scaleT);

        transform.localScale =
            Vector3.Lerp(
                baseScale,
                baseScale *
                condenseEndScale,
                scaleT);
        //
        // Formation 全体のエネルギーを
        // InnerSparkle へ反映
        //
        if (innerSparkle != null &&
            formation != null)
        {
            innerSparkle.SetEnergyLevel(formation.SparkleEnergy);
        }

        //
        // 自分自身のVortexへの合流完了。
        //
        if (!IsVortexReady &&
            joinT >= 1f)
        {
            IsBurstComplete = true;
            IsVortexReady = true;
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
        // vortexAxisに対して垂直な
        // 2本のBasisを作る。
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

    public void CompleteFormation()
    {
        phase =
            Phase.Idle;

        Destroy(
            gameObject);
    }
}