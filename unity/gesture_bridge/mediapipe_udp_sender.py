import argparse
import json
import os
import socket
import struct
import threading
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

DISPLAY_FINGER_CHAINS = (
    (1, 2, 3, 4),
    (5, 6, 7, 8),
    (9, 10, 11, 12),
    (13, 14, 15, 16),
    (17, 18, 19, 20),
)

WRIST_LANDMARKS = {0}
FINGER_CHAIN_LANDMARKS = set(range(1, 21))


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

        is_wrist_anchor = index in WRIST_LANDMARKS
        is_finger_chain = index in FINGER_CHAIN_LANDMARKS
        if is_finger_chain:
            self.x = float(x)
            self.y = float(y)
            self.z = float(z)
            self.last_time = now
            return self.x, self.y, self.z

        deadband = 0.00065 if is_wrist_anchor else 0.00008
        if displacement <= deadband:
            self.last_time = now
            return self.x, self.y, self.z

        live_motion = (displacement - deadband) / displacement
        dx *= live_motion
        dy *= live_motion
        dz *= live_motion
        if is_wrist_anchor:
            alpha = 0.24 + min(speed * 0.030, 0.26)
            max_alpha = 0.55
            min_alpha = 0.10
        else:
            alpha = 0.70 + min(speed * 0.035, 0.18)
            max_alpha = 0.92
            min_alpha = 0.35

        alpha -= max(edge - 0.78, 0.0) * (0.06 if is_wrist_anchor else 0.018)

        alpha = max(min_alpha, min(max_alpha, alpha))
        self.x += dx * alpha
        self.y += dy * alpha
        self.z += dz * alpha
        self.last_time = now
        return self.x, self.y, self.z


class ResponsiveDisplayHand:
    def __init__(self, landmarks):
        anchor = self._palm_anchor(landmarks)
        self.anchor = anchor
        self.last_time = time.time()

    def update(self, landmarks):
        raw_points = np.array([[float(point.x), float(point.y), float(point.z)] for point in landmarks], dtype=np.float32)
        raw_anchor = self._palm_anchor(landmarks)
        now = time.time()
        dt = max(now - self.last_time, 1e-3)
        delta = raw_anchor - self.anchor
        displacement = float(np.linalg.norm(delta))
        speed = displacement / dt

        deadband = 0.00022
        if displacement > deadband:
            live_motion = (displacement - deadband) / displacement
            delta *= live_motion
            alpha = 0.86 if speed < 0.055 else 1.0
            self.anchor += delta * alpha

        self.last_time = now

        shape_offsets = raw_points - raw_anchor
        display_points = self.anchor + shape_offsets
        return [{"x": float(point[0]), "y": float(point[1]), "z": float(point[2])} for point in display_points]

    @staticmethod
    def _palm_anchor(landmarks):
        if len(landmarks) >= 18:
            palm_indices = (0, 5, 9, 17)
        else:
            palm_indices = (0,)
        points = np.array([[float(landmarks[i].x), float(landmarks[i].y), float(landmarks[i].z)] for i in palm_indices], dtype=np.float32)
        return points.mean(axis=0)


