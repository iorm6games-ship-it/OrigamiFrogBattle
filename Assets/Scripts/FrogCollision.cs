using System.Collections;
using UnityEngine;


public class FrogCollision: MonoBehaviour
{
    [SerializeField] private Rigidbody rb;

    [Header("Thresholds")]
    [SerializeField] private float minImpact = 1.5f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip weakHitClip;
    [SerializeField] private AudioClip strongHitClip;

    [Header("Volume")]
    [SerializeField] private float weakHitVolume = 0.15f;
    [SerializeField] private float strongHitVolume = 0.85f;

    [Header("Hit Stop")]
    [SerializeField] private float weakDuration = 0.06f;
    [SerializeField] private float strongDuration = 0.12f;

    private void OnCollisionEnter(Collision collision)
    {
        
        if (!collision.gameObject.CompareTag("Player")) return;
        if (HitStopManager.Instance == null) return;
        float relativeImpact = collision.relativeVelocity.magnitude;
        float selfSpeed = rb.linearVelocity.magnitude;

        // 接触点の法線。相手から自分側へ押し返す向き
        Vector3 normal = collision.contacts[0].normal;

        // 自分の進行方向
        Vector3 selfDirection = rb.linearVelocity.normalized;

        // 自分がどれだけ正面から突っ込んだか
        float frontHitRate = Vector3.Dot(-selfDirection, normal);

        if (relativeImpact < minImpact) return;

        bool isStrongHit =
            relativeImpact >= 3.0f ||
            selfSpeed >= 3.0f ||
            frontHitRate >= 0.6f;

        float duration = isStrongHit ? strongDuration : weakDuration;

        // ヒット音追加
        AudioClip clip = isStrongHit ? strongHitClip : weakHitClip;
        float volume = isStrongHit ? strongHitVolume : weakHitVolume;
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip, volume);
        }
        
        HitStopManager.Instance.HitStop(duration);
    }

}

