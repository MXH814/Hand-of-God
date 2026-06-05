using HandOfGod.Gestures;
using UnityEngine;

namespace HandOfGod.Gameplay
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        private const string StaticRootName = "Level01 Ramp - Collision";
        private const string VisualRootName = "Level01 Ramp - Art";
        private const string GameplayRootName = "Level01 Ramp - Gameplay";
        private const float RoadCenterY = 1f;
        private const float RoadAngleDegrees = -8f;

        private Material stone;
        private Material paleStone;
        private Material darkStone;
        private Material cliffStone;
        private Material brass;
        private Material tealGlow;
        private Material amberGlow;
        private Material ballMaterial;
        private Material boxIdle;
        private Material boxHover;
        private Material boxHeld;

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

            BuildRampLevel(staticRoot, visualRoot, gameplayRoot, out var goal, out var movableBox);
            var ball = BuildBall(gameplayRoot);
            ball.Configure(goal);

            var receiver = GetComponent<GestureUdpReceiver>();
            if (receiver == null)
            {
                receiver = gameObject.AddComponent<GestureUdpReceiver>();
            }

            var interactor = gameObject.AddComponent<GestureBoxInteractor>();
            interactor.Configure(receiver, movableBox, movableBox.GetComponent<Renderer>(), boxIdle, boxHover, boxHeld, RoadCenterY, RoadAngleDegrees);

            var hud = gameObject.AddComponent<LevelOneHud>();
            hud.Configure(ball, interactor);
        }

        private void ConfigureGameplay()
        {
            var receiver = GetComponent<GestureUdpReceiver>();
            if (receiver == null)
            {
                receiver = gameObject.AddComponent<GestureUdpReceiver>();
            }

            var ball = FindAnyObjectByType<BallController>();
            var box = GameObject.Find("Pinch Movable Obstacle Box")?.GetComponent<Rigidbody>();
            var interactor = GetComponent<GestureBoxInteractor>();
            if (interactor == null && box != null)
            {
                interactor = gameObject.AddComponent<GestureBoxInteractor>();
                interactor.Configure(receiver, box, box.GetComponent<Renderer>(), boxIdle, boxHover, boxHeld, RoadCenterY, RoadAngleDegrees);
            }

            var hud = GetComponent<LevelOneHud>();
            if (hud == null)
            {
                hud = gameObject.AddComponent<LevelOneHud>();
            }
            hud.Configure(ball, interactor);
        }

        private void BuildMaterials()
        {
            stone = NewMaterial("Basalt green stone", new Color(0.34f, 0.43f, 0.39f), 0.38f, 0f);
            paleStone = NewMaterial("Worn top stone", new Color(0.52f, 0.60f, 0.55f), 0.3f, 0f);
            darkStone = NewMaterial("Obsidian void table", new Color(0.028f, 0.043f, 0.039f), 0.55f, 0.01f);
            cliffStone = NewMaterial("Temple cliff side", new Color(0.20f, 0.27f, 0.24f), 0.25f, 0f);
            brass = NewMaterial("Aged brass inlay", new Color(0.74f, 0.55f, 0.22f), 0.48f, 0.02f);
            tealGlow = NewMaterial("Teal sacred glow", new Color(0.13f, 0.82f, 0.72f), 0.35f, 0.62f);
            amberGlow = NewMaterial("Amber brazier glow", new Color(1f, 0.35f, 0.08f), 0.2f, 0.85f);
            ballMaterial = NewMaterial("Golden physics ball", new Color(1f, 0.67f, 0.06f), 0.65f, 0.1f);
            boxIdle = NewMaterial("Movable cedar box", new Color(0.58f, 0.34f, 0.18f), 0.28f, 0f);
            boxHover = NewMaterial("Movable box hover", new Color(0.88f, 0.55f, 0.24f), 0.35f, 0.15f);
            boxHeld = NewMaterial("Movable box held glow", new Color(0.18f, 0.95f, 0.78f), 0.35f, 0.65f);
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
            camera.transform.SetPositionAndRotation(new Vector3(0f, 7.9f, -5.2f), Quaternion.Euler(58f, 0f, 0f));
            camera.orthographic = true;
            camera.orthographicSize = 4.35f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.012f, 0.017f, 0.016f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 80f;

            var sun = new GameObject("Key Light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.12f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(46f, -34f, 22f);

            var fill = new GameObject("Temple Fill Light").AddComponent<Light>();
            fill.type = LightType.Point;
            fill.intensity = 1.4f;
            fill.range = 9f;
            fill.transform.position = new Vector3(-1.2f, 4.4f, -1.6f);

            var goalLight = new GameObject("Goal Light").AddComponent<Light>();
            goalLight.type = LightType.Point;
            goalLight.color = new Color(0.18f, 0.95f, 0.78f);
            goalLight.intensity = 2.7f;
            goalLight.range = 3.2f;
            goalLight.transform.position = new Vector3(4.15f, 0.9f, 0f);
        }

        private void BuildRampLevel(Transform staticRoot, Transform visualRoot, Transform gameplayRoot, out Transform goal, out Rigidbody movableBox)
        {
            CreateBox("void shadow plinth", new Vector3(0f, -0.62f, 0f), new Vector3(10.8f, 0.42f, 5.2f), darkStone, visualRoot, Quaternion.identity, false);
            CreateBox("floating carved base", new Vector3(0f, -0.28f, 0f), new Vector3(9.8f, 0.34f, 3.9f), cliffStone, visualRoot, Quaternion.identity, false);

            var roadRotation = Quaternion.Euler(0f, 0f, RoadAngleDegrees);
            CreateBox("single sloped road collider", new Vector3(0f, RoadCenterY, 0f), new Vector3(8.8f, 0.28f, 1.55f), paleStone, staticRoot, roadRotation, true);
            CreateBox("single sloped road carved side", new Vector3(0f, RoadCenterY - 0.17f, 0f), new Vector3(9.0f, 0.30f, 1.76f), cliffStone, visualRoot, roadRotation, false);
            CreateBox("single sloped road top highlight", new Vector3(0f, RoadCenterY + 0.16f, 0f), new Vector3(8.45f, 0.045f, 1.35f), paleStone, visualRoot, roadRotation, false);

            CreateBox("left rail", new Vector3(0f, RoadCenterY + 0.34f, -0.92f), new Vector3(8.75f, 0.58f, 0.18f), stone, staticRoot, roadRotation, true);
            CreateBox("right rail", new Vector3(0f, RoadCenterY + 0.34f, 0.92f), new Vector3(8.75f, 0.58f, 0.18f), stone, staticRoot, roadRotation, true);
            CreateBox("left brass rail cap", new Vector3(0f, RoadCenterY + 0.66f, -0.92f), new Vector3(8.75f, 0.045f, 0.22f), brass, visualRoot, roadRotation, false);
            CreateBox("right brass rail cap", new Vector3(0f, RoadCenterY + 0.66f, 0.92f), new Vector3(8.75f, 0.045f, 0.22f), brass, visualRoot, roadRotation, false);

            CreateRuneStrip("downhill rune arrow A", new Vector3(-2.7f, RoadY(-2.7f) + 0.2f, 0f), new Vector3(1.0f, 0.045f, 0.08f), visualRoot, roadRotation);
            CreateRuneStrip("downhill rune arrow B", new Vector3(1.15f, RoadY(1.15f) + 0.2f, 0f), new Vector3(1.0f, 0.045f, 0.08f), visualRoot, roadRotation);

            CreatePillar(-4.55f, -1.3f, visualRoot);
            CreatePillar(-4.55f, 1.3f, visualRoot);
            CreatePillar(4.55f, -1.3f, visualRoot);
            CreatePillar(4.55f, 1.3f, visualRoot);
            CreateBrazier(-3.75f, 1.35f, visualRoot);
            CreateBrazier(2.9f, -1.35f, visualRoot);

            movableBox = BuildMovableBox(gameplayRoot, roadRotation);
            BuildGoal(gameplayRoot, visualRoot, out goal);
        }

        private BallController BuildBall(Transform gameplayRoot)
        {
            var ballObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballObject.name = "Golden Physics Ball";
            ballObject.transform.SetParent(gameplayRoot, false);
            ballObject.transform.position = new Vector3(-4.05f, RoadY(-4.05f) + 0.46f, 0f);
            ballObject.transform.localScale = Vector3.one * 0.46f;
            ballObject.GetComponent<Renderer>().sharedMaterial = ballMaterial;

            var body = ballObject.AddComponent<Rigidbody>();
            body.mass = 1.1f;
            body.linearDamping = 0.08f;
            body.angularDamping = 0.03f;
            return ballObject.AddComponent<BallController>();
        }

        private Rigidbody BuildMovableBox(Transform gameplayRoot, Quaternion roadRotation)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "Pinch Movable Obstacle Box";
            box.transform.SetParent(gameplayRoot, false);
            box.transform.position = new Vector3(-0.45f, RoadY(-0.45f) + 0.37f, 0f);
            box.transform.rotation = roadRotation;
            box.transform.localScale = new Vector3(0.78f, 0.74f, 1.12f);
            box.GetComponent<Renderer>().sharedMaterial = boxIdle;

            var body = box.AddComponent<Rigidbody>();
            body.mass = 4.0f;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            return body;
        }

        private void BuildGoal(Transform gameplayRoot, Transform visualRoot, out Transform goal)
        {
            var goalPosition = new Vector3(4.1f, RoadY(4.1f) + 0.2f, 0f);
            var altar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            altar.name = "Goal Trigger - Sacred Altar";
            altar.transform.SetParent(gameplayRoot, false);
            altar.transform.position = goalPosition;
            altar.transform.localScale = new Vector3(0.7f, 0.11f, 0.7f);
            altar.GetComponent<Renderer>().sharedMaterial = tealGlow;
            DestroyUnityObject(altar.GetComponent<Collider>());
            goal = altar.transform;

            CreateTorus("goal sacred ring", goalPosition + new Vector3(0f, 0.12f, 0f), 0.66f, 0.055f, tealGlow, visualRoot);
        }

        private void CreateRuneStrip(string name, Vector3 position, Vector3 scale, Transform parent, Quaternion rotation)
        {
            CreateBox(name, position, scale, brass, parent, rotation, false);
            CreateBox(name + " glow", position + new Vector3(0f, 0.012f, 0f), new Vector3(scale.x * 0.72f, scale.y, scale.z * 0.55f), tealGlow, parent, rotation, false);
        }

        private void CreatePillar(float x, float z, Transform parent)
        {
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = "Carved Temple Pillar";
            pillar.transform.SetParent(parent, false);
            pillar.transform.position = new Vector3(x, 0.4f, z);
            pillar.transform.localScale = new Vector3(0.3f, 0.7f, 0.3f);
            pillar.GetComponent<Renderer>().sharedMaterial = stone;
            DestroyUnityObject(pillar.GetComponent<Collider>());

            CreateBox("pillar brass foot", new Vector3(x, -0.02f, z), new Vector3(0.52f, 0.08f, 0.52f), brass, parent, Quaternion.identity, false);
            CreateBox("pillar brass crown", new Vector3(x, 0.88f, z), new Vector3(0.55f, 0.08f, 0.55f), brass, parent, Quaternion.identity, false);
        }

        private void CreateBrazier(float x, float z, Transform parent)
        {
            var bowl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bowl.name = "Amber Brazier";
            bowl.transform.SetParent(parent, false);
            bowl.transform.position = new Vector3(x, 0.42f, z);
            bowl.transform.localScale = new Vector3(0.2f, 0.14f, 0.2f);
            bowl.GetComponent<Renderer>().sharedMaterial = brass;
            DestroyUnityObject(bowl.GetComponent<Collider>());

            var flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flame.name = "Brazier Flame";
            flame.transform.SetParent(parent, false);
            flame.transform.position = new Vector3(x, 0.62f, z);
            flame.transform.localScale = new Vector3(0.2f, 0.3f, 0.2f);
            flame.GetComponent<Renderer>().sharedMaterial = amberGlow;
            DestroyUnityObject(flame.GetComponent<Collider>());
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

        private GameObject CreateBox(string name, Vector3 position, Vector3 scale, Material material, Transform parent, Quaternion rotation, bool keepCollider)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.position = position;
            box.transform.rotation = rotation;
            box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = material;
            if (!keepCollider)
            {
                DestroyUnityObject(box.GetComponent<Collider>());
            }
            return box;
        }

        private static float RoadY(float x)
        {
            return RoadCenterY + x * Mathf.Sin(RoadAngleDegrees * Mathf.Deg2Rad);
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