class ResponsiveFingerDisplayHand:
    def __init__(self, landmarks):
        self.previous = self._points(landmarks)
        self.displayed = self.previous.copy()

    def update(self, landmarks):
        raw_points = self._points(landmarks)
        corrected = raw_points.copy()
        palm_width = max(float(np.linalg.norm(raw_points[5][:2] - raw_points[17][:2])), 0.055)
        palm_shift = float(np.linalg.norm(self._palm_center(raw_points)[:2] - self._palm_center(self.previous)[:2]))
        if palm_shift > palm_width * 1.6:
            self.previous = raw_points
            self.displayed = corrected.copy()
            return self._json_points(corrected)

        for mcp, pip, dip, tip in DISPLAY_FINGER_CHAINS:
            tip_delta = raw_points[tip] - self.previous[tip]
            tip_motion = float(np.linalg.norm(tip_delta[:2]))
            if tip_motion < 0.0015:
                continue

            finger_span = max(float(np.linalg.norm(raw_points[tip][:2] - raw_points[mcp][:2])), 0.018)
            responsiveness = min(1.0, (tip_motion - 0.0015) / 0.010)

            pip_delta = raw_points[pip] - self.previous[pip]
            dip_delta = raw_points[dip] - self.previous[dip]

            pip_motion = float(np.linalg.norm(pip_delta[:2]))
            dip_motion = float(np.linalg.norm(dip_delta[:2]))
            pip_lag = max(0.0, (tip_motion - pip_motion) / max(tip_motion, 1e-5))
            dip_lag = max(0.0, (tip_motion - dip_motion) / max(tip_motion, 1e-5))

            pip_correction = (tip_delta - pip_delta) * (0.42 * pip_lag * responsiveness)
            dip_correction = (tip_delta - dip_delta) * (0.66 * dip_lag * responsiveness)

            corrected[pip] += self._limited(pip_correction, max(finger_span * 0.28, palm_width * 0.08))
            corrected[dip] += self._limited(dip_correction, max(finger_span * 0.38, palm_width * 0.12))
            self._soft_preserve_finger_chain(raw_points, corrected, (mcp, pip, dip, tip))

        for chain in DISPLAY_FINGER_CHAINS[1:]:
            self._straighten_extended_finger(raw_points, corrected, chain, palm_width)

        stabilized = self._stabilize_display(corrected, palm_width)
        self.previous = raw_points
        self.displayed = stabilized
        return self._json_points(stabilized)

    @staticmethod
    def _points(landmarks):
        return np.array([[float(point.x), float(point.y), float(point.z)] for point in landmarks], dtype=np.float32)

    @staticmethod
    def _palm_center(points):
        return points[[0, 5, 9, 17]].mean(axis=0)

    @staticmethod
    def _limited(vector, limit):
        magnitude = float(np.linalg.norm(vector[:2]))
        if magnitude <= limit or magnitude <= 1e-6:
            return vector
        return vector * (limit / magnitude)

    def _stabilize_display(self, target, palm_width):
        stabilized = self.displayed.copy()
        jitter_radius = max(0.0038, palm_width * 0.034)
        live_radius = max(jitter_radius * 5.5, palm_width * 0.105)
        for index in range(len(target)):
            delta = target[index] - self.displayed[index]
            motion = float(np.linalg.norm(delta[:2]))
            if motion <= jitter_radius:
                continue

            motion_scale = min(1.0, (motion - jitter_radius) / live_radius)
            alpha = 0.36 + (motion_scale ** 1.2) * 0.64
            effective_deadzone = jitter_radius * (1.0 - motion_scale)
            deadzone_delta = delta * ((motion - effective_deadzone) / motion)
            stabilized[index] = self.displayed[index] + deadzone_delta * alpha
        return stabilized

    @staticmethod
    def _json_points(points):
        return [{"x": float(point[0]), "y": float(point[1]), "z": float(point[2])} for point in points]

    @staticmethod
    def _soft_preserve_finger_chain(raw_points, corrected, chain):
        mcp, pip, dip, tip = chain
        raw_lengths = [
            float(np.linalg.norm(raw_points[pip] - raw_points[mcp])),
            float(np.linalg.norm(raw_points[dip] - raw_points[pip])),
            float(np.linalg.norm(raw_points[tip] - raw_points[dip])),
        ]
        if min(raw_lengths) <= 1e-5:
            return

        segments = ((mcp, pip, raw_lengths[0]), (pip, dip, raw_lengths[1]), (dip, tip, raw_lengths[2]))
        movable = {pip, dip}
        for _ in range(5):
            for a, b, raw_length in segments:
                vector = corrected[b] - corrected[a]
                length = float(np.linalg.norm(vector))
                if length <= 1e-5:
                    vector = raw_points[b] - raw_points[a]
                    length = max(float(np.linalg.norm(vector)), 1e-5)
                min_length = raw_length * 0.78
                max_length = raw_length * 1.32
                target = min(max_length, max(min_length, length))
                if abs(target - length) <= raw_length * 0.015:
                    continue

                adjustment = vector / length * (target - length)
                a_movable = a in movable
                b_movable = b in movable
                if a_movable and b_movable:
                    corrected[a] -= adjustment * 0.5
                    corrected[b] += adjustment * 0.5
                elif a_movable:
                    corrected[a] -= adjustment
                elif b_movable:
                    corrected[b] += adjustment

    @staticmethod
    def _straighten_extended_finger(raw_points, corrected, chain, palm_width):
        mcp, pip, dip, tip = chain
        base = corrected[mcp]
        end = corrected[tip]
        line = end - base
        direct = float(np.linalg.norm(line[:2]))
        if direct <= max(0.018, palm_width * 0.20):
            return

        raw_lengths = [
            float(np.linalg.norm(raw_points[pip][:2] - raw_points[mcp][:2])),
            float(np.linalg.norm(raw_points[dip][:2] - raw_points[pip][:2])),
            float(np.linalg.norm(raw_points[tip][:2] - raw_points[dip][:2])),
        ]
        total = sum(raw_lengths)
        if total <= 1e-5:
            return

        straightness = direct / total
        if straightness < 0.62:
            return

        direction = line / max(float(np.linalg.norm(line)), 1e-5)
        pip_target = base + direction * (direct * raw_lengths[0] / total)
        dip_target = base + direction * (direct * (raw_lengths[0] + raw_lengths[1]) / total)

        pip_lateral = float(np.linalg.norm((corrected[pip] - pip_target)[:2]))
        dip_lateral = float(np.linalg.norm((corrected[dip] - dip_target)[:2]))
        lateral = max(pip_lateral, dip_lateral)
        lateral_start = palm_width * 0.018
        lateral_full = palm_width * 0.16
        if lateral <= lateral_start:
            return

        straight_blend = min(1.0, max(0.0, (straightness - 0.62) / 0.28))
        lateral_blend = min(1.0, max(0.0, (lateral - lateral_start) / max(lateral_full - lateral_start, 1e-5)))
        blend = straight_blend * lateral_blend
        if blend <= 0.0:
            return

        corrected[pip] = corrected[pip] * (1.0 - blend) + pip_target * blend
        corrected[dip] = corrected[dip] * (1.0 - blend) + dip_target * blend


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


