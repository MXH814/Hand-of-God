import * as CANNON from "cannon-es";
import * as THREE from "three";
import { LEVEL_01, type GameLevel, type LevelBlock, type LevelProp } from "./gameLevel";
import type { AnalyzedHand, MappedHandPoint, SceneObject, ShapeLibraryItem, ShapeType, Vector2 } from "./types";

interface ShapeControllerOptions {
  stageElement: HTMLElement;
  library: ShapeLibraryItem[];
}

const WORLD_SCALE = 112;
const DEPTH_STEP = 1.35;
const GAME_DEPTH = 12;
const BALL_RADIUS = 0.24;
const HAND_RADIUS = 0.34;

interface GamePlatform {
  body: CANNON.Body;
  mesh: THREE.Mesh;
}

interface GameHandCollider {
  body: CANNON.Body;
  mesh: THREE.Mesh;
  lastPosition: CANNON.Vec3;
  lastTimestamp: number;
}

export interface GameStateSnapshot {
  levelName: string;
  status: "guiding" | "goal" | "resetting" | "fallen";
  won: boolean;
  activeHands: number;
  resetReason?: "manual" | "fallen";
  ball: {
    x: number;
    y: number;
    speed: number;
  };
}

export class ShapeScene {
  private readonly level: GameLevel = LEVEL_01;
  private readonly scene = new THREE.Scene();
  private readonly camera = new THREE.OrthographicCamera(-1, 1, 1, -1, 0.1, 100);
  private readonly renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
  private readonly raycaster = new THREE.Raycaster();
  private readonly pointer = new THREE.Vector2();
  private readonly physicsWorld = new CANNON.World();
  private readonly gameMaterial = new CANNON.Material("game");
  private readonly handMaterial = new CANNON.Material("hand");
  private readonly ballBody = new CANNON.Body({
    mass: 1,
    shape: new CANNON.Sphere(BALL_RADIUS),
    material: this.gameMaterial,
    linearDamping: 0.22,
    angularDamping: 0.18,
  });
  private readonly ballMesh = new THREE.Mesh(
    new THREE.SphereGeometry(BALL_RADIUS, 32, 20),
    new THREE.MeshStandardMaterial({
      color: "#f7b801",
      roughness: 0.38,
      metalness: 0.08,
      emissive: "#6b4d00",
      emissiveIntensity: 0.16,
    }),
  );
  private readonly gamePlatforms: GamePlatform[] = [];
  private readonly levelMeshes: THREE.Object3D[] = [];
  private readonly handColliders = new Map<string, GameHandCollider>();
  private readonly goalMesh = new THREE.Mesh(
    new THREE.TorusGeometry(LEVEL_01.goal.radius, 0.045, 12, 48),
    new THREE.MeshStandardMaterial({
      color: "#43aa8b",
      roughness: 0.4,
      metalness: 0.18,
      emissive: "#0f8b8d",
      emissiveIntensity: 0.28,
    }),
  );
  private readonly goalCore = new THREE.Mesh(
    new THREE.CircleGeometry(LEVEL_01.goal.radius * 0.78, 48),
    new THREE.MeshBasicMaterial({
      color: "#43aa8b",
      transparent: true,
      opacity: 0.2,
      depthWrite: false,
    }),
  );
  private readonly objects = new Map<string, THREE.Mesh>();
  private readonly objectState = new Map<string, SceneObject>();
  private selectedObjectId?: string;
  private activeObjectId?: string;
  private deleteTargetObjectId?: string;
  private animationFrame = 0;
  private lastPhysicsTime = performance.now();
  private gameWon = false;
  private gameStatus: GameStateSnapshot["status"] = "guiding";
  private lastResetAt = 0;
  private lastResetReason: GameStateSnapshot["resetReason"];
  private previewMesh?: THREE.Mesh;
  private nextObjectDepth = 0;
  private transformBase?: {
    objectId: string;
    scale: number;
    rotation: THREE.Euler;
  };
  private singleHandBase?: {
    objectId: string;
    handScale: number;
    rotation: THREE.Euler;
    x: number;
    z: number;
    palmRoll: number;
    palmYaw: number;
  };

