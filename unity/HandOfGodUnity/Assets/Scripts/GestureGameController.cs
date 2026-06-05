using HandOfGod.Gestures;
using UnityEngine;

namespace HandOfGod.Gameplay
{
    public sealed class GestureGameController : MonoBehaviour
    {
        private enum GameMode
        {
            CalibrationOpen,
            CalibrationPinch,
            Menu,
            Level0,
            Level1,
            Pass,
        }

        private const float CalibrationHoldSeconds = 1f;
        private const float MenuDwellSeconds = 0.85f;
        private const float SafeDwellSeconds = 1f;
        private const float Level1RoadCenterY = 1f;
        private const float Level1RoadAngleDegrees = -8f;

        private GestureUdpReceiver receiver;
        private Transform lobbyRoot;
        private Transform levelRoot;
        private Camera mainCamera;
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
        private Material boxHeldMaterial;

        private GameMode mode = GameMode.CalibrationOpen;
        private GameMode lastLevel = GameMode.Level1;
        private float holdStart = -1f;
        private float pinchThreshold = 0.56f;
        private float pinchSampleSum;
        private int pinchSampleCount;
        private string hoverKey = "";
        private float hoverStart = -1f;
        private GameObject labObject;
        private Rigidbody labBody;
        private Vector3 labVelocity;
        private bool labHeld;
        private bool labCompleted;
        private float twoHandStartDistance;
        private float twoHandStartScale;
        private float twoHandStartAngle;
        private Quaternion twoHandStartRotation;
        private Rigidbody obstacleBox;
        private Renderer obstacleRenderer;
        private Vector3 boxVelocity;
        private bool boxHeld;
        private BallController levelBall;
        private bool initialized;

        public void Configure(GestureUdpReceiver gestureReceiver)
        {
            receiver = gestureReceiver;
        }

        public void InitializeForScene()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            receiver ??= GetComponent<GestureUdpReceiver>();
            BuildMaterials();
            BuildCameraAndLights();
            BuildLobbyShell();
            ResetToCalibration();
        }

        private void Awake()
        {
            InitializeForScene();
        }

        private void Update()
        {
            if (!TryGetPrimaryHand(out var hand))
            {
                return;
            }

            switch (mode)
            {
                case GameMode.CalibrationOpen:
                    UpdateCalibrationOpen(hand);
                    break;
                case GameMode.CalibrationPinch:
                    UpdateCalibrationPinch(hand);
                    break;
                case GameMode.Level0:
                    UpdateLevel0(hand);
                    break;
                case GameMode.Level1:
                    UpdateLevel1(hand);
                    break;
            }

            if (mode == GameMode.Level1 && levelBall != null && levelBall.ReachedGoal)
            {
                mode = GameMode.Pass;
            }
        }

        private void OnGUI()
        {
            GUI.color = Color.white;
            DrawCursor();

            switch (mode)
            {
                case GameMode.CalibrationOpen:
                case GameMode.CalibrationPinch:
                    DrawCalibration();
                    break;
                case GameMode.Menu:
                    DrawMenu();
                    break;
                case GameMode.Level0:
                    DrawLevel0Hud();
                    break;
                case GameMode.Level1:
                    DrawLevel1Hud();
                    break;
                case GameMode.Pass:
                    DrawPassHud();
                    break;
            }
        }

        private void ResetToCalibration()
        {
            ClearLevel();
            SetLobbyVisible(true);
            mode = GameMode.CalibrationOpen;
            holdStart = -1f;
            hoverKey = "";
            hoverStart = -1f;
            pinchSampleSum = 0f;
            pinchSampleCount = 0;
        }

        private void UpdateCalibrationOpen(GestureHandFrame hand)
        {
            if (hand.openPalm && hand.score >= 0.45f)
            {
                holdStart = holdStart < 0f ? Time.time : holdStart;
                if (Time.time - holdStart >= CalibrationHoldSeconds)
                {
                    mode = GameMode.CalibrationPinch;
                    holdStart = -1f;
                }
            }
            else
            {
                holdStart = -1f;
            }
        }

