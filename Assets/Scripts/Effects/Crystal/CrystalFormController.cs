using UnityEngine;

public sealed class CrystalFormController : MonoBehaviour
{
    private static readonly int FormProgressId =
        Shader.PropertyToID("_FormProgress");

    [Header("Reference")]
    [SerializeField]
    private Renderer targetRenderer;

    [Header("State")]
    [SerializeField]
    private bool hideOnAwake = true;

    private MaterialPropertyBlock propertyBlock;

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();

        if (hideOnAwake)
        {
            SetFormProgress(0f);
            SetRendererVisible(false);
        }
    }

    public void ResetForm()
    {
        SetRendererVisible(false);
        SetFormProgress(0f);
    }

    public void BeginForm()
    {
        SetRendererVisible(true);
        SetFormProgress(0f);
    }

    public void SetProgress(float value)
    {
        SetFormProgress(Mathf.Clamp01(value));
    }

    public void CompleteForm()
    {
        SetRendererVisible(true);
        SetFormProgress(1f);
    }

    private void SetRendererVisible(bool visible)
    {
        if (targetRenderer == null)
        {
            return;
        }

        targetRenderer.enabled = visible;
    }

    private void SetFormProgress(float value)
    {
        if (targetRenderer == null)
        {
            return;
        }

        targetRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(FormProgressId, value);
        targetRenderer.SetPropertyBlock(propertyBlock);
    }
}