using HandOfGod.Gestures;
using UnityEngine;

namespace HandOfGod.Gameplay
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        private Material stone;
        private Material darkStone;
        private Material brass;
        private Material glow;
        private Material ballMaterial;

        private void Awake()
        {
            Physics.gravity = new Vector3(0f, -9.81f, 0f);
            BuildMaterials();
            BuildCameraAndLights();

            var staticRoot = new GameObject("Temple Maze - Static Colliders").transform;
            var visualRoot = new GameObject("Temple Maze - Visual Lean").transform;

            BuildTemple(staticRoot, visualRoot, out var goal);
            var ball = BuildBall();

            var receiver = gameObject.AddComponent<GestureUdpReceiver>();
            var tilt = gameObject.AddComponent<BoardTiltController>();
            tilt.Configure(receiver, ball, visualRoot);
            ball.Configure(goal);
        }

        private void BuildMaterials()
        {
            stone = NewMaterial("Warm carved stone", new Color(0.54f, 0.62f, 0.56f), 0.35f, 0.2f);
            darkStone = NewMaterial("Obsidian floor", new Color(0.05f, 0.08f, 0.07f), 0.7f, 0.1f);
            brass = NewMaterial("Brass rune", new Color(0.96f, 0.65f, 0.16f), 0.5f, 0.3f);
            glow = NewMaterial("Goal glow", new Color(0.18f, 0.95f, 0.77f), 0.2f, 0.75f);
            ballMaterial = NewMaterial("Golden ball", new Color(1f, 0.68f, 0.05f), 0.6f, 0.45f);
        }

        private static Material NewMaterial(string name, Color color, float smoothness, float emission)
        {
            var material = new Material(Shader.Find("Standard"))
            {
                name = name,
                color = color,
            };
            material.SetFloat("_Glossiness", smoothness);
            if (emission > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * emission);
            }
            return material;
        }

        private void BuildCameraAndLights()
        {
            var cameraObject = new GameObject("Main Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.SetPositionAndRotation(new Vector3(0f, 8.8f, -7.4f), Quaternion.Euler(58f, 0f, 0f));
            camera.orthographic = true;
            camera.orthographicSize = 5.8f;
            camera.backgroundColor = new Color(0.015f, 0.025f, 0.025f);

            var sun = new GameObject("Key Light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.25f;
            sun.transform.rotation = Quaternion.Euler(45f, -30f, 20f);

            var fill = new GameObject("Temple Fill Light").AddComponent<Light>();
            fill.type = LightType.Point;
            fill.intensity = 2.6f;
            fill.range = 9f;
            fill.transform.position = new Vector3(0f, 4f, 0f);
        }

        private void BuildTemple(Transform staticRoot, Transform visualRoot, out Transform goal)
        {
            CreateBox("floating base", new Vector3(0f, -0.18f, 0f), new Vector3(10f, 0.35f, 6.2f), darkStone, staticRoot);
            CreateBox("start platform", new Vector3(-4.1f, 0.02f, -1.65f), new Vector3(1.7f, 0.2f, 1.5f), stone, staticRoot);
            CreateBox("goal altar", new Vector3(4.05f, 0.02f, 1.45f), new Vector3(1.55f, 0.2f, 1.55f), stone, staticRoot);

            CreateWall("north rail", new Vector3(0f, 0.55f, -2.95f), new Vector3(9.6f, 0.9f, 0.24f), staticRoot);
            CreateWall("south rail", new Vector3(0f, 0.55f, 2.95f), new Vector3(9.6f, 0.9f, 0.24f), staticRoot);
            CreateWall("west rail", new Vector3(-4.9f, 0.55f, 0f), new Vector3(0.24f, 0.9f, 5.7f), staticRoot);
            CreateWall("east rail", new Vector3(4.9f, 0.55f, 0f), new Vector3(0.24f, 0.9f, 5.7f), staticRoot);

            CreateWall("maze divider A", new Vector3(-2.4f, 0.32f, -0.45f), new Vector3(2.2f, 0.55f, 0.22f), staticRoot);
            CreateWall("maze divider B", new Vector3(0.1f, 0.32f, 1.1f), new Vector3(2.6f, 0.55f, 0.22f), staticRoot);
            CreateWall("maze divider C", new Vector3(2.4f, 0.32f, -0.75f), new Vector3(0.22f, 0.55f, 2.2f), staticRoot);
            CreateWall("goal guard", new Vector3(3.35f, 0.32f, 0.75f), new Vector3(1.7f, 0.55f, 0.22f), staticRoot);

            CreateRuneStrip("rune lane start", new Vector3(-2.7f, 0.16f, -1.65f), new Vector3(1.7f, 0.06f, 0.12f), visualRoot);
            CreateRuneStrip("rune lane middle", new Vector3(0f, 0.16f, -0.15f), new Vector3(1.5f, 0.06f, 0.12f), visualRoot);
            CreateRuneStrip("rune lane goal", new Vector3(3.7f, 0.16f, 1.45f), new Vector3(1.2f, 0.06f, 0.12f), visualRoot);

            CreatePillar(-4.25f, -2.25f, staticRoot);
            CreatePillar(-4.25f, -1.05f, staticRoot);
            CreatePillar(4.25f, 0.85f, staticRoot);
            CreatePillar(4.25f, 2.1f, staticRoot);

            var goalObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            goalObject.name = "glowing goal altar ring";
            goalObject.transform.SetParent(staticRoot, false);
            goalObject.transform.position = new Vector3(4.05f, 0.22f, 1.45f);
            goalObject.transform.localScale = new Vector3(1.05f, 0.05f, 1.05f);
            goalObject.GetComponent<Renderer>().sharedMaterial = glow;
            Object.Destroy(goalObject.GetComponent<Collider>());
            goal = goalObject.transform;
        }

        private BallController BuildBall()
        {
            var ballObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballObject.name = "Golden physics ball";
            ballObject.transform.position = new Vector3(-4.15f, 0.36f, -1.65f);
            ballObject.transform.localScale = Vector3.one * 0.46f;
            ballObject.GetComponent<Renderer>().sharedMaterial = ballMaterial;
            var body = ballObject.AddComponent<Rigidbody>();
            body.mass = 1.1f;
            body.drag = 0.15f;
            body.angularDrag = 0.05f;
            return ballObject.AddComponent<BallController>();
        }

        private void CreateWall(string name, Vector3 position, Vector3 scale, Transform parent)
        {
            CreateBox(name, position, scale, stone, parent);
        }

        private void CreateRuneStrip(string name, Vector3 position, Vector3 scale, Transform parent)
        {
            var rune = CreateBox(name, position, scale, brass, parent);
            Object.Destroy(rune.GetComponent<Collider>());
        }

        private void CreatePillar(float x, float z, Transform parent)
        {
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = "carved temple pillar";
            pillar.transform.SetParent(parent, false);
            pillar.transform.position = new Vector3(x, 0.55f, z);
            pillar.transform.localScale = new Vector3(0.32f, 0.7f, 0.32f);
            pillar.GetComponent<Renderer>().sharedMaterial = stone;
        }

        private GameObject CreateBox(string name, Vector3 position, Vector3 scale, Material material, Transform parent)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.position = position;
            box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = material;
            return box;
        }
    }
}
