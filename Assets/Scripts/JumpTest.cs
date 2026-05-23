using UnityEngine;

public class JumpTest : MonoBehaviour
{
    
    public float chargeSpeed = 2f;
    
    public KeyCode jumpKey = KeyCode.Space;
    public Vector3 jumpDirection = Vector3.right;

    [SerializeField] private Transform modelTransform;

    [Header("BlendShape")]
    public SkinnedMeshRenderer frogMesh;
    public int blendShapeIndex = 0;

    private Rigidbody rb;

    private float charge = 0f;
    private bool isCharging = false;
    private bool isGrounded = true;
    private bool hasJumped = false;
    [SerializeField] private FrogSound frogSound;
    private float jumpUpForce = 4f;
    private float jumpForwardForce = 10f;
    private float spinForce = 10f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        
        if (frogMesh == null)
        {
            frogMesh = GetComponentInChildren<SkinnedMeshRenderer>();
        }
        if (modelTransform == null)
        {
            modelTransform = transform.Find("Model");
        }

        SetCrush(0f);
        
    }

    // Update is called once per frame
    void Update()
    {
        // State が GameOver なら終了する
        if (GameManager.currentState == GameManager.GameState.GameOver)
        {
            return;
        }
        // 地面設置判定が false なら終了
        if (!isGrounded)
        {
            return;
        }
        if (hasJumped)
        {
            return;
        }

        if (Input.GetKeyDown(jumpKey))
        {
            isCharging = true;
            charge = 0f;
            frogSound.PlayCrouch();
            SetCrush(0f);
        }

        if (Input.GetKey(jumpKey) && isCharging)
        {
            charge += Time.deltaTime * chargeSpeed;
            charge = Mathf.Clamp01(charge);

            SetCrush(charge);
        }

        if (Input.GetKeyUp(jumpKey) && isCharging)
        {
            
            int jumpLevel = GetJumpLevel(charge);

            frogSound.PlayJump(jumpLevel);
            
            Jump();

            hasJumped = true;
            charge = 0f;
            isCharging = false;
            isGrounded = false;

            SetCrush(0f);

            GameManager.currentState = GameManager.GameState.Playing;
        }
        
    }
    private int GetJumpLevel(float charge)
    {
        if (charge < 0.25f) return 1;
        if (charge < 0.55f) return 2;
        if (charge < 0.8f) return 4;
        return 3;
        
    }
    void Jump()
    {
        float peak = 0.6f;
        float diff = charge - peak;
        float power = 1f - diff * diff * 2.5f;
        power = Mathf.Clamp(power, 0.3f, 1f);
        Vector3 forwardDirection = modelTransform != null
            ? -modelTransform.forward
            : transform.forward;

        forwardDirection.y = 0f;
        forwardDirection.Normalize();

        Vector3 force = Vector3.up * jumpUpForce +
            forwardDirection * jumpForwardForce;

        rb.AddForce(force * power, ForceMode.Impulse);
        Vector3 spinAxis =
            Vector3.Cross(Vector3.up, forwardDirection).normalized;

        rb.AddTorque(spinAxis * spinForce * power, ForceMode.Impulse);
        FlipCheck flipCheck = GetComponent<FlipCheck>();
        if (flipCheck != null)
        {
            flipCheck.StartJudge();
        }

    }


    public void SetCrush(float value01)
    {
        if (frogMesh == null)
        {
            return;
        }

        frogMesh.SetBlendShapeWeight(blendShapeIndex, value01 * 100f);
    }

    void OnCollisionEnter(Collision collision)
    {

        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
        }
    }

    public void ResetJump()
    {
        hasJumped = false;
        isCharging = false;
        isGrounded = true;
        charge = 0f;

        SetCrush(0f);
    }
}
