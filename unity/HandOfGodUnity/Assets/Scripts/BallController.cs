using UnityEngine;

namespace HandOfGod.Gameplay
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class BallController : MonoBehaviour
    {
        [SerializeField] private Transform goal;
        [SerializeField] private float goalRadius = 0.55f;
        [SerializeField] private float resetY = -2f;
        [SerializeField] private float maxPlanarSpeed = 7f;

        private Rigidbody body;
        private Vector3 startPosition;
        private Vector3 planarAcceleration;
        private bool reachedGoal;

        public bool ReachedGoal => reachedGoal;
        public float Speed => new Vector3(body.velocity.x, 0f, body.velocity.z).magnitude;

        public void Configure(Transform goalTransform)
        {
            goal = goalTransform;
        }

        public void SetPlanarAcceleration(Vector3 acceleration)
        {
            planarAcceleration = new Vector3(acceleration.x, 0f, acceleration.z);
        }

        public void ResetBall()
        {
            reachedGoal = false;
            transform.position = startPosition;
            transform.rotation = Quaternion.identity;
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.Sleep();
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            startPosition = transform.position;
            body.constraints = RigidbodyConstraints.FreezePositionY;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        private void FixedUpdate()
        {
            if (reachedGoal)
            {
                body.velocity *= 0.92f;
                body.angularVelocity *= 0.92f;
                return;
            }

            body.AddForce(planarAcceleration, ForceMode.Acceleration);
            var planarVelocity = new Vector3(body.velocity.x, 0f, body.velocity.z);
            if (planarVelocity.magnitude > maxPlanarSpeed)
            {
                planarVelocity = planarVelocity.normalized * maxPlanarSpeed;
                body.velocity = new Vector3(planarVelocity.x, body.velocity.y, planarVelocity.z);
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
