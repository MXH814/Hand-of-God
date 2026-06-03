import {
  FilesetResolver,
  HandLandmarker,
  type HandLandmarkerResult,
} from "@mediapipe/tasks-vision";
import type { RawHand, TrackingFrame } from "./types";

const WASM_BASE =
  "https://cdn.jsdelivr.net/npm/@mediapipe/tasks-vision@0.10.22/wasm";
const MODEL_URL =
  "https://storage.googleapis.com/mediapipe-models/hand_landmarker/hand_landmarker/float16/latest/hand_landmarker.task";

export interface HandTrackerOptions {
  numHands: number;
  minHandDetectionConfidence: number;
  minHandPresenceConfidence: number;
  minTrackingConfidence: number;
}

export class HandTracker {
  private landmarker?: HandLandmarker;
  private stream?: MediaStream;
  private animationFrame = 0;
  private lastFpsTimestamp = 0;
  private fps = 0;

  constructor(
    private readonly video: HTMLVideoElement,
    private readonly onFrame: (frame: TrackingFrame) => void,
    private readonly onError: (message: string) => void,
    private options: HandTrackerOptions,
  ) {}

  async initialize() {
    const vision = await FilesetResolver.forVisionTasks(WASM_BASE);
    this.landmarker = await HandLandmarker.createFromOptions(vision, {
      baseOptions: {
        modelAssetPath: MODEL_URL,
        delegate: "GPU",
      },
      runningMode: "VIDEO",
      numHands: this.options.numHands,
      minHandDetectionConfidence: this.options.minHandDetectionConfidence,
      minHandPresenceConfidence: this.options.minHandPresenceConfidence,
      minTrackingConfidence: this.options.minTrackingConfidence,
    });
  }

  async start() {
    if (!this.landmarker) {
      await this.initialize();
    }

    this.stream = await navigator.mediaDevices.getUserMedia({
      video: {
        width: { ideal: 1280 },
        height: { ideal: 720 },
        facingMode: "user",
      },
      audio: false,
    });

    this.video.srcObject = this.stream;
    await this.video.play();
    this.lastFpsTimestamp = performance.now();
    this.tick();
  }

  stop() {
    cancelAnimationFrame(this.animationFrame);
    this.stream?.getTracks().forEach((track) => track.stop());
    this.stream = undefined;
    this.video.srcObject = null;
  }

  async updateOptions(options: Partial<HandTrackerOptions>) {
    this.options = { ...this.options, ...options };
    await this.landmarker?.setOptions(options);
  }

  private tick = () => {
    try {
      if (!this.landmarker || this.video.readyState < HTMLMediaElement.HAVE_CURRENT_DATA) {
        this.animationFrame = requestAnimationFrame(this.tick);
        return;
      }

      const timestamp = performance.now();
      const result = this.landmarker.detectForVideo(this.video, timestamp);
      this.updateFps(timestamp);
      this.onFrame(this.toTrackingFrame(result, timestamp));
    } catch (error) {
      this.onError(error instanceof Error ? error.message : "Hand tracking failed.");
    }

    this.animationFrame = requestAnimationFrame(this.tick);
  };

  private updateFps(timestamp: number) {
    const delta = timestamp - this.lastFpsTimestamp;
    if (delta > 0) {
      const instantFps = 1000 / delta;
      this.fps = this.fps === 0 ? instantFps : this.fps * 0.85 + instantFps * 0.15;
    }
    this.lastFpsTimestamp = timestamp;
  }

  private toTrackingFrame(result: HandLandmarkerResult, timestamp: number): TrackingFrame {
    const hands: RawHand[] = result.landmarks.map((landmarks, index) => {
      const category = result.handedness[index]?.[0] ?? result.handednesses[index]?.[0];

      return {
        id: `${category?.categoryName ?? "Unknown"}-${index}`,
        handedness:
          category?.categoryName === "Left" || category?.categoryName === "Right"
            ? category.categoryName
            : "Unknown",
        score: category?.score ?? 0,
        landmarks,
        worldLandmarks: result.worldLandmarks[index] ?? [],
        category,
      };
    });

    return {
      hands,
      timestamp,
      fps: this.fps,
      videoWidth: this.video.videoWidth,
      videoHeight: this.video.videoHeight,
    };
  }
}
