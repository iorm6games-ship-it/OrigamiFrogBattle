using Unity.VisualScripting;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CrystalFinalPose : MonoBehaviour
{
    [Header("Final Crystal Pose")]

    [Tooltip("完成Crystalのサイズ倍率、Center は 1、Corner は、0.85～0.90程度を想定。")]
    [Range(0.5f, 1.2f)]
    [SerializeField]
    private float scaleMultiplier = 1f;

    [Tooltip("Assembly 完了時の向きを基準に追加する回転")]
    [SerializeField]
    private Vector3 rotationOffsetEuler = Vector3.zero;

    public float ScaleMultiplier =>
        scaleMultiplier;
    
    public Quaternion RotationOffset =>
        Quaternion.Euler(rotationOffsetEuler);

    private void OnValidate()
    {
        scaleMultiplier =
            Mathf.Clamp(
                scaleMultiplier,
                0.5f,
                1.2f
            );
    }

}