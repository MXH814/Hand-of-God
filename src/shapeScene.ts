import * as THREE from "three";
import type { MappedHandPoint, SceneObject, ShapeLibraryItem, ShapeType, Vector2 } from "./types";

interface ShapeControllerOptions {
  stageElement: HTMLElement;
  library: ShapeLibraryItem[];
}

const WORLD_SCALE = 140;
const DEPTH_STEP = 1.35;

export class ShapeScene {
  private readonly scene = new THREE.Scene();
  private readonly camera = new THREE.OrthographicCamera(-1, 1, 1, -1, 0.1, 100);
  private readonly renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
  private readonly raycaster = new THREE.Raycaster();
  private readonly pointer = new THREE.Vector2();
  private readonly objects = new Map<string, THREE.Mesh>();
  private readonly objectState = new Map<string, SceneObject>();
  private selectedObjectId?: string;
  private activeObjectId?: string;
  private deleteTargetObjectId?: string;
  private animationFrame = 0;
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
  };

  constructor(private readonly options: ShapeControllerOptions) {
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    this.renderer.setClearColor(0x000000, 0);
    this.renderer.domElement.className = "shape-canvas";
    this.options.stageElement.appendChild(this.renderer.domElement);
    this.camera.position.set(0, 0, 30);
    this.camera.lookAt(0, 0, 0);
    this.addLights();
    this.resize();
    this.animate();
  }

  dispose() {
    cancelAnimationFrame(this.animationFrame);
    this.renderer.dispose();
    this.renderer.domElement.remove();
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
      const depthRotation = clamp(depthByScale * 3.6 + depthByZ * 4.5, -1.2, 1.2);
      const lateralRotation = clamp((point.x - this.singleHandBase.x) / 240, -0.85, 0.85);

      mesh.rotation.x = smoothAngle(mesh.rotation.x, this.singleHandBase.rotation.x + depthRotation, 0.38);
      mesh.rotation.y = smoothAngle(mesh.rotation.y, this.singleHandBase.rotation.y + lateralRotation, 0.32);
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
    this.scene.add(ambient, key);
  }

  private animate = () => {
    this.animationFrame = requestAnimationFrame(this.animate);
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
