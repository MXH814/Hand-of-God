using HandOfGod.Gestures;
using UnityEngine;

namespace HandOfGod.Gameplay
{
    public sealed class GestureBoxInteractor : MonoBehaviour
    {
        [SerializeField] private GestureUdpReceiver receiver;
        [SerializeField] private Rigidbody boxBody;
        [SerializeField] private Renderer boxRenderer;
        [SerializeField] private Material idleMaterial;
        [SerializeField] private Material hoverMaterial;
        [SerializeField] private Material heldMaterial;
        [SerializeField] private Vector2 xRange = new Vector2(-1.2f, 1.2f);
        [SerializeField] private Vector2 zRange = new Vector2(-1.25f, 1.25f);
        [SerializeField] private float roadCenterY = 1f;
        [SerializeField] private float roadAngleDegrees = -8f;
        [SerializeField] private float boxHeight = 0.74f;
        [SerializeField] private float acquireRadius = 0.72f;
        [SerializeField] private float smoothTime = 0.055f;
        [SerializeField] private bool allowMouseFallback = true;

        private Vector3 smoothVelocity;
        private bool held;
        private bool hovering;

        public bool Held => held;
        public bool Hovering => hovering;

        public void Configure(
            GestureUdpReceiver gestureReceiver,
            Rigidbody movableBox,
            Renderer movableBoxRenderer,
            Material idle,
            Material hover,
            Material heldMat,
            float centerY,
            float angleDegrees)
        {
            receiver = gestureReceiver;
            boxBody = movableBox;
            boxRenderer = movableBoxRenderer;
            idleMaterial = idle;
            hoverMaterial = hover;
            heldMaterial = heldMat;
            roadCenterY = centerY;
            roadAngleDegrees = angleDegrees;
        }

        private void Update()
        {
            if (boxBody == null)
            {
                return;
            }

            var hasPointer = TryGetPointer(out var target, out var pinch);
            hovering = hasPointer && DistanceOnRoad(target, boxBody.position) <= acquireRadius;

            if (!held && pinch && hovering)
            {
                held = true;
                smoothVelocity = Vector3.zero;
            }
            else if (held && !pinch)
            {
                held = false;
            }

            if (held)
            {
                var clamped = ClampToInteractionRange(target);
                boxBody.MovePosition(Vector3.SmoothDamp(boxBody.position, clamped, ref smoothVelocity, smoothTime));
                boxBody.linearVelocity = Vector3.zero;
                boxBody.angularVelocity = Vector3.zero;
            }

            UpdateMaterial();
        }

        private bool TryGetPointer(out Vector3 target, out bool pinch)
        {
            if (receiver != null && receiver.HasFreshFrame)
            {
                var frame = receiver.Latest;
                target = MapPinchToWorld(frame.pinchX, frame.pinchY);
                pinch = frame.pinch && frame.confidence > 0.35f;
                return true;
            }

            if (allowMouseFallback)
            {
                target = MapPinchToWorld(Input.mousePosition.x / Screen.width, Input.mousePosition.y / Screen.height);
                pinch = Input.GetMouseButton(0);
                return true;
            }

            target = boxBody.position;
            pinch = false;
            return false;
        }

        private Vector3 MapPinchToWorld(float normalizedX, float normalizedY)
        {
            var x = Mathf.Lerp(xRange.x, xRange.y, Mathf.Clamp01(normalizedX));
            var z = Mathf.Lerp(zRange.y, zRange.x, Mathf.Clamp01(normalizedY));
            return ClampToInteractionRange(new Vector3(x, RoadY(x) + boxHeight * 0.5f, z));
        }

        private Vector3 ClampToInteractionRange(Vector3 value)
        {
            var x = Mathf.Clamp(value.x, xRange.x, xRange.y);
            var z = Mathf.Clamp(value.z, zRange.x, zRange.y);
            return new Vector3(x, RoadY(x) + boxHeight * 0.5f, z);
        }

        private float RoadY(float x)
        {
            return roadCenterY + x * Mathf.Sin(roadAngleDegrees * Mathf.Deg2Rad);
        }

        private static float DistanceOnRoad(Vector3 a, Vector3 b)
        {
            var delta = a - b;
            delta.y = 0f;
            return delta.magnitude;
        }

        private void UpdateMaterial()
        {
            if (boxRenderer == null)
            {
                return;
            }

            if (held && heldMaterial != null)
            {
                boxRenderer.sharedMaterial = heldMaterial;
            }
            else if (hovering && hoverMaterial != null)
            {
                boxRenderer.sharedMaterial = hoverMaterial;
            }
            else if (idleMaterial != null)
            {
                boxRenderer.sharedMaterial = idleMaterial;
            }
        }
    }
}