        private void UpdateCalibrationPinch(GestureHandFrame hand)
        {
            if (hand.pinchDistance < 0.62f && hand.score >= 0.45f)
            {
                holdStart = holdStart < 0f ? Time.time : holdStart;
                pinchSampleSum += hand.pinchDistance;
                pinchSampleCount += 1;
                if (Time.time - holdStart >= CalibrationHoldSeconds)
                {
                    var average = pinchSampleCount > 0 ? pinchSampleSum / pinchSampleCount : 0.34f;
                    pinchThreshold = Mathf.Clamp(average * 1.65f, 0.36f, 0.62f);
                    mode = GameMode.Menu;
                    holdStart = -1f;
                }
            }
            else
            {
                holdStart = -1f;
            }
        }

        private void DrawCalibration()
        {
            var panel = new Rect(30, 30, 430, 190);
            var title = mode == GameMode.CalibrationOpen ? "Calibration: Open Hand" : "Calibration: Pinch";
            var detail = mode == GameMode.CalibrationOpen
                ? "Hold an open palm for 1 second."
                : "Touch thumb and index finger together for 1 second.";
            var progress = holdStart < 0f ? 0f : Mathf.Clamp01((Time.time - holdStart) / CalibrationHoldSeconds);

            DrawPanel(panel);
            GUI.Label(new Rect(50, 48, 380, 30), "Hand of God");
            GUI.Label(new Rect(50, 80, 380, 26), title);
            GUI.Label(new Rect(50, 110, 390, 24), detail);
            DrawProgressBar(progress, new Rect(50, 138, 360, 18));
            DrawHoverButton("skip", "Skip calibration", new Rect(50, 166, 180, 34), SafeDwellSeconds, () =>
            {
                pinchThreshold = 0.56f;
                mode = GameMode.Menu;
            });
        }

        private void DrawMenu()
        {
            DrawPanel(new Rect(40, 40, 390, 300));
            GUI.Label(new Rect(70, 62, 300, 30), "Hand of God");
            GUI.Label(new Rect(70, 92, 320, 24), "Hover your index finger over an option.");
            DrawHoverButton("start", "Start Game", new Rect(70, 120, 260, 42), MenuDwellSeconds, () => StartLevel(GameMode.Level1));
            DrawHoverButton("level0", "Level 0: Gesture Lab", new Rect(70, 172, 260, 42), MenuDwellSeconds, () => StartLevel(GameMode.Level0));
            DrawHoverButton("level1", "Level 1: First Path", new Rect(70, 224, 260, 42), MenuDwellSeconds, () => StartLevel(GameMode.Level1));
            DrawHoverButton("recalibrate", "Recalibrate", new Rect(70, 276, 260, 42), MenuDwellSeconds, ResetToCalibration);
        }

        private void DrawLevel0Hud()
        {
            DrawPanel(new Rect(24, 24, 380, 245));
            GUI.Label(new Rect(44, 44, 320, 26), "Level 0: Gesture Lab");
            GUI.Label(new Rect(44, 74, 330, 24), labCompleted ? "Complete: you moved the object." : "Pinch the object and move it.");
            GUI.Label(new Rect(44, 100, 330, 24), $"Pinch threshold: {pinchThreshold:0.00}");
            if (labCompleted)
            {
                DrawHoverButton("next-level", "Next: Level 1", new Rect(44, 150, 210, 38), MenuDwellSeconds, () => StartLevel(GameMode.Level1));
            }
            DrawHoverButton("lab-menu", "Menu", new Rect(44, 198, 150, 36), MenuDwellSeconds, () => mode = GameMode.Menu);

            DrawPanel(new Rect(Screen.width - 270, 36, 230, 170));
            DrawHoverButton("shape-cube", "Cube", new Rect(Screen.width - 250, 56, 190, 38), MenuDwellSeconds, () => ReplaceLabObject(PrimitiveType.Cube));
            DrawHoverButton("shape-sphere", "Sphere", new Rect(Screen.width - 250, 102, 190, 38), MenuDwellSeconds, () => ReplaceLabObject(PrimitiveType.Sphere));
            DrawHoverButton("shape-cylinder", "Cylinder", new Rect(Screen.width - 250, 148, 190, 38), MenuDwellSeconds, () => ReplaceLabObject(PrimitiveType.Cylinder));
        }

        private void DrawLevel1Hud()
        {
            DrawPanel(new Rect(24, 24, 360, 166));
            GUI.Label(new Rect(44, 44, 320, 26), "Level 1: First Path");
            GUI.Label(new Rect(44, 74, 320, 24), boxHeld ? "Pinch: moving the box" : "Pinch the box and move it away.");
            GUI.Label(new Rect(44, 100, 320, 24), levelBall != null ? $"Ball speed: {levelBall.Speed:0.00}" : "Ball speed: 0.00");
            DrawHoverButton("level1-menu", "Menu", new Rect(44, 136, 150, 36), MenuDwellSeconds, () => mode = GameMode.Menu);
        }

