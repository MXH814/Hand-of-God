import type { Category, Landmark, NormalizedLandmark } from "@mediapipe/tasks-vision";

export type HandSide = "Left" | "Right" | "Unknown";
export type FingerName = "thumb" | "index" | "middle" | "ring" | "pinky";

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
  motion: Vector2;
}

export interface TrackingFrame {
  hands: RawHand[];
  timestamp: number;
  fps: number;
  videoWidth: number;
  videoHeight: number;
}
