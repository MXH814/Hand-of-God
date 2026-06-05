import "./styles.css";
import { Camera, CameraOff, createElement, FlipHorizontal, RotateCcw } from "lucide";
import { HandLandmarker } from "@mediapipe/tasks-vision";
import { CalibrationManager } from "./calibrationManager";
import { GestureAnalyzer } from "./gestureAnalyzer";
import { GestureEventEngine } from "./gestureEventEngine";
import { HandTracker, type HandTrackerOptions } from "./handTracker";
import { InteractionMapper } from "./interactionMapper";
import { SHAPE_LIBRARY } from "./shapeLibrary";
import { ShapeScene } from "./shapeScene";
import type {
  AnalyzedHand,
  GestureEvent,
  InteractionMode,
  MappedHandPoint,
  ShapeType,
  TrackingFrame,
} from "./types";

const TRAY_HOLD_MS = 1000;
const TRAY_HIT_EXPAND_Y = 58;
const INDEX_CROSS_DELETE_HOLD_MS = 850;
const INDEX_CROSS_DELETE_COOLDOWN_MS = 900;
const INDEX_CROSS_MIN_ANGLE = Math.PI / 5;

const app = document.querySelector<HTMLDivElement>("#app");

if (!app) {
  throw new Error("App root not found.");
}

app.innerHTML = `
  <main class="app-shell">
    <header class="top-bar">
      <div>
        <p class="eyebrow">HCI Gesture Game</p>
        <h1>Hand of God</h1>
      </div>
      <div class="status-pill" id="status">Idle</div>
    </header>

    <section class="workspace">
      <div class="ar-stage" id="ar-stage">
        <video id="camera" playsinline muted></video>
        <canvas id="overlay"></canvas>
        <div class="empty-state" id="empty-state">Click Start to enable camera</div>

        <div class="calibration-layer" id="calibration-layer" hidden>
          <div class="calibration-card">
            <span class="metric-label">Calibration</span>
            <strong id="calibration-title">Calibration idle</strong>
            <p id="calibration-detail">Start camera to calibrate hand gestures.</p>
            <div class="progress-track"><span id="calibration-progress"></span></div>
            <div class="calibration-actions">
              <button class="secondary-button" id="skip-calibration" type="button">Skip</button>
              <button class="secondary-button" id="recalibrate-overlay" type="button">Calibrate</button>
            </div>
          </div>
        </div>

        <div class="shape-tray" id="shape-library"></div>
        <div class="cross-delete-progress" id="cross-delete-progress" hidden>
          <span class="cross-delete-label">Delete</span>
          <span class="cross-delete-track"><span id="cross-delete-bar"></span></span>
        </div>
      </div>

      <aside class="debug-panel">
        <div class="toolbar">
          <button class="primary-button" id="toggle-camera" type="button" title="Start camera">
            <span data-icon="camera"></span>
            <span id="toggle-label">Start</span>
          </button>
          <button class="icon-button" id="reset" type="button" title="Reset smoothing">
            <span data-icon="reset"></span>
          </button>
        </div>

        <button class="secondary-button full-width" id="recalibrate-panel" type="button">Calibrate</button>

        <div class="event-panel">
          <span class="metric-label">Gesture Event</span>
          <strong id="event-name">none</strong>
          <p id="event-detail">Waiting for hand input</p>
        </div>

        <div class="metric-grid">
          <div>
            <span class="metric-label">Objects</span>
            <strong id="object-count">0</strong>
          </div>
          <div>
            <span class="metric-label">Mode</span>
            <strong id="interaction-mode">idle</strong>
          </div>
        </div>

        <div class="game-panel">
          <div>
            <span class="metric-label">Game</span>
            <strong id="game-status">Guiding</strong>
            <p id="game-detail">Control temple mechanisms to guide the ball.</p>
          </div>
          <button class="secondary-button full-width" id="reset-game" type="button">Reset Ball</button>
        </div>

        <div class="control-row">
          <label class="switch">
            <input id="mirror" type="checkbox" checked />
            <span>Mirror</span>
          </label>
          <label class="slider-label">
            Smoothing
            <input id="smoothing" min="0" max="0.95" step="0.05" value="0.55" type="range" />
          </label>
        </div>

        <div class="metric-grid">
          <div>
            <span class="metric-label">FPS</span>
            <strong id="fps">0.0</strong>
          </div>
          <div>
            <span class="metric-label">Hands</span>
            <strong id="hand-count">0</strong>
          </div>
        </div>

        <div class="hands-list" id="hands-list"></div>
      </aside>
    </section>
  </main>
`;

