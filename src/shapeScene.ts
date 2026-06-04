import * as THREE from "three";
import type { SceneObject, ShapeLibraryItem, ShapeType, Vector2 } from "./types";

interface ShapeControllerOptions {
  stageElement: HTMLElement;
  library: ShapeLibraryItem[];
}

const WORLD_SCALE = 140;

export class ShapeScene {
  private readonly scene = new THREE.Scene();
  private readonly camera = new THREE.OrthographicCamera(-1, 1, 1, -1, 0.1, 100);
  private readonly renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
  private readonly raycaster = new THREE.Raycaster();
  private readonly pointer = new THREE.Vector2();
  private readonly objects = new Map<string, THREE.Mesh>();
  private readonly objectState = new Map<string, SceneObject>();
  private selectedObjectId?: string;
  private animationFrame = 0;
  private previewMesh?: THREE.Mesh;
  private transformBase?: {
    objectId: string;
    scale: number;
    rotation: THREE.Euler;
  };

  constructor(private readonly options: ShapeControllerOptions) {
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    this.renderer.setClearColor(0x000000, 0);
    this.renderer.domElement.className = "shape-canvas";
    this.options.stageElement.appendChild(this.renderer.domElement);
    this.camera.position.set(0, 0, 10);
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
    mesh.position.set(point.x, point.y, 0);
    mesh.scale.setScalar(item.defaultScale);
    mesh.userData.objectId = id;
    this.scene.add(mesh);
    this.objects.set(id, mesh);
    this.objectState.set(id, {
      id,
      type,
      position: { x: point.x, y: point.y, z: 0 },
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

    this.previewMesh.position.set(point.x, point.y, 0.3);
  }

  applyTransformAtScreenPoint(clientX: number, clientY: number, scaleDelta: number, rotationDelta: number) {
    this.applyTransform(this.screenToWorldPoint(clientX, clientY), scaleDelta, rotationDelta);
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
  }

  endTransform() {
    this.transformBase = undefined;
  }

  applyTransform(center: Vector2, scaleDelta: number, rotationDelta: number) {
    const mesh = this.getSelectedMesh();
    if (!mesh) {
      return;
    }

    const base =
      this.transformBase && this.transformBase.objectId === this.selectedObjectId
        ? this.transformBase
        : { scale: mesh.scale.x, rotation: mesh.rotation };
    const currentWorld = new THREE.Vector2(mesh.position.x, mesh.position.y);
    currentWorld.lerp(new THREE.Vector2(center.x, center.y), 0.45);
    mesh.position.set(currentWorld.x, currentWorld.y, mesh.position.z);
    const nextScale = clamp(base.scale * scaleDelta, 0.35, 3.4);
    mesh.scale.setScalar(nextScale);
    mesh.rotation.z = base.rotation.z + rotationDelta;
    mesh.rotation.x = base.rotation.x + rotationDelta * 0.08;
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

  selectAtScreenPoint(clientX: number, clientY: number) {
    const rect = this.renderer.domElement.getBoundingClientRect();
    this.pointer.x = ((clientX - rect.left) / rect.width) * 2 - 1;
    this.pointer.y = -(((clientY - rect.top) / rect.height) * 2 - 1);
    this.raycaster.setFromCamera(this.pointer, this.camera);
    const hits = this.raycaster.intersectObjects([...this.objects.values()]);
    const id = hits[0]?.object.userData.objectId as string | undefined;
    if (id) {
      this.selectObject(id);
    }
    return id;
  }

  selectAt(clientX: number, clientY: number) {
    return this.selectAtScreenPoint(clientX, clientY);
  }

  getSceneObjects(): SceneObject[] {
    return [...this.objectState.values()];
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
        material.emissive.set(selected ? "#2f4f46" : "#000000");
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

function clamp(value: number, min: number, max: number) {
  return Math.min(Math.max(value, min), max);
}
