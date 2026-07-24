using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TitleTransitionController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private CanvasGroup[] uiGroups;

    [Header("3D Objects")]
    [SerializeField] private GameObject[] fadeObjects;

    [Header("Settings")]
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.8f;

    [Header("PaperSelection")]
    [SerializeField]
    private GameObject paperSelectionRoot;

    private List<Vector3> startScales = new();
    private Button playButton;
    private bool isTransitioning;

    private void Awake()
    {
        CollectStartScales();
        if (paperSelectionRoot != null)
        {
            paperSelectionRoot.SetActive(false);
        }
        
        // CollectMaterials();
        // fadeObjects のマテリアルを取得
        foreach (CanvasGroup group in uiGroups)
        {
            if (group == null) continue;

            Button button = group.GetComponent<Button>();

            if (button != null)
            {
                playButton = button;
                break;
            }   
        }
    }
    public void StartTitleTransition()
    {
        if (isTransitioning)
            return;
        
        StartCoroutine(FadeOutTitle());
    }
    private IEnumerator FadeOutTitle()
    {
        isTransitioning = true;
        if (playButton != null)
        {
            playButton.interactable = false;
        }
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(elapsed / fadeDuration);
            float alpha = 1f - progress;

            SetUIAlpha(alpha);
            SetObjectScale(progress);

            yield return null;
        }

        SetUIAlpha(0f);
        SetObjectScale(1f);

        DisableFadeObjects();
        if (paperSelectionRoot != null)
        {
            paperSelectionRoot.SetActive(true);
        }
    }

    private void CollectStartScales()
    {
        startScales.Clear();
        foreach (GameObject target in fadeObjects)
        {
            if (target == null)
            {
                startScales.Add(Vector3.one); // デフォルトのスケールを追加
                continue; 
            }

            Transform transform = target.transform;
            startScales.Add(transform.localScale);
        }
    }

    private void SetUIAlpha(float alpha)
    {
        foreach (CanvasGroup group in uiGroups)
        {
            if (group == null) continue;

            group.alpha = alpha;
            group.interactable = alpha > 0.99f;
            group.blocksRaycasts = alpha > 0.99f;
        }
    }

    private void SetObjectScale(float progress)
    {
        for (int i = 0; i < fadeObjects.Length; i++)
        {
            GameObject target = fadeObjects[i];
            if (target == null) continue;

            target.transform.localScale = 
                Vector3.Lerp(startScales[i], Vector3.zero, progress);
        }
    }

    private void DisableFadeObjects()
    {
        foreach (CanvasGroup group in uiGroups)
        {
            if (group != null)
            {
                group.gameObject.SetActive(false);
            }
        }

        foreach (GameObject target in fadeObjects)
        {
            if (target != null)
                target.SetActive(false);
        }
    }
}