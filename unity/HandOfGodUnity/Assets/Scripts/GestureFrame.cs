using System;
using UnityEngine;

namespace HandOfGod.Gestures
{
    [Serializable]
    public struct GestureLandmark
    {
        public float x;
        public float y;
        public float z;
    }

    [Serializable]
    public struct GestureHandFrame
    {
        public string id;
        public string handedness;
        public float score;
        public float pinchX;
        public float pinchY;
        public float indexX;
        public float indexY;
        public float pinchDistance;
        public float palmSpan;
        public float palmRoll;
        public float palmPitch;
        public float palmYaw;
        public bool pinch;
        public bool openPalm;
        public bool thumbExtended;
        public bool indexExtended;
        public bool middleExtended;
        public bool ringExtended;
        public bool pinkyExtended;
        public GestureLandmark[] landmarks;
    }

    [Serializable]
    public struct GestureFrame
    {
        public float roll;
        public float pitch;
        public float pinchX;
        public float pinchY;
        public float indexX;
        public float indexY;
        public float pinchDistance;
        public float palmSpan;
        public float palmRoll;
        public float palmPitch;
        public float palmYaw;
        public float confidence;
        public bool pinch;
        public bool openPalm;
        public int handCount;
        public GestureHandFrame[] hands;
        public double timestamp;

        public static GestureFrame Neutral => new GestureFrame
        {
            roll = 0f,
            pitch = 0f,
            pinchX = 0.5f,
            pinchY = 0.5f,
            indexX = 0.5f,
            indexY = 0.5f,
            pinchDistance = 1f,
            palmSpan = 0f,
            palmRoll = 0f,
            palmPitch = 0f,
            palmYaw = 0f,
            confidence = 0f,
            pinch = false,
            openPalm = false,
            handCount = 0,
            hands = Array.Empty<GestureHandFrame>(),
            timestamp = 0d,
        };
    }

    public static class GestureFrameUtility
    {
        public static GestureFrame Clamp(GestureFrame frame)
        {
            frame.roll = Mathf.Clamp(frame.roll, -1f, 1f);
            frame.pitch = Mathf.Clamp(frame.pitch, -1f, 1f);
            frame.pinchX = Mathf.Clamp01(frame.pinchX);
            frame.pinchY = Mathf.Clamp01(frame.pinchY);
            frame.indexX = Mathf.Clamp01(frame.indexX);
            frame.indexY = Mathf.Clamp01(frame.indexY);
            frame.pinchDistance = Mathf.Max(frame.pinchDistance, 0f);
            frame.palmSpan = Mathf.Max(frame.palmSpan, 0f);
            frame.palmRoll = Mathf.Clamp(frame.palmRoll, -1f, 1f);
            frame.palmPitch = Mathf.Clamp(frame.palmPitch, -1f, 1f);
            frame.palmYaw = Mathf.Clamp(frame.palmYaw, -1f, 1f);
            frame.confidence = Mathf.Clamp01(frame.confidence);
            frame.hands ??= Array.Empty<GestureHandFrame>();
            frame.handCount = Mathf.Max(frame.handCount, frame.hands.Length);
            return frame;
        }
    }
}
