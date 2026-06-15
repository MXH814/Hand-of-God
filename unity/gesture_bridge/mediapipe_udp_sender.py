import argparse
import json
import os
import socket
import struct
import time

import cv2
import mediapipe as mp
import numpy as np


FINGERS = {
    "thumb": (4, 3, 2),
    "index": (8, 6, 5),
    "middle": (12, 10, 9),
    "ring": (16, 14, 13),
    "pinky": (20, 18, 17),
}

PALM_LANDMARKS = {0, 9}
FINGERTIP_LANDMARKS = {4, 8, 12, 16, 20}


class LandmarkPoint:
    def __init__(self, x, y, z):
        self.x = float(x)
        self.y = float(y)
        self.z = float(z)


class SmoothedPoint:
    def __init__(self, x, y, z):
        self.x = float(x)
        self.y = float(y)
        self.z = float(z)
        self.last_time = time.time()

    def update(self, x, y, z, index=-1):
        now = time.time()
        dt = max(now - self.last_time, 1e-3)
        dx = x - self.x
        dy = y - self.y
        dz = z - self.z
        displacement = float(np.linalg.norm(np.array([dx, dy, dz])))
        speed = displacement / dt
        edge = max(abs(x - 0.5), abs(y - 0.5)) * 2.0

        is_palm_anchor = index in PALM_LANDMARKS
        is_fingertip = index in FINGERTIP_LANDMARKS
        deadband = 0.00085 if is_palm_anchor else 0.00045
        if displacement <= deadband:
            self.last_time = now
            return self.x, self.y, self.z

        live_motion = (displacement - deadband) / displacement
        dx *= live_motion
        dy *= live_motion
        dz *= live_motion
        if is_palm_anchor:
            alpha = 0.24 + min(speed * 0.030, 0.26)
            max_alpha = 0.55
            min_alpha = 0.10
        else:
            alpha = 0.74 + min(speed * 0.040, 0.20)
            max_alpha = 0.94
            min_alpha = 0.35

        if is_fingertip:
            alpha *= 0.98
        alpha -= max(edge - 0.78, 0.0) * (0.06 if is_palm_anchor else 0.03)

        alpha = max(min_alpha, min(max_alpha, alpha))
        self.x += dx * alpha
        self.y += dy * alpha
        self.z += dz * alpha
        self.last_time = now
        return self.x, self.y, self.z


def smooth_landmarks(hand_id, landmarks, smooth_points):
    slots = smooth_points.setdefault(hand_id, {})
    smoothed_json = []
    smoothed_points = []
    for index, point in enumerate(landmarks):
        slot = slots.get(index)
        if slot is None:
            slot = SmoothedPoint(point.x, point.y, point.z)
            slots[index] = slot
        x, y, z = slot.update(point.x, point.y, point.z, index)
        smoothed_points.append(LandmarkPoint(x, y, z))
        smoothed_json.append({"x": float(x), "y": float(y), "z": float(z)})
    return smoothed_json, smoothed_points


def display_landmarks(landmarks):
    return [{"x": float(point.x), "y": float(point.y), "z": float(point.z)} for point in landmarks]


class VideoStreamClient:
    def __init__(self, host, port):
        self.host = host
        self.port = port
        self.sock = None
        self.last_attempt = 0.0

    def close(self):
        if self.sock is not None:
            try:
                self.sock.close()
            except OSError:
                pass
        self.sock = None

    def connect_if_needed(self):
        if self.sock is not None:
            return True

        now = time.time()
        if now - self.last_attempt < 1.0:
            return False

        self.last_attempt = now
        try:
            sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            sock.settimeout(1.0)
            sock.connect((self.host, self.port))
            sock.settimeout(None)
            self.sock = sock
            print(f"Video stream connected to {self.host}:{self.port}")
            return True
        except OSError:
            self.close()
            return False

    def send_jpeg(self, frame, quality):
        if not self.connect_if_needed():
            return

        ok, encoded = cv2.imencode(".jpg", frame, [int(cv2.IMWRITE_JPEG_QUALITY), quality])
        if not ok:
            return

        data = encoded.tobytes()
        try:
            self.sock.sendall(struct.pack(">I", len(data)) + data)
        except OSError:
            self.close()