def display_landmarks(hand_id, landmarks, display_points):
    state = display_points.get(hand_id)
    if state is None:
        state = ResponsiveDisplayHand(landmarks)
        display_points[hand_id] = state
    return state.update(landmarks)


def responsive_finger_display_landmarks(hand_id, landmarks, display_points):
    state = display_points.get(hand_id)
    if state is None or not isinstance(state, ResponsiveFingerDisplayHand):
        state = ResponsiveFingerDisplayHand(landmarks)
        display_points[hand_id] = state
    return state.update(landmarks)


def raw_display_landmarks(landmarks):
    return [{"x": float(point.x), "y": float(point.y), "z": float(point.z)} for point in landmarks]


def landmark_points_from_json(landmarks):
    return [LandmarkPoint(point["x"], point["y"], point["z"]) for point in landmarks]


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


class LatestCameraCapture:
    def __init__(self, cap):
        self.cap = cap
        self.condition = threading.Condition()
        self.running = False
        self.thread = None
        self.frame = None
        self.sequence = 0

    def start(self):
        self.running = True
        self.thread = threading.Thread(target=self._read_loop, name="LatestCameraCapture", daemon=True)
        self.thread.start()

    def stop(self):
        self.running = False
        with self.condition:
            self.condition.notify_all()
        if self.thread is not None and self.thread.is_alive():
            self.thread.join(timeout=1.0)
        self.thread = None

    def read_latest(self, last_sequence, timeout=1.0):
        deadline = time.time() + timeout
        with self.condition:
            while self.running and self.sequence == last_sequence:
                remaining = deadline - time.time()
                if remaining <= 0:
                    return False, None, last_sequence
                self.condition.wait(remaining)

            if self.frame is None or self.sequence == last_sequence:
                return False, None, last_sequence
            return True, self.frame.copy(), self.sequence

    def _read_loop(self):
        while self.running and self.cap.isOpened():
            ok, frame = self.cap.read()
            if not ok:
                time.sleep(0.005)
                continue

            with self.condition:
                self.frame = frame
                self.sequence += 1
                self.condition.notify_all()


def normalized_distance(a, b, scale):
    return float(np.linalg.norm(np.array([a.x - b.x, a.y - b.y, a.z - b.z])) / max(scale, 1e-5))


