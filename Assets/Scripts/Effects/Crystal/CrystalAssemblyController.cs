using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CrystalAssemblyController : MonoBehaviour
{
    private static readonly int FormProgressId =
        Shader.PropertyToID("_FormProgress");

    
    private static readonly int AlphaId =
        Shader.PropertyToID("_Alpha");

    private static readonly int EmissionStrengthId =
        Shader.PropertyToID("_EmissionStrength");

    private static readonly int EdgeStrengthId =
        Shader.PropertyToID("_EdgeStrength");

    private static readonly int FaceHighlightStrengthId =
        Shader.PropertyToID("_FaceHighlightStrength");

    private static readonly int FormFrontStrengthId =
        Shader.PropertyToID("_FormFrontStrength");

    [Header("Visual Rotation Correction")]

    [Tooltip(
        "BirthPointから継承した傾きをCrystal Visualだけ補正する。"
    )]
    [SerializeField]
    private Vector3 visualRotationCorrectionEuler =
        new Vector3(30f, 0f, 0f);

    [Header("Assembly References")]
    [Tooltip("配置した PF_CrystalAssembly_16Pieces のルート。完成後の浮遊もこのTransformへ適用する。")]
    [SerializeField]
    private Transform assemblyVisualRoot;

    [Tooltip("FBX内の CrystalAssemblyRoot。直下の16オブジェクトを欠片として自動収集する。")]
    [SerializeField]
    private Transform piecesRoot;

    [Tooltip("FBX内に同梱した Crystal_Final。形成の終盤で欠片からこの完成メッシュへ切り替える。")]
    [SerializeField]
    private Transform embeddedFinalCrystal;

    [Header("Visual Alignment")]
    [Tooltip("旧CrystalのRenderer Boundsへ、新しいCrystal_Finalの見た目の大きさと中心を自動一致させる。OFFの場合はVisual Size Multiplierを全体の絶対Scaleとして使う。")]
    [SerializeField]
    private bool matchLegacyRendererBounds = false;

    [Tooltip("完成クリスタル全体の大きさ。Bounds一致OFFの場合は親Transformへ同じScale値を直接設定する。")]
    [Range(0.01f, 2f)]
    [SerializeField]
    private float visualSizeMultiplier = 1f;

    [Tooltip("Bounds一致後の微調整。X=横、Y=上、Z=奥。centerPointの軸を基準にしたワールド単位。")]
    [SerializeField]
    private Vector3 visualOffsetInCenterSpace = Vector3.zero;

    [Header("Scatter")]
    [SerializeField]
    private int scatterSeed = 73129;

    [SerializeField]
    private float startRadiusMultiplier = 2.2f;

    [SerializeField]
    private float startHeightMultiplier = 0.85f;

    [SerializeField]
    private Vector2 fragmentScaleRange =
        new Vector2(0.70f, 1.10f);

    [SerializeField]
    private float curveAmountMultiplier = 0.55f;

    [Header("Bottom-Up Assembly")]
    [Range(0f, 0.6f)]
    [SerializeField]
    private float stackStart = 0.05f;

    [Range(0.3f, 1f)]
    [SerializeField]
    private float stackEnd = 0.72f;

    [Range(0.05f, 0.7f)]
    [SerializeField]
    private float fragmentTravelWindow = 0.30f;

    [Range(0f, 0.25f)]
    [SerializeField]
    private float orderJitter = 0.025f;

    [Header("Authored Layer Order")]
    [Tooltip("Crystal_Layer01_... のLayer番号を優先して、Blenderで設計した建築順をそのまま使う。名前を解析できない欠片が1つでもある場合は、高さ順へ自動フォールバックする。")]
    [SerializeField]
    private bool useAuthoredLayerOrder = true;

    [Header("Authored Growth Order")]

    [Tooltip(
        "Layer順ではなく、結晶が隣接Shardへ伝播するように組み立てる。"
    )]
    [SerializeField]
    private bool useAuthoredGrowthOrder = true;

    [Tooltip(
        "Crystal_Lowerを除く15片のAssembly順。"
    )]
    [SerializeField]
    private string[] authoredGrowthOrder =
    {
        // Front / Right branch
        "Crystal_Layer01_B",
        "Crystal_Layer02_Front_B",

        // Front / Left branch
        "Crystal_Layer01_A",

        // Right branch grows upward first
        "Crystal_Layer03_Right_C",

        "Crystal_Layer02_Front_A",

        // Back branch starts later
        "Crystal_Layer01_C",

        // First branch reaches upper area
        "Crystal_Layer04_A",

        // Left side follows
        "Crystal_Layer03_Left_A",

        // Back-right branch
        "Crystal_Layer02_Back_D",

        // Left branch reaches Layer04
        "Crystal_Layer04_B",

        "Crystal_Layer03_Right_D",

        // Back-left fills later
        "Crystal_Layer02_Back_C",
        "Crystal_Layer03_Left_B",

        // Upper rear closes last
        "Crystal_Layer04_C",

        // Final cap
        "Crystal_Top"
    };

    [Tooltip("同じLayer内の欠片を完全同時にせず、少しだけ重ねて開始する幅。Formation Progress単位。")]
    [Range(0f, 0.15f)]
    [SerializeField]
    private float withinLayerStartSpread = 0.025f;

    [Range(0f, 0.25f)]
    [SerializeField]
    private float settlePortion = 0.22f;

    [Range(0f, 0.20f)]
    [SerializeField]
    private float settleScaleOvershoot = 0.06f;
        
    [Header("Sequential Assembly")]
    [Tooltip("1片がScatter位置から完成位置へ移動する実時間。全体時間とは独立。")]
    [Min(0.05f)]
    [SerializeField]
    private float shardTravelDuration = 0.28f;

    [Tooltip("次のShardを動かし始めるまでの実時間。Travel Durationより短くすると、順番を保ったまま滑らかに重なる。")]
    [Min(0.01f)]
    [SerializeField]
    private float shardStartInterval = 0.08f;

    [Tooltip("最後のShardが収まった後、Final Crystalへ一体化する前の短い間。")]
    [Min(0f)]
    [SerializeField]
    private float postAssemblyHoldDuration = 0.07f;

    [Tooltip("16片からFinal Crystalへ一体化する時間。")]
    [Min(0.05f)]
    [SerializeField]
    private float finalBlendDuration = 0.16f;

    [Tooltip("Shardの回転は移動より少し遅れて完成させる。位置と回転が同時にロックされる機械感を弱める。")]
    [Range(0f, 0.25f)]
    [SerializeField]
    private float rotationFollowDelay = 0.08f;

    [Tooltip("到着直前の横方向カーブを減衰させ、最後は完成面へ静かに吸着させる。")]
    [Range(0.55f, 0.95f)]
    [SerializeField]
    private float arrivalCurveDamping = 0.74f;

    [Tooltip("待機中のShardにごく小さい漂いを与える。0で完全静止。")]
    [Range(0f, 0.08f)]
    [SerializeField]
    private float waitingDriftAmount = 0.018f;

    [Tooltip("待機中のShardの漂い速度。")]
    [Range(0.1f, 3f)]
    [SerializeField]
    private float waitingDriftFrequency = 0.85f;

    [Header("Shard Pre-Spawn")]
    [Tooltip("各Shardが動き始める何秒前から姿を現すか。")]
    [Min(0.01f)]
    [SerializeField]
    private float shardPreSpawnDuration = 0.10f;

    [Tooltip("出現し始めのScale倍率。1より小さくすると、ふわっと現れる。")]
    [Range(0.5f, 1f)]
    [SerializeField]
    private float shardPreSpawnStartScale = 0.88f;

    [Tooltip("出現中の待機ドリフト量。通常待機より控えめにする。")]
    [Range(0f, 0.05f)]
    [SerializeField]
    private float shardPreSpawnDriftAmount = 0.006f;

    [Header("Assembly Seam Suppression")]
    [Tooltip("16片それぞれのEdgeを抑え、接合面に白い線が残るのを防ぐ。既存Shard Edge Multiplierへさらに掛かる。")]
    [Range(0f, 1f)]
    [SerializeField]
    private float assemblyEdgeSuppression = 0.10f;

    [Tooltip("16片それぞれのEmissionを抑え、接合面が白く発光するのを防ぐ。既存Shard Emission Multiplierへさらに掛かる。")]
    [Range(0f, 1f)]
    [SerializeField]
    private float assemblyEmissionSuppression = 0.30f;

    [Tooltip("Shardが完成位置へ近づいた何割地点から、接合線用のEdge/Emissionをさらに落とし始めるか。")]
    [Range(0f, 0.95f)]
    [SerializeField]
    private float seamFadeStart = 0.55f;

    [Tooltip("完成位置に収まったShardのEdge倍率。接合面の白線を消すため、ほぼ0を推奨。")]
    [Range(0f, 1f)]
    [SerializeField]
    private float settledEdgeMultiplier = 0.015f;

    [Tooltip("完成位置に収まったShardのEmission倍率。接合面の発光を消すため低めにする。")]
    [Range(0f, 1f)]
    [SerializeField]
    private float settledEmissionMultiplier = 0.06f;

    [Tooltip("Assembly中のShard全体のFace Highlight倍率。移動中も白飛びしすぎないよう抑える。")]
    [Range(0f, 1f)]
    [SerializeField]
    private float assemblyFaceHighlightSuppression = 0.45f;

    [Tooltip("完成位置に収まったShardのFace Highlight倍率。切断面の白い帯を消すため、かなり低くする。")]
    [Range(0f, 1f)]
    [SerializeField]
    private float settledFaceHighlightMultiplier = 0.03f;

    [Header("Progressive Seam Heal")]
    [Tooltip("組み上がったShardを、継ぎ目のないFinal Crystalへ順次クロスフェードして切れ目を消す。")]
    [SerializeField]
    private bool progressiveSeamHealing = true;

    [Tooltip("Shardが完成位置へ収まってからFade開始までの短い待ち時間。")]
    [Min(0f)]
    [SerializeField]
    private float seamHealDelay = 0.035f;

    [Tooltip("各ShardをFinal Crystalへ受け渡すFade時間。")]
    [Min(0.05f)]
    [SerializeField]
    private float seamHealDuration = 0.18f;

    [Tooltip("受け渡し完了後に残すShardのAlpha倍率。0なら完全にFinal Crystalへ置換。")]
    [Range(0f, 0.2f)]
    [SerializeField]
    private float healedShardAlpha = 0f;

    [Tooltip("Final Crystalを下から順に出す際、Assembly進捗より少し先まで見せる量。旧連続Reveal方式用。")]
    [Range(0f, 0.12f)]
    [SerializeField]
    private float finalHealRevealLead = 0.025f;

    [Tooltip("各Layer完成後、先にShardの切れ目Fadeを始めてからFinal Crystalを追従表示するまでの遅延。")]
    [Min(0f)]
    [SerializeField]
    private float layerFinalRevealDelay = 0.045f;

    [Tooltip("Layer Heal中は_FormProgressの白いFront発光を抑える。切れ目が消える前にピカッと光るのを防ぐ。")]
    [Range(0f, 1f)]
    [SerializeField]
    private float layerHealFormFrontMultiplier = 0f;

    [Tooltip("Heal完了したShard GameObjectをその場でOFFにする。透明メッシュの内部面が最後まで残るのを防ぐ。")]
    [SerializeField]
    private bool disableHealedShardObjects = true;

    [Tooltip("各Layerの上端より少しだけFinal Crystalを先まで出して隙間を防ぐ。")]
    [Range(0f, 0.08f)]
    [SerializeField]
    private float layerRevealPadding = 0.018f;

    [Tooltip("定着時のScale Overshootを抑える。今回はほぼ0推奨。")]
    [Range(0f, 1f)]
    [SerializeField]
    private float settleScaleResponse = 0.08f;

    [Tooltip("定着時のEmission/Edgeパルスを抑える。白い接合線を出さないため低め推奨。")]
    [Range(0f, 1f)]
    [SerializeField]
    private float settleVisualResponse = 0.08f;

    [Header("Anchor Formation")]
    [Tooltip("細かい結晶が最初に形成する土台Shard。通常は Crystal_Lower を自動検出する。")]
    [SerializeField]
    private string anchorFragmentName = "Crystal_Lower";

    [Range(0.5f, 1f)]
    [SerializeField]
    private float anchorStartScale = 0.88f;

    [Range(0f, 1f)]
    [SerializeField]
    private float anchorGatherBoundsFill = 0.72f;

    [Header("Shard Visual")]
    [Range(0.05f, 1f)]
    [SerializeField]
    private float shardAlphaMultiplier = 0.82f;

    [Range(0f, 2f)]
    [SerializeField]
    private float shardEmissionMultiplier = 0.28f;

    [Range(0f, 2f)]
    [SerializeField]
    private float shardEdgeMultiplier = 0.65f;

    [Range(1f, 4f)]
    [SerializeField]
    private float settleEmissionBoost = 1.75f;

    [Range(1f, 3f)]
    [SerializeField]
    private float settleEdgeBoost = 1.20f;

    [Header("Final Crystal")]
    [Range(0f, 1f)]
    [SerializeField]
    private float finalCrystalStart = 0.82f;

    [Range(0f, 1f)]
    [SerializeField]
    private float fragmentMergeStart = 0.86f;

    [Range(0.01f, 0.5f)]
    [SerializeField]
    private float fragmentEndScale = 0.10f;

    [Range(0f, 0.3f)]
    [SerializeField]
    private float mergeHeightStagger = 0.04f;

    [Header("Completed Lift")]
    [Tooltip("Lift Target未指定時だけ使うフォールバック上昇距離。通常はCrystalEffectController側のTarget位置を使う。")]
    [Min(0f)]
    [SerializeField]
    private float completedLiftDistance = 0.16f;

    [Tooltip("完成クリスタルの先行上昇時間。")]
    [Min(0.01f)]
    [SerializeField]
    private float completedLiftDuration = 0.48f;

    [Tooltip("上昇中にほんの少しだけ目標位置を越える量。Target指定Liftでは自然さ優先で抑制する。")]
    [Min(0f)]
    [SerializeField]
    private float completedLiftOvershoot = 0.012f;

    [Tooltip("Targetへ移動するときだけLift時間を伸ばし、キュッと跳ね上がる感じを弱める。")]
    [Range(1f, 2f)]
    [SerializeField]
    private float completedLiftGlideDurationMultiplier = 1.35f;

    [Header("Completed Float")]
    [Tooltip("クリスタルの主となる上下浮遊量。紙より少し小さめ・落ち着いた動きにする。")]
    [SerializeField]
    private float floatAmplitude = 0.020f;

    [Tooltip("クリスタルの主浮遊周期。Paper側と同じ周期にしない。")]
    [SerializeField]
    private float floatFrequency = 0.58f;

    [Tooltip("主浮遊に重ねるごく小さな2次波。完全なSin同期を避ける。")]
    [Min(0f)]
    [SerializeField]
    private float secondaryFloatAmplitude = 0.006f;

    [Min(0f)]
    [SerializeField]
    private float secondaryFloatFrequencyMultiplier = 1.73f;

    [Tooltip("上下だけでなく、ごく小さく横へ漂わせる量。")]
    [Min(0f)]
    [SerializeField]
    private float lateralFloatAmplitude = 0.0045f;

    [Min(0f)]
    [SerializeField]
    private float lateralFloatFrequencyMultiplier = 0.67f;

    [SerializeField]
    private float swayDegrees = 0.75f;

    [Min(0f)]
    [SerializeField]
    private float swayFrequencyMultiplier = 0.81f;

    [SerializeField]
    private float phaseOffsetRange = 0.12f;

    [Header("Energy Transfer")]

    [Tooltip("完成Crystal 先端のエネルギー送受信用Anchor")]
    [SerializeField]
    private Transform energyTip;

    public Transform EnergyTip => energyTip;
    
    public bool IsComplete { get; private set; }
    public bool IsLeadLiftComplete { get; private set; }
    public float FloatSignal { get; private set; }
    public float CurrentFloatOffset { get; private set; }

    private Transform center;
    private Transform legacyFinalTarget;
    private Transform floatTarget;

    private Renderer[] embeddedFinalRenderers =
        Array.Empty<Renderer>();

    private float[] embeddedFinalBaseFormFrontStrength =
        Array.Empty<float>();

    private Renderer[] legacyRenderers =
        Array.Empty<Renderer>();

    private bool[] legacyRendererEnabled =
        Array.Empty<bool>();

    private readonly List<FragmentState> fragments =
        new List<FragmentState>();

    private MaterialPropertyBlock propertyBlock;

    private Quaternion assemblyVisualAuthoredLocalRotation;
    private Vector3 embeddedFinalAuthoredLocalPosition;
    private Quaternion embeddedFinalAuthoredLocalRotation;
    private Vector3 embeddedFinalAuthoredLocalScale;
    private Vector3 floatBasePosition;
    private Quaternion floatBaseRotation;

    private float floatPhaseOffset;
    private float secondaryFloatPhase;
    private float lateralFloatPhase;
    private float floatElapsedTime;

    private Coroutine completedLiftCoroutine;

    private bool hasCompletedLiftTarget;
    private Vector3 completedLiftTargetPosition;

    private Quaternion completedLiftRotationOffset =
        Quaternion.identity;
    private float completedLiftScaleMultiplier = 1f;

    private bool hierarchyCached;
    private bool initialized;
    private bool fallbackMode;
    private bool floating;
    private bool legacyRendererStateCaptured;
    private bool usingAuthoredLayerOrder;

    private float runtimeAssemblyMoveDuration;
    private float runtimeAssemblyTotalDuration;
    private float runtimeFinalCrystalStart;
    private float runtimeFragmentMergeStart;

    private FragmentState anchorFragment;
    private bool anchorFormationComplete;
    private bool remainingAssemblyStarted;
    private Bounds anchorBounds;
    private bool hasAnchorBounds;

    private sealed class FragmentRendererState
    {
        public Renderer Renderer;
        public float BaseAlpha = 1f;
        public float BaseEmissionStrength = 0f;
        public float BaseEdgeStrength = 0f;
        public float BaseFaceHighlightStrength = 0f;
    }

    private sealed class FragmentState
    {
        public Transform Transform;
        public Renderer[] Renderers;
        public FragmentRendererState[] RendererStates;

        public Vector3 AuthoredLocalPosition;
        public Quaternion AuthoredLocalRotation;
        public Vector3 AuthoredLocalScale;

        public Vector3 StartPosition;
        public Quaternion StartRotation;
        public Vector3 StartScale;

        public Vector3 TargetPosition;
        public Quaternion TargetRotation;
        public Vector3 TargetScale;

        public Vector3 CurveDirection;
        public float CurveAmount;

        // 0 = 物理的な最下段 / 1 = 物理的な最上段
        public float HeightOrder;

        // Blender / FBXで付けた Crystal_LayerXX_... のXX。
        // 解析できない場合は -1。
        public int AssemblyLayer;

        // 0 = 最初のLayer / 1 = 最後のLayer
        public float LayerOrder;

        // 同一Layer内での小さな開始順。0～1。
        public float WithinLayerOrder;

        public float TargetHeight;

        // Final Crystalの_FormProgressで、このShardの上端まで覆う値。
        public float RevealTopProgress;

        public float StartProgress;
        public float EndProgress;

        // Crystal_Lowerを除いた組み立て順。0,1,2...。
        public int SequenceIndex = -1;

        // 待機中の自然な微漂い用。
        public float WaitingPhase;
        public Vector3 WaitingDirection;

        public bool IsAnchor;
    }

    private void Awake()
    {
        propertyBlock =
            new MaterialPropertyBlock();

        if (CacheAuthoredHierarchy())
        {
            SetAssemblyVisualsDormant();
        }
    }

    private void OnValidate()
    {
        startRadiusMultiplier =
            Mathf.Max(0f, startRadiusMultiplier);

        startHeightMultiplier =
            Mathf.Max(0f, startHeightMultiplier);

        fragmentScaleRange.x =
            Mathf.Max(0.01f, fragmentScaleRange.x);

        fragmentScaleRange.y =
            Mathf.Max(fragmentScaleRange.x, fragmentScaleRange.y);

        visualSizeMultiplier =
            Mathf.Clamp(visualSizeMultiplier, 0.01f, 2f);

        stackEnd =
            Mathf.Max(stackStart, stackEnd);

        fragmentTravelWindow =
            Mathf.Max(0.01f, fragmentTravelWindow);

        withinLayerStartSpread =
            Mathf.Clamp(withinLayerStartSpread, 0f, 0.15f);

        anchorStartScale =
            Mathf.Clamp(anchorStartScale, 0.5f, 1f);

        anchorGatherBoundsFill =
            Mathf.Clamp01(anchorGatherBoundsFill);

        shardAlphaMultiplier =
            Mathf.Clamp(shardAlphaMultiplier, 0.05f, 1f);

        shardEmissionMultiplier =
            Mathf.Max(0f, shardEmissionMultiplier);

        shardEdgeMultiplier =
            Mathf.Max(0f, shardEdgeMultiplier);

        settleEmissionBoost =
            Mathf.Max(1f, settleEmissionBoost);

        settleEdgeBoost =
            Mathf.Max(1f, settleEdgeBoost);

        rotationFollowDelay =
            Mathf.Clamp(rotationFollowDelay, 0f, 0.30f);

        shardTravelDuration =
            Mathf.Max(0.05f, shardTravelDuration);

        shardStartInterval =
            Mathf.Max(0.01f, shardStartInterval);

        postAssemblyHoldDuration =
            Mathf.Max(0f, postAssemblyHoldDuration);

        finalBlendDuration =
            Mathf.Max(0.05f, finalBlendDuration);

        arrivalCurveDamping =
            Mathf.Clamp(arrivalCurveDamping, 0.55f, 0.95f);

        waitingDriftAmount =
            Mathf.Max(0f, waitingDriftAmount);

        waitingDriftFrequency =
            Mathf.Max(0.1f, waitingDriftFrequency);

        assemblyEdgeSuppression =
            Mathf.Clamp01(assemblyEdgeSuppression);

        assemblyEmissionSuppression =
            Mathf.Clamp01(assemblyEmissionSuppression);

        seamFadeStart =
            Mathf.Clamp(seamFadeStart, 0f, 0.95f);

        settledEdgeMultiplier =
            Mathf.Clamp01(settledEdgeMultiplier);

        settledEmissionMultiplier =
            Mathf.Clamp01(settledEmissionMultiplier);

        assemblyFaceHighlightSuppression =
            Mathf.Clamp01(assemblyFaceHighlightSuppression);

        settledFaceHighlightMultiplier =
            Mathf.Clamp01(settledFaceHighlightMultiplier);

        seamHealDelay =
            Mathf.Max(0f, seamHealDelay);

        seamHealDuration =
            Mathf.Max(0.05f, seamHealDuration);

        healedShardAlpha =
            Mathf.Clamp(healedShardAlpha, 0f, 0.2f);

        finalHealRevealLead =
            Mathf.Clamp(finalHealRevealLead, 0f, 0.12f);

        layerFinalRevealDelay =
            Mathf.Max(0f, layerFinalRevealDelay);

        layerHealFormFrontMultiplier =
            Mathf.Clamp01(layerHealFormFrontMultiplier);

        layerRevealPadding =
            Mathf.Clamp(layerRevealPadding, 0f, 0.08f);

        settleScaleResponse =
            Mathf.Clamp01(settleScaleResponse);

        settleVisualResponse =
            Mathf.Clamp01(settleVisualResponse);

        completedLiftGlideDurationMultiplier =
            Mathf.Clamp(completedLiftGlideDurationMultiplier, 1f, 2f);

        fragmentMergeStart =
            Mathf.Max(finalCrystalStart, fragmentMergeStart);

        fragmentEndScale =
            Mathf.Max(0.01f, fragmentEndScale);

        completedLiftDistance =
            Mathf.Max(0f, completedLiftDistance);

        completedLiftDuration =
            Mathf.Max(0.01f, completedLiftDuration);

        completedLiftOvershoot =
            Mathf.Max(0f, completedLiftOvershoot);

        floatAmplitude =
            Mathf.Max(0f, floatAmplitude);

        floatFrequency =
            Mathf.Max(0f, floatFrequency);

        secondaryFloatAmplitude =
            Mathf.Max(0f, secondaryFloatAmplitude);

        secondaryFloatFrequencyMultiplier =
            Mathf.Max(0f, secondaryFloatFrequencyMultiplier);

        lateralFloatAmplitude =
            Mathf.Max(0f, lateralFloatAmplitude);

        lateralFloatFrequencyMultiplier =
            Mathf.Max(0f, lateralFloatFrequencyMultiplier);

        swayFrequencyMultiplier =
            Mathf.Max(0f, swayFrequencyMultiplier);
        shardPreSpawnDuration =
            Mathf.Max(0.01f, shardPreSpawnDuration);

        shardPreSpawnStartScale =
            Mathf.Clamp(shardPreSpawnStartScale, 0.5f, 1f);

        shardPreSpawnDriftAmount =
            Mathf.Clamp(shardPreSpawnDriftAmount, 0f, 0.05f);            
    }

    /// <summary>
    /// 既存APIを維持する。
    /// finalCrystalTransform は表示メッシュではなく、完成位置・スケールの基準として使う。
    /// 完成時の向きは、生成地点の回転とPrefabに保存した向きを維持する。
    /// </summary>
    public void Begin(
        Transform centerPoint,
        Transform finalCrystalTransform)
    {
        RestoreLegacyRendererState();

        center = centerPoint;
        legacyFinalTarget = finalCrystalTransform;
        
        IsComplete = false;
        IsLeadLiftComplete = false;
        FloatSignal = 0f;
        CurrentFloatOffset = 0f;
        floatElapsedTime = 0f;

        hasCompletedLiftTarget = false;
        completedLiftTargetPosition = Vector3.zero;
        completedLiftRotationOffset =
            Quaternion.identity;
        completedLiftScaleMultiplier = 1f;

        if (completedLiftCoroutine != null)
        {
            StopCoroutine(completedLiftCoroutine);
            completedLiftCoroutine = null;
        }

        initialized = false;
        fallbackMode = false;
        floating = false;
        floatTarget = null;

        anchorFormationComplete = false;
        remainingAssemblyStarted = false;
        anchorFragment = null;
        hasAnchorBounds = false;

        if (center == null ||
            legacyFinalTarget == null)
        {
            Debug.LogError(
                $"{name}: BeginにはcenterPointとfinalCrystalTransformの両方が必要です。",
                this);
            return;
        }

        CaptureLegacyRendererState();

        if (TryPrepareEmbeddedAssembly())
        {
            HideLegacyFinal();
            fallbackMode = false;
        }
        else
        {
            PrepareLegacyFallback();
            fallbackMode = true;

            Debug.LogWarning(
                $"{name}: 16片モデルの参照が未設定です。旧Crystalを使うフォールバックで再生します。",
                this);
        }

        initialized = true;

        if (!fallbackMode)
        {
            SetFragmentsWaitingForAnchor();
        }
        else
        {
            SetProgress(0f);
        }
    }

    public bool HasAnchorFragment =>
        !fallbackMode &&
        anchorFragment != null;

    public Vector3 AnchorWorldPosition =>
        anchorFragment != null
            ? anchorFragment.TargetPosition
            : (legacyFinalTarget != null
                ? legacyFinalTarget.position
                : transform.position);

    public void SetAnchorProgress(float progress)
    {
        if (!initialized ||
            fallbackMode ||
            anchorFragment == null)
        {
            return;
        }

        float t =
            Mathf.Clamp01(progress);

        for (int i = 0; i < fragments.Count; i++)
        {
            FragmentState state = fragments[i];

            if (!state.IsAnchor)
            {
                state.Transform.gameObject.SetActive(false);
                continue;
            }

            state.Transform.gameObject.SetActive(true);

            state.Transform.position =
                state.TargetPosition;

            state.Transform.rotation =
                state.TargetRotation;

            float eased =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    t);

            state.Transform.localScale =
                state.TargetScale *
                Mathf.Lerp(
                    anchorStartScale,
                    1f,
                    eased);

            SetFormProgress(
                state.Renderers,
                eased);

            ApplyFragmentVisual(
                state,
                Mathf.Sin(eased * Mathf.PI) * 0.35f);
        }
    }

    public void CompleteAnchorFormation()
    {
        if (!initialized ||
            fallbackMode ||
            anchorFragment == null)
        {
            anchorFormationComplete = true;
            return;
        }

        anchorFragment.Transform.gameObject.SetActive(true);
        anchorFragment.Transform.position =
            anchorFragment.TargetPosition;
        anchorFragment.Transform.rotation =
            anchorFragment.TargetRotation;
        anchorFragment.Transform.localScale =
            anchorFragment.TargetScale;

        SetFormProgress(
            anchorFragment.Renderers,
            1f);

        ApplyFragmentVisual(
            anchorFragment,
            0f,
            1f);

        anchorFormationComplete = true;
    }

    public void BeginRemainingAssembly()
    {
        if (!initialized ||
            fallbackMode)
        {
            return;
        }

        if (!anchorFormationComplete)
        {
            CompleteAnchorFormation();
        }

        remainingAssemblyStarted = true;

        for (int i = 0; i < fragments.Count; i++)
        {
            FragmentState state = fragments[i];

            if (state.IsAnchor)
            {
                continue;
            }

            state.Transform.position =
                state.StartPosition;

            state.Transform.rotation =
                state.StartRotation;

            state.Transform.localScale =
                state.StartScale;

            state.Transform.gameObject.SetActive(true);

            SetFormProgress(
                state.Renderers,
                1f);

            ApplyFragmentVisual(
                state,
                0f);
        }
    }

    public Vector3 GetAnchorGatherPoint(
        int index,
        int totalCount)
    {
        if (!hasAnchorBounds)
        {
            return AnchorWorldPosition;
        }

        int safeTotal =
            Mathf.Max(
                1,
                totalCount);

        float u =
            Mathf.Repeat(
                (index + 0.5f) *
                0.61803398875f,
                1f);

        float v =
            Mathf.Repeat(
                (index + 0.5f) *
                0.754877666f,
                1f);

        float w =
            Mathf.Repeat(
                (index + 0.5f) *
                0.569840296f,
                1f);

        Vector3 normalized =
            new Vector3(
                u - 0.5f,
                v - 0.5f,
                w - 0.5f) *
            anchorGatherBoundsFill;

        Vector3 extent =
            anchorBounds.extents;

        return
            anchorBounds.center +
            Vector3.Scale(
                normalized * 2f,
                extent);
    }

    public IEnumerator PlayRemainingAssembly()
    {
        if (!initialized)
        {
            yield break;
        }

        if (!remainingAssemblyStarted &&
            !fallbackMode)
        {
            BeginRemainingAssembly();
        }

        float duration =
            Mathf.Max(
                0.01f,
                runtimeAssemblyTotalDuration > 0f
                    ? runtimeAssemblyTotalDuration
                    : 0.8f);

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            SetProgress(
                Mathf.Clamp01(
                    elapsed /
                    duration));

            yield return null;
        }

        SetProgress(1f);
    }

    public float RemainingAssemblyDuration =>
        runtimeAssemblyTotalDuration;

    public void SetProgress(float progress)
    {
        if (!initialized)
        {
            return;
        }

        float t =
            Mathf.Clamp01(progress);

        if (fallbackMode)
        {
            UpdateLegacyFallback(t);
            return;
        }

        if (!remainingAssemblyStarted)
        {
            return;
        }

        UpdateFragments(t);
        UpdateEmbeddedFinal(t);
    }

    public void CompleteAssembly(
        bool startCompletedLift = true)
    {
        if (!initialized)
        {
            IsComplete = true;
            IsLeadLiftComplete = true;
            return;
        }

        if (fallbackMode)
        {
            SetLegacyVisibility(true);
            SetFormProgress(legacyRenderers, 1f);

            floatTarget = legacyFinalTarget;
            floatBasePosition = legacyFinalTarget.position;
            floatBaseRotation = legacyFinalTarget.rotation;
        }
        else
        {
            assemblyVisualRoot.position =
                floatBasePosition;

            assemblyVisualRoot.rotation =
                floatBaseRotation;

            embeddedFinalCrystal.gameObject.SetActive(true);
            SetFormProgress(embeddedFinalRenderers, 1f);
            SetEmbeddedFinalFormFrontMultiplier(1f);

            for (int i = 0; i < fragments.Count; i++)
            {
                FragmentState state = fragments[i];

                if (state.Transform != null)
                {
                    state.Transform.gameObject.SetActive(false);
                }
            }
        }

        IsComplete = true;
        floating = false;
        IsLeadLiftComplete = false;

        if (completedLiftCoroutine != null)
        {
            StopCoroutine(completedLiftCoroutine);
            completedLiftCoroutine = null;
        }

        if (startCompletedLift)
        {
            StartCompletedLift();
        }
    }

    /// <summary>
    /// 完成済みCrystalの先行上昇を開始する。
    /// CrystalEffectControllerから中央→四隅の順番を制御するため、
    /// CompleteAssemblyとは分離して呼べるようにしている。
    /// </summary>
    /// <summary>
    /// CrystalEffectController から直接 await できる完成後Lift。
    /// 外部のrequest flagを介さず、このController自身がTargetまで移動して完了を返す。
    /// </summary>
    public IEnumerator PlayCompletedLiftTo(
        Transform liftTarget)
    {
        if (!IsComplete)
        {
            Debug.LogWarning(
                $"[CrystalLift] {name}: Assembly未完了のためLiftを開始できません。",
                this);
            yield break;
        }

        if (liftTarget == null)
        {
            Debug.LogWarning(
                $"[CrystalLift] {name}: Lift Targetがnullです。fallback distanceを使用します。",
                this);

            hasCompletedLiftTarget = false;
        }
        else
        {
            hasCompletedLiftTarget = true;
            completedLiftTargetPosition =
                liftTarget.position;

            completedLiftRotationOffset =
                Quaternion.identity;
            completedLiftScaleMultiplier = 1f;
            CrystalFinalPose finalPose =
                liftTarget.GetComponent<CrystalFinalPose>();

            if (finalPose != null)
            {
                completedLiftRotationOffset =
                    finalPose.RotationOffset;
                completedLiftScaleMultiplier =
                    finalPose.ScaleMultiplier;

            }
        }

        if (completedLiftCoroutine != null)
        {
            StopCoroutine(
                completedLiftCoroutine);

            completedLiftCoroutine = null;
        }

        floating = false;
        IsLeadLiftComplete = false;

        Debug.Log(
            $"[CrystalLift] {name}: DIRECT START " +
            $"target={(liftTarget != null ? liftTarget.name : "fallback")} " +
            $"world={(liftTarget != null ? liftTarget.position.ToString("F3") : "-")}",
            this);

        yield return
            PlayCompletedLiftThenFloat();
    }

    public void StartCompletedLift()
    {
        hasCompletedLiftTarget = false;
        StartCompletedLiftInternal();
    }

    /// <summary>
    /// 完成したCrystalを指定したWorld Positionまで移動させる。
    /// 5本の最終配置はCrystalEffectController側のLift Targetで決める。
    /// </summary>
    public void StartCompletedLift(
        Vector3 targetWorldPosition)
    {
        hasCompletedLiftTarget = true;
        completedLiftTargetPosition =
            targetWorldPosition;

        StartCompletedLiftInternal();
    }

    private void StartCompletedLiftInternal()
    {
        if (!IsComplete)
        {
            Debug.LogWarning(
                $"[CrystalLift] {name}: StartCompletedLift ignored because IsComplete=false.",
                this);
            return;
        }

        if (IsLeadLiftComplete ||
            completedLiftCoroutine != null)
        {
            Debug.LogWarning(
                $"[CrystalLift] {name}: StartCompletedLift ignored. " +
                $"IsLeadLiftComplete={IsLeadLiftComplete}, " +
                $"CoroutineActive={completedLiftCoroutine != null}.",
                this);
            return;
        }

        floating = false;
        IsLeadLiftComplete = false;

        completedLiftCoroutine =
            StartCoroutine(
                PlayCompletedLiftThenFloat());
    }

    private Vector3 GetCompletedCrystalVisualAnchor()
    {
        //
        // 本番16片モデル:
        // Final CrystalのRenderer Bounds中心を「見た目の位置」とする。
        //
        if (!fallbackMode &&
            embeddedFinalRenderers != null &&
            embeddedFinalRenderers.Length > 0 &&
            TryCalculateRendererBounds(
                embeddedFinalRenderers,
                out Bounds embeddedBounds))
        {
            return embeddedBounds.center;
        }

        //
        // Fallback:
        // 旧Crystal側もRenderer Bounds中心を優先。
        //
        if (legacyRenderers != null &&
            legacyRenderers.Length > 0 &&
            TryCalculateRendererBounds(
                legacyRenderers,
                out Bounds legacyBounds))
        {
            return legacyBounds.center;
        }

        //
        // Rendererが取れない場合のみTransform Pivotへフォールバック。
        //
        if (floatTarget != null)
        {
            return floatTarget.position;
        }

        return transform.position;
    }

    private IEnumerator PlayCompletedLiftThenFloat()
    {
        if (floatTarget == null ||
            center == null)
        {
            IsLeadLiftComplete = true;
            floating = true;
            completedLiftCoroutine = null;
            yield break;
        }

        Vector3 axis =
            SafeNormalized(
                center.up,
                Vector3.up);

        Vector3 startPosition =
            floatTarget.position;
        
        Quaternion startRotation =
            floatTarget.rotation;

        Vector3 startScale =
            floatTarget.localScale;
        
        Quaternion targetRotation =
            startRotation *
            completedLiftRotationOffset;

        Vector3 targetScale =
            startScale *
            completedLiftScaleMultiplier;
        
        Vector3 startVisualAnchor =
            GetCompletedCrystalVisualAnchor();

        Vector3 targetPosition;

        if (hasCompletedLiftTarget)
        {
            //
            // Lift Target は「Assembly RootのPivot位置」ではなく、
            // Scene上で見た完成Crystalそのものの到着位置として扱う。
            //
            // Assembly RootはFBX/Prefab内部のPivotオフセットを持つため、
            // root.position = target とすると見た目のCrystalがTargetへ来ない。
            // 現在の完成Crystalの見た目中心との差分だけRootを移動する。
            //
            Vector3 currentVisualAnchor =
                GetCompletedCrystalVisualAnchor();

            Vector3 visualDelta =
                completedLiftTargetPosition -
                currentVisualAnchor;

            targetPosition =
                startPosition +
                visualDelta;

            Debug.Log(
                $"[CrystalLift] {name} " +
                $"visualStart={currentVisualAnchor:F3}, " +
                $"target={completedLiftTargetPosition:F3}, " +
                $"rootStart={startPosition:F3}, " +
                $"rootTarget={targetPosition:F3}",
                this);
        }
        else
        {
            targetPosition =
                startPosition +
                axis *
                completedLiftDistance;
        }

        Vector3 travelDirection =
            targetPosition -
            startPosition;

        Vector3 overshootDirection =
            travelDirection.sqrMagnitude > 0.000001f
                ? travelDirection.normalized
                : axis;

        float duration =
            Mathf.Max(
                0.01f,
                completedLiftDuration *
                (hasCompletedLiftTarget
                    ? completedLiftGlideDurationMultiplier
                    : 1f));

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed +=
                Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    duration);

            //
            // ゆっくり立ち上がって最後は静かに止まる。
            // 中盤だけ少し目標位置を越えて、単純Lerp感を消す。
            //
            //
            // Cosine Ease-In-Out:
            // 最初から急に加速せず、中央も突っ走りすぎず、
            // 最後まで「スーッ」と流れる上昇にする。
            //
            float eased =
                0.5f -
                0.5f *
                Mathf.Cos(
                    Mathf.PI *
                    t);

            //
            // Scene上のLift Targetへ向かう場合は
            // overshootを切って「キュッ」と跳ねる印象を消す。
            //
            float overshoot =
                hasCompletedLiftTarget
                    ? 0f
                    : Mathf.Sin(
                        t *
                        Mathf.PI) *
                      completedLiftOvershoot;

            floatTarget.position =
                Vector3.Lerp(
                    startPosition,
                    targetPosition,
                    eased) +
                overshootDirection *
                overshoot;

            floatTarget.rotation =
                Quaternion.Slerp(
                    startRotation,
                    targetRotation,
                    eased);
            floatTarget.localScale =
                Vector3.Lerp(
                    startScale,
                    targetScale,
                    eased);
            
            if (hasCompletedLiftTarget)
            {
                Vector3 desiredVisualAnchor =
                    Vector3.Lerp(
                        startVisualAnchor,
                        completedLiftTargetPosition,
                        eased);
                Vector3 currentVisualAnchor =
                    GetCompletedCrystalVisualAnchor();
                floatTarget.position +=
                    desiredVisualAnchor -
                    currentVisualAnchor;
            }

            yield return null;
        }

        if (hasCompletedLiftTarget)
        {
            floatTarget.position =
                targetPosition;
            floatTarget.localScale =
                targetScale;
            Vector3 currentVisualAnchor =
                GetCompletedCrystalVisualAnchor();
            floatTarget.position +=
                completedLiftTargetPosition -
                currentVisualAnchor;
        }
        else
        {
            floatTarget.position =
                targetPosition;
            floatTarget.rotation =
                targetRotation;
            floatTarget.localScale =
                targetScale;
        }

        //
        // ここから先の浮遊中心は、
        // 「紙より先に上がった位置」そのもの。
        //
        floatBasePosition =
            floatTarget.position;

        floatBaseRotation =
            targetRotation;

        floatElapsedTime = 0f;
        FloatSignal = 0f;
        CurrentFloatOffset = 0f;

        IsLeadLiftComplete = true;
        floating = true;
        completedLiftCoroutine = null;
    }

    private void LateUpdate()
    {
        if (!floating ||
            center == null ||
            floatTarget == null)
        {
            return;
        }

        floatElapsedTime +=
            Time.deltaTime;

        float primaryTime =
            floatElapsedTime *
            floatFrequency *
            Mathf.PI *
            2f +
            floatPhaseOffset;

        float secondaryTime =
            floatElapsedTime *
            floatFrequency *
            secondaryFloatFrequencyMultiplier *
            Mathf.PI *
            2f +
            secondaryFloatPhase;

        float lateralTime =
            floatElapsedTime *
            floatFrequency *
            lateralFloatFrequencyMultiplier *
            Mathf.PI *
            2f +
            lateralFloatPhase;

        float primaryWave =
            Mathf.Sin(
                primaryTime);

        float secondaryWave =
            Mathf.Sin(
                secondaryTime);

        float lateralWave =
            Mathf.Sin(
                lateralTime);

        FloatSignal =
            primaryWave;

        CurrentFloatOffset =
            primaryWave *
            floatAmplitude +
            secondaryWave *
            secondaryFloatAmplitude;

        Vector3 axis =
            SafeNormalized(
                center.up,
                Vector3.up);

        Vector3 lateralAxis =
            SafeNormalized(
                center.right,
                Vector3.right);

        Vector3 swayAxis =
            SafeNormalized(
                center.forward,
                Vector3.forward);

        floatTarget.position =
            floatBasePosition +
            axis *
            CurrentFloatOffset +
            lateralAxis *
            lateralWave *
            lateralFloatAmplitude;

        float swayWave =
            Mathf.Sin(
                floatElapsedTime *
                floatFrequency *
                swayFrequencyMultiplier *
                Mathf.PI *
                2f +
                floatPhaseOffset +
                0.85f);

        floatTarget.rotation =
            Quaternion.AngleAxis(
                swayWave *
                swayDegrees,
                swayAxis) *
            floatBaseRotation;
    }

    private void OnDestroy()
    {
        RestoreLegacyRendererState();
    }

    private bool TryPrepareEmbeddedAssembly()
    {
        if (!CacheAuthoredHierarchy() ||
            fragments.Count != 16)
        {
            return false;
        }

        assemblyVisualRoot.gameObject.SetActive(true);
        RestoreAuthoredHierarchy();

        embeddedFinalRenderers =
            embeddedFinalCrystal.GetComponentsInChildren<Renderer>(true);

        CaptureEmbeddedFinalFormFrontStrength();

        // Renderer.boundsを正しく取得できる状態で、旧Crystalへ見た目を合わせる。
        embeddedFinalCrystal.gameObject.SetActive(true);
        AlignAssemblyToLegacyTarget();

        for (int i = 0; i < fragments.Count; i++)
        {
            FragmentState state = fragments[i];

            state.Transform.gameObject.SetActive(true);
            SetFormProgress(state.Renderers, 1f);
            ApplyFragmentVisual(state, 0f);

            state.TargetPosition =
                state.Transform.position;

            state.TargetRotation =
                state.Transform.rotation;

            // 完成時はBlenderで作った値へ完全一致させる。
            state.TargetScale =
                state.AuthoredLocalScale;
        }

        anchorFragment =
            FindAnchorFragment();

        if (anchorFragment != null)
        {
            anchorFragment.IsAnchor = true;

            hasAnchorBounds =
                TryCalculateRendererBounds(
                    anchorFragment.Renderers,
                    out anchorBounds);
        }
        else
        {
            Debug.LogWarning(
                $"{name}: Anchor Fragment '{anchorFragmentName}' が見つかりません。Dust→Lowerの受け渡しは無効になります。",
                this);
        }

        BuildFragmentAnimationData();

        embeddedFinalCrystal.gameObject.SetActive(false);
        SetFormProgress(embeddedFinalRenderers, 0f);

        floatTarget = assemblyVisualRoot;
        floatBasePosition = assemblyVisualRoot.position;
        floatBaseRotation = assemblyVisualRoot.rotation;

        return true;
    }

    private bool CacheAuthoredHierarchy()
    {
        if (hierarchyCached)
        {
            return true;
        }

        if (assemblyVisualRoot == null ||
            piecesRoot == null ||
            embeddedFinalCrystal == null)
        {
            return false;
        }

        if (!embeddedFinalCrystal.IsChildOf(piecesRoot))
        {
            return false;
        }

        assemblyVisualAuthoredLocalRotation =
            assemblyVisualRoot.localRotation;

        embeddedFinalAuthoredLocalPosition =
            embeddedFinalCrystal.localPosition;

        embeddedFinalAuthoredLocalRotation =
            embeddedFinalCrystal.localRotation;

        embeddedFinalAuthoredLocalScale =
            embeddedFinalCrystal.localScale;

        fragments.Clear();

        for (int i = 0; i < piecesRoot.childCount; i++)
        {
            Transform child =
                piecesRoot.GetChild(i);

            if (child == embeddedFinalCrystal)
            {
                continue;
            }

            Renderer[] renderers =
                child.GetComponentsInChildren<Renderer>(true);

            if (renderers.Length == 0)
            {
                continue;
            }

            int assemblyLayer =
                TryParseAssemblyLayer(
                    child.name,
                    out int parsedLayer)
                    ? parsedLayer
                    : -1;


            fragments.Add(
                new FragmentState
                {
                    Transform = child,
                    Renderers = renderers,
                    RendererStates = BuildFragmentRendererStates(renderers),
                    AuthoredLocalPosition = child.localPosition,
                    AuthoredLocalRotation = child.localRotation,
                    AuthoredLocalScale = child.localScale,
                    AssemblyLayer = assemblyLayer,
                    IsAnchor = string.Equals(
                        child.name,
                        anchorFragmentName,
                        StringComparison.OrdinalIgnoreCase)
                });
        }

        if (fragments.Count != 16)
        {
            fragments.Clear();
            return false;
        }

        hierarchyCached = true;
        return true;
    }

    private void RestoreAuthoredHierarchy()
    {
        embeddedFinalCrystal.localPosition =
            embeddedFinalAuthoredLocalPosition;

        embeddedFinalCrystal.localRotation =
            embeddedFinalAuthoredLocalRotation;

        embeddedFinalCrystal.localScale =
            embeddedFinalAuthoredLocalScale;

        for (int i = 0; i < fragments.Count; i++)
        {
            FragmentState state = fragments[i];

            state.Transform.localPosition =
                state.AuthoredLocalPosition;

            state.Transform.localRotation =
                state.AuthoredLocalRotation;

            state.Transform.localScale =
                state.AuthoredLocalScale;

            state.Transform.gameObject.SetActive(true);
        }
    }

    private void SetAssemblyVisualsDormant()
    {
        if (embeddedFinalCrystal != null)
        {
            embeddedFinalCrystal.gameObject.SetActive(false);
        }

        for (int i = 0; i < fragments.Count; i++)
        {
            if (fragments[i].Transform != null)
            {
                fragments[i].Transform.gameObject.SetActive(false);
            }
        }
    }

    private void AlignAssemblyToLegacyTarget()
    {
        Debug.Log(
            $"[CrystalAssemblyScale] Begin root={assemblyVisualRoot.name}, " +
            $"matchBounds={matchLegacyRendererBounds}, " +
            $"multiplier={visualSizeMultiplier}, " +
            $"before={assemblyVisualRoot.localScale}",
            this);

        // 外側のControllerはbirthPoint.rotationで生成されている。
        // 旧CrystalとFBXではメッシュ軸が異なるため、旧Transformの
        // rotationはコピーせず、Prefabに保存したローカル回転を維持する。
        assemblyVisualRoot.localRotation =
            Quaternion.Euler(
                visualRotationCorrectionEuler) *
            assemblyVisualAuthoredLocalRotation;

        Vector3 finalPositionInRoot =
            assemblyVisualRoot.InverseTransformPoint(
                embeddedFinalCrystal.position);

        Vector3 finalScaleInRoot =
            DivideComponents(
                embeddedFinalCrystal.lossyScale,
                assemblyVisualRoot.lossyScale);

        Vector3 desiredRootWorldScale =
            DivideComponents(
                legacyFinalTarget.lossyScale,
                finalScaleInRoot);

        if (matchLegacyRendererBounds)
        {
            SetWorldScale(
                assemblyVisualRoot,
                desiredRootWorldScale);

            assemblyVisualRoot.position =
                legacyFinalTarget.position -
                assemblyVisualRoot.TransformVector(finalPositionInRoot);

            MatchLegacyRendererBounds();

            Debug.Log(
                $"[CrystalAssemblyScale] Applied mode=Bounds, " +
                $"after={assemblyVisualRoot.localScale}",
                this);

            return;
        }

        // Bounds一致を使わない場合は、Prefabに保存されている親Scaleを
        // 掛け合わせず、Inspectorの値を全体の絶対Scaleとして適用する。
        // これにより、保存Scaleが大きくても倍率が相殺されない。
        assemblyVisualRoot.localScale =
            Vector3.one * visualSizeMultiplier;

        Debug.Log(
            $"[CrystalAssemblyScale] Applied mode=Manual, " +
            $"after={assemblyVisualRoot.localScale}",
            this);

        assemblyVisualRoot.position =
            legacyFinalTarget.position -
            assemblyVisualRoot.TransformVector(finalPositionInRoot);

        assemblyVisualRoot.position +=
            CenterSpaceToWorldDirection(
                visualOffsetInCenterSpace);
    }

    private void MatchLegacyRendererBounds()
    {
        bool hasLegacyBounds =
            TryCalculateRendererBounds(
                legacyRenderers,
                out Bounds legacyBounds);

        bool hasEmbeddedBounds =
            TryCalculateRendererBounds(
                embeddedFinalRenderers,
                out Bounds embeddedBounds);

        float sizeMultiplier =
            visualSizeMultiplier;

        if (matchLegacyRendererBounds &&
            hasLegacyBounds &&
            hasEmbeddedBounds)
        {
            Vector3 axis =
                SafeNormalized(center.up, Vector3.up);

            float legacyHeight =
                ProjectedExtent(
                    legacyBounds.extents,
                    axis) *
                2f;

            float embeddedHeight =
                ProjectedExtent(
                    embeddedBounds.extents,
                    axis) *
                2f;

            if (legacyHeight > 0.0001f &&
                embeddedHeight > 0.0001f)
            {
                sizeMultiplier *=
                    legacyHeight /
                    embeddedHeight;
            }
        }

        assemblyVisualRoot.localScale *=
            sizeMultiplier;

        // Scale変更後のBoundsを再取得し、見た目の中心同士を一致させる。
        if (matchLegacyRendererBounds &&
            hasLegacyBounds &&
            TryCalculateRendererBounds(
                embeddedFinalRenderers,
                out embeddedBounds))
        {
            assemblyVisualRoot.position +=
                legacyBounds.center -
                embeddedBounds.center;
        }

        assemblyVisualRoot.position +=
            CenterSpaceToWorldDirection(
                visualOffsetInCenterSpace);
    }

    private void BuildFragmentAnimationData()
    {
        Bounds bounds =
            CalculateFragmentBounds();

        Vector3 axis =
            SafeNormalized(center.up, Vector3.up);

        Vector3 basisX =
            Vector3.ProjectOnPlane(center.right, axis);

        basisX =
            SafeNormalized(basisX, Vector3.right);

        Vector3 basisZ =
            Vector3.Cross(axis, basisX);

        basisZ =
            SafeNormalized(basisZ, Vector3.forward);

        float minimumHeight =
            float.PositiveInfinity;

        float maximumHeight =
            float.NegativeInfinity;

        for (int i = 0; i < fragments.Count; i++)
        {
            FragmentState state = fragments[i];

            state.TargetHeight =
                Vector3.Dot(
                    state.TargetPosition - bounds.center,
                    axis);

            minimumHeight =
                Mathf.Min(
                    minimumHeight,
                    state.TargetHeight);

            maximumHeight =
                Mathf.Max(
                    maximumHeight,
                    state.TargetHeight);
        }

        //
        // まず物理的な高さ順を保持する。
        // Scatterの高さや、名前解析失敗時のフォールバックに使う。
        //
        for (int i = 0; i < fragments.Count; i++)
        {
            FragmentState state = fragments[i];

            state.HeightOrder =
                Mathf.Abs(
                    maximumHeight -
                    minimumHeight) < 0.0001f
                    ? 0f
                    : Mathf.InverseLerp(
                        minimumHeight,
                        maximumHeight,
                        state.TargetHeight);
        }

        //
        // Final Crystalは_FormProgressで下→上へ表示する。
        // Layer Healの到達点を正確にするため、
        // 各ShardのRenderer Bounds上端を0～1へ変換して保持する。
        //
        float overallBottom =
            Vector3.Dot(
                bounds.center,
                axis) -
            ProjectedExtent(
                bounds.extents,
                axis);

        float overallTop =
            Vector3.Dot(
                bounds.center,
                axis) +
            ProjectedExtent(
                bounds.extents,
                axis);

        float overallHeight =
            Mathf.Max(
                0.0001f,
                overallTop -
                overallBottom);

        for (int i = 0; i < fragments.Count; i++)
        {
            FragmentState state =
                fragments[i];

            float fragmentTop =
                Vector3.Dot(
                    state.TargetPosition,
                    axis);

            bool hasRendererBounds = false;

            if (state.Renderers != null)
            {
                for (int r = 0;
                     r < state.Renderers.Length;
                     r++)
                {
                    Renderer renderer =
                        state.Renderers[r];

                    if (renderer == null)
                    {
                        continue;
                    }

                    Bounds rendererBounds =
                        renderer.bounds;

                    float rendererTop =
                        Vector3.Dot(
                            rendererBounds.center,
                            axis) +
                        ProjectedExtent(
                            rendererBounds.extents,
                            axis);

                    fragmentTop =
                        hasRendererBounds
                            ? Mathf.Max(
                                fragmentTop,
                                rendererTop)
                            : rendererTop;

                    hasRendererBounds = true;
                }
            }

            state.RevealTopProgress =
                Mathf.Clamp01(
                    (
                        fragmentTop -
                        overallBottom
                    ) /
                    overallHeight);
        }

        usingAuthoredLayerOrder =
            useAuthoredLayerOrder &&
            CanUseAuthoredLayerOrder();

        List<int> layerNumbers =
            new List<int>();

        if (usingAuthoredLayerOrder)
        {
            for (int i = 0; i < fragments.Count; i++)
            {
                FragmentState state =
                    fragments[i];

                if (state.IsAnchor)
                {
                    state.LayerOrder = 0f;
                    state.WithinLayerOrder = 0f;
                    continue;
                }

                int layer =
                    state.AssemblyLayer;

                if (!layerNumbers.Contains(layer))
                {
                    layerNumbers.Add(layer);
                }
            }

            layerNumbers.Sort();

            fragments.Sort(
                (a, b) =>
                {
                    int layerCompare =
                        a.AssemblyLayer.CompareTo(
                            b.AssemblyLayer);

                    if (layerCompare != 0)
                    {
                        return layerCompare;
                    }

                    return string.CompareOrdinal(
                        a.Transform.name,
                        b.Transform.name);
                });

            for (int layerIndex = 0;
                 layerIndex < layerNumbers.Count;
                 layerIndex++)
            {
                int layerNumber =
                    layerNumbers[layerIndex];

                List<FragmentState> layerFragments =
                    new List<FragmentState>();

                for (int i = 0; i < fragments.Count; i++)
                {
                    if (!fragments[i].IsAnchor &&
                        fragments[i].AssemblyLayer ==
                        layerNumber)
                    {
                        layerFragments.Add(
                            fragments[i]);
                    }
                }

                float layerOrder =
                    layerNumbers.Count <= 1
                        ? 0f
                        : layerIndex /
                          (float)(
                              layerNumbers.Count - 1);

                for (int i = 0;
                     i < layerFragments.Count;
                     i++)
                {
                    FragmentState state =
                        layerFragments[i];

                    state.LayerOrder =
                        layerOrder;

                    state.WithinLayerOrder =
                        layerFragments.Count <= 1
                            ? 0.5f
                            : i /
                              (float)(
                                  layerFragments.Count - 1);
                }
            }
        }
        else
        {
            fragments.Sort(
                (a, b) =>
                    a.TargetHeight.CompareTo(
                        b.TargetHeight));

            int count =
                fragments.Count;

            for (int i = 0; i < count; i++)
            {
                FragmentState state =
                    fragments[i];

                float order =
                    count <= 1
                        ? 0f
                        : i /
                          (float)(
                              count - 1);

                if (state.IsAnchor)
                {
                    state.LayerOrder = 0f;
                    state.WithinLayerOrder = 0f;
                }
                else
                {
                    state.LayerOrder =
                        order;

                    state.WithinLayerOrder =
                        0.5f;
                }
            }
        }

        float verticalExtent =
            Mathf.Max(
                0.08f,
                ProjectedExtent(
                    bounds.extents,
                    axis));

        float horizontalExtent =
            Mathf.Max(
                0.04f,
                Mathf.Max(
                    ProjectedExtent(
                        bounds.extents,
                        basisX),
                    ProjectedExtent(
                        bounds.extents,
                        basisZ)));

        float startRadius =
            horizontalExtent *
            startRadiusMultiplier;

        float startHeightSpread =
            Mathf.Max(
                0.06f,
                verticalExtent *
                startHeightMultiplier);

        int movingFragmentCount = 0;

        for (int i = 0;
            i < fragments.Count;
            i++)
        {
            if (!fragments[i].IsAnchor)
            {
                movingFragmentCount++;
            }
        }

        bool usingAuthoredGrowthOrder =
            TryAssignAuthoredGrowthSequence();

        //
        // Growth Orderを使えなかった場合のみ、
        // これまでのLayer順をそのままSequenceとして使う。
        //
        if (!usingAuthoredGrowthOrder)
        {
            int sequenceIndex = 0;

            for (int i = 0;
                i < fragments.Count;
                i++)
            {
                FragmentState state =
                    fragments[i];

                if (state.IsAnchor)
                {
                    state.SequenceIndex = -1;
                    continue;
                }

                state.SequenceIndex =
                    sequenceIndex;

                sequenceIndex++;
            }
        }

        //
        // ここからはProgress幅ではなく、実時間で設計する。
        //
        runtimeAssemblyMoveDuration =
            movingFragmentCount <= 0
                ? 0f
                : shardTravelDuration +
                  Mathf.Max(
                      0,
                      movingFragmentCount - 1) *
                  shardStartInterval;

        runtimeAssemblyTotalDuration =
            Mathf.Max(
                0.01f,
                runtimeAssemblyMoveDuration +
                postAssemblyHoldDuration +
                finalBlendDuration);

        runtimeFinalCrystalStart =
            Mathf.Clamp01(
                (
                    runtimeAssemblyMoveDuration +
                    postAssemblyHoldDuration
                ) /
                runtimeAssemblyTotalDuration);

        runtimeFragmentMergeStart =
            runtimeFinalCrystalStart;

        System.Random random =
            new System.Random(
                BuildStableSeed(
                    legacyFinalTarget.position));

        int fragmentCount =
            fragments.Count;

        for (int i = 0;
             i < fragmentCount;
             i++)
        {
            FragmentState state =
                fragments[i];

            if (state.IsAnchor)
            {
                state.StartPosition =
                    state.TargetPosition;

                state.StartRotation =
                    state.TargetRotation;

                state.StartScale =
                    state.TargetScale *
                    anchorStartScale;

                state.StartProgress = 0f;
                state.EndProgress = 0f;

                continue;
            }

            //
            // Scatter自体は物理的な高さを使う。
            // Layer設計は「組み立て開始順」にだけ効かせる。
            //
            float heightOrder =
                state.HeightOrder;

            float angle =
                i * 137.507764f +
                NextFloat(
                    random,
                    -22f,
                    22f);

            float angleRadians =
                angle *
                Mathf.Deg2Rad;

            Vector3 radialDirection =
                basisX *
                Mathf.Cos(
                    angleRadians) +
                basisZ *
                Mathf.Sin(
                    angleRadians);

            float radialDistance =
                NextFloat(
                    random,
                    startRadius * 0.72f,
                    startRadius * 1.18f);

            float heightOffset =
                Mathf.Lerp(
                    -startHeightSpread * 0.30f,
                    startHeightSpread * 0.65f,
                    heightOrder) +
                NextFloat(
                    random,
                    -startHeightSpread * 0.35f,
                    startHeightSpread * 0.35f);

            state.StartPosition =
                bounds.center +
                radialDirection *
                radialDistance +
                axis *
                heightOffset;

            state.StartRotation =
                Quaternion.Euler(
                    NextFloat(
                        random,
                        0f,
                        360f),
                    NextFloat(
                        random,
                        0f,
                        360f),
                    NextFloat(
                        random,
                        0f,
                        360f));

            float scaleMultiplier =
                NextFloat(
                    random,
                    fragmentScaleRange.x,
                    fragmentScaleRange.y);

            state.StartScale =
                state.TargetScale *
                scaleMultiplier;

            state.CurveDirection =
                Vector3.Cross(
                    axis,
                    state.StartPosition -
                    state.TargetPosition);

            state.CurveDirection =
                SafeNormalized(
                    state.CurveDirection,
                    basisX);

            state.CurveAmount =
                Vector3.Distance(
                    state.StartPosition,
                    state.TargetPosition) *
                0.18f *
                curveAmountMultiplier;

            //
            // 本番Sequence:
            // LowerはすでにDustから形成済み。
            // 残り15片を 0 → 1 → 2 → ... の順で1枚ずつ動かす。
            //
            // 前のShardの移動中でも shardStartInterval 秒後に次を開始し
            // 次のShardを開始するため、全部の面が一気に閉じることはない。
            //
            int sequenceIndex =
                Mathf.Max(
                    0,
                    state.SequenceIndex);

            float startSeconds =
                sequenceIndex *
                shardStartInterval;

            float endSeconds =
                startSeconds +
                shardTravelDuration;

            state.StartProgress =
                Mathf.Clamp01(
                    startSeconds /
                    runtimeAssemblyTotalDuration);

            state.EndProgress =
                Mathf.Clamp01(
                    endSeconds /
                    runtimeAssemblyTotalDuration);

            state.WaitingPhase =
                NextFloat(
                    random,
                    0f,
                    Mathf.PI * 2f);

            Vector3 waitingDirection =
                Vector3.Cross(
                    axis,
                    state.StartPosition -
                    state.TargetPosition);

            state.WaitingDirection =
                SafeNormalized(
                    waitingDirection,
                    basisX);

            state.Transform.position =
                state.StartPosition;

            state.Transform.rotation =
                state.StartRotation;

            state.Transform.localScale =
                state.StartScale;
        }

        floatPhaseOffset =
            NextFloat(
                random,
                -phaseOffsetRange,
                phaseOffsetRange);

        secondaryFloatPhase =
            NextFloat(
                random,
                0f,
                Mathf.PI * 2f);

        lateralFloatPhase =
            NextFloat(
                random,
                0f,
                Mathf.PI * 2f);

        if (usingAuthoredLayerOrder)
        {
            Debug.Log(
                $"[CrystalAssembly] Sequential Assembly: " +
                $"{layerNumbers.Count} layers / " +
                $"{fragments.Count} pieces / " +
                $"{movingFragmentCount} moving shards / " +
                $"travel={shardTravelDuration:F2}s / " +
                $"interval={shardStartInterval:F2}s / " +
                $"move={runtimeAssemblyMoveDuration:F2}s / " +
                $"total={runtimeAssemblyTotalDuration:F2}s",
                this);
        }
        else if (useAuthoredLayerOrder)
        {
            Debug.LogWarning(
                $"{name}: Crystal_LayerXX_... を全16片から解析できなかったため、" +
                "Assembly順は高さ順へフォールバックしました。",
                this);
        }
    }

    private float GetNormalizedSeconds(
        float seconds)
    {
        if (runtimeAssemblyTotalDuration <= 0.0001f)
        {
            return 0f;
        }

        return
            Mathf.Max(0f, seconds) /
            runtimeAssemblyTotalDuration;
    }

    private bool IsSameAssemblyLayer(
        FragmentState a,
        FragmentState b)
    {
        if (a == null ||
            b == null ||
            a.IsAnchor ||
            b.IsAnchor)
        {
            return false;
        }

        if (usingAuthoredLayerOrder &&
            a.AssemblyLayer >= 0 &&
            b.AssemblyLayer >= 0)
        {
            return
                a.AssemblyLayer ==
                b.AssemblyLayer;
        }

        return
            Mathf.Abs(
                a.LayerOrder -
                b.LayerOrder) < 0.0001f;
    }

    private float GetFirstPlayableLayerEndProgress()
    {
        bool found = false;
        int firstAssemblyLayer = int.MaxValue;
        float firstLayerOrder = float.MaxValue;

        for (int i = 0; i < fragments.Count; i++)
        {
            FragmentState state = fragments[i];

            if (state == null ||
                state.IsAnchor)
            {
                continue;
            }

            if (usingAuthoredLayerOrder &&
                state.AssemblyLayer >= 0)
            {
                firstAssemblyLayer =
                    Mathf.Min(
                        firstAssemblyLayer,
                        state.AssemblyLayer);
                found = true;
            }
            else
            {
                firstLayerOrder =
                    Mathf.Min(
                        firstLayerOrder,
                        state.LayerOrder);
                found = true;
            }
        }

        if (!found)
        {
            return 0f;
        }

        float layerEndProgress = 0f;

        for (int i = 0; i < fragments.Count; i++)
        {
            FragmentState state = fragments[i];

            if (state == null ||
                state.IsAnchor)
            {
                continue;
            }

            bool sameLayer;

            if (usingAuthoredLayerOrder &&
                state.AssemblyLayer >= 0)
            {
                sameLayer =
                    state.AssemblyLayer ==
                    firstAssemblyLayer;
            }
            else
            {
                sameLayer =
                    Mathf.Abs(
                        state.LayerOrder -
                        firstLayerOrder) < 0.0001f;
            }

            if (!sameLayer)
            {
                continue;
            }

            layerEndProgress =
                Mathf.Max(
                    layerEndProgress,
                    state.EndProgress);
        }

        return layerEndProgress;
    }

    private float GetLayerEndProgress(
        FragmentState state)
    {
        if (state == null)
        {
            return 0f;
        }

        //
        // Anchor(Crystal_Lower) は、
        // 最初の通常レイヤーが組み上がったタイミングで heal 開始。
        //
        if (state.IsAnchor)
        {
            return
                GetFirstPlayableLayerEndProgress();
        }

        float layerEndProgress = 0f;
        bool found = false;

        for (int i = 0; i < fragments.Count; i++)
        {
            FragmentState other = fragments[i];

            if (!IsSameAssemblyLayer(
                    state,
                    other))
            {
                continue;
            }

            layerEndProgress =
                Mathf.Max(
                    layerEndProgress,
                    other.EndProgress);

            found = true;
        }

        return found
            ? layerEndProgress
            : state.EndProgress;
    }
    private float GetLayerRevealTarget(
        FragmentState state)
    {
        if (state == null)
        {
            return 0f;
        }

        float revealTarget = 0f;
        bool found = false;

        if (state.IsAnchor)
        {
            int firstAssemblyLayer =
                int.MaxValue;

            float firstLayerOrder =
                float.MaxValue;

            for (int i = 0;
                 i < fragments.Count;
                 i++)
            {
                FragmentState other =
                    fragments[i];

                if (other == null ||
                    other.IsAnchor)
                {
                    continue;
                }

                if (usingAuthoredLayerOrder &&
                    other.AssemblyLayer >= 0)
                {
                    firstAssemblyLayer =
                        Mathf.Min(
                            firstAssemblyLayer,
                            other.AssemblyLayer);
                }
                else
                {
                    firstLayerOrder =
                        Mathf.Min(
                            firstLayerOrder,
                            other.LayerOrder);
                }
            }

            for (int i = 0;
                 i < fragments.Count;
                 i++)
            {
                FragmentState other =
                    fragments[i];

                if (other == null ||
                    other.IsAnchor)
                {
                    continue;
                }

                bool sameFirstLayer =
                    usingAuthoredLayerOrder &&
                    other.AssemblyLayer >= 0
                        ? other.AssemblyLayer ==
                          firstAssemblyLayer
                        : Mathf.Abs(
                            other.LayerOrder -
                            firstLayerOrder) <
                          0.0001f;

                if (!sameFirstLayer)
                {
                    continue;
                }

                revealTarget =
                    Mathf.Max(
                        revealTarget,
                        other.RevealTopProgress);

                found = true;
            }
        }
        else
        {
            for (int i = 0;
                 i < fragments.Count;
                 i++)
            {
                FragmentState other =
                    fragments[i];

                if (!IsSameAssemblyLayer(
                        state,
                        other))
                {
                    continue;
                }

                revealTarget =
                    Mathf.Max(
                        revealTarget,
                        other.RevealTopProgress);

                found = true;
            }
        }

        if (!found)
        {
            revealTarget =
                state.RevealTopProgress;
        }

        return
            Mathf.Clamp01(
                revealTarget +
                layerRevealPadding);
    }

    private float GetLayerFinalRevealStartProgress(
        FragmentState state)
    {
        return
            GetLayerEndProgress(state) +
            GetNormalizedSeconds(
                seamHealDelay +
                layerFinalRevealDelay);
    }

    private float GetLayerFinalRevealEndProgress(
        FragmentState state)
    {
        return
            GetLayerFinalRevealStartProgress(state) +
            GetNormalizedSeconds(
                Mathf.Max(
                    0.05f,
                    seamHealDuration -
                    layerFinalRevealDelay));
    }

    private float GetSteppedFinalRevealProgress(
        float progress)
    {
        List<FragmentState> representatives =
            new List<FragmentState>();

        for (int i = 0;
             i < fragments.Count;
             i++)
        {
            FragmentState state =
                fragments[i];

            if (state == null ||
                state.IsAnchor)
            {
                continue;
            }

            bool alreadyAdded = false;

            for (int j = 0;
                 j < representatives.Count;
                 j++)
            {
                if (IsSameAssemblyLayer(
                        state,
                        representatives[j]))
                {
                    alreadyAdded = true;
                    break;
                }
            }

            if (!alreadyAdded)
            {
                representatives.Add(
                    state);
            }
        }

        representatives.Sort(
            (a, b) =>
            {
                if (usingAuthoredLayerOrder)
                {
                    return
                        a.AssemblyLayer.CompareTo(
                            b.AssemblyLayer);
                }

                return
                    a.LayerOrder.CompareTo(
                        b.LayerOrder);
            });

        float previousReveal = 0f;

        for (int i = 0;
             i < representatives.Count;
             i++)
        {
            FragmentState state =
                representatives[i];

            float revealStart =
                GetLayerFinalRevealStartProgress(
                    state);

            float revealEnd =
                GetLayerFinalRevealEndProgress(
                    state);

            float targetReveal =
                GetLayerRevealTarget(
                    state);

            if (i ==
                representatives.Count - 1)
            {
                targetReveal = 1f;
            }

            if (progress <= revealStart)
            {
                return previousReveal;
            }

            float t =
                SmootherStep(
                    Mathf.InverseLerp(
                        revealStart,
                        revealEnd,
                        progress));

            float currentReveal =
                Mathf.Lerp(
                    previousReveal,
                    targetReveal,
                    t);

            if (progress < revealEnd)
            {
                return currentReveal;
            }

            previousReveal =
                targetReveal;
        }

        return previousReveal;
    }

    private void CaptureEmbeddedFinalFormFrontStrength()
    {
        embeddedFinalBaseFormFrontStrength =
            new float[
                embeddedFinalRenderers.Length];

        for (int i = 0;
             i < embeddedFinalRenderers.Length;
             i++)
        {
            Renderer renderer =
                embeddedFinalRenderers[i];

            if (renderer == null)
            {
                continue;
            }

            Material material =
                renderer.sharedMaterial;

            if (material != null &&
                material.HasProperty(
                    FormFrontStrengthId))
            {
                embeddedFinalBaseFormFrontStrength[i] =
                    material.GetFloat(
                        FormFrontStrengthId);
            }
        }
    }

    private void SetEmbeddedFinalFormFrontMultiplier(
        float multiplier)
    {
        if (propertyBlock == null)
        {
            propertyBlock =
                new MaterialPropertyBlock();
        }

        multiplier =
            Mathf.Max(
                0f,
                multiplier);

        for (int i = 0;
             i < embeddedFinalRenderers.Length;
             i++)
        {
            Renderer renderer =
                embeddedFinalRenderers[i];

            if (renderer == null)
            {
                continue;
            }

            Material material =
                renderer.sharedMaterial;

            if (material == null ||
                !material.HasProperty(
                    FormFrontStrengthId))
            {
                continue;
            }

            float baseStrength =
                i <
                embeddedFinalBaseFormFrontStrength.Length
                    ? embeddedFinalBaseFormFrontStrength[i]
                    : material.GetFloat(
                        FormFrontStrengthId);

            propertyBlock.Clear();
            renderer.GetPropertyBlock(
                propertyBlock);

            propertyBlock.SetFloat(
                FormFrontStrengthId,
                baseStrength *
                multiplier);

            renderer.SetPropertyBlock(
                propertyBlock);
        }
    }

    private float GetShardHealProgress(
        FragmentState state,
        float progress)
    {
        if (!progressiveSeamHealing ||
            state == null)
        {
            return 0f;
        }

        float layerEndProgress =
            GetLayerEndProgress(state);

        float healStartProgress =
            layerEndProgress +
            GetNormalizedSeconds(
                seamHealDelay);

        float healEndProgress =
            healStartProgress +
            GetNormalizedSeconds(
                seamHealDuration);

        return
            SmootherStep(
                Mathf.InverseLerp(
                    healStartProgress,
                    healEndProgress,
                    progress));
    }

    private float GetShardVisibility(
        FragmentState state,
        float progress)
    {
        if (!progressiveSeamHealing)
        {
            return 1f;
        }

        float healT =
            GetShardHealProgress(
                state,
                progress);

        return
            Mathf.Lerp(
                1f,
                healedShardAlpha,
                healT);
    }

    private float GetPreSpawnStartProgress(
        FragmentState state)
    {
        if (state == null || state.IsAnchor)
        {
            return 0f;
        }

        return Mathf.Max(
            0f,
            state.StartProgress -
            GetNormalizedSeconds(
                shardPreSpawnDuration));
    }

    private void ApplyHiddenState(
        FragmentState state)
    {
        if (state == null ||
            state.Transform == null)
        {
            return;
        }

        if (state.Transform.gameObject.activeSelf)
        {
            state.Transform.gameObject.SetActive(false);
        }
    }

    private void ApplyPreSpawnState(
        FragmentState state,
        float progress)
    {
        if (state == null ||
            state.Transform == null)
        {
            return;
        }

        if (!state.Transform.gameObject.activeSelf)
        {
            state.Transform.gameObject.SetActive(true);
        }

        float preSpawnStart =
            GetPreSpawnStartProgress(state);

        float spawnT =
            Mathf.InverseLerp(
                preSpawnStart,
                state.StartProgress,
                progress);

        spawnT =
            SmootherStep(spawnT);

        float waitTime =
            Time.time *
            waitingDriftFrequency +
            state.WaitingPhase;

        float waitWave =
            Mathf.Sin(waitTime);

        float waitWave2 =
            Mathf.Sin(
                waitTime * 0.67f +
                1.3f);

        Vector3 drift =
            state.WaitingDirection *
            waitWave *
            shardPreSpawnDriftAmount +
            center.up *
            waitWave2 *
            shardPreSpawnDriftAmount *
            0.35f;

        state.Transform.position =
            state.StartPosition +
            drift;

        state.Transform.rotation =
            state.StartRotation;

        float startScaleMultiplier =
            Mathf.Lerp(
                shardPreSpawnStartScale,
                1f,
                spawnT);

        state.Transform.localScale =
            state.StartScale *
            startScaleMultiplier;

        ApplyFragmentVisual(
            state,
            0f,
            0f,
            spawnT);
    }

    private void UpdateFragments(float progress)
    {
        for (int i = 0; i < fragments.Count; i++)
        {
            FragmentState state = fragments[i];

            float mergeStart =
                runtimeFragmentMergeStart;

            //
            // Alphaだけでは透明Crystalの内部面/屈折が残るため、
            // Layer Heal完了時点で実GeometryもOFFにする。
            // IMPORTANT:
            // Final phaseへ入っても再表示しない。
            // SetProgress(1)の瞬間にShardを再Active化すると、
            // 最終発光時だけ16分割の切れ目が一瞬復活するため。
            //
            if (progressiveSeamHealing &&
                disableHealedShardObjects &&
                GetShardHealProgress(
                    state,
                    progress) >= 0.999f)
            {
                if (state.Transform.gameObject.activeSelf)
                {
                    state.Transform.gameObject.SetActive(false);
                }

                continue;
            }

            if (state.IsAnchor &&
                progress < mergeStart)
            {
                state.Transform.gameObject.SetActive(true);
                state.Transform.position =
                    state.TargetPosition;
                state.Transform.rotation =
                    state.TargetRotation;
                state.Transform.localScale =
                    state.TargetScale;

                SetFormProgress(
                    state.Renderers,
                    1f);

                ApplyFragmentVisual(
                    state,
                    0f,
                    1f,
                    GetShardVisibility(
                        state,
                        progress));

                continue;
            }

        //
        // Anchor以外は、最初から全部見せない。
        // 自分の番の少し前だけ現れて、そのまま移動へ入る。
        if (!state.IsAnchor)
        {
            float preSpawnStart =
                GetPreSpawnStartProgress(state);

            if (progress < preSpawnStart)
            {
                ApplyHiddenState(state);
                continue;
            }

            if (progress < state.StartProgress)
            {
                ApplyPreSpawnState(
                    state,
                    progress);
                continue;
            }
        }
        else
        {
            //
            // Anchorだけは最初から存在していてよい。
            // ここは従来どおり固定表示。
            //
            if (!state.Transform.gameObject.activeSelf)
            {
                state.Transform.gameObject.SetActive(true);
            }

            state.Transform.position =
                state.TargetPosition;

            state.Transform.rotation =
                state.TargetRotation;

            state.Transform.localScale =
                state.TargetScale;

            SetFormProgress(
                state.Renderers,
                1f);

            ApplyFragmentVisual(
                state,
                0f,
                1f,
                GetShardVisibility(
                    state,
                    progress));

            continue;
        }
            if (!state.Transform.gameObject.activeSelf)
            {
                state.Transform.gameObject.SetActive(true);
            }

            //
            // まだ自分の番ではないShardは、Scatter位置でごく小さく漂わせる。
            // 完全静止の「部品待ち」感を消しつつ、勝手に組み立て始めない。
            //
            if (progress < state.StartProgress)
            {
                float waitTime =
                    Time.time *
                    waitingDriftFrequency +
                    state.WaitingPhase;

                float waitWave =
                    Mathf.Sin(
                        waitTime);

                float waitWave2 =
                    Mathf.Sin(
                        waitTime * 0.67f +
                        1.3f);

                state.Transform.position =
                    state.StartPosition +
                    state.WaitingDirection *
                    waitWave *
                    waitingDriftAmount +
                    center.up *
                    waitWave2 *
                    waitingDriftAmount *
                    0.35f;

                state.Transform.rotation =
                    state.StartRotation;

                state.Transform.localScale =
                    state.StartScale;

                ApplyFragmentVisual(
                    state,
                    0f);

                continue;
            }

            float localT =
                Mathf.InverseLerp(
                    state.StartProgress,
                    state.EndProgress,
                    progress);

            //
            // 1枚ずつ、開始も到着も滑らかに。
            //
            float travelT =
                SmootherStep(
                    localT);

            //
            // 軌道は中盤だけ少しカーブし、
            // 最後の25%ほどは完成面へ真っすぐ静かに吸着。
            //
            float arrivalDampT =
                SmootherStep(
                    Mathf.InverseLerp(
                        arrivalCurveDamping,
                        1f,
                        localT));

            float arc =
                Mathf.Sin(
                    travelT *
                    Mathf.PI) *
                state.CurveAmount *
                (1f - arrivalDampT);

            state.Transform.position =
                Vector3.LerpUnclamped(
                    state.StartPosition,
                    state.TargetPosition,
                    travelT) +
                state.CurveDirection *
                arc;

            //
            // 回転だけ少し遅れて完成角度へ。
            //
            float rotationT =
                SmootherStep(
                    Mathf.InverseLerp(
                        rotationFollowDelay,
                        1f,
                        localT));

            state.Transform.rotation =
                Quaternion.SlerpUnclamped(
                    state.StartRotation,
                    state.TargetRotation,
                    rotationT);

            //
            // Scaleはほぼ素直に完成値へ。
            // 到着時のポンッ/ガチャンを出さない。
            //
            float scaleT =
                SmootherStep(
                    localT);

            Vector3 scale =
                Vector3.LerpUnclamped(
                    state.StartScale,
                    state.TargetScale,
                    scaleT);

            float settlePulse = 0f;

            if (settleScaleResponse > 0.0001f &&
                settlePortion > 0.0001f)
            {
                float settleStart =
                    1f -
                    settlePortion;

                if (localT > settleStart)
                {
                    float settleT =
                        Mathf.InverseLerp(
                            settleStart,
                            1f,
                            localT);

                    settlePulse =
                        Mathf.Sin(
                            settleT *
                            Mathf.PI);

                    float pulse =
                        settlePulse *
                        settleScaleOvershoot *
                        settleScaleResponse;

                    scale =
                        Vector3.Scale(
                            scale,
                            Vector3.one *
                            (1f + pulse));
                }
            }

            state.Transform.localScale =
                scale;

            ApplyFragmentVisual(
                state,
                settlePulse *
                settleVisualResponse,
                localT,
                GetShardVisibility(
                    state,
                    progress));
        }
    }

    private void UpdateEmbeddedFinal(float progress)
    {
        if (progressiveSeamHealing)
        {
            //
            // 切れ目Fadeを先に開始し、その少し後から
            // 継ぎ目のないFinal CrystalをLayer単位で追従させる。
            //
            // _FormProgressのFront発光はHeal中だけ抑えるので、
            // 「ピカッ → 切れ目が消える」の逆転を防ぐ。
            //
            SetEmbeddedFinalFormFrontMultiplier(
                layerHealFormFrontMultiplier);

            float assemblyReveal =
                GetSteppedFinalRevealProgress(
                    progress);

            if (assemblyReveal <= 0.0001f)
            {
                if (embeddedFinalCrystal.gameObject.activeSelf)
                {
                    embeddedFinalCrystal.gameObject.SetActive(false);
                }

                SetFormProgress(
                    embeddedFinalRenderers,
                    0f);

                return;
            }

            if (!embeddedFinalCrystal.gameObject.activeSelf)
            {
                SetFormProgress(
                    embeddedFinalRenderers,
                    0f);

                embeddedFinalCrystal.gameObject.SetActive(true);
            }

            SetFormProgress(
                embeddedFinalRenderers,
                assemblyReveal);

            return;
        }

        SetEmbeddedFinalFormFrontMultiplier(1f);

        if (progress < runtimeFinalCrystalStart)
        {
            if (embeddedFinalCrystal.gameObject.activeSelf)
            {
                embeddedFinalCrystal.gameObject.SetActive(false);
            }

            SetFormProgress(
                embeddedFinalRenderers,
                0f);

            return;
        }

        if (!embeddedFinalCrystal.gameObject.activeSelf)
        {
            SetFormProgress(
                embeddedFinalRenderers,
                0f);

            embeddedFinalCrystal.gameObject.SetActive(true);
        }

        float formT =
            Mathf.InverseLerp(
                runtimeFinalCrystalStart,
                1f,
                progress);

        formT =
            Mathf.SmoothStep(
                0f,
                1f,
                formT);

        SetFormProgress(
            embeddedFinalRenderers,
            formT);
    }

    private void PrepareLegacyFallback()
    {
        legacyFinalTarget.gameObject.SetActive(true);

        floatTarget =
            legacyFinalTarget;

        floatBasePosition =
            legacyFinalTarget.position;

        floatBaseRotation =
            legacyFinalTarget.rotation;

        floatPhaseOffset = 0f;
        secondaryFloatPhase = 1.73f;
        lateralFloatPhase = 3.11f;

        SetLegacyVisibility(false);
        SetFormProgress(legacyRenderers, 0f);
    }

    private void UpdateLegacyFallback(float progress)
    {
        bool visible =
            progress >= finalCrystalStart;

        SetLegacyVisibility(visible);

        float formT =
            visible
                ? Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(
                        finalCrystalStart,
                        1f,
                        progress))
                : 0f;

        SetFormProgress(legacyRenderers, formT);
    }

    private void CaptureLegacyRendererState()
    {
        legacyRenderers =
            legacyFinalTarget.GetComponentsInChildren<Renderer>(true);

        legacyRendererEnabled =
            new bool[legacyRenderers.Length];

        for (int i = 0; i < legacyRenderers.Length; i++)
        {
            legacyRendererEnabled[i] =
                legacyRenderers[i] != null &&
                legacyRenderers[i].enabled;
        }

        legacyRendererStateCaptured = true;
    }

    private void HideLegacyFinal()
    {
        if (legacyFinalTarget == embeddedFinalCrystal)
        {
            return;
        }

        SetLegacyVisibility(false);
    }

    private void SetLegacyVisibility(bool visible)
    {
        for (int i = 0; i < legacyRenderers.Length; i++)
        {
            Renderer renderer =
                legacyRenderers[i];

            if (renderer == null)
            {
                continue;
            }

            bool originallyEnabled =
                i < legacyRendererEnabled.Length &&
                legacyRendererEnabled[i];

            renderer.enabled =
                visible && originallyEnabled;
        }
    }

    private void RestoreLegacyRendererState()
    {
        if (!legacyRendererStateCaptured)
        {
            return;
        }

        for (int i = 0; i < legacyRenderers.Length; i++)
        {
            Renderer renderer =
                legacyRenderers[i];

            if (renderer != null &&
                i < legacyRendererEnabled.Length)
            {
                renderer.enabled =
                    legacyRendererEnabled[i];
            }
        }

        legacyRendererStateCaptured = false;
    }

    private Bounds CalculateFragmentBounds()
    {
        bool hasBounds = false;
        Bounds bounds = default;

        for (int i = 0; i < fragments.Count; i++)
        {
            Renderer[] renderers =
                fragments[i].Renderers;

            for (int j = 0; j < renderers.Length; j++)
            {
                Renderer renderer =
                    renderers[j];

                if (renderer == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
        }

        if (!hasBounds)
        {
            bounds =
                new Bounds(
                    embeddedFinalCrystal.position,
                    Vector3.one * 0.1f);
        }

        return bounds;
    }

    private static bool TryCalculateRendererBounds(
        Renderer[] renderers,
        out Bounds bounds)
    {
        bounds = default;

        if (renderers == null)
        {
            return false;
        }

        bool hasBounds = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer =
                renderers[i];

            if (renderer == null)
            {
                continue;
            }

            Bounds rendererBounds =
                renderer.bounds;

            if (rendererBounds.size.sqrMagnitude < 0.0000001f)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = rendererBounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(rendererBounds);
            }
        }

        return hasBounds;
    }

    private FragmentRendererState[] BuildFragmentRendererStates(
        Renderer[] renderers)
    {
        if (renderers == null)
        {
            return Array.Empty<FragmentRendererState>();
        }

        FragmentRendererState[] result =
            new FragmentRendererState[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            FragmentRendererState state =
                new FragmentRendererState
                {
                    Renderer = renderer
                };

            Material material =
                renderer != null
                    ? renderer.sharedMaterial
                    : null;

            if (material != null)
            {
                if (material.HasProperty(AlphaId))
                {
                    state.BaseAlpha =
                        material.GetFloat(AlphaId);
                }

                if (material.HasProperty(EmissionStrengthId))
                {
                    state.BaseEmissionStrength =
                        material.GetFloat(EmissionStrengthId);
                }

                if (material.HasProperty(EdgeStrengthId))
                {
                    state.BaseEdgeStrength =
                        material.GetFloat(EdgeStrengthId);
                }

                if (material.HasProperty(FaceHighlightStrengthId))
                {
                    state.BaseFaceHighlightStrength =
                        material.GetFloat(
                            FaceHighlightStrengthId);
                }
            }

            result[i] = state;
        }

        return result;
    }

    private void ApplyFragmentVisual(
        FragmentState fragment,
        float settlePulse)
    {
        ApplyFragmentVisual(
            fragment,
            settlePulse,
            0f,
            1f);
    }

    private void ApplyFragmentVisual(
        FragmentState fragment,
        float settlePulse,
        float assemblyProgress)
    {
        ApplyFragmentVisual(
            fragment,
            settlePulse,
            assemblyProgress,
            1f);
    }

    private void ApplyFragmentVisual(
        FragmentState fragment,
        float settlePulse,
        float assemblyProgress,
        float visibilityMultiplier)
    {
        if (fragment == null ||
            fragment.RendererStates == null)
        {
            return;
        }

        settlePulse =
            Mathf.Clamp01(settlePulse);

        assemblyProgress =
            Mathf.Clamp01(assemblyProgress);

        visibilityMultiplier =
            Mathf.Clamp01(visibilityMultiplier);

        //
        // 移動中はShardの存在感を残す。
        // 完成位置へ近づいた後半だけEdge/Emissionを急激に落として、
        // 接合面が白い線として残るのを防ぐ。
        //
        float seamFade =
            SmootherStep(
                Mathf.InverseLerp(
                    seamFadeStart,
                    1f,
                    assemblyProgress));

        float emissionMultiplier =
            shardEmissionMultiplier *
            assemblyEmissionSuppression *
            Mathf.Lerp(
                1f,
                settledEmissionMultiplier,
                seamFade) *
            Mathf.Lerp(
                1f,
                settleEmissionBoost,
                settlePulse);

        float edgeMultiplier =
            shardEdgeMultiplier *
            assemblyEdgeSuppression *
            Mathf.Lerp(
                1f,
                settledEdgeMultiplier,
                seamFade) *
            Mathf.Lerp(
                1f,
                settleEdgeBoost,
                settlePulse);

        float faceHighlightMultiplier =
            assemblyFaceHighlightSuppression *
            Mathf.Lerp(
                1f,
                settledFaceHighlightMultiplier,
                seamFade);

        for (int i = 0;
             i < fragment.RendererStates.Length;
             i++)
        {
            FragmentRendererState rendererState =
                fragment.RendererStates[i];

            Renderer renderer =
                rendererState.Renderer;

            if (renderer == null)
            {
                continue;
            }

            propertyBlock.Clear();
            renderer.GetPropertyBlock(propertyBlock);

            Material material =
                renderer.sharedMaterial;

            if (material != null &&
                material.HasProperty(AlphaId))
            {
                propertyBlock.SetFloat(
                    AlphaId,
                    rendererState.BaseAlpha *
                    shardAlphaMultiplier *
                    visibilityMultiplier);
            }

            if (material != null &&
                material.HasProperty(EmissionStrengthId))
            {
                propertyBlock.SetFloat(
                    EmissionStrengthId,
                    rendererState.BaseEmissionStrength *
                    emissionMultiplier);
            }

            if (material != null &&
                material.HasProperty(EdgeStrengthId))
            {
                propertyBlock.SetFloat(
                    EdgeStrengthId,
                    rendererState.BaseEdgeStrength *
                    edgeMultiplier);
            }

            if (material != null &&
                material.HasProperty(FaceHighlightStrengthId))
            {
                propertyBlock.SetFloat(
                    FaceHighlightStrengthId,
                    rendererState.BaseFaceHighlightStrength *
                    faceHighlightMultiplier);
            }

            renderer.SetPropertyBlock(propertyBlock);
        }
    }

    private void SetFormProgress(
        Renderer[] renderers,
        float progress)
    {
        if (propertyBlock == null)
        {
            propertyBlock =
                new MaterialPropertyBlock();
        }

        float value =
            Mathf.Clamp01(progress);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer =
                renderers[i];

            if (renderer == null)
            {
                continue;
            }

            propertyBlock.Clear();
            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(FormProgressId, value);
            renderer.SetPropertyBlock(propertyBlock);
        }
    }

    private FragmentState FindAnchorFragment()
    {
        for (int i = 0; i < fragments.Count; i++)
        {
            FragmentState state =
                fragments[i];

            if (state.IsAnchor ||
                string.Equals(
                    state.Transform.name,
                    anchorFragmentName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return state;
            }
        }

        return null;
    }

    private void SetFragmentsWaitingForAnchor()
    {
        embeddedFinalCrystal.gameObject.SetActive(false);
        SetFormProgress(
            embeddedFinalRenderers,
            0f);

        for (int i = 0; i < fragments.Count; i++)
        {
            FragmentState state =
                fragments[i];

            state.Transform.gameObject.SetActive(false);

            if (state.IsAnchor)
            {
                state.Transform.position =
                    state.TargetPosition;
                state.Transform.rotation =
                    state.TargetRotation;
                state.Transform.localScale =
                    state.TargetScale *
                    anchorStartScale;

                SetFormProgress(
                    state.Renderers,
                    0f);
            }
        }
    }

    private bool CanUseAuthoredLayerOrder()
    {
        if (fragments.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < fragments.Count; i++)
        {
            if (fragments[i].AssemblyLayer < 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryParseAssemblyLayer(
        string objectName,
        out int layer)
    {
        layer = -1;

        if (string.IsNullOrEmpty(objectName))
        {
            return false;
        }

        if (objectName.IndexOf(
                "Lower",
                StringComparison.OrdinalIgnoreCase) >= 0)
        {
            layer = 0;
            return true;
        }

        if (objectName.IndexOf(
                "Top",
                StringComparison.OrdinalIgnoreCase) >= 0)
        {
            layer = 999;
            return true;
        }

        const string marker = "Layer";

        int markerIndex =
            objectName.IndexOf(
                marker,
                StringComparison.OrdinalIgnoreCase);

        if (markerIndex < 0)
        {
            return false;
        }

        int index =
            markerIndex +
            marker.Length;

        while (index < objectName.Length &&
               !char.IsDigit(objectName[index]))
        {
            index++;
        }

        if (index >= objectName.Length)
        {
            return false;
        }

        int value = 0;
        int digitCount = 0;

        while (index < objectName.Length &&
               char.IsDigit(objectName[index]))
        {
            value =
                value * 10 +
                (objectName[index] - '0');

            index++;
            digitCount++;
        }

        if (digitCount == 0)
        {
            return false;
        }

        layer = value;
        return true;
    }

    private int BuildStableSeed(Vector3 position)
    {
        unchecked
        {
            int seed = scatterSeed;
            seed = seed * 397 ^ Mathf.RoundToInt(position.x * 1000f);
            seed = seed * 397 ^ Mathf.RoundToInt(position.y * 1000f);
            seed = seed * 397 ^ Mathf.RoundToInt(position.z * 1000f);
            return seed;
        }
    }

    private Vector3 CenterSpaceToWorldDirection(
        Vector3 centerSpaceDirection)
    {
        Vector3 right =
            SafeNormalized(center.right, Vector3.right);

        Vector3 up =
            SafeNormalized(center.up, Vector3.up);

        Vector3 forward =
            SafeNormalized(center.forward, Vector3.forward);

        return
            right * centerSpaceDirection.x +
            up * centerSpaceDirection.y +
            forward * centerSpaceDirection.z;
    }

    private static float NextFloat(
        System.Random random,
        float minimum,
        float maximum)
    {
        return Mathf.Lerp(
            minimum,
            maximum,
            (float)random.NextDouble());
    }

    private static float ProjectedExtent(
        Vector3 extents,
        Vector3 axis)
    {
        Vector3 absoluteAxis =
            new Vector3(
                Mathf.Abs(axis.x),
                Mathf.Abs(axis.y),
                Mathf.Abs(axis.z));

        return Vector3.Dot(extents, absoluteAxis);
    }

    private static Vector3 SafeNormalized(
        Vector3 value,
        Vector3 fallback)
    {
        return value.sqrMagnitude > 0.000001f
            ? value.normalized
            : fallback;
    }

    private static Vector3 DivideComponents(
        Vector3 numerator,
        Vector3 denominator)
    {
        return new Vector3(
            SafeDivide(numerator.x, denominator.x),
            SafeDivide(numerator.y, denominator.y),
            SafeDivide(numerator.z, denominator.z));
    }

    private static float SafeDivide(
        float numerator,
        float denominator)
    {
        return Mathf.Abs(denominator) > 0.000001f
            ? numerator / denominator
            : numerator;
    }

    private static void SetWorldScale(
        Transform target,
        Vector3 worldScale)
    {
        if (target.parent == null)
        {
            target.localScale = worldScale;
            return;
        }

        target.localScale =
            DivideComponents(
                worldScale,
                target.parent.lossyScale);
    }

    private static float SmootherStep(float t)
    {
        t = Mathf.Clamp01(t);

        return
            t * t * t *
            (t * (t * 6f - 15f) + 10f);
    }

    private static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        float inverse = 1f - t;
        return 1f - inverse * inverse * inverse;
    }

    private static float EaseInCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t;
    }

    private bool TryAssignAuthoredGrowthSequence()
    {
        if (!useAuthoredGrowthOrder ||
            authoredGrowthOrder == null ||
            authoredGrowthOrder.Length == 0)
        {
            return false;
        }

        int movingFragmentCount = 0;

        for (int i = 0; i < fragments.Count; i++)
        {
            FragmentState state = fragments[i];

            state.SequenceIndex = -1;

            if (!state.IsAnchor)
            {
                movingFragmentCount++;
            }
        }

        if (authoredGrowthOrder.Length !=
            movingFragmentCount)
        {
            Debug.LogWarning(
                $"{name}: Authored Growth Orderの要素数が不正です。 " +
                $"expected={movingFragmentCount}, " +
                $"actual={authoredGrowthOrder.Length}. " +
                "従来順へフォールバックします。",
                this);

            return false;
        }

        for (int sequenceIndex = 0;
            sequenceIndex < authoredGrowthOrder.Length;
            sequenceIndex++)
        {
            string targetName =
                authoredGrowthOrder[sequenceIndex];

            FragmentState matchedState = null;

            for (int i = 0;
                i < fragments.Count;
                i++)
            {
                FragmentState state =
                    fragments[i];

                if (state.IsAnchor ||
                    state.Transform == null)
                {
                    continue;
                }

                if (string.Equals(
                        state.Transform.name,
                        targetName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    matchedState = state;
                    break;
                }
            }

            if (matchedState == null)
            {
                Debug.LogWarning(
                    $"{name}: Authored Growth OrderのShard " +
                    $"'{targetName}' が見つかりません。 " +
                    "従来順へフォールバックします。",
                    this);

                return false;
            }

            if (matchedState.SequenceIndex >= 0)
            {
                Debug.LogWarning(
                    $"{name}: Authored Growth Orderに " +
                    $"'{targetName}' が重複しています。 " +
                    "従来順へフォールバックします。",
                    this);

                return false;
            }

            matchedState.SequenceIndex =
                sequenceIndex;
        }

        for (int i = 0;
            i < fragments.Count;
            i++)
        {
            FragmentState state =
                fragments[i];

            if (!state.IsAnchor &&
                state.SequenceIndex < 0)
            {
                Debug.LogWarning(
                    $"{name}: Growth Orderに含まれていないShardがあります。 " +
                    $"Shard={state.Transform.name}. " +
                    "従来順へフォールバックします。",
                    this);

                return false;
            }
        }

        return true;
    }
}