def normalized_distance(a, b, scale):
    return float(np.linalg.norm(np.array([a.x - b.x, a.y - b.y, a.z - b.z])) / max(scale, 1e-5))


def clamp(value, low=-1.0, high=1.0):
    return max(low, min(high, value))


def finger_extended(landmarks, name):
    tip, pip, mcp = FINGERS[name]
    wrist = landmarks[0]
    if name == "thumb":
        return normalized_distance(landmarks[tip], landmarks[5], 1.0) > normalized_distance(landmarks[pip], landmarks[5], 1.0)
    tip_to_wrist = normalized_distance(landmarks[tip], wrist, 1.0)
    pip_to_wrist = normalized_distance(landmarks[pip], wrist, 1.0)
    return tip_to_wrist > pip_to_wrist and landmarks[tip].y < landmarks[pip].y


def analyze_hand(landmarks, handedness, score, hand_id, neutral, smoothed_landmarks):
    wrist = landmarks[0]
    middle_mcp = landmarks[9]
    index_mcp = landmarks[5]
    pinky_mcp = landmarks[17]
    thumb_tip = landmarks[4]
    index_tip = landmarks[8]

    palm_span = normalized_distance(index_mcp, pinky_mcp, 1.0)
    pinch_distance = normalized_distance(thumb_tip, index_tip, palm_span)
    raw_pitch = -(middle_mcp.y - wrist.y) * 3.0
    raw_roll = (index_mcp.y - pinky_mcp.y) * 4.0
    raw_yaw = (middle_mcp.x - wrist.x) * 3.0

    if neutral is not None:
        raw_roll -= neutral["roll"]
        raw_pitch -= neutral["pitch"]
        raw_yaw -= neutral["yaw"]

    fingers = {name: finger_extended(landmarks, name) for name in FINGERS}
    open_palm = sum(1 for extended in fingers.values() if extended) >= 4

    return {
        "id": hand_id,
        "handedness": handedness,
        "score": float(score),
        "pinchX": clamp((thumb_tip.x + index_tip.x) * 0.5, 0.0, 1.0),
        "pinchY": clamp((thumb_tip.y + index_tip.y) * 0.5, 0.0, 1.0),
        "indexX": clamp(index_tip.x, 0.0, 1.0),
        "indexY": clamp(index_tip.y, 0.0, 1.0),
        "pinchDistance": pinch_distance,
        "palmSpan": palm_span,
        "palmRoll": clamp(raw_roll),
        "palmPitch": clamp(raw_pitch),
        "palmYaw": clamp(raw_yaw),
        "pinch": False,
        "openPalm": open_palm,
        "thumbExtended": fingers["thumb"],
        "indexExtended": fingers["index"],
        "middleExtended": fingers["middle"],
        "ringExtended": fingers["ring"],
        "pinkyExtended": fingers["pinky"],
        "landmarks": smoothed_landmarks,
    }, {"roll": raw_roll, "pitch": raw_pitch, "yaw": raw_yaw, "pinchDistance": pinch_distance}


def neutral_payload():
    return {
        "roll": 0.0,
        "pitch": 0.0,
        "pinchX": 0.5,
        "pinchY": 0.5,
        "indexX": 0.5,
        "indexY": 0.5,
        "pinchDistance": 1.0,
        "palmSpan": 0.0,
        "palmRoll": 0.0,
        "palmPitch": 0.0,
        "palmYaw": 0.0,
        "confidence": 0.0,
        "bridgeFps": 0.0,
        "processingMs": 0.0,
        "pinch": False,
        "openPalm": False,
        "handCount": 0,
        "hands": [],
        "timestamp": time.time(),
    }