        private void DrawPassHud()
        {
            var panelX = Screen.width / 2f - 185f;
            DrawPanel(new Rect(panelX, 70, 370, 210));
            GUI.Label(new Rect(panelX + 40, 98, 300, 34), "PASS");
            GUI.Label(new Rect(panelX + 40, 132, 300, 24), "The ball reached the altar.");
            DrawHoverButton("pass-restart", "Restart", new Rect(Screen.width / 2f - 140f, 170, 120, 40), MenuDwellSeconds, () => StartLevel(lastLevel));
            DrawHoverButton("pass-menu", "Menu", new Rect(Screen.width / 2f + 20f, 170, 120, 40), MenuDwellSeconds, () => mode = GameMode.Menu);
        }

        private void StartLevel(GameMode level)
        {
            lastLevel = level;
            ClearLevel();
            SetLobbyVisible(false);
            if (level == GameMode.Level0)
            {
                BuildLevel0();
            }
            else
            {
                BuildLevel1();
            }
            mode = level;
            hoverKey = "";
            hoverStart = -1f;
        }

        private void BuildLevel0()
        {
            levelRoot = new GameObject("Level00 Gesture Lab").transform;
            CreateBox("lab base", new Vector3(0f, -0.15f, 0f), new Vector3(5.5f, 0.3f, 3.2f), darkStone, levelRoot, Quaternion.identity, true);
            CreateBox("lab guide", new Vector3(0f, 0.02f, 0f), new Vector3(3.6f, 0.05f, 2.0f), paleStone, levelRoot, Quaternion.identity, false);
            ReplaceLabObject(PrimitiveType.Cube);
            labCompleted = false;
        }

        private void BuildLobbyShell()
        {
            DestroyNamed("Menu Temple Lobby");
            lobbyRoot = new GameObject("Menu Temple Lobby").transform;
            CreateBox("lobby obsidian base", new Vector3(0f, -0.45f, 0f), new Vector3(7.6f, 0.42f, 4.1f), darkStone, lobbyRoot, Quaternion.identity, false);
            CreateBox("lobby raised stone", new Vector3(0f, -0.12f, 0f), new Vector3(5.2f, 0.28f, 2.7f), cliffStone, lobbyRoot, Quaternion.identity, false);
            CreateBox("lobby center slab", new Vector3(0f, 0.08f, 0f), new Vector3(3.2f, 0.18f, 1.55f), paleStone, lobbyRoot, Quaternion.identity, false);
            CreateTorus("lobby calibration ring", new Vector3(0f, 0.27f, 0f), 0.82f, 0.055f, tealGlow, lobbyRoot);
            CreateBox("lobby brass axis", new Vector3(0f, 0.31f, 0f), new Vector3(2.5f, 0.045f, 0.08f), brass, lobbyRoot, Quaternion.identity, false);
            CreateBox("lobby brass cross", new Vector3(0f, 0.32f, 0f), new Vector3(0.08f, 0.045f, 1.25f), brass, lobbyRoot, Quaternion.identity, false);
            CreateLobbyPillar(-2.85f, -1.35f);
            CreateLobbyPillar(-2.85f, 1.35f);
            CreateLobbyPillar(2.85f, -1.35f);
            CreateLobbyPillar(2.85f, 1.35f);
        }

        private void CreateLobbyPillar(float x, float z)
        {
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = "Lobby Temple Pillar";
            pillar.transform.SetParent(lobbyRoot, false);
            pillar.transform.position = new Vector3(x, 0.35f, z);
            pillar.transform.localScale = new Vector3(0.25f, 0.65f, 0.25f);
            pillar.GetComponent<Renderer>().sharedMaterial = stone;
            DestroyUnityObject(pillar.GetComponent<Collider>());
            CreateBox("lobby pillar foot", new Vector3(x, -0.06f, z), new Vector3(0.44f, 0.08f, 0.44f), brass, lobbyRoot, Quaternion.identity, false);
            CreateBox("lobby pillar crown", new Vector3(x, 0.76f, z), new Vector3(0.48f, 0.08f, 0.48f), brass, lobbyRoot, Quaternion.identity, false);
        }

        private void SetLobbyVisible(bool visible)
        {
            if (lobbyRoot != null)
            {
                lobbyRoot.gameObject.SetActive(visible);
            }
        }

