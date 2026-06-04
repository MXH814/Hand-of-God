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
    };
  }

  mapHands(hands: AnalyzedHand[], bounds: DOMRect): MappedHandPoint[] {
    return hands.map((hand) => this.mapHand(hand, bounds));
  }

  toScenePoint(point: MappedHandPoint, bounds: DOMRect): Vector2 {
    return {
      x: ((point.x - bounds.left) / bounds.width - 0.5) * 6,
      y: -((point.y - bounds.top) / bounds.height - 0.5) * 3.4,
    };
  }
}

function getPinchPoint(hand: AnalyzedHand): Vector2 {
  const thumbTip = hand.landmarks[4];
  const indexTip = hand.landmarks[8];

  return {
    x: (thumbTip.x + indexTip.x) / 2,
    y: (thumbTip.y + indexTip.y) / 2,
  };
}