def main():
    parser = argparse.ArgumentParser(description="Send MediaPipe hand frames to Unity over UDP.")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=5005)
    parser.add_argument("--camera", type=int, default=0)
    parser.add_argument("--mirror", action="store_true", default=True)
    parser.add_argument("--preview", action="store_true", help="Open a separate OpenCV preview window for debugging.")
    parser.add_argument("--no-preview", action="store_true", help="Deprecated compatibility flag. The bridge is headless unless --preview is set.")
    parser.add_argument("--video-host", default="127.0.0.1")
    parser.add_argument("--video-port", type=int, default=5006)
    parser.add_argument("--video-width", type=int, default=640)
    parser.add_argument("--video-height", type=int, default=480)
    parser.add_argument("--camera-fps", type=int, default=30)
    parser.add_argument("--jpeg-quality", type=int, default=72)
    parser.add_argument("--lock-port", type=int, default=5007)
    parser.add_argument("--model-complexity", type=int, choices=(0, 1), default=1, help="MediaPipe Hands model complexity. 1 improves finger-joint fidelity; 0 is faster.")
    args = parser.parse_args()

    lock_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    try:
        lock_socket.bind(("127.0.0.1", args.lock_port))
        lock_socket.listen(1)
    except OSError:
        print(f"Another Hand of God gesture bridge is already running on lock port {args.lock_port}.")
        return

    os.environ.setdefault("GLOG_minloglevel", "1")
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    video = VideoStreamClient(args.video_host, args.video_port)
    hands_model = mp.solutions.hands.Hands(max_num_hands=2, model_complexity=args.model_complexity, min_detection_confidence=0.65, min_tracking_confidence=0.65)
    drawing = mp.solutions.drawing_utils
    cap = cv2.VideoCapture(args.camera, cv2.CAP_DSHOW)
    cap.set(cv2.CAP_PROP_BUFFERSIZE, 1)
    cap.set(cv2.CAP_PROP_FRAME_WIDTH, args.video_width)
    cap.set(cv2.CAP_PROP_FRAME_HEIGHT, args.video_height)
    cap.set(cv2.CAP_PROP_FPS, args.camera_fps)
    neutral = None
    last_raw = {"roll": 0.0, "pitch": 0.0, "yaw": 0.0}
    pinch_latches = {}
    smooth_landmarks_by_hand = {}
    smooth_cursors = {}
    bridge_fps = 0.0
    processing_ms = 0.0
    frame_counter = 0
    diagnostics_start = time.time()

    print(f"Hand of God bridge: headless camera tracking started with MediaPipe model_complexity={args.model_complexity}, camera_fps={args.camera_fps}. Use --preview for a debug window.")
    try:
        while cap.isOpened():
            frame_start = time.time()
            ok, frame = cap.read()
            if not ok:
                break

            frame = cv2.resize(frame, (args.video_width, args.video_height), interpolation=cv2.INTER_AREA)

            if args.mirror:
                frame = cv2.flip(frame, 1)

            result = hands_model.process(cv2.cvtColor(frame, cv2.COLOR_BGR2RGB))
            payload = neutral_payload()
            analyzed = []

            active_ids = set()
            if result.multi_hand_landmarks:
                handedness_list = result.multi_handedness or []
                for index, hand in enumerate(result.multi_hand_landmarks[:2]):
                    category = handedness_list[index].classification[0] if index < len(handedness_list) else None
                    label = category.label if category else "Unknown"
                    score = category.score if category else 1.0
                    hand_id = label if label in ("Left", "Right") else f"Unknown-{index}"
                    active_ids.add(hand_id)
                    display_landmark_json = display_landmarks(hand.landmark)
                    smoothed_landmarks, smoothed_points = smooth_landmarks(hand_id, hand.landmark, smooth_landmarks_by_hand)
                    hand_payload, raw = analyze_hand(smoothed_points, label, score, hand_id, neutral, smoothed_landmarks)
                    hand_payload["displayLandmarks"] = display_landmark_json
                    last_raw = raw

                    distance = raw["pinchDistance"]
                    latched = pinch_latches.get(hand_id, False)
                    latched = distance < (0.92 if latched else 0.72)
                    pinch_latches[hand_id] = latched

                    index_only = (
                        hand_payload["indexExtended"]
                        and not hand_payload["middleExtended"]
                        and not hand_payload["ringExtended"]
                        and not hand_payload["pinkyExtended"]
                    )
                    pinch_alpha = 0.65 if latched else 0.45
                    previous = smooth_cursors.get(hand_id, (hand_payload["pinchX"], hand_payload["pinchY"], hand_payload["indexX"], hand_payload["indexY"]))
                    index_motion = float(np.linalg.norm(np.array([
                        hand_payload["indexX"] - previous[2],
                        hand_payload["indexY"] - previous[3],
                    ])))
                    if index_only:
                        index_alpha = 0.22 if index_motion < 0.012 else 0.52
                    else:
                        index_alpha = 0.58
                    smoothed = (
                        previous[0] * (1.0 - pinch_alpha) + hand_payload["pinchX"] * pinch_alpha,
                        previous[1] * (1.0 - pinch_alpha) + hand_payload["pinchY"] * pinch_alpha,
                        previous[2] * (1.0 - index_alpha) + hand_payload["indexX"] * index_alpha,
                        previous[3] * (1.0 - index_alpha) + hand_payload["indexY"] * index_alpha,
                    )
                    smooth_cursors[hand_id] = smoothed

                    hand_payload["pinch"] = latched
                    hand_payload["openPalm"] = hand_payload["openPalm"] and not latched
                    hand_payload["pinchX"], hand_payload["pinchY"], hand_payload["indexX"], hand_payload["indexY"] = smoothed
                    analyzed.append(hand_payload)
                    if args.preview:
                        drawing.draw_landmarks(frame, hand, mp.solutions.hands.HAND_CONNECTIONS)
            stale_ids = [hand_id for hand_id in smooth_landmarks_by_hand if hand_id not in active_ids]
            for hand_id in stale_ids:
                smooth_landmarks_by_hand.pop(hand_id, None)
                smooth_cursors.pop(hand_id, None)
                pinch_latches.pop(hand_id, None)

            if analyzed:
                primary = sorted(analyzed, key=lambda h: (h["pinch"], h["score"]), reverse=True)[0]
                payload.update({
                    "roll": primary["palmRoll"],
                    "pitch": primary["palmPitch"],
                    "pinchX": primary["pinchX"],
                    "pinchY": primary["pinchY"],
                    "indexX": primary["indexX"],
                    "indexY": primary["indexY"],
                    "pinchDistance": primary["pinchDistance"],
                    "palmSpan": primary["palmSpan"],
                    "palmRoll": primary["palmRoll"],
                    "palmPitch": primary["palmPitch"],
                    "palmYaw": primary["palmYaw"],
                    "confidence": primary["score"],
                    "bridgeFps": bridge_fps,
                    "processingMs": processing_ms,
                    "pinch": primary["pinch"],
                    "openPalm": primary["openPalm"],
                    "handCount": len(analyzed),
                    "hands": analyzed,
                    "timestamp": time.time(),
                })
            payload["bridgeFps"] = bridge_fps
            payload["processingMs"] = processing_ms

            sock.sendto(json.dumps(payload).encode("utf-8"), (args.host, args.port))
            video.send_jpeg(frame, max(30, min(args.jpeg_quality, 95)))
            processing_ms = (time.time() - frame_start) * 1000.0
            frame_counter += 1
            elapsed = time.time() - diagnostics_start
            if elapsed >= 5.0:
                bridge_fps = frame_counter / elapsed
                print(f"Bridge diagnostics: fps={bridge_fps:.1f} processing={processing_ms:.1f}ms model_complexity={args.model_complexity}")
                diagnostics_start = time.time()
                frame_counter = 0

            if args.preview:
                cv2.putText(frame, "C calibrate neutral | Q quit", (18, 34), cv2.FONT_HERSHEY_SIMPLEX, 0.8, (80, 240, 180), 2)
                cv2.putText(frame, f"hands {payload['handCount']} pinch {payload['pinchDistance']:.2f} fps {bridge_fps:.1f}", (18, 66), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (80, 240, 180), 2)
                cv2.imshow("Hand of God Gesture Bridge", frame)
                key = cv2.waitKey(1) & 0xFF
                if key == ord("q"):
                    break
                if key == ord("c"):
                    neutral = dict(last_raw)
                    print(f"Calibrated bridge neutral pose: {neutral}")
            else:
                time.sleep(0.001)
    finally:
        cap.release()
        video.close()
        hands_model.close()
        lock_socket.close()
        if args.preview:
            cv2.destroyAllWindows()


if __name__ == "__main__":
    main()
