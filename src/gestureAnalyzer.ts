import type { AnalyzedHand, FingerName, FingerState, RawHand, Vector2, Vector3 } from "./types";

const FINGER_TIPS: Record<FingerName, number> = {
  thumb: 4,
  index: 8,
  middle: 12,
  ring: 16,
  pinky: 20,
};

const FINGER_PIPS: Record<FingerName, number> = {
  thumb: 3,
  index: 6,
  middle: 10,
  ring: 14,
  pinky: 18,
};

const FINGER_MCP: Record<FingerName, number> = {
  thumb: 2,
  index: 5,
  middle: 9,
  ring: 13,
  pinky: 17,
};

interface PreviousHand {
  center: Vector3;
  motion: Vector2;
}

export class GestureAnalyzer {
  private previousHands = new Map<string, PreviousHand>();

  analyze(hands: RawHand[], smoothing: number, pinchThreshold = 0.56): AnalyzedHand[] {
    const alpha = Math.min(Math.max(smoothing, 0), 0.95);
    const currentIds = new Set<string>();

    const analyzed = hands.map((hand) => {
      currentIds.add(hand.id);
      return this.analyzeHand(hand, alpha, pinchThreshold);
    });

    for (const id of this.previousHands.keys()) {
      if (!currentIds.has(id)) {
        this.previousHands.delete(id);
      }
    }

    return analyzed;
  }

  reset() {
    this.previousHands.clear();
  }

  private analyzeHand(hand: RawHand, smoothing: number, pinchThreshold: number): AnalyzedHand {
    const center = average([
      toVector3(hand.landmarks[0]),
      toVector3(hand.landmarks[5]),
      toVector3(hand.landmarks[9]),
      toVector3(hand.landmarks[13]),
      toVector3(hand.landmarks[17]),
    ]);
    const previous = this.previousHands.get(hand.id);
    const rawMotion = previous
      ? {
          x: center.x - previous.center.x,
          y: center.y - previous.center.y,
        }
      : { x: 0, y: 0 };
    const motion = previous
      ? {
          x: previous.motion.x * smoothing + rawMotion.x * (1 - smoothing),
          y: previous.motion.y * smoothing + rawMotion.y * (1 - smoothing),
        }
      : rawMotion;
    const palmNormal = normalize(
      cross(
        subtract(toVector3(hand.landmarks[5]), toVector3(hand.landmarks[0])),
        subtract(toVector3(hand.landmarks[17]), toVector3(hand.landmarks[0])),
      ),
    );

    this.previousHands.set(hand.id, { center, motion });

    return {
      ...hand,
      center,
      palmNormal,
      palmFacing: classifyPalmFacing(palmNormal),
      fingers: getFingerStates(hand),
      pinch: getPinch(hand, pinchThreshold),
      indexMiddleTogether: getIndexMiddleTogether(hand),
      motion,
    };
  }
}

function getFingerStates(hand: RawHand): FingerState[] {
  return (Object.keys(FINGER_TIPS) as FingerName[]).map((name) => {
    const tip = hand.landmarks[FINGER_TIPS[name]];
    const pip = hand.landmarks[FINGER_PIPS[name]];
    const mcp = hand.landmarks[FINGER_MCP[name]];
    const wrist = hand.landmarks[0];
    const palmSpan = distance(hand.landmarks[5], hand.landmarks[17]) || 1;
    const tipToWrist = distance(tip, wrist);
    const pipToWrist = distance(pip, wrist);
    const curl = clamp01(distance(tip, mcp) / palmSpan);

    if (name === "thumb") {
      return {
        name,
        extended: distance(tip, hand.landmarks[5]) > distance(pip, hand.landmarks[5]),
        curl,
      };
    }

    return {
      name,
      extended: tipToWrist > pipToWrist && tip.y < pip.y,
      curl,
    };
  });
}

function getPinch(hand: RawHand, threshold: number) {
  const palmSpan = distance(hand.landmarks[5], hand.landmarks[17]) || 1;
  const normalizedDistance = distance(hand.landmarks[4], hand.landmarks[8]) / palmSpan;

  return {
    active: normalizedDistance < threshold,
    distance: normalizedDistance,
  };
}

function getIndexMiddleTogether(hand: RawHand) {
  const palmSpan = distance(hand.landmarks[5], hand.landmarks[17]) || 1;
  const normalizedDistance = distance(hand.landmarks[8], hand.landmarks[12]) / palmSpan;
  const indexExtended = hand.landmarks[8].y < hand.landmarks[6].y;
  const middleExtended = hand.landmarks[12].y < hand.landmarks[10].y;

  return {
    active: normalizedDistance < 0.62 && indexExtended && middleExtended,
    distance: normalizedDistance,
  };
}

function classifyPalmFacing(normal: Vector3): AnalyzedHand["palmFacing"] {
  if (normal.z > 0.35) {
    return "camera";
  }
  if (normal.z < -0.35) {
    return "away";
  }
  return "side";
}

function toVector3(point: { x: number; y: number; z?: number }): Vector3 {
  return { x: point.x, y: point.y, z: point.z ?? 0 };
}

function distance(a: { x: number; y: number; z?: number }, b: { x: number; y: number; z?: number }) {
  const dz = (a.z ?? 0) - (b.z ?? 0);
  return Math.hypot(a.x - b.x, a.y - b.y, dz);
}

function subtract(a: Vector3, b: Vector3): Vector3 {
  return { x: a.x - b.x, y: a.y - b.y, z: a.z - b.z };
}

function cross(a: Vector3, b: Vector3): Vector3 {
  return {
    x: a.y * b.z - a.z * b.y,
    y: a.z * b.x - a.x * b.z,
    z: a.x * b.y - a.y * b.x,
  };
}

function normalize(vector: Vector3): Vector3 {
  const length = Math.hypot(vector.x, vector.y, vector.z) || 1;
  return {
    x: vector.x / length,
    y: vector.y / length,
    z: vector.z / length,
  };
}

function average(vectors: Vector3[]): Vector3 {
  return vectors.reduce(
    (sum, vector) => ({
      x: sum.x + vector.x / vectors.length,
      y: sum.y + vector.y / vectors.length,
      z: sum.z + vector.z / vectors.length,
    }),
    { x: 0, y: 0, z: 0 },
  );
}

function clamp01(value: number) {
  return Math.min(Math.max(value, 0), 1);
}