def clamp(value, low=-1.0, high=1.0):
    return max(low, min(high, value))


def decode_fourcc(value):
    code = int(value)
    chars = []
    for shift in (0, 8, 16, 24):
        byte = (code >> shift) & 0xFF
        chars.append(chr(byte) if 32 <= byte <= 126 else "?")
    return "".join(chars)


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
        "captureWidth": 0.0,
        "captureHeight": 0.0,
        "captureFps": 0.0,
        "captureFourcc": "",
        "displayMode": "",
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
    parser.add_argument("--capture-width", type=int, default=960, help="Camera capture and MediaPipe processing width.")
    parser.add_argument("--capture-height", type=int, default=540, help="Camera capture and MediaPipe processing height.")
    parser.add_argument("--camera-fourcc", default="MJPG", help="Preferred DirectShow camera FOURCC. Use empty string to leave driver default.")
    parser.add_argument("--video-width", type=int, default=640)
    parser.add_argument("--video-height", type=int, default=360)
    parser.add_argument("--camera-fps", type=int, default=60)
    parser.add_argument("--video-fps", type=float, default=18.0, help="JPEG camera stream frame rate. Gesture UDP still runs every processed frame.")
    parser.add_argument("--jpeg-quality", type=int, default=72)
    parser.add_argument("--lock-port", type=int, default=5007)
    parser.add_argument("--model-complexity", type=int, choices=(0, 1), default=1, help="MediaPipe Hands model complexity. 1 improves finger-joint fidelity; 0 is faster.")
    parser.add_argument("--track-roi", action="store_true", help="Use MediaPipe ROI tracking between detections. Faster, but fast finger-bend changes can feel less immediate.")
    parser.add_argument("--raw-display-landmarks", action="store_true", help="Send raw MediaPipe landmarks for the Unity skeleton overlay without finger-joint responsiveness correction.")
    parser.add_argument("--stable-display-landmarks", action="store_true", help="Apply light palm-anchor stabilization to display landmarks. Overrides the default responsive current-frame display mode.")
    parser.add_argument("--detection-confidence", type=float, default=0.60)
    parser.add_argument("--tracking-confidence", type=float, default=0.82)
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
    hands_model = mp.solutions.hands.Hands(
        static_image_mode=not args.track_roi,
        max_num_hands=2,
        model_complexity=args.model_complexity,
        min_detection_confidence=args.detection_confidence,
        min_tracking_confidence=args.tracking_confidence,
    )
    drawing = mp.solutions.drawing_utils
    cap = cv2.VideoCapture(args.camera, cv2.CAP_DSHOW)
    cap.set(cv2.CAP_PROP_BUFFERSIZE, 1)
    if args.camera_fourcc:
        fourcc = args.camera_fourcc[:4].ljust(4)
        cap.set(cv2.CAP_PROP_FOURCC, cv2.VideoWriter_fourcc(*fourcc))
    cap.set(cv2.CAP_PROP_FRAME_WIDTH, args.capture_width)
    cap.set(cv2.CAP_PROP_FRAME_HEIGHT, args.capture_height)
    cap.set(cv2.CAP_PROP_FPS, args.camera_fps)
    camera = LatestCameraCapture(cap)
    camera.start()
    neutral = None
    last_raw = {"roll": 0.0, "pitch": 0.0, "yaw": 0.0}
    pinch_latches = {}
    display_landmarks_by_hand = {}
    bridge_fps = 0.0
    processing_ms = 0.0
    frame_counter = 0
    diagnostics_start = time.time()
    next_video_send = 0.0
    last_camera_sequence = 0

    tracking_mode = "roi-tracking" if args.track_roi else "detect-every-frame"
    if args.stable_display_landmarks:
        display_mode = "stable-palm-anchor"
    elif args.raw_display_landmarks:
        display_mode = "raw-current-frame"
    else:
        display_mode = "responsive-finger-current-frame"
    actual_capture_width = cap.get(cv2.CAP_PROP_FRAME_WIDTH)
    actual_capture_height = cap.get(cv2.CAP_PROP_FRAME_HEIGHT)
    actual_capture_fps = cap.get(cv2.CAP_PROP_FPS)
    actual_fourcc = decode_fourcc(cap.get(cv2.CAP_PROP_FOURCC))
    print(f"Hand of God bridge: headless camera tracking started with MediaPipe model_complexity={args.model_complexity}, requested_capture={args.capture_width}x{args.capture_height}@{args.camera_fps} {args.camera_fourcc or 'driver-default'}, actual_capture={actual_capture_width:.0f}x{actual_capture_height:.0f}@{actual_capture_fps:.1f} {actual_fourcc}, video={args.video_width}x{args.video_height}@{args.video_fps}, mode={tracking_mode}, display={display_mode}. Use --preview for a debug window.")
    try:
        while cap.isOpened():
            frame_start = time.time()
            ok, frame, last_camera_sequence = camera.read_latest(last_camera_sequence)
            if not ok:
                break

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
                    if args.stable_display_landmarks:
                        display_landmark_json = display_landmarks(hand_id, hand.landmark, display_landmarks_by_hand)
                    elif args.raw_display_landmarks:
                        display_landmark_json = raw_display_landmarks(hand.landmark)
                    else:
                        display_landmark_json = responsive_finger_display_landmarks(hand_id, hand.landmark, display_landmarks_by_hand)
                    interaction_points = landmark_points_from_json(display_landmark_json)
                    hand_payload, raw = analyze_hand(interaction_points, label, score, hand_id, neutral, display_landmark_json)
                    hand_payload["displayLandmarks"] = display_landmark_json
                    last_raw = raw

                    distance = raw["pinchDistance"]
                    latched = pinch_latches.get(hand_id, False)
                    latched = distance < (0.92 if latched else 0.72)
                    pinch_latches[hand_id] = latched

                    hand_payload["pinch"] = latched
                    hand_payload["openPalm"] = hand_payload["openPalm"] and not latched
                    analyzed.append(hand_payload)
                    if args.preview:
                        drawing.draw_landmarks(frame, hand, mp.solutions.hands.HAND_CONNECTIONS)
            stale_ids = [hand_id for hand_id in set(display_landmarks_by_hand) | set(pinch_latches) if hand_id not in active_ids]
            for hand_id in stale_ids:
                display_landmarks_by_hand.pop(hand_id, None)
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
                    "captureWidth": actual_capture_width,
                    "captureHeight": actual_capture_height,
                    "captureFps": actual_capture_fps,
                    "captureFourcc": actual_fourcc,
                    "displayMode": display_mode,
                    "pinch": primary["pinch"],
                    "openPalm": primary["openPalm"],
                    "handCount": len(analyzed),
                    "hands": analyzed,
                    "timestamp": time.time(),
                })
            payload["bridgeFps"] = bridge_fps
            payload["processingMs"] = processing_ms
            payload["captureWidth"] = actual_capture_width
            payload["captureHeight"] = actual_capture_height
            payload["captureFps"] = actual_capture_fps
            payload["captureFourcc"] = actual_fourcc
            payload["displayMode"] = display_mode

            sock.sendto(json.dumps(payload).encode("utf-8"), (args.host, args.port))
            now = time.time()
            video_interval = 1.0 / max(args.video_fps, 1.0)
            if now >= next_video_send:
                video_frame = cv2.resize(frame, (args.video_width, args.video_height), interpolation=cv2.INTER_AREA)
                video.send_jpeg(video_frame, max(30, min(args.jpeg_quality, 95)))
                next_video_send = now + video_interval
            processing_ms = (time.time() - frame_start) * 1000.0
            frame_counter += 1
            elapsed = time.time() - diagnostics_start
            if elapsed >= 5.0:
                bridge_fps = frame_counter / elapsed
                print(f"Bridge diagnostics: fps={bridge_fps:.1f} processing={processing_ms:.1f}ms model_complexity={args.model_complexity} actual_capture={actual_capture_width:.0f}x{actual_capture_height:.0f}@{actual_capture_fps:.1f} {actual_fourcc} video={args.video_width}x{args.video_height}@{args.video_fps} mode={tracking_mode} display={display_mode}")
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
        camera.stop()
        cap.release()
        video.close()
        hands_model.close()
        lock_socket.close()
        if args.preview:
            cv2.destroyAllWindows()


if __name__ == "__main__":
    main()
