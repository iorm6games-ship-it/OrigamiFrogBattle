
using UnityEngine;

public class GameReset : MonoBehaviour
{
    
    public Rigidbody frogA;
    public Rigidbody frogB;
    private Vector3 frogAStartPos;
    private Quaternion frogAStartRot;
    private Vector3 frogBStartPos;
    private Quaternion frogBStartRot;
    [SerializeField] private StartCountdown startCountdown;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
    
        frogAStartPos = frogA.transform.position;
        frogAStartRot = frogA.transform.rotation;
        frogBStartPos = frogB.transform.position;
        frogBStartRot = frogB.transform.rotation;

    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetObjects();
        }
    }

    void ResetObjects()
    {
        
        ResetRigidbody(frogA, frogAStartPos, frogAStartRot);
        ResetRigidbody(frogB, frogBStartPos, frogBStartRot);
        GameManager.currentState = GameManager.GameState.Ready;
        GameJudge judge = FindAnyObjectByType<GameJudge>();
        GameManager.NextRound();
        if (judge != null)
        {
            judge.ResetJudge();
        }

        foreach (FlipCheck check in FindObjectsByType<FlipCheck>())
        {
            check.ResetCheck();
        }

        foreach (JumpTest jump in FindObjectsByType<JumpTest>())
        {
            jump.ResetJump();
        }
        startCountdown.ResetCountdown();
    }

    void ResetRigidbody(Rigidbody rb, Vector3 pos, Quaternion rot)
    {
        
        if (rb == null)
        {
            Debug.LogError("Reset対象のRigidbodyが設定されていません。");
        }
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        rb.transform.position = pos;
        rb.transform.rotation = rot;
    }
}
