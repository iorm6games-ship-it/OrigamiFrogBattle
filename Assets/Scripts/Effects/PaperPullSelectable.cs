using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class PaperPullSelectable :
    MonoBehaviour,
    IPointerDownHandler,
    IDragHandler,
    IPointerUpHandler
{
    [Header("Paper Information")]
    [SerializeField]
    private string colorName = "red";

    [SerializeField]
    private SkinnedMeshRenderer targetRenderer;

    [SerializeField]
    private Transform lightTarget;

    [Header("Pull Motion")]
    [Tooltip(
        "画面高さの何割ドラッグしたら" +
        "最大引っ張り量になるか"
    )]
    [Range(0.05f, 0.5f)]
    [SerializeField]
    private float screenDragRatioForMax = 0.16f;

    [Tooltip(
        "ドラッグ距離を軌跡進捗へ変換するカーブ。" +
        "開始を緩くすると引き抜く抵抗感が出る"
    )]
    [SerializeField]
    private AnimationCurve dragResponseCurve =
        AnimationCurve.Linear(
            0f,
            0f,
            1f,
            1f
        );

    [Tooltip("紙を下へ動かす最大距離")]
    [Min(0.01f)]
    [SerializeField]
    private float maxPullDistance = 0.55f;

    [Tooltip("この割合以上引っ張って離すと選択確定")]
    [Range(0.1f, 1f)]
    [SerializeField]
    private float confirmThreshold = 0.65f;

    [Tooltip("最大まで引いた時の拡大率")]
    [Min(1f)]
    [SerializeField]
    private float pullScaleMultiplier = 1.0f;

    [Header("Return Motion")]
    [Tooltip("選択されなかったばあいに戻る時間")]
    [Min(0.01f)]
    [SerializeField]
    private float returnDuration = 0.18f;

    [Header("Pull Surface Motion")]

    [Tooltip("ドラッグ処理で使用するカメラ")]
    [SerializeField]
    private Camera targetCamera;

    [Tooltip(
        "紙を前面へ移動するときの" +
        "カメラ奥行き基準点"
    )]
    [SerializeField]
    private Transform pullFrontDepthPoint;

    [Tooltip(
        "紙面のローカル法線" +
        "最初は（0, 1, 0）で確認する"
    )]
    [SerializeField]
    private Vector3 localPaperNormal =
        Vector3.up;

    [Tooltip(
        "この割合までは紙面に沿って引き抜き" +
        "以降は前面へ浮かせる"
    )]
    [Range(0.1f, 0.9f)]
    [SerializeField]
    private float frontLiftStart = 0.3f;

    [Header("Selected Motion")]

    [Min(0.01f)]
    [SerializeField]
    private float selectedMoveDuration = 0.8f;

    [Min(0.1f)]
    [SerializeField]
    private float selectedMultiplier = 1.05f;

    [SerializeField]
    private PaperAbsorbLightController absorbLightController;

    public string ColorName => colorName;

    public Renderer TargetRenderer =>
        absorbLightController != null
            ? absorbLightController.TargetRenderer
            : targetRenderer;
            
    public Transform LightTarget =>
        lightTarget;
    public Transform PaperTransform =>
        transform;
    
    private PaperSelectionController owner;
    private BoxCollider hitCollider;
    private bool interactionEnabled;
    private bool dragging;
    private Transform selectedTargetPoint;
    private float pressScreenY;
    private float currentPullAmount;
    private Vector3 restLocalPosition;
    private Vector3 restLocalScale;

    private Coroutine moveCoroutine;

    private static readonly int FadeId =
        Shader.PropertyToID("_Fade");
    [Header("Unselected Fade")]

    [Tooltip("選ばれなかった紙が消える瞬間")]
    [Min(0.01f)]
    [SerializeField]
    private float unselectedFadeDuration = 0.45f;

    [Header("Trail Prototype")]
    [SerializeField]
    private PaperPullTrailPrototype trailPrototype;

    private MaterialPropertyBlock propertyBlock;

    [Header("Bone Pull")]
    [SerializeField]
    private PaperPullBoneController bonePullController;

    [Header("Selected Floating Motion")]

    [Tooltip("選択確定後の上下移動量")]
    [Min(0f)]
    [SerializeField]
    private float floatAmplitude = 0.04f;

    [Tooltip("上下に移動する速さ")]
    [Min(0.01f)]
    [SerializeField]
    private float floatFrequency = 0.7f;

    [Tooltip("左右に揺れる最大角度")]
    [Min(0f)]
    [SerializeField]
    private float floatRotationAngle = 1.5f;

    [Header("Fold Animation")]
    [SerializeField]
    private Animator foldAnimator;

    [Header("Fold Completion Flip")]

    [Tooltip("折り完了後、裏返しを始めるまでの間")]
    [Min(0f)]
    [SerializeField]
    private float foldFlipDelay = 0.1f;

    [Tooltip("完成したカエルを裏返す時間")]
    [Min(0.01f)]
    [SerializeField]
    private float foldFlipDuration = 0.45f;

    [Tooltip("裏返しに使うローカル軸")]
    [SerializeField]
    private Vector3 foldFlipLocalAxis =
        Vector3.right;

    [Tooltip("裏返しの回転角度。回転方向を反転する場合は負の値にする")]
    [SerializeField]
    private float foldFlipAngle = 220f;

    [SerializeField]
    private float foldFinalScaleMultiplier = 1.4f;

    [SerializeField]
    private float foldScaleDelay = 1.8f;

    [SerializeField]
    private float foldScaleDuration = 1.5f;
    
    [Header("Fold Step Flash")]
    [SerializeField]
    private float foldStepFlashDuration = 0.18f;

    private Coroutine foldStepFlashCoroutine;
    private Coroutine foldAnimationCoroutine;

    private float foldCenterOffset;

    private Coroutine floatingCoroutine;
    private Vector3 floatingBasePosition;
    private Quaternion floatingBaseRotation;
    private float floatingElapsedTime;
    private float floatingLiftOffset;
    private bool hasFloatingBasePose;

    private void Awake()
    {
        EnsureReferences();
        propertyBlock =
            new MaterialPropertyBlock();
        
        SetFadeImmediately(1f);
    }

    public void FadeOutAsUnselected()
    {
        interactionEnabled = false;
        dragging = false;
        
        EnsureReferences();
        if (hitCollider != null)
        {
            hitCollider.enabled = false;
        }
        StopMoveCoroutine();

        moveCoroutine =
            StartCoroutine(
                FadeOutAsUnselectedCoroutine()
            );

    }

    public void PlayFoldAnimation()
    {
        if (foldAnimator == null)
        {
            Debug.LogWarning(
                $"{nameof(PaperPullSelectable)}: Fold Animator が未設定です",
                this
            );
            return;
        }

        if (foldAnimationCoroutine != null)
        {
            StopCoroutine(
                foldAnimationCoroutine
            );
        }

        foldAnimationCoroutine =
            StartCoroutine(
                PlayFoldAnimationCoroutine()
            );
    }

    public IEnumerator WaitForFoldAnimation()
    {
        while (foldAnimationCoroutine != null)
        {
            yield return null;
        }
    }

    private IEnumerator PlayFoldAnimationCoroutine()
    {
        foldAnimator.Play(
            "Fold",
            0,
            0f
        );

        // AnimatorがFoldステートを反映するまで1フレーム待つ
        yield return null;

        bool enteredFoldState = false;

        while (true)
        {
            AnimatorStateInfo stateInfo =
                foldAnimator.GetCurrentAnimatorStateInfo(0);

            if (stateInfo.IsName("Fold"))
            {
                enteredFoldState = true;

                if (stateInfo.normalizedTime >= 1f &&
                    !foldAnimator.IsInTransition(0))
                {
                    break;
                }
            }
            else if (enteredFoldState)
            {
                // Fold完了後に別ステートへ遷移した場合
                break;
            }

            yield return null;
        }

        if (foldFlipDelay > 0f)
        {
            yield return new WaitForSeconds(
                foldFlipDelay
            );
        }

        yield return FlipCompletedFrogCoroutine();

        foldAnimationCoroutine = null;
    }

    private IEnumerator FlipCompletedFrogCoroutine()
    {
        float duration =
            Mathf.Max(
                0.01f,
                foldFlipDuration
            );

        Vector3 localAxis =
            foldFlipLocalAxis.sqrMagnitude > 0.0001f
                ? foldFlipLocalAxis.normalized
                : Vector3.right;

        Quaternion startRotation =
            floatingCoroutine != null
                ? floatingBaseRotation
                : transform.rotation;

        Quaternion endRotation =
            startRotation *
            Quaternion.AngleAxis(
                foldFlipAngle,
                localAxis
            );

        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;

            float normalizedTime =
                Mathf.Clamp01(
                    elapsedTime / duration
                );

            float easedTime =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    normalizedTime
                );

            Quaternion currentRotation =
                Quaternion.Slerp(
                    startRotation,
                    endRotation,
                    easedTime
                );

            // 浮遊はこの基準回転を元に継続される
            floatingBaseRotation =
                currentRotation;

            if (floatingCoroutine == null)
            {
                transform.rotation =
                    currentRotation;
            }

            yield return null;
        }

        floatingBaseRotation =
            endRotation;

        if (floatingCoroutine == null)
        {
            transform.rotation =
                endRotation;
        }
    }

    private IEnumerator FadeOutAsUnselectedCoroutine()
    {
        if (targetRenderer == null)
        {
            gameObject.SetActive(false);
            moveCoroutine = null;
            yield break;
        }

        float elapsedTime = 0f;

        while (elapsedTime < unselectedFadeDuration)
        {
            elapsedTime += Time.deltaTime;

            float normalizedTime =
                Mathf.Clamp01(
                    elapsedTime / unselectedFadeDuration
                );
            
            float easedTime =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    normalizedTime
                );
            
            SetFadeImmediately(
                1f - easedTime
            );

            yield return null;
        }
        SetFadeImmediately(0f);
        moveCoroutine = null;
        gameObject.SetActive(false);
    }

    private void SetFadeImmediately(
        float fade
    )
    {
        if (targetRenderer == null)
        {
            return;
        }

        if (propertyBlock == null)
        {
            propertyBlock =
                new MaterialPropertyBlock();
        }

        targetRenderer.GetPropertyBlock(
            propertyBlock
        );
        propertyBlock.SetFloat(
            FadeId,
            Mathf.Clamp01(fade)
        );
        targetRenderer.SetPropertyBlock(
            propertyBlock
        );
    }
    public void Initialize(
        PaperSelectionController selectionController
    )
    {
        owner = selectionController;
        EnsureReferences();
    }
    public void SetSelectedTarget(
        Transform targetPoint
    )
    {
        selectedTargetPoint = targetPoint;
    }
    public void SetInteractionEnabled(
        bool isEnabled
    )
    {
        EnsureReferences();
        interactionEnabled = isEnabled;

        if (hitCollider != null)
        {
            hitCollider.enabled =
                isEnabled;
        }

        if (isEnabled)
        {
            SetFadeImmediately(1f);
            CacheResetPose();
        }
        else
        {
            dragging = false;
        }
    }
    
    public void OnPointerDown(
        PointerEventData eventData
    )
    {
        Debug.Log(
            $"PointerDown: {name}",
            this
        );
        if (
            !interactionEnabled ||
            owner == null ||
            !owner.CanBeginPull(this)
        )
        {
            return;
        }
    
        StopMoveCoroutine();
        CacheResetPose();
        
        if (bonePullController != null)
        {
            bool pathPrepared =
                bonePullController.PreparePath(
                    selectedTargetPoint
                );
            if (!pathPrepared)
            {
                Debug.LogWarning(
                    $"{name}: Bone経路を作成できませんでした",
                    this
                );
                return;
            }
        }

        dragging = true;
        trailPrototype?.BeginRecording();
        currentPullAmount = 0f;
        pressScreenY = eventData.position.y;
    }

    public void OnDrag(
        PointerEventData eventData
    )
    {
        if (!dragging)
        {
            return;
        }

        float draggedPixels =
            Mathf.Max(
                0f,
                pressScreenY -
                eventData.position.y
            );
        float maxDragPixels =
            Mathf.Max(
                1f,
                Screen.height *
                screenDragRatioForMax
            );
        float rawDragAmount =
            Mathf.Clamp01(
                draggedPixels /
                maxDragPixels
            );

        currentPullAmount =
            Mathf.SmoothStep(
                    0f,
                    1f,
                    rawDragAmount
                );

        Debug.Log(
            $"OnDrag: {name}, pull={currentPullAmount:F3}",
            this
        );
        ApplyPullPose(
            currentPullAmount
        );
    }

    public void OnPointerUp(
        PointerEventData eventData
    )
    {
        if (!dragging)
        {
            return;
        }
        trailPrototype?.EndRecording();
        dragging = false;

        if (
            currentPullAmount >=
            confirmThreshold
        )
        {
            owner.ConfirmSelection(this);
        }
        else
        {
            ReturnToReset();
        }
    }

    public void ReturnToReset()
    {
        StopMoveCoroutine();

        moveCoroutine = 
            StartCoroutine(
                ReturnToResetCoroutine()
            );
    }

    public void CancelAndReturn()
    {
        interactionEnabled = false;

        EnsureReferences();

        if (hitCollider != null)
        {
            hitCollider.enabled = false;
        }

        dragging = false;
        ReturnToReset();
    }

    public void LockAsSelected()
    {
        interactionEnabled = false;
        EnsureReferences();

        if (hitCollider != null)
        {
            hitCollider.enabled = false;
        }

        dragging = false;
        StopMoveCoroutine();

        currentPullAmount =
            Mathf.Max(
                currentPullAmount,
                confirmThreshold
            );
        
        ApplyPullPose(
            currentPullAmount
        );
    }

    private void CacheResetPose()
    {
        restLocalPosition =
            transform.localPosition;
        restLocalScale =
            transform.localScale;
    }

    private void ApplyPullPose(
        float pullAmount
    )
    {
        Debug.Log(
            $"ApplyPullPose: {name}, " +
            $"pull={pullAmount:F3}, " +
            $"boneController=" +
            $"{(bonePullController != null)}",
            this
        );
        
        if (bonePullController != null)
        {
            bonePullController.ApplyPullAmount(
                pullAmount
            );
            transform.localScale =
                Vector3.Lerp(
                    restLocalScale,
                    restLocalScale *
                    pullScaleMultiplier,
                    pullAmount
                );
            return;
        }

        if (
            targetCamera == null ||
            transform.parent == null
        )
        {
            ApplyFallbackPullPose(
                pullAmount
            );
            return;
        }
        Vector3 restWorldPosition =
            transform.parent.TransformPoint(
                restLocalPosition
            );
        Vector3 paperNomalWorld =
            transform.TransformDirection(
                localPaperNormal.normalized
            );
        Vector3 screenDownWorld =
            -targetCamera.transform.up;
        
        Vector3 slideDirectionWorld =
            Vector3.ProjectOnPlane(
                screenDownWorld,
                paperNomalWorld
            );
        
        if (
            slideDirectionWorld.sqrMagnitude <
            0.0001f
        )
        {
            slideDirectionWorld = screenDownWorld;
        }
        slideDirectionWorld.Normalize();

        float slideAmount =
            Mathf.SmoothStep(
                0f,
                1f,
                pullAmount
            );
        
        Vector3 slideEndWorldPosition =
            restWorldPosition +
            slideDirectionWorld *
            maxPullDistance;

        Vector3 slideWorldPosition =
            Vector3.Lerp(
                restWorldPosition,
                slideEndWorldPosition,
                slideAmount
            );
            
        float liftAmount =
            Mathf.InverseLerp(
                frontLiftStart,
                1f,
                pullAmount
            );
        liftAmount =
            Mathf.SmoothStep(
                0f,
                1f,
                liftAmount
            );

        Vector3 frontWorldPosition =
            CalculateFrontWorldPosition(
                slideWorldPosition
            );
        Vector3 currentWorldPosition =
            Vector3.Lerp(
                slideWorldPosition,
                frontWorldPosition,
                liftAmount
            );
        
        transform.localPosition = 
            transform.parent.InverseTransformPoint(
                currentWorldPosition
            );

        transform.localScale = 
            Vector3.Lerp(
                restLocalScale,
                restLocalScale *
                pullScaleMultiplier,
                pullAmount
            );
        
    }

    private Vector3 CalculateFrontWorldPosition(
        Vector3 sourceWorldPosition
    )
    {
        if (
            targetCamera == null ||
            pullFrontDepthPoint == null
        )
        {
            return sourceWorldPosition;
        }

        Vector3 screenPosition =
            targetCamera.WorldToScreenPoint(
                sourceWorldPosition
            );

        Ray cameraRay =
            targetCamera.ScreenPointToRay(
                screenPosition
            );
        
        Plane frontPlane =
            new Plane(
                targetCamera.transform.forward,
                pullFrontDepthPoint.position
            );

        if (
            frontPlane.Raycast(
                cameraRay,
                out float enter
            )
        )
        {
            return cameraRay.GetPoint(
                enter
            );
        }
        return sourceWorldPosition;
    }

    private void ApplyFallbackPullPose(float pullAmount)
    {
        Vector3 position =
            restLocalPosition +
            Vector3.down *
            (
                maxPullDistance *
                pullAmount
            );
        
        transform.localPosition =
            position;
        
        transform.localScale =
            Vector3.Lerp(
                restLocalScale,
                restLocalScale *
                pullScaleMultiplier,
                pullAmount
            );
    }

    private IEnumerator ReturnToResetCoroutine()
    {
        float startPullAmount =
            currentPullAmount;

        Vector3 startPosition =
            transform.localPosition;

        Vector3 startScale =
            transform.localScale;

        float elapsedTime = 0f;

        while (elapsedTime < returnDuration)
        {
            elapsedTime += Time.deltaTime;

            float normalizedTime =
                Mathf.Clamp01(
                    elapsedTime /
                    returnDuration
                );

            float easedTime =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    normalizedTime
                );

            if (bonePullController != null)
            {
                currentPullAmount =
                    Mathf.Lerp(
                        startPullAmount,
                        0f,
                        easedTime
                    );

                ApplyPullPose(
                    currentPullAmount
                );
            }
            else
            {
                transform.localPosition =
                    Vector3.Lerp(
                        startPosition,
                        restLocalPosition,
                        easedTime
                    );

                transform.localScale =
                    Vector3.Lerp(
                        startScale,
                        restLocalScale,
                        easedTime
                    );
            }

            yield return null;
        }

        currentPullAmount = 0f;

        if (bonePullController != null)
        {
            bonePullController.ApplyPullAmount(0f);
            bonePullController.ResetBonesImmediately();
        }
        else
        {
            transform.localPosition =
                restLocalPosition;
        }

        transform.localScale =
            restLocalScale;

        moveCoroutine = null;
    }

    private void StopMoveCoroutine()
    {
        if (moveCoroutine == null)
        {
            return;
        }
        StopCoroutine(moveCoroutine);
        moveCoroutine = null;
    }

    private void EnsureReferences()
    {
        if (hitCollider == null)
        {
            hitCollider =
                GetComponent<BoxCollider>();
        }

        if (targetRenderer == null)
        {
            targetRenderer =
                GetComponent<SkinnedMeshRenderer>();
        }

        if (targetCamera == null)
        {
            targetCamera =
                Camera.main;
        }
    }
    public void MoveToSelectedPosition(
        Transform targetPoint,
        Action onCompleted = null
    )
    {
        if (targetPoint == null)
        {
            Debug.LogWarning(
                $"{name}: SelectedPaperCenterPointが未設定です。",
                this
            );

            onCompleted?.Invoke();
            return;
        }

        StopMoveCoroutine();

        if (bonePullController != null)
        {
            /*
            * 通常はPointerDown時に作成済みだが、
            * 念のため最終地点を使って再準備できるようにする。
            */
            if (!bonePullController.IsPathPrepared)
            {
                if (
                    !bonePullController.PreparePath(
                        targetPoint
                    )
                )
                {
                    onCompleted?.Invoke();
                    return;
                }
            }

            moveCoroutine =
                StartCoroutine(
                    CompleteBonePathCoroutine(
                        onCompleted
                    )
                );

            return;
        }

        moveCoroutine =
            StartCoroutine(
                MoveToSelectedPositionCoroutine(
                    targetPoint,
                    onCompleted
                )
            );
    }
    private IEnumerator MoveToSelectedPositionCoroutine(
       Transform targetPoint,
       Action onCompleted
    )
    {
        Vector3 startPosition =
            transform.position;
        Quaternion startRotation =
            transform.rotation;
        Vector3 startScale =
            transform.localScale;
        Vector3 targetScale =
            restLocalScale *
            selectedMultiplier;

        float elapsedTime = 0f;

        while (elapsedTime < selectedMoveDuration)
        {
            elapsedTime += Time.deltaTime;

            float normalizedTime =
                Mathf.Clamp01(
                    elapsedTime / selectedMoveDuration
                );
            
            float easedTime =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    normalizedTime
                );
            
            transform.position =
                Vector3.Lerp(
                    startPosition,
                    targetPoint.position,
                    easedTime
                );
            transform.rotation =
                Quaternion.Slerp(
                    startRotation,
                    targetPoint.rotation,
                    easedTime
                );
            transform.localScale = 
                Vector3.Lerp(
                    startScale,
                    targetScale,
                    easedTime
                );

            yield return null;
        }
        transform.position = 
            targetPoint.position;
        transform.rotation =
            targetPoint.rotation;
        transform.localScale =
            targetScale;

        moveCoroutine = null;
        onCompleted?.Invoke();
    }
    private IEnumerator CompleteBonePathCoroutine(
        Action onCompleted
    )
    {
        float startProgress =
            currentPullAmount;

        Vector3 startScale =
            transform.localScale;

        Vector3 targetScale =
            restLocalScale *
            selectedMultiplier;

        /*
        * すでに最大まで引いていた場合でも、
        * 極端に長い待ち時間にならないよう調整する。
        */
        float remainingProgress =
            1f - startProgress;

        float duration =
            Mathf.Max(
                0.01f,
                selectedMoveDuration *
                remainingProgress
            );

        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime +=
                Time.deltaTime;

            float normalizedTime =
                Mathf.Clamp01(
                    elapsedTime /
                    duration
                );

            float easedTime =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    normalizedTime
                );

            currentPullAmount =
                Mathf.Lerp(
                    startProgress,
                    1f,
                    easedTime
                );

            ApplyPullPose(
                currentPullAmount
            );

            transform.localScale =
                Vector3.Lerp(
                    startScale,
                    targetScale,
                    easedTime
                );

            yield return null;
        }

        currentPullAmount = 1f;

        bonePullController.ApplyPullAmount(
            1f
        );

        /*
        * Boneで作った最終見た目と同じ位置へ
        * Paper_Red本体を移動し、Boneを通常姿勢へ戻す。
        *
        * 見た目上の移動は発生しない。
        */
        bonePullController.CommitTargetPose();

        transform.localScale =
            targetScale;

        moveCoroutine = null;

        onCompleted?.Invoke();
    }

    public void StartFloating()
    {
        StopFloating();

        floatingBasePosition =
            transform.position;

        floatingBaseRotation =
            transform.rotation;

        floatingElapsedTime = 0f;
        floatingLiftOffset = 0f;

        floatingCoroutine =
            StartCoroutine(
                FloatingCoroutine()
            );
    }
    public void StopFloating()
    {
        if (floatingCoroutine == null)
        {
            return;
        }

        StopCoroutine(floatingCoroutine);
        floatingCoroutine = null;
    }

    private IEnumerator FloatingCoroutine()
    {
        while (true)
        {
            floatingElapsedTime +=
                Time.deltaTime;

            float wave =
                Mathf.Sin(
                    floatingElapsedTime *
                    floatFrequency *
                    Mathf.PI * 2f
                );

            Vector3 position =
                floatingBasePosition;

            // 通常の浮遊
            position.y +=
                wave *
                floatAmplitude;

            // 浮遊の中心自体を上昇させる
            position.y +=
                floatingLiftOffset;
            
            // 折り畳み中の中央補正
            Vector3 rightDirection =
                targetCamera != null
                    ? targetCamera.transform.right
                    : Vector3.right;

            position +=
                rightDirection *
                foldCenterOffset;
            
            transform.position =
                position;

            transform.rotation =
                floatingBaseRotation *
                Quaternion.Euler(
                    0f,
                    0f,
                    wave *
                    floatRotationAngle
                );

            yield return null;
        }
    }
    
    public IEnumerator PlayPreFoldLift(
        float liftDistance,
        float duration
    )
    {
        duration =
            Mathf.Max(
                0.01f,
                duration
            );

        // 念のため浮遊していなければ開始
        if (floatingCoroutine == null)
        {
            StartFloating();
        }

        float startOffset =
            floatingLiftOffset;

        float targetOffset =
            liftDistance;

        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime +=
                Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsedTime /
                    duration
                );

            float eased =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    t
                );

            floatingLiftOffset =
                Mathf.Lerp(
                    startOffset,
                    targetOffset,
                    eased
                );

            yield return null;
        }

        floatingLiftOffset =
            targetOffset;
    }

    public IEnumerator PlayAbsorbLightOnly(
        Action<float> onProgress,
        float firstPhaseDuration,
        float secondPhaseDuration,
        float firstPhaseEndProgress
    )
    {
        if (absorbLightController == null)
        {
            yield break;
        }

        yield return absorbLightController.PlayAbsorb(
            onProgress,
            firstPhaseDuration,
            secondPhaseDuration,
            firstPhaseEndProgress
        );
    }
    public void SetPreFoldFlash(
        float amount
    )
    {
        if (absorbLightController == null)
        {
            return;
        }

        absorbLightController.SetPreFoldFlash(
            amount
        );
    }

    public void ClearPreFoldFlash()
    {
        if (absorbLightController == null)
        {
            return;
        }

        absorbLightController.ClearPreFoldFlash();
    }
    public IEnumerator ScaleDuringLateFold()
    {
        Vector3 startScale =
            transform.localScale;

        Vector3 endScale =
            startScale *
            foldFinalScaleMultiplier;

        float elapsed = 0f;

        while (elapsed < foldScaleDelay)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        elapsed = 0f;

        while (elapsed < foldScaleDuration)
        {
            elapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed / foldScaleDuration
                );

            float eased =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    t
                );

            transform.localScale =
                Vector3.Lerp(
                    startScale,
                    endScale,
                    eased
                );

            yield return null;
        }

        transform.localScale =
            endScale;
    }
    public void FlashFoldLine()
    {
        Debug.Log("[FoldFlash] ALL", this);
        StartCoroutine(
            FlashFoldLineCoroutine()
        );
    }

    private IEnumerator FlashFoldLineCoroutine()
    {
        const float duration = 0.18f;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed / duration
                );

            float pulse =
                t < 0.35f
                    ? Mathf.SmoothStep(
                        0f,
                        1f,
                        t / 0.35f
                    )
                    : Mathf.SmoothStep(
                        1f,
                        0f,
                        (t - 0.35f) / 0.65f
                    );

            SetPreFoldFlash(
                pulse
            );

            yield return null;
        }

        ClearPreFoldFlash();
    }
    public void FlashFoldStepLine(
        int step
    )
    {
        Debug.Log($"[FoldFlash] STEP {step}", this);
        if (absorbLightController == null)
        {
            return;
        }

        if (step < 2 || step > 9)
        {
            Debug.LogWarning(
                $"FlashFoldLine: step={step} は範囲外です",
                this
            );
            return;
        }

        if (foldStepFlashCoroutine != null)
        {
            StopCoroutine(
                foldStepFlashCoroutine
            );
        }

        foldStepFlashCoroutine =
            StartCoroutine(
                FlashFoldLineCoroutine(step)
            );
    }
    private IEnumerator FlashFoldLineCoroutine(
        int step
    )
    {
        // FoldStepMask側と同じ値
        //
        // step 2 → 0.12
        // step 3 → 0.24
        // ...
        // step 9 → 0.96
        float stepValue =
            0.12f * (step - 1);

        float duration =
            Mathf.Max(
                0.01f,
                foldStepFlashDuration
            );

        // Step 3は折り畳み中に見失いやすいため、
        // ほかのStepより少し長く表示する。
        if (step == 3)
        {
            duration = Mathf.Max(
                duration,
                0.32f
            );
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed / duration
                );

            float intensity;

            if (step == 3 && t < 0.2f)
            {
                intensity =
                    Mathf.SmoothStep(
                        0f,
                        1f,
                        t / 0.2f
                    );
            }
            else if (step == 3 && t < 0.55f)
            {
                intensity = 1f;
            }
            else if (step == 3)
            {
                intensity =
                    Mathf.SmoothStep(
                        1f,
                        0f,
                        (t - 0.55f) / 0.45f
                    );
            }
            else if (t < 0.3f)
            {
                intensity =
                    Mathf.SmoothStep(
                        0f,
                        1f,
                        t / 0.3f
                    );
            }
            else
            {
                intensity =
                    Mathf.SmoothStep(
                        1f,
                        0f,
                        (t - 0.3f) / 0.7f
                    );
            }

            absorbLightController.SetFoldStepFlash(
                stepValue,
                intensity
            );

            yield return null;
        }

        absorbLightController.ClearFoldStepFlash();

        foldStepFlashCoroutine = null;
    }

}
