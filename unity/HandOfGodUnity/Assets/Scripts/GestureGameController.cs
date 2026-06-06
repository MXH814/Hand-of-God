using HandOfGod.Gestures;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

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

        private enum TutorialStage
        {
            FindHands,
            OneHandDrag,
            TwoHandRotate,
            MapControl,
            Complete,
        }

        private const float CalibrationHoldSeconds = 1f;
        private const float MenuDwellSeconds = 0.85f;
        private const float SafeDwellSeconds = 1f;
        private const float TutorialHoldSeconds = 3f;
        private const float Level1RoadCenterY = 1f;
        private const float Level1RoadAngleDegrees = -8f;
        private static readonly int[] HandConnectionPairs =
        {
            0, 1, 1, 2, 2, 3, 3, 4,
            0, 5, 5, 6, 6, 7, 7, 8,
            5, 9, 9, 10, 10, 11, 11, 12,
            9, 13, 13, 14, 14, 15, 15, 16,
            13, 17, 17, 18, 18, 19, 19, 20,
            0, 17,
        };

        private GestureUdpReceiver receiver;
        private CameraFrameReceiver cameraFrames;
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
        private Material cameraBackgroundMaterial;
        private Transform cameraBackgroundPlane;

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
        private Renderer labRenderer;
        private Vector3 labGrabOffset;
        private bool labHeld;
        private bool labCompleted;
        private TutorialStage tutorialStage = TutorialStage.FindHands;
        private float tutorialProgressStart = -1f;
        private bool tutorialObjectMoved;
        private bool tutorialObjectRotated;
        private bool tutorialMapAdjusted;
        private Vector3 tutorialDragStart;
        private float twoHandStartDistance;
        private float twoHandStartAngle;
        private Quaternion twoHandStartRotation;
        private float twoFingerMapStartDistance;
        private float twoFingerMapStartAngle;
        private Vector3 twoFingerMapStartScale;
        private Quaternion twoFingerMapStartRotation;
        private Rigidbody obstacleBox;
        private Renderer obstacleRenderer;
        private bool boxHeld;
        private BallController levelBall;
        private bool initialized;
        private Texture2D lineTexture;
        private Process bridgeProcess;
        private bool launchedBridge;
        private bool usesExternalBridge;
        private string bridgeStatus = "Starting camera...";

        public void Configure(GestureUdpReceiver gestureReceiver, CameraFrameReceiver frameReceiver)
        {
            receiver = gestureReceiver;
            cameraFrames = frameReceiver;
        }

        public void InitializeForScene()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            receiver ??= GetComponent<GestureUdpReceiver>();
            cameraFrames ??= GetComponent<CameraFrameReceiver>();
            DestroyNamed("Main Camera");
            DestroyNamed("Key Light");
            DestroyNamed("Temple Fill Light");
            DestroyNamed("Menu Temple Lobby");
            BuildMaterials();
            BuildCameraAndLights();
            BuildLobbyShell();
            ResetToCalibration();
            StartGestureBridgeIfNeeded();
        }

        private void Awake()
        {
            InitializeForScene();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                QuitGame();
            }

            RefreshBridgeStatus();
            UpdateCameraBackground();

            if (HandleLevel1BallState())
            {
                return;
            }

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

            HandleLevel1BallState();
        }

        private bool HandleLevel1BallState()
        {
            if (mode != GameMode.Level1 || levelBall == null)
            {
                return false;
            }

            if (levelBall.Failed)
            {
                StartLevel(GameMode.Level1);
                return true;
            }

            if (levelBall.ReachedGoal)
            {
                mode = GameMode.Pass;
                return true;
            }

            return false;
        }

        private void RefreshBridgeStatus()
        {
            if (receiver != null && receiver.HasFreshFrame)
            {
                bridgeStatus = "Camera: tracking hand";
                return;
            }

            if (bridgeProcess != null && bridgeProcess.HasExited)
            {
                bridgeStatus = $"Camera bridge exited ({bridgeProcess.ExitCode}). Check gesture-bridge-runtime.log.";
                launchedBridge = false;
            }
            else if (cameraFrames != null && cameraFrames.HasFreshFrame)
            {
                bridgeStatus = "Camera image live; show your hand to the camera.";
            }
            else if (usesExternalBridge)
            {
                bridgeStatus = "Launcher started camera bridge; waiting for camera image...";
            }
            else if (launchedBridge)
            {
                bridgeStatus = "Starting camera bridge; waiting for camera image...";
            }
        }

        private void OnGUI()
        {
            GUI.color = Color.white;

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

            DrawHandSkeletonOverlay();
            DrawCursor();
            DrawGlobalControls();
        }

        private void ResetToCalibration()
        {
            ClearLevel();
            SetLobbyVisible(false);
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
                    StartLevel(GameMode.Level0);
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
            var panel = new Rect(Screen.width / 2f - 250f, 34, 500, 252);
            var title = mode == GameMode.CalibrationOpen ? "Calibration: Open Hand" : "Calibration: Pinch";
            var detail = mode == GameMode.CalibrationOpen
                ? "Hold an open palm for 1 second."
                : "Touch thumb and index finger together for 1 second.";
            var progress = holdStart < 0f ? 0f : Mathf.Clamp01((Time.time - holdStart) / CalibrationHoldSeconds);

            DrawPanel(panel);
            GUI.Label(new Rect(panel.x + 24, panel.y + 18, 430, 30), "Hand of God");
            GUI.Label(new Rect(panel.x + 24, panel.y + 50, 430, 26), title);
            GUI.Label(new Rect(panel.x + 24, panel.y + 82, 440, 24), detail);
            DrawProgressBar(progress, new Rect(panel.x + 24, panel.y + 112, 410, 18));
            var cameraStatus = cameraFrames != null && cameraFrames.HasFreshFrame ? "Camera image: live" : "Camera image: waiting";
            GUI.Label(new Rect(panel.x + 24, panel.y + 140, 450, 24), receiver != null && receiver.HasFreshFrame ? "Camera: tracking hand" : $"{bridgeStatus} | {cameraStatus}");
            DrawUtilityButton("start-camera", "Start / Retry Camera", new Rect(panel.x + 24, panel.y + 178, 190, 34), SafeDwellSeconds, StartVisibleGestureBridge);
            DrawHoverButton("skip", "Skip calibration", new Rect(panel.x + 236, panel.y + 178, 190, 34), SafeDwellSeconds, () =>
            {
                pinchThreshold = 0.56f;
                StartLevel(GameMode.Level0);
            });
        }

        private void DrawGlobalControls()
        {
            var exitRect = new Rect(Screen.width - 176, 38, 128, 46);
            DrawUtilityButton("global-exit", "Exit", exitRect, SafeDwellSeconds, QuitGame);
            if (mode != GameMode.CalibrationOpen && mode != GameMode.CalibrationPinch)
            {
                DrawUtilityButton("global-recalibrate", "Calibrate", new Rect(exitRect.x - 150, exitRect.y, 132, 46), MenuDwellSeconds, ResetToCalibration);
            }
        }

        private void DrawMenu()
        {
            DrawPanel(new Rect(40, 40, 390, 300));
            GUI.Label(new Rect(70, 62, 300, 30), "Hand of God");
            GUI.Label(new Rect(70, 92, 320, 24), "Hover your index finger over an option.");
            DrawHoverButton("start", "Start Game", new Rect(70, 120, 260, 42), MenuDwellSeconds, () => StartLevel(GameMode.Level1));
            DrawHoverButton("level0", "Level 0: Tutorial", new Rect(70, 172, 260, 42), MenuDwellSeconds, () => StartLevel(GameMode.Level0));
            DrawHoverButton("level1", "Level 1: First Path", new Rect(70, 224, 260, 42), MenuDwellSeconds, () => StartLevel(GameMode.Level1));
            DrawHoverButton("recalibrate", "Recalibrate", new Rect(70, 276, 260, 42), MenuDwellSeconds, ResetToCalibration);
        }

        private void DrawLevel0Hud()
        {
            var titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            var stepStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            var detailStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            var statusStyle = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            var panelWidth = Mathf.Min(Screen.width - 160f, 860f);
            var panel = new Rect(Screen.width * 0.5f - panelWidth * 0.5f, 42f, panelWidth, 184f);
            DrawPanel(panel);
            GUI.Label(new Rect(panel.x + 32f, panel.y + 16f, panel.width - 64f, 32f), "Level 0: Gesture Tutorial", titleStyle);
            GUI.Label(new Rect(panel.x + 32f, panel.y + 52f, panel.width - 64f, 28f), TutorialTitle(), stepStyle);
            GUI.Label(new Rect(panel.x + 32f, panel.y + 84f, panel.width - 64f, 42f), TutorialDetail(), detailStyle);
            DrawProgressBar(TutorialProgress(), new Rect(panel.x + 32f, panel.y + 132f, panel.width - 64f, 22f));
            GUI.Label(new Rect(panel.x + 32f, panel.y + 158f, panel.width - 64f, 22f), $"{HandStatusText()}    Pinch threshold: {pinchThreshold:0.00}", statusStyle);

            if (tutorialStage == TutorialStage.Complete)
            {
                DrawHoverButton("next-level", "Next: Level 1", new Rect(Screen.width * 0.5f - 120f, panel.yMax + 18f, 240f, 48f), MenuDwellSeconds, () => StartLevel(GameMode.Level1));
            }

            var shapePanel = new Rect(Screen.width - 320f, 260f, 260f, 188f);
            DrawPanel(shapePanel);
            GUI.Label(new Rect(shapePanel.x + 20f, shapePanel.y + 18f, 220f, 24f), "Practice object");
            DrawHoverButton("shape-cube", "Cube", new Rect(shapePanel.x + 20f, shapePanel.y + 50f, 210f, 34f), MenuDwellSeconds, () => ReplaceLabObject(PrimitiveType.Cube));
            DrawHoverButton("shape-sphere", "Sphere", new Rect(shapePanel.x + 20f, shapePanel.y + 94f, 210f, 34f), MenuDwellSeconds, () => ReplaceLabObject(PrimitiveType.Sphere));
            DrawHoverButton("shape-cylinder", "Cylinder", new Rect(shapePanel.x + 20f, shapePanel.y + 138f, 210f, 34f), MenuDwellSeconds, () => ReplaceLabObject(PrimitiveType.Cylinder));
        }

        private void DrawLevel1Hud()
        {
            DrawPanel(new Rect(24, 24, 360, 166));
            GUI.Label(new Rect(44, 44, 320, 26), "Level 1: First Path");
            GUI.Label(new Rect(44, 74, 320, 24), boxHeld ? "Pinch: moving the box" : "Pinch the box and move it away.");
            GUI.Label(new Rect(44, 100, 320, 24), levelBall != null ? $"Ball speed: {levelBall.Speed:0.00}" : "Ball speed: 0.00");
            DrawHoverButton("level1-calibrate", "Calibrate", new Rect(44, 136, 150, 36), MenuDwellSeconds, ResetToCalibration);
        }

        private void DrawPassHud()
        {
            var panelX = Screen.width / 2f - 185f;
            DrawPanel(new Rect(panelX, 70, 370, 210));
            GUI.Label(new Rect(panelX + 40, 98, 300, 34), "PASS");
            GUI.Label(new Rect(panelX + 40, 132, 300, 24), "The ball reached the altar.");
            DrawHoverButton("pass-restart", "Restart", new Rect(Screen.width / 2f - 140f, 170, 120, 40), MenuDwellSeconds, () => StartLevel(lastLevel));
            DrawHoverButton("pass-level0", "Tutorial", new Rect(Screen.width / 2f + 20f, 170, 120, 40), MenuDwellSeconds, () => StartLevel(GameMode.Level0));
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
            tutorialStage = TutorialStage.FindHands;
            tutorialProgressStart = -1f;
            tutorialObjectMoved = false;
            tutorialObjectRotated = false;
            tutorialMapAdjusted = false;
            twoFingerMapStartDistance = 0f;
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
            labRenderer = labObject.GetComponent<Renderer>();
        }

        private void UpdateLevel0(GestureHandFrame hand)
        {
            if (labBody == null)
            {
                return;
            }

            var frame = receiver != null && receiver.HasFreshFrame ? receiver.Latest : GestureFrame.Neutral;
            UpdateTutorialStage(frame);
            var target = ScreenToWorldPlane(hand.pinchX, hand.pinchY, 0.7f);
            var isPinch = IsPinching(hand);
            var twoHandsPinching = TryGetTwoPinchingHands(out var a, out var b);
            var close = Vector3.Distance(target, labBody.position) < 1.0f;
            if (labRenderer != null)
            {
                labRenderer.sharedMaterial = boxIdle;
            }
            if (!labHeld && isPinch && close && !twoHandsPinching && CanDragLabObject())
            {
                labHeld = true;
                labGrabOffset = labBody.position - target;
                tutorialDragStart = labBody.position;
            }
            if (labHeld && (!isPinch || twoHandsPinching))
            {
                labHeld = false;
            }
            if (!labHeld && close && CanDragLabObject() && labRenderer != null)
            {
                labRenderer.sharedMaterial = boxHover;
            }
            if (labHeld)
            {
                target += labGrabOffset;
                target.x = Mathf.Clamp(target.x, -2.45f, 2.45f);
                target.z = Mathf.Clamp(target.z, -1.35f, 1.35f);
                target.y = 0.7f;
                labBody.MovePosition(target);
                tutorialObjectMoved = Vector3.Distance(labBody.position, tutorialDragStart) > 0.55f;
                if (labRenderer != null)
                {
                    labRenderer.sharedMaterial = boxHeldMaterial;
                }
            }

            if (twoHandsPinching && CanRotateLabObject())
            {
                if (labRenderer != null)
                {
                    labRenderer.sharedMaterial = boxHeldMaterial;
                }
                var angle = Mathf.Atan2(b.pinchY - a.pinchY, b.pinchX - a.pinchX);
                if (twoHandStartDistance <= 0f)
                {
                    var distance = Vector2.Distance(new Vector2(a.pinchX, a.pinchY), new Vector2(b.pinchX, b.pinchY));
                    twoHandStartDistance = Mathf.Max(distance, 0.001f);
                    twoHandStartAngle = angle;
                    twoHandStartRotation = labObject.transform.rotation;
                }
                var deltaDegrees = Mathf.DeltaAngle(twoHandStartAngle * Mathf.Rad2Deg, angle * Mathf.Rad2Deg);
                labObject.transform.rotation = twoHandStartRotation * Quaternion.Euler(0f, deltaDegrees, 0f);
                tutorialObjectRotated = Mathf.Abs(deltaDegrees) > 12f;
            }
            else
            {
                twoHandStartDistance = 0f;
            }

            if (CanControlMap() && TryGetTwoFingerMapHands(frame, out var left, out var right))
            {
                var leftPoint = FingerMidpoint(left);
                var rightPoint = FingerMidpoint(right);
                var distance = Vector2.Distance(leftPoint, rightPoint);
                var angle = Mathf.Atan2(rightPoint.y - leftPoint.y, rightPoint.x - leftPoint.x);
                if (twoFingerMapStartDistance <= 0f)
                {
                    twoFingerMapStartDistance = Mathf.Max(distance, 0.001f);
                    twoFingerMapStartAngle = angle;
                    twoFingerMapStartScale = levelRoot.localScale;
                    twoFingerMapStartRotation = levelRoot.rotation;
                }
                var mapScale = Mathf.Clamp(twoFingerMapStartScale.x * distance / twoFingerMapStartDistance, 0.82f, 1.22f);
                levelRoot.localScale = Vector3.one * mapScale;
                levelRoot.rotation = twoFingerMapStartRotation * Quaternion.Euler(0f, (angle - twoFingerMapStartAngle) * Mathf.Rad2Deg, 0f);
                tutorialMapAdjusted = Mathf.Abs(mapScale - twoFingerMapStartScale.x) > 0.06f || Mathf.Abs(Mathf.DeltaAngle(levelRoot.eulerAngles.y, twoFingerMapStartRotation.eulerAngles.y)) > 7f;
            }
            else
            {
                twoFingerMapStartDistance = 0f;
            }
        }

        private bool CanDragLabObject()
        {
            return tutorialStage == TutorialStage.OneHandDrag || tutorialStage == TutorialStage.Complete;
        }

        private bool CanRotateLabObject()
        {
            return tutorialStage == TutorialStage.TwoHandRotate || tutorialStage == TutorialStage.Complete;
        }

        private bool CanControlMap()
        {
            return tutorialStage == TutorialStage.MapControl || tutorialStage == TutorialStage.Complete;
        }

        private void UpdateTutorialStage(GestureFrame frame)
        {
            switch (tutorialStage)
            {
                case TutorialStage.FindHands:
                    UpdateStageHold(HasLeftAndRightHands(frame), TutorialStage.OneHandDrag);
                    break;
                case TutorialStage.OneHandDrag:
                    UpdateStageHold(tutorialObjectMoved, TutorialStage.TwoHandRotate);
                    break;
                case TutorialStage.TwoHandRotate:
                    UpdateStageHold(tutorialObjectRotated, TutorialStage.MapControl);
                    break;
                case TutorialStage.MapControl:
                    UpdateStageHold(tutorialMapAdjusted, TutorialStage.Complete);
                    break;
                case TutorialStage.Complete:
                    labCompleted = true;
                    break;
            }
        }

        private void UpdateStageHold(bool condition, TutorialStage nextStage)
        {
            if (condition)
            {
                tutorialProgressStart = tutorialProgressStart < 0f ? Time.time : tutorialProgressStart;
                if (Time.time - tutorialProgressStart >= TutorialHoldSeconds)
                {
                    tutorialStage = nextStage;
                    tutorialProgressStart = -1f;
                    labHeld = false;
                    twoHandStartDistance = 0f;
                    twoFingerMapStartDistance = 0f;
                }
            }
            else
            {
                tutorialProgressStart = -1f;
            }
        }

        private string TutorialTitle()
        {
            return tutorialStage switch
            {
                TutorialStage.FindHands => "1/5 Show both hands and point with index fingers.",
                TutorialStage.OneHandDrag => "2/5 Pinch an object with one hand and drag it.",
                TutorialStage.TwoHandRotate => "3/5 Pinch both sides and rotate the object.",
                TutorialStage.MapControl => "4/5 Bring index and middle fingers together on both hands.",
                TutorialStage.Complete => "5/5 Tutorial complete. Keep practicing or enter Level 1.",
                _ => "",
            };
        }

        private string TutorialDetail()
        {
            return tutorialStage switch
            {
                TutorialStage.FindHands => "Keep left and right hands visible. The skeleton labels show which hand is detected.",
                TutorialStage.OneHandDrag => "Touch thumb and index finger, grab the object, then move it across the practice slab.",
                TutorialStage.TwoHandRotate => "Pinch the object from both sides, then turn your hands like rotating a real block.",
                TutorialStage.MapControl => "On each hand, keep index and middle fingertips close, then move both hands to adjust the map.",
                TutorialStage.Complete => "You can still drag, rotate, and adjust the map. Hold over Next: Level 1 when ready.",
                _ => "",
            };
        }

        private float TutorialProgress()
        {
            if (tutorialStage == TutorialStage.Complete)
            {
                return 1f;
            }
            return tutorialProgressStart < 0f ? 0f : Mathf.Clamp01((Time.time - tutorialProgressStart) / TutorialHoldSeconds);
        }

        private string HandStatusText()
        {
            var frame = receiver != null && receiver.HasFreshFrame ? receiver.Latest : GestureFrame.Neutral;
            var left = HasHandedness(frame, "Left") ? "Left: yes" : "Left: waiting";
            var right = HasHandedness(frame, "Right") ? "Right: yes" : "Right: waiting";
            return $"{left}    {right}    Hands: {frame.handCount}";
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
            }
            if (boxHeld && !isPinch)
            {
                boxHeld = false;
            }
            if (boxHeld)
            {
                obstacleRenderer.sharedMaterial = boxHeldMaterial;
                obstacleBox.MovePosition(target);
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

        private static bool HasLeftAndRightHands(GestureFrame frame)
        {
            return HasHandedness(frame, "Left") && HasHandedness(frame, "Right");
        }

        private static bool HasHandedness(GestureFrame frame, string handedness)
        {
            if (frame.hands == null)
            {
                return false;
            }

            foreach (var hand in frame.hands)
            {
                if (hand.score >= 0.35f && string.Equals(hand.handedness, handedness, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private bool TryGetTwoFingerMapHands(GestureFrame frame, out GestureHandFrame a, out GestureHandFrame b)
        {
            a = default;
            b = default;
            if (frame.hands == null || frame.hands.Length < 2)
            {
                return false;
            }

            var found = 0;
            foreach (var hand in frame.hands)
            {
                if (hand.score < 0.35f || !IndexMiddleTogether(hand))
                {
                    continue;
                }

                if (found == 0)
                {
                    a = hand;
                }
                else
                {
                    b = hand;
                    return true;
                }
                found++;
            }
            return false;
        }

        private static bool IndexMiddleTogether(GestureHandFrame hand)
        {
            if (hand.landmarks == null || hand.landmarks.Length < 21 || hand.palmSpan <= 0f)
            {
                return false;
            }

            var indexTip = hand.landmarks[8];
            var middleTip = hand.landmarks[12];
            var dx = indexTip.x - middleTip.x;
            var dy = indexTip.y - middleTip.y;
            var dz = indexTip.z - middleTip.z;
            var distance = Mathf.Sqrt(dx * dx + dy * dy + dz * dz) / Mathf.Max(hand.palmSpan, 0.0001f);
            return distance < 0.55f && hand.indexExtended && hand.middleExtended;
        }

        private static Vector2 FingerMidpoint(GestureHandFrame hand)
        {
            if (hand.landmarks == null || hand.landmarks.Length < 13)
            {
                return new Vector2(hand.indexX, hand.indexY);
            }

            return new Vector2(
                (hand.landmarks[8].x + hand.landmarks[12].x) * 0.5f,
                (hand.landmarks[8].y + hand.landmarks[12].y) * 0.5f);
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
            DrawButtonCore(key, label, rect, dwellSeconds, action, false);
        }

        private void DrawUtilityButton(string key, string label, Rect rect, float dwellSeconds, System.Action action)
        {
            DrawButtonCore(key, label, rect, dwellSeconds, action, true);
        }

        private void DrawButtonCore(string key, string label, Rect rect, float dwellSeconds, System.Action action, bool allowMouseClick)
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
            var clicked = allowMouseClick ? GUI.Button(rect, label) : false;
            if (!allowMouseClick)
            {
                GUI.Box(rect, label);
            }
            var bar = new Rect(rect.x, rect.yMax - 6f, rect.width * progress, 6f);
            GUI.color = Color.cyan;
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = Color.white;
            if (clicked || progress >= 1f)
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

        private void DrawHandSkeletonOverlay()
        {
            var frame = receiver != null && receiver.HasFreshFrame ? receiver.Latest : GestureFrame.Neutral;
            if (frame.hands == null || frame.hands.Length == 0)
            {
                var message = launchedBridge
                    ? "Waiting for camera hand tracking..."
                    : bridgeStatus;
                GUI.Label(new Rect(Screen.width / 2f - 170f, Screen.height - 48f, 340f, 24f), message);
                return;
            }

            foreach (var hand in frame.hands)
            {
                if (hand.landmarks == null || hand.landmarks.Length < 21)
                {
                    continue;
                }

                var baseColor = string.Equals(hand.handedness, "Left", System.StringComparison.OrdinalIgnoreCase)
                    ? new Color(0.20f, 0.74f, 1f, 0.96f)
                    : new Color(1f, 0.72f, 0.20f, 0.96f);
                var pinchColor = IsPinching(hand) ? new Color(0.12f, 1f, 0.85f, 1f) : baseColor;
                for (var i = 0; i < HandConnectionPairs.Length; i += 2)
                {
                    var a = LandmarkToScreen(hand.landmarks[HandConnectionPairs[i]]);
                    var b = LandmarkToScreen(hand.landmarks[HandConnectionPairs[i + 1]]);
                    DrawLine(a, b, pinchColor, 3f);
                }

                for (var i = 0; i < hand.landmarks.Length; i++)
                {
                    var point = LandmarkToScreen(hand.landmarks[i]);
                    var radius = i == 4 || i == 8 ? 5f : 3.5f;
                    GUI.color = i == 4 || i == 8 ? Color.cyan : baseColor;
                    GUI.DrawTexture(new Rect(point.x - radius, point.y - radius, radius * 2f, radius * 2f), Texture2D.whiteTexture);
                }
                var label = $"{hand.handedness} {hand.score:0.00}";
                var labelPoint = LandmarkToScreen(hand.landmarks[0]);
                GUI.color = baseColor;
                GUI.Label(new Rect(labelPoint.x + 8f, labelPoint.y - 10f, 110f, 22f), label);
                GUI.color = Color.white;
            }
        }

        private static Vector2 LandmarkToScreen(GestureLandmark landmark)
        {
            return new Vector2(landmark.x * Screen.width, landmark.y * Screen.height);
        }

        private void DrawLine(Vector2 start, Vector2 end, Color color, float width)
        {
            lineTexture ??= Texture2D.whiteTexture;
            var delta = end - start;
            var angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            var oldColor = GUI.color;
            GUI.color = color;
            GUIUtility.RotateAroundPivot(angle, start);
            GUI.DrawTexture(new Rect(start.x, start.y - width * 0.5f, delta.magnitude, width), lineTexture);
            GUIUtility.RotateAroundPivot(-angle, start);
            GUI.color = oldColor;
        }

        private static void DrawProgressBar(float progress, Rect rect)
        {
            GUI.Box(rect, "");
            GUI.color = Color.cyan;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * progress, rect.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void StartGestureBridgeIfNeeded()
        {
            if (Application.isBatchMode)
            {
                bridgeStatus = "Camera bridge disabled in batch build.";
                return;
            }

            usesExternalBridge = HasCommandLineFlag("--gesture-bridge-external");
            if (usesExternalBridge)
            {
                launchedBridge = true;
                bridgeStatus = "Camera bridge started by launcher; waiting for hand...";
                return;
            }

            if (launchedBridge)
            {
                return;
            }

            launchedBridge = true;
            var bridgeDirectory = FindBridgeDirectory();
            if (string.IsNullOrEmpty(bridgeDirectory))
            {
                bridgeStatus = "Camera bridge not found. Use Play-HandOfGod.bat.";
                Debug.LogWarning(bridgeStatus);
                return;
            }

            StartHiddenGestureBridge(bridgeDirectory);
        }

        private void StartHiddenGestureBridge(string bridgeDirectory)
        {
            if (bridgeProcess != null && !bridgeProcess.HasExited)
            {
                bridgeStatus = "Camera bridge is already running.";
                return;
            }

            var scriptPath = Path.Combine(bridgeDirectory, "mediapipe_udp_sender.py");
            var python = ResolveGesturePython(bridgeDirectory);
            var logPath = Path.Combine(bridgeDirectory, "gesture-bridge-runtime.log");
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = python,
                    Arguments = $"\"{scriptPath}\" --no-preview",
                    WorkingDirectory = bridgeDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                };
                launchedBridge = true;
                bridgeStatus = "Starting camera...";
                bridgeProcess = Process.Start(startInfo);
                if (bridgeProcess != null)
                {
                    bridgeProcess.OutputDataReceived += (_, args) => AppendBridgeLog(logPath, args.Data);
                    bridgeProcess.ErrorDataReceived += (_, args) => AppendBridgeLog(logPath, args.Data);
                    bridgeProcess.BeginOutputReadLine();
                    bridgeProcess.BeginErrorReadLine();
                }
                bridgeStatus = "Camera bridge launched; waiting for hand...";
            }
            catch (System.Exception exception)
            {
                bridgeStatus = "Failed to launch camera bridge.";
                Debug.LogWarning($"Failed to launch gesture bridge: {exception.Message}");
            }
        }

        private void StartVisibleGestureBridge()
        {
            if (bridgeProcess != null && !bridgeProcess.HasExited)
            {
                bridgeStatus = "Camera bridge is already running.";
                return;
            }

            var bridgeDirectory = FindBridgeDirectory();
            if (string.IsNullOrEmpty(bridgeDirectory))
            {
                bridgeStatus = "Camera bridge not found. Start with Play-HandOfGod.bat.";
                return;
            }

            var scriptPath = Path.Combine(bridgeDirectory, "mediapipe_udp_sender.py");
            var python = ResolveGesturePython(bridgeDirectory);
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = python,
                    Arguments = $"\"{scriptPath}\" --no-preview",
                    WorkingDirectory = bridgeDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                };
                var logPath = Path.Combine(bridgeDirectory, "gesture-bridge-runtime.log");
                launchedBridge = true;
                bridgeStatus = "Starting camera...";
                bridgeProcess = Process.Start(startInfo);
                if (bridgeProcess != null)
                {
                    bridgeProcess.OutputDataReceived += (_, args) => AppendBridgeLog(logPath, args.Data);
                    bridgeProcess.ErrorDataReceived += (_, args) => AppendBridgeLog(logPath, args.Data);
                    bridgeProcess.BeginOutputReadLine();
                    bridgeProcess.BeginErrorReadLine();
                }
                usesExternalBridge = false;
                bridgeStatus = "Camera bridge started; waiting for image and hand...";
            }
            catch (System.Exception exception)
            {
                bridgeStatus = "Failed to start camera bridge.";
                Debug.LogWarning($"Failed to start gesture bridge: {exception.Message}");
            }
        }

        private static string FindBridgeDirectory()
        {
            var cliDirectory = GetCommandLineValue("--gesture-bridge-dir");
            if (!string.IsNullOrEmpty(cliDirectory) && File.Exists(Path.Combine(cliDirectory, "mediapipe_udp_sender.py")))
            {
                return cliDirectory;
            }

            var candidates = new System.Collections.Generic.List<string>
            {
                Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "gesture_bridge")),
                Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "..", "gesture_bridge")),
                Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "gesture_bridge")),
                Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "..", "unity", "gesture_bridge")),
                Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "..", "..", "unity", "gesture_bridge")),
            };

            var cursor = new DirectoryInfo(Application.dataPath);
            for (var i = 0; i < 8 && cursor != null; i++)
            {
                candidates.Add(Path.Combine(cursor.FullName, "gesture_bridge"));
                candidates.Add(Path.Combine(cursor.FullName, "unity", "gesture_bridge"));
                cursor = cursor.Parent;
            }

            foreach (var candidate in candidates)
            {
                if (File.Exists(Path.Combine(candidate, "mediapipe_udp_sender.py")))
                {
                    return candidate;
                }
            }
            return "";
        }

        private static string ResolveGesturePython(string bridgeDirectory)
        {
            var cliPython = GetCommandLineValue("--gesture-python");
            if (!string.IsNullOrEmpty(cliPython) && File.Exists(cliPython))
            {
                return cliPython;
            }

            var asciiRuntimePython = @"E:\Unity\HandOfGodGestureBridge\.venv\Scripts\python.exe";
            if (File.Exists(asciiRuntimePython))
            {
                return asciiRuntimePython;
            }

            var projectVenvPython = Path.Combine(bridgeDirectory, ".venv", "Scripts", "python.exe");
            return File.Exists(projectVenvPython) ? projectVenvPython : "python";
        }

        private static bool HasCommandLineFlag(string flag)
        {
            foreach (var argument in System.Environment.GetCommandLineArgs())
            {
                if (string.Equals(argument, flag, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static string GetCommandLineValue(string key)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], key, System.StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }
            return "";
        }

        private static void AppendBridgeLog(string logPath, string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return;
            }

            try
            {
                File.AppendAllText(logPath, $"[{System.DateTime.Now:HH:mm:ss}] {line}{System.Environment.NewLine}");
            }
            catch (System.Exception)
            {
                // Logging must not break gameplay.
            }
        }

        private void QuitGame()
        {
            if (Application.isEditor)
            {
                Debug.Log("Quit requested.");
                return;
            }
            Application.Quit();
        }

        private void OnApplicationQuit()
        {
            StopBridgeProcess();
        }

        private void OnDisable()
        {
            StopBridgeProcess();
        }

        private void OnDestroy()
        {
            StopBridgeProcess();
        }

        private void StopBridgeProcess()
        {
            if (bridgeProcess == null)
            {
                return;
            }

            try
            {
                if (!bridgeProcess.HasExited)
                {
                    KillProcessTree(bridgeProcess.Id);
                    if (!bridgeProcess.WaitForExit(1200))
                    {
                        bridgeProcess.Kill();
                        bridgeProcess.WaitForExit(500);
                    }
                }
            }
            catch (System.Exception)
            {
                // Process may already be gone during application shutdown.
            }
            finally
            {
                bridgeProcess.Dispose();
                bridgeProcess = null;
                launchedBridge = false;
            }
        }

        private static void KillProcessTree(int processId)
        {
            if (processId <= 0)
            {
                return;
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "taskkill",
                    Arguments = $"/PID {processId} /T /F",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                };
                using var killer = Process.Start(startInfo);
                killer?.WaitForExit(1000);
            }
            catch (System.Exception)
            {
                // Fall back to Process.Kill in the caller.
            }
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
            labRenderer = null;
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
            BuildCameraBackgroundPlane(cameraObject.transform);

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

        private void BuildCameraBackgroundPlane(Transform cameraTransform)
        {
            var plane = GameObject.CreatePrimitive(PrimitiveType.Quad);
            plane.name = "Embedded Camera Feed Background";
            plane.transform.SetParent(cameraTransform, false);
            plane.transform.localPosition = new Vector3(0f, 0f, 30f);
            plane.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            DestroyUnityObject(plane.GetComponent<Collider>());

            cameraBackgroundMaterial = new Material(Shader.Find("Unlit/Texture"))
            {
                name = "Embedded Camera Feed Material",
            };
            plane.GetComponent<Renderer>().sharedMaterial = cameraBackgroundMaterial;
            cameraBackgroundPlane = plane.transform;
            UpdateCameraBackground();
        }

        private void UpdateCameraBackground()
        {
            if (cameraBackgroundPlane == null || mainCamera == null)
            {
                return;
            }

            var height = mainCamera.orthographicSize * 2f;
            var width = height * Mathf.Max(Screen.width / (float)Mathf.Max(Screen.height, 1), 0.01f);
            cameraBackgroundPlane.localScale = new Vector3(width, height, 1f);

            if (cameraBackgroundMaterial != null && cameraFrames != null && cameraFrames.HasFreshFrame && cameraFrames.Texture != null)
            {
                cameraBackgroundMaterial.mainTexture = cameraFrames.Texture;
                cameraBackgroundMaterial.color = Color.white;
            }
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
