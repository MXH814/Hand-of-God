import type { AnalyzedHand, CalibrationProfile, CalibrationStage } from "./types";

const HOLD_MS = 1000;
const DEFAULT_PINCH_THRESHOLD = 0.56;

interface HoldState {
  startedAt?: number;
  samples: number[];
  progress: number;
}

export class CalibrationManager {
  private stage: CalibrationStage = "idle";
  private profile?: CalibrationProfile;
  private openHand: HoldState = createHoldState();
  private pinch: HoldState = createHoldState();

  start(timestamp = performance.now()) {
    this.stage = "openHand";
    this.profile = undefined;
    this.openHand = createHoldState(timestamp);
    this.pinch = createHoldState();
  }

  skip(timestamp = performance.now()) {
    this.stage = "skipped";
    this.profile = {
      openPalmSpan: 0,
      pinchDistance: DEFAULT_PINCH_THRESHOLD,
      pinchThreshold: DEFAULT_PINCH_THRESHOLD,
      confidenceBaseline: 0.55,
      createdAt: timestamp,
    };
  }

  reset() {
    this.stage = "idle";
    this.profile = undefined;
    this.openHand = createHoldState();
    this.pinch = createHoldState();
  }

  update(hands: AnalyzedHand[], timestamp: number) {
    const hand = hands[0];

    if (this.stage === "openHand") {
      if (hand && isOpenHand(hand)) {
        this.advanceHold(this.openHand, timestamp, getPalmSpan(hand));
        if (this.openHand.progress >= 1) {
          this.stage = "pinch";
          this.pinch = createHoldState(timestamp);
        }
      } else {
        this.openHand = createHoldState();
      }
    }

    if (this.stage === "pinch") {
      if (hand && hand.pinch.distance < 0.62) {
        this.advanceHold(this.pinch, timestamp, hand.pinch.distance);
        if (this.pinch.progress >= 1) {
          this.complete(timestamp);
        }
      } else {
        this.pinch = createHoldState();
      }
    }

    return this.getState();
  }

  getState() {
    return {
      stage: this.stage,
      progress:
        this.stage === "openHand"
          ? this.openHand.progress
          : this.stage === "pinch"
            ? this.pinch.progress
            : this.stage === "ready" || this.stage === "skipped"
              ? 1
              : 0,
      profile: this.profile,
      prompt: this.getPrompt(),
    };
  }

  getProfile() {
    return this.profile;
  }

  getPinchThreshold() {
    return this.profile?.pinchThreshold ?? DEFAULT_PINCH_THRESHOLD;
  }

  isInteractive() {
    return this.stage === "ready" || this.stage === "skipped";
  }

  private advanceHold(state: HoldState, timestamp: number, sample: number) {
    state.startedAt ??= timestamp;
    state.samples.push(sample);
    state.progress = Math.min((timestamp - state.startedAt) / HOLD_MS, 1);
  }

  private complete(timestamp: number) {
    const openPalmSpan = average(this.openHand.samples);
    const pinchDistance = average(this.pinch.samples);
    this.stage = "ready";
    this.profile = {
      openPalmSpan,
      pinchDistance,
      pinchThreshold: clamp(pinchDistance * 1.65, 0.36, 0.62),
      confidenceBaseline: 0.62,
      createdAt: timestamp,
    };
  }

  private getPrompt() {
    switch (this.stage) {
      case "openHand":
        return "Calibrating: open hand";
      case "pinch":
        return "Calibrating: pinch";
      case "ready":
        return "Ready";
      case "skipped":
        return "Skipped calibration";
      case "idle":
        return "Calibration idle";
    }
  }
}

function createHoldState(startedAt?: number): HoldState {
  return { startedAt, samples: [], progress: 0 };
}

function isOpenHand(hand: AnalyzedHand) {
  return hand.score >= 0.55 && hand.fingers.filter((finger) => finger.extended).length >= 4;
}

function getPalmSpan(hand: AnalyzedHand) {
  return distance(hand.landmarks[5], hand.landmarks[17]);
}

function distance(a: { x: number; y: number; z?: number }, b: { x: number; y: number; z?: number }) {
  const dz = (a.z ?? 0) - (b.z ?? 0);
  return Math.hypot(a.x - b.x, a.y - b.y, dz);
}

function average(values: number[]) {
  if (values.length === 0) {
    return 0;
  }
  return values.reduce((sum, value) => sum + value, 0) / values.length;
}

function clamp(value: number, min: number, max: number) {
  return Math.min(Math.max(value, min), max);
}
