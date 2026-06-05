using HandOfGod.Gestures;
using UnityEngine;

namespace HandOfGod.Gameplay
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        public void BuildGameWorld()
        {
            Physics.gravity = new Vector3(0f, -9.81f, 0f);
            if (!Application.isPlaying)
            {
                RemoveLegacyRoots();
            }
            EnsureGestureGameController();
        }

        private void Awake()
        {
            Physics.gravity = new Vector3(0f, -9.81f, 0f);
            EnsureGestureGameController();
        }

        private void EnsureGestureGameController()
        {
            var receiver = GetComponent<GestureUdpReceiver>();
            if (receiver == null)
            {
                receiver = gameObject.AddComponent<GestureUdpReceiver>();
            }

            var controller = GetComponent<GestureGameController>();
            if (controller == null)
            {
                controller = gameObject.AddComponent<GestureGameController>();
            }
            controller.Configure(receiver);
            controller.InitializeForScene();
        }

        private static void RemoveLegacyRoots()
        {
            DestroyNamed("Level01 Ramp - Collision");
            DestroyNamed("Level01 Ramp - Art");
            DestroyNamed("Level01 Ramp - Gameplay");
            DestroyNamed("Level00 Gesture Lab");
            DestroyNamed("Level01 First Path");
            DestroyNamed("Menu Temple Lobby");
            DestroyNamed("Main Camera");
            DestroyNamed("Key Light");
            DestroyNamed("Temple Fill Light");
            DestroyNamed("Goal Light");
        }

        private static void DestroyNamed(string objectName)
        {
            var existing = GameObject.Find(objectName);
            if (existing != null)
            {
                DestroyUnityObject(existing);
            }
        }

        private static void DestroyUnityObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