const video = getElement<HTMLVideoElement>("camera");
const canvas = getElement<HTMLCanvasElement>("overlay");
const arStage = getElement<HTMLDivElement>("ar-stage");
const emptyState = getElement<HTMLDivElement>("empty-state");
const statusNode = getElement<HTMLDivElement>("status");
const toggleButton = getElement<HTMLButtonElement>("toggle-camera");
const toggleLabel = getElement<HTMLSpanElement>("toggle-label");
const resetButton = getElement<HTMLButtonElement>("reset");
const mirrorInput = getElement<HTMLInputElement>("mirror");
const smoothingInput = getElement<HTMLInputElement>("smoothing");
const fpsNode = getElement<HTMLElement>("fps");
const handCountNode = getElement<HTMLElement>("hand-count");
const handsList = getElement<HTMLDivElement>("hands-list");
const shapeLibrary = getElement<HTMLDivElement>("shape-library");
const eventName = getElement<HTMLElement>("event-name");
const eventDetail = getElement<HTMLParagraphElement>("event-detail");
const crossDeleteProgress = getElement<HTMLDivElement>("cross-delete-progress");
const crossDeleteBar = getElement<HTMLSpanElement>("cross-delete-bar");
const objectCount = getElement<HTMLElement>("object-count");
const interactionModeNode = getElement<HTMLElement>("interaction-mode");
const gameStatus = getElement<HTMLElement>("game-status");
const gameDetail = getElement<HTMLParagraphElement>("game-detail");
const resetGameButton = getElement<HTMLButtonElement>("reset-game");
const calibrationLayer = getElement<HTMLDivElement>("calibration-layer");
const calibrationTitle = getElement<HTMLElement>("calibration-title");
const calibrationDetail = getElement<HTMLParagraphElement>("calibration-detail");
const calibrationProgress = getElement<HTMLSpanElement>("calibration-progress");
const skipCalibrationButton = getElement<HTMLButtonElement>("skip-calibration");
const recalibrateOverlayButton = getElement<HTMLButtonElement>("recalibrate-overlay");
const recalibratePanelButton = getElement<HTMLButtonElement>("recalibrate-panel");

const analyzer = new GestureAnalyzer();
const mapper = new InteractionMapper();
const calibration = new CalibrationManager();
const eventEngine = new GestureEventEngine();
const shapeScene = new ShapeScene({ stageElement: arStage, library: SHAPE_LIBRARY });
const trackerOptions: HandTrackerOptions = {
  numHands: 2,
  minHandDetectionConfidence: 0.55,
  minHandPresenceConfidence: 0.55,
  minTrackingConfidence: 0.55,
};
let tracker: HandTracker | undefined;
let running = false;
let latestHands: AnalyzedHand[] = [];
let latestMappedPoints: MappedHandPoint[] = [];
let activeDraggedShape: ShapeType | undefined;
let interactionMode: InteractionMode = "idle";
let trayHold:
  | {
      shape: ShapeType;
      startedAt: number;
    }
  | undefined;
let armedTrayShape: ShapeType | undefined;
let lastIndexCrossDeleteAt = 0;
let indexCrossHold:
  | {
      objectId: string;
      startedAt: number;
    }
  | undefined;