  constructor(private readonly options: ShapeControllerOptions) {
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    this.renderer.setClearColor(0x000000, 0);
    this.renderer.shadowMap.enabled = true;
    this.renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    this.renderer.domElement.className = "shape-canvas";
    this.options.stageElement.appendChild(this.renderer.domElement);
    this.camera.position.set(0, 0, 30);
    this.camera.lookAt(0, 0, 0);
    this.addLights();
    this.setupPhysics();
    this.setupGameLevel();
    this.resize();
    this.animate();
  }

  dispose() {
    cancelAnimationFrame(this.animationFrame);
    this.renderer.dispose();
    this.renderer.domElement.remove();
  }

  resetGame(reason: "manual" | "fallen" = "manual") {
    this.gameWon = false;
    this.gameStatus = reason === "fallen" ? "fallen" : "resetting";
    this.lastResetAt = performance.now();
    this.lastResetReason = reason;
    this.ballBody.position.set(this.level.start.x, this.level.start.y, 0);
    this.ballBody.velocity.set(0, 0, 0);
    this.ballBody.angularVelocity.set(0, 0, 0);
    this.ballBody.quaternion.set(0, 0, 0, 1);
    this.ballBody.wakeUp();
  }

  updateGameHands(points: MappedHandPoint[], hands: AnalyzedHand[], timestamp: number) {
    const activeIds = new Set<string>();

    for (const point of points) {
      const hand = hands.find((candidate) => candidate.id === point.handId);
      if (!hand || hand.score < 0.5) {
        continue;
      }

      activeIds.add(point.handId);
      const collider = this.getOrCreateHandCollider(point.handId);
      const worldPoint = this.screenToWorldPoint(point.x, point.y);
      const nextPosition = new CANNON.Vec3(worldPoint.x, worldPoint.y, 0);
      const dt = Math.max((timestamp - collider.lastTimestamp) / 1000, 1 / 120);
      const velocity = nextPosition.vsub(collider.lastPosition).scale(1 / dt);
      const pinchBoost = hand.pinch.active ? 1.32 : 1;

      collider.body.velocity.set(
        clamp(velocity.x * pinchBoost, -8, 8),
        clamp(velocity.y * pinchBoost, -8, 8),
        0,
      );
      collider.body.position.copy(nextPosition);
      collider.body.wakeUp();
      collider.mesh.visible = true;
      collider.mesh.position.set(nextPosition.x, nextPosition.y, GAME_DEPTH + 0.7);
      collider.mesh.scale.setScalar(hand.pinch.active ? 1.18 : 1);
      collider.lastPosition.copy(nextPosition);
      collider.lastTimestamp = timestamp;
    }

    for (const [id, collider] of this.handColliders.entries()) {
      if (activeIds.has(id)) {
        continue;
      }
      collider.mesh.visible = false;
      collider.body.position.set(30, 30, 0);
      collider.body.velocity.set(0, 0, 0);
    }
  }

  getGameState(): GameStateSnapshot {
    const recentReset = performance.now() - this.lastResetAt < 900;
    return {
      levelName: this.level.name,
      status: recentReset ? this.gameStatus : this.gameWon ? "goal" : "guiding",
      won: this.gameWon,
      activeHands: [...this.handColliders.values()].filter((collider) => collider.mesh.visible).length,
      resetReason: recentReset ? this.lastResetReason : undefined,
      ball: {
        x: this.ballBody.position.x,
        y: this.ballBody.position.y,
        speed: this.ballBody.velocity.length(),
      },
    };
  }

