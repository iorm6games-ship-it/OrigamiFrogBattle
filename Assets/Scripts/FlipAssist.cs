using UnityEditor.ShaderGraph;
using UnityEngine;

public class FlipAssist : MonoBehaviour
{
    private Rigidbody rb;

    public float torquePower = 1f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.name == "Cube")
        {
            rb.AddTorque(Vector3.forward * torquePower, ForceMode.Impulse);
        }
    }
    
}