mountIcons();
renderShapeLibrary();
setMirror(true);
renderHandsList([]);
renderCalibration();
setInteractionMode("idle");
renderGameHud();
window.setInterval(renderGameHud, 250);

toggleButton.addEventListener("click", async () => {
  if (running) {
    stopCamera();
    return;
  }

  await startCamera();
});

resetButton.addEventListener("click", () => {
  analyzer.reset();
  eventEngine.reset();
  setInteractionMode("idle");
  setEventHud("reset", "Smoothing and event state cleared");
});

resetGameButton.addEventListener("click", () => {
  shapeScene.resetGame();
  renderGameHud();
  setEventHud("gameReset", "Ball returned to the start");
});

mirrorInput.addEventListener("change", () => {
  setMirror(mirrorInput.checked);
});

skipCalibrationButton.addEventListener("click", () => {
  calibration.skip();
  renderCalibration();
  setStatus("Skipped calibration");
});

recalibrateOverlayButton.addEventListener("click", startCalibration);
recalibratePanelButton.addEventListener("click", startCalibration);

arStage.addEventListener("click", (event) => {
  if (isInsideShapeTray(event.clientX, event.clientY)) {
    return;
  }

  const selected = shapeScene.selectAtScreenPoint(event.clientX, event.clientY);
  if (selected) {
    refreshObjectCount();
    setEventHud("sceneSelect", selected);
  }
});

window.addEventListener("resize", () => shapeScene.resize());

async function startCamera() {
  toggleButton.disabled = true;
  setStatus("Loading model");

  try {
    tracker = new HandTracker(video, renderFrame, setStatus, setStatus, trackerOptions);
    await tracker.start();
    running = true;
    emptyState.hidden = true;
    toggleLabel.textContent = "Stop";
    replaceIcon(toggleButton, CameraOff);
    startCalibration();
  } catch (error) {
    const message = error instanceof Error ? error.message : "Camera failed";
    stopCamera(message);
  } finally {
    toggleButton.disabled = false;
  }
}

function startCalibration() {
  if (!running) {
    setEventHud("calibration", "Start camera before calibration");
    return;
  }

  analyzer.reset();
  eventEngine.reset();
  shapeScene.setGameActive(false);
  calibration.start();
  setInteractionMode("idle");
  renderCalibration();
  setStatus("Calibrating");
}

function stopCamera(status = "Idle") {
  tracker?.stop();
  tracker = undefined;
  running = false;
  latestHands = [];
  latestMappedPoints = [];
  activeDraggedShape = undefined;
  clearIndexCrossHold();
  clearTrayHold();
  analyzer.reset();
  eventEngine.reset();
  calibration.reset();
  shapeScene.setGameActive(false);
  shapeScene.setPreview(undefined);
  clearCanvas();
  renderHandsList([]);
  renderCalibration();
  emptyState.hidden = false;
  fpsNode.textContent = "0.0";
  handCountNode.textContent = "0";
  toggleLabel.textContent = "Start";
  replaceIcon(toggleButton, Camera);
  setInteractionMode("idle");
  setStatus(status);
  setEventHud("none", "Waiting for hand input");
}

function renderFrame(frame: TrackingFrame) {
  const smoothing = Number(smoothingInput.value);
  latestHands = analyzer.analyze(frame.hands, smoothing, calibration.getPinchThreshold());
  calibration.update(latestHands, frame.timestamp);
  latestMappedPoints = mapper.mapHands(latestHands, arStage.getBoundingClientRect());
  const isInteractive = calibration.isInteractive();
  shapeScene.setGameActive(isInteractive);
  const events = calibration.isInteractive()
    ? eventEngine.update(latestHands, latestMappedPoints, frame.timestamp, calibration.getProfile())
    : [];

  resizeCanvas(frame.videoWidth, frame.videoHeight);
  drawHands(latestHands, frame.videoWidth, frame.videoHeight);
  applyGestureEvents(events);
  detectIndexCrossDelete(frame.timestamp);
  renderCalibration();
  fpsNode.textContent = frame.fps.toFixed(1);
  handCountNode.textContent = String(latestHands.length);
  renderGameHud();
  renderHandsList(latestHands);
}

