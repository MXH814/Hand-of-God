export type LevelBlockKind = "void" | "platform" | "wall" | "rail" | "ramp";
export type LevelPropKind = "pillar" | "rune" | "arrow" | "torch";

export interface LevelRotation {
  x?: number;
  y?: number;
  z?: number;
}

export interface LevelBlock {
  id: string;
  kind: LevelBlockKind;
  position: { x: number; y: number; z: number };
  size: { x: number; y: number; z: number };
  rotation?: LevelRotation;
  color: string;
  physics?: boolean;
  opacity?: number;
}

export interface LevelProp {
  id: string;
  kind: LevelPropKind;
  position: { x: number; y: number; z: number };
  radius?: number;
  height?: number;
  size?: { x: number; y: number; z: number };
  rotation?: LevelRotation;
  color: string;
}

export interface LevelMechanism {
  id: string;
  name: string;
  kind: "tiltRamp";
  position: { x: number; y: number; z: number };
  size: { x: number; y: number; z: number };
  yaw: number;
  initialTilt: number;
  minTilt: number;
  maxTilt: number;
  color: string;
  handleOffset: { x: number; y: number; z: number };
}

export interface GameLevel {
  id: string;
  name: string;
  theme: string;
  gravity: { x: number; y: number; z: number };
  start: { x: number; y: number; z: number };
  goal: { x: number; y: number; z: number; radius: number };
  fallBounds: {
    left: number;
    right: number;
    back: number;
    front: number;
    bottom: number;
  };
  blocks: LevelBlock[];
  props: LevelProp[];
  mechanisms: LevelMechanism[];
}

