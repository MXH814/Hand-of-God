import "./styles.css";
import { Camera, CameraOff, createElement, FlipHorizontal, RotateCcw } from "lucide";
import { HandLandmarker } from "@mediapipe/tasks-vision";
import { GestureAnalyzer } from "./gestureAnalyzer";
import { GestureEventEngine } from "./gestureEventEngine";
import { HandTracker, type HandTrackerOptions } from "./handTracker";
import { InteractionMapper } from "./interactionMapper";
import { SHAPE_LIBRARY } from "./shapeLibrary";
import { ShapeScene } from "./shapeScene";
import type {
  AnalyzedHand,
  GestureEvent,
  MappedHandPoint,
  ShapeType,
  TrackingFrame,
} from "./types";

const app = document.querySelector<HTMLDivElement>("#app");

if (!app) {
  throw new Error("App root not found.");
}

app.innerHTML = `
  <main class="app-shell">
    <header class="top-bar">
      <div>
        <p class="eyebrow">HCI Gesture Game</p>
        <h1>Gesture Geometry Sandbox</h1>
      </div>
      <div class="status-pill" id="status">Idle</div>
    </header>

    <section class="workspace">
      <div class="viewport-stack">
        <div class="video-shell" id="video-shell">
          <video id="camera" playsinline muted></video>
          <canvas id="overlay"></canvas>
          <div class="empty-state" id="empty-state">Click Start to enable camera</div>
        </div>
        <div class="scene-shell">
          <div class="scene-header">
            <span>3D Sandbox</span>
            <strong id="object-count">0 objects</strong>
          </div>
          <div class="scene-viewport" id="scene-viewport"></div>
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

        <div class="shape-library" id="shape-library"></div>

        <div class="event-panel">
          <span class="metric-label">Gesture Event</span>
          <strong id="event-name">none</strong>
          <p id="event-detail">Waiting for hand input</p>
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
const videoShell = getElement<HTMLDivElement>("video-shell");
const sceneViewport = getElement<HTMLDivElement>("scene-viewport");
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
const objectCount = getElement<HTMLElement>("object-count");

const analyzer = new GestureAnalyzer();
const mapper = new InteractionMapper();
const eventEngine = new GestureEventEngine();
const shapeScene = new ShapeScene({ container: sceneViewport, library: SHAPE_LIBRARY });
const trackerOptions: HandTrackerOptions = {
  numHands: 2,
  minHandDetectionConfidence: 0.55,
  minHandPresenceConfidence: 0.55,
  minTrackingConfidence: 0.55,
};
let tracker: HandTracker | undefined;
let running = false;
let latestHands: AnalyzedHand[] = [];
let activeDraggedShape: ShapeType | undefined;
let latestMappedPoints: MappedHandPoint[] = [];

mountIcons();
renderShapeLibrary();
setMirror(true);
renderHandsList([]);

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
  setEventHud("reset", "Smoothing and event state cleared");
});

mirrorInput.addEventListener("change", () => {
  setMirror(mirrorInput.checked);
});

sceneViewport.addEventListener("click", (event) => {
  const selected = shapeScene.selectAt(event.clientX, event.clientY);
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
  } catch (error) {
    const message = error instanceof Error ? error.message : "Camera failed";
    stopCamera(message);
  } finally {
    toggleButton.disabled = false;
  }
}

function stopCamera(status = "Idle") {
  tracker?.stop();
  tracker = undefined;
  running = false;
  latestHands = [];
  latestMappedPoints = [];
  activeDraggedShape = undefined;
  analyzer.reset();
  eventEngine.reset();
  shapeScene.setPreview(undefined);
  clearCanvas();
  renderHandsList([]);
  emptyState.hidden = false;
  fpsNode.textContent = "0.0";
  handCountNode.textContent = "0";
  toggleLabel.textContent = "Start";
  replaceIcon(toggleButton, Camera);
  setStatus(status);
  setEventHud("none", "Waiting for hand input");
}

function renderFrame(frame: TrackingFrame) {
  const smoothing = Number(smoothingInput.value);
  latestHands = analyzer.analyze(frame.hands, smoothing);
  latestMappedPoints = mapper.mapHands(latestHands, videoShell.getBoundingClientRect());
  const events = eventEngine.update(latestHands, latestMappedPoints, frame.timestamp);
  resizeCanvas(frame.videoWidth, frame.videoHeight);
  drawHands(latestHands, frame.videoWidth, frame.videoHeight);
  applyGestureEvents(events);
  fpsNode.textContent = frame.fps.toFixed(1);
  handCountNode.textContent = String(latestHands.length);
  renderHandsList(latestHands);
}

function applyGestureEvents(events: GestureEvent[]) {
  for (const event of events) {
    setEventHud(event.type, formatEventDetail(event));

    if (event.type === "pinchStart" || event.type === "pinchMove") {
      handlePinchDrag(event);
    }

    if (event.type === "pinchEnd") {
      finishPinchDrag(event);
    }

    if (event.type === "twoHandTransformMove" && event.transform) {
      const scenePoint = mapper.toScenePoint(
        {
          handId: "two-hand-center",
          handedness: "Unknown",
          x: event.transform.center.x,
          y: event.transform.center.y,
        },
        sceneViewport.getBoundingClientRect(),
      );
      shapeScene.applyTransform(scenePoint, event.transform.scaleDelta, event.transform.rotationDelta);
      refreshObjectCount();
    }
  }
}

function handlePinchDrag(event: GestureEvent) {
  if (!event.mappedPoint) {
    return;
  }

  if (!activeDraggedShape) {
    activeDraggedShape = getShapeUnderPoint(event.mappedPoint.x, event.mappedPoint.y);
  }

  if (!activeDraggedShape) {
    shapeScene.selectAt(event.mappedPoint.x, event.mappedPoint.y);
    return;
  }

  const scenePoint = mapper.toScenePoint(event.mappedPoint, sceneViewport.getBoundingClientRect());
  shapeScene.setPreview(activeDraggedShape, scenePoint);
}

function finishPinchDrag(event: GestureEvent) {
  if (!activeDraggedShape || !event.mappedPoint) {
    activeDraggedShape = undefined;
    shapeScene.setPreview(undefined);
    return;
  }

  const sceneBounds = sceneViewport.getBoundingClientRect();
  const insideScene =
    event.mappedPoint.x >= sceneBounds.left &&
    event.mappedPoint.x <= sceneBounds.right &&
    event.mappedPoint.y >= sceneBounds.top &&
    event.mappedPoint.y <= sceneBounds.bottom;

  if (insideScene) {
    const scenePoint = mapper.toScenePoint(event.mappedPoint, sceneBounds);
    const id = shapeScene.addShape(activeDraggedShape, scenePoint);
    if (id) {
      setEventHud("shapeCreated", `${activeDraggedShape} -> ${id}`);
      refreshObjectCount();
    }
  }

  activeDraggedShape = undefined;
  shapeScene.setPreview(undefined);
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

    context.strokeStyle = "rgba(28, 40, 35, 0.72)";
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

function renderShapeLibrary() {
  shapeLibrary.innerHTML = `
    <span class="metric-label">Shape Library</span>
    <div class="shape-grid">
      ${SHAPE_LIBRARY.map(
        (shape) => `
          <button class="shape-button" type="button" data-shape="${shape.type}">
            <span class="shape-swatch" style="background: ${shape.color}"></span>
            <span>${shape.label}</span>
          </button>
        `,
      ).join("")}
    </div>
  `;

  for (const button of shapeLibrary.querySelectorAll<HTMLButtonElement>("[data-shape]")) {
    button.addEventListener("click", () => {
      const type = button.dataset.shape as ShapeType;
      const id = shapeScene.addShape(type, { x: 0, y: 0 });
      if (id) {
        refreshObjectCount();
        setEventHud("shapeCreated", `${type} -> ${id}`);
      }
    });
  }
}

function getShapeUnderPoint(x: number, y: number) {
  for (const button of shapeLibrary.querySelectorAll<HTMLButtonElement>("[data-shape]")) {
    const rect = button.getBoundingClientRect();
    if (x >= rect.left && x <= rect.right && y >= rect.top && y <= rect.bottom) {
      return button.dataset.shape as ShapeType;
    }
  }
  return undefined;
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
        <div><dt>Motion</dt><dd>x ${formatMotion(hand.motion.x)} · y ${formatMotion(hand.motion.y)}</dd></div>
        <div><dt>Fingers</dt><dd>${activeFingers || "none"}</dd></div>
      </dl>
    </article>
  `;
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

function setEventHud(name: string, detail: string) {
  eventName.textContent = name;
  eventDetail.textContent = detail;
}

function refreshObjectCount() {
  const count = shapeScene.getSceneObjects().length;
  objectCount.textContent = `${count} ${count === 1 ? "object" : "objects"}`;
}

function formatEventDetail(event: GestureEvent) {
  if (event.transform) {
    return `scale ${event.transform.scaleDelta.toFixed(2)} · rotate ${event.transform.rotationDelta.toFixed(2)}`;
  }
  if (event.mappedPoint) {
    return `x ${Math.round(event.mappedPoint.x)} · y ${Math.round(event.mappedPoint.y)} · pinch ${event.confidence.pinch.toFixed(2)}`;
  }
  return `pinch ${event.confidence.pinch.toFixed(2)} · two-hand ${event.confidence.twoHandTransform.toFixed(2)}`;
}

function mountIcons() {
  replaceIcon(toggleButton, Camera);
  getElement<HTMLSpanElement>("reset").querySelector("[data-icon='reset']")?.appendChild(
    createElement(RotateCcw, iconAttrs()),
  );
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