        private void ReplaceLabObject(PrimitiveType primitive)
        {
            if (labObject != null)
            {
                DestroyUnityObject(labObject);
            }
            labObject = GameObject.CreatePrimitive(primitive);
            labObject.name = $"Gesture Lab {primitive}";
            labObject.transform.SetParent(levelRoot, false);
            labObject.transform.position = new Vector3(0f, 0.7f, 0f);
            labObject.transform.localScale = Vector3.one * 0.75f;
            labObject.GetComponent<Renderer>().sharedMaterial = boxIdle;
            labBody = labObject.AddComponent<Rigidbody>();
            labBody.isKinematic = true;
            labBody.interpolation = RigidbodyInterpolation.Interpolate;
        }

        private void UpdateLevel0(GestureHandFrame hand)
        {
            if (labBody == null)
            {
                return;
            }

            var target = ScreenToWorldPlane(hand.pinchX, hand.pinchY, 0.7f);
            var isPinch = IsPinching(hand);
            var close = Vector3.Distance(target, labBody.position) < 1.0f;
            if (!labHeld && isPinch && close)
            {
                labHeld = true;
                labVelocity = Vector3.zero;
            }
            if (labHeld && !isPinch)
            {
                labHeld = false;
            }
            if (labHeld)
            {
                target.x = Mathf.Clamp(target.x, -1.75f, 1.75f);
                target.z = Mathf.Clamp(target.z, -1.0f, 1.0f);
                target.y = 0.7f;
                labBody.MovePosition(Vector3.SmoothDamp(labBody.position, target, ref labVelocity, 0.055f));
                labCompleted = true;
            }

            if (TryGetTwoPinchingHands(out var a, out var b))
            {
                var distance = Vector2.Distance(new Vector2(a.pinchX, a.pinchY), new Vector2(b.pinchX, b.pinchY));
                var angle = Mathf.Atan2(b.pinchY - a.pinchY, b.pinchX - a.pinchX);
                if (twoHandStartDistance <= 0f)
                {
                    twoHandStartDistance = Mathf.Max(distance, 0.001f);
                    twoHandStartScale = labObject.transform.localScale.x;
                    twoHandStartAngle = angle;
                    twoHandStartRotation = labObject.transform.rotation;
                }
                var scale = Mathf.Clamp(twoHandStartScale * distance / twoHandStartDistance, 0.45f, 1.6f);
                labObject.transform.localScale = Vector3.one * scale;
                labObject.transform.rotation = twoHandStartRotation * Quaternion.Euler(0f, -(angle - twoHandStartAngle) * Mathf.Rad2Deg, 0f);
            }
            else
            {
                twoHandStartDistance = 0f;
            }
        }

        private void BuildLevel1()
        {
            levelRoot = new GameObject("Level01 First Path").transform;
            var roadRotation = Quaternion.Euler(0f, 0f, Level1RoadAngleDegrees);
            CreateBox("void shadow plinth", new Vector3(0f, -0.62f, 0f), new Vector3(10.8f, 0.42f, 5.2f), darkStone, levelRoot, Quaternion.identity, false);
            CreateBox("floating carved base", new Vector3(0f, -0.28f, 0f), new Vector3(9.8f, 0.34f, 3.9f), cliffStone, levelRoot, Quaternion.identity, false);
            CreateBox("single sloped road", new Vector3(0f, Level1RoadCenterY, 0f), new Vector3(8.8f, 0.28f, 1.55f), paleStone, levelRoot, roadRotation, true);
            CreateBox("left rail", new Vector3(0f, Level1RoadCenterY + 0.34f, -0.92f), new Vector3(8.75f, 0.58f, 0.18f), stone, levelRoot, roadRotation, true);
            CreateBox("right rail", new Vector3(0f, Level1RoadCenterY + 0.34f, 0.92f), new Vector3(8.75f, 0.58f, 0.18f), stone, levelRoot, roadRotation, true);

            obstacleBox = CreateBox("Pinch Movable Obstacle Box", new Vector3(-0.45f, RoadY(-0.45f) + 0.37f, 0f), new Vector3(0.78f, 0.74f, 1.12f), boxIdle, levelRoot, roadRotation, true).AddComponent<Rigidbody>();
            obstacleBox.isKinematic = true;
            obstacleBox.interpolation = RigidbodyInterpolation.Interpolate;
            obstacleBox.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            obstacleRenderer = obstacleBox.GetComponent<Renderer>();

            var ballObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballObject.name = "Golden Physics Ball";
            ballObject.transform.SetParent(levelRoot, false);
            ballObject.transform.position = new Vector3(-4.05f, RoadY(-4.05f) + 0.46f, 0f);
            ballObject.transform.localScale = Vector3.one * 0.46f;
            ballObject.GetComponent<Renderer>().sharedMaterial = ballMaterial;
            var body = ballObject.AddComponent<Rigidbody>();
            body.mass = 1.1f;
            body.linearDamping = 0.08f;
            body.angularDamping = 0.03f;
            levelBall = ballObject.AddComponent<BallController>();

            var goal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            goal.name = "Goal Trigger - Sacred Altar";
            goal.transform.SetParent(levelRoot, false);
            goal.transform.position = new Vector3(4.1f, RoadY(4.1f) + 0.2f, 0f);
            goal.transform.localScale = new Vector3(0.7f, 0.11f, 0.7f);
            goal.GetComponent<Renderer>().sharedMaterial = tealGlow;
            DestroyUnityObject(goal.GetComponent<Collider>());
            levelBall.Configure(goal.transform);
            CreateTorus("goal sacred ring", goal.transform.position + new Vector3(0f, 0.12f, 0f), 0.66f, 0.055f, tealGlow, levelRoot);
        }

