using System;
using UnityEngine;

namespace HandOfGod.Gestures
{
    [Serializable]
    public struct GestureFrame
    {
        public float roll;
        public float pitch;
        public float pinchX;
        public float pinchY;
        public float confidence;
        public bool pinch;
        public bool openPalm;
        public double timestamp;

        public static GestureFrame Neutral => new GestureFrame
        {
            roll = 0f,
            pitch = 0f,
            pinchX = 0.5f,
            pinchY = 0.5f,
            confidence = 0f,
            pinch = false,
            openPalm = false,
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
            frame.confidence = Mathf.Clamp01(frame.confidence);
            return frame;
        }
    }
}