function applyGestureEvents(events: GestureEvent[]) {
  for (const event of events) {
    setEventHud(event.type, formatEventDetail(event));

    if (event.type === "pinchStart" || event.type === "pinchMove") {
      handlePinch(event);
    }

    if (event.type === "pinchEnd") {
      finishPinch(event);
    }

    if (event.type === "twoHandTransformStart") {
      if (interactionMode === "mechanismControl") {
        continue;
      }
      shapeScene.endSingleHandTransform();
      shapeScene.beginTransform();
      setInteractionMode("twoHandTransform");
    }

    if (event.type === "twoHandTransformMove" && event.transform) {
      shapeScene.applyTransformAtScreenPoint(
        event.transform.center.x,
        event.transform.center.y,
        event.transform.scaleDelta,
        event.transform.rotationDelta,
        event.transform.rotationXDelta,
        event.transform.rotationYDelta,
      );
      refreshObjectCount();
    }

    if (event.type === "twoHandTransformEnd") {
      shapeScene.endTransform();
      setInteractionMode("idle");
    }
  }
}

function handlePinch(event: GestureEvent) {
  const point = event.screenPoint ?? event.mappedPoint;
  if (!point) {
    return;
  }

  if (event.type === "pinchStart") {
    if (event.mappedPoint && shapeScene.beginMechanismControl(event.mappedPoint)) {
      shapeScene.endSingleHandTransform();
      setInteractionMode("mechanismControl");
      setEventHud("mechanismSelected", "Tilt the divine ramp by rotating your wrist");
      return;
    }

    const trayShape = getShapeUnderPoint(point.x, point.y, TRAY_HIT_EXPAND_Y);
    if (trayShape) {
      startTrayHold(trayShape, event.timestamp);
      return;
    }

    const selected = shapeScene.selectAtScreenPoint(point.x, point.y);
    if (selected) {
      if (event.mappedPoint) {
        shapeScene.beginSingleHandTransform(event.mappedPoint);
      }
      setInteractionMode("movingObject");
      setEventHud("objectSelected", selected);
    }
  }

  if (event.type === "pinchMove") {
    if (interactionMode === "mechanismControl" && event.mappedPoint) {
      shapeScene.updateMechanismControl(event.mappedPoint);
      setEventHud("mechanismMove", "Ramp tilt follows wrist rotation");
      return;
    }

    if (updateTrayHold(point.x, point.y, event.timestamp)) {
      return;
    }

    if (interactionMode === "draggingShape" && activeDraggedShape) {
      shapeScene.setPreviewAtScreenPoint(activeDraggedShape, point.x, point.y);
      return;
    }

    if (interactionMode === "movingObject") {
      if (event.mappedPoint) {
        shapeScene.moveSelectedByHandPoint(event.mappedPoint);
      } else {
        shapeScene.moveSelectedAtScreenPoint(point.x, point.y);
      }
      refreshObjectCount();
    }
  }
}

function finishPinch(event: GestureEvent) {
  const point = event.screenPoint ?? event.mappedPoint;

  if (interactionMode === "draggingShape" && activeDraggedShape && point) {
    const insideStage = isInsideStage(point.x, point.y);
    if (insideStage && !isInsideShapeTray(point.x, point.y)) {
      const id = shapeScene.addShapeAtScreenPoint(activeDraggedShape, point.x, point.y);
      if (id) {
        setEventHud("shapeCreated", `${activeDraggedShape} -> ${id}`);
        refreshObjectCount();
      }
    }
  }

  activeDraggedShape = undefined;
  shapeScene.setPreview(undefined);
  shapeScene.endMechanismControl();
  shapeScene.endSingleHandTransform();
  clearTrayHold();
  setInteractionMode("idle");
}