        private void UpdateLevel1(GestureHandFrame hand)
        {
            if (obstacleBox == null)
            {
                return;
            }

            var target = MapPinchToRoad(hand.pinchX, hand.pinchY);
            var close = DistanceXZ(target, obstacleBox.position) < 0.72f;
            var isPinch = IsPinching(hand);
            obstacleRenderer.sharedMaterial = boxIdle;
            if (!boxHeld && close)
            {
                obstacleRenderer.sharedMaterial = boxHover;
            }
            if (!boxHeld && isPinch && close)
            {
                boxHeld = true;
                boxVelocity = Vector3.zero;
            }
            if (boxHeld && !isPinch)
            {
                boxHeld = false;
            }
            if (boxHeld)
            {
                obstacleRenderer.sharedMaterial = boxHeldMaterial;
                obstacleBox.MovePosition(Vector3.SmoothDamp(obstacleBox.position, target, ref boxVelocity, 0.055f));
                obstacleBox.linearVelocity = Vector3.zero;
                obstacleBox.angularVelocity = Vector3.zero;
            }
        }

        private Vector3 MapPinchToRoad(float normalizedX, float normalizedY)
        {
            var x = Mathf.Lerp(-1.2f, 1.2f, Mathf.Clamp01(normalizedX));
            var z = Mathf.Lerp(1.25f, -1.25f, Mathf.Clamp01(normalizedY));
            x = Mathf.Clamp(x, -1.2f, 1.2f);
            z = Mathf.Clamp(z, -1.25f, 1.25f);
            return new Vector3(x, RoadY(x) + 0.37f, z);
        }

        private bool IsPinching(GestureHandFrame hand)
        {
            return hand.score >= 0.35f && hand.pinchDistance < pinchThreshold;
        }

        private bool TryGetPrimaryHand(out GestureHandFrame hand)
        {
            var frame = receiver != null && receiver.HasFreshFrame ? receiver.Latest : GestureFrame.Neutral;
            if (frame.hands != null && frame.hands.Length > 0)
            {
                hand = frame.hands[0];
                return true;
            }

            if (frame.confidence > 0f)
            {
                hand = new GestureHandFrame
                {
                    id = "primary",
                    handedness = "Unknown",
                    score = frame.confidence,
                    pinchX = frame.pinchX,
                    pinchY = frame.pinchY,
                    indexX = frame.indexX,
                    indexY = frame.indexY,
                    pinchDistance = frame.pinchDistance,
                    palmSpan = frame.palmSpan,
                    palmRoll = frame.palmRoll,
                    palmPitch = frame.palmPitch,
                    palmYaw = frame.palmYaw,
                    pinch = frame.pinch,
                    openPalm = frame.openPalm,
                };
                return true;
            }

            hand = default;
            return false;
        }

        private bool TryGetTwoPinchingHands(out GestureHandFrame a, out GestureHandFrame b)
        {
            var frame = receiver != null && receiver.HasFreshFrame ? receiver.Latest : GestureFrame.Neutral;
            a = default;
            b = default;
            if (frame.hands == null || frame.hands.Length < 2)
            {
                return false;
            }

            a = frame.hands[0];
            b = frame.hands[1];
            return IsPinching(a) && IsPinching(b);
        }

