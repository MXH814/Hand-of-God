import argparse
import json
import socket
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


def analyze_hand(landmarks, handedness, score, hand_id, neutral):
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
        "landmarks": [{"x": float(p.x), "y": float(p.y), "z": float(p.z)} for p in landmarks],
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
    args = parser.parse_args()

    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    hands_model = mp.solutions.hands.Hands(max_num_hands=2, min_detection_confidence=0.65, min_tracking_confidence=0.65)
    drawing = mp.solutions.drawing_utils
    cap = cv2.VideoCapture(args.camera)
    neutral = None
    last_raw = {"roll": 0.0, "pitch": 0.0, "yaw": 0.0}
    pinch_latches = {}
    smooth_points = {}

    print("Hand of God bridge: C calibrates neutral pose, Q quits.")
    while cap.isOpened():
        ok, frame = cap.read()
        if not ok:
            break

        if args.mirror:
            frame = cv2.flip(frame, 1)

        result = hands_model.process(cv2.cvtColor(frame, cv2.COLOR_BGR2RGB))
        payload = neutral_payload()
        analyzed = []

        if result.multi_hand_landmarks:
            handedness_list = result.multi_handedness or []
            for index, hand in enumerate(result.multi_hand_landmarks[:2]):
                category = handedness_list[index].classification[0] if index < len(handedness_list) else None
                label = category.label if category else "Unknown"
                score = category.score if category else 1.0
                hand_id = f"{label}-{index}"
                hand_payload, raw = analyze_hand(hand.landmark, label, score, hand_id, neutral)
                last_raw = raw

                distance = raw["pinchDistance"]
                latched = pinch_latches.get(hand_id, False)
                latched = distance < (0.92 if latched else 0.72)
                pinch_latches[hand_id] = latched

                previous = smooth_points.get(hand_id, (hand_payload["pinchX"], hand_payload["pinchY"], hand_payload["indexX"], hand_payload["indexY"]))
                smoothed = (
                    previous[0] * 0.72 + hand_payload["pinchX"] * 0.28,
                    previous[1] * 0.72 + hand_payload["pinchY"] * 0.28,
                    previous[2] * 0.72 + hand_payload["indexX"] * 0.28,
                    previous[3] * 0.72 + hand_payload["indexY"] * 0.28,
                )
                smooth_points[hand_id] = smoothed

                hand_payload["pinch"] = latched
                hand_payload["openPalm"] = hand_payload["openPalm"] and not latched
                hand_payload["pinchX"], hand_payload["pinchY"], hand_payload["indexX"], hand_payload["indexY"] = smoothed
                analyzed.append(hand_payload)
                drawing.draw_landmarks(frame, hand, mp.solutions.hands.HAND_CONNECTIONS)

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
                "pinch": primary["pinch"],
                "openPalm": primary["openPalm"],
                "handCount": len(analyzed),
                "hands": analyzed,
                "timestamp": time.time(),
            })

        sock.sendto(json.dumps(payload).encode("utf-8"), (args.host, args.port))

        cv2.putText(frame, "C calibrate neutral | Q quit", (18, 34), cv2.FONT_HERSHEY_SIMPLEX, 0.8, (80, 240, 180), 2)
        cv2.putText(frame, f"hands {payload['handCount']} pinch {payload['pinchDistance']:.2f}", (18, 66), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (80, 240, 180), 2)
        cv2.imshow("Hand of God Gesture Bridge", frame)
        key = cv2.waitKey(1) & 0xFF
        if key == ord("q"):
            break
        if key == ord("c"):
            neutral = dict(last_raw)
            print(f"Calibrated bridge neutral pose: {neutral}")

    cap.release()
    cv2.destroyAllWindows()


if __name__ == "__main__":
    main()