  resize() {
    const rect = this.options.stageElement.getBoundingClientRect();
    const width = Math.max(1, Math.floor(rect.width));
    const height = Math.max(1, Math.floor(rect.height));
    const worldWidth = width / WORLD_SCALE;
    const worldHeight = height / WORLD_SCALE;

    this.camera.left = -worldWidth / 2;
    this.camera.right = worldWidth / 2;
    this.camera.top = worldHeight / 2;
    this.camera.bottom = -worldHeight / 2;
    this.camera.updateProjectionMatrix();
    this.renderer.setSize(width, height, false);
  }

  screenToWorldPoint(clientX: number, clientY: number): Vector2 {
    const rect = this.options.stageElement.getBoundingClientRect();
    return {
      x: (clientX - rect.left - rect.width / 2) / WORLD_SCALE,
      y: -(clientY - rect.top - rect.height / 2) / WORLD_SCALE,
    };
  }

  addShapeAtScreenPoint(type: ShapeType, clientX: number, clientY: number) {
    return this.addShape(type, this.screenToWorldPoint(clientX, clientY));
  }

  addShape(type: ShapeType, point: Vector2) {
    const item = this.options.library.find((shape) => shape.type === type);
    if (!item) {
      return undefined;
    }

    const mesh = this.createMesh(item);
    const id = `${type}-${Date.now()}`;
    const z = this.nextObjectDepth;
    this.nextObjectDepth += DEPTH_STEP;
    mesh.position.set(point.x, point.y, z);
    mesh.scale.setScalar(item.defaultScale);
    mesh.renderOrder = Math.round(z * 1000);
    mesh.userData.objectId = id;
    this.scene.add(mesh);
    this.objects.set(id, mesh);
    this.objectState.set(id, {
      id,
      type,
      position: { x: point.x, y: point.y, z },
      rotation: { x: mesh.rotation.x, y: mesh.rotation.y, z: mesh.rotation.z },
      scale: item.defaultScale,
      selected: true,
    });
    this.selectObject(id);

    return id;
  }

  setPreviewAtScreenPoint(type: ShapeType | undefined, clientX?: number, clientY?: number) {
    if (!type || clientX === undefined || clientY === undefined) {
      this.setPreview(undefined);
      return;
    }

    this.setPreview(type, this.screenToWorldPoint(clientX, clientY));
  }

  setPreview(type: ShapeType | undefined, point?: Vector2) {
    if (!type || !point) {
      if (this.previewMesh) {
        this.scene.remove(this.previewMesh);
        this.previewMesh.geometry.dispose();
        disposeMaterial(this.previewMesh.material);
        this.previewMesh = undefined;
      }
      return;
    }

    const item = this.options.library.find((shape) => shape.type === type);
    if (!item) {
      return;
    }

    if (!this.previewMesh || this.previewMesh.userData.type !== type) {
      this.setPreview(undefined);
      this.previewMesh = this.createMesh(item, true);
      this.previewMesh.userData.type = type;
      this.scene.add(this.previewMesh);
    }

    this.previewMesh.position.set(point.x, point.y, this.nextObjectDepth + 0.16);
    this.previewMesh.renderOrder = Math.round((this.nextObjectDepth + 0.16) * 1000);
  }

  applyTransformAtScreenPoint(
    clientX: number,
    clientY: number,
    scaleDelta: number,
    rotationDelta: number,
    rotationXDelta = 0,
    rotationYDelta = 0,
  ) {
    this.applyTransform(
      this.screenToWorldPoint(clientX, clientY),
      scaleDelta,
      rotationDelta,
      rotationXDelta,
      rotationYDelta,
    );
  }

  beginTransform() {
    const mesh = this.getSelectedMesh();
    const objectId = this.selectedObjectId;
    if (!mesh || !objectId) {
      this.transformBase = undefined;
      return;
    }

    this.transformBase = {
      objectId,
      scale: mesh.scale.x,
      rotation: mesh.rotation.clone(),
    };
    this.setActiveObject(objectId);
  }

  endTransform() {
    this.transformBase = undefined;
    this.setActiveObject(undefined);
  }

