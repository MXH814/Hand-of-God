import type {
  AnalyzedHand,
  CalibrationProfile,
  GestureConfidence,
  GestureEvent,
  MappedHandPoint,
  TwoHandTransform,
  Vector2,
} from "./types";

const PINCH_START_FRAMES = 2;
const PINCH_END_FRAMES = 2;
const TWO_HAND_START_FRAMES = 3;
const TWO_HAND_END_FRAMES = 3;

interface PinchState {
  active: boolean;
  startFrames: number;
  endFrames: number;
  handId?: string;
}

interface TwoHandState {
  active: boolean;
  startFrames: number;
  endFrames: number;
  baselineDistance: number;
  baselineAngle: number;
  baselineAverageScale: number;
  baselineScaleSkew: number;
  baselineZSkew: number;
}

export class GestureEventEngine {
  private pinchState: PinchState = { active: false, startFrames: 0, endFrames: 0 };
  private twoHandState: TwoHandState = {
    active: false,
    startFrames: 0,
    endFrames: 0,
    baselineDistance: 1,
    baselineAngle: 0,
    baselineAverageScale: 1,
    baselineScaleSkew: 0,
    baselineZSkew: 0,
  };

  update(
    hands: AnalyzedHand[],
    mappedPoints: MappedHandPoint[],
    timestamp: number,
    profile?: CalibrationProfile,
  ): GestureEvent[] {
    const events: GestureEvent[] = [];
    const primaryHand = choosePrimaryHand(hands);
    const primaryPoint = primaryHand
      ? mappedPoints.find((point) => point.handId === primaryHand.id)
      : undefined;
    const confidence = getConfidence(hands, profile?.pinchThreshold ?? 0.45);

    this.updatePinch(primaryHand, primaryPoint, timestamp, confidence, events);
    this.updateTwoHand(hands, mappedPoints, timestamp, confidence, events);

    return events;
  }

  reset() {
    this.pinchState = { active: false, startFrames: 0, endFrames: 0 };
    this.twoHandState = {
      active: false,
      startFrames: 0,
      endFrames: 0,
      baselineDistance: 1,
      baselineAngle: 0,
      baselineAverageScale: 1,
      baselineScaleSkew: 0,
      baselineZSkew: 0,
    };
  }

  private updatePinch(
    hand: AnalyzedHand | undefined,
    point: MappedHandPoint | undefined,
    timestamp: number,
    confidence: GestureConfidence,
    events: GestureEvent[],
  ) {
    const hasPinch = Boolean(hand?.pinch.active && point && confidence.pinch >= 0.55);

    if (hasPinch) {
      this.pinchState.startFrames += 1;
      this.pinchState.endFrames = 0;
    } else {
      this.pinchState.endFrames += 1;
      this.pinchState.startFrames = 0;
    }

    if (!this.pinchState.active && hasPinch && this.pinchState.startFrames >= PINCH_START_FRAMES) {
      this.pinchState.active = true;
      this.pinchState.handId = hand?.id;
      events.push({
        type: "pinchStart",
        timestamp,
        primaryHand: hand,
        mappedPoint: point,
        screenPoint: point,
        confidence,
        calibratedConfidence: confidence,
      });
      return;
    }

    if (this.pinchState.active && hasPinch && this.pinchState.handId === hand?.id) {
      events.push({
        type: "pinchMove",
        timestamp,
        primaryHand: hand,
        mappedPoint: point,
        screenPoint: point,
        confidence,
        calibratedConfidence: confidence,
      });
      return;
    }

    if (this.pinchState.active && this.pinchState.endFrames >= PINCH_END_FRAMES) {
      this.pinchState.active = false;
      this.pinchState.handId = undefined;
      events.push({
        type: "pinchEnd",
        timestamp,
        primaryHand: hand,
        mappedPoint: point,
        screenPoint: point,
        confidence,
        calibratedConfidence: confidence,
      });
    }
  }