export const LEVEL_01: GameLevel = {
  id: "level-01",
  name: "Level 01: Temple of the First Hand",
  theme: "mechanical-temple",
  gravity: { x: 0, y: -9.8, z: 0 },
  start: { x: -3.15, y: 0.72, z: 1.42 },
  goal: { x: 3.02, y: 0.74, z: -1.45, radius: 0.48 },
  fallBounds: {
    left: -4.7,
    right: 4.7,
    back: -3.2,
    front: 2.8,
    bottom: -2.2,
  },
  blocks: [
    {
      id: "shadow-void",
      kind: "void",
      position: { x: 0, y: -0.72, z: 0 },
      size: { x: 9.2, y: 0.18, z: 5.9 },
      color: "#07110f",
      physics: false,
    },
    {
      id: "start-plaza",
      kind: "platform",
      position: { x: -3.15, y: 0.23, z: 1.42 },
      size: { x: 1.72, y: 0.46, z: 1.28 },
      color: "#6e7f77",
    },
    {
      id: "upper-causeway",
      kind: "platform",
      position: { x: -1.78, y: 0.17, z: 0.92 },
      size: { x: 1.72, y: 0.34, z: 0.74 },
      rotation: { y: -0.22 },
      color: "#8c9a91",
    },
    {
      id: "middle-island",
      kind: "platform",
      position: { x: 1.18, y: 0.13, z: -0.06 },
      size: { x: 1.72, y: 0.34, z: 1.12 },
      rotation: { y: -0.08 },
      color: "#7e8b83",
    },
    {
      id: "goal-terrace",
      kind: "platform",
      position: { x: 2.95, y: 0.23, z: -1.42 },
      size: { x: 1.46, y: 0.46, z: 1.34 },
      color: "#8f9d95",
    },
    {
      id: "lower-catch-ledge",
      kind: "platform",
      position: { x: 0.18, y: -0.03, z: -1.58 },
      size: { x: 2.08, y: 0.3, z: 0.48 },
      rotation: { y: 0.08 },
      color: "#75847d",
    },
    {
      id: "start-back-wall",
      kind: "wall",
      position: { x: -3.28, y: 0.72, z: 2.05 },
      size: { x: 1.5, y: 0.9, z: 0.14 },
      color: "#485a52",
    },
    {
      id: "start-left-rail",
      kind: "rail",
      position: { x: -3.93, y: 0.68, z: 1.35 },
      size: { x: 0.16, y: 0.74, z: 1.12 },
      color: "#42554d",
    },
    {
      id: "upper-right-rail",
      kind: "rail",
      position: { x: -1.54, y: 0.64, z: 1.34 },
      size: { x: 1.58, y: 0.58, z: 0.14 },
      rotation: { y: -0.22 },
      color: "#41524b",
    },
    {
      id: "island-back-rail",
      kind: "rail",
      position: { x: 1.24, y: 0.58, z: 0.52 },
      size: { x: 1.42, y: 0.54, z: 0.13 },
      rotation: { y: -0.08 },
      color: "#41524b",
    },
    {
      id: "goal-back-wall",
      kind: "wall",
      position: { x: 2.96, y: 0.76, z: -2.08 },
      size: { x: 1.42, y: 0.9, z: 0.16 },
      color: "#485a52",
    },
    {
      id: "goal-side-rail",
      kind: "rail",
      position: { x: 3.68, y: 0.68, z: -1.34 },
      size: { x: 0.16, y: 0.72, z: 1.1 },
      color: "#41524b",
    },
  ],
  mechanisms: [
    {
      id: "divine-tilt-ramp",
      name: "Divine Tilt Ramp",
      kind: "tiltRamp",
      position: { x: -0.18, y: 0.44, z: 0.42 },
      size: { x: 1.96, y: 0.22, z: 0.62 },
      yaw: -0.18,
      initialTilt: -0.08,
      minTilt: -0.49,
      maxTilt: 0.49,
      color: "#b79a57",
      handleOffset: { x: 0.82, y: 0.28, z: -0.38 },
    },
  ],
  props: [
    { id: "start-rune", kind: "rune", position: { x: -3.15, y: 0.49, z: 1.42 }, radius: 0.48, color: "#d9b44a" },
    { id: "goal-rune", kind: "rune", position: { x: 3.02, y: 0.51, z: -1.45 }, radius: 0.5, color: "#58c6a7" },
    { id: "left-pillar-a", kind: "pillar", position: { x: -3.93, y: 0.82, z: 2.02 }, radius: 0.13, height: 1.14, color: "#a6b4ac" },
    { id: "left-pillar-b", kind: "pillar", position: { x: -2.37, y: 0.75, z: 1.96 }, radius: 0.11, height: 0.98, color: "#a6b4ac" },
    { id: "mid-pillar-a", kind: "pillar", position: { x: 0.66, y: 0.68, z: 0.42 }, radius: 0.1, height: 0.86, color: "#9baaa2" },
    { id: "mid-pillar-b", kind: "pillar", position: { x: 1.84, y: 0.68, z: 0.42 }, radius: 0.1, height: 0.86, color: "#9baaa2" },
    { id: "goal-pillar-a", kind: "pillar", position: { x: 2.26, y: 0.78, z: -1.98 }, radius: 0.12, height: 1, color: "#9baaa2" },
    { id: "goal-pillar-b", kind: "pillar", position: { x: 3.7, y: 0.78, z: -1.98 }, radius: 0.12, height: 1, color: "#9baaa2" },
    { id: "path-arrow-a", kind: "arrow", position: { x: -2.1, y: 0.39, z: 0.78 }, size: { x: 0.46, y: 0.04, z: 0.18 }, rotation: { y: -0.22 }, color: "#d9b44a" },
    { id: "path-arrow-b", kind: "arrow", position: { x: 1.3, y: 0.33, z: -0.28 }, size: { x: 0.46, y: 0.04, z: 0.18 }, rotation: { y: -0.08 }, color: "#d9b44a" },
    { id: "left-torch", kind: "torch", position: { x: -2.68, y: 0.82, z: 2.07 }, radius: 0.08, height: 0.4, color: "#d46d32" },
    { id: "goal-torch", kind: "torch", position: { x: 3.5, y: 0.82, z: -2.02 }, radius: 0.08, height: 0.4, color: "#58c6a7" },
  ],
};
