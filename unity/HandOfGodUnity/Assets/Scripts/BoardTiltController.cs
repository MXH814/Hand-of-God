using HandOfGod.Gestures;
using UnityEngine;

namespace HandOfGod.Gameplay
{
    public sealed class BoardTiltController : MonoBehaviour
    {
        [SerializeField] private GestureUdpReceiver receiver;
        [SerializeField] private BallController ball;
        [SerializeField] private Transform boardVisualRoot;
        [SerializeField] private bool mirrorRoll = true;
        [SerializeField] private float maxBoardLeanDegrees = 10f;
        [SerializeField] private float pitchSensitivity = 1.65f;
        [SerializeField] private bool allowKeyboardFallback = true;

        private readonly OneEuroFilter rollFilter = new OneEuroFilter();
        private readonly OneEuroFilter pitchFilter = new OneEuroFilter();
        private float roll;
        private float pitch;
        private bool keyboardActive;

        public bool InputReady => keyboardActive || (receiver != null && receiver.HasFreshFrame);
        public float Roll => roll;
        public float Pitch => pitch;

        public void Configure(GestureUdpReceiver gestureReceiver, BallController controlledBall, Transform visualRoot)
        {
            receiver = gestureReceiver;
            ball = controlledBall;
            boardVisualRoot = visualRoot;
        }

        private void Update()
        {
            var targetRoll = 0f;
            var targetPitch = 0f;
            keyboardActive = false;

            if (receiver != null && receiver.HasFreshFrame)
            {
                var frame = receiver.Latest;
                targetRoll = mirrorRoll ? -frame.roll : frame.roll;
                targetPitch = frame.pitch * pitchSensitivity;
            }
            else if (allowKeyboardFallback)
            {
                targetRoll = Input.GetAxisRaw("Horizontal");
                targetPitch = Input.GetAxisRaw("Vertical") * pitchSensitivity;
                keyboardActive = Mathf.Abs(targetRoll) > 0.01f || Mathf.Abs(targetPitch) > 0.01f;
            }

            var rate = 1.0 / Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            roll = Mathf.Clamp((float)rollFilter.Filter(targetRoll, rate), -1f, 1f);
            pitch = Mathf.Clamp((float)pitchFilter.Filter(targetPitch, rate), -1f, 1f);

            if (boardVisualRoot != null)
            {
                boardVisualRoot.localRotation = Quaternion.Euler(pitch * maxBoardLeanDegrees, 0f, -roll * maxBoardLeanDegrees);
            }

            // Level 01 no longer lets hand tilt or push the ball directly.
        }

        private void OnGUI()
        {
            const int width = 340;
            GUILayout.BeginArea(new Rect(22, 22, width, 160), GUI.skin.box);
            GUILayout.Label("Hand of God - Unity Prototype");
            GUILayout.Label(InputReady ? "Input: gesture bridge / keyboard ready" : "Input: waiting for calibrated camera bridge");
            GUILayout.Label($"Tilt X/Z: {roll:0.00}, {pitch:0.00}");
            if (ball != null)
            {
                GUILayout.Label(ball.ReachedGoal ? "Goal reached" : $"Ball speed: {ball.Speed:0.00}");
            }
            GUILayout.Label("Fallback: WASD / Arrow keys");
            GUILayout.EndArea();
        }
    }
}
