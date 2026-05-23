using System;
using UnityEngine;

public class FlipCheck : MonoBehaviour
{
    [SerializeField] private Rigidbody rb;

    [Header("Stop Check")]
    [SerializeField] private float velocityThreshold = 0.06f;
    [SerializeField] private float angularVelocityThreshold = 0.05f;
    [SerializeField] private float stopRequiredTime = 1.0f;

    [Header("Win Check")]
    [SerializeField] private Transform topReference;
    
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip landingSE;

    [Header("Back KO Check")]
    [SerializeField] private Transform backReference;
    [SerializeField] private float backTouchThreshold = 0.75f;

    private float stoppedTimer = 0f;

    private bool isGrounded = false;
    private bool canJudge = false;
    public bool isContact = false;

    public bool HasStopped { get; private set; } = false;
    private bool hasLeftGroundAfterJump = false;
    private float landingSoundCooldown = 0.15f;
    private float lastLandingSoundTime = -999f;
    public bool HasEverGroundedAfterJump { get; private set; } = false;
    public bool HasPlayerContacted { get; private set; } = false;

    void Start()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }
    }
    // Update is called once per frame
    void Update()
    {
        if (!canJudge) return;
        if (GameManager.currentState == GameManager.GameState.GameOver) return;
        if (HasStopped) return;
        if (!isGrounded && !HasPlayerContacted) return;

        float velMag = rb.linearVelocity.magnitude;
        float angVelMag = rb.angularVelocity.magnitude;

        bool stopped =
            velMag < velocityThreshold &&
            angVelMag < angularVelocityThreshold;

        if (stopped)
        {
            stoppedTimer += Time.deltaTime;

            if (stoppedTimer > stopRequiredTime)
            {
                HasStopped = true;
                Debug.Log($"{gameObject.name}: Stopped! vel={velMag:F4} angvel={angVelMag:F4}");
            }
        }
        else
        {
            if (stoppedTimer > 0f)
            {
                Debug.Log($"{gameObject.name}: Moving again. vel={velMag:F4} (threshold={velocityThreshold}) angvel={angVelMag:F4} (threshold={angularVelocityThreshold}) timer={stoppedTimer:F2}");
            }
            stoppedTimer = 0f;
        }
    }
    public void StartJudge()
    {
        canJudge = true;
        HasStopped = false;
        stoppedTimer = 0f;
        isContact = false;
        hasLeftGroundAfterJump = false;
        HasPlayerContacted = false;
        HasEverGroundedAfterJump = false;
    }


    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = false;
            if (canJudge)
            {
                hasLeftGroundAfterJump = true;
            }
        }

    }

    private void OnCollisionEnter(Collision collision)
    {

        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
            
            if (canJudge && hasLeftGroundAfterJump)
            {
                HasEverGroundedAfterJump = true;
                PlayLandingSound(collision);
                hasLeftGroundAfterJump= false;
            }
        }

        if (collision.gameObject.CompareTag("Player"))
        {
            HasPlayerContacted = true;
        }
    }
    public void ResetCheck()
    {
        isGrounded = false;
        canJudge = false;
        stoppedTimer = 0f;
        HasStopped = false;
        isContact = false;
        HasPlayerContacted = false;
        HasEverGroundedAfterJump= false;
    }

    private void PlayLandingSound(Collision collision)
    {
        if (audioSource == null || landingSE == null) return;

        if (Time.time - lastLandingSoundTime < landingSoundCooldown) return;

        float impact = collision.relativeVelocity.magnitude;

        float volume = Mathf.Clamp01(impact / 5f);
        volume = Mathf.Clamp(volume, 0.25f, 1f);

        audioSource.pitch = UnityEngine.Random.Range(0.95f, 1.05f);
        audioSource.PlayOneShot(landingSE, volume);

        lastLandingSoundTime = Time.time;
        
    }

    public bool IsBackDown()
    {
        Transform reference = backReference != null ? backReference : transform;
        float backUp = Vector3.Dot(reference.up, Vector3.up);

        return backUp < -backTouchThreshold;
    }
}
