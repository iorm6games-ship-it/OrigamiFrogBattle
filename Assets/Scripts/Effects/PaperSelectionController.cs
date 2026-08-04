using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class PaperSelectionController : MonoBehaviour
{
    [Header("Sequence")]
    [SerializeField]
    private PaperSelectionAppearController appearController;

    [Header("Section Guide")]
    [SerializeField]
    private CanvasGroup guideCanvasGroup;

    [Tooltip("紙が着地してからガイドを出すまでの時間")]
    [Min(0f)]
    [SerializeField]
    private float guideShowDelay = 0.3f;

    [Tooltip("ガイド文のフェード時間")]
    [SerializeField]
    private float guideFadeDuration = 0.35f;

    [Header("Selectable Papers")]
    [SerializeField]
    private PaperPullSelectable[] papers;

    [Header("Selected Paper Motion")]
    [SerializeField]
    private Transform selectedPaperCenterPoint;


    /// <summary>
    /// 紙の選択が確定したときに通知する
    /// 次の召喚演出から、このイベントを利用する
    /// </summary>
    public event Action<PaperPullSelectable> SelectionConfirmed;

    public bool CanSelect {get; private set;}

    public PaperPullSelectable SelectedPaper
    {
        get;
        private set;
    }

    private Coroutine guideCoroutine;

    private void Awake()
    {
        if (appearController == null)
        {
            appearController =
                GetComponent<PaperSelectionAppearController>();
        }

        if (papers == null)
        {
            papers =
                Array.Empty<PaperPullSelectable>();
        }

        foreach (PaperPullSelectable paper in papers)
        {
            if (paper == null)
            {
                continue;
            }

            paper.Initialize(this);

            paper.SetSelectedTarget(
                selectedPaperCenterPoint
            );
        }

        SetGuideImmediately(0f);
        SetPapersInteraction(false);
    }

    private void OnEnable()
    {
        if (appearController != null)
        {
            appearController.AppearCompleted +=
                HandleAppearCompleted;
        }
    }

    private void OnDisable()
    {
        if (appearController != null)
        {
            appearController.AppearCompleted -=
                HandleAppearCompleted;
        }

        if (guideCoroutine != null)
        {
            StopCoroutine(guideCoroutine);
            guideCoroutine = null;
        }
        CanSelect = false;
    }
    private void HandleAppearCompleted()
    {
        SelectedPaper = null;
        CanSelect = false;

        SetPapersInteraction(false);

        if (guideCoroutine != null)
        {
            StopCoroutine(guideCoroutine);
            guideCoroutine = null;
        }

        guideCoroutine =
            StartCoroutine(ShowGuide());
    }
    /// <summary>
    /// この紙が現在引っ張り操作を開始できるか
    /// </summary>
    public bool CanBeginPull(
        PaperPullSelectable paper
    )
    {
        if (!CanSelect || SelectedPaper != null)
        {
            return false;
        }

        return Array.IndexOf(papers, paper) >= 0;
    }

    /// <summary>
    /// 閾値を超えて離された紙を選択確定にする
    /// </summary>
    public void ConfirmSelection(
        PaperPullSelectable selectedPaper
    )
    {
        if (!CanBeginPull(selectedPaper))
        {
            selectedPaper.ReturnToReset();
            return;
        }

        SelectedPaper =
            selectedPaper;

        CanSelect = false;

        foreach (
            PaperPullSelectable paper in papers
        )
        {
            if (paper == null)
            {
                continue;
            }

            if (paper == selectedPaper)
            {
                paper.LockAsSelected();
            }
            else
            {
                paper.FadeOutAsUnselected();
            }
        }

        if (guideCoroutine != null)
        {
            StopCoroutine(
                guideCoroutine
            );
        }

        guideCoroutine =
            StartCoroutine(
                HideGuide()
            );

        selectedPaper.MoveToSelectedPosition(
            selectedPaperCenterPoint,
            () =>
            {
                Debug.Log(
                    $"選択された紙: "
                    + $"{selectedPaper.ColorName}",
                    selectedPaper
                );

                SelectionConfirmed?.Invoke(
                    selectedPaper
                );
            }
        );
    }
    private void HandleApearCompleted()
    {
        SelectedPaper = null;
        CanSelect = false;

        SetPapersInteraction(false);

        if (guideCoroutine != null)
        {
            StopCoroutine(guideCoroutine);
        }

        guideCoroutine = StartCoroutine(ShowGuide());
    }

    private IEnumerator ShowGuide()
    {
        SetGuideImmediately(0f);

        if (guideShowDelay > 0f)
        {
            yield return new WaitForSeconds(
                guideShowDelay
            );
        }

        yield return FadeGuide(1f);

        CanSelect = true;
        SetPapersInteraction(true);

        guideCoroutine = null;
    }
    private IEnumerator HideGuide()
    {
        yield return FadeGuide(0f);

        guideCoroutine = null;
    }
    private IEnumerator FadeGuide(
        float targetAlpha
    )
    {
        if (guideCanvasGroup == null)
        {
            yield break;
        }
        float startAlpha =
            guideCanvasGroup.alpha;

        float elapsedTime = 0f;

        while (elapsedTime < guideFadeDuration)
        {
            elapsedTime += Time.deltaTime;

            float normalizedTime =
                Mathf.Clamp01(
                    elapsedTime /
                        guideFadeDuration
                );
            float easedTime =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    normalizedTime
                );
            
            guideCanvasGroup.alpha = 
                Mathf.Lerp(
                    startAlpha,
                    targetAlpha,
                    easedTime
                );
            yield return null;
        }
        guideCanvasGroup.alpha = targetAlpha;
    }

    private void SetGuideImmediately(
        float alpha
    )
    {
        if (guideCanvasGroup == null)
        {
            return;
        }
        guideCanvasGroup.alpha = alpha;

        guideCanvasGroup.interactable = false;
        guideCanvasGroup.blocksRaycasts = false;
    }

    private void SetPapersInteraction(
        bool isEnabled
    )
    {
        foreach (PaperPullSelectable paper in papers)
        {
            if (paper != null)
            {
                paper.SetInteractionEnabled(
                    isEnabled
                );
            }
        }
    }

}