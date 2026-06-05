using UnityEngine;

namespace HandOfGod.Gameplay
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class BallController : MonoBehaviour
    {
        [SerializeField] private Transform goal;
        [SerializeField] private float goalRadius = 0.55f;
        [SerializeField] private float resetY = -2f;
        [SerializeField] private float maxSpeed = 8f;

        private Rigidbody body;
        private Vector3 startPosition;
        private bool reachedGoal;

        public bool ReachedGoal => reachedGoal;
        public float Speed => body.linearVelocity.magnitude;

        public void Configure(Transform goalTransform)
        {
            goal = goalTransform;
        }

        public void ResetBall()
        {
            reachedGoal = false;
            transform.position = startPosition;
            transform.rotation = Quaternion.identity;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.Sleep();
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            startPosition = transform.position;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        private void FixedUpdate()
        {
            if (reachedGoal)
            {
                body.linearVelocity *= 0.92f;
                body.angularVelocity *= 0.92f;
                return;
            }

            if (body.linearVelocity.magnitude > maxSpeed)
            {
                body.linearVelocity = body.linearVelocity.normalized * maxSpeed;
            }

            if (transform.position.y < resetY)
            {
                ResetBall();
            }

            if (goal != null && Vector3.Distance(ProjectXZ(transform.position), ProjectXZ(goal.position)) <= goalRadius)
            {
                reachedGoal = true;
            }
        }

        private static Vector3 ProjectXZ(Vector3 value)
        {
            return new Vector3(value.x, 0f, value.z);
        }
    }
}