  beginSingleHandTransform(point: MappedHandPoint) {
    const mesh = this.getSelectedMesh();
    const objectId = this.selectedObjectId;
    if (!mesh || !objectId) {
      this.singleHandBase = undefined;
      return;
    }

    this.singleHandBase = {
      objectId,
      handScale: Math.max(point.handScale, 0.001),
      rotation: mesh.rotation.clone(),
      x: point.x,
      z: point.z,
      palmRoll: point.palmRoll,
      palmYaw: point.palmYaw,
    };
    this.setActiveObject(objectId);
  }

  endSingleHandTransform() {
    this.singleHandBase = undefined;
    this.setActiveObject(undefined);
  }

  applyTransform(
    center: Vector2,
    scaleDelta: number,
    rotationDelta: number,
    rotationXDelta = 0,
    rotationYDelta = 0,
  ) {
    const mesh = this.getSelectedMesh();
    if (!mesh) {
      return;
    }

    const base =
      this.transformBase && this.transformBase.objectId === this.selectedObjectId
        ? this.transformBase
        : { scale: mesh.scale.x, rotation: mesh.rotation };
    const currentWorld = new THREE.Vector2(mesh.position.x, mesh.position.y);
    currentWorld.lerp(new THREE.Vector2(center.x, center.y), 0.58);
    mesh.position.set(currentWorld.x, currentWorld.y, mesh.position.z);
    const nextScale = clamp(base.scale * scaleDelta, 0.35, 3.4);
    mesh.scale.setScalar(nextScale);
    mesh.rotation.x = smoothAngle(mesh.rotation.x, base.rotation.x + rotationXDelta, 0.42);
    mesh.rotation.y = smoothAngle(mesh.rotation.y, base.rotation.y + rotationYDelta, 0.42);
    mesh.rotation.z = smoothAngle(mesh.rotation.z, base.rotation.z + rotationDelta, 0.5);
    this.syncState(mesh);
  }

  moveSelectedAtScreenPoint(clientX: number, clientY: number) {
    const mesh = this.getSelectedMesh();
    if (!mesh) {
      return false;
    }

    const point = this.screenToWorldPoint(clientX, clientY);
    const currentWorld = new THREE.Vector2(mesh.position.x, mesh.position.y);
    currentWorld.lerp(new THREE.Vector2(point.x, point.y), 0.55);
    mesh.position.set(currentWorld.x, currentWorld.y, mesh.position.z);
    this.syncState(mesh);
    return true;
  }

  moveSelectedByHandPoint(point: MappedHandPoint) {
    const mesh = this.getSelectedMesh();
    if (!mesh) {
      return false;
    }

    const moved = this.moveSelectedAtScreenPoint(point.x, point.y);
    if (!moved) {
      return false;
    }

    if (this.singleHandBase && this.singleHandBase.objectId === this.selectedObjectId) {
      const depthByScale = Math.log(Math.max(point.handScale, 0.001) / this.singleHandBase.handScale);
      const depthByZ = this.singleHandBase.z - point.z;
      const rollDelta = normalizeAngle(point.palmRoll - this.singleHandBase.palmRoll);
      const yawDelta = point.palmYaw - this.singleHandBase.palmYaw;
      const lateralNudge = (point.x - this.singleHandBase.x) / 360;
      const depthRotation = clamp(depthByScale * 4.6 + depthByZ * 6.2, -1.65, 1.65);
      const yawRotation = clamp(yawDelta * 2.25 + lateralNudge, -1.75, 1.75);
      const rollRotation = clamp(rollDelta * 1.15, -1.55, 1.55);

      mesh.rotation.x = smoothAngle(mesh.rotation.x, this.singleHandBase.rotation.x + depthRotation, 0.46);
      mesh.rotation.y = smoothAngle(mesh.rotation.y, this.singleHandBase.rotation.y + yawRotation, 0.5);
      mesh.rotation.z = smoothAngle(mesh.rotation.z, this.singleHandBase.rotation.z + rollRotation, 0.48);
      this.syncState(mesh);
    }

    return true;
  }

