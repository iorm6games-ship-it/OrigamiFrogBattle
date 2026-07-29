using System;
using System.Collections;
using System.ComponentModel;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
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

    [Tooltip("紙を下へ動かす最大距離")]
    [Min(0.01f)]
    [SerializeField]
    private float maxPullDistance = 0.55f;

    [Tooltip("この割合以上引っ張って離すと選択確定")]
    [Range(0.1f, 1f)]
    [SerializeField]
    private float confirmThreshold = 0.65f;

    [Tooltip(
        "引っ張った紙をカメラ側へ出すZ差" +
        "現在のシーンではマイナス側が手前"
    )]
    [SerializeField]
    private float frontZOffset = -0.08f;

    [Tooltip("最大まで引いた時の拡大率")]
    [Min(1f)]
    [SerializeField]
    private float pullScaleMultiplier = 1.05f;

    [Header("Return Motion")]
    [Tooltip("選択されなかったばあいに戻る時間")]
    [Min(0.01f)]
    [SerializeField]
    private float returnDuration = 0.18f;

    public string ColorName => colorName;

    public SkinnedMeshRenderer TargetRenderer =>
        targetRenderer;
    public Transform LightTarget =>
        lightTarget;
    public Transform PaperTransform =>
        transform;
    
    private PaperSelectionController owner;
    private BoxCollider hitCollider;

    private bool interactionEnabled;
    private bool dragging;

    private float pressScreenY;
    private float currentPullAmount;

    private Vector3 restLocalPosition;
    private Vector3 restLocalScale;

    private Coroutine moveCoroutine;

    private void Awake()
    {
        EnsureReferences();
    }

    public void Initialize(
        PaperSelectionController selectionController
    )
    {
        owner = selectionController;
        EnsureReferences();
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

        dragging = true;
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
        currentPullAmount =
            Mathf.Clamp01(
                draggedPixels /
                maxDragPixels
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
        Vector3 position =
            restLocalPosition +
            Vector3.down *
            (maxPullDistance * pullAmount);
        position.z +=
            frontZOffset *
            pullAmount;

        transform.localPosition = position;

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
                    elapsedTime / returnDuration
                );
            
            float easedTime =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    normalizedTime
                );
            
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

            yield return null;
        }
        transform.localPosition = 
            restLocalPosition;
        transform.localScale =
            restLocalScale;

        currentPullAmount = 0f;
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
        if (hitCollider = null)
        {
            hitCollider =
                GetComponent<BoxCollider>();
        }

        if (targetRenderer == null)
        {
            targetRenderer =
                GetComponent<SkinnedMeshRenderer>();
        }
    }




}