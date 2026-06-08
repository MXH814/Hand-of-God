using UnityEngine;

public class AirBeltTrigger : MonoBehaviour
{
    public int beltIndex = 0;
    public int direction = 0; // -1 left, 1 right
    // Tunable params: lower force for gentler push on Level2
    public float force = 10f;
    public float boostThreshold = 1.2f;
    public float boostSpeed = 2.2f;

    private void OnTriggerStay(Collider other)
    {
        if (direction == 0) return;

        var rb = other.attachedRigidbody;
        if (rb == null) return;

        Debug.Log($"[AirBelt {beltIndex}] OnTriggerStay: direction={direction}, ballVel={rb.linearVelocity}");
        rb.AddForce(new Vector3(direction * force, 0f, 0f), ForceMode.Acceleration);

        if (rb.linearVelocity.x * direction < boostThreshold)
        {
            rb.linearVelocity = new Vector3(direction * boostSpeed, rb.linearVelocity.y, rb.linearVelocity.z);
            Debug.Log($"[AirBelt {beltIndex}] Boost applied (speed={boostSpeed})!");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.attachedRigidbody != null)
        {
            Debug.Log($"[AirBelt {beltIndex}] OnTriggerEnter: {other.name}, direction={direction}");
        }
    }
}