function drawHands(hands: AnalyzedHand[], width: number, height: number) {
  const context = canvas.getContext("2d");
  if (!context) {
    return;
  }

  context.clearRect(0, 0, canvas.width, canvas.height);
  context.lineWidth = Math.max(2, width / 420);
  context.lineCap = "round";

  for (const hand of hands) {
    const accent = hand.handedness === "Left" ? "#0f8b8d" : "#d1495b";

    context.strokeStyle = "rgba(232, 240, 235, 0.82)";
    for (const connection of HandLandmarker.HAND_CONNECTIONS) {
      const from = hand.landmarks[connection.start];
      const to = hand.landmarks[connection.end];
      context.beginPath();
      context.moveTo(from.x * width, from.y * height);
      context.lineTo(to.x * width, to.y * height);
      context.stroke();
    }

    hand.landmarks.forEach((landmark, index) => {
      context.fillStyle = index === 4 || index === 8 ? "#f7b801" : accent;
      context.beginPath();
      context.arc(landmark.x * width, landmark.y * height, index === 0 ? 7 : 4.5, 0, Math.PI * 2);
      context.fill();
    });
  }
}

function detectIndexCrossDelete(timestamp: number) {
  if (!calibration.isInteractive() || timestamp - lastIndexCrossDeleteAt < INDEX_CROSS_DELETE_COOLDOWN_MS) {
    return;
  }

  const candidates = latestHands.filter((hand) => hand.score >= 0.55 && isFingerExtended(hand, "index"));
  if (candidates.length < 2) {
    clearIndexCrossHold();
    return;
  }

  const stageBounds = arStage.getBoundingClientRect();
  const [first, second] = candidates
    .slice(0, 2)
    .map((hand) => ({
      hand,
      start: mapper.mapLandmark(hand, 5, stageBounds),
      end: mapper.mapLandmark(hand, 8, stageBounds),
    }));
  const intersection = getSegmentIntersection(first.start, first.end, second.start, second.end);
  if (!intersection) {
    clearIndexCrossHold();
    return;
  }

  const angle = getLineAngle(first.start, first.end, second.start, second.end);
  if (angle < INDEX_CROSS_MIN_ANGLE) {
    clearIndexCrossHold();
    return;
  }

  const targetId = shapeScene.getFrontmostObjectAtScreenPoint(intersection.x, intersection.y);
  if (!targetId) {
    clearIndexCrossHold();
    setEventHud("indexCross", `cross at x ${Math.round(intersection.x)} / y ${Math.round(intersection.y)}: no shape`);
    return;
  }

  if (!indexCrossHold || indexCrossHold.objectId !== targetId) {
    indexCrossHold = { objectId: targetId, startedAt: timestamp };
  }

  const progress = Math.min((timestamp - indexCrossHold.startedAt) / INDEX_CROSS_DELETE_HOLD_MS, 1);
  shapeScene.setDeleteTargetObject(targetId);
  showIndexCrossProgress(intersection.x, intersection.y, progress);
  setEventHud("indexCross", `${targetId}: ${Math.round(progress * 100)}%`);

  if (progress < 1) {
    return;
  }

  const deletedId = shapeScene.deleteObject(targetId);
  if (!deletedId) {
    clearIndexCrossHold();
    return;
  }

  clearIndexCrossHold();
  lastIndexCrossDeleteAt = timestamp;
  refreshObjectCount();
  setInteractionMode("idle");
  setEventHud("shapeDeleted", `${deletedId} deleted by crossed index fingers`);
}

function isFingerExtended(hand: AnalyzedHand, name: "index") {
  return hand.fingers.some((finger) => finger.name === name && finger.extended);
}

