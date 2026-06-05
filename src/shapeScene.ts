import * as CANNON from "cannon-es";
import * as THREE from "three";
import { LEVEL_01, type GameLevel, type LevelBlock, type LevelMechanism, type LevelProp } from "./gameLevel";
import type { MappedHandPoint, SceneObject, ShapeLibraryItem, ShapeType, Vector2 } from "./types";

interface ShapeControllerOptions {
  stageElement: HTMLElement;
  library: ShapeLibraryItem[];
}

const WORLD_SCALE = 72;
const DEBUG_OBJECT_DEPTH = 3.8;
const DEPTH_STEP = 0.32;
const BALL_RADIUS = 0.22;
const MECHANISM_HIT_RADIUS = 150;
const STONE_TOP = "#9ba9a0";
const STONE_SIDE = "#56665e";
const TRIM = "#c0a15b";

interface LevelBody {
  body: CANNON.Body;
  mesh: THREE.Object3D;
}

interface TiltMechanism {
  config: LevelMechanism;
  body: CANNON.Body;
  mesh: THREE.Group;
  rampMesh: THREE.Mesh;
  handle: THREE.Group;
  arc: THREE.Group;
  hotspot: THREE.Group;
  tilt: number;
  selected: boolean;
  hinted: boolean;
}

interface MechanismControl {
  id: string;
  baseTilt: number;
  baseRoll: number;
}

