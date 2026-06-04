import type { AnalyzedHand, MappedHandPoint, Vector2 } from "./types";

export class InteractionMapper {
  private mirror = true;

  setMirror(enabled: boolean) {
    this.mirror = enabled;
  }

  mapHand(hand: AnalyzedHand, bounds: DOMRect): MappedHandPoint {
    const pinchPoint = getPinchPoint(hand);
    const normalizedX = this.mirror ? 1 - pinchPoint.x : pinchPoint.x;

    return {
      handId: hand.id,
      handedness: hand.handedness,
      x: bounds.left + normalizedX * bounds.width,
      y: bounds.top + pinchPoint.y * bounds.height,
      z: pinchPoint.z,
      handScale: getHandScale(hand),
    };
  }

  mapHands(hands: AnalyzedHand[], bounds: DOMRect): MappedHandPoint[] {
    return hands.map((hand) => this.mapHand(hand, bounds));
  }

  mapLandmark(hand: AnalyzedHand, landmarkIndex: number, bounds: DOMRect): Vector2 {
    const landmark = hand.landmarks[landmarkIndex];
    const normalizedX = this.mirror ? 1 - landmark.x : landmark.x;

    return {
      x: bounds.left + normalizedX * bounds.width,
      y: bounds.top + landmark.y * bounds.height,
    };
  }

  toScenePoint(point: MappedHandPoint, bounds: DOMRect): Vector2 {
    return {
      x: ((point.x - bounds.left) / bounds.width - 0.5) * 6,
      y: -((point.y - bounds.top) / bounds.height - 0.5) * 3.4,
    };
  }
}

function getPinchPoint(hand: AnalyzedHand): Vector2 & { z: number } {
  const thumbTip = hand.landmarks[4];
  const indexTip = hand.landmarks[8];

  return {
    x: (thumbTip.x + indexTip.x) / 2,
    y: (thumbTip.y + indexTip.y) / 2,
    z: ((thumbTip.z ?? 0) + (indexTip.z ?? 0)) / 2,
  };
}

function getHandScale(hand: AnalyzedHand) {
  const wrist = hand.landmarks[0];
  const indexMcp = hand.landmarks[5];
  const middleMcp = hand.landmarks[9];
  const pinkyMcp = hand.landmarks[17];

  return (
    distance(indexMcp, pinkyMcp) * 0.55 +
    distance(wrist, middleMcp) * 0.45
  );
}

function distance(a: { x: number; y: number; z?: number }, b: { x: number; y: number; z?: number }) {
  const dz = (a.z ?? 0) - (b.z ?? 0);
  return Math.hypot(a.x - b.x, a.y - b.y, dz);
}