  selectAtScreenPoint(clientX: number, clientY: number) {
    const id = this.getHitObjectId(clientX, clientY);
    if (id) {
      this.selectObject(id);
    }
    return id;
  }

  selectAt(clientX: number, clientY: number) {
    return this.selectAtScreenPoint(clientX, clientY);
  }

  deleteFrontmostAtScreenPoint(clientX: number, clientY: number) {
    const id = this.getHitObjectId(clientX, clientY);
    return this.deleteObject(id);
  }

  getFrontmostObjectAtScreenPoint(clientX: number, clientY: number) {
    return this.getHitObjectId(clientX, clientY);
  }

  deleteObject(id: string | undefined) {
    if (!id) {
      return undefined;
    }

    const mesh = this.objects.get(id);
    if (!mesh) {
      return undefined;
    }

    this.scene.remove(mesh);
    mesh.geometry.dispose();
    disposeMaterial(mesh.material);
    this.objects.delete(id);
    this.objectState.delete(id);
    if (this.selectedObjectId === id) {
      this.selectedObjectId = undefined;
      this.transformBase = undefined;
      this.singleHandBase = undefined;
    }
    if (this.activeObjectId === id) {
      this.activeObjectId = undefined;
    }
    if (this.deleteTargetObjectId === id) {
      this.deleteTargetObjectId = undefined;
    }

    return id;
  }

  setActiveObject(id: string | undefined) {
    this.activeObjectId = id;
    this.updateMaterialStates();
  }

  setDeleteTargetObject(id: string | undefined) {
    this.deleteTargetObjectId = id;
    this.updateMaterialStates();
  }

  getSceneObjects(): SceneObject[] {
    return [...this.objectState.values()];
  }

  private getHitObjectId(clientX: number, clientY: number) {
    const rect = this.renderer.domElement.getBoundingClientRect();
    this.pointer.x = ((clientX - rect.left) / rect.width) * 2 - 1;
    this.pointer.y = -(((clientY - rect.top) / rect.height) * 2 - 1);
    this.raycaster.setFromCamera(this.pointer, this.camera);
    const hits = this.raycaster.intersectObjects([...this.objects.values()]);
    return hits[0]?.object.userData.objectId as string | undefined;
  }

  private selectObject(id: string) {
    this.selectedObjectId = id;
    for (const [objectId, mesh] of this.objects.entries()) {
      const selected = objectId === id;
      const state = this.objectState.get(objectId);
      if (state) {
        state.selected = selected;
      }
      const material = mesh.material;
      if (material instanceof THREE.MeshStandardMaterial) {
        setMaterialHighlight(material, selected, objectId === this.activeObjectId, objectId === this.deleteTargetObjectId);
      }
    }
  }

  private updateMaterialStates() {
    for (const [objectId, mesh] of this.objects.entries()) {
      const material = mesh.material;
      const state = this.objectState.get(objectId);
      if (material instanceof THREE.MeshStandardMaterial) {
        setMaterialHighlight(
          material,
          Boolean(state?.selected),
          objectId === this.activeObjectId,
          objectId === this.deleteTargetObjectId,
        );
      }
    }
  }

  private getSelectedMesh() {
    return this.selectedObjectId ? this.objects.get(this.selectedObjectId) : undefined;
  }

  private syncState(mesh: THREE.Mesh) {
    const id = mesh.userData.objectId as string | undefined;
    if (!id) {
      return;
    }

    const state = this.objectState.get(id);
    if (!state) {
      return;
    }

    state.position = { x: mesh.position.x, y: mesh.position.y, z: mesh.position.z };
    state.rotation = { x: mesh.rotation.x, y: mesh.rotation.y, z: mesh.rotation.z };
    state.scale = mesh.scale.x;
  }

