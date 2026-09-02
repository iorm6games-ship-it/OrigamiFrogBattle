using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

public sealed class CrystalInnerSparkleController : MonoBehaviour
{
    private static readonly int Position1Id =
        Shader.PropertyToID("_InnerSparklePosition1");
    private static readonly int Strength1Id =
        Shader.PropertyToID("_InnerSparkleStrength1");
    private static readonly int Position2Id =
        Shader.PropertyToID("_InnerSparklePosition2");
    private static readonly int Strength2Id =
        Shader.PropertyToID("_InnerSparkleStrength2");
    
    [Header("Reference")]
    [SerializeField]
    private Renderer targetRenderer;

    [Header("Position Range")]
    [SerializeField]
    private Vector2 xRange = new Vector2(-0.003f, 0.003f);
    
    [SerializeField]
    private Vector2 yRange = new Vector2(-0.004f, 0.004f);

    [Header("Timing")]
    [SerializeField]
    private Vector2 waitRange = new Vector2(0.5f, 1.8f);

    [SerializeField]
    private float fadeInDuration = 0.06f;

    [SerializeField]
    private float holdDuration = 0.04f;

    [SerializeField]
    private float fadeOutDuration = 0.18f;

    [Header("Strength")]
    [SerializeField]
    private Vector2 strengthRange = new Vector2(1.5f, 3f);

    private MaterialPropertyBlock propertyBlock;

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        StartCoroutine(SparkleLoop(
            Position1Id,
            Strength1Id
        ));
        StartCoroutine(SparkleLoop(
            Position2Id,
            Strength2Id
        ));
    }

    private IEnumerator SparkleLoop(
        int positionId,
        int strengthId
    )
    {
        while (true)
        {
            yield return new WaitForSeconds(
                UnityEngine.Random.Range(
                    waitRange.x,
                    waitRange.y
                ));
            Vector3 position = new Vector3(
                UnityEngine.Random.Range(xRange.x, xRange.y),
                UnityEngine.Random.Range(yRange.x, yRange.y),
                0f);
            
            float peakStrength =
                UnityEngine.Random.Range(
                    strengthRange.x,
                    strengthRange.y);
            SetPosition(positionId, position);

            yield return AnimateStrength(
                strengthId,
                0f,
                peakStrength,
                fadeInDuration);
            
            yield return new WaitForSeconds(holdDuration);

            yield return AnimateStrength(
                strengthId,
                peakStrength,
                0f,
                fadeOutDuration);
        }
    }

    private IEnumerator AnimateStrength(
        int propertyId,
        float from,
        float to,
        float duration
    )
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);

            float eased =
                1f - Mathf.Pow(1f -t, 3f);
            
            SetFloat(
                propertyId,
                Mathf.Lerp(from, to, eased));
            yield return null;
        }
        SetFloat(propertyId, to);
    }

    private void SetPosition(
        int propertyId,
        Vector3 value
    )
    {
        targetRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetVector(propertyId, value);
        targetRenderer.SetPropertyBlock(propertyBlock);
    }

    private void SetFloat(
        int propertyId,
        float value
    )
    {
        targetRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(propertyId, value);
        targetRenderer.SetPropertyBlock(propertyBlock);
    }
}