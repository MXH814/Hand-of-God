using UnityEngine;

namespace HandOfGod.Gameplay
{
    public sealed class LevelOneHud : MonoBehaviour
    {
        [SerializeField] private BallController ball;
        [SerializeField] private GestureBoxInteractor boxInteractor;

        public void Configure(BallController controlledBall, GestureBoxInteractor interactor)
        {
            ball = controlledBall;
            boxInteractor = interactor;
        }

        private void OnGUI()
        {
            var panelWidth = 360;
            GUILayout.BeginArea(new Rect(20, 20, panelWidth, 150), GUI.skin.box);
            GUILayout.Label("Hand of God - Level 01");
            if (ball != null && ball.ReachedGoal)
            {
                GUILayout.Label("PASS: The ball reached the altar.");
            }
            else if (boxInteractor != null && boxInteractor.Held)
            {
                GUILayout.Label("Pinch: moving the obstacle box");
            }
            else if (boxInteractor != null && boxInteractor.Hovering)
            {
                GUILayout.Label("Pinch now to pick up the glowing box");
            }
            else
            {
                GUILayout.Label("Goal: pinch and move the box away");
            }

            if (ball != null)
            {
                GUILayout.Label($"Ball speed: {ball.Speed:0.00}");
            }
            GUILayout.Label("Fallback: drag box with left mouse button");
            GUILayout.EndArea();
        }
    }
}