        private Vector2 CursorScreen()
        {
            return TryGetPrimaryHand(out var hand)
                ? new Vector2(hand.indexX * Screen.width, hand.indexY * Screen.height)
                : new Vector2(-1000f, -1000f);
        }

        private void DrawCursor()
        {
            if (!TryGetPrimaryHand(out var hand))
            {
                return;
            }
            var pos = CursorScreen();
            var rect = new Rect(pos.x - 14f, pos.y - 14f, 28f, 28f);
            GUI.color = IsPinching(hand) ? Color.cyan : Color.white;
            GUI.Box(rect, IsPinching(hand) ? "P" : "");
            GUI.color = Color.white;
        }

        private void DrawHoverButton(string key, string label, Rect rect, float dwellSeconds, System.Action action)
        {
            var cursor = CursorScreen();
            var inside = rect.Contains(cursor);
            if (inside)
            {
                if (hoverKey != key)
                {
                    hoverKey = key;
                    hoverStart = Time.time;
                }
            }
            else if (hoverKey == key)
            {
                hoverKey = "";
                hoverStart = -1f;
            }

            var progress = hoverKey == key && hoverStart >= 0f ? Mathf.Clamp01((Time.time - hoverStart) / dwellSeconds) : 0f;
            GUI.Box(rect, label);
            var bar = new Rect(rect.x, rect.yMax - 6f, rect.width * progress, 6f);
            GUI.color = Color.cyan;
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = Color.white;
            if (progress >= 1f)
            {
                hoverKey = "";
                hoverStart = -1f;
                action();
            }
        }

        private static void DrawPanel(Rect rect)
        {
            GUI.Box(rect, "");
        }

        private static void DrawProgressBar(float progress, Rect rect)
        {
            GUI.Box(rect, "");
            GUI.color = Color.cyan;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * progress, rect.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private Vector3 ScreenToWorldPlane(float normalizedX, float normalizedY, float y)
        {
            var ray = mainCamera.ScreenPointToRay(new Vector3(normalizedX * Screen.width, (1f - normalizedY) * Screen.height, 0f));
            var plane = new Plane(Vector3.up, new Vector3(0f, y, 0f));
            return plane.Raycast(ray, out var distance) ? ray.GetPoint(distance) : new Vector3(0f, y, 0f);
        }

        private void ClearLevel()
        {
            if (levelRoot != null)
            {
                DestroyUnityObject(levelRoot.gameObject);
            }
            levelRoot = null;
            labObject = null;
            labBody = null;
            obstacleBox = null;
            obstacleRenderer = null;
            levelBall = null;
            labHeld = false;
            boxHeld = false;
            twoHandStartDistance = 0f;
        }

        private static void DestroyNamed(string objectName)
        {
            var existing = GameObject.Find(objectName);
            if (existing != null)
            {
                DestroyUnityObject(existing);
            }
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
            boxHeldMaterial = NewMaterial("Movable box held glow", new Color(0.18f, 0.95f, 0.78f), 0.35f, 0.65f);
        }

        private static Material NewMaterial(string name, Color color, float smoothness, float emission)
        {
            var material = new Material(Shader.Find("Standard")) { name = name, color = color };
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
            mainCamera = cameraObject.AddComponent<Camera>();
            mainCamera.tag = "MainCamera";
            mainCamera.transform.SetPositionAndRotation(new Vector3(0f, 7.9f, -5.2f), Quaternion.Euler(58f, 0f, 0f));
            mainCamera.orthographic = true;
            mainCamera.orthographicSize = 4.35f;
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = new Color(0.012f, 0.017f, 0.016f);

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
                    var radius = majorRadius + minorRadius * Mathf.Cos(v);
                    vertices[i * minorSegments + j] = new Vector3(radius * Mathf.Cos(u), minorRadius * Mathf.Sin(v), radius * Mathf.Sin(u));
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
                    triangles[tri++] = a; triangles[tri++] = b; triangles[tri++] = c;
                    triangles[tri++] = a; triangles[tri++] = c; triangles[tri++] = d;
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

        private static float RoadY(float x)
        {
            return Level1RoadCenterY + x * Mathf.Sin(Level1RoadAngleDegrees * Mathf.Deg2Rad);
        }

        private static float DistanceXZ(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
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
