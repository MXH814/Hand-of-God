import type { AnalyzedHand, MappedHandPoint, Vector2 } from "./types";

export class InteractionMapper {
  private mirror = true;

  setMirror(enabled: boolean) {
    this.mirror = enabled;
  }

  mapHand(hand: AnalyzedHand, bounds: DOMRect): MappedHandPoint {
    const normalizedX = this.mirror ? 1 - hand.center.x : hand.center.x;

    return {
      handId: hand.id,
      handedness: hand.handedness,
      x: bounds.left + normalizedX * bounds.width,
      y: bounds.top + hand.center.y * bounds.height,
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
