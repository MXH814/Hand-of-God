export type LevelBlockKind = "floor" | "platform" | "wall" | "rail" | "hazard" | "ramp";
export type LevelPropKind = "pillar" | "marker" | "arrow" | "bumper";

export interface LevelBlock {
  id: string;
  kind: LevelBlockKind;
  x: number;
  y: number;
  width: number;
  height: number;
  angle?: number;
  color: string;
  height3d?: number;
  physics?: boolean;
  opacity?: number;
}

export interface LevelProp {
  id: string;
  kind: LevelPropKind;
  x: number;
  y: number;
  radius?: number;
  width?: number;
  height?: number;
  angle?: number;
  color: string;
}

export interface GameLevel {
  id: string;
  name: string;
  gravity: { x: number; y: number; z: number };
  start: { x: number; y: number };
  goal: { x: number; y: number; radius: number };
  fallBounds: {
    left: number;
    right: number;
    bottom: number;
  };
  blocks: LevelBlock[];
  props: LevelProp[];
}

export const LEVEL_01: GameLevel = {
  id: "level-01",
  name: "Level 01: Toybox Descent",
  gravity: { x: 0, y: -6.8, z: 0 },
  start: { x: -2.75, y: 1.62 },
  goal: { x: 2.76, y: -1.54, radius: 0.44 },
  fallBounds: {
    left: -3.75,
    right: 3.75,
    bottom: -2.85,
  },
  blocks: [
    { id: "table", kind: "floor", x: 0, y: 0, width: 7.1, height: 4.25, color: "#12221c", height3d: 0.12, physics: false },
    { id: "pit-left", kind: "hazard", x: -1.2, y: -1.28, width: 1.85, height: 0.82, angle: -0.08, color: "#07100d", height3d: 0.03, physics: false, opacity: 0.82 },
    { id: "pit-right", kind: "hazard", x: 1.14, y: 0.02, width: 1.5, height: 0.62, angle: 0.1, color: "#07100d", height3d: 0.03, physics: false, opacity: 0.8 },
    { id: "start-pad", kind: "platform", x: -2.55, y: 1.35, width: 1.25, height: 0.68, angle: -0.2, color: "#0f8b8d", height3d: 0.18 },
    { id: "upper-path", kind: "platform", x: -1.55, y: 0.95, width: 2.05, height: 0.36, angle: -0.2, color: "#dfeee8", height3d: 0.14 },
    { id: "red-bridge", kind: "platform", x: -0.42, y: 0.03, width: 2.2, height: 0.34, angle: 0.23, color: "#d1495b", height3d: 0.15 },
    { id: "blue-run", kind: "platform", x: 1.45, y: -0.72, width: 2.0, height: 0.34, angle: -0.15, color: "#6a7fdb", height3d: 0.15 },
    { id: "goal-pad", kind: "platform", x: 2.45, y: -1.55, width: 1.24, height: 0.78, angle: 0.08, color: "#dfeee8", height3d: 0.14 },
    { id: "lower-catch", kind: "platform", x: 0, y: -2.25, width: 6.55, height: 0.3, angle: 0.01, color: "#e8f0eb", height3d: 0.14 },
    { id: "left-wall", kind: "wall", x: -3.34, y: -0.22, width: 0.24, height: 4.2, color: "#9fb5aa", height3d: 0.46 },
    { id: "right-wall", kind: "wall", x: 3.34, y: -0.22, width: 0.24, height: 4.2, color: "#9fb5aa", height3d: 0.46 },
    { id: "top-rail", kind: "rail", x: -1.75, y: 2.04, width: 3.1, height: 0.18, angle: -0.06, color: "#43aa8b", height3d: 0.38 },
    { id: "mid-rail", kind: "rail", x: 1.16, y: 0.82, width: 0.24, height: 1.18, angle: 0.55, color: "#f7b801", height3d: 0.34 },
    { id: "goal-guard", kind: "rail", x: 2.12, y: -1.02, width: 0.24, height: 0.96, angle: -0.25, color: "#43aa8b", height3d: 0.32 },
    { id: "ramp-a", kind: "ramp", x: -2.42, y: 0.36, width: 0.92, height: 0.24, angle: 0.46, color: "#f7b801", height3d: 0.16 },
    { id: "ramp-b", kind: "ramp", x: 0.58, y: -1.36, width: 1.25, height: 0.24, angle: -0.34, color: "#0f8b8d", height3d: 0.16 },
  ],
  props: [
    { id: "start-ring", kind: "marker", x: -2.75, y: 1.62, radius: 0.42, color: "#f7b801" },
    { id: "left-pillar-a", kind: "pillar", x: -3.0, y: 1.42, radius: 0.13, height: 0.55, color: "#dfeee8" },
    { id: "left-pillar-b", kind: "pillar", x: -2.05, y: 1.68, radius: 0.1, height: 0.45, color: "#dfeee8" },
    { id: "mid-bumper", kind: "bumper", x: 0.52, y: 0.48, radius: 0.18, color: "#f7b801" },
    { id: "goal-pillar-a", kind: "pillar", x: 2.05, y: -1.86, radius: 0.11, height: 0.42, color: "#43aa8b" },
    { id: "goal-pillar-b", kind: "pillar", x: 3.03, y: -1.1, radius: 0.11, height: 0.42, color: "#43aa8b" },
    { id: "path-arrow-a", kind: "arrow", x: -1.5, y: 0.58, width: 0.5, height: 0.16, angle: -0.2, color: "#f7b801" },
    { id: "path-arrow-b", kind: "arrow", x: 1.08, y: -1.15, width: 0.5, height: 0.16, angle: -0.15, color: "#f7b801" },
  ],
};
