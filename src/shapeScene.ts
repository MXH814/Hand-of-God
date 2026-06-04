import * as THREE from "three";
import type { SceneObject, ShapeLibraryItem, ShapeType, Vector2 } from "./types";

interface ShapeControllerOptions {
  container: HTMLElement;
  library: ShapeLibraryItem[];
}

export class ShapeScene {
  private readonly scene = new THREE.Scene();
  private readonly camera = new THREE.PerspectiveCamera(45, 16 / 9, 0.1, 100);
  private readonly renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
  private readonly raycaster = new THREE.Raycaster();
  private readonly pointer = new THREE.Vector2();
  private readonly objects = new Map<string, THREE.Mesh>();
  private readonly objectState = new Map<string, SceneObject>();
  private selectedObjectId?: string;
  private animationFrame = 0;
  private previewMesh?: THREE.Mesh;

  constructor(private readonly options: ShapeControllerOptions) {
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    this.renderer.setClearColor(0x000000, 0);
    this.options.container.appendChild(this.renderer.domElement);
    this.camera.position.set(0, 2.2, 7);
    this.camera.lookAt(0, 0, 0);
    this.scene.background = new THREE.Color("#13221b");
    this.addReferenceObjects();
    this.resize();
    this.animate();
  }

  dispose() {
    cancelAnimationFrame(this.animationFrame);
    this.renderer.dispose();
    this.renderer.domElement.remove();
  }

  resize() {
    const rect = this.options.container.getBoundingClientRect();
    const width = Math.max(1, Math.floor(rect.width));
    const height = Math.max(1, Math.floor(rect.height));
    this.camera.aspect = width / height;
    this.camera.updateProjectionMatrix();
    this.renderer.setSize(width, height, false);
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

    this.previewMesh.position.set(point.x, point.y, 0.2);
  }

  applyTransform(center: Vector2, scaleDelta: number, rotationDelta: number) {
    const mesh = this.getSelectedMesh();
    if (!mesh) {
      return;
    }

    mesh.position.set(center.x, center.y, mesh.position.z);
    const nextScale = clamp(mesh.scale.x * (1 + (scaleDelta - 1) * 0.12), 0.35, 3.2);
    mesh.scale.setScalar(nextScale);
    mesh.rotation.z += rotationDelta;
    mesh.rotation.x += rotationDelta * 0.18;
    this.syncState(mesh);
  }

  selectAt(clientX: number, clientY: number) {
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
      roughness: 0.48,
      metalness: 0.18,
      transparent: preview,
      opacity: preview ? 0.55 : 1,
    });
    const mesh = new THREE.Mesh(createGeometry(item.type), material);
    mesh.castShadow = true;
    mesh.receiveShadow = true;
    return mesh;
  }

  private addReferenceObjects() {
    const ambient = new THREE.AmbientLight("#ffffff", 0.82);
    const key = new THREE.DirectionalLight("#ffffff", 1.2);
    key.position.set(3, 4, 5);
    this.scene.add(ambient, key);

    const grid = new THREE.GridHelper(8, 16, "#6f8579", "#30423a");
    grid.position.y = -1.7;
    this.scene.add(grid);

    const axes = new THREE.AxesHelper(1.4);
    axes.position.set(-3.25, -1.65, 0);
    this.scene.add(axes);
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
      return new THREE.SphereGeometry(0.7, 36, 24);
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