function getSegmentIntersection(a: { x: number; y: number }, b: { x: number; y: number }, c: { x: number; y: number }, d: { x: number; y: number }) {
  const denominator = (a.x - b.x) * (c.y - d.y) - (a.y - b.y) * (c.x - d.x);
  if (Math.abs(denominator) < 0.001) {
    return undefined;
  }

  const t = ((a.x - c.x) * (c.y - d.y) - (a.y - c.y) * (c.x - d.x)) / denominator;
  const u = -((a.x - b.x) * (a.y - c.y) - (a.y - b.y) * (a.x - c.x)) / denominator;

  if (t < 0 || t > 1 || u < 0 || u > 1) {
    return undefined;
  }

  return {
    x: a.x + t * (b.x - a.x),
    y: a.y + t * (b.y - a.y),
  };
}

function getLineAngle(a: { x: number; y: number }, b: { x: number; y: number }, c: { x: number; y: number }, d: { x: number; y: number }) {
  const angleA = Math.atan2(b.y - a.y, b.x - a.x);
  const angleB = Math.atan2(d.y - c.y, d.x - c.x);
  const diff = Math.abs(normalizeAngle(angleA - angleB));
  return Math.min(diff, Math.PI - diff);
}

function normalizeAngle(angle: number) {
  let normalized = angle;
  while (normalized > Math.PI) normalized -= Math.PI * 2;
  while (normalized < -Math.PI) normalized += Math.PI * 2;
  return normalized;
}

function clearIndexCrossHold() {
  indexCrossHold = undefined;
  shapeScene.setDeleteTargetObject(undefined);
  crossDeleteProgress.hidden = true;
  crossDeleteBar.style.width = "0%";
}

function showIndexCrossProgress(x: number, y: number, progress: number) {
  const stageRect = arStage.getBoundingClientRect();
  crossDeleteProgress.hidden = false;
  crossDeleteProgress.style.left = `${x - stageRect.left}px`;
  crossDeleteProgress.style.top = `${y - stageRect.top}px`;
  crossDeleteBar.style.width = `${Math.round(progress * 100)}%`;
}

function renderShapeLibrary() {
  shapeLibrary.innerHTML = `
    ${SHAPE_LIBRARY.map(
      (shape) => `
        <button class="shape-button" type="button" data-shape="${shape.type}">
          <span class="shape-progress"></span>
          <span class="shape-swatch" style="background: ${shape.color}"></span>
          <span>${shape.label}</span>
        </button>
      `,
    ).join("")}
  `;

  for (const button of shapeLibrary.querySelectorAll<HTMLButtonElement>("[data-shape]")) {
    button.addEventListener("click", () => {
      const type = button.dataset.shape as ShapeType;
      const rect = arStage.getBoundingClientRect();
      const id = shapeScene.addShapeAtScreenPoint(type, rect.left + rect.width / 2, rect.top + rect.height / 2);
      if (id) {
        refreshObjectCount();
        setEventHud("shapeCreated", `${type} -> ${id}`);
      }
    });
  }
}

function getShapeUnderPoint(x: number, y: number, expandY = 0) {
  for (const button of shapeLibrary.querySelectorAll<HTMLButtonElement>("[data-shape]")) {
    const rect = button.getBoundingClientRect();
    if (x >= rect.left && x <= rect.right && y >= rect.top - expandY && y <= rect.bottom + expandY) {
      return button.dataset.shape as ShapeType;
    }
  }
  return undefined;
}

function startTrayHold(shape: ShapeType, timestamp: number) {
  trayHold = { shape, startedAt: timestamp };
  armedTrayShape = undefined;
  activeDraggedShape = undefined;
  setInteractionMode("idle");
  updateTrayHighlight(shape, 0, false);
  setEventHud("trayHold", `${shape}: hold pinch for 1s`);
}