  private createMesh(item: ShapeLibraryItem, preview = false) {
    const material = new THREE.MeshStandardMaterial({
      color: item.color,
      roughness: 0.45,
      metalness: 0.14,
      transparent: preview,
      opacity: preview ? 0.55 : 1,
    });
    const mesh = new THREE.Mesh(createGeometry(item.type), material);
    mesh.castShadow = false;
    mesh.receiveShadow = false;
    return mesh;
  }

  private addLights() {
    const ambient = new THREE.AmbientLight("#ffffff", 0.88);
    const key = new THREE.DirectionalLight("#ffffff", 1.1);
    key.position.set(2, 3, 5);
    key.castShadow = true;
    key.shadow.mapSize.set(1024, 1024);
    this.scene.add(ambient, key);
  }

  private setupPhysics() {
    this.physicsWorld.gravity.set(this.level.gravity.x, this.level.gravity.y, this.level.gravity.z);
    this.physicsWorld.allowSleep = true;
    this.physicsWorld.addContactMaterial(
      new CANNON.ContactMaterial(this.gameMaterial, this.gameMaterial, {
        friction: 0.42,
        restitution: 0.18,
      }),
    );
    this.physicsWorld.addContactMaterial(
      new CANNON.ContactMaterial(this.gameMaterial, this.handMaterial, {
        friction: 0.12,
        restitution: 0.08,
      }),
    );
    this.ballBody.position.set(this.level.start.x, this.level.start.y, 0);
    this.physicsWorld.addBody(this.ballBody);
    this.ballMesh.castShadow = false;
    this.ballMesh.receiveShadow = false;
    this.ballMesh.renderOrder = GAME_DEPTH * 1000 + 10;
    this.scene.add(this.ballMesh);
  }

  private setupGameLevel() {
    this.goalMesh.position.set(this.level.goal.x, this.level.goal.y, GAME_DEPTH + 0.16);
    this.goalMesh.renderOrder = GAME_DEPTH * 1000;
    this.goalCore.position.copy(this.goalMesh.position);
    this.goalCore.renderOrder = GAME_DEPTH * 1000 - 1;
    this.goalCore.rotation.z = -0.08;
    this.scene.add(this.goalCore, this.goalMesh);

    for (const block of this.level.blocks) {
      this.addLevelBlock(block);
    }

    for (const prop of this.level.props) {
      this.addLevelProp(prop);
    }
  }

  private addLevelBlock(block: LevelBlock) {
    const blockHeight = block.height3d ?? 0.14;
    const mesh = new THREE.Mesh(
      new THREE.BoxGeometry(block.width, block.height, blockHeight),
      new THREE.MeshStandardMaterial({
        color: block.color,
        roughness: block.kind === "floor" ? 0.74 : 0.5,
        metalness: block.kind === "hazard" ? 0.04 : 0.1,
        transparent: block.opacity !== undefined,
        opacity: block.opacity ?? 1,
      }),
    );
    mesh.position.set(block.x, block.y, GAME_DEPTH + blockHeight / 2 - 0.08);
    mesh.rotation.z = block.angle ?? 0;
    mesh.renderOrder = block.kind === "floor" || block.kind === "hazard" ? GAME_DEPTH * 1000 - 4 : GAME_DEPTH * 1000;
    mesh.receiveShadow = true;
    mesh.castShadow = block.kind !== "floor" && block.kind !== "hazard";
    this.scene.add(mesh);
    this.levelMeshes.push(mesh);

    if (block.kind === "hazard") {
      const rim = new THREE.Mesh(
        new THREE.BoxGeometry(block.width + 0.08, block.height + 0.08, 0.025),
        new THREE.MeshBasicMaterial({
          color: "#d1495b",
          transparent: true,
          opacity: 0.32,
          depthWrite: false,
        }),
      );
      rim.position.set(block.x, block.y, GAME_DEPTH + 0.02);
      rim.rotation.z = block.angle ?? 0;
      rim.renderOrder = GAME_DEPTH * 1000 - 3;
      this.scene.add(rim);
      this.levelMeshes.push(rim);
    }

    if (block.physics === false || block.kind === "floor" || block.kind === "hazard") {
      return;
    }

    const halfExtents = new CANNON.Vec3(block.width / 2, block.height / 2, 0.28);
    const body = new CANNON.Body({
      mass: 0,
      shape: new CANNON.Box(halfExtents),
      material: this.gameMaterial,
    });
    body.position.set(block.x, block.y, 0);
    body.quaternion.setFromEuler(0, 0, block.angle ?? 0);
    this.physicsWorld.addBody(body);
    this.gamePlatforms.push({ body, mesh });
  }