export interface GameStateSnapshot {
  levelName: string;
  status: "waiting" | "guiding" | "goal" | "resetting" | "fallen";
  won: boolean;
  active: boolean;
  activeHands: number;
  activeMechanism?: string;
  resetReason?: "manual" | "fallen";
  ball: {
    x: number;
    y: number;
    z: number;
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
  private readonly debugPlane = new THREE.Plane(new THREE.Vector3(0, 1, 0), -1.15);
  private readonly planeHit = new THREE.Vector3();
  private readonly physicsWorld = new CANNON.World();
  private readonly gameMaterial = new CANNON.Material("temple-stone");
  private readonly ballMaterial = new CANNON.Material("temple-ball");
  private readonly ballBody = new CANNON.Body({
    mass: 1,
    shape: new CANNON.Sphere(BALL_RADIUS),
    material: this.ballMaterial,
    linearDamping: 0.16,
    angularDamping: 0.18,
  });
  private readonly ballMesh = new THREE.Mesh(
    new THREE.SphereGeometry(BALL_RADIUS, 36, 24),
    new THREE.MeshStandardMaterial({
      color: "#d9a325",
      roughness: 0.28,
      metalness: 0.18,
      emissive: "#5f3f00",
      emissiveIntensity: 0.14,
    }),
  );
  private readonly goalRing = new THREE.Mesh(
    new THREE.TorusGeometry(LEVEL_01.goal.radius, 0.055, 16, 72),
    new THREE.MeshStandardMaterial({
      color: "#58c6a7",
      roughness: 0.32,
      metalness: 0.18,
      emissive: "#1c9e88",
      emissiveIntensity: 0.5,
    }),
  );
  private readonly goalCore = new THREE.Mesh(
    new THREE.CylinderGeometry(LEVEL_01.goal.radius * 0.82, LEVEL_01.goal.radius * 0.82, 0.035, 72),
    new THREE.MeshBasicMaterial({
      color: "#58c6a7",
      transparent: true,
      opacity: 0.26,
      depthWrite: false,
    }),
  );
  private readonly levelBodies: LevelBody[] = [];
  private readonly mechanisms = new Map<string, TiltMechanism>();
  private readonly objects = new Map<string, THREE.Mesh>();
  private readonly objectState = new Map<string, SceneObject>();
  private selectedObjectId?: string;
  private activeObjectId?: string;
  private deleteTargetObjectId?: string;
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
  private mechanismControl?: MechanismControl;
  private animationFrame = 0;
  private lastPhysicsTime = performance.now();
  private gameActive = false;
  private gameWon = false;
  private gameStatus: GameStateSnapshot["status"] = "guiding";
  private lastResetAt = 0;
  private lastResetReason: GameStateSnapshot["resetReason"];

  constructor(private readonly options: ShapeControllerOptions) {
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    this.renderer.setClearColor(0x000000, 0);
    this.renderer.shadowMap.enabled = true;
    this.renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    this.renderer.domElement.className = "shape-canvas";
    this.options.stageElement.appendChild(this.renderer.domElement);
    this.scene.background = null;
    this.camera.up.set(0, 1, 0);
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
    this.ballBody.position.set(this.level.start.x, this.level.start.y, this.level.start.z);
    this.ballBody.velocity.set(0, 0, 0);
    this.ballBody.angularVelocity.set(0, 0, 0);
    this.ballBody.quaternion.set(0, 0, 0, 1);
    this.ballBody.sleep();
    if (this.gameActive) {
      this.ballBody.wakeUp();
    }
  }

  setGameActive(active: boolean) {
    if (this.gameActive === active) {
      return;
    }

    this.gameActive = active;
    this.lastPhysicsTime = performance.now();
    this.endMechanismControl();
    if (active) {
      this.resetGame("manual");
      this.ballBody.wakeUp();
      return;
    }

    this.ballBody.sleep();
  }

  beginMechanismControl(point: MappedHandPoint) {
    const mechanism = this.getMechanismNearScreenPoint(point.x, point.y);
    if (!mechanism) {
      return false;
    }

    this.mechanismControl = {
      id: mechanism.config.id,
      baseTilt: mechanism.tilt,
      baseRoll: point.palmRoll,
    };
    this.setMechanismSelected(mechanism.config.id);
    this.gameStatus = "guiding";
    return true;
  }

  updateMechanismControl(point: MappedHandPoint) {
    if (!this.mechanismControl) {
      return false;
    }

    const mechanism = this.mechanisms.get(this.mechanismControl.id);
    if (!mechanism) {
      return false;
    }

    const rollDelta = normalizeAngle(point.palmRoll - this.mechanismControl.baseRoll);
    const nextTilt = clamp(
      this.mechanismControl.baseTilt + rollDelta * 0.82,
      mechanism.config.minTilt,
      mechanism.config.maxTilt,
    );
    this.setMechanismTilt(mechanism, nextTilt);
    return true;
  }

  endMechanismControl() {
    this.mechanismControl = undefined;
    this.setMechanismSelected(undefined);
  }

  updateMechanismHints(points: MappedHandPoint[]) {
    if (this.mechanismControl) {
      return;
    }

    const nearest = this.getNearestMechanism(points);
    this.setMechanismHinted(nearest?.config.id);
  }

  getGameState(): GameStateSnapshot {
    const recentReset = performance.now() - this.lastResetAt < 900;
    return {
      levelName: this.level.name,
      status: !this.gameActive ? "waiting" : recentReset ? this.gameStatus : this.gameWon ? "goal" : "guiding",
      won: this.gameWon,
      active: this.gameActive,
      activeHands: this.mechanismControl ? 1 : 0,
      activeMechanism: this.mechanismControl?.id,
      resetReason: recentReset ? this.lastResetReason : undefined,
      ball: {
        x: this.ballBody.position.x,
        y: this.ballBody.position.y,
        z: this.ballBody.position.z,
        speed: this.ballBody.velocity.length(),
      },
    };
  }

  resize() {
    const rect = this.options.stageElement.getBoundingClientRect();
    const width = Math.max(1, Math.floor(rect.width));
    const height = Math.max(1, Math.floor(rect.height));
    const worldHeight = height / WORLD_SCALE;
    const worldWidth = worldHeight * (width / height);

    this.camera.left = -worldWidth / 2;
    this.camera.right = worldWidth / 2;
    this.camera.top = worldHeight / 2;
    this.camera.bottom = -worldHeight / 2;
    this.camera.updateProjectionMatrix();
    this.camera.position.set(5.7, 7.8, 7.6);
    this.camera.lookAt(0, 0.15, 0);
    this.renderer.setSize(width, height, false);
  }

  screenToWorldPoint(clientX: number, clientY: number): Vector2 {
    const hit = this.screenToGround(clientX, clientY);
    if (hit) {
      return {
        x: hit.x,
        y: hit.z,
      };
    }

    return { x: 0, y: 0 };
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
    const y = DEBUG_OBJECT_DEPTH + this.nextObjectDepth;
    this.nextObjectDepth += DEPTH_STEP;
    mesh.position.set(point.x, y, point.y);
    mesh.scale.setScalar(item.defaultScale);
    mesh.userData.objectId = id;
    this.scene.add(mesh);
    this.objects.set(id, mesh);
    this.objectState.set(id, {
      id,
      type,
      position: { x: point.x, y, z: point.y },
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

    this.previewMesh.position.set(point.x, DEBUG_OBJECT_DEPTH + this.nextObjectDepth + 0.12, point.y);
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

  applyTransform(center: Vector2, scaleDelta: number, rotationDelta: number, rotationXDelta = 0, rotationYDelta = 0) {
    const mesh = this.getSelectedMesh();
    if (!mesh) {
      return;
    }

    const base =
      this.transformBase && this.transformBase.objectId === this.selectedObjectId
        ? this.transformBase
        : { scale: mesh.scale.x, rotation: mesh.rotation };
    const currentWorld = new THREE.Vector2(mesh.position.x, mesh.position.z);
    currentWorld.lerp(new THREE.Vector2(center.x, center.y), 0.58);
    mesh.position.set(currentWorld.x, mesh.position.y, currentWorld.y);
    const nextScale = clamp(base.scale * scaleDelta, 0.35, 3.4);
    mesh.scale.setScalar(nextScale);
    mesh.rotation.x = smoothAngle(mesh.rotation.x, base.rotation.x + rotationXDelta, 0.42);
    mesh.rotation.y = smoothAngle(mesh.rotation.y, base.rotation.y + rotationDelta, 0.5);
    mesh.rotation.z = smoothAngle(mesh.rotation.z, base.rotation.z + rotationYDelta, 0.42);
    this.syncState(mesh);
  }

  moveSelectedAtScreenPoint(clientX: number, clientY: number) {
    const mesh = this.getSelectedMesh();
    if (!mesh) {
      return false;
    }

    const point = this.screenToWorldPoint(clientX, clientY);
    const currentWorld = new THREE.Vector2(mesh.position.x, mesh.position.z);
    currentWorld.lerp(new THREE.Vector2(point.x, point.y), 0.55);
    mesh.position.set(currentWorld.x, mesh.position.y, currentWorld.y);
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
      mesh.rotation.y = smoothAngle(mesh.rotation.y, this.singleHandBase.rotation.y + rollRotation, 0.48);
      mesh.rotation.z = smoothAngle(mesh.rotation.z, this.singleHandBase.rotation.z + yawRotation, 0.5);
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
    mesh.castShadow = true;
    mesh.receiveShadow = true;
    return mesh;
  }

  private addLights() {
    const ambient = new THREE.AmbientLight("#dbe6df", 0.58);
    const key = new THREE.DirectionalLight("#ffffff", 2.6);
    key.position.set(-2.8, 6.8, 4.6);
    key.castShadow = true;
    key.shadow.mapSize.set(2048, 2048);
    key.shadow.camera.left = -6;
    key.shadow.camera.right = 6;
    key.shadow.camera.top = 5;
    key.shadow.camera.bottom = -5;
    const fill = new THREE.DirectionalLight("#7cc7b6", 0.62);
    fill.position.set(3.5, 3, -3);
    this.scene.add(ambient, key, fill);
  }

  private setupPhysics() {
    this.physicsWorld.gravity.set(this.level.gravity.x, this.level.gravity.y, this.level.gravity.z);
    this.physicsWorld.allowSleep = true;
    this.physicsWorld.defaultContactMaterial.friction = 0.46;
    this.physicsWorld.defaultContactMaterial.restitution = 0.08;
    this.physicsWorld.addContactMaterial(
      new CANNON.ContactMaterial(this.ballMaterial, this.gameMaterial, {
        friction: 0.52,
        restitution: 0.08,
      }),
    );
    this.ballBody.position.set(this.level.start.x, this.level.start.y, this.level.start.z);
    this.physicsWorld.addBody(this.ballBody);
    this.ballMesh.castShadow = true;
    this.ballMesh.receiveShadow = true;
    this.scene.add(this.ballMesh);
  }

  private setupGameLevel() {
    for (const block of this.level.blocks) {
      this.addLevelBlock(block);
    }

    for (const mechanism of this.level.mechanisms) {
      this.addTiltMechanism(mechanism);
    }

    for (const prop of this.level.props) {
      this.addLevelProp(prop);
    }

    this.goalRing.position.set(this.level.goal.x, this.level.goal.y + 0.035, this.level.goal.z);
    this.goalRing.rotation.x = Math.PI / 2;
    this.goalCore.position.copy(this.goalRing.position);
    this.goalCore.rotation.x = Math.PI / 2;
    this.scene.add(this.goalCore, this.goalRing);
  }

  private addLevelBlock(block: LevelBlock) {
    const mesh = this.createStoneBlock(block);
    this.scene.add(mesh);

    if (block.physics === false || block.kind === "void") {
      return;
    }

    const body = new CANNON.Body({
      mass: 0,
      shape: new CANNON.Box(new CANNON.Vec3(block.size.x / 2, block.size.y / 2, block.size.z / 2)),
      material: this.gameMaterial,
    });
    body.position.set(block.position.x, block.position.y, block.position.z);
    body.quaternion.copy(toCannonQuaternion(block.rotation));
    this.physicsWorld.addBody(body);
    this.levelBodies.push({ body, mesh });
  }

  private createStoneBlock(block: LevelBlock) {
    const group = new THREE.Group();
    group.position.set(block.position.x, block.position.y, block.position.z);
    group.rotation.set(block.rotation?.x ?? 0, block.rotation?.y ?? 0, block.rotation?.z ?? 0);

    const material = new THREE.MeshStandardMaterial({
      color: block.color,
      roughness: block.kind === "void" ? 0.9 : 0.72,
      metalness: block.kind === "void" ? 0.02 : 0.08,
      transparent: block.opacity !== undefined,
      opacity: block.opacity ?? 1,
    });
    const base = new THREE.Mesh(new THREE.BoxGeometry(block.size.x, block.size.y, block.size.z), material);
    base.castShadow = block.kind !== "void";
    base.receiveShadow = true;
    group.add(base);

    if (block.kind === "platform" || block.kind === "wall" || block.kind === "rail") {
      const trimMaterial = new THREE.MeshStandardMaterial({
        color: block.kind === "platform" ? STONE_TOP : STONE_SIDE,
        roughness: 0.64,
        metalness: 0.06,
      });
      const top = new THREE.Mesh(
        new THREE.BoxGeometry(block.size.x * 0.88, 0.028, block.size.z * 0.82),
        trimMaterial,
      );
      top.position.y = block.size.y / 2 + 0.016;
      top.receiveShadow = true;
      group.add(top);

      if (block.kind === "platform") {
        group.add(createTrim(block.size.x, block.size.z));
      }
    }

    if (block.kind === "void") {
      const grid = new THREE.GridHelper(block.size.x, 18, "#1b3029", "#10231d");
      grid.position.y = block.size.y / 2 + 0.01;
      grid.material.opacity = 0.18;
      grid.material.transparent = true;
      group.add(grid);
    }

    return group;
  }

  private addTiltMechanism(config: LevelMechanism) {
    const mesh = new THREE.Group();
    const rampMaterial = new THREE.MeshStandardMaterial({
      color: config.color,
      roughness: 0.42,
      metalness: 0.2,
      emissive: "#4a3210",
      emissiveIntensity: 0.12,
    });
    const rampMesh = new THREE.Mesh(new THREE.BoxGeometry(config.size.x, config.size.y, config.size.z), rampMaterial);
    rampMesh.castShadow = true;
    rampMesh.receiveShadow = true;
    mesh.add(rampMesh);
    mesh.add(createRampTrim(config.size));

    const handle = createMechanismHandle();
    handle.position.set(config.handleOffset.x, config.handleOffset.y, config.handleOffset.z);
    mesh.add(handle);

    const arc = createArcIndicator();
    arc.position.set(config.handleOffset.x, config.handleOffset.y + 0.08, config.handleOffset.z);
    mesh.add(arc);

    const hotspot = createMechanismHotspot();
    hotspot.position.set(config.handleOffset.x, config.handleOffset.y + 0.03, config.handleOffset.z);
    mesh.add(hotspot);

    const body = new CANNON.Body({
      mass: 0,
      type: CANNON.Body.KINEMATIC,
      shape: new CANNON.Box(new CANNON.Vec3(config.size.x / 2, config.size.y / 2, config.size.z / 2)),
      material: this.gameMaterial,
    });
    this.physicsWorld.addBody(body);

    const mechanism: TiltMechanism = {
      config,
      body,
      mesh,
      rampMesh,
      handle,
      arc,
      hotspot,
      tilt: config.initialTilt,
      selected: false,
      hinted: false,
    };
    this.scene.add(mesh);
    this.mechanisms.set(config.id, mechanism);
    this.setMechanismTilt(mechanism, config.initialTilt);
  }

  private setMechanismTilt(mechanism: TiltMechanism, tilt: number) {
    mechanism.tilt = tilt;
    mechanism.mesh.position.set(mechanism.config.position.x, mechanism.config.position.y, mechanism.config.position.z);
    mechanism.mesh.rotation.set(tilt, mechanism.config.yaw, 0);
    mechanism.body.position.set(mechanism.config.position.x, mechanism.config.position.y, mechanism.config.position.z);
    mechanism.body.quaternion.setFromEuler(tilt, mechanism.config.yaw, 0, "XYZ");
    mechanism.body.velocity.set(0, 0, 0);
    mechanism.body.angularVelocity.set(0, 0, 0);
    mechanism.body.aabbNeedsUpdate = true;

    const material = mechanism.rampMesh.material;
    if (material instanceof THREE.MeshStandardMaterial) {
      material.emissiveIntensity = mechanism.selected ? 0.55 : 0.12;
    }
    mechanism.arc.rotation.y = -mechanism.config.yaw;
  }

  private addLevelProp(prop: LevelProp) {
    const mesh = createPropMesh(prop);
    mesh.position.set(prop.position.x, prop.position.y, prop.position.z);
    mesh.rotation.set(prop.rotation?.x ?? 0, prop.rotation?.y ?? 0, prop.rotation?.z ?? 0);
    this.scene.add(mesh);
  }

  private setMechanismSelected(id: string | undefined) {
    for (const mechanism of this.mechanisms.values()) {
      mechanism.selected = mechanism.config.id === id;
      const activeCue = mechanism.selected || mechanism.hinted;
      mechanism.arc.visible = activeCue;
      const scale = mechanism.selected ? 1.16 : 1;
      mechanism.handle.scale.setScalar(scale);
      mechanism.hotspot.scale.setScalar(mechanism.selected ? 1.18 : mechanism.hinted ? 1.08 : 1);
      const material = mechanism.rampMesh.material;
      if (material instanceof THREE.MeshStandardMaterial) {
        material.emissive.set(mechanism.selected ? "#d9b44a" : mechanism.hinted ? "#58c6a7" : "#4a3210");
        material.emissiveIntensity = mechanism.selected ? 0.55 : mechanism.hinted ? 0.34 : 0.12;
      }
    }
  }

  private setMechanismHinted(id: string | undefined) {
    for (const mechanism of this.mechanisms.values()) {
      mechanism.hinted = mechanism.config.id === id;
    }
    this.setMechanismSelected(this.mechanismControl?.id);
  }

  private getNearestMechanism(points: MappedHandPoint[]) {
    let best: TiltMechanism | undefined;
    let bestDistance = Number.POSITIVE_INFINITY;

    for (const point of points) {
      const candidate = this.getMechanismNearScreenPoint(point.x, point.y, MECHANISM_HIT_RADIUS * 1.15);
      if (!candidate) {
        continue;
      }
      const distance = this.getMechanismScreenDistance(candidate, point.x, point.y);
      if (distance < bestDistance) {
        best = candidate;
        bestDistance = distance;
      }
    }

    return best;
  }

  private getMechanismNearScreenPoint(clientX: number, clientY: number, radius = MECHANISM_HIT_RADIUS) {
    let best: TiltMechanism | undefined;
    let bestDistance = Number.POSITIVE_INFINITY;

    for (const mechanism of this.mechanisms.values()) {
      const distance = this.getMechanismScreenDistance(mechanism, clientX, clientY);
      if (distance < bestDistance && distance < radius) {
        best = mechanism;
        bestDistance = distance;
      }
    }

    return best;
  }

  private getMechanismScreenDistance(mechanism: TiltMechanism, clientX: number, clientY: number) {
    const handleWorld = new THREE.Vector3(
      mechanism.config.handleOffset.x,
      mechanism.config.handleOffset.y,
      mechanism.config.handleOffset.z,
    );
    mechanism.mesh.localToWorld(handleWorld);
    const centerWorld = new THREE.Vector3(0, 0, 0);
    mechanism.mesh.localToWorld(centerWorld);
    const handleScreen = this.worldToScreen(handleWorld);
    const centerScreen = this.worldToScreen(centerWorld);
    const handleDistance = Math.hypot(handleScreen.x - clientX, handleScreen.y - clientY);
    const centerDistance = Math.hypot(centerScreen.x - clientX, centerScreen.y - clientY);
    return Math.min(handleDistance, centerDistance * 0.78);
  }

  private worldToScreen(world: THREE.Vector3) {
    const rect = this.renderer.domElement.getBoundingClientRect();
    const projected = world.clone().project(this.camera);
    return {
      x: rect.left + ((projected.x + 1) / 2) * rect.width,
      y: rect.top + ((1 - projected.y) / 2) * rect.height,
    };
  }

  private screenToGround(clientX: number, clientY: number) {
    const rect = this.options.stageElement.getBoundingClientRect();
    this.pointer.x = ((clientX - rect.left) / rect.width) * 2 - 1;
    this.pointer.y = -(((clientY - rect.top) / rect.height) * 2 - 1);
    this.raycaster.setFromCamera(this.pointer, this.camera);
    return this.raycaster.ray.intersectPlane(this.debugPlane, this.planeHit);
  }

  private stepGame() {
    const now = performance.now();
    const delta = Math.min((now - this.lastPhysicsTime) / 1000, 1 / 30);
    this.lastPhysicsTime = now;
    if (this.gameActive) {
      this.physicsWorld.step(1 / 60, delta, 3);
    }

    const position = this.ballBody.position;
    if (
      this.gameActive &&
      (position.y < this.level.fallBounds.bottom ||
        position.x < this.level.fallBounds.left ||
        position.x > this.level.fallBounds.right ||
        position.z < this.level.fallBounds.back ||
        position.z > this.level.fallBounds.front)
    ) {
      this.resetGame("fallen");
    }

    const goalDistance = Math.hypot(position.x - this.level.goal.x, position.z - this.level.goal.z);
    if (
      this.gameActive &&
      !this.gameWon &&
      goalDistance < this.level.goal.radius &&
      Math.abs(position.y - this.level.goal.y) < 0.44 &&
      this.ballBody.velocity.length() < 2.8
    ) {
      this.gameWon = true;
      this.gameStatus = "goal";
      this.goalCore.material.opacity = 0.45;
    } else if (!this.gameWon) {
      this.gameStatus = "guiding";
      this.goalCore.material.opacity = 0.26;
    }

    this.syncBallMesh();
  }

  private syncBallMesh() {
    this.ballMesh.position.set(this.ballBody.position.x, this.ballBody.position.y, this.ballBody.position.z);
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
    this.goalRing.rotation.z += 0.008;
    for (const mechanism of this.mechanisms.values()) {
      mechanism.hotspot.rotation.y += 0.012;
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
  if (prop.kind === "pillar") {
    const group = new THREE.Group();
    const radius = prop.radius ?? 0.12;
    const height = prop.height ?? 0.9;
    const material = new THREE.MeshStandardMaterial({
      color: prop.color,
      roughness: 0.66,
      metalness: 0.08,
    });
    const shaft = new THREE.Mesh(new THREE.CylinderGeometry(radius, radius, height, 28), material);
    shaft.castShadow = true;
    shaft.receiveShadow = true;
    const capMaterial = new THREE.MeshStandardMaterial({ color: "#bac7bf", roughness: 0.58, metalness: 0.06 });
    const top = new THREE.Mesh(new THREE.CylinderGeometry(radius * 1.45, radius * 1.45, 0.12, 28), capMaterial);
    const base = top.clone();
    top.position.y = height / 2 + 0.06;
    base.position.y = -height / 2 - 0.06;
    group.add(shaft, top, base);
    return group;
  }

  if (prop.kind === "rune") {
    const group = new THREE.Group();
    const radius = prop.radius ?? 0.45;
    const ring = new THREE.Mesh(
      new THREE.TorusGeometry(radius, 0.035, 12, 64),
      new THREE.MeshStandardMaterial({
        color: prop.color,
        roughness: 0.34,
        metalness: 0.2,
        emissive: prop.color,
        emissiveIntensity: 0.34,
      }),
    );
    ring.rotation.x = Math.PI / 2;
    const line = new THREE.Mesh(
      new THREE.BoxGeometry(radius * 1.18, 0.02, 0.035),
      new THREE.MeshBasicMaterial({ color: prop.color, transparent: true, opacity: 0.76 }),
    );
    group.add(ring, line);
    return group;
  }

  if (prop.kind === "torch") {
    const group = new THREE.Group();
    const radius = prop.radius ?? 0.08;
    const height = prop.height ?? 0.4;
    const post = new THREE.Mesh(
      new THREE.CylinderGeometry(radius, radius, height, 18),
      new THREE.MeshStandardMaterial({ color: "#4d4238", roughness: 0.48, metalness: 0.22 }),
    );
    const glow = new THREE.Mesh(
      new THREE.SphereGeometry(radius * 1.7, 18, 12),
      new THREE.MeshBasicMaterial({ color: prop.color, transparent: true, opacity: 0.68 }),
    );
    glow.position.y = height / 2 + radius * 1.5;
    const light = new THREE.PointLight(prop.color, 0.75, 2.2);
    light.position.copy(glow.position);
    group.add(post, glow, light);
    return group;
  }

  const group = new THREE.Group();
  const size = prop.size ?? { x: 0.46, y: 0.04, z: 0.18 };
  const material = new THREE.MeshBasicMaterial({
    color: prop.color,
    transparent: true,
    opacity: 0.78,
    depthWrite: false,
  });
  const stem = new THREE.Mesh(new THREE.BoxGeometry(size.x * 0.62, size.y, size.z * 0.34), material);
  const cone = new THREE.Mesh(new THREE.ConeGeometry(size.z * 0.5, size.x * 0.32, 3), material);
  cone.rotation.z = -Math.PI / 2;
  cone.position.x = size.x * 0.33;
  group.add(stem, cone);
  return group;
}

function createTrim(width: number, depth: number) {
  const group = new THREE.Group();
  const material = new THREE.MeshStandardMaterial({ color: TRIM, roughness: 0.48, metalness: 0.22 });
  const front = new THREE.Mesh(new THREE.BoxGeometry(width * 0.92, 0.035, 0.035), material);
  const back = front.clone();
  const left = new THREE.Mesh(new THREE.BoxGeometry(0.035, 0.035, depth * 0.82), material);
  const right = left.clone();
  front.position.set(0, 0.055, depth * 0.43);
  back.position.set(0, 0.055, -depth * 0.43);
  left.position.set(-width * 0.47, 0.055, 0);
  right.position.set(width * 0.47, 0.055, 0);
  group.add(front, back, left, right);
  return group;
}

function createRampTrim(size: { x: number; y: number; z: number }) {
  const group = new THREE.Group();
  const material = new THREE.MeshStandardMaterial({ color: "#e0c273", roughness: 0.4, metalness: 0.24 });
  const axis = new THREE.Mesh(new THREE.CylinderGeometry(0.035, 0.035, size.x * 1.08, 16), material);
  axis.rotation.z = Math.PI / 2;
  axis.position.y = size.y / 2 + 0.04;
  const stripeA = new THREE.Mesh(new THREE.BoxGeometry(size.x * 0.84, 0.018, 0.03), material);
  const stripeB = stripeA.clone();
  stripeA.position.set(0, size.y / 2 + 0.052, size.z * 0.24);
  stripeB.position.set(0, size.y / 2 + 0.052, -size.z * 0.24);
  group.add(axis, stripeA, stripeB);
  return group;
}

function createMechanismHandle() {
  const group = new THREE.Group();
  const material = new THREE.MeshStandardMaterial({
    color: "#58c6a7",
    roughness: 0.28,
    metalness: 0.18,
    emissive: "#1c9e88",
    emissiveIntensity: 0.32,
  });
  const knob = new THREE.Mesh(new THREE.SphereGeometry(0.2, 28, 18), material);
  const stem = new THREE.Mesh(new THREE.CylinderGeometry(0.055, 0.055, 0.46, 20), material);
  stem.position.y = -0.19;
  group.add(knob, stem);
  return group;
}

function createMechanismHotspot() {
  const group = new THREE.Group();
  const material = new THREE.MeshBasicMaterial({
    color: "#58c6a7",
    transparent: true,
    opacity: 0.82,
    depthWrite: false,
  });
  const ring = new THREE.Mesh(new THREE.TorusGeometry(0.58, 0.035, 12, 72), material);
  ring.rotation.x = Math.PI / 2;
  const halo = new THREE.Mesh(
    new THREE.CircleGeometry(0.72, 72),
    new THREE.MeshBasicMaterial({
      color: "#58c6a7",
      transparent: true,
      opacity: 0.2,
      depthWrite: false,
    }),
  );
  halo.rotation.x = Math.PI / 2;
  const beacon = new THREE.Mesh(
    new THREE.CylinderGeometry(0.09, 0.18, 0.64, 24),
    new THREE.MeshBasicMaterial({
      color: "#58c6a7",
      transparent: true,
      opacity: 0.28,
      depthWrite: false,
    }),
  );
  beacon.position.y = 0.34;
  const cap = new THREE.Mesh(new THREE.SphereGeometry(0.16, 24, 14), material.clone());
  cap.position.y = 0.7;
  group.add(halo, ring, beacon, cap);
  return group;
}

function createArcIndicator() {
  const group = new THREE.Group();
  const curve = new THREE.EllipseCurve(0, 0, 0.34, 0.34, -0.85, 0.85);
  const points = curve.getPoints(26);
  const geometry = new THREE.BufferGeometry().setFromPoints(points.map((point) => new THREE.Vector3(point.x, 0, point.y)));
  const line = new THREE.Line(
    geometry,
    new THREE.LineBasicMaterial({ color: "#58c6a7", transparent: true, opacity: 0.95 }),
  );
  const arrow = new THREE.Mesh(
    new THREE.ConeGeometry(0.055, 0.13, 3),
    new THREE.MeshBasicMaterial({ color: "#58c6a7", transparent: true, opacity: 0.95 }),
  );
  arrow.position.set(0.23, 0, 0.24);
  arrow.rotation.z = -0.5;
  group.add(line, arrow);
  group.visible = false;
  return group;
}

function toCannonQuaternion(rotation?: { x?: number; y?: number; z?: number }) {
  const quaternion = new CANNON.Quaternion();
  quaternion.setFromEuler(rotation?.x ?? 0, rotation?.y ?? 0, rotation?.z ?? 0, "XYZ");
  return quaternion;
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
    material.emissive.set("#d9b44a");
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
