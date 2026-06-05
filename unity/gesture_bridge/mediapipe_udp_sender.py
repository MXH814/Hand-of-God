import argparse
import json
import socket
import time

import cv2
import mediapipe as mp
import numpy as np


def normalized_distance(a, b, scale):
    return float(np.linalg.norm(np.array([a.x - b.x, a.y - b.y, a.z - b.z])) / max(scale, 1e-5))


def clamp(value, low=-1.0, high=1.0):
    return max(low, min(high, value))


def analyze_hand(landmarks, neutral):
    wrist = landmarks[0]
    middle_mcp = landmarks[9]
    index_mcp = landmarks[5]
    pinky_mcp = landmarks[17]
    thumb_tip = landmarks[4]
    index_tip = landmarks[8]

    palm_width = normalized_distance(index_mcp, pinky_mcp, 1.0)
    pinch_distance = normalized_distance(thumb_tip, index_tip, palm_width)
    pinch = pinch_distance < 0.72

    raw_pitch = -(middle_mcp.y - wrist.y) * 3.0
    raw_roll = (index_mcp.y - pinky_mcp.y) * 4.0

    if neutral is not None:
        raw_roll -= neutral["roll"]
        raw_pitch -= neutral["pitch"]

    extended = 0
    for tip, pip in [(8, 6), (12, 10), (16, 14), (20, 18)]:
        if landmarks[tip].y < landmarks[pip].y:
            extended += 1

    return {
        "roll": clamp(raw_roll),
        "pitch": clamp(raw_pitch),
        "pinchX": clamp(index_tip.x, 0.0, 1.0),
        "pinchY": clamp(index_tip.y, 0.0, 1.0),
        "confidence": 1.0,
        "pinch": pinch,
        "openPalm": extended >= 3 and not pinch,
        "timestamp": time.time(),
    }, {"roll": raw_roll, "pitch": raw_pitch}


def main():
    parser = argparse.ArgumentParser(description="Send MediaPipe hand tilt frames to Unity over UDP.")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=5005)
    parser.add_argument("--camera", type=int, default=0)
    parser.add_argument("--mirror", action="store_true", default=True)
    args = parser.parse_args()

    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    hands = mp.solutions.hands.Hands(max_num_hands=1, min_detection_confidence=0.65, min_tracking_confidence=0.65)
    drawing = mp.solutions.drawing_utils
    cap = cv2.VideoCapture(args.camera)
    neutral = None
    last_raw = {"roll": 0.0, "pitch": 0.0}

    print("Press C to calibrate neutral hand pose, Q to quit.")
    while cap.isOpened():
        ok, frame = cap.read()
        if not ok:
            break

        if args.mirror:
            frame = cv2.flip(frame, 1)

        rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        result = hands.process(rgb)
        payload = {
            "roll": 0.0,
            "pitch": 0.0,
            "pinchX": 0.5,
            "pinchY": 0.5,
            "confidence": 0.0,
            "pinch": False,
            "openPalm": False,
            "timestamp": time.time(),
        }

        if result.multi_hand_landmarks:
            hand = result.multi_hand_landmarks[0]
            payload, last_raw = analyze_hand(hand.landmark, neutral)
            drawing.draw_landmarks(frame, hand, mp.solutions.hands.HAND_CONNECTIONS)

        sock.sendto(json.dumps(payload).encode("utf-8"), (args.host, args.port))

        cv2.putText(frame, "C calibrate | Q quit", (18, 34), cv2.FONT_HERSHEY_SIMPLEX, 0.8, (80, 240, 180), 2)
        cv2.putText(frame, f"roll {payload['roll']:.2f} pitch {payload['pitch']:.2f}", (18, 66), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (80, 240, 180), 2)
        cv2.imshow("Hand of God Gesture Bridge", frame)
        key = cv2.waitKey(1) & 0xFF
        if key == ord("q"):
            break
        if key == ord("c"):
            neutral = dict(last_raw)
            print(f"Calibrated neutral pose: {neutral}")

    cap.release()
    cv2.destroyAllWindows()


if __name__ == "__main__":
    main()