  private addLevelProp(prop: LevelProp) {
    const mesh = createPropMesh(prop);
    mesh.position.set(prop.x, prop.y, GAME_DEPTH + 0.25);
    mesh.rotation.z = prop.angle ?? 0;
    mesh.renderOrder = GAME_DEPTH * 1000 + 4;
    mesh.castShadow = true;
    mesh.receiveShadow = true;
    this.scene.add(mesh);
    this.levelMeshes.push(mesh);

    if (prop.kind !== "bumper") {
      return;
    }

    const body = new CANNON.Body({
      mass: 0,
      shape: new CANNON.Sphere(prop.radius ?? 0.18),
      material: this.gameMaterial,
    });
    body.position.set(prop.x, prop.y, 0);
    this.physicsWorld.addBody(body);
  }

  private getOrCreateHandCollider(id: string) {
    const existing = this.handColliders.get(id);
    if (existing) {
      return existing;
    }

    const body = new CANNON.Body({
      mass: 0,
      type: CANNON.Body.KINEMATIC,
      shape: new CANNON.Sphere(HAND_RADIUS),
      material: this.handMaterial,
    });
    body.position.set(30, 30, 0);
    this.physicsWorld.addBody(body);

    const mesh = new THREE.Mesh(
      new THREE.SphereGeometry(HAND_RADIUS, 24, 16),
      new THREE.MeshBasicMaterial({
        color: "#ffffff",
        transparent: true,
        opacity: 0.28,
        depthWrite: false,
      }),
    );
    mesh.visible = false;
    mesh.renderOrder = GAME_DEPTH * 1000 + 30;
    this.scene.add(mesh);

    const collider = {
      body,
      mesh,
      lastPosition: body.position.clone(),
      lastTimestamp: performance.now(),
    };
    this.handColliders.set(id, collider);
    return collider;
  }

  private stepGame() {
    const now = performance.now();
    const delta = Math.min((now - this.lastPhysicsTime) / 1000, 1 / 30);
    this.lastPhysicsTime = now;
    this.physicsWorld.step(1 / 60, delta, 3);

    this.ballBody.position.z = 0;
    this.ballBody.velocity.z = 0;
    if (
      this.ballBody.position.y < this.level.fallBounds.bottom ||
      this.ballBody.position.x < this.level.fallBounds.left ||
      this.ballBody.position.x > this.level.fallBounds.right
    ) {
      this.resetGame("fallen");
    }

    const goalDistance = Math.hypot(
      this.ballBody.position.x - this.level.goal.x,
      this.ballBody.position.y - this.level.goal.y,
    );
    if (!this.gameWon && goalDistance < this.level.goal.radius && this.ballBody.velocity.length() < 2.8) {
      this.gameWon = true;
      this.gameStatus = "goal";
      this.goalCore.material.opacity = 0.42;
    } else if (!this.gameWon) {
      this.gameStatus = "guiding";
      this.goalCore.material.opacity = 0.2;
    }

    this.ballMesh.position.set(this.ballBody.position.x, this.ballBody.position.y, GAME_DEPTH + 0.36);
    this.ballMesh.quaternion.copy(this.ballBody.quaternion as unknown as THREE.Quaternion);
  }