function updateTrayHold(x: number, y: number, timestamp: number) {
  const shape = getShapeUnderPoint(x, y, armedTrayShape ? 0 : TRAY_HIT_EXPAND_Y);

  if (interactionMode === "draggingShape") {
    return false;
  }

  if (trayHold && shape === trayHold.shape) {
    const progress = Math.min((timestamp - trayHold.startedAt) / TRAY_HOLD_MS, 1);
    armedTrayShape = progress >= 1 ? trayHold.shape : undefined;
    updateTrayHighlight(trayHold.shape, progress, progress >= 1);
    setEventHud(progress >= 1 ? "shapeArmed" : "trayHold", `${trayHold.shape}: ${Math.round(progress * 100)}%`);
    return true;
  }

  if (trayHold && armedTrayShape && !shape) {
    activeDraggedShape = armedTrayShape;
    setInteractionMode("draggingShape");
    updateTrayHighlight(armedTrayShape, 1, true);
    shapeScene.setPreviewAtScreenPoint(activeDraggedShape, x, y);
    setEventHud("shapeDrag", `${activeDraggedShape}: move out and release`);
    return true;
  }

  if (shape) {
    startTrayHold(shape, timestamp);
    return true;
  }

  if (trayHold) {
    clearTrayHold();
  }

  return false;
}

function clearTrayHold() {
  trayHold = undefined;
  armedTrayShape = undefined;
  updateTrayHighlight(undefined, 0, false);
}

function updateTrayHighlight(shape: ShapeType | undefined, progress: number, armed: boolean) {
  for (const button of shapeLibrary.querySelectorAll<HTMLButtonElement>("[data-shape]")) {
    const isTarget = button.dataset.shape === shape;
    button.classList.toggle("is-holding", isTarget && progress > 0 && !armed);
    button.classList.toggle("is-armed", isTarget && armed);
    button.style.setProperty("--hold-progress", isTarget ? `${Math.round(progress * 100)}%` : "0%");
  }
}

function renderHandsList(hands: AnalyzedHand[]) {
  if (hands.length === 0) {
    handsList.innerHTML = `<div class="no-hands">No hands detected</div>`;
    return;
  }

  handsList.innerHTML = hands.map(renderHand).join("");
}

function renderHand(hand: AnalyzedHand) {
  const activeFingers = hand.fingers
    .filter((finger) => finger.extended)
    .map((finger) => finger.name)
    .join(", ");

  return `
    <article class="hand-card">
      <div class="hand-header">
        <strong>${hand.handedness}</strong>
        <span>${Math.round(hand.score * 100)}%</span>
      </div>
      <dl>
        <div><dt>Pinch</dt><dd class="${hand.pinch.active ? "hot" : ""}">${hand.pinch.active ? "active" : "open"} / ${hand.pinch.distance.toFixed(2)}</dd></div>
        <div><dt>Palm</dt><dd>${hand.palmFacing}</dd></div>
        <div><dt>Motion</dt><dd>x ${formatMotion(hand.motion.x)} / y ${formatMotion(hand.motion.y)}</dd></div>
        <div><dt>Fingers</dt><dd>${activeFingers || "none"}</dd></div>
      </dl>
    </article>
  `;
}

function renderCalibration() {
  const state = calibration.getState();
  calibrationLayer.hidden = !running || calibration.isInteractive();
  calibrationTitle.textContent = state.prompt;
  calibrationProgress.style.width = `${Math.round(state.progress * 100)}%`;

  if (running && state.stage !== "idle") {
    setStatus(state.prompt);
  }

  if (state.stage === "openHand") {
    calibrationDetail.textContent = "Open your hand and hold it steady for 1 second.";
  } else if (state.stage === "pinch") {
    calibrationDetail.textContent = "Touch thumb and index finger together and hold for 1 second.";
  } else if (state.stage === "ready") {
    calibrationDetail.textContent = `Ready. Pinch threshold ${state.profile?.pinchThreshold.toFixed(2) ?? "0.56"}.`;
  } else if (state.stage === "skipped") {
    calibrationDetail.textContent = "Using default gesture thresholds.";
  } else {
    calibrationDetail.textContent = "Start camera to calibrate hand gestures.";
  }
}

function resizeCanvas(width: number, height: number) {
  if (canvas.width !== width || canvas.height !== height) {
    canvas.width = width;
    canvas.height = height;
  }
}

