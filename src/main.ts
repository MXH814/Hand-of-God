import "./styles.css";
import { Camera, CameraOff, createElement, FlipHorizontal, RotateCcw } from "lucide";
import { HandLandmarker } from "@mediapipe/tasks-vision";
import { GestureAnalyzer } from "./gestureAnalyzer";
import { HandTracker, type HandTrackerOptions } from "./handTracker";
import type { AnalyzedHand, TrackingFrame } from "./types";

const app = document.querySelector<HTMLDivElement>("#app");

if (!app) {
  throw new Error("App root not found.");
}

app.innerHTML = `
  <main class="app-shell">
    <header class="top-bar">
      <div>
        <p class="eyebrow">HCI Gesture Game</p>
        <h1>Hand Tracking Debugger</h1>
      </div>
      <div class="status-pill" id="status">Idle</div>
    </header>

    <section class="stage">
      <div class="video-shell">
        <video id="camera" playsinline muted></video>
        <canvas id="overlay"></canvas>
        <div class="empty-state" id="empty-state">Click Start to enable camera</div>
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
      </div>
    </section>
  </main>
`;

const video = getElement<HTMLVideoElement>("camera");
const canvas = getElement<HTMLCanvasElement>("overlay");
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

const analyzer = new GestureAnalyzer();
const trackerOptions: HandTrackerOptions = {
  numHands: 2,
  minHandDetectionConfidence: 0.55,
  minHandPresenceConfidence: 0.55,
  minTrackingConfidence: 0.55,
};
let tracker: HandTracker | undefined;
let running = false;
let latestHands: AnalyzedHand[] = [];

mountIcons();
setMirror(true);

toggleButton.addEventListener("click", async () => {
  if (running) {
    stopCamera();
    return;
  }

  await startCamera();
});

resetButton.addEventListener("click", () => {
  analyzer.reset();
  setStatus("Smoothing reset");
});

mirrorInput.addEventListener("change", () => {
  setMirror(mirrorInput.checked);
});

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
  analyzer.reset();
  clearCanvas();
  renderHandsList([]);
  emptyState.hidden = false;
  fpsNode.textContent = "0.0";
  handCountNode.textContent = "0";
  toggleLabel.textContent = "Start";
  replaceIcon(toggleButton, Camera);
  setStatus(status);
}

function renderFrame(frame: TrackingFrame) {
  const smoothing = Number(smoothingInput.value);
  latestHands = analyzer.analyze(frame.hands, smoothing);
  resizeCanvas(frame.videoWidth, frame.videoHeight);
  drawHands(latestHands, frame.videoWidth, frame.videoHeight);
  fpsNode.textContent = frame.fps.toFixed(1);
  handCountNode.textContent = String(latestHands.length);
  renderHandsList(latestHands);
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
  video.classList.toggle("mirrored", enabled);
  canvas.classList.toggle("mirrored", enabled);
}

function setStatus(message: string) {
  statusNode.textContent = message;
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