  private animate = () => {
    this.animationFrame = requestAnimationFrame(this.animate);
    this.stepGame();
    for (const mesh of this.objects.values()) {
      if (mesh.userData.objectId !== this.selectedObjectId) {
        mesh.rotation.y += 0.002;
      }
    }
    this.renderer.render(this.scene, this.camera);
  };
}

function createGeometry(type: ShapeType) {
  switch (type) {
    case "cube":
      return new THREE.BoxGeometry(1.1, 1.1, 1.1);
    case "sphere":
      return new THREE.SphereGeometry(0.72, 36, 24);
    case "cylinder":
      return new THREE.CylinderGeometry(0.55, 0.55, 1.4, 36);
    case "cone":
      return new THREE.ConeGeometry(0.62, 1.45, 36);
    case "torus":
      return new THREE.TorusGeometry(0.58, 0.18, 18, 48);
  }
}

function createPropMesh(prop: LevelProp) {
  if (prop.kind === "pillar" || prop.kind === "bumper") {
    const radius = prop.radius ?? 0.12;
    const height = prop.height ?? 0.38;
    const material = new THREE.MeshStandardMaterial({
      color: prop.color,
      roughness: 0.42,
      metalness: prop.kind === "bumper" ? 0.18 : 0.08,
      emissive: prop.kind === "bumper" ? prop.color : "#000000",
      emissiveIntensity: prop.kind === "bumper" ? 0.12 : 0,
    });
    const mesh = new THREE.Mesh(new THREE.CylinderGeometry(radius, radius, height, 24), material);
    mesh.rotation.x = Math.PI / 2;
    return mesh;
  }

  if (prop.kind === "marker") {
    return new THREE.Mesh(
      new THREE.RingGeometry((prop.radius ?? 0.38) * 0.72, prop.radius ?? 0.38, 40),
      new THREE.MeshBasicMaterial({
        color: prop.color,
        transparent: true,
        opacity: 0.7,
        depthWrite: false,
      }),
    );
  }

  const group = new THREE.Group();
  const width = prop.width ?? 0.48;
  const height = prop.height ?? 0.16;
  const material = new THREE.MeshBasicMaterial({
    color: prop.color,
    transparent: true,
    opacity: 0.82,
    depthWrite: false,
  });
  const stem = new THREE.Mesh(new THREE.BoxGeometry(width * 0.62, height * 0.42, 0.035), material);
  const triangle = new THREE.Shape();
  triangle.moveTo(width * 0.2, -height * 0.8);
  triangle.lineTo(width * 0.2, height * 0.8);
  triangle.lineTo(width * 0.52, 0);
  triangle.closePath();
  const head = new THREE.Mesh(new THREE.ShapeGeometry(triangle), material);
  head.position.x = width * 0.36;
  group.add(stem, head);
  return group;
}

function disposeMaterial(material: THREE.Material | THREE.Material[]) {
  if (Array.isArray(material)) {
    material.forEach((item) => item.dispose());
    return;
  }
  material.dispose();
}

function setMaterialHighlight(
  material: THREE.MeshStandardMaterial,
  selected: boolean,
  active: boolean,
  deleteTarget: boolean,
) {
  if (deleteTarget) {
    material.emissive.set("#d1495b");
    material.emissiveIntensity = 0.9;
    return;
  }
  if (active) {
    material.emissive.set("#f7b801");
    material.emissiveIntensity = 0.75;
    return;
  }
  if (selected) {
    material.emissive.set("#2f4f46");
    material.emissiveIntensity = 0.55;
    return;
  }
  material.emissive.set("#000000");
  material.emissiveIntensity = 0;
}

function clamp(value: number, min: number, max: number) {
  return Math.min(Math.max(value, min), max);
}

function smoothAngle(current: number, target: number, alpha: number) {
  return current + normalizeAngle(target - current) * alpha;
}

function normalizeAngle(angle: number) {
  let normalized = angle;
  while (normalized > Math.PI) normalized -= Math.PI * 2;
  while (normalized < -Math.PI) normalized += Math.PI * 2;
  return normalized;
}
