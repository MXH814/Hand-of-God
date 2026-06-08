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
            Level2,
            Pass,
        }

        private enum TutorialStage
        {
            FindHands,
            OneHandDrag,
            TwoHandRotate,
            BridgePull,
            PalmActivate,
            MapControl,
            AirflowDirection,
            Complete,
        }

        private enum Level1Stage
        {
            ClearBlock,
            JoinBridge,
            RotateGate,
            ActivateSeal,
            RunToGoal,
        }

        private const float CalibrationHoldSeconds = 1f;
        private const float MenuDwellSeconds = 0.85f;
        private const float SafeDwellSeconds = 1f;
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
        private Material level2FloorMaterial;
        private Material level2WallMaterial;
        private Material level2TrimMaterial;
        private Material level2PortalCoreMaterial;
        private Material level2WindMaterial;
        private Material level2WindRibbonMaterial;
        private Material level2WindMistMaterial;
        private Material level2PortalTwirlMaterial;
        private Material level2RuneMaterial;
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
        private bool tutorialStageSucceeded;
        private bool tutorialObjectMoved;
        private bool tutorialObjectRotated;
        private bool tutorialBridgePulled;
        private bool tutorialPalmActivated;
        private bool tutorialMapAdjusted;
        private bool tutorialAirflowDirected;
        private int tutorialAirflowPreviewDirection;
        private Vector3 tutorialDragStart;
        private Transform tutorialBridgeLeft;
        private Transform tutorialBridgeRight;
        private Transform tutorialBridgeRoot;
        private Transform tutorialSealRoot;
        private Transform tutorialAirflowRoot;
        private Renderer tutorialBridgeLeftRenderer;
        private Renderer tutorialBridgeRightRenderer;
        private Renderer tutorialSealRenderer;
        private Renderer tutorialAirflowPadRenderer;
        private Transform tutorialAirflowArrow;
        private float tutorialBridgeStartDistance;
        private float tutorialPalmStart = -1f;
        private float twoHandStartDistance;
        private float twoHandStartAngle;
        private Quaternion twoHandStartRotation;
        private float twoFingerMapStartDistance;
        private float twoFingerMapStartAngle;
        private Vector3 twoFingerMapStartScale;
        private Quaternion twoFingerMapStartRotation;
        private Rigidbody obstacleBox;
        private Renderer obstacleRenderer;
        private Vector3 level1BoxGrabOffset;
        private bool boxHeld;
        private BallController levelBall;
        private Level1Stage level1Stage;
        private Renderer blockSlotRenderer;
        private Transform bridgeLeft;
        private Transform bridgeRight;
        private Renderer bridgeLeftRenderer;
        private Renderer bridgeRightRenderer;
        private Transform rotateGate;
        private Renderer rotateGateRenderer;
        private Renderer sealRenderer;
        private bool bridgeLocked;
        private bool rotateGateLocked;
        private bool sealActivated;
        private bool rotateGateHeld;
        private float level1BridgeStartDistance;
        private float level1RotateStartAngle;
        private float sealHoldStart = -1f;
        private GameObject startGate;
        private GameObject bridgeGate;
        private GameObject rotateGateStop;
        private GameObject goalGate;
        private float level1SuccessUntil = -1f;
        private bool initialized;
        private Texture2D lineTexture;
        private Process bridgeProcess;
        private bool launchedBridge;
        private bool usesExternalBridge;
        private string bridgeStatus = "Starting camera...";

        // Level2: Portal + Airflow mechanics
        private GameObject portalKey;
        private Rigidbody portalKeyBody;
        private Renderer portalKeyRenderer;
        private Renderer[] portalKeyRenderers;
        private Material[] portalKeyIdleMaterials;
        private Transform runeLeft;
        private Transform runeRight;
        private Renderer runeLeftRenderer;
        private Renderer runeRightRenderer;
        private GameObject runeLeftArrow;
        private GameObject runeRightArrow;
        private string level2HintMessage = "";
        private Vector3 portalAPosition;
        private Vector3 portalBPosition;
        private bool portalAActive;
        private bool portalBActive;
        private Renderer portalARenderer;
        private Renderer portalBRenderer;
        private Transform[] airBelts;
        private int[] airBeltDirection;
        private Renderer[] airBeltRenderers;
        private AirBeltTrigger[] airBeltTriggers;
        private Transform[] airBeltArrowTransforms;
        private Renderer[] airBeltArrowRenderers;
        private Transform[] airBeltStreaks;
        private Renderer[] airBeltStreakRenderers;
        private Transform[] airBeltMistQuads;
        private Renderer[] airBeltMistRenderers;
        private ParticleSystem[] airBeltParticles;
        private ParticleSystem portalAParticles;
        private ParticleSystem portalBParticles;
        private Rigidbody levelBallBody;
        private Renderer levelBallRenderer;
        private Material levelBallRuntimeMaterial;
        private Vector3 levelBallBaseScale = Vector3.one;
        private const float AirBeltForce = 5.2f;
        private bool keyHeld;
        private Vector3 keyGrabOffset;
        private float level2LastTeleport = -10f;
        private bool level2Teleporting;
        private float level2TeleportStart;
        private Vector3 level2TeleportStartPosition;
        private Vector3 level2TeleportEndPosition;
        private int pendingAirDirection;
        private float pendingAirDirectionStart = -1f;
        private const float AirflowDirectionDeadZone = 0.045f;
        private const float AirflowDirectionHoldSeconds = 0.25f;
        private float levelStartTime = -10f;

        // tuning: larger grab radii for the portal key to make it easier to pick up
        private const float PortalKeyGrabPinchRadius = 1.6f;
        private const float PortalKeyGrabIdleRadius = 1.1f;

        // UI/debug helpers
        private bool lastPinchState = false;
        private bool lastKeyInRange = false;
        private float keyHoverStart = -1f;
        private const float KeyDwellSeconds = 0.6f;

        public void Configure(GestureUdpReceiver gestureReceiver, CameraFrameReceiver frameReceiver)
        {
            receiver = gestureReceiver;
            cameraFrames = frameReceiver;
        }

        // Editor helper: allow editor scripts to start a specific GameMode by index
        public void EditorStartLevel(int levelIndex)
        {
            // levelIndex mapping: 0=CalibrationOpen,1=CalibrationPinch,2=Menu,3=Level0,4=Level1,5=Level2,6=Pass
            if (levelIndex == 3) StartLevel(GameMode.Level0);
            else if (levelIndex == 4) StartLevel(GameMode.Level1);
            else if (levelIndex == 5) StartLevel(GameMode.Level2);
            else if (levelIndex == 2) { mode = GameMode.Menu; SetLobbyVisible(true); }
        }

        public void EditorPreviewLevel2Airflow()
        {
            StartLevel(GameMode.Level2);
            portalAActive = true;
            if (runeLeftRenderer != null) runeLeftRenderer.sharedMaterial = tealGlow;
            if (portalARenderer != null) portalARenderer.sharedMaterial = tealGlow;
            if (portalBRenderer != null) portalBRenderer.sharedMaterial = tealGlow;
            if (levelBall != null)
            {
                levelBall.transform.position = portalBPosition + new Vector3(0f, 0.2f, 0f);
            }
            if (levelBallBody != null)
            {
                levelBallBody.isKinematic = false;
                levelBallBody.linearVelocity = Vector3.zero;
                levelBallBody.angularVelocity = Vector3.zero;
            }
            OpenGate(bridgeGate);
            SetAirBeltDirection(1);
            OpenGate(rotateGateStop);
            level1Stage = Level1Stage.RunToGoal;
            level2HintMessage = "Airflow: RIGHT. The wind will gradually carry the ball to the altar.";
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
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (string.Equals(activeScene.name, "Level02", System.StringComparison.OrdinalIgnoreCase))
            {
                StartLevel(GameMode.Level2);
            }
            else
            {
                ResetToCalibration();
            }
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

            if (mode == GameMode.Level2)
            {
                UpdateLevel2Autonomous();
            }

            if (!TryGetPrimaryHand(out var hand))
            {
                if (mode == GameMode.Level2)
                {
                    ReleaseLevel2KeyIfTrackingLost();
                    pendingAirDirection = 0;
                    pendingAirDirectionStart = -1f;
                }
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
                case GameMode.Level2:
                    UpdateLevel2(hand);
                    break;
            }

            HandleLevel1BallState();
        }

        private bool HandleLevel1BallState()
        {
            if ((mode != GameMode.Level1 && mode != GameMode.Level2) || levelBall == null)
            {
                return false;
            }

            if (levelBall.Failed)
            {
                // restart current level
                Debug.Log("[LevelState] Ball failed — restarting level");
                StartLevel(mode == GameMode.Level1 ? GameMode.Level1 : GameMode.Level2);
                return true;
            }

            // Only allow automatic pass when the level is in the final "RunToGoal" stage.
            if (levelBall.ReachedGoal)
            {
                Debug.Log($"[LevelState] ReachedGoal detected. mode={mode} level1Stage={level1Stage} ballPos={levelBall.transform.position} timeSinceStart={Time.time - levelStartTime}");
                if (mode == GameMode.Level1 || (mode == GameMode.Level2 && level1Stage == Level1Stage.RunToGoal))
                {
                    Debug.Log("[LevelState] Level pass condition satisfied — switching to Pass mode");
                    mode = GameMode.Pass;
                    return true;
                }
                // ignore reached goal until level progression reaches running-to-goal phase
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
                case GameMode.Level2:
                    DrawLevel2Hud();
                    break;
                case GameMode.Pass:
                    DrawPassHud();
                    break;
            }

            DrawHandSkeletonOverlay();
            DrawCursor();

            DrawGlobalControls();
            DrawLevelSelectSidebar();
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
            var exitRect = new Rect(48, 38, 128, 46);
            DrawUtilityButton("global-exit", "Exit", exitRect, SafeDwellSeconds, QuitGame);
            if (mode != GameMode.CalibrationOpen && mode != GameMode.CalibrationPinch)
            {
                DrawUtilityButton("global-recalibrate", "Calibrate", new Rect(exitRect.xMax + 18, exitRect.y, 132, 46), MenuDwellSeconds, ResetToCalibration);
            }
        }

        private void DrawLevelSelectSidebar()
        {
            if (mode == GameMode.CalibrationOpen || mode == GameMode.CalibrationPinch)
            {
                return;
            }

            var panel = new Rect(48f, 104f, 228f, 190f);
            DrawPanel(panel);
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            GUI.Label(new Rect(panel.x + 12f, panel.y + 12f, panel.width - 24f, 26f), "Level Select", titleStyle);

            DrawLevelSelectButton("select-level0", "Level 0: Tutorial", GameMode.Level0, new Rect(panel.x + 18f, panel.y + 48f, panel.width - 36f, 34f));
            DrawLevelSelectButton("select-level1", "Level 1: Moving Path", GameMode.Level1, new Rect(panel.x + 18f, panel.y + 92f, panel.width - 36f, 34f));
            DrawLevelSelectButton("select-level2", "Level 2: Portals", GameMode.Level2, new Rect(panel.x + 18f, panel.y + 136f, panel.width - 36f, 34f));
        }

        private void DrawLevelSelectButton(string key, string label, GameMode targetMode, Rect rect)
        {
            var active = mode == targetMode;
            DrawHoverButton(key, active ? $"> {label}" : label, rect, MenuDwellSeconds, () => StartLevel(targetMode), 15);
        }

        private void DrawMenu()
        {
            DrawPanel(new Rect(40, 40, 390, 300));
            GUI.Label(new Rect(70, 62, 300, 30), "Hand of God");
            GUI.Label(new Rect(70, 92, 320, 24), "Hover your index finger over an option.");
            DrawHoverButton("start", "Start Game", new Rect(70, 120, 260, 42), MenuDwellSeconds, () => StartLevel(GameMode.Level1));
            DrawHoverButton("level0", "Level 0: Tutorial", new Rect(70, 172, 260, 42), MenuDwellSeconds, () => StartLevel(GameMode.Level0));
            DrawHoverButton("level1", "Level 1: First Path", new Rect(70, 224, 260, 42), MenuDwellSeconds, () => StartLevel(GameMode.Level1));
            DrawHoverButton("level2", "Level 2: Portals & Airflow", new Rect(70, 276, 260, 42), MenuDwellSeconds, () => StartLevel(GameMode.Level2));
            DrawHoverButton("recalibrate", "Recalibrate", new Rect(70, 328, 260, 42), MenuDwellSeconds, ResetToCalibration);
        }

        private void DrawLevel0Hud()
        {
            var titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            var stepStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            var detailStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            var statusStyle = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            var panelLeft = 340f;
            var panelWidth = Mathf.Min(Screen.width - panelLeft - 70f, 860f);
            panelLeft = Mathf.Max(panelLeft, Screen.width * 0.5f - panelWidth * 0.5f);
            var panel = new Rect(panelLeft, 42f, panelWidth, 170f);
            DrawPanel(panel);
            GUI.Label(new Rect(panel.x + 32f, panel.y + 16f, panel.width - 64f, 32f), "Level 0: Gesture Tutorial", titleStyle);
            GUI.Label(new Rect(panel.x + 32f, panel.y + 52f, panel.width - 64f, 28f), TutorialTitle(), stepStyle);
            GUI.Label(new Rect(panel.x + 32f, panel.y + 84f, panel.width - 64f, 48f), TutorialDetail(), detailStyle);
            GUI.Label(new Rect(panel.x + 32f, panel.y + 136f, panel.width - 64f, 22f), $"{HandStatusText()}    Pinch threshold: {pinchThreshold:0.00}", statusStyle);

            if (TutorialStageSucceeded())
            {
                var finalStage = IsFinalTutorialStage();
                DrawSuccessBanner(new Rect(Screen.width * 0.5f - 210f, panel.yMax + 14f, 420f, 58f), finalStage ? "TUTORIAL COMPLETE" : "SUCCESS");
                var buttonLabel = finalStage ? "Next: Level 1" : "Continue";
                var buttonAction = finalStage ? (System.Action)(() => StartLevel(GameMode.Level1)) : AdvanceTutorialStage;
                DrawHoverButton("tutorial-continue", buttonLabel, new Rect(Screen.width * 0.5f - 170f, panel.yMax + 82f, 340f, 64f), MenuDwellSeconds, buttonAction, 24);
            }

            DrawTutorialStageMenu();
            if (TutorialUsesLabObject())
            {
                var shapePanel = new Rect(48f, 590f, 260f, 124f);
                DrawPanel(shapePanel);
                GUI.Label(new Rect(shapePanel.x + 20f, shapePanel.y + 10f, 220f, 22f), "Practice object");
                DrawHoverButton("shape-cube", "Cube", new Rect(shapePanel.x + 20f, shapePanel.y + 36f, 210f, 24f), MenuDwellSeconds, () => ReplaceLabObject(PrimitiveType.Cube), 13);
                DrawHoverButton("shape-sphere", "Sphere", new Rect(shapePanel.x + 20f, shapePanel.y + 64f, 210f, 24f), MenuDwellSeconds, () => ReplaceLabObject(PrimitiveType.Sphere), 13);
                DrawHoverButton("shape-cylinder", "Cylinder", new Rect(shapePanel.x + 20f, shapePanel.y + 92f, 210f, 24f), MenuDwellSeconds, () => ReplaceLabObject(PrimitiveType.Cylinder), 13);
            }
        }

        private void DrawTutorialStageMenu()
        {
            var panel = new Rect(48f, 310f, 260f, 268f);
            DrawPanel(panel);
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            GUI.Label(new Rect(panel.x + 12f, panel.y + 10f, panel.width - 24f, 24f), "Tutorial Steps", titleStyle);
            var y = panel.y + 42f;
            DrawTutorialStageButton("tutorial-jump-hands", "1  Recognize hands", TutorialStage.FindHands, new Rect(panel.x + 18f, y, panel.width - 36f, 26f));
            DrawTutorialStageButton("tutorial-jump-drag", "2  One-hand drag", TutorialStage.OneHandDrag, new Rect(panel.x + 18f, y + 30f, panel.width - 36f, 26f));
            DrawTutorialStageButton("tutorial-jump-rotate", "3  Two-hand rotate", TutorialStage.TwoHandRotate, new Rect(panel.x + 18f, y + 60f, panel.width - 36f, 26f));
            DrawTutorialStageButton("tutorial-jump-bridge", "4  Join bridge", TutorialStage.BridgePull, new Rect(panel.x + 18f, y + 90f, panel.width - 36f, 26f));
            DrawTutorialStageButton("tutorial-jump-palm", "5  Palm seal", TutorialStage.PalmActivate, new Rect(panel.x + 18f, y + 120f, panel.width - 36f, 26f));
            DrawTutorialStageButton("tutorial-jump-map", "6  Map control", TutorialStage.MapControl, new Rect(panel.x + 18f, y + 150f, panel.width - 36f, 26f));
            DrawTutorialStageButton("tutorial-jump-air", "7  Airflow", TutorialStage.AirflowDirection, new Rect(panel.x + 18f, y + 180f, panel.width - 36f, 26f));
        }

        private void DrawTutorialStageButton(string key, string label, TutorialStage targetStage, Rect rect)
        {
            var active = tutorialStage == targetStage;
            DrawHoverButton(key, active ? $"> {label}" : label, rect, MenuDwellSeconds, () => SelectTutorialStage(targetStage), 12);
        }

        private void DrawLevel1Hud()
        {
            var panelWidth = Mathf.Min(Screen.width - 180f, 820f);
            var panel = new Rect(Screen.width * 0.5f - panelWidth * 0.5f, 42f, panelWidth, 132f);
            DrawPanel(panel);
            var titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            var objectiveStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true };
            GUI.Label(new Rect(panel.x + 28f, panel.y + 16f, panel.width - 56f, 30f), "Level 1: Trial of the Moving Path", titleStyle);
            GUI.Label(new Rect(panel.x + 28f, panel.y + 52f, panel.width - 56f, 42f), Level1ObjectiveText(), objectiveStyle);
            GUI.Label(new Rect(panel.x + 28f, panel.y + 96f, panel.width - 56f, 24f), levelBall != null ? $"Ball speed: {levelBall.Speed:0.00}" : "Ball speed: 0.00");
            if (Time.time < level1SuccessUntil)
            {
                DrawSuccessBanner(new Rect(Screen.width * 0.5f - 135f, panel.yMax + 12f, 270f, 50f), "SUCCESS");
            }
        }

        private void DrawPassHud()
        {
            var panelX = Screen.width / 2f - 230f;
            DrawPanel(new Rect(panelX, 64, 460, 250));
            DrawSuccessBanner(new Rect(panelX + 40, 92, 380, 70), "PASS");
            var messageStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold,
            };
            GUI.color = new Color(0.92f, 1f, 0.94f, 1f);
            GUI.Label(new Rect(panelX + 48, 166, 364, 30), "The ball reached the altar.", messageStyle);
            GUI.color = Color.white;
            DrawHoverButton("pass-restart", "Restart", new Rect(Screen.width / 2f - 230f, 222, 150, 52), MenuDwellSeconds, () => StartLevel(lastLevel), 20);
            // 当上一关是 Level1 时，显示进入 Level2 的按钮
            if (lastLevel == GameMode.Level1)
            {
                DrawHoverButton("pass-next", "Next: Level 2", new Rect(Screen.width / 2f - 50f, 222, 150, 52), MenuDwellSeconds, () => StartLevel(GameMode.Level2), 20);
                DrawHoverButton("pass-level0", "Tutorial", new Rect(Screen.width / 2f + 130f, 222, 150, 52), MenuDwellSeconds, () => StartLevel(GameMode.Level0), 20);
            }
            else
            {
                DrawHoverButton("pass-level0", "Tutorial", new Rect(Screen.width / 2f + 20f, 222, 150, 52), MenuDwellSeconds, () => StartLevel(GameMode.Level0), 20);
            }
        }

        private void DrawLevel2Hud()
        {
            var panelWidth = Mathf.Min(Screen.width - 180f, 820f);
            var panel = new Rect(Screen.width * 0.5f - panelWidth * 0.5f, 42f, panelWidth, 132f);
            DrawPanel(panel);
            var titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            var objectiveStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true };
            GUI.Label(new Rect(panel.x + 28f, panel.y + 16f, panel.width - 56f, 30f), "Level 2: Portals & Airflow", titleStyle);
            var detail = level1Stage switch
            {
                Level1Stage.ClearBlock => "Drag the glowing key onto the left rune to unlock the path.",
                Level1Stage.JoinBridge => "Use the airflow gesture: thumb out, index+middle together, ring+pinky folded.",
                Level1Stage.RotateGate => "Place the key on the right rune to activate the portal.",
                Level1Stage.RunToGoal => "The path is open. Guide the ball to the altar.",
                _ => "",
            };
            GUI.Label(new Rect(panel.x + 28f, panel.y + 52f, panel.width - 56f, 42f), detail, objectiveStyle);
            var wind = airBeltDirection != null && airBeltDirection.Length > 0 ? airBeltDirection[0] : 0;
            var windText = wind == 1 ? "RIGHT" : (wind == -1 ? "LEFT" : "OFF");
            GUI.Label(new Rect(panel.x + 28f, panel.y + 96f, panel.width - 56f, 24f), levelBall != null ? $"Ball speed: {levelBall.Speed:0.00}    Airflow: {windText}" : $"Ball speed: 0.00    Airflow: {windText}");

            // dynamic level hints
            if (!string.IsNullOrEmpty(level2HintMessage))
            {
                var hintRect = new Rect(panel.x + 28f, panel.y + 128f, panel.width - 56f, 28f);
                GUI.Label(hintRect, level2HintMessage);
            }
        }

        private void DrawSuccessBanner(Rect rect, string text)
        {
            var pulse = (Mathf.Sin(Time.time * 5.4f) + 1f) * 0.5f;
            var glowRect = new Rect(rect.x - 12f, rect.y - 8f, rect.width + 24f, rect.height + 16f);
            var oldColor = GUI.color;
            GUI.color = new Color(0.08f, 1f, 0.76f, 0.20f + pulse * 0.18f);
            GUI.DrawTexture(glowRect, Texture2D.whiteTexture);

            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(30f + pulse * 6f),
                fontStyle = FontStyle.Bold,
            };
            GUI.color = Color.black;
            GUI.Label(new Rect(rect.x + 3f, rect.y + 3f, rect.width, rect.height), text, style);
            GUI.color = new Color(0.16f, 1f, 0.82f, 1f);
            GUI.Label(rect, text, style);
            GUI.color = oldColor;
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
            else if (level == GameMode.Level1)
            {
                BuildLevel1();
            }
            else if (level == GameMode.Level2)
            {
                BuildLevel2();
            }
            mode = level;
            levelStartTime = Time.time;
            hoverKey = "";
            hoverStart = -1f;
        }

        private void BuildLevel0()
        {
            levelRoot = new GameObject("Level00 Gesture Lab").transform;
            CreateBox("lab base", new Vector3(0f, -0.15f, 0f), new Vector3(8.4f, 0.3f, 4.6f), darkStone, levelRoot, Quaternion.identity, true);
            CreateBox("lab guide", new Vector3(0f, 0.02f, 0f), new Vector3(6.2f, 0.05f, 3.15f), paleStone, levelRoot, Quaternion.identity, false);
            ReplaceLabObject(PrimitiveType.Cube);
            labCompleted = false;
            tutorialStage = TutorialStage.FindHands;
            tutorialStageSucceeded = false;
            tutorialObjectMoved = false;
            tutorialObjectRotated = false;
            tutorialBridgePulled = false;
            tutorialPalmActivated = false;
            tutorialMapAdjusted = false;
            tutorialAirflowDirected = false;
            tutorialAirflowPreviewDirection = 0;
            tutorialPalmStart = -1f;
            BuildTutorialMechanisms();
            twoFingerMapStartDistance = 0f;
            ApplyTutorialStageVisibility();
        }

        private void BuildTutorialMechanisms()
        {
            tutorialBridgeRoot = new GameObject("tutorial bridge props").transform;
            tutorialBridgeRoot.SetParent(levelRoot, false);
            var left = CreateBox("tutorial bridge left half", new Vector3(-0.55f, 0.22f, -1.05f), new Vector3(1.0f, 0.08f, 0.42f), boxHover, tutorialBridgeRoot, Quaternion.identity, true);
            var right = CreateBox("tutorial bridge right half", new Vector3(0.55f, 0.22f, -1.05f), new Vector3(1.0f, 0.08f, 0.42f), boxHover, tutorialBridgeRoot, Quaternion.identity, true);
            tutorialBridgeLeft = left.transform;
            tutorialBridgeRight = right.transform;
            tutorialBridgeLeftRenderer = left.GetComponent<Renderer>();
            tutorialBridgeRightRenderer = right.GetComponent<Renderer>();

            tutorialSealRoot = new GameObject("tutorial palm seal props").transform;
            tutorialSealRoot.SetParent(levelRoot, false);
            var seal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            seal.name = "tutorial palm seal";
            seal.transform.SetParent(tutorialSealRoot, false);
            seal.transform.position = new Vector3(0f, 0.16f, 1.05f);
            seal.transform.localScale = new Vector3(0.56f, 0.04f, 0.56f);
            tutorialSealRenderer = seal.GetComponent<Renderer>();
            tutorialSealRenderer.sharedMaterial = amberGlow;
            DestroyUnityObject(seal.GetComponent<Collider>());
            CreateTorus("tutorial seal ring", seal.transform.position + new Vector3(0f, 0.04f, 0f), 0.5f, 0.035f, tealGlow, tutorialSealRoot);

            BuildTutorialAirflowArea();
        }

        private void BuildTutorialAirflowArea()
        {
            tutorialAirflowRoot = new GameObject("tutorial level2 airflow props").transform;
            tutorialAirflowRoot.SetParent(levelRoot, false);

            var floor = CreateBox("tutorial level2 central wind gallery floor", new Vector3(1.25f, -0.015f, 0f), new Vector3(4.95f, 0.13f, 1.72f), level2FloorMaterial, tutorialAirflowRoot, Quaternion.identity, false);
            tutorialAirflowPadRenderer = floor.GetComponent<Renderer>();
            CreateTutorialChamberFrame(1.25f, 4.95f, 1.72f);
            CreateBox("tutorial level2 wind channel low glow", new Vector3(1.25f, 0.055f, 0f), new Vector3(4.65f, 0.006f, 0.045f), level2WallMaterial, tutorialAirflowRoot, Quaternion.identity, false);
            CreateTutorialWindFloorHints();

            airBelts = new Transform[1];
            airBeltDirection = new int[1];
            airBeltRenderers = new Renderer[1];
            airBeltTriggers = new AirBeltTrigger[1];
            airBeltArrowRenderers = new Renderer[1];
            airBeltArrowTransforms = new Transform[1];
            airBeltStreaks = new Transform[6];
            airBeltStreakRenderers = new Renderer[6];
            airBeltMistQuads = new Transform[4];
            airBeltMistRenderers = new Renderer[4];

            var belt = CreateBox("tutorial air belt trigger", new Vector3(1.25f, 0.18f, 0f), new Vector3(4.95f, 0.36f, 1.65f), level2WindMaterial, tutorialAirflowRoot, Quaternion.identity, true);
            airBelts[0] = belt.transform;
            airBeltRenderers[0] = belt.GetComponent<Renderer>();
            airBeltDirection[0] = 0;
            var col = belt.GetComponent<Collider>();
            col.isTrigger = true;
            var trigger = belt.AddComponent<AirBeltTrigger>();
            trigger.beltIndex = 0;
            trigger.direction = 0;
            trigger.force = AirBeltForce;
            trigger.maxWindSpeed = 1.95f;
            trigger.rampSeconds = 1.45f;
            airBeltTriggers[0] = trigger;

            var beltVisualRoot = new GameObject("tutorial air belt visual root");
            beltVisualRoot.transform.SetParent(tutorialAirflowRoot, false);
            beltVisualRoot.transform.position = belt.transform.position;

            var arrow = CreateBox("tutorial air arrow glow", beltVisualRoot.transform.position + new Vector3(0f, 0.21f, -0.70f), new Vector3(0.34f, 0.018f, 0.055f), level2PortalCoreMaterial, beltVisualRoot.transform, Quaternion.identity, false);
            tutorialAirflowArrow = arrow.transform;
            airBeltArrowRenderers[0] = arrow.GetComponent<Renderer>();
            airBeltArrowTransforms[0] = arrow.transform;

            for (var i = 0; i < airBeltMistQuads.Length; i++)
            {
                var xOffset = Mathf.Lerp(-1.68f, 1.68f, i / (float)(airBeltMistQuads.Length - 1));
                var zOffset = i % 2 == 0 ? -0.28f : 0.28f;
                var mist = CreateAirflowMist($"tutorial air flow mist sheet {i + 1}", beltVisualRoot.transform, new Vector3(xOffset, 0.285f, zOffset), 2.0f, 0.68f);
                airBeltMistQuads[i] = mist.transform;
                airBeltMistRenderers[i] = mist.GetComponent<Renderer>();
            }

            for (var i = 0; i < airBeltStreaks.Length; i++)
            {
                var xOffset = Mathf.Lerp(-2.05f, 2.05f, i / (float)(airBeltStreaks.Length - 1));
                var zOffset = Mathf.Lerp(-0.58f, 0.58f, i / (float)(airBeltStreaks.Length - 1));
                var streak = CreateAirflowRibbon($"tutorial air flow texture ribbon {i + 1}", beltVisualRoot.transform, new Vector3(xOffset, 0.33f, zOffset), 1.42f, 0.30f);
                airBeltStreaks[i] = streak.transform;
                airBeltStreakRenderers[i] = streak.GetComponent<Renderer>();
            }

            airBeltParticles = new[]
            {
                CreateAirflowParticles("tutorial airflow cyan mist", beltVisualRoot.transform),
            };

            if (airBeltRenderers[0] != null)
            {
                airBeltRenderers[0].enabled = false;
            }
            if (airBeltArrowRenderers[0] != null)
            {
                airBeltArrowRenderers[0].sharedMaterial = boxIdle;
            }
            UpdateAirBeltVisuals();
        }

        private void CreateTutorialChamberFrame(float centerX, float width, float depth)
        {
            var frontZ = -depth * 0.5f;
            var rearZ = depth * 0.5f;
            CreateBox("tutorial level2 chamber front trim", new Vector3(centerX, 0.105f, frontZ), new Vector3(width, 0.055f, 0.055f), level2TrimMaterial, tutorialAirflowRoot, Quaternion.identity, false);
            CreateBox("tutorial level2 chamber rear trim", new Vector3(centerX, 0.105f, rearZ), new Vector3(width, 0.055f, 0.055f), level2TrimMaterial, tutorialAirflowRoot, Quaternion.identity, false);
            CreateBox("tutorial level2 chamber left trim", new Vector3(centerX - width * 0.5f, 0.105f, 0f), new Vector3(0.055f, 0.055f, depth), level2TrimMaterial, tutorialAirflowRoot, Quaternion.identity, false);
            CreateBox("tutorial level2 chamber right trim", new Vector3(centerX + width * 0.5f, 0.105f, 0f), new Vector3(0.055f, 0.055f, depth), level2TrimMaterial, tutorialAirflowRoot, Quaternion.identity, false);
        }

        private void CreateTutorialWindFloorHints()
        {
            CreateTutorialWindGrille("tutorial level2 wind intake grille", new Vector3(-0.95f, 0.118f, 0f));
            CreateTutorialWindGrille("tutorial level2 wind output grille", new Vector3(3.38f, 0.118f, 0f));
            CreateTutorialWindChevron(new Vector3(0.12f, 0.142f, 0f), 0.50f);
            CreateTutorialWindChevron(new Vector3(1.25f, 0.142f, 0f), 0.50f);
            CreateTutorialWindChevron(new Vector3(2.38f, 0.142f, 0f), 0.50f);
        }

        private void CreateTutorialWindGrille(string name, Vector3 position)
        {
            CreateBox(name + " base", position, new Vector3(0.52f, 0.018f, 0.84f), darkStone, tutorialAirflowRoot, Quaternion.identity, false);
            for (var i = 0; i < 5; i++)
            {
                var z = Mathf.Lerp(-0.32f, 0.32f, i / 4f);
                CreateBox($"{name} cyan slit {i}", position + new Vector3(0f, 0.022f, z), new Vector3(0.42f, 0.012f, 0.030f), level2PortalCoreMaterial, tutorialAirflowRoot, Quaternion.identity, false);
            }
        }

        private void CreateTutorialWindChevron(Vector3 position, float length)
        {
            CreateBox("tutorial level2 floor wind chevron upper", position + new Vector3(0f, 0f, 0.11f), new Vector3(length, 0.014f, 0.045f), level2PortalCoreMaterial, tutorialAirflowRoot, Quaternion.Euler(0f, 28f, 0f), false);
            CreateBox("tutorial level2 floor wind chevron lower", position + new Vector3(0f, 0f, -0.11f), new Vector3(length, 0.014f, 0.045f), level2PortalCoreMaterial, tutorialAirflowRoot, Quaternion.Euler(0f, -28f, 0f), false);
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

            if (twoHandsPinching && CanPullBridge())
            {
                UpdateTutorialBridgePull(a, b);
            }
            else if (tutorialStage == TutorialStage.BridgePull)
            {
                tutorialBridgeStartDistance = 0f;
            }

            if (CanActivatePalm())
            {
                UpdateTutorialPalmActivation(hand);
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

            if (CanPracticeAirflow())
            {
                UpdateTutorialAirflow(frame);
            }
            else
            {
                tutorialAirflowPreviewDirection = 0;
                UpdateTutorialAirflowVisuals(0);
            }
        }

        private bool CanDragLabObject()
        {
            return tutorialStage == TutorialStage.OneHandDrag;
        }

        private bool CanRotateLabObject()
        {
            return tutorialStage == TutorialStage.TwoHandRotate;
        }

        private bool CanControlMap()
        {
            return tutorialStage == TutorialStage.MapControl;
        }

        private bool CanPullBridge()
        {
            return tutorialStage == TutorialStage.BridgePull;
        }

        private bool CanActivatePalm()
        {
            return tutorialStage == TutorialStage.PalmActivate;
        }

        private bool CanPracticeAirflow()
        {
            return tutorialStage == TutorialStage.AirflowDirection;
        }

        private bool TutorialUsesLabObject()
        {
            return tutorialStage == TutorialStage.OneHandDrag || tutorialStage == TutorialStage.TwoHandRotate;
        }

        private bool IsFinalTutorialStage()
        {
            return tutorialStage == TutorialStage.AirflowDirection || tutorialStage == TutorialStage.Complete;
        }

        private void SelectTutorialStage(TutorialStage stage)
        {
            tutorialStage = stage;
            ResetTutorialStageProgress();
            ApplyTutorialStageVisibility();
        }

        private void ResetTutorialStageProgress()
        {
            tutorialStageSucceeded = tutorialStage == TutorialStage.Complete;
            tutorialObjectMoved = false;
            tutorialObjectRotated = false;
            tutorialBridgePulled = false;
            tutorialPalmActivated = false;
            tutorialMapAdjusted = false;
            tutorialAirflowDirected = false;
            tutorialAirflowPreviewDirection = 0;
            labHeld = false;
            tutorialPalmStart = -1f;
            tutorialBridgeStartDistance = 0f;
            twoHandStartDistance = 0f;
            twoFingerMapStartDistance = 0f;
            if (levelRoot != null)
            {
                levelRoot.localScale = Vector3.one;
                levelRoot.rotation = Quaternion.identity;
            }
            if (labObject != null)
            {
                labObject.transform.position = new Vector3(0f, 0.7f, 0f);
                labObject.transform.rotation = Quaternion.identity;
            }
            if (tutorialBridgeLeft != null)
            {
                tutorialBridgeLeft.localPosition = new Vector3(-0.55f, 0.22f, -1.05f);
            }
            if (tutorialBridgeRight != null)
            {
                tutorialBridgeRight.localPosition = new Vector3(0.55f, 0.22f, -1.05f);
            }
            if (tutorialBridgeLeftRenderer != null)
            {
                tutorialBridgeLeftRenderer.sharedMaterial = boxHover;
            }
            if (tutorialBridgeRightRenderer != null)
            {
                tutorialBridgeRightRenderer.sharedMaterial = boxHover;
            }
            if (tutorialSealRenderer != null)
            {
                tutorialSealRenderer.sharedMaterial = amberGlow;
            }
            SetTutorialAirflowDirection(0);
        }

        private void ApplyTutorialStageVisibility()
        {
            if (labObject != null)
            {
                labObject.SetActive(TutorialUsesLabObject());
            }
            if (tutorialBridgeRoot != null)
            {
                tutorialBridgeRoot.gameObject.SetActive(tutorialStage == TutorialStage.BridgePull);
            }
            if (tutorialSealRoot != null)
            {
                tutorialSealRoot.gameObject.SetActive(tutorialStage == TutorialStage.PalmActivate);
            }
            if (tutorialAirflowRoot != null)
            {
                tutorialAirflowRoot.gameObject.SetActive(tutorialStage == TutorialStage.AirflowDirection);
            }
        }

        private void SetTutorialAirflowDirection(int direction)
        {
            if (airBeltDirection != null && airBeltDirection.Length > 0)
            {
                airBeltDirection[0] = direction;
            }
            if (airBeltTriggers != null && airBeltTriggers.Length > 0 && airBeltTriggers[0] != null)
            {
                airBeltTriggers[0].direction = direction;
                if (direction == 0)
                {
                    airBeltTriggers[0].ResetWindState();
                }
            }
            if (airBeltArrowRenderers != null && airBeltArrowRenderers.Length > 0 && airBeltArrowRenderers[0] != null)
            {
                airBeltArrowRenderers[0].sharedMaterial = direction == 1 ? tealGlow : (direction == -1 ? amberGlow : boxIdle);
            }
            if (airBeltArrowTransforms != null && airBeltArrowTransforms.Length > 0 && airBeltArrowTransforms[0] != null)
            {
                airBeltArrowTransforms[0].localPosition = new Vector3(direction < 0 ? -0.18f : 0.18f, 0.28f, -0.70f);
            }
            UpdateAirBeltVisuals();
        }

        private void UpdateTutorialAirflow(GestureFrame frame)
        {
            var direction = 0;
            if (TryGetAirflowHand(frame, out var airflowHand))
            {
                var dirX = airflowHand.landmarks[8].x - airflowHand.landmarks[0].x;
                direction = Mathf.Abs(dirX) < AirflowDirectionDeadZone ? 0 : (dirX > 0f ? 1 : -1);
                if (direction != 0)
                {
                    tutorialAirflowDirected = true;
                }
            }

            tutorialAirflowPreviewDirection = direction;
            UpdateTutorialAirflowVisuals(direction);
        }

        private void UpdateTutorialAirflowVisuals(int direction)
        {
            if (tutorialAirflowPadRenderer != null)
            {
                tutorialAirflowPadRenderer.sharedMaterial = level2FloorMaterial;
            }

            SetTutorialAirflowDirection(direction);
        }

        private void UpdateTutorialBridgePull(GestureHandFrame a, GestureHandFrame b)
        {
            if (tutorialBridgeLeft == null || tutorialBridgeRight == null)
            {
                return;
            }

            var distance = Vector2.Distance(new Vector2(a.pinchX, a.pinchY), new Vector2(b.pinchX, b.pinchY));
            if (tutorialBridgeStartDistance <= 0f)
            {
                tutorialBridgeStartDistance = Mathf.Max(distance, 0.001f);
            }

            var t = Mathf.Clamp01(1f - distance / tutorialBridgeStartDistance);
            tutorialBridgeLeft.localPosition = Vector3.Lerp(new Vector3(-0.55f, 0.22f, -1.05f), new Vector3(-0.16f, 0.22f, -1.05f), t);
            tutorialBridgeRight.localPosition = Vector3.Lerp(new Vector3(0.55f, 0.22f, -1.05f), new Vector3(0.16f, 0.22f, -1.05f), t);
            if (tutorialBridgeLeftRenderer != null)
            {
                tutorialBridgeLeftRenderer.sharedMaterial = boxHeldMaterial;
            }
            if (tutorialBridgeRightRenderer != null)
            {
                tutorialBridgeRightRenderer.sharedMaterial = boxHeldMaterial;
            }
            if (distance < tutorialBridgeStartDistance * 0.72f)
            {
                tutorialBridgePulled = true;
                tutorialBridgeLeft.localPosition = new Vector3(-0.16f, 0.22f, -1.05f);
                tutorialBridgeRight.localPosition = new Vector3(0.16f, 0.22f, -1.05f);
            }
        }

        private void UpdateTutorialPalmActivation(GestureHandFrame hand)
        {
            if (tutorialSealRenderer == null)
            {
                return;
            }

            var target = ScreenToWorldPlane(hand.indexX, hand.indexY, 0.16f);
            var nearSeal = DistanceXZ(target, new Vector3(0f, 0f, 1.05f)) < 0.82f;
            if (hand.openPalm && hand.score >= 0.45f && nearSeal)
            {
                tutorialPalmStart = tutorialPalmStart < 0f ? Time.time : tutorialPalmStart;
                tutorialSealRenderer.sharedMaterial = boxHeldMaterial;
                if (Time.time - tutorialPalmStart >= 0.7f)
                {
                    tutorialPalmActivated = true;
                }
            }
            else if (!tutorialPalmActivated)
            {
                tutorialPalmStart = -1f;
                tutorialSealRenderer.sharedMaterial = amberGlow;
            }

            if (tutorialPalmActivated)
            {
                tutorialSealRenderer.sharedMaterial = tealGlow;
            }
        }

        private void UpdateTutorialStage(GestureFrame frame)
        {
            switch (tutorialStage)
            {
                case TutorialStage.FindHands:
                    tutorialStageSucceeded |= HasLeftAndRightHands(frame);
                    break;
                case TutorialStage.OneHandDrag:
                    tutorialStageSucceeded |= tutorialObjectMoved;
                    break;
                case TutorialStage.TwoHandRotate:
                    tutorialStageSucceeded |= tutorialObjectRotated;
                    break;
                case TutorialStage.BridgePull:
                    tutorialStageSucceeded |= tutorialBridgePulled;
                    break;
                case TutorialStage.PalmActivate:
                    tutorialStageSucceeded |= tutorialPalmActivated;
                    break;
                case TutorialStage.MapControl:
                    tutorialStageSucceeded |= tutorialMapAdjusted;
                    break;
                case TutorialStage.AirflowDirection:
                    tutorialStageSucceeded |= tutorialAirflowDirected;
                    break;
                case TutorialStage.Complete:
                    labCompleted = true;
                    tutorialStageSucceeded = true;
                    break;
            }
        }

        private bool TutorialStageSucceeded()
        {
            return tutorialStageSucceeded || tutorialStage == TutorialStage.Complete;
        }

        private void AdvanceTutorialStage()
        {
            var nextStage = tutorialStage;
            switch (tutorialStage)
            {
                case TutorialStage.FindHands:
                    nextStage = TutorialStage.OneHandDrag;
                    break;
                case TutorialStage.OneHandDrag:
                    nextStage = TutorialStage.TwoHandRotate;
                    break;
                case TutorialStage.TwoHandRotate:
                    nextStage = TutorialStage.BridgePull;
                    break;
                case TutorialStage.BridgePull:
                    nextStage = TutorialStage.PalmActivate;
                    break;
                case TutorialStage.PalmActivate:
                    nextStage = TutorialStage.MapControl;
                    break;
                case TutorialStage.MapControl:
                    nextStage = TutorialStage.AirflowDirection;
                    break;
                case TutorialStage.AirflowDirection:
                    StartLevel(GameMode.Level1);
                    return;
            }

            if (nextStage != tutorialStage)
            {
                SelectTutorialStage(nextStage);
            }
            else
            {
                tutorialStageSucceeded = tutorialStage == TutorialStage.Complete;
                labHeld = false;
                twoHandStartDistance = 0f;
                twoFingerMapStartDistance = 0f;
                tutorialAirflowPreviewDirection = 0;
            }
            if (tutorialStage == TutorialStage.Complete)
            {
                labCompleted = true;
            }
        }

        private string TutorialTitle()
        {
            return tutorialStage switch
            {
                TutorialStage.FindHands => "1/7 Move your hands freely.",
                TutorialStage.OneHandDrag => "2/7 Pinch an object with one hand and drag it.",
                TutorialStage.TwoHandRotate => "3/7 Pinch both sides and rotate the object.",
                TutorialStage.BridgePull => "4/7 Join a bridge with both hands.",
                TutorialStage.PalmActivate => "5/7 Open your palm over the glowing seal.",
                TutorialStage.MapControl => "6/7 Join index+middle on both hands.",
                TutorialStage.AirflowDirection => "7/7 Point the airflow with one hand.",
                TutorialStage.Complete => "Tutorial complete.",
                _ => "",
            };
        }

        private string TutorialDetail()
        {
            return tutorialStage switch
            {
                TutorialStage.FindHands => "Move both hands freely and watch how the game recognizes them on screen.",
                TutorialStage.OneHandDrag => "Touch thumb and index finger, grab the object, then move it across the practice slab.",
                TutorialStage.TwoHandRotate => "Pinch the object from both sides, then turn your hands like rotating a real block.",
                TutorialStage.BridgePull => "Pinch both bridge halves, then move your hands closer together until they lock.",
                TutorialStage.PalmActivate => "Open one hand and hold it over the glowing seal until it lights up.",
                TutorialStage.MapControl => "On each hand, keep index and middle fingertips close, with ring and pinky folded, then move both hands to adjust the map.",
                TutorialStage.AirflowDirection => "Use one hand: extend your thumb, keep index and middle together, fold ring and pinky, then point left or right.",
                TutorialStage.Complete => "Hold over Next: Level 1 when ready.",
                _ => "",
            };
        }

        private string HandStatusText()
        {
            var frame = receiver != null && receiver.HasFreshFrame ? receiver.Latest : GestureFrame.Neutral;
            var left = HasHandedness(frame, "Left") ? "Left: yes" : "Left: waiting";
            var right = HasHandedness(frame, "Right") ? "Right: yes" : "Right: waiting";
            if (tutorialStage == TutorialStage.AirflowDirection || tutorialStage == TutorialStage.Complete)
            {
                var airflow = tutorialAirflowPreviewDirection > 0 ? "Airflow: RIGHT" : (tutorialAirflowPreviewDirection < 0 ? "Airflow: LEFT" : "Airflow: waiting");
                return $"{left}    {right}    Hands: {frame.handCount}    {airflow}";
            }
            return $"{left}    {right}    Hands: {frame.handCount}";
        }

        private void BuildLevel1()
        {
            levelRoot = new GameObject("Level01 First Path").transform;
            var roadRotation = Quaternion.Euler(0f, 0f, Level1RoadAngleDegrees);
            level1Stage = Level1Stage.ClearBlock;
            bridgeLocked = false;
            rotateGateLocked = false;
            sealActivated = false;
            rotateGateHeld = false;
            level1BridgeStartDistance = 0f;
            sealHoldStart = -1f;
            level1SuccessUntil = -1f;

            CreateBox("trial void shadow plinth", new Vector3(0f, -0.62f, 0f), new Vector3(10.9f, 0.42f, 5.2f), darkStone, levelRoot, Quaternion.identity, false);
            CreateBox("trial carved base", new Vector3(0f, -0.28f, 0f), new Vector3(9.9f, 0.34f, 3.9f), cliffStone, levelRoot, Quaternion.identity, false);
            CreateBox("road segment start", new Vector3(-3.25f, RoadY(-3.25f), 0f), new Vector3(2.45f, 0.28f, 1.55f), paleStone, levelRoot, roadRotation, true);
            CreateBox("road segment middle", new Vector3(0.38f, RoadY(0.38f), 0f), new Vector3(2.65f, 0.28f, 1.55f), paleStone, levelRoot, roadRotation, true);
            CreateBox("road segment finish", new Vector3(2.95f, RoadY(2.95f), 0f), new Vector3(2.45f, 0.28f, 1.55f), paleStone, levelRoot, roadRotation, true);
            CreateBox("left rail start", new Vector3(-3.25f, RoadY(-3.25f) + 0.34f, -0.92f), new Vector3(2.45f, 0.58f, 0.18f), stone, levelRoot, roadRotation, true);
            CreateBox("right rail start", new Vector3(-3.25f, RoadY(-3.25f) + 0.34f, 0.92f), new Vector3(2.45f, 0.58f, 0.18f), stone, levelRoot, roadRotation, true);
            CreateBox("left rail finish", new Vector3(2.95f, RoadY(2.95f) + 0.34f, -0.92f), new Vector3(2.45f, 0.58f, 0.18f), stone, levelRoot, roadRotation, true);
            CreateBox("right rail finish", new Vector3(2.95f, RoadY(2.95f) + 0.34f, 0.92f), new Vector3(2.45f, 0.58f, 0.18f), stone, levelRoot, roadRotation, true);

            obstacleBox = CreateBox("Pinch Movable Obstacle Box", new Vector3(-2.85f, RoadY(-2.85f) + 0.37f, 0f), new Vector3(0.72f, 0.72f, 0.98f), boxIdle, levelRoot, roadRotation, true).AddComponent<Rigidbody>();
            obstacleBox.isKinematic = true;
            obstacleBox.interpolation = RigidbodyInterpolation.Interpolate;
            obstacleBox.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            obstacleRenderer = obstacleBox.GetComponent<Renderer>();
            var slot = CreateBox("block side slot", new Vector3(-2.85f, RoadY(-2.85f) + 0.08f, 1.35f), new Vector3(0.95f, 0.08f, 0.75f), amberGlow, levelRoot, roadRotation, false);
            blockSlotRenderer = slot.GetComponent<Renderer>();

            startGate = CreateGate("start release gate", -3.55f);
            bridgeGate = CreateGate("bridge release gate", -2.05f);
            rotateGateStop = CreateGate("rotating gate stop", 0.9f);
            goalGate = CreateGate("goal seal gate", 2.55f);

            var leftBridge = CreateBox("left sliding bridge half", new Vector3(-1.4f, RoadY(-1.4f) + 0.02f, -0.58f), new Vector3(1.3f, 0.18f, 0.62f), boxHover, levelRoot, roadRotation, true);
            var rightBridge = CreateBox("right sliding bridge half", new Vector3(-1.4f, RoadY(-1.4f) + 0.02f, 0.58f), new Vector3(1.3f, 0.18f, 0.62f), boxHover, levelRoot, roadRotation, true);
            bridgeLeft = leftBridge.transform;
            bridgeRight = rightBridge.transform;
            bridgeLeftRenderer = leftBridge.GetComponent<Renderer>();
            bridgeRightRenderer = rightBridge.GetComponent<Renderer>();

            var rotateGateObject = CreateBox("rotating path gate", new Vector3(0.92f, RoadY(0.92f) + 0.42f, 0f), new Vector3(1.35f, 0.48f, 0.18f), boxHover, levelRoot, roadRotation * Quaternion.Euler(0f, 90f, 0f), true);
            rotateGate = rotateGateObject.transform;
            rotateGateRenderer = rotateGateObject.GetComponent<Renderer>();

            var seal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            seal.name = "final palm seal";
            seal.transform.SetParent(levelRoot, false);
            seal.transform.position = new Vector3(3.35f, RoadY(3.35f) + 0.14f, 1.22f);
            seal.transform.localScale = new Vector3(0.58f, 0.05f, 0.58f);
            sealRenderer = seal.GetComponent<Renderer>();
            sealRenderer.sharedMaterial = amberGlow;
            CreateTorus("final seal ring", seal.transform.position + new Vector3(0f, 0.06f, 0f), 0.53f, 0.04f, tealGlow, levelRoot);

            var ballObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballObject.name = "Golden Physics Ball";
            ballObject.transform.SetParent(levelRoot, false);
            ballObject.transform.position = new Vector3(-4.32f, RoadY(-4.32f) + 0.46f, 0f);
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
            var isPinch = IsPinching(hand);
            var twoHandsPinching = TryGetTwoPinchingHands(out var a, out var b);
            UpdateLevel1Block(target, isPinch);
            UpdateLevel1Bridge(a, b, twoHandsPinching);
            UpdateLevel1RotatingGate(a, b, twoHandsPinching);
            UpdateLevel1Seal(hand);
        }

        private void UpdateLevel1Block(Vector3 target, bool isPinch)
        {
            var close = DistanceXZ(target, obstacleBox.position) < (isPinch ? 1.05f : 0.72f);
            obstacleRenderer.sharedMaterial = boxIdle;
            if (!boxHeld && close)
            {
                obstacleRenderer.sharedMaterial = boxHover;
            }
            if (!boxHeld && isPinch && close)
            {
                boxHeld = true;
                level1BoxGrabOffset = obstacleBox.position - target;
            }
            if (boxHeld && !isPinch)
            {
                boxHeld = false;
            }
            if (boxHeld)
            {
                obstacleRenderer.sharedMaterial = boxHeldMaterial;
                target += level1BoxGrabOffset;
                target.x = Mathf.Clamp(target.x, -3.45f, -2.15f);
                target.z = Mathf.Clamp(target.z, -0.65f, 1.55f);
                target.y = RoadY(target.x) + 0.37f;
                obstacleBox.MovePosition(target);
                obstacleBox.linearVelocity = Vector3.zero;
                obstacleBox.angularVelocity = Vector3.zero;
            }

            if (level1Stage == Level1Stage.ClearBlock && DistanceXZ(obstacleBox.position, new Vector3(-2.85f, 0f, 1.35f)) < 0.45f)
            {
                obstacleRenderer.sharedMaterial = boxHeldMaterial;
                blockSlotRenderer.sharedMaterial = tealGlow;
                OpenGate(startGate);
                AdvanceLevel1(Level1Stage.JoinBridge);
            }
        }

        private void UpdateLevel1Bridge(GestureHandFrame a, GestureHandFrame b, bool twoHandsPinching)
        {
            if (bridgeLocked || level1Stage != Level1Stage.JoinBridge || !twoHandsPinching || bridgeLeft == null || bridgeRight == null)
            {
                if (!bridgeLocked && level1Stage == Level1Stage.JoinBridge)
                {
                    level1BridgeStartDistance = 0f;
                }
                return;
            }

            var distance = Vector2.Distance(new Vector2(a.pinchX, a.pinchY), new Vector2(b.pinchX, b.pinchY));
            if (level1BridgeStartDistance <= 0f)
            {
                level1BridgeStartDistance = Mathf.Max(distance, 0.001f);
            }

            var t = Mathf.Clamp01(1f - distance / level1BridgeStartDistance);
            var y = RoadY(-1.4f) + 0.02f;
            bridgeLeft.position = Vector3.Lerp(new Vector3(-1.4f, y, -0.58f), new Vector3(-1.4f, y, -0.28f), t);
            bridgeRight.position = Vector3.Lerp(new Vector3(-1.4f, y, 0.58f), new Vector3(-1.4f, y, 0.28f), t);
            bridgeLeftRenderer.sharedMaterial = boxHeldMaterial;
            bridgeRightRenderer.sharedMaterial = boxHeldMaterial;
            if (distance < level1BridgeStartDistance * 0.72f)
            {
                bridgeLocked = true;
                bridgeLeft.position = new Vector3(-1.4f, y, -0.28f);
                bridgeRight.position = new Vector3(-1.4f, y, 0.28f);
                bridgeLeftRenderer.sharedMaterial = tealGlow;
                bridgeRightRenderer.sharedMaterial = tealGlow;
                OpenGate(bridgeGate);
                AdvanceLevel1(Level1Stage.RotateGate);
            }
        }

        private void UpdateLevel1RotatingGate(GestureHandFrame a, GestureHandFrame b, bool twoHandsPinching)
        {
            if (rotateGateLocked || level1Stage != Level1Stage.RotateGate || rotateGate == null)
            {
                return;
            }

            if (twoHandsPinching)
            {
                var angle = Mathf.Atan2(b.pinchY - a.pinchY, b.pinchX - a.pinchX);
                if (!rotateGateHeld)
                {
                    rotateGateHeld = true;
                    level1RotateStartAngle = angle;
                }
                var deltaDegrees = Mathf.DeltaAngle(level1RotateStartAngle * Mathf.Rad2Deg, angle * Mathf.Rad2Deg);
                var yaw = Mathf.Clamp(90f - Mathf.Abs(deltaDegrees), 0f, 90f);
                rotateGate.rotation = Quaternion.Euler(0f, 0f, Level1RoadAngleDegrees) * Quaternion.Euler(0f, yaw, 0f);
                rotateGateRenderer.sharedMaterial = boxHeldMaterial;
                if (yaw <= 12f)
                {
                    rotateGateLocked = true;
                    rotateGate.rotation = Quaternion.Euler(0f, 0f, Level1RoadAngleDegrees);
                    rotateGateRenderer.sharedMaterial = tealGlow;
                    DestroyUnityObject(rotateGate.GetComponent<Collider>());
                    OpenGate(rotateGateStop);
                    AdvanceLevel1(Level1Stage.ActivateSeal);
                }
            }
            else
            {
                rotateGateHeld = false;
                if (!rotateGateLocked)
                {
                    rotateGateRenderer.sharedMaterial = boxHover;
                }
            }
        }

        private void UpdateLevel1Seal(GestureHandFrame hand)
        {
            if (sealActivated || level1Stage != Level1Stage.ActivateSeal)
            {
                return;
            }

            var target = ScreenToWorldPlane(hand.indexX, hand.indexY, RoadY(3.35f) + 0.14f);
            var nearSeal = DistanceXZ(target, new Vector3(3.35f, 0f, 1.22f)) < 0.8f;
            if (hand.openPalm && hand.score >= 0.45f && nearSeal)
            {
                sealHoldStart = sealHoldStart < 0f ? Time.time : sealHoldStart;
                if (sealRenderer != null)
                {
                    sealRenderer.sharedMaterial = boxHeldMaterial;
                }
                if (Time.time - sealHoldStart >= 0.85f)
                {
                    sealActivated = true;
                    if (sealRenderer != null)
                    {
                        sealRenderer.sharedMaterial = tealGlow;
                    }
                    OpenGate(goalGate);
                    AdvanceLevel1(Level1Stage.RunToGoal);
                }
            }
            else
            {
                sealHoldStart = -1f;
                if (sealRenderer != null)
                {
                    sealRenderer.sharedMaterial = amberGlow;
                }
            }
        }

        private GameObject CreateGate(string name, float x)
        {
            return CreateBox(name, new Vector3(x, RoadY(x) + 0.66f, 0f), new Vector3(0.16f, 1.05f, 1.85f), amberGlow, levelRoot, Quaternion.Euler(0f, 0f, Level1RoadAngleDegrees), true);
        }

        private void OpenGate(GameObject gate)
        {
            if (gate != null)
            {
                gate.SetActive(false);
            }
        }

        private void AdvanceLevel1(Level1Stage nextStage)
        {
            if (level1Stage == nextStage)
            {
                return;
            }

            level1Stage = nextStage;
            level1SuccessUntil = Time.time + 1.2f;
        }

        private string Level1ObjectiveText()
        {
            return level1Stage switch
            {
                Level1Stage.ClearBlock => "Move the block into the glowing side slot to release the ball.",
                Level1Stage.JoinBridge => "Pinch both bridge halves and pull your hands together to join the bridge.",
                Level1Stage.RotateGate => "Pinch with both hands and rotate the gate until it aligns with the path.",
                Level1Stage.ActivateSeal => "Open your palm over the glowing seal to unlock the final gate.",
                Level1Stage.RunToGoal => "The path is open. Guide the ball safely to the altar.",
                _ => "",
            };
        }

        private Vector3 MapPinchToRoad(float normalizedX, float normalizedY)
        {
            var x = Mathf.Lerp(-4.2f, 4.15f, Mathf.Clamp01(normalizedX));
            var z = Mathf.Lerp(1.55f, -1.55f, Mathf.Clamp01(normalizedY));
            x = Mathf.Clamp(x, -4.2f, 4.15f);
            z = Mathf.Clamp(z, -1.55f, 1.55f);
            return new Vector3(x, RoadY(x) + 0.37f, z);
        }

        private bool IsPinching(GestureHandFrame hand)
        {
            return hand.score >= 0.35f && hand.pinchDistance < pinchThreshold;
        }

        // -------------------- Level2: Portals & Airflow --------------------
        private void BuildLevel2()
        {
            levelRoot = new GameObject("Level02 Portal Airflow").transform;
            Debug.Log("[Level2] BuildLevel2 start");
            level1Stage = Level1Stage.ClearBlock; // ClearBlock=Place key, JoinBridge=Airflow, RotateGate=Finish
            portalAActive = false;
            portalBActive = false;
            level1SuccessUntil = -1f;
            level2Teleporting = false;
            pendingAirDirection = 0;
            pendingAirDirectionStart = -1f;
            level2HintMessage = "";
            ConfigureLevel2CameraAndLights();
            
            // Portal A: ball spawn point (left side)
            // Portal B: after gate (right side, where ball teleports to)
            portalAPosition = new Vector3(-3.5f, 0.15f, 0f);
            portalBPosition = new Vector3(0.5f, 0.15f, 0f);

            BuildLevel2DungeonArt();
            var floorCollider = CreateBox("level2 gameplay floor collider", new Vector3(0.55f, 0.02f, 0f), new Vector3(9.9f, 0.045f, 2.25f), level2FloorMaterial, levelRoot, Quaternion.identity, true);
            var floorRenderer = floorCollider.GetComponent<Renderer>();
            if (floorRenderer != null) floorRenderer.enabled = false;

            // single rune for key placement (activates teleport)
            var rune = CreateBox("rune", new Vector3(-2.0f, 0.08f, 1.22f), new Vector3(0.92f, 0.08f, 0.92f), level2RuneMaterial, levelRoot, Quaternion.identity, true);
            runeLeft = rune.transform;
            var runeColliderRenderer = rune.GetComponent<Renderer>();
            if (runeColliderRenderer != null) runeColliderRenderer.enabled = false;
            runeLeftRenderer = CreateLevel2RuneArt(rune.transform.position, amberGlow);
            runeRight = null;
            runeRightRenderer = null;
            runeLeftArrow = null;
            runeRightArrow = null;

            // draggable geometric key
            var key = new GameObject("Portal Key");
            key.name = "Portal Key";
            key.transform.SetParent(levelRoot, false);
            key.transform.position = new Vector3(-3.85f, 0.32f, 1.12f);
            key.transform.rotation = Quaternion.Euler(0f, 24f, 0f);
            var keyCollider = key.AddComponent<BoxCollider>();
            keyCollider.center = new Vector3(0.18f, 0.06f, 0f);
            keyCollider.size = new Vector3(1.08f, 0.35f, 0.42f);
            CreatePortalKeyVisual(key.transform);
            portalKeyRenderers = key.GetComponentsInChildren<Renderer>();
            portalKeyIdleMaterials = new Material[portalKeyRenderers.Length];
            for (var i = 0; i < portalKeyRenderers.Length; i++)
            {
                portalKeyIdleMaterials[i] = portalKeyRenderers[i].sharedMaterial;
            }
            portalKeyRenderer = portalKeyRenderers.Length > 0 ? portalKeyRenderers[0] : null;
            RestorePortalKeyMaterial();
            portalKeyBody = key.AddComponent<Rigidbody>();
            portalKeyBody.isKinematic = true;
            portalKeyBody.interpolation = RigidbodyInterpolation.Interpolate;
            portalKeyBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            portalKey = key;

            // portals visual markers
            portalARenderer = CreateLevel2Portal("portal A", portalAPosition, tealGlow, out portalAParticles);
            portalBRenderer = CreateLevel2Portal("portal B", portalBPosition, amberGlow, out portalBParticles);

            // single air belt (default direction: RIGHT, pushes ball from portal B toward goal)
            airBelts = new Transform[1];
            airBeltDirection = new int[1];
            airBeltRenderers = new Renderer[1];
            airBeltTriggers = new AirBeltTrigger[1];
            airBeltArrowRenderers = new Renderer[1];
            airBeltArrowTransforms = new Transform[1];
            airBeltStreaks = new Transform[6];
            airBeltStreakRenderers = new Renderer[6];
            airBeltMistQuads = new Transform[4];
            airBeltMistRenderers = new Renderer[4];
            
            var beltX = 1.25f; // Matches the full central wind gallery floor.
            var beltY = 0.18f;
            // Use keepCollider=true to preserve the collider, then set it as trigger
            var belt = CreateBox("air belt trigger", new Vector3(beltX, beltY, 0f), new Vector3(4.95f, 0.36f, 1.65f), level2WindMaterial, levelRoot, Quaternion.identity, true);
            airBelts[0] = belt.transform;
            airBeltRenderers[0] = belt.GetComponent<Renderer>();
            airBeltDirection[0] = 0; // Default: OFF (no wind)

            var col = belt.GetComponent<Collider>();
            col.isTrigger = true;

            var trigger = belt.AddComponent<AirBeltTrigger>();
            trigger.beltIndex = 0;
            trigger.direction = 0; // Default: OFF
            trigger.force = AirBeltForce;
            trigger.maxWindSpeed = 1.95f;
            trigger.rampSeconds = 1.45f;
            airBeltTriggers[0] = trigger;

            var beltVisualRoot = new GameObject("air belt visual root");
            beltVisualRoot.transform.SetParent(levelRoot, false);
            beltVisualRoot.transform.position = belt.transform.position;

            var arrow = CreateBox("air arrow glow", beltVisualRoot.transform.position + new Vector3(0f, 0.21f, -0.70f), new Vector3(0.34f, 0.018f, 0.055f), level2PortalCoreMaterial, beltVisualRoot.transform, Quaternion.identity, false);
            airBeltArrowRenderers[0] = arrow.GetComponent<Renderer>();
            airBeltArrowTransforms[0] = arrow.transform;

            for (var i = 0; i < airBeltMistQuads.Length; i++)
            {
                var xOffset = Mathf.Lerp(-1.68f, 1.68f, i / (float)(airBeltMistQuads.Length - 1));
                var zOffset = i % 2 == 0 ? -0.28f : 0.28f;
                var mist = CreateAirflowMist($"air flow mist sheet {i + 1}", beltVisualRoot.transform, new Vector3(xOffset, 0.285f, zOffset), 2.0f, 0.68f);
                airBeltMistQuads[i] = mist.transform;
                airBeltMistRenderers[i] = mist.GetComponent<Renderer>();
            }

            for (var i = 0; i < airBeltStreaks.Length; i++)
            {
                var xOffset = Mathf.Lerp(-2.05f, 2.05f, i / (float)(airBeltStreaks.Length - 1));
                var zOffset = Mathf.Lerp(-0.58f, 0.58f, i / (float)(airBeltStreaks.Length - 1));
                var streak = CreateAirflowRibbon($"air flow texture ribbon {i + 1}", beltVisualRoot.transform, new Vector3(xOffset, 0.33f, zOffset), 1.42f, 0.30f);
                airBeltStreaks[i] = streak.transform;
                airBeltStreakRenderers[i] = streak.GetComponent<Renderer>();
            }
            airBeltParticles = new[]
            {
                CreateAirflowParticles("airflow cyan mist", beltVisualRoot.transform),
            };

            if (airBeltRenderers[0] != null)
            {
                airBeltRenderers[0].enabled = false;
            }
            if (airBeltArrowRenderers[0] != null)
            {
                airBeltArrowRenderers[0].sharedMaterial = boxIdle;
            }
            UpdateAirBeltVisuals();

            // single gate between portal A and portal B
            startGate = null;
            bridgeGate = CreateLevel2Gate("level2 rune gate", new Vector3(-1.5f, 0.68f, 0f), 2.55f);
            // gate between air belt and goal (opens when airflow direction is set to RIGHT)
            rotateGateStop = CreateLevel2Gate("level2 wind gate", new Vector3(3.75f, 0.68f, 0f), 2.55f);
            goalGate = null;

            // ball spawns at portal A
            var ballObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballObject.name = "Golden Physics Ball";
            ballObject.transform.SetParent(levelRoot, false);
            ballObject.transform.position = portalAPosition + new Vector3(0f, 0.2f, 0f);
            ballObject.transform.localScale = Vector3.one * 0.46f;
            levelBallBaseScale = ballObject.transform.localScale;
            levelBallRenderer = ballObject.GetComponent<Renderer>();
            levelBallRuntimeMaterial = new Material(ballMaterial) { name = "Golden physics ball runtime" };
            ConfigureTransparentMaterial(levelBallRuntimeMaterial);
            levelBallRenderer.sharedMaterial = levelBallRuntimeMaterial;
            var body = ballObject.AddComponent<Rigidbody>();
            body.mass = 1.1f;
            body.linearDamping = 0.02f;
            body.angularDamping = 0.01f;
            levelBall = ballObject.AddComponent<BallController>();
            levelBallBody = body;

            // goal at right side
            var goal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            goal.name = "Goal Trigger";
            goal.transform.SetParent(levelRoot, false);
            goal.transform.position = new Vector3(4.85f, 0.2f, 0f);
            goal.transform.localScale = new Vector3(0.7f, 0.11f, 0.7f);
            goal.GetComponent<Renderer>().sharedMaterial = level2PortalCoreMaterial;
            DestroyUnityObject(goal.GetComponent<Collider>());
            levelBall.Configure(goal.transform);
            CreateLevel2GoalArt(goal.transform.position);
        }

        private void ConfigureLevel2CameraAndLights()
        {
            if (mainCamera != null)
            {
                mainCamera.orthographicSize = 3.9f;
                mainCamera.transform.SetPositionAndRotation(new Vector3(0.45f, 7.1f, -5.8f), Quaternion.Euler(55f, 0f, 0f));
                mainCamera.backgroundColor = new Color(0.010f, 0.012f, 0.015f);
            }

            var accent = new GameObject("Level2 Portal Accent Light");
            accent.transform.SetParent(levelRoot, false);
            accent.transform.position = new Vector3(0.6f, 2.8f, -1.4f);
            var light = accent.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.23f, 0.95f, 1f);
            light.intensity = 1.05f;
            light.range = 5.2f;
        }

        private void BuildLevel2DungeonArt()
        {
            CreateBox("level2 void plinth", new Vector3(0.45f, -0.50f, 0f), new Vector3(11.2f, 0.42f, 5.0f), darkStone, levelRoot, Quaternion.identity, false);

            CreateBox("level2 left portal chamber floor", new Vector3(-3.0f, -0.015f, 0f), new Vector3(2.55f, 0.13f, 2.48f), level2FloorMaterial, levelRoot, Quaternion.identity, false);
            CreateBox("level2 central wind gallery floor", new Vector3(1.25f, -0.015f, 0f), new Vector3(4.95f, 0.13f, 1.72f), level2FloorMaterial, levelRoot, Quaternion.identity, false);
            CreateBox("level2 right altar chamber floor", new Vector3(4.55f, -0.015f, 0f), new Vector3(1.65f, 0.13f, 2.15f), level2FloorMaterial, levelRoot, Quaternion.identity, false);

            CreateCleanChamberFrame(-3.0f, 2.55f, 2.48f);
            CreateCleanChamberFrame(1.25f, 4.95f, 1.72f);
            CreateCleanChamberFrame(4.55f, 1.65f, 2.15f);

            CreateBox("level2 wind channel low glow", new Vector3(1.25f, 0.055f, 0f), new Vector3(4.65f, 0.006f, 0.045f), level2WallMaterial, levelRoot, Quaternion.identity, false);
            CreateLevel2WindFloorHints();
        }

        private void CreateLevel2WindFloorHints()
        {
            CreateLevel2WindGrille("level2 wind intake grille", new Vector3(-0.95f, 0.118f, 0f));
            CreateLevel2WindGrille("level2 wind output grille", new Vector3(3.38f, 0.118f, 0f));
            CreateLevel2WindChevron(new Vector3(0.12f, 0.142f, 0f), 0.50f);
            CreateLevel2WindChevron(new Vector3(1.25f, 0.142f, 0f), 0.50f);
            CreateLevel2WindChevron(new Vector3(2.38f, 0.142f, 0f), 0.50f);
        }

        private void CreateLevel2WindGrille(string name, Vector3 position)
        {
            CreateBox(name + " base", position, new Vector3(0.52f, 0.018f, 0.84f), darkStone, levelRoot, Quaternion.identity, false);
            for (var i = 0; i < 5; i++)
            {
                var z = Mathf.Lerp(-0.32f, 0.32f, i / 4f);
                CreateBox($"{name} cyan slit {i}", position + new Vector3(0f, 0.022f, z), new Vector3(0.42f, 0.012f, 0.030f), level2PortalCoreMaterial, levelRoot, Quaternion.identity, false);
            }
        }

        private void CreateLevel2WindChevron(Vector3 position, float length)
        {
            CreateBox("level2 floor wind chevron upper", position + new Vector3(0f, 0f, 0.11f), new Vector3(length, 0.014f, 0.045f), level2PortalCoreMaterial, levelRoot, Quaternion.Euler(0f, 28f, 0f), false);
            CreateBox("level2 floor wind chevron lower", position + new Vector3(0f, 0f, -0.11f), new Vector3(length, 0.014f, 0.045f), level2PortalCoreMaterial, levelRoot, Quaternion.Euler(0f, -28f, 0f), false);
        }

        private void CreateCleanChamberFrame(float centerX, float width, float depth)
        {
            var frontZ = -depth * 0.5f;
            var rearZ = depth * 0.5f;
            CreateBox($"level2 chamber front trim {centerX:0.0}", new Vector3(centerX, 0.105f, frontZ), new Vector3(width, 0.055f, 0.055f), level2TrimMaterial, levelRoot, Quaternion.identity, false);
            CreateBox($"level2 chamber rear trim {centerX:0.0}", new Vector3(centerX, 0.105f, rearZ), new Vector3(width, 0.055f, 0.055f), level2TrimMaterial, levelRoot, Quaternion.identity, false);
            CreateBox($"level2 chamber left trim {centerX:0.0}", new Vector3(centerX - width * 0.5f, 0.105f, 0f), new Vector3(0.055f, 0.055f, depth), level2TrimMaterial, levelRoot, Quaternion.identity, false);
            CreateBox($"level2 chamber right trim {centerX:0.0}", new Vector3(centerX + width * 0.5f, 0.105f, 0f), new Vector3(0.055f, 0.055f, depth), level2TrimMaterial, levelRoot, Quaternion.identity, false);
        }

        private GameObject InstantiateDungeonModel(string resourceName, Vector3 position, Vector3 scale, Quaternion rotation, Material overrideMaterial)
        {
            var prefab = Resources.Load<GameObject>($"KenneyDungeon/{resourceName}");
            if (prefab == null)
            {
                Debug.LogWarning($"Missing Kenney dungeon model in Resources: {resourceName}");
                return null;
            }

            var instance = Instantiate(prefab, position, rotation, levelRoot);
            instance.name = $"Kenney {resourceName}";
            instance.transform.localScale = scale;
            DestroyImportedColliders(instance.transform);
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = true;
                if (overrideMaterial != null)
                {
                    renderer.sharedMaterial = overrideMaterial;
                }
            }
            return instance;
        }

        private static void DestroyImportedColliders(Transform root)
        {
            foreach (var collider in root.GetComponentsInChildren<Collider>())
            {
                DestroyUnityObject(collider);
            }
        }

        private void CreateLevel2Pillar(Vector3 position)
        {
            var baseObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            baseObject.name = "level2 carved pillar";
            baseObject.transform.SetParent(levelRoot, false);
            baseObject.transform.position = position;
            baseObject.transform.localScale = new Vector3(0.18f, 0.34f, 0.18f);
            baseObject.GetComponent<Renderer>().sharedMaterial = level2WallMaterial;
            DestroyUnityObject(baseObject.GetComponent<Collider>());

            var cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cap.name = "level2 pillar amber cap";
            cap.transform.SetParent(levelRoot, false);
            cap.transform.position = position + new Vector3(0f, 0.43f, 0f);
            cap.transform.localScale = Vector3.one * 0.20f;
            cap.GetComponent<Renderer>().sharedMaterial = amberGlow;
            DestroyUnityObject(cap.GetComponent<Collider>());
        }

        private Renderer CreateLevel2Portal(string name, Vector3 position, Material idleMaterial, out ParticleSystem particles)
        {
            var root = new GameObject(name);
            root.transform.SetParent(levelRoot, false);
            root.transform.position = position;

            var core = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            core.name = name + " core";
            core.transform.SetParent(root.transform, false);
            core.transform.localPosition = Vector3.zero;
            core.transform.localScale = new Vector3(0.54f, 0.035f, 0.54f);
            core.GetComponent<Renderer>().sharedMaterial = idleMaterial;
            DestroyUnityObject(core.GetComponent<Collider>());

            var ring = CreateTorus(name + " energy ring", position + new Vector3(0f, 0.11f, 0f), 0.55f, 0.045f, level2PortalCoreMaterial, root.transform);
            ring.transform.localPosition = new Vector3(0f, 0.11f, 0f);
            ring.transform.localRotation = Quaternion.identity;

            var inner = CreateTorus(name + " inner ripple", position + new Vector3(0f, 0.16f, 0f), 0.34f, 0.026f, idleMaterial, root.transform);
            inner.transform.localPosition = new Vector3(0f, 0.16f, 0f);
            inner.transform.localRotation = Quaternion.identity;

            var swirl = GameObject.CreatePrimitive(PrimitiveType.Quad);
            swirl.name = name + " Kenney particle swirl";
            swirl.transform.SetParent(root.transform, false);
            swirl.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            swirl.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            swirl.transform.localScale = Vector3.one * 0.92f;
            swirl.GetComponent<Renderer>().sharedMaterial = level2PortalTwirlMaterial != null ? level2PortalTwirlMaterial : level2WindMaterial;
            DestroyUnityObject(swirl.GetComponent<Collider>());

            particles = null;
            return core.GetComponent<Renderer>();
        }

        private Renderer CreateLevel2RuneArt(Vector3 position, Material material)
        {
            var root = new GameObject("level2 key socket art");
            root.transform.SetParent(levelRoot, false);
            root.transform.position = position;

            var baseBlock = CreateBox("level2 key socket stone block", position + new Vector3(0f, 0.065f, 0f), new Vector3(0.92f, 0.18f, 0.66f), level2WallMaterial, root.transform, Quaternion.identity, false);
            baseBlock.transform.localPosition = new Vector3(0f, 0.065f, 0f);

            var topPlate = CreateBox("level2 key socket brass plate", position + new Vector3(0f, 0.168f, 0f), new Vector3(0.82f, 0.026f, 0.48f), level2TrimMaterial, root.transform, Quaternion.identity, false);
            topPlate.transform.localPosition = new Vector3(0f, 0.168f, 0f);

            var slotRotation = Quaternion.Euler(0f, 24f, 0f);
            var slot = CreateBox("level2 key socket dark slot", position + new Vector3(0.12f, 0.188f, 0f), new Vector3(0.76f, 0.024f, 0.105f), darkStone, root.transform, slotRotation, false);
            slot.transform.localPosition = new Vector3(0.12f, 0.188f, 0f);

            var leftRail = CreateBox("level2 key socket left brass rail", position + new Vector3(0.12f, 0.215f, 0.095f), new Vector3(0.72f, 0.045f, 0.035f), level2TrimMaterial, root.transform, slotRotation, false);
            leftRail.transform.localPosition = new Vector3(0.12f, 0.215f, 0.095f);
            var rightRail = CreateBox("level2 key socket right brass rail", position + new Vector3(0.12f, 0.215f, -0.095f), new Vector3(0.72f, 0.045f, 0.035f), level2TrimMaterial, root.transform, slotRotation, false);
            rightRail.transform.localPosition = new Vector3(0.12f, 0.215f, -0.095f);

            var lockCore = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            lockCore.name = "level2 key socket glow core";
            lockCore.transform.SetParent(root.transform, false);
            lockCore.transform.localPosition = new Vector3(-0.26f, 0.225f, 0f);
            lockCore.transform.localScale = new Vector3(0.16f, 0.018f, 0.16f);
            var renderer = lockCore.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            DestroyUnityObject(lockCore.GetComponent<Collider>());
            return renderer;
        }

        private ParticleSystem CreatePortalParticles(string name, Transform parent, Color color)
        {
            var particleObject = new GameObject(name);
            particleObject.transform.SetParent(parent, false);
            particleObject.transform.localPosition = new Vector3(0f, 0.16f, 0f);
            var ps = particleObject.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.42f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.12f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(color.r, color.g, color.b, 0.72f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 42f;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.52f;
            var renderer = particleObject.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = level2WindMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            return ps;
        }

        private GameObject CreateAirflowRibbon(string name, Transform parent, Vector3 localPosition, float length, float width)
        {
            var ribbon = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ribbon.name = name;
            ribbon.transform.SetParent(parent, false);
            ribbon.transform.localPosition = localPosition;
            ribbon.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ribbon.transform.localScale = new Vector3(length, width, 1f);
            var renderer = ribbon.GetComponent<Renderer>();
            renderer.sharedMaterial = level2WindRibbonMaterial != null ? level2WindRibbonMaterial : level2WindMaterial;
            DestroyUnityObject(ribbon.GetComponent<Collider>());
            return ribbon;
        }

        private GameObject CreateAirflowMist(string name, Transform parent, Vector3 localPosition, float length, float width)
        {
            var mist = GameObject.CreatePrimitive(PrimitiveType.Quad);
            mist.name = name;
            mist.transform.SetParent(parent, false);
            mist.transform.localPosition = localPosition;
            mist.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            mist.transform.localScale = new Vector3(length, width, 1f);
            mist.GetComponent<Renderer>().sharedMaterial = level2WindMistMaterial != null ? level2WindMistMaterial : level2WindMaterial;
            DestroyUnityObject(mist.GetComponent<Collider>());
            return mist;
        }

        private ParticleSystem CreateAirflowParticles(string name, Transform parent)
        {
            var particleObject = new GameObject(name);
            particleObject.transform.SetParent(parent, false);
            particleObject.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            var ps = particleObject.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.85f, 1.35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.16f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.14f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.42f, 0.96f, 1f, 0.52f));
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = ps.emission;
            emission.rateOverTime = 16f;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(2.0f, 0.12f, 1.25f);
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(0.2f);
            var renderer = particleObject.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = level2WindMistMaterial != null ? level2WindMistMaterial : level2WindMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            return ps;
        }

        private GameObject CreateLevel2Gate(string name, Vector3 position, float width)
        {
            var gateRoot = new GameObject(name);
            gateRoot.transform.SetParent(levelRoot, false);
            gateRoot.transform.position = position;
            var collider = gateRoot.AddComponent<BoxCollider>();
            collider.center = Vector3.zero;
            collider.size = new Vector3(0.18f, 1.18f, width);

            for (var i = 0; i < 7; i++)
            {
                var z = Mathf.Lerp(-width * 0.40f, width * 0.40f, i / 6f);
                CreateBox($"{name} metal bar {i}", position + new Vector3(0f, 0.02f, z), new Vector3(0.10f, 0.86f, 0.045f), level2WallMaterial, gateRoot.transform, Quaternion.identity, false);
            }
            CreateBox(name + " cyan lock core", position + new Vector3(0f, 0.08f, 0f), new Vector3(0.13f, 0.22f, 0.22f), level2PortalCoreMaterial, gateRoot.transform, Quaternion.identity, false);
            CreateBox(name + " glow lintel", position + new Vector3(0f, 0.36f, 0f), new Vector3(0.16f, 0.08f, width), level2TrimMaterial, gateRoot.transform, Quaternion.identity, false);
            CreateBox(name + " lower rail", position + new Vector3(0f, -0.28f, 0f), new Vector3(0.16f, 0.08f, width), level2TrimMaterial, gateRoot.transform, Quaternion.identity, false);
            return gateRoot;
        }

        private void CreateLevel2GoalArt(Vector3 position)
        {
            InstantiateDungeonModel("template-detail", position + new Vector3(0f, -0.13f, 0f), new Vector3(0.88f, 0.3f, 0.88f), Quaternion.identity, level2TrimMaterial);
            CreateTorus("level2 goal outer halo", position + new Vector3(0f, 0.13f, 0f), 0.66f, 0.055f, level2PortalCoreMaterial, levelRoot);
            CreateTorus("level2 goal inner halo", position + new Vector3(0f, 0.23f, 0f), 0.40f, 0.030f, tealGlow, levelRoot);
        }

        private void UpdateLevel2(GestureHandFrame hand)
        {
            if (portalKey == null || levelBall == null)
            {
                return;
            }

            var frame = receiver != null && receiver.HasFreshFrame ? receiver.Latest : GestureFrame.Neutral;
            var target = MapPinchToRoad(hand.pinchX, hand.pinchY);
            var isPinch = IsPinching(hand);

            // key dragging + magnet snap + UI state
            var grabRadius = isPinch ? PortalKeyGrabPinchRadius : PortalKeyGrabIdleRadius;
            var close = DistanceXZ(target, portalKey.transform.position) < grabRadius;
            RestorePortalKeyMaterial();

            // update UI debug flags
            lastKeyInRange = close;
            lastPinchState = isPinch;
            if (!level2Teleporting && level1Stage == Level1Stage.ClearBlock && !portalAActive)
            {
                level2HintMessage = "";
            }

            // hover dwell hint
            if (!keyHeld && close && !isPinch)
            {
                if (keyHoverStart < 0f) keyHoverStart = Time.time;
                if (Time.time - keyHoverStart >= KeyDwellSeconds)
                {
                    SetPortalKeyMaterial(boxHover);
                    level2HintMessage = "Pinch to grab the key";
                }
            }
            else
            {
                keyHoverStart = -1f;
            }

            // magnetic snap: if player pinches near the key, snap it to the pinch point and grab
            var magnetRadius = 2.0f;
            if (!keyHeld && isPinch && (close || DistanceXZ(target, portalKey.transform.position) < magnetRadius))
            {
                keyHeld = true;
                var snapPos = target;
                snapPos.y = 0.32f;
                portalKeyBody.MovePosition(snapPos);
                keyGrabOffset = portalKey.transform.position - target;
                portalKeyBody.isKinematic = true;
                Debug.Log("[Level2] Key grabbed by player.");
            }

            if (keyHeld && !isPinch)
            {
                keyHeld = false;
                TryActivateLevel2RuneFromKeyPosition();
            }
            if (keyHeld)
            {
                SetPortalKeyMaterial(boxHeldMaterial);
                var pos = target + keyGrabOffset;
                pos.y = 0.32f;
                portalKeyBody.MovePosition(pos);
                level2HintMessage = "Place the key onto the rune to activate teleport.";
            }

            if (!keyHeld && !isPinch)
            {
                TryActivateLevel2RuneFromKeyPosition();
            }

            // airflow gesture: change belt direction only after the portal transfer unlocks the wind gallery.
            var airflowUnlocked = level1Stage == Level1Stage.JoinBridge || level1Stage == Level1Stage.RunToGoal;
            if (airflowUnlocked && TryGetAirflowHand(frame, out var ghand))
            {
                var dirX = ghand.landmarks != null && ghand.landmarks.Length > 8 ? ghand.landmarks[8].x - ghand.landmarks[0].x : ghand.indexX - ghand.pinchX;
                var candidateDir = Mathf.Abs(dirX) < AirflowDirectionDeadZone ? 0 : (dirX > 0f ? 1 : -1);
                if (candidateDir != 0)
                {
                    if (pendingAirDirection != candidateDir)
                    {
                        pendingAirDirection = candidateDir;
                        pendingAirDirectionStart = Time.time;
                    }
                    else if (Time.time - pendingAirDirectionStart >= AirflowDirectionHoldSeconds)
                    {
                        SetAirBeltDirection(candidateDir);
                    }
                }
            }
            else
            {
                pendingAirDirection = 0;
                pendingAirDirectionStart = -1f;
            }

        }

        private void UpdateLevel2Autonomous()
        {
            if (portalKey == null || levelBall == null)
            {
                return;
            }

            UpdateAirBeltVisuals();
            if (level2Teleporting)
            {
                UpdateLevel2Teleport();
            }

            if (level1Stage == Level1Stage.ClearBlock && portalAActive && !level2Teleporting)
            {
                if (Time.time - level2LastTeleport > 0.5f)
                {
                    BeginLevel2Teleport();
                }
            }

            if (level1Stage == Level1Stage.JoinBridge || level1Stage == Level1Stage.RunToGoal)
            {
                var direction = airBeltDirection != null && airBeltDirection.Length > 0 ? airBeltDirection[0] : 0;
                if (direction == 1)
                {
                    level2HintMessage = "Airflow: RIGHT. The wind will gradually carry the ball to the altar.";
                    if (rotateGateStop != null)
                    {
                        OpenGate(rotateGateStop);
                        AdvanceLevel1(Level1Stage.RunToGoal);
                    }
                }
                else if (direction == -1)
                {
                    level2HintMessage = "Airflow: LEFT. Turn your gesture the other way to reach the goal.";
                }
            }
        }

        private void ReleaseLevel2KeyIfTrackingLost()
        {
            if (!keyHeld)
            {
                return;
            }

            keyHeld = false;
            RestorePortalKeyMaterial();
            TryActivateLevel2RuneFromKeyPosition();
            keyHoverStart = -1f;
            lastPinchState = false;
            lastKeyInRange = false;
        }

        private bool TryActivateLevel2RuneFromKeyPosition()
        {
            if (portalAActive || portalKey == null || runeLeft == null)
            {
                return false;
            }

            if (DistanceXZ(portalKey.transform.position, runeLeft.position) >= 0.65f)
            {
                return false;
            }

            portalAActive = true;
            Debug.Log("[Level2] Rune activated - teleport enabled");
            LockPortalKeyIntoSocket();
            if (runeLeftRenderer != null) runeLeftRenderer.sharedMaterial = tealGlow;
            if (portalARenderer != null) portalARenderer.sharedMaterial = tealGlow;
            if (portalBRenderer != null) portalBRenderer.sharedMaterial = tealGlow;
            level2HintMessage = "Teleport activated. Watch the ball cross the gate.";
            return true;
        }

        private void LockPortalKeyIntoSocket()
        {
            if (portalKey == null || runeLeft == null)
            {
                return;
            }

            keyHeld = false;
            keyGrabOffset = Vector3.zero;
            var lockedPosition = runeLeft.position + new Vector3(0.12f, 0.265f, 0f);
            var lockedRotation = Quaternion.Euler(0f, 24f, 0f);
            if (portalKeyBody != null)
            {
                portalKeyBody.isKinematic = true;
                portalKeyBody.linearVelocity = Vector3.zero;
                portalKeyBody.angularVelocity = Vector3.zero;
                portalKeyBody.MovePosition(lockedPosition);
                portalKeyBody.MoveRotation(lockedRotation);
            }
            portalKey.transform.SetPositionAndRotation(lockedPosition, lockedRotation);
            RestorePortalKeyMaterial();
        }

        private void CreatePortalKeyVisual(Transform keyTransform)
        {
            var importedKey = Resources.Load<GameObject>("OpenGameArt/LowPolyKey/key");
            if (importedKey != null)
            {
                var model = Instantiate(importedKey, keyTransform);
                model.name = "OpenGameArt low-poly key model";
                model.transform.localPosition = new Vector3(0.18f, 0.08f, 0f);
                model.transform.localRotation = Quaternion.Euler(0f, 90f, 90f);
                model.transform.localScale = Vector3.one;
                DestroyImportedColliders(model.transform);
                FitImportedVisual(model.transform, 0.92f);

                foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                {
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                    renderer.sharedMaterial = brass;
                }

                var gem = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                gem.name = "portal key inset gem";
                gem.transform.SetParent(keyTransform, false);
                gem.transform.localPosition = new Vector3(-0.30f, 0.16f, 0f);
                gem.transform.localScale = Vector3.one * 0.12f;
                gem.GetComponent<Renderer>().sharedMaterial = level2PortalCoreMaterial;
                DestroyUnityObject(gem.GetComponent<Collider>());
                return;
            }

            var ring = CreateTorus("portal key round bow", keyTransform.position, 0.24f, 0.035f, brass, keyTransform);
            ring.transform.localPosition = new Vector3(-0.30f, 0.08f, 0f);
            ring.transform.localRotation = Quaternion.identity;

            CreateKeyPrimitive("portal key gem", PrimitiveType.Sphere, keyTransform, new Vector3(-0.30f, 0.08f, 0f), Vector3.one * 0.18f, Quaternion.identity, level2PortalCoreMaterial);
            CreateKeyPrimitive("portal key rounded shaft", PrimitiveType.Cylinder, keyTransform, new Vector3(0.12f, 0.08f, 0f), new Vector3(0.055f, 0.42f, 0.055f), Quaternion.Euler(0f, 0f, 90f), brass);
            CreateKeyPrimitive("portal key upper ward", PrimitiveType.Cylinder, keyTransform, new Vector3(0.48f, 0.08f, 0.13f), new Vector3(0.050f, 0.14f, 0.050f), Quaternion.Euler(90f, 0f, 0f), level2TrimMaterial);
            CreateKeyPrimitive("portal key lower ward", PrimitiveType.Cylinder, keyTransform, new Vector3(0.62f, 0.08f, -0.10f), new Vector3(0.050f, 0.13f, 0.050f), Quaternion.Euler(90f, 0f, 0f), level2TrimMaterial);
            CreateKeyPrimitive("portal key luminous bit", PrimitiveType.Sphere, keyTransform, new Vector3(0.76f, 0.08f, 0f), new Vector3(0.15f, 0.11f, 0.11f), Quaternion.identity, level2PortalCoreMaterial);
        }

        private static void FitImportedVisual(Transform root, float targetLargestAxis)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return;
            }

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            var largestAxis = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (largestAxis <= 0.0001f)
            {
                return;
            }

            root.localScale *= targetLargestAxis / largestAxis;
        }

        private void CreateKeyPrimitive(string name, PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 localScale, Quaternion localRotation, Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = localRotation;
            part.transform.localScale = localScale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            DestroyUnityObject(part.GetComponent<Collider>());
        }

        private void SetPortalKeyMaterial(Material material)
        {
            if (portalKeyRenderers == null)
            {
                if (portalKeyRenderer != null) portalKeyRenderer.sharedMaterial = material;
                return;
            }

            foreach (var renderer in portalKeyRenderers)
            {
                if (renderer != null) renderer.sharedMaterial = material;
            }
        }

        private void RestorePortalKeyMaterial()
        {
            if (portalKeyRenderers == null || portalKeyIdleMaterials == null)
            {
                if (portalKeyRenderer != null) portalKeyRenderer.sharedMaterial = brass;
                return;
            }

            for (var i = 0; i < portalKeyRenderers.Length; i++)
            {
                if (portalKeyRenderers[i] != null && i < portalKeyIdleMaterials.Length)
                {
                    portalKeyRenderers[i].sharedMaterial = portalKeyIdleMaterials[i];
                }
            }
        }

        private void BeginLevel2Teleport()
        {
            if (levelBall == null)
            {
                return;
            }

            level2Teleporting = true;
            level2TeleportStart = Time.time;
            level2TeleportStartPosition = portalAPosition + new Vector3(0f, 0.2f, 0f);
            level2TeleportEndPosition = portalBPosition + new Vector3(0f, 0.2f, 0f);
            level2LastTeleport = Time.time;
            level2HintMessage = "Portal transfer in progress...";

            levelBall.gameObject.SetActive(true);
            levelBall.transform.localScale = levelBallBaseScale;
            if (levelBallBody != null)
            {
                levelBallBody.linearVelocity = Vector3.zero;
                levelBallBody.angularVelocity = Vector3.zero;
                levelBallBody.isKinematic = true;
            }
            SetBallAlpha(1f);
        }

        private void UpdateLevel2Teleport()
        {
            if (levelBall == null)
            {
                level2Teleporting = false;
                return;
            }

            const float duration = 1.55f;
            var progress = Mathf.Clamp01((Time.time - level2TeleportStart) / duration);
            var rise = new Vector3(0f, 0.78f, 0f);

            if (progress < 0.38f)
            {
                var t = Mathf.SmoothStep(0f, 1f, progress / 0.38f);
                levelBall.transform.position = Vector3.Lerp(level2TeleportStartPosition, level2TeleportStartPosition + rise, t);
                levelBall.transform.localScale = levelBallBaseScale * Mathf.Lerp(1f, 0.58f, t);
                SetBallAlpha(1f - t);
            }
            else if (progress < 0.55f)
            {
                levelBall.transform.position = level2TeleportEndPosition + rise;
                levelBall.transform.localScale = levelBallBaseScale * 0.58f;
                SetBallAlpha(0f);
            }
            else if (progress < 1f)
            {
                var t = Mathf.SmoothStep(0f, 1f, (progress - 0.55f) / 0.45f);
                levelBall.transform.position = Vector3.Lerp(level2TeleportEndPosition + rise, level2TeleportEndPosition, t);
                levelBall.transform.localScale = levelBallBaseScale * Mathf.Lerp(0.58f, 1f, t);
                SetBallAlpha(t);
            }
            else
            {
                levelBall.transform.position = level2TeleportEndPosition;
                levelBall.transform.localScale = levelBallBaseScale;
                SetBallAlpha(1f);
                if (levelBallBody != null)
                {
                    levelBallBody.isKinematic = false;
                    levelBallBody.linearVelocity = Vector3.zero;
                    levelBallBody.angularVelocity = Vector3.zero;
                    levelBallBody.WakeUp();
                }
                OpenGate(bridgeGate);
                AdvanceLevel1(Level1Stage.JoinBridge);
                level2Teleporting = false;
                level2HintMessage = "Use the airflow gesture to set wind direction.";
            }
        }

        private void SetBallAlpha(float alpha)
        {
            if (levelBallRuntimeMaterial == null || levelBallRenderer == null)
            {
                return;
            }

            var color = levelBallRuntimeMaterial.color;
            color.a = Mathf.Clamp01(alpha);
            levelBallRuntimeMaterial.color = color;
            levelBallRenderer.sharedMaterial = levelBallRuntimeMaterial;
        }

        private static void ConfigureTransparentMaterial(Material material)
        {
            material.SetFloat("_Mode", 3f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        private void SetAirBeltDirection(int direction)
        {
            if (airBeltDirection == null || airBeltDirection.Length == 0)
            {
                return;
            }

            airBeltDirection[0] = direction;
            if (airBeltTriggers != null && airBeltTriggers.Length > 0 && airBeltTriggers[0] != null)
            {
                airBeltTriggers[0].direction = direction;
            }
            if (airBeltRenderers != null && airBeltRenderers.Length > 0 && airBeltRenderers[0] != null)
            {
                airBeltRenderers[0].sharedMaterial = direction == 1 ? tealGlow : (direction == -1 ? amberGlow : boxIdle);
            }
            if (airBeltArrowRenderers != null && airBeltArrowRenderers.Length > 0 && airBeltArrowRenderers[0] != null)
            {
                airBeltArrowRenderers[0].sharedMaterial = direction == 1 ? tealGlow : (direction == -1 ? amberGlow : boxIdle);
            }
            if (airBeltArrowTransforms != null && airBeltArrowTransforms.Length > 0 && airBeltArrowTransforms[0] != null)
            {
                airBeltArrowTransforms[0].localPosition = new Vector3(direction < 0 ? -0.18f : 0.18f, 0.28f, -0.70f);
            }
            UpdateAirBeltVisuals();
        }

        private void UpdateAirBeltVisuals()
        {
            if (airBelts == null || airBelts.Length == 0 || airBelts[0] == null)
            {
                return;
            }

            var direction = airBeltDirection != null && airBeltDirection.Length > 0 ? airBeltDirection[0] : 0;
            var active = direction != 0;
            var material = direction == -1 ? amberGlow : (direction == 1 ? tealGlow : boxIdle);
            var windColor = direction == -1 ? new Color(1f, 0.73f, 0.22f, active ? 0.82f : 0.20f) : new Color(0.38f, 0.95f, 1f, active ? 0.82f : 0.20f);
            TintParticleMaterial(level2WindRibbonMaterial, windColor);
            TintParticleMaterial(level2WindMistMaterial, new Color(windColor.r, windColor.g, windColor.b, active ? 0.42f : 0.12f));
            if (airBeltRenderers != null && airBeltRenderers.Length > 0 && airBeltRenderers[0] != null)
            {
                airBeltRenderers[0].enabled = false;
                airBeltRenderers[0].sharedMaterial = material;
            }
            if (airBeltArrowRenderers != null && airBeltArrowRenderers.Length > 0 && airBeltArrowRenderers[0] != null)
            {
                airBeltArrowRenderers[0].enabled = active;
                airBeltArrowRenderers[0].sharedMaterial = material;
            }

            if (airBeltStreaks == null)
            {
                UpdateAirflowParticleVisuals(direction, active, material.color);
                return;
            }

            if (airBeltMistQuads != null)
            {
                for (var i = 0; i < airBeltMistQuads.Length; i++)
                {
                    var mist = airBeltMistQuads[i];
                    if (mist == null)
                    {
                        continue;
                    }

                    var baseZ = i % 2 == 0 ? -0.28f : 0.28f;
                    var cycle = Mathf.Repeat(Time.time * (active ? 0.30f : 0.08f) + i * 0.19f, 1f);
                    var x = active ? Mathf.Lerp(-1.92f, 1.92f, direction > 0 ? cycle : 1f - cycle) : Mathf.Lerp(-1.68f, 1.68f, i / (float)Mathf.Max(airBeltMistQuads.Length - 1, 1));
                    mist.localPosition = new Vector3(x, 0.285f, baseZ);
                    mist.localScale = new Vector3(active ? 2.0f : 1.32f, active ? 0.74f : 0.44f, 1f);
                    if (airBeltMistRenderers != null && i < airBeltMistRenderers.Length && airBeltMistRenderers[i] != null)
                    {
                        airBeltMistRenderers[i].enabled = active;
                        airBeltMistRenderers[i].sharedMaterial = level2WindMistMaterial != null ? level2WindMistMaterial : level2WindMaterial;
                    }
                }
            }

            for (var i = 0; i < airBeltStreaks.Length; i++)
            {
                var streak = airBeltStreaks[i];
                if (streak == null)
                {
                    continue;
                }

                var baseZ = i % 2 == 0 ? -0.42f : 0.42f;
                var cycle = Mathf.Repeat(Time.time * (active ? 0.72f : 0.18f) + i * 0.23f, 1f);
                var x = active ? Mathf.Lerp(-2.18f, 2.18f, direction > 0 ? cycle : 1f - cycle) : Mathf.Lerp(-2.02f, 2.02f, i / (float)Mathf.Max(airBeltStreaks.Length - 1, 1));
                streak.localPosition = new Vector3(x, 0.31f, baseZ);
                streak.localScale = new Vector3(active ? 1.42f : 0.86f, active ? 0.30f : 0.18f, 1f);
                if (airBeltStreakRenderers != null && i < airBeltStreakRenderers.Length && airBeltStreakRenderers[i] != null)
                {
                    airBeltStreakRenderers[i].enabled = active;
                    airBeltStreakRenderers[i].sharedMaterial = level2WindRibbonMaterial != null ? level2WindRibbonMaterial : level2WindMaterial;
                }
            }
            UpdateAirflowParticleVisuals(direction, active, material.color);
        }

        private static void TintParticleMaterial(Material material, Color color)
        {
            if (material == null)
            {
                return;
            }

            material.color = color;
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_TintColor")) material.SetColor("_TintColor", color);
        }

        private void UpdateAirflowParticleVisuals(int direction, bool active, Color color)
        {
            if (airBeltParticles == null)
            {
                return;
            }

            foreach (var particleSystem in airBeltParticles)
            {
                if (particleSystem == null)
                {
                    continue;
                }

                var main = particleSystem.main;
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(color.r, color.g, color.b, active ? 0.68f : 0.24f));
                main.startSpeed = new ParticleSystem.MinMaxCurve(active ? 0.20f : 0.05f, active ? 0.48f : 0.14f);
                var emission = particleSystem.emission;
                emission.rateOverTime = active ? 52f : 12f;
                var velocity = particleSystem.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.Local;
                velocity.x = new ParticleSystem.MinMaxCurve(active ? direction * 1.7f : 0.18f);
                if (!particleSystem.isPlaying)
                {
                    particleSystem.Play();
                }
            }
        }

        private static bool IsAirflowGesture(GestureHandFrame hand)
        {
            if (hand.landmarks == null || hand.landmarks.Length < 21)
            {
                return false;
            }

            if (hand.openPalm || hand.palmSpan <= 0f || !hand.thumbExtended || !IndexMiddleTogether(hand))
            {
                return false;
            }

            var ringCurled = FingerCurled(hand, 16, 14, 13);
            var pinkyCurled = FingerCurled(hand, 20, 18, 17);
            return ringCurled && pinkyCurled && !hand.ringExtended && !hand.pinkyExtended;
        }

        private static bool FingerCurled(GestureHandFrame hand, int tipIndex, int pipIndex, int mcpIndex)
        {
            var wrist = hand.landmarks[0];
            var tip = hand.landmarks[tipIndex];
            var pip = hand.landmarks[pipIndex];
            var mcp = hand.landmarks[mcpIndex];
            var span = Mathf.Max(hand.palmSpan, 0.0001f);
            var tipToWrist = LandmarkDistance(tip, wrist) / span;
            var pipToWrist = LandmarkDistance(pip, wrist) / span;
            var mcpToWrist = LandmarkDistance(mcp, wrist) / span;

            var foldedTowardPalm = tipToWrist < pipToWrist * 1.08f || tipToWrist < mcpToWrist * 1.28f;
            var notPointingUp = tip.y > pip.y - span * 0.04f;
            return foldedTowardPalm || notPointingUp;
        }

        private static float LandmarkDistance(GestureLandmark a, GestureLandmark b)
        {
            var dx = a.x - b.x;
            var dy = a.y - b.y;
            var dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private bool TryGetAirflowHand(GestureFrame frame, out GestureHandFrame hand)
        {
            hand = default;
            if (frame.hands == null || frame.hands.Length == 0)
            {
                return false;
            }
            foreach (var h in frame.hands)
            {
                if (h.score >= 0.45f && IsAirflowGesture(h))
                {
                    hand = h; return true;
                }
            }
            return false;
        }

        // -------------------- End Level2 methods --------------------

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
                var bestScore = PrimaryHandScore(hand);
                for (var i = 1; i < frame.hands.Length; i++)
                {
                    var candidateScore = PrimaryHandScore(frame.hands[i]);
                    if (candidateScore > bestScore)
                    {
                        hand = frame.hands[i];
                        bestScore = candidateScore;
                    }
                }
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

        private float PrimaryHandScore(GestureHandFrame hand)
        {
            var score = hand.score;
            if (IsPinching(hand))
            {
                score += 0.45f;
            }
            if (hand.indexExtended)
            {
                score += 0.25f;
            }
            if (!hand.middleExtended && !hand.ringExtended && !hand.pinkyExtended)
            {
                score += 0.10f;
            }
            return score;
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

        private void DrawHoverButton(string key, string label, Rect rect, float dwellSeconds, System.Action action, int fontSize = 0)
        {
            DrawButtonCore(key, label, rect, dwellSeconds, action, false, fontSize);
        }

        private void DrawUtilityButton(string key, string label, Rect rect, float dwellSeconds, System.Action action)
        {
            DrawButtonCore(key, label, rect, dwellSeconds, action, true, 0);
        }

        private void DrawButtonCore(string key, string label, Rect rect, float dwellSeconds, System.Action action, bool allowMouseClick, int fontSize)
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
            var style = fontSize > 0
                ? new GUIStyle(GUI.skin.button)
                {
                    fontSize = fontSize,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                }
                : GUI.skin.button;
            var clicked = allowMouseClick ? GUI.Button(rect, label, style) : false;
            if (!allowMouseClick)
            {
                GUI.Box(rect, label, style);
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
            ResetLevel2RuntimeReferences();
            if (levelRoot != null)
            {
                DestroyUnityObject(levelRoot.gameObject);
            }
            levelRoot = null;
            labObject = null;
            labBody = null;
            labRenderer = null;
            tutorialBridgeLeft = null;
            tutorialBridgeRight = null;
            tutorialBridgeRoot = null;
            tutorialSealRoot = null;
            tutorialAirflowRoot = null;
            tutorialBridgeLeftRenderer = null;
            tutorialBridgeRightRenderer = null;
            tutorialSealRenderer = null;
            tutorialAirflowPadRenderer = null;
            tutorialAirflowArrow = null;
            tutorialStageSucceeded = false;
            tutorialObjectMoved = false;
            tutorialObjectRotated = false;
            tutorialBridgePulled = false;
            tutorialPalmActivated = false;
            tutorialMapAdjusted = false;
            tutorialAirflowDirected = false;
            tutorialAirflowPreviewDirection = 0;
            obstacleBox = null;
            obstacleRenderer = null;
            blockSlotRenderer = null;
            bridgeLeft = null;
            bridgeRight = null;
            bridgeLeftRenderer = null;
            bridgeRightRenderer = null;
            rotateGate = null;
            rotateGateRenderer = null;
            sealRenderer = null;
            levelBall = null;
            labHeld = false;
            boxHeld = false;
            bridgeLocked = false;
            rotateGateLocked = false;
            sealActivated = false;
            rotateGateHeld = false;
            startGate = null;
            bridgeGate = null;
            rotateGateStop = null;
            goalGate = null;
            level1BridgeStartDistance = 0f;
            sealHoldStart = -1f;
            level1SuccessUntil = -1f;
            level1BoxGrabOffset = Vector3.zero;
            twoHandStartDistance = 0f;
        }

        private void ResetLevel2RuntimeReferences()
        {
            if (airBeltTriggers != null)
            {
                foreach (var trigger in airBeltTriggers)
                {
                    if (trigger != null)
                    {
                        trigger.ResetWindState();
                        trigger.direction = 0;
                    }
                }
            }

            if (airBeltParticles != null)
            {
                foreach (var particleSystem in airBeltParticles)
                {
                    if (particleSystem != null)
                    {
                        particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    }
                }
            }

            portalKey = null;
            portalKeyBody = null;
            portalKeyRenderer = null;
            portalKeyRenderers = null;
            portalKeyIdleMaterials = null;
            runeLeft = null;
            runeRight = null;
            runeLeftRenderer = null;
            runeRightRenderer = null;
            runeLeftArrow = null;
            runeRightArrow = null;
            portalAActive = false;
            portalBActive = false;
            portalARenderer = null;
            portalBRenderer = null;
            airBelts = null;
            airBeltDirection = null;
            airBeltRenderers = null;
            airBeltTriggers = null;
            airBeltArrowTransforms = null;
            airBeltArrowRenderers = null;
            airBeltStreaks = null;
            airBeltStreakRenderers = null;
            airBeltMistQuads = null;
            airBeltMistRenderers = null;
            airBeltParticles = null;
            portalAParticles = null;
            portalBParticles = null;
            levelBallBody = null;
            levelBallRenderer = null;
            levelBallRuntimeMaterial = null;
            levelBallBaseScale = Vector3.one;
            keyHeld = false;
            keyGrabOffset = Vector3.zero;
            level2LastTeleport = -10f;
            level2Teleporting = false;
            level2TeleportStart = 0f;
            level2TeleportStartPosition = Vector3.zero;
            level2TeleportEndPosition = Vector3.zero;
            pendingAirDirection = 0;
            pendingAirDirectionStart = -1f;
            level2HintMessage = "";
            lastPinchState = false;
            lastKeyInRange = false;
            keyHoverStart = -1f;
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
            level2FloorMaterial = NewMaterial("Level2 carved slate floor", new Color(0.21f, 0.25f, 0.27f), 0.42f, 0.02f);
            level2WallMaterial = NewMaterial("Level2 blue gray dungeon wall", new Color(0.30f, 0.36f, 0.38f), 0.34f, 0.01f);
            level2TrimMaterial = NewMaterial("Level2 worn brass trim", new Color(0.82f, 0.62f, 0.30f), 0.58f, 0.08f);
            level2PortalCoreMaterial = NewMaterial("Level2 cyan portal core", new Color(0.10f, 0.96f, 1f), 0.18f, 0.95f);
            level2WindMaterial = NewParticleMaterial("Level2 translucent wind", new Color(0.38f, 0.95f, 1f, 0.58f));
            level2WindRibbonMaterial = NewParticleMaterial("Kenney wind trace material", new Color(0.38f, 0.95f, 1f, 0.78f), Resources.Load<Texture2D>("KenneyParticles/wind_trace"));
            level2WindMistMaterial = NewParticleMaterial("Kenney wind mist material", new Color(0.38f, 0.95f, 1f, 0.32f), Resources.Load<Texture2D>("KenneyParticles/soft_smoke"));
            level2PortalTwirlMaterial = NewParticleMaterial("Kenney portal twirl material", new Color(0.10f, 0.96f, 1f, 0.68f), Resources.Load<Texture2D>("KenneyParticles/portal_twirl"));
            level2RuneMaterial = NewMaterial("Level2 active rune gold", new Color(1f, 0.73f, 0.20f), 0.36f, 0.65f);
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

        private static Material NewParticleMaterial(string name, Color color)
        {
            return NewParticleMaterial(name, color, null);
        }

        private static Material NewParticleMaterial(string name, Color color, Texture texture)
        {
            var shader = texture != null ? Shader.Find("Legacy Shaders/Particles/Alpha Blended") : Shader.Find("Particles/Standard Unlit");
            shader ??= texture != null ? Shader.Find("Unlit/Transparent") : null;
            shader ??= Shader.Find("Legacy Shaders/Particles/Additive");
            shader ??= Shader.Find("Standard");

            var material = new Material(shader) { name = name, color = color };
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_TintColor")) material.SetColor("_TintColor", color);
            if (texture != null)
            {
                material.mainTexture = texture;
                if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
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

        private GameObject CreateTorus(string name, Vector3 position, float majorRadius, float minorRadius, Material material, Transform parent)
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
            return torus;
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
