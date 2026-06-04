import type {
  AnalyzedHand,
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
  previousAngle: number;
}

export class GestureEventEngine {
  private pinchState: PinchState = { active: false, startFrames: 0, endFrames: 0 };
  private twoHandState: TwoHandState = {
    active: false,
    startFrames: 0,
    endFrames: 0,
    baselineDistance: 1,
    previousAngle: 0,
  };

  update(hands: AnalyzedHand[], mappedPoints: MappedHandPoint[], timestamp: number): GestureEvent[] {
    const events: GestureEvent[] = [];
    const primaryHand = choosePrimaryHand(hands);
    const primaryPoint = primaryHand
      ? mappedPoints.find((point) => point.handId === primaryHand.id)
      : undefined;
    const confidence = getConfidence(hands);

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
      previousAngle: 0,
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
        confidence,
      });
      return;
    }

    if (this.pinchState.active && hasPinch && this.pinchState.handId === hand?.id) {
      events.push({
        type: "pinchMove",
        timestamp,
        primaryHand: hand,
        mappedPoint: point,
        confidence,
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
        confidence,
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
    const pair = getTwoHandPair(hands, mappedPoints);
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
      this.twoHandState.previousAngle = transform.angle;
      events.push({ type: "twoHandTransformStart", timestamp, transform, confidence });
      return;
    }

    if (this.twoHandState.active && canTransform) {
      const moveTransform = getTwoHandTransform(pair.left, pair.right, this.twoHandState);
      this.twoHandState.previousAngle = moveTransform.angle;
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
  return hands.find((hand) => hand.handedness === "Right") ?? hands[0];
}

function getConfidence(hands: AnalyzedHand[]): GestureConfidence {
  const primary = choosePrimaryHand(hands);
  const pinchDistance = primary?.pinch.distance ?? 1;

  return {
    pinch: primary ? clamp01((0.7 - pinchDistance) / 0.45) * primary.score : 0,
    twoHandTransform:
      hands.length >= 2 ? clamp01((hands[0].score + hands[1].score) / 2) : 0,
  };
}

function getTwoHandPair(hands: AnalyzedHand[], mappedPoints: MappedHandPoint[]) {
  if (hands.length < 2) {
    return undefined;
  }

  const sortedHands = [...hands].sort((a, b) => {
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

  return {
    center,
    distance,
    angle,
    scaleDelta: clamp(distance / (state.baselineDistance || distance || 1), 0.35, 3),
    rotationDelta: normalizeAngle(angle - state.previousAngle),
  };
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