  private updateTwoHand(
    hands: AnalyzedHand[],
    mappedPoints: MappedHandPoint[],
    timestamp: number,
    confidence: GestureConfidence,
    events: GestureEvent[],
  ) {
    const pair = getTwoHandPinchPair(hands, mappedPoints);
    const canTransform = Boolean(pair && confidence.twoHandTransform >= 0.55);

    if (canTransform) {
      this.twoHandState.startFrames += 1;
      this.twoHandState.endFrames = 0;
    } else {
      this.twoHandState.endFrames += 1;
      this.twoHandState.startFrames = 0;
    }

    if (!pair) {
      if (this.twoHandState.active && this.twoHandState.endFrames >= TWO_HAND_END_FRAMES) {
        this.twoHandState.active = false;
        events.push({ type: "twoHandTransformEnd", timestamp, confidence });
      }
      return;
    }

    const transform = getTwoHandTransform(pair.left, pair.right, this.twoHandState);

    if (!this.twoHandState.active && canTransform && this.twoHandState.startFrames >= TWO_HAND_START_FRAMES) {
      this.twoHandState.active = true;
      this.twoHandState.baselineDistance = transform.distance || 1;
      this.twoHandState.baselineAngle = transform.angle;
      this.twoHandState.baselineAverageScale = getAverageScale(pair.left, pair.right);
      this.twoHandState.baselineScaleSkew = getScaleSkew(pair.left, pair.right);
      this.twoHandState.baselineZSkew = getZSkew(pair.left, pair.right);
      events.push({ type: "twoHandTransformStart", timestamp, transform, confidence });
      return;
    }

    if (this.twoHandState.active && canTransform) {
      const moveTransform = getTwoHandTransform(pair.left, pair.right, this.twoHandState);
      events.push({
        type: "twoHandTransformMove",
        timestamp,
        transform: moveTransform,
        confidence,
      });
      return;
    }

    if (this.twoHandState.active && this.twoHandState.endFrames >= TWO_HAND_END_FRAMES) {
      this.twoHandState.active = false;
      events.push({ type: "twoHandTransformEnd", timestamp, transform, confidence });
    }
  }
}

function choosePrimaryHand(hands: AnalyzedHand[]) {
  return (
    hands.find((hand) => hand.pinch.active && hand.handedness === "Right") ??
    hands.find((hand) => hand.pinch.active) ??
    hands.find((hand) => hand.handedness === "Right") ??
    hands[0]
  );
}

function getConfidence(hands: AnalyzedHand[], pinchThreshold: number): GestureConfidence {
  const primary = choosePrimaryHand(hands);
  const pinchDistance = primary?.pinch.distance ?? 1;
  const confidenceWindow = 0.24;

  return {
    pinch: primary ? clamp01((pinchThreshold + confidenceWindow - pinchDistance) / confidenceWindow) * primary.score : 0,
    twoHandTransform:
      hands.length >= 2 ? clamp01((hands[0].score + hands[1].score) / 2) : 0,
  };
}

function getTwoHandPinchPair(hands: AnalyzedHand[], mappedPoints: MappedHandPoint[]) {
  if (hands.length < 2) {
    return undefined;
  }

  const pinchingHands = hands.filter((hand) => hand.pinch.active && hand.score >= 0.55);
  if (pinchingHands.length < 2) {
    return undefined;
  }

  const sortedHands = [...pinchingHands].sort((a, b) => {
    if (a.handedness === "Left" && b.handedness !== "Left") return -1;
    if (a.handedness !== "Left" && b.handedness === "Left") return 1;
    return b.score - a.score;
  });
  const left = mappedPoints.find((point) => point.handId === sortedHands[0].id);
  const right = mappedPoints.find((point) => point.handId === sortedHands[1].id);

  return left && right ? { left, right } : undefined;
}

function getTwoHandTransform(
  left: MappedHandPoint,
  right: MappedHandPoint,
  state: TwoHandState,
): TwoHandTransform {
  const center: Vector2 = {
    x: (left.x + right.x) / 2,
    y: (left.y + right.y) / 2,
  };
  const dx = right.x - left.x;
  const dy = right.y - left.y;
  const distance = Math.hypot(dx, dy) || 1;
  const angle = Math.atan2(dy, dx);
  const averageScale = getAverageScale(left, right);
  const depthDelta = Math.log(averageScale / (state.baselineAverageScale || averageScale || 1));
  const scaleSkewDelta = (getScaleSkew(left, right) - state.baselineScaleSkew) / (state.baselineAverageScale || averageScale || 1);
  const zSkewDelta = getZSkew(left, right) - state.baselineZSkew;

  return {
    center,
    distance,
    angle,
    scaleDelta: clamp(distance / (state.baselineDistance || distance || 1), 0.35, 3),
    rotationDelta: clamp(normalizeAngle(angle - state.baselineAngle) * 1.35, -Math.PI, Math.PI),
    rotationXDelta: clamp(depthDelta * 4.2, -1.35, 1.35),
    rotationYDelta: clamp(scaleSkewDelta * 3.2 + zSkewDelta * 5.5, -1.45, 1.45),
    depthDelta,
  };
}

function getAverageScale(left: MappedHandPoint, right: MappedHandPoint) {
  return Math.max((left.handScale + right.handScale) / 2, 0.001);
}

function getScaleSkew(left: MappedHandPoint, right: MappedHandPoint) {
  return right.handScale - left.handScale;
}

function getZSkew(left: MappedHandPoint, right: MappedHandPoint) {
  return left.z - right.z;
}

function normalizeAngle(angle: number) {
  let normalized = angle;
  while (normalized > Math.PI) normalized -= Math.PI * 2;
  while (normalized < -Math.PI) normalized += Math.PI * 2;
  return normalized;
}

function clamp01(value: number) {
  return clamp(value, 0, 1);
}

function clamp(value: number, min: number, max: number) {
  return Math.min(Math.max(value, min), max);
}