function clearCanvas() {
  const context = canvas.getContext("2d");
  context?.clearRect(0, 0, canvas.width, canvas.height);
}

function setMirror(enabled: boolean) {
  mapper.setMirror(enabled);
  video.classList.toggle("mirrored", enabled);
  canvas.classList.toggle("mirrored", enabled);
}

function setStatus(message: string) {
  statusNode.textContent = message;
}

function setInteractionMode(mode: InteractionMode) {
  interactionMode = mode;
  interactionModeNode.textContent = mode;
}

function setEventHud(name: string, detail: string) {
  eventName.textContent = name;
  eventDetail.textContent = detail;
}

function refreshObjectCount() {
  const count = shapeScene.getSceneObjects().length;
  objectCount.textContent = String(count);
}

function renderGameHud() {
  const state = shapeScene.getGameState();
  gameStatus.textContent =
    state.status === "waiting"
      ? "Waiting for calibration"
      : state.status === "goal"
      ? "Goal reached"
      : state.status === "fallen"
        ? "Fell off"
        : state.status === "resetting"
          ? "Resetting"
          : state.levelName;
  gameDetail.textContent =
    state.status === "waiting"
      ? "Start camera and finish calibration to begin."
      : state.status === "goal"
      ? "Nice. The ball reached the goal."
      : `${state.resetReason === "fallen" ? "Dropped and reset. " : ""}ball x ${state.ball.x.toFixed(2)} / y ${state.ball.y.toFixed(2)} / z ${state.ball.z.toFixed(2)} / speed ${state.ball.speed.toFixed(2)} / mechanism ${state.activeMechanism ?? "none"}`;
}

function formatEventDetail(event: GestureEvent) {
  if (event.transform) {
    return `scale ${event.transform.scaleDelta.toFixed(2)} ${event.transform.scaleEnabled ? "on" : "off"} / rot x ${event.transform.rotationXDelta.toFixed(2)} / y ${event.transform.rotationYDelta.toFixed(2)} / z ${event.transform.rotationDelta.toFixed(2)} / depth ${event.transform.depthDelta.toFixed(2)}`;
  }
  const point = event.screenPoint ?? event.mappedPoint;
  if (point) {
    return `x ${Math.round(point.x)} / y ${Math.round(point.y)} / pinch ${event.confidence.pinch.toFixed(2)}`;
  }
  return `pinch ${event.confidence.pinch.toFixed(2)} / two-hand ${event.confidence.twoHandTransform.toFixed(2)}`;
}

function isInsideStage(x: number, y: number) {
  const rect = arStage.getBoundingClientRect();
  return x >= rect.left && x <= rect.right && y >= rect.top && y <= rect.bottom;
}

function isInsideShapeTray(x: number, y: number) {
  const rect = shapeLibrary.getBoundingClientRect();
  return x >= rect.left && x <= rect.right && y >= rect.top && y <= rect.bottom;
}

function mountIcons() {
  replaceIcon(toggleButton, Camera);
  resetButton.querySelector("[data-icon='reset']")?.appendChild(createElement(RotateCcw, iconAttrs()));
  const mirrorLabel = mirrorInput.closest("label");
  mirrorLabel?.prepend(createElement(FlipHorizontal, iconAttrs()));
}

function replaceIcon(button: HTMLButtonElement, icon: Parameters<typeof createElement>[0]) {
  const target = button.querySelector("[data-icon]");
  if (!target) {
    return;
  }

  target.replaceChildren(createElement(icon, iconAttrs()));
}

function iconAttrs() {
  return {
    width: 18,
    height: 18,
    "stroke-width": 2.2,
    "aria-hidden": "true",
  };
}

function formatMotion(value: number) {
  return value >= 0 ? `+${value.toFixed(3)}` : value.toFixed(3);
}

function getElement<T extends HTMLElement>(id: string) {
  const element = document.getElementById(id);
  if (!element) {
    throw new Error(`Missing element: ${id}`);
  }
  return element as T;
}
