using HandOfGod.Gestures;
using UnityEngine;

namespace HandOfGod.Gameplay
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        private const string StaticRootName = "Level01 Temple - Collision";
        private const string VisualRootName = "Level01 Temple - Art";
        private const string GameplayRootName = "Level01 Temple - Gameplay";

        private Material stone;
        private Material paleStone;
        private Material darkStone;
        private Material cliffStone;
        private Material brass;
        private Material tealGlow;
        private Material amberGlow;
        private Material ballMaterial;

        private void Awake()
        {
            Physics.gravity = new Vector3(0f, -9.81f, 0f);
            if (!GameObject.Find(StaticRootName))
            {
                BuildGameWorld();
            }

            ConfigureGameplay();
        }

        public void BuildGameWorld()
        {
            DestroyNamed(StaticRootName);
            DestroyNamed(VisualRootName);
            DestroyNamed(GameplayRootName);
            DestroyNamed("Main Camera");
            DestroyNamed("Key Light");
            DestroyNamed("Temple Fill Light");
            DestroyNamed("Goal Light");

            BuildMaterials();
            BuildCameraAndLights();

            var staticRoot = new GameObject(StaticRootName).transform;
            var visualRoot = new GameObject(VisualRootName).transform;
            var gameplayRoot = new GameObject(GameplayRootName).transform;

            BuildTemple(staticRoot, visualRoot, gameplayRoot, out var goal);
            BuildBall(gameplayRoot).Configure(goal);
        }

        private void ConfigureGameplay()
        {
            var receiver = GetComponent<GestureUdpReceiver>();
            if (receiver == null)
            {
                receiver = gameObject.AddComponent<GestureUdpReceiver>();
            }

            var ball = FindObjectOfType<BallController>();
            var tilt = GetComponent<BoardTiltController>();
            if (tilt == null)
            {
                tilt = gameObject.AddComponent<BoardTiltController>();
            }

            tilt.Configure(receiver, ball, GameObject.Find(VisualRootName)?.transform);
        }

        private void BuildMaterials()
        {
            stone = NewMaterial("Basalt green stone", new Color(0.34f, 0.43f, 0.39f), 0.38f, 0f);
            paleStone = NewMaterial("Worn top stone", new Color(0.52f, 0.60f, 0.55f), 0.3f, 0f);
            darkStone = NewMaterial("Obsidian table", new Color(0.028f, 0.043f, 0.039f), 0.55f, 0.01f);
            cliffStone = NewMaterial("Temple cliff side", new Color(0.20f, 0.27f, 0.24f), 0.25f, 0f);
            brass = NewMaterial("Aged brass inlay", new Color(0.74f, 0.55f, 0.22f), 0.48f, 0.02f);
            tealGlow = NewMaterial("Teal sacred glow", new Color(0.13f, 0.82f, 0.72f), 0.35f, 0.62f);
            amberGlow = NewMaterial("Amber brazier glow", new Color(1f, 0.35f, 0.08f), 0.2f, 0.85f);
            ballMaterial = NewMaterial("Golden physics ball", new Color(1f, 0.67f, 0.06f), 0.65f, 0.1f);
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
            camera.transform.SetPositionAndRotation(new Vector3(0f, 9.8f, -5.7f), Quaternion.Euler(60f, 0f, 0f));
            camera.orthographic = true;
            camera.orthographicSize = 5.25f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.012f, 0.017f, 0.016f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 80f;

            var sun = new GameObject("Key Light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.05f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(46f, -34f, 22f);

            var fill = new GameObject("Temple Fill Light").AddComponent<Light>();
            fill.type = LightType.Point;
            fill.intensity = 1.45f;
            fill.range = 12f;
            fill.transform.position = new Vector3(-1.2f, 4.4f, -1.6f);

            var goalLight = new GameObject("Goal Light").AddComponent<Light>();
            goalLight.type = LightType.Point;
            goalLight.color = new Color(0.18f, 0.95f, 0.78f);
            goalLight.intensity = 2.8f;
            goalLight.range = 4f;
            goalLight.transform.position = new Vector3(4.7f, 1.5f, 1.8f);
        }

        private void BuildTemple(Transform staticRoot, Transform visualRoot, Transform gameplayRoot, out Transform goal)
        {
            CreateBox("void shadow plinth", new Vector3(0f, -0.55f, 0f), new Vector3(12.8f, 0.42f, 8.15f), darkStone, visualRoot, false);
            CreateBox("main carved floating island", new Vector3(0f, -0.22f, 0f), new Vector3(11.2f, 0.36f, 6.8f), cliffStone, visualRoot, false);
            CreateBox("playable collision plane", new Vector3(0f, 0.03f, 0f), new Vector3(10.75f, 0.16f, 6.28f), darkStone, staticRoot, true);

            BuildSlabPath(staticRoot, visualRoot);
            BuildMazeRails(staticRoot, visualRoot);
            BuildColumnsAndProps(visualRoot);
            BuildGoal(gameplayRoot, out goal);
        }

        private void BuildSlabPath(Transform staticRoot, Transform visualRoot)
        {
            CreateSlab("start terrace", new Vector3(-4.55f, 0.22f, -2.0f), new Vector3(1.9f, 0.26f, 1.65f), staticRoot, visualRoot);
            CreateSlab("west causeway", new Vector3(-3.25f, 0.23f, -1.15f), new Vector3(1.45f, 0.22f, 1.08f), staticRoot, visualRoot);
            CreateSlab("lower turning stone", new Vector3(-1.95f, 0.24f, -0.55f), new Vector3(1.55f, 0.22f, 1.15f), staticRoot, visualRoot);
            CreateRoundCourt("central hand court", new Vector3(-0.35f, 0.24f, 0.05f), 1.55f, staticRoot, visualRoot);
            CreateSlab("east bridge", new Vector3(1.55f, 0.24f, 0.48f), new Vector3(1.85f, 0.22f, 1.05f), staticRoot, visualRoot);
            CreateSlab("upper gate deck", new Vector3(3.0f, 0.24f, 1.08f), new Vector3(1.55f, 0.22f, 1.35f), staticRoot, visualRoot);
            CreateSlab("goal terrace", new Vector3(4.65f, 0.25f, 1.8f), new Vector3(1.65f, 0.24f, 1.7f), staticRoot, visualRoot);

            CreateRuneStrip("start rune guide", new Vector3(-3.55f, 0.41f, -1.62f), new Vector3(0.9f, 0.045f, 0.12f), visualRoot);
            CreateRuneStrip("court rune guide", new Vector3(-0.35f, 0.43f, 0.05f), new Vector3(1.4f, 0.045f, 0.1f), visualRoot);
            CreateRuneStrip("goal rune guide", new Vector3(4.15f, 0.44f, 1.55f), new Vector3(0.95f, 0.045f, 0.1f), visualRoot);

            CreateBox("start emblem bar", new Vector3(-4.55f, 0.43f, -2.53f), new Vector3(1.15f, 0.045f, 0.08f), brass, visualRoot, false);
            CreateBox("goal emblem bar", new Vector3(4.65f, 0.46f, 2.32f), new Vector3(1.0f, 0.045f, 0.08f), tealGlow, visualRoot, false);
        }

        private void BuildMazeRails(Transform staticRoot, Transform visualRoot)
        {
            CreateRail("north outer rail", new Vector3(0f, 0.68f, -3.08f), new Vector3(10.7f, 0.76f, 0.18f), staticRoot, visualRoot);
            CreateRail("south outer rail", new Vector3(0f, 0.68f, 3.08f), new Vector3(10.7f, 0.76f, 0.18f), staticRoot, visualRoot);
            CreateRail("west outer rail", new Vector3(-5.35f, 0.68f, 0f), new Vector3(0.18f, 0.76f, 6.05f), staticRoot, visualRoot);
            CreateRail("east outer rail", new Vector3(5.35f, 0.68f, 0f), new Vector3(0.18f, 0.76f, 6.05f), staticRoot, visualRoot);

            CreateRail("start pocket back", new Vector3(-4.5f, 0.72f, -2.85f), new Vector3(1.7f, 0.7f, 0.2f), staticRoot, visualRoot);
            CreateRail("start pocket side", new Vector3(-5.05f, 0.72f, -1.35f), new Vector3(0.2f, 0.7f, 1.55f), staticRoot, visualRoot);
            CreateRail("lower deflector", new Vector3(-2.45f, 0.66f, 0.28f), new Vector3(1.9f, 0.58f, 0.16f), staticRoot, visualRoot);
            CreateRail("upper deflector", new Vector3(-1.35f, 0.66f, -1.42f), new Vector3(1.7f, 0.58f, 0.16f), staticRoot, visualRoot);
            CreateRail("court left cheek", new Vector3(-0.95f, 0.66f, 1.35f), new Vector3(1.3f, 0.58f, 0.16f), staticRoot, visualRoot);
            CreateRail("bridge north rail", new Vector3(1.55f, 0.66f, -0.2f), new Vector3(1.65f, 0.58f, 0.16f), staticRoot, visualRoot);
            CreateRail("bridge south rail", new Vector3(1.65f, 0.66f, 1.18f), new Vector3(1.45f, 0.58f, 0.16f), staticRoot, visualRoot);
            CreateRail("gate vertical rail", new Vector3(3.45f, 0.66f, 0.1f), new Vector3(0.16f, 0.58f, 1.55f), staticRoot, visualRoot);
            CreateRail("goal guard rail", new Vector3(4.12f, 0.66f, 0.82f), new Vector3(1.25f, 0.58f, 0.16f), staticRoot, visualRoot);
        }

        private void BuildColumnsAndProps(Transform visualRoot)
        {
            CreatePillar(-5.05f, -2.8f, visualRoot);
            CreatePillar(-5.05f, 2.8f, visualRoot);
            CreatePillar(5.05f, -2.8f, visualRoot);
            CreatePillar(5.05f, 2.8f, visualRoot);
            CreatePillar(-0.9f, -2.65f, visualRoot);
            CreatePillar(1.85f, 2.55f, visualRoot);

            CreateBrazier(-4.55f, -1.2f, visualRoot);
            CreateBrazier(0.55f, -1.25f, visualRoot);
            CreateBrazier(3.85f, 2.55f, visualRoot);

            CreateTorus("central control halo", new Vector3(-0.35f, 0.52f, 0.05f), 1.03f, 0.055f, tealGlow, visualRoot);
            CreateTorus("goal sacred ring", new Vector3(4.65f, 0.56f, 1.8f), 0.72f, 0.07f, tealGlow, visualRoot);
        }

        private void BuildGoal(Transform gameplayRoot, out Transform goal)
        {
            var altar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            altar.name = "Goal Trigger - Sacred Altar";
            altar.transform.SetParent(gameplayRoot, false);
            altar.transform.position = new Vector3(4.65f, 0.5f, 1.8f);
            altar.transform.localScale = new Vector3(0.82f, 0.16f, 0.82f);
            altar.GetComponent<Renderer>().sharedMaterial = tealGlow;
            DestroyUnityObject(altar.GetComponent<Collider>());
            goal = altar.transform;
        }

        private BallController BuildBall(Transform gameplayRoot)
        {
            var ballObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballObject.name = "Golden Physics Ball";
            ballObject.transform.SetParent(gameplayRoot, false);
            ballObject.transform.position = new Vector3(-4.55f, 0.58f, -2.0f);
            ballObject.transform.localScale = Vector3.one * 0.46f;
            ballObject.GetComponent<Renderer>().sharedMaterial = ballMaterial;

            var body = ballObject.AddComponent<Rigidbody>();
            body.mass = 1.1f;
            body.drag = 0.15f;
            body.angularDrag = 0.05f;
            return ballObject.AddComponent<BallController>();
        }

        private void CreateSlab(string name, Vector3 position, Vector3 scale, Transform staticRoot, Transform visualRoot)
        {
            CreateBox(name + " collider", position, scale, paleStone, staticRoot, true);
            CreateBox(name + " carved side", position + new Vector3(0f, -0.11f, 0f), new Vector3(scale.x + 0.08f, scale.y, scale.z + 0.08f), cliffStone, visualRoot, false);
            CreateBox(name + " top", position + new Vector3(0f, 0.04f, 0f), new Vector3(scale.x, 0.045f, scale.z), paleStone, visualRoot, false);
        }

        private void CreateRoundCourt(string name, Vector3 position, float radius, Transform staticRoot, Transform visualRoot)
        {
            var collider = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            collider.name = name + " collider";
            collider.transform.SetParent(staticRoot, false);
            collider.transform.position = position;
            collider.transform.localScale = new Vector3(radius, 0.13f, radius);
            collider.GetComponent<Renderer>().sharedMaterial = paleStone;

            var top = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            top.name = name + " carved top";
            top.transform.SetParent(visualRoot, false);
            top.transform.position = position + new Vector3(0f, 0.055f, 0f);
            top.transform.localScale = new Vector3(radius * 0.98f, 0.04f, radius * 0.98f);
            top.GetComponent<Renderer>().sharedMaterial = paleStone;
            DestroyUnityObject(top.GetComponent<Collider>());
        }

        private void CreateRail(string name, Vector3 position, Vector3 scale, Transform staticRoot, Transform visualRoot)
        {
            CreateBox(name + " collider", position, scale, stone, staticRoot, true);
            CreateBox(name + " cap", position + new Vector3(0f, 0.39f, 0f), new Vector3(scale.x + 0.04f, 0.045f, scale.z + 0.04f), brass, visualRoot, false);
        }

        private void CreateRuneStrip(string name, Vector3 position, Vector3 scale, Transform parent)
        {
            CreateBox(name, position, scale, brass, parent, false);
            CreateBox(name + " glow", position + new Vector3(0f, 0.012f, 0f), new Vector3(scale.x * 0.72f, scale.y, scale.z * 0.55f), tealGlow, parent, false);
        }

        private void CreatePillar(float x, float z, Transform parent)
        {
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = "Carved Temple Pillar";
            pillar.transform.SetParent(parent, false);
            pillar.transform.position = new Vector3(x, 0.65f, z);
            pillar.transform.localScale = new Vector3(0.32f, 0.72f, 0.32f);
            pillar.GetComponent<Renderer>().sharedMaterial = stone;
            DestroyUnityObject(pillar.GetComponent<Collider>());

            CreateBox("pillar brass foot", new Vector3(x, 0.2f, z), new Vector3(0.55f, 0.08f, 0.55f), brass, parent, false);
            CreateBox("pillar brass crown", new Vector3(x, 1.15f, z), new Vector3(0.58f, 0.08f, 0.58f), brass, parent, false);
        }

        private void CreateBrazier(float x, float z, Transform parent)
        {
            var bowl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bowl.name = "Amber Brazier";
            bowl.transform.SetParent(parent, false);
            bowl.transform.position = new Vector3(x, 0.62f, z);
            bowl.transform.localScale = new Vector3(0.22f, 0.16f, 0.22f);
            bowl.GetComponent<Renderer>().sharedMaterial = brass;
            DestroyUnityObject(bowl.GetComponent<Collider>());

            var flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flame.name = "Brazier Flame";
            flame.transform.SetParent(parent, false);
            flame.transform.position = new Vector3(x, 0.86f, z);
            flame.transform.localScale = new Vector3(0.22f, 0.34f, 0.22f);
            flame.GetComponent<Renderer>().sharedMaterial = amberGlow;
            DestroyUnityObject(flame.GetComponent<Collider>());

            var light = new GameObject("Brazier Point Light").AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.position = new Vector3(x, 1.0f, z);
            light.type = LightType.Point;
            light.color = new Color(1f, 0.5f, 0.18f);
            light.intensity = 1.25f;
            light.range = 2.2f;
        }

        private void CreateTorus(string name, Vector3 position, float majorRadius, float minorRadius, Material material, Transform parent)
        {
            var mesh = new Mesh { name = name + " mesh" };
            const int majorSegments = 64;
            const int minorSegments = 10;
            var vertices = new Vector3[majorSegments * minorSegments];
            var triangles = new int[majorSegments * minorSegments * 6];

            for (var i = 0; i < majorSegments; i++)
            {
                var u = i / (float)majorSegments * Mathf.PI * 2f;
                for (var j = 0; j < minorSegments; j++)
                {
                    var v = j / (float)minorSegments * Mathf.PI * 2f;
                    var index = i * minorSegments + j;
                    var radius = majorRadius + minorRadius * Mathf.Cos(v);
                    vertices[index] = new Vector3(radius * Mathf.Cos(u), minorRadius * Mathf.Sin(v), radius * Mathf.Sin(u));
                }
            }

            var tri = 0;
            for (var i = 0; i < majorSegments; i++)
            {
                for (var j = 0; j < minorSegments; j++)
                {
                    var a = i * minorSegments + j;
                    var b = ((i + 1) % majorSegments) * minorSegments + j;
                    var c = ((i + 1) % majorSegments) * minorSegments + (j + 1) % minorSegments;
                    var d = i * minorSegments + (j + 1) % minorSegments;
                    triangles[tri++] = a;
                    triangles[tri++] = b;
                    triangles[tri++] = c;
                    triangles[tri++] = a;
                    triangles[tri++] = c;
                    triangles[tri++] = d;
                }
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();

            var torus = new GameObject(name);
            torus.transform.SetParent(parent, false);
            torus.transform.position = position;
            torus.AddComponent<MeshFilter>().sharedMesh = mesh;
            torus.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private GameObject CreateBox(string name, Vector3 position, Vector3 scale, Material material, Transform parent, bool keepCollider)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.position = position;
            box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = material;
            if (!keepCollider)
            {
                DestroyUnityObject(box.GetComponent<Collider>());
            }
            return box;
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
