import type { Category, Landmark, NormalizedLandmark } from "@mediapipe/tasks-vision";

export type HandSide = "Left" | "Right" | "Unknown";
export type FingerName = "thumb" | "index" | "middle" | "ring" | "pinky";
export type GestureEventType =
  | "pinchStart"
  | "pinchMove"
  | "pinchEnd"
  | "twoHandTransformStart"
  | "twoHandTransformMove"
  | "twoHandTransformEnd";
export type ShapeType = "cube" | "sphere" | "cylinder" | "cone" | "torus";
export type CalibrationStage = "idle" | "openHand" | "pinch" | "ready" | "skipped";
export type InteractionMode = "idle" | "draggingShape" | "movingObject" | "twoHandTransform";

export interface Vector2 {
  x: number;
  y: number;
}

export interface Vector3 {
  x: number;
  y: number;
  z: number;
}

export interface FingerState {
  name: FingerName;
  extended: boolean;
  curl: number;
}

export interface RawHand {
  id: string;
  handedness: HandSide;
  score: number;
  landmarks: NormalizedLandmark[];
  worldLandmarks: Landmark[];
  category?: Category;
}

export interface AnalyzedHand extends RawHand {
  center: Vector3;
  palmNormal: Vector3;
  palmFacing: "camera" | "away" | "side";
  fingers: FingerState[];
  pinch: {
    active: boolean;
    distance: number;
  };
  indexMiddleTogether: {
    active: boolean;
    distance: number;
  };
  motion: Vector2;
}

export interface GestureConfidence {
  pinch: number;
  twoHandTransform: number;
}

export interface MappedHandPoint {
  handId: string;
  handedness: HandSide;
  x: number;
  y: number;
  z: number;
  handScale: number;
}

export interface TwoHandTransform {
  center: Vector2;
  distance: number;
  angle: number;
  scaleDelta: number;
  rotationDelta: number;
  rotationXDelta: number;
  rotationYDelta: number;
  depthDelta: number;
  scaleEnabled: boolean;
}

export interface GestureEvent {
  type: GestureEventType;
  timestamp: number;
  primaryHand?: AnalyzedHand;
  mappedPoint?: MappedHandPoint;
  screenPoint?: Vector2;
  transform?: TwoHandTransform;
  confidence: GestureConfidence;
  calibratedConfidence?: GestureConfidence;
}

export interface CalibrationProfile {
  openPalmSpan: number;
  pinchDistance: number;
  pinchThreshold: number;
  confidenceBaseline: number;
  createdAt: number;
}

export interface SceneObject {
  id: string;
  type: ShapeType;
  position: Vector3;
  rotation: Vector3;
  scale: number;
  selected: boolean;
}

export interface ShapeLibraryItem {
  type: ShapeType;
  label: string;
  color: string;
  defaultScale: number;
}

export interface TrackingFrame {
  hands: RawHand[];
  timestamp: number;
  fps: number;
  videoWidth: number;
  videoHeight: number;
}
