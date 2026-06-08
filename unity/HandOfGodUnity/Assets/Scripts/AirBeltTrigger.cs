using UnityEngine;

public class AirBeltTrigger : MonoBehaviour
{
    public int beltIndex = 0;
    public int direction = 0; // -1 left, 1 right
    public float force = 6f;
    public float maxWindSpeed = 2.0f;
    public float rampSeconds = 1.45f;

    private float strength;
    private int lastDirection;

    private void OnTriggerStay(Collider other)
    {
        if (direction == 0)
        {
            strength = Mathf.MoveTowards(strength, 0f, Time.fixedDeltaTime * 2f);
            lastDirection = 0;
            return;
        }

        var rb = other.attachedRigidbody;
        if (rb == null) return;

        if (lastDirection != direction)
        {
            strength = 0.12f;
            lastDirection = direction;
        }

        var ramp = Mathf.Max(rampSeconds, 0.1f);
        strength = Mathf.MoveTowards(strength, 1f, Time.fixedDeltaTime / ramp);
        rb.AddForce(new Vector3(direction * force * strength, 0f, 0f), ForceMode.Acceleration);

        var velocity = rb.linearVelocity;
        var directionalSpeed = velocity.x * direction;
        if (directionalSpeed > maxWindSpeed)
        {
            velocity.x = direction * maxWindSpeed;
            rb.linearVelocity = velocity;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.attachedRigidbody != null) strength = Mathf.Min(strength, 0.18f);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.attachedRigidbody != null) strength = 0f;
    }
}
