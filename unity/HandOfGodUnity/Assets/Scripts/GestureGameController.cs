using HandOfGod.Gestures;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

namespace HandOfGod.Gameplay
{
    public sealed class GestureGameController : MonoBehaviour
    {
        private enum GameMode
        {
            CalibrationOpen,
            CalibrationPinch,
            Menu,
            Level0,
            Level1,
            Level2,
            Level3,
            Level4,
            Pass,
        }

        private enum TutorialStage
        {
            FindHands,
            OneHandDrag,
            TwoHandRotate,
            BridgePull,
            PalmActivate,
            MapControl,
            AirflowDirection,
            DrawCreate,
            DrawErase,
            MirrorRotate,
            MagnetPolarity,
            Complete,
        }

        private enum TutorialDrawState
        {
            WaitingStart,
            StartConfirm,
            Drawing,
            FinishConfirm,
            CancelConfirm,
        }

        private enum TutorialDrawShape
        {
            Circle,
            Rectangle,
            Triangle,
        }

        private enum Level1Stage
        {
            ClearBlock,
            JoinBridge,
            RotateGate,
            ActivateSeal,
            RunToGoal,
        }

        private enum Level4Stage
        {
            LightGuide,
            MagneticTurntable,
            RunToGoal,
        }

        private enum Level3Stage
        {
            CreateBridgeObject,
            PlaceCreatedObject,
            EraseLockBlock,
            RunToGoal,
        }

        private struct TutorialDrawShapeResult
        {
            public TutorialDrawShape Shape;
            public Vector3 Center;
            public Vector3 Scale;
            public Quaternion Rotation;
            public float Radius;
            public float Height;
            public Vector2 TriangleA;
            public Vector2 TriangleB;
            public Vector2 TriangleC;
        }

        private struct TutorialDrawShapeScores
        {
            public float Circle;
            public float Rectangle;
            public float Triangle;
            public TutorialDrawShape BestShape;
            public float BestScore;
            public float SecondScore;
        }

        private readonly struct UiPointer
        {
            public readonly string Id;
            public readonly Vector2 Position;
            public readonly bool Pinching;

            public UiPointer(string id, Vector2 position, bool pinching)
            {
                Id = id;
                Position = position;
                Pinching = pinching;
            }
        }

        private readonly struct Level3TransformOverride
        {
            public readonly string Name;
            public readonly Vector3 LocalPosition;
            public readonly Quaternion LocalRotation;
            public readonly Vector3 LocalScale;

            public Level3TransformOverride(string name, Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
            {
                Name = name;
                LocalPosition = localPosition;
                LocalRotation = localRotation;
                LocalScale = localScale;
            }
        }

        private sealed class TutorialDrawnShapeMarker : MonoBehaviour
        {
            public TutorialDrawShape Shape;
        }

        private const float CalibrationHoldSeconds = 1f;
        private const float MenuDwellSeconds = 0.85f;
        private const float SafeDwellSeconds = 1f;
        private const float Level1RoadCenterY = 1f;
        private const float Level1RoadAngleDegrees = -8f;
        private const float Level1RotateBridgeStartYaw = 90f;
        private const float TutorialDrawConfirmSeconds = 2f;
        private const float Level3RoadAngleDegrees = -4.2f;
        private const float Level3RoadBaseY = 0.28f;
        private const float Level3RoadSlope = -0.0735f;
        private const float Level3RoadThickness = 0.22f;
        private const float Level3SlidePlatformThickness = 0.18f;
        private const float Level3SlideBridgeParkZ = 2.42f;
        private const float Level3SlideBridgeFinalZ = 0f;
        private const float Level3SlideBridgeCenterX = 1.15f;
        private const float Level3SlideBridgeWidth = 1.12f;
        private static readonly Level3TransformOverride[] Level3SceneTransformOverrides =
        {
            new Level3TransformOverride("Kenney template-detail", new Vector3(4.45f, -0.287825f, 0f), new Quaternion(0f, 0f, 0f, 1f), new Vector3(0.88f, 0.3f, 0.88f)),
            new Level3TransformOverride("Level03 Creation Erasure", new Vector3(0f, 0f, 0f), new Quaternion(0f, 0f, 0f, 1f), new Vector3(1f, 1f, 1f)),
            new Level3TransformOverride("level2 goal inner halo", new Vector3(4.45f, 0.072175f, 0f), new Quaternion(0f, 0f, 0f, 1f), new Vector3(1.1f, 1.1f, 1.1f)),
            new Level3TransformOverride("level2 goal outer halo", new Vector3(4.45f, -0.027825f, 0f), new Quaternion(0f, 0f, 0f, 1f), new Vector3(1f, 1f, 1f)),
            new Level3TransformOverride("Level2 Portal Accent Light", new Vector3(0.6f, 2.8f, -1.4f), new Quaternion(0f, 0f, 0f, 1f), new Vector3(1f, 1f, 1f)),
            new Level3TransformOverride("level3 created objects", new Vector3(0f, 0f, 0f), new Quaternion(0f, 0f, 0f, 1f), new Vector3(1f, 1f, 1f)),
            new Level3TransformOverride("level3 creation bridge patch", new Vector3(-2.12f, 0.12507f, 0f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(1.92f, 0.16f, 1.62f)),
            new Level3TransformOverride("level3 creation gate", new Vector3(-2.82f, 0.83652f, 0f), new Quaternion(0f, 0f, 0f, 1f), new Vector3(1f, 1f, 1f)),
            new Level3TransformOverride("level3 creation gate cyan lock core", new Vector3(0.024f, -0.094f, 0f), new Quaternion(0f, 0f, 0f, 1f), new Vector3(0.13f, 0.22f, 0.22f)),
            new Level3TransformOverride("level3 creation gate glow lintel", new Vector3(0.024f, 0.186f, 0f), new Quaternion(0f, 0f, 0f, 1f), new Vector3(0.16f, 0.08f, 1.74f)),
            new Level3TransformOverride("level3 creation gate lower rail", new Vector3(0.024f, -0.454f, 0f), new Quaternion(0f, 0f, 0f, 1f), new Vector3(0.16f, 0.08f, 1.74f)),
            new Level3TransformOverride("level3 creation gate metal bar 0", new Vector3(0.024f, -0.154f, -0.696f), new Quaternion(0f, 0f, 0f, 1f), new Vector3(0.1f, 0.86f, 0.045f)),
            new Level3TransformOverride("level3 creation gate metal bar 1", new Vector3(0.024f, -0.154f, -0.464f), new Quaternion(0f, 0f, 0f, 1f), new Vector3(0.1f, 0.86f, 0.045f)),
            new Level3TransformOverride("level3 creation gate metal bar 2", new Vector3(0.024f, -0.154f, -0.232f), new Quaternion(0f, 0f, 0f, 1f), new Vector3(0.1f, 0.86f, 0.045f)),
            new Level3TransformOverride("level3 creation gate metal bar 3", new Vector3(0.024f, -0.154f, 0f), new Quaternion(0f, 0f, 0f, 1f), new Vector3(0.1f, 0.86f, 0.045f)),
            new Level3TransformOverride("level3 creation gate metal bar 4", new Vector3(0.024f, -0.154f, 0.232f), new Quaternion(0f, 0f, 0f, 1f), new Vector3(0.1f, 0.86f, 0.045f)),
            new Level3TransformOverride("level3 creation gate metal bar 5", new Vector3(0.024f, -0.154f, 0.464f), new Quaternion(0f, 0f, 0f, 1f), new Vector3(0.1f, 0.86f, 0.045f)),
            new Level3TransformOverride("level3 creation gate metal bar 6", new Vector3(0.024f, -0.154f, 0.696f), new Quaternion(0f, 0f, 0f, 1f), new Vector3(0.1f, 0.86f, 0.045f)),
            new Level3TransformOverride("level3 cube platform", new Vector3(-2.113f, 0.285f, 1.3f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(1.32f, 0.06f, 0.98f)),
            new Level3TransformOverride("level3 cube platform icon", new Vector3(-2.131f, 0.307f, 1.3f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(0.24f, 0.05f, 0.24f)),
            new Level3TransformOverride("level3 cube platform rune corner glow 0", new Vector3(-2.68f, 0.36507f, 0.9f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(0.13f, 0.028f, 0.13f)),
            new Level3TransformOverride("level3 cube platform rune corner glow 1", new Vector3(-2.68f, 0.36507f, 1.7f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(0.13f, 0.028f, 0.13f)),
            new Level3TransformOverride("level3 cube platform rune corner glow 2", new Vector3(-1.563f, 0.276f, 0.9f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(0.13f, 0.028f, 0.13f)),
            new Level3TransformOverride("level3 cube platform rune corner glow 3", new Vector3(-1.563f, 0.276f, 1.7f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(0.13f, 0.028f, 0.13f)),
            new Level3TransformOverride("level3 finish centerline", new Vector3(3.25f, -0.159625f, 0f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(2.45f, 0.018f, 0.055f)),
            new Level3TransformOverride("level3 finish floor", new Vector3(3.25f, -0.289625f, 0f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(3.15f, 0.22f, 1.78f)),
            new Level3TransformOverride("level3 finish railing side 0 lower rail", new Vector3(3.245f, -0.054737f, -0.86f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(2.85f, 0.045f, 0.05f)),
            new Level3TransformOverride("level3 finish railing side 0 support 0", new Vector3(1.82f, 0.06f, -0.86f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(0.055f, 0.28f, 0.055f)),
            new Level3TransformOverride("level3 finish railing side 0 support 1", new Vector3(2.77f, -0.009825f, -0.86f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(0.055f, 0.28f, 0.055f)),
            new Level3TransformOverride("level3 finish railing side 0 support 2", new Vector3(3.72f, -0.07965f, -0.86f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(0.055f, 0.28f, 0.055f)),
            new Level3TransformOverride("level3 finish railing side 0 support 3", new Vector3(4.67f, -0.149475f, -0.86f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(0.055f, 0.28f, 0.055f)),
            new Level3TransformOverride("level3 finish railing side 0 upper rail", new Vector3(3.245f, 0.075263f, -0.86f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(2.85f, 0.055f, 0.06f)),
            new Level3TransformOverride("level3 finish railing side 1 lower rail", new Vector3(3.245f, -0.063f, 0.853f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(2.85f, 0.045f, 0.05f)),
            new Level3TransformOverride("level3 finish railing side 1 support 0", new Vector3(1.82f, 0.051737f, 0.853f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(0.055f, 0.28f, 0.055f)),
            new Level3TransformOverride("level3 finish railing side 1 support 1", new Vector3(2.77f, -0.018088f, 0.853f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(0.055f, 0.28f, 0.055f)),
            new Level3TransformOverride("level3 finish railing side 1 support 2", new Vector3(3.72f, -0.087913f, 0.853f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(0.055f, 0.28f, 0.055f)),
            new Level3TransformOverride("level3 finish railing side 1 support 3", new Vector3(4.67f, -0.157738f, 0.853f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(0.055f, 0.28f, 0.055f)),
            new Level3TransformOverride("level3 finish railing side 1 upper rail", new Vector3(3.245f, 0.067f, 0.853f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(2.85f, 0.055f, 0.06f)),
            new Level3TransformOverride("Level3 Goal Trigger - Sacred Altar", new Vector3(4.45f, -0.157825f, 0f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(0.7f, 0.11f, 0.7f)),
            new Level3TransformOverride("Level3 Golden Physics Ball", new Vector3(-4.56f, 0.76441f, 0f), new Quaternion(0f, 0f, 0f, 1f), new Vector3(0.46f, 0.46f, 0.46f)),
            new Level3TransformOverride("level3 middle centerline", new Vector3(-0.42f, 0.11012f, 0f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(1.15f, 0.018f, 0.055f)),
            new Level3TransformOverride("level3 middle floor", new Vector3(-0.42f, -0.01988f, 0f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(2.05f, 0.22f, 1.78f)),
            new Level3TransformOverride("level3 middle railing side 0 lower rail", new Vector3(-0.424667f, 0.21107f, -0.84f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(1.72f, 0.045f, 0.05f)),
            new Level3TransformOverride("level3 middle railing side 0 support 0", new Vector3(-1.284f, 0.298f, -0.836f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(0.055f, 0.28f, 0.055f)),
            new Level3TransformOverride("level3 middle railing side 0 support 1", new Vector3(-0.711333f, 0.24214f, -0.84f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(0.055f, 0.28f, 0.055f)),
            new Level3TransformOverride("level3 middle railing side 0 support 2", new Vector3(-0.138f, 0.2f, -0.84f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(0.055f, 0.28f, 0.055f)),
            new Level3TransformOverride("level3 middle railing side 0 support 3", new Vector3(0.435333f, 0.15786f, -0.84f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(0.055f, 0.28f, 0.055f)),
            new Level3TransformOverride("level3 middle railing side 0 upper rail", new Vector3(-0.424667f, 0.34107f, -0.84f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(1.72f, 0.055f, 0.06f)),
            new Level3TransformOverride("level3 middle railing side 1 lower rail", new Vector3(-0.426f, 0.19421f, 0.851f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(1.72f, 0.045f, 0.05f)),
            new Level3TransformOverride("level3 middle railing side 1 support 0", new Vector3(-1.286f, 0.26742f, 0.851f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(0.055f, 0.28f, 0.055f)),
            new Level3TransformOverride("level3 middle railing side 1 support 1", new Vector3(-0.712667f, 0.22528f, 0.851f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(0.055f, 0.28f, 0.055f)),
            new Level3TransformOverride("level3 middle railing side 1 support 2", new Vector3(-0.139333f, 0.18314f, 0.851f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(0.055f, 0.28f, 0.055f)),
            new Level3TransformOverride("level3 middle railing side 1 support 3", new Vector3(0.434f, 0.141f, 0.851f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(0.055f, 0.28f, 0.055f)),
            new Level3TransformOverride("level3 middle railing side 1 upper rail", new Vector3(-0.426f, 0.32421f, 0.851f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(1.72f, 0.055f, 0.06f)),
            new Level3TransformOverride("level3 side parked sliding bridge", new Vector3(1.15f, -0.115329f, 2.42f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(1.12f, 0.18f, 1.78f)),
            new Level3TransformOverride("level3 slide gate", new Vector3(0.78f, 0.57192f, 0f), new Quaternion(0f, 0f, 0f, 1f), new Vector3(1f, 1f, 1f)),
            new Level3TransformOverride("level3 slide gate cyan lock core", new Vector3(-0.234f, -0.082f, 0f), new Quaternion(0f, 0f, 0f, 1f), new Vector3(0.13f, 0.22f, 0.22f)),
            new Level3TransformOverride("level3 slide gate glow lintel", new Vector3(-0.234f, 0.198f, 0f), new Quaternion(0f, 0f, 0f, 1f), new Vector3(0.16f, 0.08f, 1.74f)),
            new Level3TransformOverride("level3 slide gate lower rail", new Vector3(-0.234f, -0.442f, 0f), new Quaternion(0f, 0f, 0f, 1f), new Vector3(0.16f, 0.08f, 1.74f)),
            new Level3TransformOverride("level3 slide gate metal bar 0", new Vector3(-0.234f, -0.142f, -0.696f), new Quaternion(0f, 0f, 0f, 1f), new Vector3(0.1f, 0.86f, 0.045f)),
            new Level3TransformOverride("level3 slide gate metal bar 1", new Vector3(-0.234f, -0.142f, -0.464f), new Quaternion(0f, 0f, 0f, 1f), new Vector3(0.1f, 0.86f, 0.045f)),
            new Level3TransformOverride("level3 slide gate metal bar 2", new Vector3(-0.234f, -0.142f, -0.232f), new Quaternion(0f, 0f, 0f, 1f), new Vector3(0.1f, 0.86f, 0.045f)),
            new Level3TransformOverride("level3 slide gate metal bar 3", new Vector3(-0.234f, -0.142f, 0f), new Quaternion(0f, 0f, 0f, 1f), new Vector3(0.1f, 0.86f, 0.045f)),
            new Level3TransformOverride("level3 slide gate metal bar 4", new Vector3(-0.234f, -0.142f, 0.232f), new Quaternion(0f, 0f, 0f, 1f), new Vector3(0.1f, 0.86f, 0.045f)),
            new Level3TransformOverride("level3 slide gate metal bar 5", new Vector3(-0.234f, -0.142f, 0.464f), new Quaternion(0f, 0f, 0f, 1f), new Vector3(0.1f, 0.86f, 0.045f)),
            new Level3TransformOverride("level3 slide gate metal bar 6", new Vector3(-0.234f, -0.142f, 0.696f), new Quaternion(0f, 0f, 0f, 1f), new Vector3(0.1f, 0.86f, 0.045f)),
            new Level3TransformOverride("level3 slide path chevron lower 0", new Vector3(1.303f, -0.035f, 2.939f), new Quaternion(-0.008865f, -0.241759f, -0.035555f, 0.969644f), new Vector3(0.36f, 0.018f, 0.045f)),
            new Level3TransformOverride("level3 slide path chevron lower 1", new Vector3(1.303f, -0.035f, 2.539f), new Quaternion(-0.008865f, -0.241759f, -0.035555f, 0.969644f), new Vector3(0.36f, 0.018f, 0.045f)),
            new Level3TransformOverride("level3 slide path chevron lower 2", new Vector3(1.303f, -0.035f, 2.139f), new Quaternion(-0.008865f, -0.241759f, -0.035555f, 0.969644f), new Vector3(0.36f, 0.018f, 0.045f)),
            new Level3TransformOverride("level3 slide path chevron lower 3", new Vector3(1.303f, -0.035f, 1.739f), new Quaternion(-0.008865f, -0.241759f, -0.035555f, 0.969644f), new Vector3(0.36f, 0.018f, 0.045f)),
            new Level3TransformOverride("level3 slide path chevron lower 4", new Vector3(1.303f, -0.035f, 1.339f), new Quaternion(-0.008865f, -0.241759f, -0.035555f, 0.969644f), new Vector3(0.36f, 0.018f, 0.045f)),
            new Level3TransformOverride("level3 slide path chevron upper 0", new Vector3(1.006f, -0.013f, 2.936f), new Quaternion(0.008865f, 0.241759f, -0.035555f, 0.969644f), new Vector3(0.36f, 0.018f, 0.045f)),
            new Level3TransformOverride("level3 slide path chevron upper 1", new Vector3(1.006f, -0.013f, 2.536f), new Quaternion(0.008865f, 0.241759f, -0.035555f, 0.969644f), new Vector3(0.36f, 0.018f, 0.045f)),
            new Level3TransformOverride("level3 slide path chevron upper 2", new Vector3(1.006f, -0.013f, 2.136f), new Quaternion(0.008865f, 0.241759f, -0.035555f, 0.969644f), new Vector3(0.36f, 0.018f, 0.045f)),
            new Level3TransformOverride("level3 slide path chevron upper 3", new Vector3(1.006f, -0.013f, 1.736f), new Quaternion(0.008865f, 0.241759f, -0.035555f, 0.969644f), new Vector3(0.36f, 0.018f, 0.045f)),
            new Level3TransformOverride("level3 slide path chevron upper 4", new Vector3(1.006f, -0.013f, 1.336f), new Quaternion(0.008865f, 0.241759f, -0.035555f, 0.969644f), new Vector3(0.36f, 0.018f, 0.045f)),
            new Level3TransformOverride("level3 sliding bridge left rail", new Vector3(0.541f, -0.135f, 1.863f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(0.08f, 0.1f, 3f)),
            new Level3TransformOverride("level3 sliding bridge lock block", new Vector3(1.152f, 0.169f, 1.25f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(0.96f, 0.72f, 0.5f)),
            new Level3TransformOverride("level3 sliding bridge parked slot", new Vector3(1.111f, -0.205f, 2.09f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(1.26f, 0.035f, 2.6f)),
            new Level3TransformOverride("level3 sliding bridge right rail", new Vector3(1.741f, -0.195f, 1.866f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(0.08f, 0.1f, 3f)),
            new Level3TransformOverride("level3 sphere platform", new Vector3(-2.12f, 0.28507f, -1.3f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(1.32f, 0.06f, 0.98f)),
            new Level3TransformOverride("level3 sphere platform icon", new Vector3(-2.132f, 0.319f, -1.3f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(0.28f, 0.05f, 0.28f)),
            new Level3TransformOverride("level3 sphere platform rune corner glow 0", new Vector3(-2.68f, 0.36507f, -1.7f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(0.13f, 0.028f, 0.13f)),
            new Level3TransformOverride("level3 sphere platform rune corner glow 1", new Vector3(-2.68f, 0.36507f, -0.9f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(0.13f, 0.028f, 0.13f)),
            new Level3TransformOverride("level3 sphere platform rune corner glow 2", new Vector3(-1.563f, 0.276f, -1.7f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(0.13f, 0.028f, 0.13f)),
            new Level3TransformOverride("level3 sphere platform rune corner glow 3", new Vector3(-1.563f, 0.276f, -0.9f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(0.13f, 0.028f, 0.13f)),
            new Level3TransformOverride("level3 start centerline", new Vector3(-3.86f, 0.36296f, 0f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(1.75f, 0.018f, 0.055f)),
            new Level3TransformOverride("level3 start floor", new Vector3(-3.86f, 0.23296f, 0f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(2.25f, 0.22f, 1.78f)),
            new Level3TransformOverride("level3 start railing side 0 lower rail", new Vector3(-3.864333f, 0.479888f, -0.849f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(2.05f, 0.045f, 0.05f)),
            new Level3TransformOverride("level3 start railing side 0 support 0", new Vector3(-4.889333f, 0.565225f, -0.849f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(0.055f, 0.28f, 0.055f)),
            new Level3TransformOverride("level3 start railing side 0 support 1", new Vector3(-4.206f, 0.515f, -0.849f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(0.055f, 0.28f, 0.055f)),
            new Level3TransformOverride("level3 start railing side 0 support 2", new Vector3(-3.522667f, 0.464775f, -0.849f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(0.055f, 0.28f, 0.055f)),
            new Level3TransformOverride("level3 start railing side 0 support 3", new Vector3(-2.839334f, 0.41455f, -0.849f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(0.055f, 0.28f, 0.055f)),
            new Level3TransformOverride("level3 start railing side 0 upper rail", new Vector3(-3.864333f, 0.609888f, -0.849f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(2.05f, 0.055f, 0.06f)),
            new Level3TransformOverride("level3 start railing side 1 lower rail", new Vector3(-3.865f, 0.465337f, 0.842f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(2.05f, 0.045f, 0.05f)),
            new Level3TransformOverride("level3 start railing side 1 support 0", new Vector3(-4.889999f, 0.550675f, 0.842f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(0.055f, 0.28f, 0.055f)),
            new Level3TransformOverride("level3 start railing side 1 support 1", new Vector3(-4.206666f, 0.50045f, 0.842f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(0.055f, 0.28f, 0.055f)),
            new Level3TransformOverride("level3 start railing side 1 support 2", new Vector3(-3.523333f, 0.450225f, 0.842f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(0.055f, 0.28f, 0.055f)),
            new Level3TransformOverride("level3 start railing side 1 support 3", new Vector3(-2.84f, 0.4f, 0.842f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(0.055f, 0.28f, 0.055f)),
            new Level3TransformOverride("level3 start railing side 1 upper rail", new Vector3(-3.865f, 0.595338f, 0.842f), new Quaternion(0f, 0f, -0.036644f, 0.999328f), new Vector3(2.05f, 0.055f, 0.06f)),
            new Level3TransformOverride("level3 void shadow plinth", new Vector3(0f, -0.72f, 0f), new Quaternion(0f, 0f, 0f, 1f), new Vector3(11.4f, 0.42f, 5.1f)),
        };
        private static readonly int[] HandConnectionPairs =
        {
            0, 1, 1, 2, 2, 3, 3, 4,
            0, 5, 5, 6, 6, 7, 7, 8,
            5, 9, 9, 10, 10, 11, 11, 12,
            9, 13, 13, 14, 14, 15, 15, 16,
            13, 17, 17, 18, 18, 19, 19, 20,
            0, 17,
        };

        private GestureUdpReceiver receiver;
        private CameraFrameReceiver cameraFrames;
        private Transform lobbyRoot;
        private Transform levelRoot;
        private Camera mainCamera;
        private Material stone;
        private Material paleStone;
        private Material darkStone;
        private Material cliffStone;
        private Material brass;
        private Material tealGlow;
        private Material amberGlow;
        private Material ballMaterial;
        private Material boxIdle;
        private Material boxHover;
        private Material boxHeldMaterial;
        private Material level2FloorMaterial;
        private Material level2WallMaterial;
        private Material level2TrimMaterial;
        private Material level2PortalCoreMaterial;
        private Material level2WindMaterial;
        private Material level2WindRibbonMaterial;
        private Material level2WindMistMaterial;
        private Material level2PortalTwirlMaterial;
        private Material level2RuneMaterial;
        private Material level4BeamAmberMaterial;
        private Material level4BeamTealMaterial;
        private Material level4BeamHaloAmberMaterial;
        private Material level4BeamHaloTealMaterial;
        private Material cameraBackgroundMaterial;
        private Transform cameraBackgroundPlane;

        private GameMode mode = GameMode.CalibrationOpen;
        private GameMode lastLevel = GameMode.Level1;
        private float holdStart = -1f;
        private float pinchThreshold = 0.56f;
        private float pinchSampleSum;
        private int pinchSampleCount;
        private readonly Dictionary<string, float> hoverStartsByPointer = new Dictionary<string, float>();
        private readonly Dictionary<string, Vector2[]> skeletonScreenCache = new Dictionary<string, Vector2[]>();
        private GameObject labObject;
        private Rigidbody labBody;
        private Renderer labRenderer;
        private Vector3 labGrabOffset;
        private bool labHeld;
        private bool labCompleted;
        private TutorialStage tutorialStage = TutorialStage.FindHands;
        private bool tutorialStageSucceeded;
        private bool tutorialObjectMoved;
        private bool tutorialObjectRotated;
        private bool tutorialBridgePulled;
        private bool tutorialPalmActivated;
        private bool tutorialMapAdjusted;
        private bool tutorialAirflowDirected;
        private bool tutorialDrawCreated;
        private bool tutorialDrawErased;
        private bool tutorialDrawInvalid;
        private bool tutorialMirrorRotated;
        private bool tutorialMagnetPolarityChanged;
        private int tutorialAirflowPreviewDirection;
        private int tutorialMagnetPreviewPolarity;
        private int tutorialMagnetReversalCount;
        private int tutorialPendingMagnetDirection;
        private float tutorialPendingMagnetDirectionStart = -1f;
        private Vector3 tutorialDragStart;
        private TutorialDrawState tutorialDrawState = TutorialDrawState.WaitingStart;
        private float tutorialDrawHoldStart = -1f;
        private bool tutorialDrawSawFingerSeparation;
        private string tutorialDrawMessage = "";
        private float tutorialDrawMessageUntil = -1f;
        private Transform tutorialDrawnRoot;
        private System.Collections.Generic.List<GameObject> tutorialDrawnObjects = new System.Collections.Generic.List<GameObject>();
        private System.Collections.Generic.List<Vector2> tutorialDrawScreenPoints = new System.Collections.Generic.List<Vector2>();
        private System.Collections.Generic.List<Vector3> tutorialDrawWorldPoints = new System.Collections.Generic.List<Vector3>();
        private GameObject tutorialDrawHeldObject;
        private Vector3 tutorialDrawGrabOffset;
        private bool tutorialDrawObjectHeld;
        private GameObject tutorialDrawRotatingObject;
        private float tutorialDrawRotateStartDistance;
        private float tutorialDrawRotateStartAngle;
        private Quaternion tutorialDrawRotateStartRotation;
        private GameObject tutorialEraseTarget;
        private Renderer tutorialEraseRenderer;
        private Material tutorialEraseIdleMaterial;
        private Transform tutorialBridgeLeft;
        private Transform tutorialBridgeRight;
        private Transform tutorialBridgeRoot;
        private Transform tutorialSealRoot;
        private Transform tutorialAirflowRoot;
        private Transform tutorialMirrorRoot;
        private Transform tutorialMirrorProp;
        private Transform tutorialMirrorReflectedBeam;
        private Transform tutorialMagnetRoot;
        private Transform tutorialMagnetDisk;
        private Renderer tutorialBridgeLeftRenderer;
        private Renderer tutorialBridgeRightRenderer;
        private Renderer tutorialSealRenderer;
        private Renderer tutorialAirflowPadRenderer;
        private Renderer tutorialMirrorRenderer;
        private Renderer tutorialMagnetRenderer;
        private Transform tutorialAirflowArrow;
        private float tutorialBridgeStartDistance;
        private float tutorialMirrorStartAngle;
        private float tutorialMirrorStartYaw;
        private bool tutorialMirrorHeld;
        private float tutorialPalmStart = -1f;
        private float twoHandStartDistance;
        private float twoHandStartAngle;
        private Quaternion twoHandStartRotation;
        private float twoFingerMapStartDistance;
        private float twoFingerMapStartAngle;
        private Vector3 twoFingerMapStartScale;
        private Quaternion twoFingerMapStartRotation;
        private Rigidbody obstacleBox;
        private Renderer obstacleRenderer;
        private Vector3 level1BoxGrabOffset;
        private bool boxHeld;
        private BallController levelBall;
        private Level1Stage level1Stage;
        private Renderer blockSlotRenderer;
        private Transform bridgeLeft;
        private Transform bridgeRight;
        private Renderer bridgeLeftRenderer;
        private Renderer bridgeRightRenderer;
        private Transform rotateGate;
        private Renderer rotateGateRenderer;
        private Renderer sealRenderer;
        private bool bridgeLocked;
        private bool rotateGateLocked;
        private bool sealActivated;
        private bool rotateGateHeld;
        private bool level1RotateRequiresPinchReset;
        private float level1BridgeStartDistance;
        private float level1RotateStartAngle;
        private float sealHoldStart = -1f;
        private GameObject startGate;
        private GameObject bridgeGate;
        private GameObject rotateGateStop;
        private GameObject goalGate;
        private float level1SuccessUntil = -1f;
        private bool initialized;
        private Texture2D lineTexture;
        private Process bridgeProcess;
        private bool launchedBridge;
        private bool usesExternalBridge;
        private bool stoppingBridge;
        private string bridgeStatus = "Starting camera...";

        // Level2: Portal + Airflow mechanics
        private GameObject portalKey;
        private Rigidbody portalKeyBody;
        private Renderer portalKeyRenderer;
        private Renderer[] portalKeyRenderers;
        private Material[] portalKeyIdleMaterials;
        private Transform runeLeft;
        private Transform runeRight;
        private Renderer runeLeftRenderer;
        private Renderer runeRightRenderer;
        private GameObject runeLeftArrow;
        private GameObject runeRightArrow;
        private string level2HintMessage = "";
        private Vector3 portalAPosition;
        private Vector3 portalBPosition;
        private bool portalAActive;
        private bool portalBActive;
        private Renderer portalARenderer;
        private Renderer portalBRenderer;
        private Transform[] airBelts;
        private int[] airBeltDirection;
        private Renderer[] airBeltRenderers;
        private AirBeltTrigger[] airBeltTriggers;
        private Transform[] airBeltArrowTransforms;
        private Renderer[] airBeltArrowRenderers;
        private Transform[] airBeltStreaks;
        private Renderer[] airBeltStreakRenderers;
        private Transform[] airBeltMistQuads;
        private Renderer[] airBeltMistRenderers;
        private ParticleSystem[] airBeltParticles;
        private ParticleSystem portalAParticles;
        private ParticleSystem portalBParticles;
        private Rigidbody levelBallBody;
        private Renderer levelBallRenderer;
        private Material levelBallRuntimeMaterial;
        private Vector3 levelBallBaseScale = Vector3.one;
        private const float AirBeltForce = 5.2f;
        private bool keyHeld;
        private Vector3 keyGrabOffset;
        private float level2LastTeleport = -10f;
        private bool level2Teleporting;
        private float level2TeleportStart;
        private Vector3 level2TeleportStartPosition;
        private Vector3 level2TeleportEndPosition;
        private int pendingAirDirection;
        private float pendingAirDirectionStart = -1f;
        private const float AirflowDirectionDeadZone = 0.045f;
        private const float AirflowDirectionHoldSeconds = 0.25f;
        private float levelStartTime = -10f;

        // Level4: Mirror & magnet mechanics
        private Level4Stage level4Stage;
        private Transform level4Mirror;
        private Renderer level4MirrorRenderer;
        private GameObject level4LightGate;
        private Renderer level4DoorRenderer;
        private Transform[] level4BeamSegments;
        private Transform level4MagnetTurntable;
        private Renderer level4MagnetRenderer;
        private Transform level4MagnetNeedle;
        private Renderer level4MagnetNeedleRenderer;
        private GameObject level4MagnetGate;
        private GameObject level4MagnetBackstop;
        private Material level4NorthMaterial;
        private Material level4SouthMaterial;
        private int level4MagnetPolarity;
        private float level4RotateStartAngle;
        private float level4MirrorStartYaw;
        private bool level4MirrorHeld;
        private bool level4LightSolved;
        private bool level4BackstopRaised;
        private int level4PendingMagnetDirection;
        private float level4PendingMagnetDirectionStart = -1f;
        private const float Level4RoadAngleDegrees = -8f;
        private const float Level4RoadCenterY = 0.24f;
        private const float Level4MirrorTargetYaw = 0f;
        private const float Level4LightGateX = -1.50f;
        private const float Level4MagnetTriggerX = -0.50f;
        private const float Level4MagnetGateX = 1.45f;
        private const float Level4GoalX = 2.50f;
        private const float Level4MagnetX = 5.35f;
        private const float Level4MagnetForce = 5.1f;
        private const float Level4MagnetRadius = 6.60f;
        private const float MagnetThumbDirectionDeadZone = 0.055f;
        private const float MagnetThumbDirectionHoldSeconds = 0.18f;

        // tuning: larger grab radii for the portal key to make it easier to pick up
        private const float PortalKeyGrabPinchRadius = 1.6f;
        private const float PortalKeyGrabIdleRadius = 1.1f;

        // UI/debug helpers
        private bool lastPinchState = false;
        private bool lastKeyInRange = false;
        private float keyHoverStart = -1f;
        private const float KeyDwellSeconds = 0.6f;

        private Level3Stage level3Stage;
        private Transform level3CubePlate;
        private Transform level3SpherePlate;
        private Renderer level3CubePlateRenderer;
        private Renderer level3SpherePlateRenderer;
        private GameObject level3BridgePatch;
        private Renderer level3BridgePatchRenderer;
        private GameObject level3LockBlock;
        private GameObject level3LockBlockHalo;
        private Renderer level3LockBlockRenderer;
        private Transform level3SlideBridge;
        private Renderer level3SlideBridgeRenderer;
        private Transform level3SlideBridgeCore;
        private float level3SlideBridgeReleaseStart = -1f;
        private bool level3CubePlaced;
        private bool level3SpherePlaced;
        private bool level3BridgePlaced;
        private bool level3SlideBridgeReleased;
        private bool level3EraseRequiresGestureReset;
        private string level3HintMessage = "";

        public void Configure(GestureUdpReceiver gestureReceiver, CameraFrameReceiver frameReceiver)
        {
            receiver = gestureReceiver;
            cameraFrames = frameReceiver;
        }

        // Editor helper: allow editor scripts to start a specific GameMode by index
        public void EditorStartLevel(int levelIndex)
        {
            // levelIndex mapping: 0=CalibrationOpen,1=CalibrationPinch,2=Menu,3=Level0,4=Level1,5=Level2,6=Level3,7=Level4,8=Pass
            if (levelIndex == 3) StartLevel(GameMode.Level0);
            else if (levelIndex == 4) StartLevel(GameMode.Level1);
            else if (levelIndex == 5) StartLevel(GameMode.Level2);
            else if (levelIndex == 6) StartLevel(GameMode.Level3);
            else if (levelIndex == 7) StartLevel(GameMode.Level4);
            else if (levelIndex == 2) { mode = GameMode.Menu; SetLobbyVisible(true); }
        }

        public void EditorPreviewLevel2Airflow()
        {
            StartLevel(GameMode.Level2);
            portalAActive = true;
            if (runeLeftRenderer != null) runeLeftRenderer.sharedMaterial = tealGlow;
            if (portalARenderer != null) portalARenderer.sharedMaterial = tealGlow;
            if (portalBRenderer != null) portalBRenderer.sharedMaterial = tealGlow;
            if (levelBall != null)
            {
                levelBall.transform.position = portalBPosition + new Vector3(0f, 0.2f, 0f);
            }
            if (levelBallBody != null)
            {
                levelBallBody.isKinematic = false;
                levelBallBody.linearVelocity = Vector3.zero;
                levelBallBody.angularVelocity = Vector3.zero;
            }
            OpenGate(bridgeGate);
            SetAirBeltDirection(1);
            OpenGate(rotateGateStop);
            level1Stage = Level1Stage.RunToGoal;
            level2HintMessage = "Airflow: RIGHT. The wind will gradually carry the ball to the altar.";
        }

        public void InitializeForScene()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            receiver ??= GetComponent<GestureUdpReceiver>();
            cameraFrames ??= GetComponent<CameraFrameReceiver>();
            DestroyNamed("Main Camera");
            DestroyNamed("Key Light");
            DestroyNamed("Temple Fill Light");
            DestroyNamed("Menu Temple Lobby");
            DestroyNamed("Level00 Gesture Lab");
            DestroyNamed("Level01 First Path");
            DestroyNamed("Level02 Portal Airflow");
            DestroyNamed("Level03 Creation Erasure");
            DestroyNamed("Level04 Rotation Reflection");
            DestroyNamed("Level04 Mirror Magnet");
            BuildMaterials();
            BuildCameraAndLights();
            BuildLobbyShell();
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (string.Equals(activeScene.name, "Level02", System.StringComparison.OrdinalIgnoreCase))
            {
                StartLevel(GameMode.Level2);
            }
            else if (string.Equals(activeScene.name, "Level03", System.StringComparison.OrdinalIgnoreCase))
            {
                StartLevel(GameMode.Level3);
            }
            else if (string.Equals(activeScene.name, "Level04", System.StringComparison.OrdinalIgnoreCase))
            {
                StartLevel(GameMode.Level4);
            }
            else
            {
                ResetToCalibration();
            }
            StartGestureBridgeIfNeeded();
        }

        private void Awake()
        {
            InitializeForScene();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                QuitGame();
            }

            RefreshBridgeStatus();
            UpdateCameraBackground();

            if (HandleLevel1BallState())
            {
                return;
            }

            if (mode == GameMode.Level2)
            {
                UpdateLevel2Autonomous();
            }
            if (mode == GameMode.Level3)
            {
                UpdateLevel3SlideBridgeAnimation();
            }
            if (mode == GameMode.Level4)
            {
                UpdateLevel4Autonomous();
            }

            if (!TryGetPrimaryHand(out var hand))
            {
                if (mode == GameMode.Level2)
                {
                    ReleaseLevel2KeyIfTrackingLost();
                    pendingAirDirection = 0;
                    pendingAirDirectionStart = -1f;
                    SetAirBeltDirection(0);
                }
                else if (mode == GameMode.Level4)
                {
                    ResetLevel4MagnetFlipGesture();
                }
                else if (mode == GameMode.Level0)
                {
                    ResetTutorialMagnetFlipGesture();
                }
                return;
            }

            switch (mode)
            {
                case GameMode.CalibrationOpen:
                    UpdateCalibrationOpen(hand);
                    break;
                case GameMode.CalibrationPinch:
                    UpdateCalibrationPinch(hand);
                    break;
                case GameMode.Level0:
                    UpdateLevel0(hand);
                    break;
                case GameMode.Level1:
                    UpdateLevel1(hand);
                    break;
                case GameMode.Level2:
                    UpdateLevel2(hand);
                    break;
                case GameMode.Level3:
                    UpdateLevel3(hand);
                    break;
                case GameMode.Level4:
                    UpdateLevel4(hand);
                    break;
            }

            HandleLevel1BallState();
        }

        private bool HandleLevel1BallState()
        {
            if ((mode != GameMode.Level1 && mode != GameMode.Level2 && mode != GameMode.Level3 && mode != GameMode.Level4) || levelBall == null)
            {
                return false;
            }

            if (levelBall.Failed)
            {
                // restart current level
                Debug.Log("[LevelState] Ball failed — restarting level");
                StartLevel(mode);
                return true;
            }

            // Only allow automatic pass when the level is in the final "RunToGoal" stage.
            if (levelBall.ReachedGoal)
            {
                Debug.Log($"[LevelState] ReachedGoal detected. mode={mode} level1Stage={level1Stage} ballPos={levelBall.transform.position} timeSinceStart={Time.time - levelStartTime}");
                if (mode == GameMode.Level1
                    || (mode == GameMode.Level2 && level1Stage == Level1Stage.RunToGoal)
                    || (mode == GameMode.Level3 && level3Stage == Level3Stage.RunToGoal)
                    || (mode == GameMode.Level4 && level4Stage == Level4Stage.RunToGoal))
                {
                    Debug.Log("[LevelState] Level pass condition satisfied — switching to Pass mode");
                    mode = GameMode.Pass;
                    return true;
                }
                // ignore reached goal until level progression reaches running-to-goal phase
            }

            return false;
        }

        private void RefreshBridgeStatus()
        {
            if (receiver != null && receiver.HasFreshFrame)
            {
                bridgeStatus = "Camera: tracking hand";
                return;
            }

            if (bridgeProcess != null && bridgeProcess.HasExited)
            {
                bridgeStatus = $"Camera bridge exited ({bridgeProcess.ExitCode}). Check gesture-bridge-runtime.log.";
                launchedBridge = false;
            }
            else if (cameraFrames != null && cameraFrames.HasFreshFrame)
            {
                bridgeStatus = "Camera image live; show your hand to the camera.";
            }
            else if (usesExternalBridge)
            {
                bridgeStatus = "Launcher started camera bridge; waiting for camera image...";
            }
            else if (launchedBridge)
            {
                bridgeStatus = "Starting camera bridge; waiting for camera image...";
            }
        }

        private void OnGUI()
        {
            GUI.color = Color.white;

            switch (mode)
            {
                case GameMode.CalibrationOpen:
                case GameMode.CalibrationPinch:
                    DrawCalibration();
                    break;
                case GameMode.Menu:
                    DrawMenu();
                    break;
                case GameMode.Level0:
                    DrawLevel0Hud();
                    break;
                case GameMode.Level1:
                    DrawLevel1Hud();
                    break;
                case GameMode.Level2:
                    DrawLevel2Hud();
                    break;
                case GameMode.Level3:
                    DrawLevel3Hud();
                    break;
                case GameMode.Level4:
                    DrawLevel4Hud();
                    break;
                case GameMode.Pass:
                    DrawPassHud();
                    break;
            }

            DrawHandSkeletonOverlay();
            DrawCursor();

            DrawGlobalControls();
            DrawLevelSelectSidebar();
        }

        private void ResetToCalibration()
        {
            ClearLevel();
            SetLobbyVisible(false);
            mode = GameMode.CalibrationOpen;
            holdStart = -1f;
            hoverStartsByPointer.Clear();
            pinchSampleSum = 0f;
            pinchSampleCount = 0;
        }

        private void UpdateCalibrationOpen(GestureHandFrame hand)
        {
            if (hand.openPalm && hand.score >= 0.45f)
            {
                holdStart = holdStart < 0f ? Time.time : holdStart;
                if (Time.time - holdStart >= CalibrationHoldSeconds)
                {
                    mode = GameMode.CalibrationPinch;
                    holdStart = -1f;
                }
            }
            else
            {
                holdStart = -1f;
            }
        }

        private void UpdateCalibrationPinch(GestureHandFrame hand)
        {
            if (hand.pinchDistance < 0.62f && hand.score >= 0.45f)
            {
                holdStart = holdStart < 0f ? Time.time : holdStart;
                pinchSampleSum += hand.pinchDistance;
                pinchSampleCount += 1;
                if (Time.time - holdStart >= CalibrationHoldSeconds)
                {
                    var average = pinchSampleCount > 0 ? pinchSampleSum / pinchSampleCount : 0.34f;
                    pinchThreshold = Mathf.Clamp(average * 1.65f, 0.36f, 0.62f);
                    StartLevel(GameMode.Level0);
                    holdStart = -1f;
                }
            }
            else
            {
                holdStart = -1f;
            }
        }

        private void DrawCalibration()
        {
            var panel = new Rect(Screen.width / 2f - 250f, 34, 500, 278);
            var title = mode == GameMode.CalibrationOpen ? "Calibration: Open Hand" : "Calibration: Pinch";
            var detail = mode == GameMode.CalibrationOpen
                ? "Hold an open palm for 1 second."
                : "Touch thumb and index finger together for 1 second.";
            var progress = holdStart < 0f ? 0f : Mathf.Clamp01((Time.time - holdStart) / CalibrationHoldSeconds);

            DrawPanel(panel);
            GUI.Label(new Rect(panel.x + 24, panel.y + 18, 430, 30), "Hand of God");
            GUI.Label(new Rect(panel.x + 24, panel.y + 50, 430, 26), title);
            GUI.Label(new Rect(panel.x + 24, panel.y + 82, 440, 24), detail);
            DrawProgressBar(progress, new Rect(panel.x + 24, panel.y + 112, 410, 18));
            var cameraStatus = cameraFrames != null && cameraFrames.HasFreshFrame ? "Camera image: live" : "Camera image: waiting";
            GUI.Label(new Rect(panel.x + 24, panel.y + 140, 450, 24), receiver != null && receiver.HasFreshFrame ? "Camera: tracking hand" : $"{bridgeStatus} | {cameraStatus}");
            if (receiver != null && receiver.HasFreshFrame)
            {
                var frame = receiver.Latest;
                GUI.Label(new Rect(panel.x + 24, panel.y + 164, 450, 24), $"Bridge: {frame.bridgeFps:0.0} FPS  |  processing {frame.processingMs:0.0} ms");
            }
            DrawUtilityButton("start-camera", "Start / Retry Camera", new Rect(panel.x + 24, panel.y + 204, 190, 34), SafeDwellSeconds, StartVisibleGestureBridge);
            DrawHoverButton("skip", "Skip calibration", new Rect(panel.x + 236, panel.y + 204, 190, 34), SafeDwellSeconds, () =>
            {
                pinchThreshold = 0.56f;
                StartLevel(GameMode.Level0);
            });
        }

        private void DrawGlobalControls()
        {
            var exitRect = new Rect(48, 38, 128, 46);
            DrawUtilityButton("global-exit", "Exit", exitRect, SafeDwellSeconds, QuitGame);
            if (mode != GameMode.CalibrationOpen && mode != GameMode.CalibrationPinch)
            {
                DrawUtilityButton("global-recalibrate", "Calibrate", new Rect(exitRect.xMax + 18, exitRect.y, 132, 46), MenuDwellSeconds, ResetToCalibration);
            }
        }

        private void DrawLevelSelectSidebar()
        {
            if (mode == GameMode.CalibrationOpen || mode == GameMode.CalibrationPinch)
            {
                return;
            }

            var panel = new Rect(48f, 104f, 260f, 238f);
            DrawPanel(panel);
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            GUI.Label(new Rect(panel.x + 12f, panel.y + 12f, panel.width - 24f, 26f), "Level Select", titleStyle);

            DrawLevelSelectButton("select-level0", "Level 0: Tutorial", GameMode.Level0, new Rect(panel.x + 18f, panel.y + 44f, panel.width - 36f, 30f));
            DrawLevelSelectButton("select-level1", "Level 1: Moving Path", GameMode.Level1, new Rect(panel.x + 18f, panel.y + 80f, panel.width - 36f, 30f));
            DrawLevelSelectButton("select-level2", "Level 2: Portals", GameMode.Level2, new Rect(panel.x + 18f, panel.y + 116f, panel.width - 36f, 30f));
            DrawLevelSelectButton("select-level3", "Level 3: Creation & Erasure", GameMode.Level3, new Rect(panel.x + 18f, panel.y + 152f, panel.width - 36f, 30f));
            DrawLevelSelectButton("select-level4", "Level 4: Mirror & Magnet", GameMode.Level4, new Rect(panel.x + 18f, panel.y + 188f, panel.width - 36f, 30f));
        }

        private void DrawLevelSelectButton(string key, string label, GameMode targetMode, Rect rect)
        {
            var active = mode == targetMode;
            DrawHoverButton(key, active ? $"> {label}" : label, rect, MenuDwellSeconds, () => StartLevel(targetMode), 15);
        }

        private void DrawMenu()
        {
            DrawPanel(new Rect(40, 40, 390, 456));
            GUI.Label(new Rect(70, 62, 300, 30), "Hand of God");
            GUI.Label(new Rect(70, 92, 320, 24), "Hover your index finger over an option.");
            DrawHoverButton("start", "Start Game", new Rect(70, 120, 260, 42), MenuDwellSeconds, () => StartLevel(GameMode.Level1));
            DrawHoverButton("level0", "Level 0: Tutorial", new Rect(70, 172, 260, 42), MenuDwellSeconds, () => StartLevel(GameMode.Level0));
            DrawHoverButton("level1", "Level 1: First Path", new Rect(70, 224, 260, 42), MenuDwellSeconds, () => StartLevel(GameMode.Level1));
            DrawHoverButton("level2", "Level 2: Portals & Airflow", new Rect(70, 276, 260, 42), MenuDwellSeconds, () => StartLevel(GameMode.Level2));
            DrawHoverButton("level3", "Level 3: Creation & Erasure", new Rect(70, 328, 260, 42), MenuDwellSeconds, () => StartLevel(GameMode.Level3));
            DrawHoverButton("level4", "Level 4: Mirror & Magnet", new Rect(70, 380, 260, 42), MenuDwellSeconds, () => StartLevel(GameMode.Level4));
            DrawHoverButton("recalibrate", "Recalibrate", new Rect(70, 432, 260, 42), MenuDwellSeconds, ResetToCalibration);
        }

        private void DrawLevel0Hud()
        {
            var titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            var stepStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            var detailStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            var statusStyle = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            var panelLeft = 340f;
            var panelWidth = Mathf.Min(Screen.width - panelLeft - 70f, 860f);
            panelLeft = Mathf.Max(panelLeft, Screen.width * 0.5f - panelWidth * 0.5f);
            var panel = new Rect(panelLeft, 42f, panelWidth, 170f);
            DrawPanel(panel);
            GUI.Label(new Rect(panel.x + 32f, panel.y + 16f, panel.width - 64f, 32f), "Level 0: Gesture Tutorial", titleStyle);
            GUI.Label(new Rect(panel.x + 32f, panel.y + 52f, panel.width - 64f, 28f), TutorialTitle(), stepStyle);
            GUI.Label(new Rect(panel.x + 32f, panel.y + 84f, panel.width - 64f, 48f), TutorialDetail(), detailStyle);
            GUI.Label(new Rect(panel.x + 32f, panel.y + 136f, panel.width - 64f, 22f), $"{HandStatusText()}    Pinch threshold: {pinchThreshold:0.00}", statusStyle);

            var stageSucceeded = TutorialStageSucceeded();
            DrawTutorialStageMenu();
            if (!stageSucceeded && (tutorialStage == TutorialStage.DrawCreate || tutorialStage == TutorialStage.DrawErase))
            {
                DrawTutorialDrawingHud();
            }
            if (!stageSucceeded && TutorialUsesLabObject())
            {
                var shapePanel = new Rect(Screen.width - 330f, 310f, 260f, 124f);
                DrawPanel(shapePanel);
                GUI.Label(new Rect(shapePanel.x + 20f, shapePanel.y + 10f, 220f, 22f), "Practice object");
                DrawHoverButton("shape-cube", "Cube", new Rect(shapePanel.x + 20f, shapePanel.y + 36f, 210f, 24f), MenuDwellSeconds, () => ReplaceLabObject(PrimitiveType.Cube), 13);
                DrawHoverButton("shape-sphere", "Sphere", new Rect(shapePanel.x + 20f, shapePanel.y + 64f, 210f, 24f), MenuDwellSeconds, () => ReplaceLabObject(PrimitiveType.Sphere), 13);
                DrawHoverButton("shape-cylinder", "Cylinder", new Rect(shapePanel.x + 20f, shapePanel.y + 92f, 210f, 24f), MenuDwellSeconds, () => ReplaceLabObject(PrimitiveType.Cylinder), 13);
            }

            if (stageSucceeded)
            {
                var finalStage = IsFinalTutorialStage();
                DrawSuccessBanner(new Rect(Screen.width * 0.5f - 210f, panel.yMax + 14f, 420f, 58f), finalStage ? "TUTORIAL COMPLETE" : "SUCCESS");
                var buttonLabel = finalStage ? "Next: Level 1" : "Continue";
                var buttonAction = finalStage ? (System.Action)(() => StartLevel(GameMode.Level1)) : AdvanceTutorialStage;
                DrawHoverButton("tutorial-continue", buttonLabel, new Rect(Screen.width * 0.5f - 170f, panel.yMax + 82f, 340f, 64f), MenuDwellSeconds, buttonAction, 24);
            }
        }

        private void DrawTutorialStageMenu()
        {
            var panel = new Rect(48f, 362f, 260f, 348f);
            DrawPanel(panel);
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            GUI.Label(new Rect(panel.x + 12f, panel.y + 10f, panel.width - 24f, 24f), "Tutorial Steps", titleStyle);
            var y = panel.y + 40f;
            DrawTutorialStageButton("tutorial-jump-hands", "1  Recognize hands", TutorialStage.FindHands, new Rect(panel.x + 18f, y, panel.width - 36f, 24f));
            DrawTutorialStageButton("tutorial-jump-drag", "2  One-hand drag", TutorialStage.OneHandDrag, new Rect(panel.x + 18f, y + 28f, panel.width - 36f, 24f));
            DrawTutorialStageButton("tutorial-jump-rotate", "3  Two-hand rotate", TutorialStage.TwoHandRotate, new Rect(panel.x + 18f, y + 56f, panel.width - 36f, 24f));
            DrawTutorialStageButton("tutorial-jump-bridge", "4  Join bridge", TutorialStage.BridgePull, new Rect(panel.x + 18f, y + 84f, panel.width - 36f, 24f));
            DrawTutorialStageButton("tutorial-jump-palm", "5  Palm seal", TutorialStage.PalmActivate, new Rect(panel.x + 18f, y + 112f, panel.width - 36f, 24f));
            DrawTutorialStageButton("tutorial-jump-map", "6  Map control", TutorialStage.MapControl, new Rect(panel.x + 18f, y + 140f, panel.width - 36f, 24f));
            DrawTutorialStageButton("tutorial-jump-air", "7  Airflow", TutorialStage.AirflowDirection, new Rect(panel.x + 18f, y + 168f, panel.width - 36f, 24f));
            DrawTutorialStageButton("tutorial-jump-draw", "8  Draw object", TutorialStage.DrawCreate, new Rect(panel.x + 18f, y + 196f, panel.width - 36f, 24f));
            DrawTutorialStageButton("tutorial-jump-erase", "9  Erase object", TutorialStage.DrawErase, new Rect(panel.x + 18f, y + 224f, panel.width - 36f, 24f));
            DrawTutorialStageButton("tutorial-jump-mirror", "10 Mirror rotate", TutorialStage.MirrorRotate, new Rect(panel.x + 18f, y + 252f, panel.width - 36f, 24f));
            DrawTutorialStageButton("tutorial-jump-magnet", "11 Magnet poles", TutorialStage.MagnetPolarity, new Rect(panel.x + 18f, y + 280f, panel.width - 36f, 24f));
        }

        private void DrawTutorialStageButton(string key, string label, TutorialStage targetStage, Rect rect)
        {
            var active = tutorialStage == targetStage;
            DrawHoverButton(key, active ? $"> {label}" : label, rect, MenuDwellSeconds, () => SelectTutorialStage(targetStage), 12);
        }

        private void DrawTutorialDrawingHud()
        {
            if (tutorialDrawScreenPoints.Count > 1)
            {
                var lineColor = tutorialDrawState == TutorialDrawState.CancelConfirm
                    ? new Color(1f, 0.36f, 0.12f, 0.92f)
                    : new Color(0.16f, 1f, 0.82f, 0.95f);
                for (var i = 1; i < tutorialDrawScreenPoints.Count; i++)
                {
                    DrawLine(tutorialDrawScreenPoints[i - 1], tutorialDrawScreenPoints[i], lineColor, 5f);
                }
            }

            var panel = new Rect(Screen.width * 0.5f - 250f, 230f, 500f, 90f);
            DrawPanel(panel);
            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
                wordWrap = true,
            };
            GUI.Label(new Rect(panel.x + 20f, panel.y + 10f, panel.width - 40f, 28f), TutorialDrawStatusText(), style);

            var progress = TutorialDrawHoldProgress();
            if (progress > 0f)
            {
                DrawProgressBar(progress, new Rect(panel.x + 50f, panel.y + 54f, panel.width - 100f, 18f));
            }
            else if (!string.IsNullOrEmpty(tutorialDrawMessage) && Time.time < tutorialDrawMessageUntil)
            {
                GUI.color = tutorialDrawInvalid ? new Color(1f, 0.48f, 0.24f, 1f) : new Color(0.18f, 1f, 0.78f, 1f);
                GUI.Label(new Rect(panel.x + 20f, panel.y + 52f, panel.width - 40f, 26f), tutorialDrawMessage, style);
                GUI.color = Color.white;
            }
        }

        private void DrawLevel1Hud()
        {
            var panelWidth = Mathf.Min(Screen.width - 180f, 820f);
            var panel = new Rect(Screen.width * 0.5f - panelWidth * 0.5f, 42f, panelWidth, 132f);
            DrawPanel(panel);
            var titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            var objectiveStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true };
            GUI.Label(new Rect(panel.x + 28f, panel.y + 16f, panel.width - 56f, 30f), "Level 1: Trial of the Moving Path", titleStyle);
            GUI.Label(new Rect(panel.x + 28f, panel.y + 52f, panel.width - 56f, 42f), Level1ObjectiveText(), objectiveStyle);
            GUI.Label(new Rect(panel.x + 28f, panel.y + 96f, panel.width - 56f, 24f), levelBall != null ? $"Ball speed: {levelBall.Speed:0.00}" : "Ball speed: 0.00");
            if (Time.time < level1SuccessUntil)
            {
                DrawSuccessBanner(new Rect(Screen.width * 0.5f - 135f, panel.yMax + 12f, 270f, 50f), "SUCCESS");
            }
        }

        private void DrawPassHud()
        {
            var panelX = Screen.width / 2f - 230f;
            DrawPanel(new Rect(panelX, 64, 460, 250));
            DrawSuccessBanner(new Rect(panelX + 40, 92, 380, 70), "PASS");
            var messageStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold,
            };
            GUI.color = new Color(0.92f, 1f, 0.94f, 1f);
            GUI.Label(new Rect(panelX + 48, 166, 364, 30), "The ball reached the altar.", messageStyle);
            GUI.color = Color.white;
            DrawHoverButton("pass-restart", "Restart", new Rect(Screen.width / 2f - 230f, 222, 150, 52), MenuDwellSeconds, () => StartLevel(lastLevel), 20);
            if (lastLevel == GameMode.Level1)
            {
                DrawHoverButton("pass-next", "Next: Level 2", new Rect(Screen.width / 2f - 50f, 222, 150, 52), MenuDwellSeconds, () => StartLevel(GameMode.Level2), 20);
                DrawHoverButton("pass-level0", "Tutorial", new Rect(Screen.width / 2f + 130f, 222, 150, 52), MenuDwellSeconds, () => StartLevel(GameMode.Level0), 20);
            }
            else if (lastLevel == GameMode.Level2)
            {
                DrawHoverButton("pass-next", "Next: Level 3", new Rect(Screen.width / 2f - 50f, 222, 150, 52), MenuDwellSeconds, () => StartLevel(GameMode.Level3), 20);
                DrawHoverButton("pass-level0", "Tutorial", new Rect(Screen.width / 2f + 130f, 222, 150, 52), MenuDwellSeconds, () => StartLevel(GameMode.Level0), 20);
            }
            else if (lastLevel == GameMode.Level3)
            {
                DrawHoverButton("pass-next", "Next: Level 4", new Rect(Screen.width / 2f - 50f, 222, 150, 52), MenuDwellSeconds, () => StartLevel(GameMode.Level4), 20);
                DrawHoverButton("pass-level0", "Tutorial", new Rect(Screen.width / 2f + 130f, 222, 150, 52), MenuDwellSeconds, () => StartLevel(GameMode.Level0), 20);
            }
            else
            {
                DrawHoverButton("pass-level0", "Tutorial", new Rect(Screen.width / 2f + 20f, 222, 150, 52), MenuDwellSeconds, () => StartLevel(GameMode.Level0), 20);
            }
        }

        private void DrawLevel3Hud()
        {
            var panelWidth = Mathf.Min(Screen.width - 180f, 840f);
            var panel = new Rect(Screen.width * 0.5f - panelWidth * 0.5f, 42f, panelWidth, 154f);
            DrawPanel(panel);
            var titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            var objectiveStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true };
            GUI.Label(new Rect(panel.x + 28f, panel.y + 16f, panel.width - 56f, 30f), "Level 3: Creation & Erasure", titleStyle);
            GUI.Label(new Rect(panel.x + 28f, panel.y + 52f, panel.width - 56f, 48f), Level3ObjectiveText(), objectiveStyle);
            GUI.Label(new Rect(panel.x + 28f, panel.y + 104f, panel.width - 56f, 24f), levelBall != null ? $"Ball speed: {levelBall.Speed:0.00}" : "Ball speed: 0.00");
            if (!string.IsNullOrEmpty(level3HintMessage))
            {
                GUI.Label(new Rect(panel.x + 28f, panel.y + 128f, panel.width - 56f, 24f), level3HintMessage);
            }

            if (CanDrawCreateObject() || CanLevel3EraseDrawnObject())
            {
                DrawTutorialDrawingHud();
            }

            if (Time.time < level1SuccessUntil)
            {
                DrawSuccessBanner(new Rect(Screen.width * 0.5f - 135f, panel.yMax + 12f, 270f, 50f), "SUCCESS");
            }
        }

        private void DrawLevel2Hud()
        {
            var panelWidth = Mathf.Min(Screen.width - 180f, 820f);
            var panel = new Rect(Screen.width * 0.5f - panelWidth * 0.5f, 42f, panelWidth, 132f);
            DrawPanel(panel);
            var titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            var objectiveStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true };
            GUI.Label(new Rect(panel.x + 28f, panel.y + 16f, panel.width - 56f, 30f), "Level 2: Portals & Airflow", titleStyle);
            var detail = level1Stage switch
            {
                Level1Stage.ClearBlock => "Drag the glowing key onto the left rune to unlock the path.",
                Level1Stage.JoinBridge => "Use the airflow gesture: thumb out, index+middle together, ring+pinky folded.",
                Level1Stage.RotateGate => "Keep the airflow gesture active until the path is fully open.",
                Level1Stage.RunToGoal => "The path is open. Guide the ball to the altar.",
                _ => "",
            };
            GUI.Label(new Rect(panel.x + 28f, panel.y + 52f, panel.width - 56f, 42f), detail, objectiveStyle);
            var wind = airBeltDirection != null && airBeltDirection.Length > 0 ? airBeltDirection[0] : 0;
            var windText = wind == 1 ? "RIGHT" : (wind == -1 ? "LEFT" : "OFF");
            GUI.Label(new Rect(panel.x + 28f, panel.y + 96f, panel.width - 56f, 24f), levelBall != null ? $"Ball speed: {levelBall.Speed:0.00}    Airflow: {windText}" : $"Ball speed: 0.00    Airflow: {windText}");

            // dynamic level hints
            if (!string.IsNullOrEmpty(level2HintMessage))
            {
                var hintRect = new Rect(panel.x + 28f, panel.y + 128f, panel.width - 56f, 28f);
                GUI.Label(hintRect, level2HintMessage);
            }
        }

        private void DrawLevel4Hud()
        {
            var panelWidth = Mathf.Min(Screen.width - 180f, 860f);
            var panel = new Rect(Screen.width * 0.5f - panelWidth * 0.5f, 42f, panelWidth, 132f);
            DrawPanel(panel);
            var titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            var objectiveStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true };
            GUI.Label(new Rect(panel.x + 28f, panel.y + 16f, panel.width - 56f, 30f), "Level 4: Mirror & Magnet", titleStyle);
            var detail = level4Stage switch
            {
                Level4Stage.LightGuide => "Pinch with both hands and rotate the mirror until the beam strikes the light gate.",
                Level4Stage.MagneticTurntable => "Make a thumbs-up fist. Point the thumb left for BLUE-left repel, or right for BLUE-right attract.",
                Level4Stage.RunToGoal => "The magnetic rail is aligned. Let the metal ball reach the altar.",
                _ => "",
            };
            GUI.Label(new Rect(panel.x + 28f, panel.y + 52f, panel.width - 56f, 42f), detail, objectiveStyle);
            var polarity = level4MagnetPolarity > 0 ? "BLUE RIGHT / ATTRACT" : (level4MagnetPolarity < 0 ? "BLUE LEFT / REPEL" : "OFF");
            var mirrorYaw = level4Mirror != null ? NormalizeYaw(level4Mirror.eulerAngles.y) : 0f;
            GUI.Label(new Rect(panel.x + 28f, panel.y + 96f, panel.width - 56f, 24f), levelBall != null ? $"Ball speed: {levelBall.Speed:0.00}    Mirror: {mirrorYaw:0} deg    Magnet: {polarity}" : $"Ball speed: 0.00    Magnet: {polarity}");
            if (Time.time < level1SuccessUntil)
            {
                DrawSuccessBanner(new Rect(Screen.width * 0.5f - 135f, panel.yMax + 12f, 270f, 50f), "SUCCESS");
            }
        }

        private void DrawSuccessBanner(Rect rect, string text)
        {
            var pulse = (Mathf.Sin(Time.time * 5.4f) + 1f) * 0.5f;
            var glowRect = new Rect(rect.x - 12f, rect.y - 8f, rect.width + 24f, rect.height + 16f);
            var oldColor = GUI.color;
            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            GUI.color = new Color(0.08f, 1f, 0.76f, 0.20f + pulse * 0.18f);
            GUI.DrawTexture(glowRect, Texture2D.whiteTexture);

            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = rect.height >= 64f ? 38 : 32,
                fontStyle = FontStyle.Bold,
            };
            GUI.color = Color.black;
            GUI.Label(new Rect(rect.x + 3f, rect.y + 3f, rect.width, rect.height), text, style);
            GUI.color = new Color(0.16f + pulse * 0.18f, 1f, 0.82f, 1f);
            GUI.Label(rect, text, style);
            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
        }

        private void StartLevel(GameMode level)
        {
            lastLevel = level;
            ClearLevel();
            SetLobbyVisible(false);
            if (level == GameMode.Level0)
            {
                Physics.gravity = new Vector3(0f, -9.81f, 0f);
                BuildLevel0();
            }
            else if (level == GameMode.Level1)
            {
                Physics.gravity = new Vector3(0f, -9.81f, 0f);
                BuildLevel1();
            }
            else if (level == GameMode.Level2)
            {
                Physics.gravity = new Vector3(0f, -9.81f, 0f);
                BuildLevel2();
            }
            else if (level == GameMode.Level3)
            {
                Physics.gravity = new Vector3(0f, -9.81f, 0f);
                BuildLevel3();
            }
            else if (level == GameMode.Level4)
            {
                Physics.gravity = new Vector3(0f, -3.35f, 0f);
                BuildLevel4();
            }
            mode = level;
            levelStartTime = Time.time;
            hoverStartsByPointer.Clear();
        }

        private void BuildLevel0()
        {
            levelRoot = new GameObject("Level00 Gesture Lab").transform;
            CreateBox("lab base", new Vector3(0f, -0.15f, 0f), new Vector3(8.4f, 0.3f, 4.6f), darkStone, levelRoot, Quaternion.identity, true);
            CreateBox("lab guide", new Vector3(0f, 0.02f, 0f), new Vector3(6.2f, 0.05f, 3.15f), paleStone, levelRoot, Quaternion.identity, false);
            level4NorthMaterial ??= NewMaterial("Level4 soft north red", new Color(0.82f, 0.28f, 0.25f), 0.44f, 0.18f);
            level4SouthMaterial ??= NewMaterial("Level4 soft south blue", new Color(0.25f, 0.44f, 0.86f), 0.44f, 0.18f);
            ReplaceLabObject(PrimitiveType.Cube);
            labCompleted = false;
            tutorialStage = TutorialStage.FindHands;
            tutorialStageSucceeded = false;
            tutorialObjectMoved = false;
            tutorialObjectRotated = false;
            tutorialBridgePulled = false;
            tutorialPalmActivated = false;
            tutorialMapAdjusted = false;
            tutorialAirflowDirected = false;
            tutorialDrawCreated = false;
            tutorialDrawErased = false;
            tutorialDrawInvalid = false;
            tutorialMirrorRotated = false;
            tutorialMagnetPolarityChanged = false;
            tutorialAirflowPreviewDirection = 0;
            tutorialMagnetPreviewPolarity = 0;
            tutorialMagnetReversalCount = 0;
            ResetTutorialMagnetFlipGesture();
            tutorialPalmStart = -1f;
            tutorialDrawState = TutorialDrawState.WaitingStart;
            tutorialDrawHoldStart = -1f;
            tutorialDrawSawFingerSeparation = false;
            tutorialDrawMessage = "";
            tutorialDrawMessageUntil = -1f;
            tutorialDrawnRoot = new GameObject("tutorial drawn objects").transform;
            tutorialDrawnRoot.SetParent(levelRoot, false);
            tutorialDrawnObjects.Clear();
            tutorialDrawScreenPoints.Clear();
            tutorialDrawWorldPoints.Clear();
            tutorialDrawHeldObject = null;
            tutorialDrawGrabOffset = Vector3.zero;
            tutorialDrawObjectHeld = false;
            tutorialDrawRotatingObject = null;
            tutorialDrawRotateStartDistance = 0f;
            tutorialDrawRotateStartAngle = 0f;
            tutorialDrawRotateStartRotation = Quaternion.identity;
            BuildTutorialMechanisms();
            twoFingerMapStartDistance = 0f;
            ApplyTutorialStageVisibility();
        }

        private void BuildTutorialMechanisms()
        {
            tutorialBridgeRoot = new GameObject("tutorial bridge props").transform;
            tutorialBridgeRoot.SetParent(levelRoot, false);
            var left = CreateBox("tutorial bridge left half", new Vector3(-0.55f, 0.22f, -1.05f), new Vector3(1.0f, 0.08f, 0.42f), boxHover, tutorialBridgeRoot, Quaternion.identity, true);
            var right = CreateBox("tutorial bridge right half", new Vector3(0.55f, 0.22f, -1.05f), new Vector3(1.0f, 0.08f, 0.42f), boxHover, tutorialBridgeRoot, Quaternion.identity, true);
            tutorialBridgeLeft = left.transform;
            tutorialBridgeRight = right.transform;
            tutorialBridgeLeftRenderer = left.GetComponent<Renderer>();
            tutorialBridgeRightRenderer = right.GetComponent<Renderer>();

            tutorialSealRoot = new GameObject("tutorial palm seal props").transform;
            tutorialSealRoot.SetParent(levelRoot, false);
            var seal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            seal.name = "tutorial palm seal";
            seal.transform.SetParent(tutorialSealRoot, false);
            seal.transform.position = new Vector3(0f, 0.16f, 1.05f);
            seal.transform.localScale = new Vector3(0.56f, 0.04f, 0.56f);
            tutorialSealRenderer = seal.GetComponent<Renderer>();
            tutorialSealRenderer.sharedMaterial = amberGlow;
            DestroyUnityObject(seal.GetComponent<Collider>());
            CreateTorus("tutorial seal ring", seal.transform.position + new Vector3(0f, 0.04f, 0f), 0.5f, 0.035f, tealGlow, tutorialSealRoot);

            BuildTutorialAirflowArea();
            BuildTutorialLevel4Area();
        }

        private void BuildTutorialLevel4Area()
        {
            tutorialMirrorRoot = new GameObject("tutorial level4 mirror props").transform;
            tutorialMirrorRoot.SetParent(levelRoot, false);
            CreateBox("tutorial mirror side slab", new Vector3(0f, -0.015f, 0f), new Vector3(4.2f, 0.13f, 1.62f), level2FloorMaterial, tutorialMirrorRoot, Quaternion.identity, false);
            CreateBox("tutorial mirror light source", new Vector3(-1.55f, 0.20f, 0.52f), new Vector3(0.28f, 0.10f, 0.28f), amberGlow, tutorialMirrorRoot, Quaternion.identity, false);
            tutorialMirrorProp = CreateBox("tutorial side mirror", new Vector3(-0.35f, 0.50f, 0.52f), new Vector3(0.14f, 0.70f, 0.52f), level2TrimMaterial, tutorialMirrorRoot, Quaternion.Euler(0f, -18f, 0f), false).transform;
            tutorialMirrorRenderer = tutorialMirrorProp.GetComponent<Renderer>();
            CreateTorus("tutorial mirror target receiver", new Vector3(1.22f, 0.28f, 0.52f), 0.28f, 0.026f, tealGlow, tutorialMirrorRoot);
            CreateLevel4Beam("tutorial mirror light beam", new Vector3(-1.55f, 0.42f, 0.52f), tutorialMirrorProp.position, amberGlow).transform.SetParent(tutorialMirrorRoot, true);
            var initialReflectedEnd = tutorialMirrorProp.position + Quaternion.Euler(0f, -18f, 0f) * Vector3.right * 1.70f;
            tutorialMirrorReflectedBeam = CreateLevel4Beam("tutorial mirror reflected beam", tutorialMirrorProp.position, initialReflectedEnd, amberGlow).transform;
            tutorialMirrorReflectedBeam.SetParent(tutorialMirrorRoot, true);

            tutorialMagnetRoot = new GameObject("tutorial level4 magnet props").transform;
            tutorialMagnetRoot.SetParent(levelRoot, false);
            CreateBox("tutorial magnet practice slab", new Vector3(0f, -0.015f, 0f), new Vector3(4.2f, 0.13f, 1.62f), level2FloorMaterial, tutorialMagnetRoot, Quaternion.identity, false);
            var stand = CreateBox("tutorial bar magnet stand", new Vector3(0f, 0.16f, 0f), new Vector3(1.62f, 0.12f, 0.58f), level2TrimMaterial, tutorialMagnetRoot, Quaternion.identity, false);
            tutorialMagnetRenderer = stand.GetComponent<Renderer>();
            tutorialMagnetDisk = new GameObject("tutorial rotating bar magnet").transform;
            tutorialMagnetDisk.SetParent(tutorialMagnetRoot, false);
            tutorialMagnetDisk.position = new Vector3(0f, 0.35f, 0f);
            var north = CreateBox("tutorial bar north red", new Vector3(-0.42f, 0.35f, 0f), new Vector3(0.82f, 0.16f, 0.34f), level4NorthMaterial ?? tealGlow, tutorialMagnetDisk, Quaternion.identity, false);
            var south = CreateBox("tutorial bar south blue", new Vector3(0.42f, 0.35f, 0f), new Vector3(0.82f, 0.16f, 0.34f), level4SouthMaterial ?? amberGlow, tutorialMagnetDisk, Quaternion.identity, false);
            north.transform.localPosition = new Vector3(-0.42f, 0f, 0f);
            south.transform.localPosition = new Vector3(0.42f, 0f, 0f);
            CreateLevel4MagnetLabel("N", north.transform.position + new Vector3(0f, 0.10f, 0f), level4NorthMaterial, tutorialMagnetDisk);
            CreateLevel4MagnetLabel("S", south.transform.position + new Vector3(0f, 0.10f, 0f), level4SouthMaterial, tutorialMagnetDisk);
            tutorialMagnetPreviewPolarity = -1;
            UpdateTutorialMagnetVisuals(tutorialMagnetPreviewPolarity);
        }

        private void BuildTutorialAirflowArea()
        {
            tutorialAirflowRoot = new GameObject("tutorial level2 airflow props").transform;
            tutorialAirflowRoot.SetParent(levelRoot, false);

            var floor = CreateBox("tutorial level2 central wind gallery floor", new Vector3(0f, -0.015f, 0f), new Vector3(4.95f, 0.13f, 1.72f), level2FloorMaterial, tutorialAirflowRoot, Quaternion.identity, false);
            tutorialAirflowPadRenderer = floor.GetComponent<Renderer>();
            CreateTutorialChamberFrame(0f, 4.95f, 1.72f);
            CreateBox("tutorial level2 wind channel low glow", new Vector3(0f, 0.055f, 0f), new Vector3(4.65f, 0.006f, 0.045f), level2WallMaterial, tutorialAirflowRoot, Quaternion.identity, false);
            CreateTutorialWindFloorHints();

            airBelts = new Transform[1];
            airBeltDirection = new int[1];
            airBeltRenderers = new Renderer[1];
            airBeltTriggers = new AirBeltTrigger[1];
            airBeltArrowRenderers = new Renderer[1];
            airBeltArrowTransforms = new Transform[1];
            airBeltStreaks = new Transform[6];
            airBeltStreakRenderers = new Renderer[6];
            airBeltMistQuads = new Transform[4];
            airBeltMistRenderers = new Renderer[4];

            var belt = CreateBox("tutorial air belt trigger", new Vector3(0f, 0.18f, 0f), new Vector3(4.95f, 0.36f, 1.65f), level2WindMaterial, tutorialAirflowRoot, Quaternion.identity, true);
            airBelts[0] = belt.transform;
            airBeltRenderers[0] = belt.GetComponent<Renderer>();
            airBeltDirection[0] = 0;
            var col = belt.GetComponent<Collider>();
            col.isTrigger = true;
            var trigger = belt.AddComponent<AirBeltTrigger>();
            trigger.beltIndex = 0;
            trigger.direction = 0;
            trigger.force = AirBeltForce;
            trigger.maxWindSpeed = 1.95f;
            trigger.rampSeconds = 1.45f;
            airBeltTriggers[0] = trigger;

            var beltVisualRoot = new GameObject("tutorial air belt visual root");
            beltVisualRoot.transform.SetParent(tutorialAirflowRoot, false);
            beltVisualRoot.transform.position = belt.transform.position;

            var arrow = CreateBox("tutorial air arrow glow", beltVisualRoot.transform.position + new Vector3(0f, 0.21f, -0.70f), new Vector3(0.34f, 0.018f, 0.055f), level2PortalCoreMaterial, beltVisualRoot.transform, Quaternion.identity, false);
            tutorialAirflowArrow = arrow.transform;
            airBeltArrowRenderers[0] = arrow.GetComponent<Renderer>();
            airBeltArrowTransforms[0] = arrow.transform;

            for (var i = 0; i < airBeltMistQuads.Length; i++)
            {
                var xOffset = Mathf.Lerp(-1.68f, 1.68f, i / (float)(airBeltMistQuads.Length - 1));
                var zOffset = i % 2 == 0 ? -0.28f : 0.28f;
                var mist = CreateAirflowMist($"tutorial air flow mist sheet {i + 1}", beltVisualRoot.transform, new Vector3(xOffset, 0.285f, zOffset), 2.0f, 0.68f);
                airBeltMistQuads[i] = mist.transform;
                airBeltMistRenderers[i] = mist.GetComponent<Renderer>();
            }

            for (var i = 0; i < airBeltStreaks.Length; i++)
            {
                var xOffset = Mathf.Lerp(-2.05f, 2.05f, i / (float)(airBeltStreaks.Length - 1));
                var zOffset = Mathf.Lerp(-0.58f, 0.58f, i / (float)(airBeltStreaks.Length - 1));
                var streak = CreateAirflowRibbon($"tutorial air flow texture ribbon {i + 1}", beltVisualRoot.transform, new Vector3(xOffset, 0.33f, zOffset), 1.42f, 0.30f);
                airBeltStreaks[i] = streak.transform;
                airBeltStreakRenderers[i] = streak.GetComponent<Renderer>();
            }

            airBeltParticles = new[]
            {
                CreateAirflowParticles("tutorial airflow cyan mist", beltVisualRoot.transform),
            };

            if (airBeltRenderers[0] != null)
            {
                airBeltRenderers[0].enabled = false;
            }
            if (airBeltArrowRenderers[0] != null)
            {
                airBeltArrowRenderers[0].sharedMaterial = boxIdle;
            }
            UpdateAirBeltVisuals();
        }

        private void CreateTutorialChamberFrame(float centerX, float width, float depth)
        {
            var frontZ = -depth * 0.5f;
            var rearZ = depth * 0.5f;
            CreateBox("tutorial level2 chamber front trim", new Vector3(centerX, 0.105f, frontZ), new Vector3(width, 0.055f, 0.055f), level2TrimMaterial, tutorialAirflowRoot, Quaternion.identity, false);
            CreateBox("tutorial level2 chamber rear trim", new Vector3(centerX, 0.105f, rearZ), new Vector3(width, 0.055f, 0.055f), level2TrimMaterial, tutorialAirflowRoot, Quaternion.identity, false);
            CreateBox("tutorial level2 chamber left trim", new Vector3(centerX - width * 0.5f, 0.105f, 0f), new Vector3(0.055f, 0.055f, depth), level2TrimMaterial, tutorialAirflowRoot, Quaternion.identity, false);
            CreateBox("tutorial level2 chamber right trim", new Vector3(centerX + width * 0.5f, 0.105f, 0f), new Vector3(0.055f, 0.055f, depth), level2TrimMaterial, tutorialAirflowRoot, Quaternion.identity, false);
        }

        private void CreateTutorialWindFloorHints()
        {
            CreateTutorialWindGrille("tutorial level2 wind intake grille", new Vector3(-2.20f, 0.118f, 0f));
            CreateTutorialWindGrille("tutorial level2 wind output grille", new Vector3(2.13f, 0.118f, 0f));
            CreateTutorialWindChevron(new Vector3(-1.13f, 0.142f, 0f), 0.50f);
            CreateTutorialWindChevron(new Vector3(0f, 0.142f, 0f), 0.50f);
            CreateTutorialWindChevron(new Vector3(1.13f, 0.142f, 0f), 0.50f);
        }

        private void CreateTutorialWindGrille(string name, Vector3 position)
        {
            CreateBox(name + " base", position, new Vector3(0.52f, 0.018f, 0.84f), darkStone, tutorialAirflowRoot, Quaternion.identity, false);
            for (var i = 0; i < 5; i++)
            {
                var z = Mathf.Lerp(-0.32f, 0.32f, i / 4f);
                CreateBox($"{name} cyan slit {i}", position + new Vector3(0f, 0.022f, z), new Vector3(0.42f, 0.012f, 0.030f), level2PortalCoreMaterial, tutorialAirflowRoot, Quaternion.identity, false);
            }
        }

        private void CreateTutorialWindChevron(Vector3 position, float length)
        {
            CreateBox("tutorial level2 floor wind chevron upper", position + new Vector3(0f, 0f, 0.11f), new Vector3(length, 0.014f, 0.045f), level2PortalCoreMaterial, tutorialAirflowRoot, Quaternion.Euler(0f, 28f, 0f), false);
            CreateBox("tutorial level2 floor wind chevron lower", position + new Vector3(0f, 0f, -0.11f), new Vector3(length, 0.014f, 0.045f), level2PortalCoreMaterial, tutorialAirflowRoot, Quaternion.Euler(0f, -28f, 0f), false);
        }

        private void BuildLobbyShell()
        {
            DestroyNamed("Menu Temple Lobby");
            lobbyRoot = new GameObject("Menu Temple Lobby").transform;
            CreateBox("lobby obsidian base", new Vector3(0f, -0.45f, 0f), new Vector3(7.6f, 0.42f, 4.1f), darkStone, lobbyRoot, Quaternion.identity, false);
            CreateBox("lobby raised stone", new Vector3(0f, -0.12f, 0f), new Vector3(5.2f, 0.28f, 2.7f), cliffStone, lobbyRoot, Quaternion.identity, false);
            CreateBox("lobby center slab", new Vector3(0f, 0.08f, 0f), new Vector3(3.2f, 0.18f, 1.55f), paleStone, lobbyRoot, Quaternion.identity, false);
            CreateTorus("lobby calibration ring", new Vector3(0f, 0.27f, 0f), 0.82f, 0.055f, tealGlow, lobbyRoot);
            CreateBox("lobby brass axis", new Vector3(0f, 0.31f, 0f), new Vector3(2.5f, 0.045f, 0.08f), brass, lobbyRoot, Quaternion.identity, false);
            CreateBox("lobby brass cross", new Vector3(0f, 0.32f, 0f), new Vector3(0.08f, 0.045f, 1.25f), brass, lobbyRoot, Quaternion.identity, false);
            CreateLobbyPillar(-2.85f, -1.35f);
            CreateLobbyPillar(-2.85f, 1.35f);
            CreateLobbyPillar(2.85f, -1.35f);
            CreateLobbyPillar(2.85f, 1.35f);
        }

        private void CreateLobbyPillar(float x, float z)
        {
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = "Lobby Temple Pillar";
            pillar.transform.SetParent(lobbyRoot, false);
            pillar.transform.position = new Vector3(x, 0.35f, z);
            pillar.transform.localScale = new Vector3(0.25f, 0.65f, 0.25f);
            pillar.GetComponent<Renderer>().sharedMaterial = stone;
            DestroyUnityObject(pillar.GetComponent<Collider>());
            CreateBox("lobby pillar foot", new Vector3(x, -0.06f, z), new Vector3(0.44f, 0.08f, 0.44f), brass, lobbyRoot, Quaternion.identity, false);
            CreateBox("lobby pillar crown", new Vector3(x, 0.76f, z), new Vector3(0.48f, 0.08f, 0.48f), brass, lobbyRoot, Quaternion.identity, false);
        }

        private void SetLobbyVisible(bool visible)
        {
            if (lobbyRoot != null)
            {
                lobbyRoot.gameObject.SetActive(visible);
            }
        }

        private void ReplaceLabObject(PrimitiveType primitive)
        {
            if (labObject != null)
            {
                DestroyUnityObject(labObject);
            }
            labObject = GameObject.CreatePrimitive(primitive);
            labObject.name = $"Gesture Lab {primitive}";
            labObject.transform.SetParent(levelRoot, false);
            labObject.transform.position = new Vector3(0f, 0.7f, 0f);
            labObject.transform.localScale = Vector3.one * 0.75f;
            labObject.GetComponent<Renderer>().sharedMaterial = boxIdle;
            labBody = labObject.AddComponent<Rigidbody>();
            labBody.isKinematic = true;
            labBody.interpolation = RigidbodyInterpolation.Interpolate;
            labRenderer = labObject.GetComponent<Renderer>();
        }

        private void UpdateLevel0(GestureHandFrame hand)
        {
            if (labBody == null)
            {
                return;
            }

            var frame = receiver != null && receiver.HasFreshFrame ? receiver.Latest : GestureFrame.Neutral;
            UpdateTutorialStage(frame);
            var target = ScreenToWorldPlane(hand.pinchX, hand.pinchY, 0.7f);
            var isPinch = IsPinching(hand);
            var twoHandsPinching = TryGetTwoPinchingHands(out var a, out var b);
            var close = Vector3.Distance(target, labBody.position) < 1.0f;
            if (labRenderer != null)
            {
                labRenderer.sharedMaterial = boxIdle;
            }
            if (CanDrawCreateObject() || CanEraseDrawnObject())
            {
                UpdateTutorialDrawing(frame);
            }
            if (CanManipulateDrawnObjects())
            {
                UpdateTutorialDrawnObjectManipulation(hand, target, isPinch, twoHandsPinching, a, b);
            }
            if (!labHeld && isPinch && close && !twoHandsPinching && CanDragLabObject())
            {
                labHeld = true;
                labGrabOffset = labBody.position - target;
                tutorialDragStart = labBody.position;
            }
            if (labHeld && (!isPinch || twoHandsPinching))
            {
                labHeld = false;
            }
            if (!labHeld && close && CanDragLabObject() && labRenderer != null)
            {
                labRenderer.sharedMaterial = boxHover;
            }
            if (labHeld)
            {
                target += labGrabOffset;
                target.x = Mathf.Clamp(target.x, -2.45f, 2.45f);
                target.z = Mathf.Clamp(target.z, -1.35f, 1.35f);
                target.y = 0.7f;
                labBody.MovePosition(target);
                tutorialObjectMoved = Vector3.Distance(labBody.position, tutorialDragStart) > 0.55f;
                if (labRenderer != null)
                {
                    labRenderer.sharedMaterial = boxHeldMaterial;
                }
            }

            if (twoHandsPinching && CanRotateLabObject())
            {
                if (labRenderer != null)
                {
                    labRenderer.sharedMaterial = boxHeldMaterial;
                }
                var angle = Mathf.Atan2(b.pinchY - a.pinchY, b.pinchX - a.pinchX);
                if (twoHandStartDistance <= 0f)
                {
                    var distance = Vector2.Distance(new Vector2(a.pinchX, a.pinchY), new Vector2(b.pinchX, b.pinchY));
                    twoHandStartDistance = Mathf.Max(distance, 0.001f);
                    twoHandStartAngle = angle;
                    twoHandStartRotation = labObject.transform.rotation;
                }
                var deltaDegrees = Mathf.DeltaAngle(twoHandStartAngle * Mathf.Rad2Deg, angle * Mathf.Rad2Deg);
                labObject.transform.rotation = twoHandStartRotation * Quaternion.Euler(0f, deltaDegrees, 0f);
                tutorialObjectRotated = Mathf.Abs(deltaDegrees) > 12f;
            }
            else
            {
                twoHandStartDistance = 0f;
            }

            if (twoHandsPinching && CanPullBridge())
            {
                UpdateTutorialBridgePull(a, b);
            }
            else if (tutorialStage == TutorialStage.BridgePull)
            {
                tutorialBridgeStartDistance = 0f;
            }

            if (CanActivatePalm())
            {
                UpdateTutorialPalmActivation(hand);
            }

            if (CanControlMap() && TryGetTwoFingerMapHands(frame, out var left, out var right))
            {
                var leftPoint = FingerMidpoint(left);
                var rightPoint = FingerMidpoint(right);
                var distance = Vector2.Distance(leftPoint, rightPoint);
                var angle = Mathf.Atan2(rightPoint.y - leftPoint.y, rightPoint.x - leftPoint.x);
                if (twoFingerMapStartDistance <= 0f)
                {
                    twoFingerMapStartDistance = Mathf.Max(distance, 0.001f);
                    twoFingerMapStartAngle = angle;
                    twoFingerMapStartScale = levelRoot.localScale;
                    twoFingerMapStartRotation = levelRoot.rotation;
                }
                var mapScale = Mathf.Clamp(twoFingerMapStartScale.x * distance / twoFingerMapStartDistance, 0.82f, 1.22f);
                levelRoot.localScale = Vector3.one * mapScale;
                levelRoot.rotation = twoFingerMapStartRotation * Quaternion.Euler(0f, (angle - twoFingerMapStartAngle) * Mathf.Rad2Deg, 0f);
                tutorialMapAdjusted = Mathf.Abs(mapScale - twoFingerMapStartScale.x) > 0.06f || Mathf.Abs(Mathf.DeltaAngle(levelRoot.eulerAngles.y, twoFingerMapStartRotation.eulerAngles.y)) > 7f;
            }
            else
            {
                twoFingerMapStartDistance = 0f;
            }

            if (CanPracticeAirflow())
            {
                UpdateTutorialAirflow(frame);
            }
            else
            {
                tutorialAirflowPreviewDirection = 0;
                UpdateTutorialAirflowVisuals(0);
            }

            if (CanPracticeMirrorRotate() && twoHandsPinching)
            {
                UpdateTutorialMirrorRotate(a, b);
            }
            else if (tutorialStage == TutorialStage.MirrorRotate)
            {
                tutorialMirrorHeld = false;
            }

            if (CanPracticeMagnetPolarity())
            {
                UpdateTutorialMagnetPolarity(hand);
            }
        }

        private bool CanDragLabObject()
        {
            return tutorialStage == TutorialStage.OneHandDrag;
        }

        private bool CanRotateLabObject()
        {
            return tutorialStage == TutorialStage.TwoHandRotate;
        }

        private bool CanControlMap()
        {
            return tutorialStage == TutorialStage.MapControl;
        }

        private bool CanPullBridge()
        {
            return tutorialStage == TutorialStage.BridgePull;
        }

        private bool CanActivatePalm()
        {
            return tutorialStage == TutorialStage.PalmActivate;
        }

        private bool CanPracticeAirflow()
        {
            return tutorialStage == TutorialStage.AirflowDirection;
        }

        private bool CanPracticeMirrorRotate()
        {
            return tutorialStage == TutorialStage.MirrorRotate;
        }

        private bool CanPracticeMagnetPolarity()
        {
            return tutorialStage == TutorialStage.MagnetPolarity;
        }

        private bool CanDrawCreateObject()
        {
            return (mode == GameMode.Level0 && tutorialStage == TutorialStage.DrawCreate)
                || (mode == GameMode.Level3 && (level3Stage == Level3Stage.CreateBridgeObject || level3Stage == Level3Stage.PlaceCreatedObject));
        }

        private bool CanEraseDrawnObject()
        {
            return mode == GameMode.Level0 && tutorialStage == TutorialStage.DrawErase;
        }

        private bool CanLevel3EraseDrawnObject()
        {
            return mode == GameMode.Level3;
        }

        private bool CanEraseLevel3LockBlock()
        {
            return mode == GameMode.Level3
                && (level3BridgePlaced || level3Stage == Level3Stage.EraseLockBlock || level3Stage == Level3Stage.RunToGoal);
        }

        private bool CanManipulateDrawnObjects()
        {
            if (TutorialDrawInputActive())
            {
                return false;
            }

            return (mode == GameMode.Level0 && (tutorialStage == TutorialStage.DrawCreate || tutorialStage == TutorialStage.DrawErase))
                || mode == GameMode.Level3;
        }

        private bool TutorialDrawInputActive()
        {
            return tutorialDrawState == TutorialDrawState.StartConfirm
                || tutorialDrawState == TutorialDrawState.Drawing
                || tutorialDrawState == TutorialDrawState.FinishConfirm
                || tutorialDrawState == TutorialDrawState.CancelConfirm;
        }

        private void UpdateTutorialDrawnObjectManipulation(GestureHandFrame hand, Vector3 target, bool isPinch, bool twoHandsPinching, GestureHandFrame a, GestureHandFrame b)
        {
            if (tutorialEraseTarget != null && (!tutorialDrawnObjects.Contains(tutorialEraseTarget) || !CanSelectTutorialDrawnObjectForErase(tutorialEraseTarget)))
            {
                ClearTutorialEraseSelection(true);
            }

            ResetTutorialDrawnObjectMaterials();
            var interactionTarget = target;
            if (twoHandsPinching)
            {
                interactionTarget = ScreenToWorldPlane((a.pinchX + b.pinchX) * 0.5f, (a.pinchY + b.pinchY) * 0.5f, 0.7f);
            }
            var hoverObject = FindNearestTutorialDrawnObject(interactionTarget, twoHandsPinching ? 1.55f : 0.78f);
            var hoverCanMove = hoverObject != null && hoverObject != level3LockBlock;
            if (tutorialDrawObjectHeld && (!isPinch || twoHandsPinching))
            {
                tutorialDrawObjectHeld = false;
                tutorialDrawHeldObject = null;
            }

            if (!tutorialDrawObjectHeld && isPinch && !twoHandsPinching && hoverCanMove && hoverObject != tutorialEraseTarget)
            {
                tutorialDrawObjectHeld = true;
                tutorialDrawHeldObject = hoverObject;
                tutorialDrawGrabOffset = tutorialDrawHeldObject.transform.position - target;
            }

            if (tutorialDrawObjectHeld && tutorialDrawHeldObject != null)
            {
                var position = target + tutorialDrawGrabOffset;
                position.x = Mathf.Clamp(position.x, -2.45f, 2.45f);
                position.z = Mathf.Clamp(position.z, -1.35f, 1.35f);
                position.y = tutorialDrawHeldObject.transform.position.y;
                tutorialDrawHeldObject.transform.position = position;
                SetTutorialDrawnObjectMaterial(tutorialDrawHeldObject, boxHeldMaterial);
            }
            else if (hoverCanMove && hoverObject != tutorialEraseTarget)
            {
                SetTutorialDrawnObjectMaterial(hoverObject, boxHover);
            }

            if (twoHandsPinching && hoverCanMove && hoverObject != tutorialEraseTarget)
            {
                SetTutorialDrawnObjectMaterial(hoverObject, boxHeldMaterial);
                var angle = Mathf.Atan2(b.pinchY - a.pinchY, b.pinchX - a.pinchX);
                if (tutorialDrawRotateStartDistance <= 0f || tutorialDrawRotatingObject != hoverObject)
                {
                    var distance = Vector2.Distance(new Vector2(a.pinchX, a.pinchY), new Vector2(b.pinchX, b.pinchY));
                    tutorialDrawRotateStartDistance = Mathf.Max(distance, 0.001f);
                    tutorialDrawRotateStartAngle = angle;
                    tutorialDrawRotateStartRotation = YawOnly(hoverObject.transform.rotation);
                    tutorialDrawRotatingObject = hoverObject;
                }
                var deltaDegrees = Mathf.DeltaAngle(tutorialDrawRotateStartAngle * Mathf.Rad2Deg, angle * Mathf.Rad2Deg);
                hoverObject.transform.rotation = tutorialDrawRotateStartRotation * Quaternion.Euler(0f, deltaDegrees, 0f);
            }
            else
            {
                tutorialDrawRotateStartDistance = 0f;
                tutorialDrawRotatingObject = null;
            }
        }

        private bool TutorialUsesLabObject()
        {
            return tutorialStage == TutorialStage.OneHandDrag || tutorialStage == TutorialStage.TwoHandRotate;
        }

        private bool IsFinalTutorialStage()
        {
            return tutorialStage == TutorialStage.MagnetPolarity || tutorialStage == TutorialStage.Complete;
        }

        private void SelectTutorialStage(TutorialStage stage)
        {
            tutorialStage = stage;
            ResetTutorialStageProgress();
            ApplyTutorialStageVisibility();
        }

        private void ResetTutorialStageProgress()
        {
            tutorialStageSucceeded = tutorialStage == TutorialStage.Complete;
            tutorialObjectMoved = false;
            tutorialObjectRotated = false;
            tutorialBridgePulled = false;
            tutorialPalmActivated = false;
            tutorialMapAdjusted = false;
            tutorialAirflowDirected = false;
            tutorialDrawInvalid = false;
            tutorialMirrorRotated = false;
            tutorialMagnetPolarityChanged = false;
            tutorialAirflowPreviewDirection = 0;
            tutorialMagnetPreviewPolarity = tutorialStage == TutorialStage.MagnetPolarity ? -1 : 0;
            tutorialMagnetReversalCount = 0;
            labHeld = false;
            tutorialPalmStart = -1f;
            tutorialBridgeStartDistance = 0f;
            tutorialMirrorHeld = false;
            tutorialMirrorStartAngle = 0f;
            tutorialMirrorStartYaw = 0f;
            ResetTutorialMagnetFlipGesture();
            twoHandStartDistance = 0f;
            twoFingerMapStartDistance = 0f;
            tutorialDrawObjectHeld = false;
            tutorialDrawHeldObject = null;
            tutorialDrawGrabOffset = Vector3.zero;
            tutorialDrawRotatingObject = null;
            tutorialDrawRotateStartDistance = 0f;
            ResetTutorialDrawingState(tutorialStage != TutorialStage.DrawCreate);
            if (tutorialStage == TutorialStage.DrawCreate)
            {
                tutorialDrawCreated = false;
                ClearTutorialDrawnObjects();
            }
            if (tutorialStage == TutorialStage.DrawErase)
            {
                tutorialDrawErased = false;
                ClearTutorialDrawnObjects();
                EnsureTutorialErasePracticeObject();
            }
            if (levelRoot != null)
            {
                levelRoot.localScale = Vector3.one;
                levelRoot.rotation = Quaternion.identity;
            }
            if (labObject != null)
            {
                labObject.transform.position = new Vector3(0f, 0.7f, 0f);
                labObject.transform.rotation = Quaternion.identity;
            }
            if (tutorialBridgeLeft != null)
            {
                tutorialBridgeLeft.localPosition = new Vector3(-0.55f, 0.22f, -1.05f);
            }
            if (tutorialBridgeRight != null)
            {
                tutorialBridgeRight.localPosition = new Vector3(0.55f, 0.22f, -1.05f);
            }
            if (tutorialBridgeLeftRenderer != null)
            {
                tutorialBridgeLeftRenderer.sharedMaterial = boxHover;
            }
            if (tutorialBridgeRightRenderer != null)
            {
                tutorialBridgeRightRenderer.sharedMaterial = boxHover;
            }
            if (tutorialSealRenderer != null)
            {
                tutorialSealRenderer.sharedMaterial = amberGlow;
            }
            if (tutorialMirrorProp != null)
            {
                tutorialMirrorProp.rotation = Quaternion.Euler(0f, -18f, 0f);
            }
            if (tutorialMirrorRenderer != null)
            {
                tutorialMirrorRenderer.sharedMaterial = level2TrimMaterial;
            }
            if (tutorialMirrorReflectedBeam != null && tutorialMirrorProp != null)
            {
                var initialReflectedEnd = tutorialMirrorProp.position + Quaternion.Euler(0f, -18f, 0f) * Vector3.right * 1.70f;
                PositionLevel4Beam(tutorialMirrorReflectedBeam, tutorialMirrorProp.position, initialReflectedEnd);
                SetLevel4BeamMaterials(tutorialMirrorReflectedBeam, false);
            }
            UpdateTutorialMagnetVisuals(tutorialMagnetPreviewPolarity);
            SetTutorialAirflowDirection(0);
        }

        private void ApplyTutorialStageVisibility()
        {
            if (labObject != null)
            {
                labObject.SetActive(TutorialUsesLabObject());
            }
            if (tutorialDrawnRoot != null)
            {
                tutorialDrawnRoot.gameObject.SetActive(tutorialStage == TutorialStage.DrawCreate || tutorialStage == TutorialStage.DrawErase);
            }
            if (tutorialBridgeRoot != null)
            {
                tutorialBridgeRoot.gameObject.SetActive(tutorialStage == TutorialStage.BridgePull);
            }
            if (tutorialSealRoot != null)
            {
                tutorialSealRoot.gameObject.SetActive(tutorialStage == TutorialStage.PalmActivate);
            }
            if (tutorialAirflowRoot != null)
            {
                tutorialAirflowRoot.gameObject.SetActive(tutorialStage == TutorialStage.AirflowDirection);
            }
            if (tutorialMirrorRoot != null)
            {
                tutorialMirrorRoot.gameObject.SetActive(tutorialStage == TutorialStage.MirrorRotate);
            }
            if (tutorialMagnetRoot != null)
            {
                tutorialMagnetRoot.gameObject.SetActive(tutorialStage == TutorialStage.MagnetPolarity);
            }
        }

        private void SetTutorialAirflowDirection(int direction)
        {
            if (airBeltDirection != null && airBeltDirection.Length > 0)
            {
                airBeltDirection[0] = direction;
            }
            if (airBeltTriggers != null && airBeltTriggers.Length > 0 && airBeltTriggers[0] != null)
            {
                airBeltTriggers[0].direction = direction;
                if (direction == 0)
                {
                    airBeltTriggers[0].ResetWindState();
                }
            }
            if (airBeltArrowRenderers != null && airBeltArrowRenderers.Length > 0 && airBeltArrowRenderers[0] != null)
            {
                airBeltArrowRenderers[0].sharedMaterial = direction == 1 ? tealGlow : (direction == -1 ? amberGlow : boxIdle);
            }
            if (airBeltArrowTransforms != null && airBeltArrowTransforms.Length > 0 && airBeltArrowTransforms[0] != null)
            {
                airBeltArrowTransforms[0].localPosition = new Vector3(direction < 0 ? -0.18f : 0.18f, 0.28f, -0.70f);
            }
            UpdateAirBeltVisuals();
        }

        private void UpdateTutorialAirflow(GestureFrame frame)
        {
            var direction = 0;
            if (TryGetAirflowHand(frame, out var airflowHand))
            {
                var dirX = airflowHand.landmarks[8].x - airflowHand.landmarks[0].x;
                direction = Mathf.Abs(dirX) < AirflowDirectionDeadZone ? 0 : (dirX > 0f ? 1 : -1);
                if (direction != 0)
                {
                    tutorialAirflowDirected = true;
                }
            }

            tutorialAirflowPreviewDirection = direction;
            UpdateTutorialAirflowVisuals(direction);
        }

        private void UpdateTutorialAirflowVisuals(int direction)
        {
            if (tutorialAirflowPadRenderer != null)
            {
                tutorialAirflowPadRenderer.sharedMaterial = level2FloorMaterial;
            }

            SetTutorialAirflowDirection(direction);
        }

        private void UpdateTutorialDrawing(GestureFrame frame)
        {
            var hasRight = TryGetHandBySide(frame, "Right", out var rightHand);
            var hasLeft = TryGetHandBySide(frame, "Left", out var leftHand);
            var fingertipsTogether = hasLeft && hasRight && IndexTipsTogether(leftHand, rightHand);
            var rightFist = hasRight && IsFist(rightHand);

            if (CanEraseDrawnObject())
            {
                UpdateTutorialDrawErase(frame);
                return;
            }
            if (!CanDrawCreateObject())
            {
                return;
            }

            switch (tutorialDrawState)
            {
                case TutorialDrawState.WaitingStart:
                    if (fingertipsTogether)
                    {
                        tutorialDrawState = TutorialDrawState.StartConfirm;
                        StartTutorialDrawHold();
                    }
                    else
                    {
                        ResetTutorialDrawHold();
                    }
                    break;

                case TutorialDrawState.StartConfirm:
                    if (!fingertipsTogether)
                    {
                        tutorialDrawState = TutorialDrawState.WaitingStart;
                        ResetTutorialDrawHold();
                    }
                    else if (TutorialDrawHoldProgress() >= 1f)
                    {
                        BeginTutorialDrawing(rightHand);
                    }
                    break;

                case TutorialDrawState.Drawing:
                    if (!hasRight)
                    {
                        return;
                    }

                    AddTutorialDrawPoint(rightHand);
                    if (!fingertipsTogether)
                    {
                        tutorialDrawSawFingerSeparation = true;
                    }
                    if (rightFist)
                    {
                        tutorialDrawState = TutorialDrawState.CancelConfirm;
                        StartTutorialDrawHold();
                    }
                    else if (tutorialDrawSawFingerSeparation && fingertipsTogether && tutorialDrawWorldPoints.Count >= 5)
                    {
                        tutorialDrawState = TutorialDrawState.FinishConfirm;
                        StartTutorialDrawHold();
                    }
                    break;

                case TutorialDrawState.FinishConfirm:
                    if (rightFist)
                    {
                        tutorialDrawState = TutorialDrawState.CancelConfirm;
                        StartTutorialDrawHold();
                    }
                    else if (!fingertipsTogether)
                    {
                        tutorialDrawState = TutorialDrawState.Drawing;
                        ResetTutorialDrawHold();
                    }
                    else if (TutorialDrawHoldProgress() >= 1f)
                    {
                        FinishTutorialDrawing();
                    }
                    break;

                case TutorialDrawState.CancelConfirm:
                    if (!rightFist)
                    {
                        tutorialDrawState = TutorialDrawState.Drawing;
                        ResetTutorialDrawHold();
                    }
                    else if (TutorialDrawHoldProgress() >= 1f)
                    {
                        ResetTutorialDrawingState(false);
                        tutorialDrawMessage = "Drawing canceled.";
                        tutorialDrawMessageUntil = Time.time + 1.6f;
                    }
                    break;
            }
        }

        private void BeginTutorialDrawing(GestureHandFrame rightHand)
        {
            tutorialDrawState = TutorialDrawState.Drawing;
            tutorialDrawInvalid = false;
            tutorialDrawMessage = "";
            tutorialDrawSawFingerSeparation = false;
            tutorialDrawScreenPoints.Clear();
            tutorialDrawWorldPoints.Clear();
            ResetTutorialDrawHold();
            AddTutorialDrawPoint(rightHand, true);
        }

        private void AddTutorialDrawPoint(GestureHandFrame hand, bool force = false)
        {
            var screen = new Vector2(hand.indexX * Screen.width, hand.indexY * Screen.height);
            var world = ScreenToWorldPlane(hand.indexX, hand.indexY, 0.64f);
            world.x = Mathf.Clamp(world.x, -2.55f, 2.55f);
            world.z = Mathf.Clamp(world.z, -1.42f, 1.42f);
            world.y = 0.64f;
            var minPointDistance = 0.055f;
            if (!force && tutorialDrawWorldPoints.Count > 0 && Vector3.Distance(tutorialDrawWorldPoints[tutorialDrawWorldPoints.Count - 1], world) < minPointDistance)
            {
                return;
            }

            tutorialDrawScreenPoints.Add(screen);
            tutorialDrawWorldPoints.Add(world);
        }

        private void FinishTutorialDrawing()
        {
            if (!TryClassifyTutorialShape(out var result))
            {
                tutorialDrawInvalid = true;
                tutorialDrawMessage = "Invalid drawing. Draw a circle, rectangle, or triangle.";
                tutorialDrawMessageUntil = Time.time + 2.2f;
                ResetTutorialDrawingState(false);
                return;
            }

            CreateTutorialDrawnObject(result);
            tutorialDrawCreated = true;
            if (mode == GameMode.Level0)
            {
                tutorialStageSucceeded = true;
            }
            else if (mode == GameMode.Level3 && level3Stage == Level3Stage.CreateBridgeObject)
            {
                AdvanceLevel3(Level3Stage.PlaceCreatedObject);
            }
            if (mode == GameMode.Level3)
            {
                level3EraseRequiresGestureReset = true;
            }
            tutorialDrawInvalid = false;
            tutorialDrawMessage = mode == GameMode.Level3 ? "Object created. Move or rotate it onto the glowing plate." : "Object created. You can draw another one or continue.";
            tutorialDrawMessageUntil = Time.time + 2.2f;
            ResetTutorialDrawingState(false);
        }

        private GameObject CreateTutorialDrawnObject(TutorialDrawShapeResult result)
        {
            if (tutorialDrawnRoot == null)
            {
                tutorialDrawnRoot = new GameObject("tutorial drawn objects").transform;
                tutorialDrawnRoot.SetParent(levelRoot, false);
            }

            GameObject drawn;
            switch (result.Shape)
            {
                case TutorialDrawShape.Circle:
                    drawn = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    drawn.name = "Tutorial Drawn Sphere";
                    break;
                case TutorialDrawShape.Rectangle:
                    drawn = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    drawn.name = "Tutorial Drawn Box";
                    break;
                case TutorialDrawShape.Triangle:
                    drawn = CreateTutorialTriangularPyramid("Tutorial Drawn Triangular Pyramid", result);
                    break;
                default:
                    return null;
            }

            drawn.transform.SetParent(tutorialDrawnRoot, false);
            drawn.transform.SetPositionAndRotation(result.Center, result.Rotation);
            drawn.transform.localScale = result.Scale;
            drawn.GetComponent<Renderer>().sharedMaterial = boxIdle;
            drawn.AddComponent<TutorialDrawnShapeMarker>().Shape = result.Shape;
            tutorialDrawnObjects.Add(drawn);
            return drawn;
        }

        private GameObject CreateTutorialDrawnObject(PrimitiveType primitive, Vector3 center, Vector3 scale)
        {
            if (tutorialDrawnRoot == null)
            {
                tutorialDrawnRoot = new GameObject("tutorial drawn objects").transform;
                tutorialDrawnRoot.SetParent(levelRoot, false);
            }

            var drawn = GameObject.CreatePrimitive(primitive);
            drawn.name = $"Tutorial Drawn {primitive}";
            drawn.transform.SetParent(tutorialDrawnRoot, false);
            drawn.transform.position = center;
            drawn.transform.localScale = scale;
            drawn.GetComponent<Renderer>().sharedMaterial = boxIdle;
            var shape = primitive == PrimitiveType.Sphere ? TutorialDrawShape.Circle : TutorialDrawShape.Rectangle;
            drawn.AddComponent<TutorialDrawnShapeMarker>().Shape = shape;
            tutorialDrawnObjects.Add(drawn);
            return drawn;
        }

        private bool TryClassifyTutorialShape(out TutorialDrawShapeResult result)
        {
            result = new TutorialDrawShapeResult
            {
                Shape = TutorialDrawShape.Rectangle,
                Center = new Vector3(0f, 0.7f, 0f),
                Scale = Vector3.one * 0.75f,
                Rotation = Quaternion.identity,
                Radius = 0.36f,
                Height = 0.7f,
            };

            if (tutorialDrawWorldPoints.Count < 8)
            {
                return false;
            }

            var points = BuildTutorialDraw2DPoints();
            if (points.Count < 8)
            {
                return false;
            }

            var minX = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;
            var minZ = float.PositiveInfinity;
            var maxZ = float.NegativeInfinity;
            var pathLength = 0f;
            for (var i = 0; i < points.Count; i++)
            {
                var point = points[i];
                minX = Mathf.Min(minX, point.x);
                maxX = Mathf.Max(maxX, point.x);
                minZ = Mathf.Min(minZ, point.y);
                maxZ = Mathf.Max(maxZ, point.y);
                if (i > 0)
                {
                    pathLength += Vector2.Distance(points[i - 1], point);
                }
            }

            var width = maxX - minX;
            var depth = maxZ - minZ;
            var diagonal = Mathf.Sqrt(width * width + depth * depth);
            if (pathLength < 0.55f || diagonal < 0.24f)
            {
                return false;
            }

            var center2 = new Vector2((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f);
            var directDistance = Vector2.Distance(points[0], points[points.Count - 1]);
            var closed = directDistance < Mathf.Max(0.24f, diagonal * 0.34f);
            if (!closed)
            {
                return false;
            }

            var larger = Mathf.Max(width, depth);
            var smaller = Mathf.Min(width, depth);
            var aspect = smaller / Mathf.Max(larger, 0.001f);
            var templateScores = ScoreTutorialDrawTemplates(points);
            var confidentTemplate = templateScores.BestScore < 0.24f
                && templateScores.SecondScore - templateScores.BestScore > 0.018f;

            if (TryFindTutorialDrawPolygon(points, diagonal, 3, out var triangle) && IsTriangleLike(triangle, diagonal))
            {
                var trianglePerimeter = PolygonPerimeter(triangle);
                var triangleFit = PolygonFitError(points, triangle);
                var triangleMaxFit = PolygonMaxFitError(points, triangle);
                var triangleFineVertices = SimplifyTutorialDrawPolygon(points, diagonal * 0.055f).Count;
                var trianglePathRatio = pathLength / Mathf.Max(trianglePerimeter, 0.001f);
                var triangleTemplateMargin = Mathf.Min(templateScores.Circle, templateScores.Rectangle) - templateScores.Triangle;
                var triangleTemplateValid = templateScores.Triangle < 0.25f
                    && triangleTemplateMargin > 0.016f
                    && (!confidentTemplate || templateScores.BestShape == TutorialDrawShape.Triangle);
                if (triangleFit < diagonal * 0.085f
                    && triangleMaxFit < diagonal * 0.23f
                    && trianglePathRatio < 1.42f
                    && triangleFineVertices <= 5
                    && triangleTemplateValid)
                {
                    var dimensions = TriangleBaseAndHeight(triangle);
                    var radius = Mathf.Clamp(dimensions.x * 0.5f, 0.22f, 0.9f);
                    var height = Mathf.Clamp(dimensions.y, 0.42f, 1.25f);
                    result.Shape = TutorialDrawShape.Triangle;
                    result.Center = new Vector3(center2.x, 0.7f, center2.y);
                    result.Scale = Vector3.one;
                    result.Rotation = Quaternion.identity;
                    result.Radius = radius;
                    result.Height = height;
                    result.TriangleA = triangle[0] - center2;
                    result.TriangleB = triangle[1] - center2;
                    result.TriangleC = triangle[2] - center2;
                    return true;
                }
            }

            var hasRectangle = TryFindTutorialDrawPolygon(points, diagonal, 4, out var rectangle);
            var rectangleFit = hasRectangle ? PolygonFitError(points, rectangle) : float.PositiveInfinity;
            var rectangleMaxFit = hasRectangle ? PolygonMaxFitError(points, rectangle) : float.PositiveInfinity;
            var rectanglePerimeter = hasRectangle ? PolygonPerimeter(rectangle) : 0f;
            var rectanglePathRatio = pathLength / Mathf.Max(rectanglePerimeter, 0.001f);
            var rectangleCornerError = hasRectangle ? RectangleCornerError(rectangle) : float.PositiveInfinity;
            var rectangleValid = hasRectangle
                && IsRectangleLike(rectangle, diagonal)
                && rectangleFit < diagonal * 0.14f
                && rectangleMaxFit < diagonal * 0.32f
                && rectanglePathRatio < 1.34f
                && rectangleCornerError < 0.36f
                && templateScores.Rectangle < 0.25f
                && templateScores.Rectangle <= templateScores.Triangle + 0.012f
                && (!confidentTemplate || templateScores.BestShape == TutorialDrawShape.Rectangle || templateScores.BestShape == TutorialDrawShape.Circle);

            var averageRadius = 0f;
            var radiusDeviation = 0f;
            for (var i = 0; i < points.Count; i++)
            {
                averageRadius += Vector2.Distance(points[i], center2);
            }
            averageRadius /= Mathf.Max(points.Count, 1);
            for (var i = 0; i < points.Count; i++)
            {
                radiusDeviation += Mathf.Abs(Vector2.Distance(points[i], center2) - averageRadius);
            }
            radiusDeviation /= Mathf.Max(points.Count, 1);
            var radialError = radiusDeviation / Mathf.Max(averageRadius, 0.001f);
            var circlePolygon = SimplifyTutorialDrawPolygon(points, diagonal * 0.035f);
            var circlePathRatio = pathLength / Mathf.Max(2f * Mathf.PI * averageRadius, 0.001f);
            var circleValid = aspect > 0.58f
                && radialError < 0.25f
                && circlePathRatio > 0.74f
                && circlePathRatio < 1.30f
                && circlePolygon.Count >= 5
                && circlePolygon.Count != 3
                && templateScores.Circle < 0.24f
                && templateScores.Circle <= templateScores.Triangle + 0.010f
                && (!confidentTemplate || templateScores.BestShape == TutorialDrawShape.Circle || templateScores.BestShape == TutorialDrawShape.Rectangle);

            if (rectangleValid && circleValid)
            {
                var rectangleScore = rectangleFit / diagonal * 1.35f
                    + rectangleMaxFit / diagonal * 0.35f
                    + Mathf.Abs(rectanglePathRatio - 1f) * 0.55f
                    + rectangleCornerError * 0.75f
                    + templateScores.Rectangle * 0.85f;
                var circleScore = radialError * 1.75f
                    + Mathf.Abs(circlePathRatio - 1f) * 0.70f
                    + (1f - aspect) * 0.55f
                    + (circlePolygon.Count <= 5 ? 0.05f : 0f)
                    + templateScores.Circle * 0.85f;
                if (Mathf.Abs(rectangleScore - circleScore) < 0.025f)
                {
                    return false;
                }
                if (rectangleScore < circleScore)
                {
                    return ApplyTutorialRectangleResult(rectangle, points, center2, out result);
                }
                return ApplyTutorialCircleResult(averageRadius, center2, out result);
            }

            if (rectangleValid)
            {
                return ApplyTutorialRectangleResult(rectangle, points, center2, out result);
            }

            if (circleValid)
            {
                return ApplyTutorialCircleResult(averageRadius, center2, out result);
            }

            return false;
        }

        private static TutorialDrawShapeScores ScoreTutorialDrawTemplates(System.Collections.Generic.List<Vector2> points)
        {
            const int sampleCount = 64;
            var resampled = NormalizeTutorialTemplatePoints(ResampleTutorialDrawPoints(points, sampleCount));
            var scores = new TutorialDrawShapeScores
            {
                Circle = float.PositiveInfinity,
                Rectangle = float.PositiveInfinity,
                Triangle = float.PositiveInfinity,
                BestShape = TutorialDrawShape.Rectangle,
                BestScore = float.PositiveInfinity,
                SecondScore = float.PositiveInfinity,
            };

            for (var i = 0; i < 2; i++)
            {
                var reverse = i == 1;
                scores.Circle = Mathf.Min(scores.Circle, MatchTutorialTemplateFamily(resampled, TutorialDrawShape.Circle, reverse, sampleCount));
                scores.Rectangle = Mathf.Min(scores.Rectangle, MatchTutorialTemplateFamily(resampled, TutorialDrawShape.Rectangle, reverse, sampleCount));
                scores.Triangle = Mathf.Min(scores.Triangle, MatchTutorialTemplateFamily(resampled, TutorialDrawShape.Triangle, reverse, sampleCount));
            }

            ApplyTutorialTemplateBest(TutorialDrawShape.Circle, scores.Circle, ref scores);
            ApplyTutorialTemplateBest(TutorialDrawShape.Rectangle, scores.Rectangle, ref scores);
            ApplyTutorialTemplateBest(TutorialDrawShape.Triangle, scores.Triangle, ref scores);
            return scores;
        }

        private static void ApplyTutorialTemplateBest(TutorialDrawShape shape, float score, ref TutorialDrawShapeScores scores)
        {
            if (score < scores.BestScore)
            {
                scores.SecondScore = scores.BestScore;
                scores.BestScore = score;
                scores.BestShape = shape;
            }
            else if (score < scores.SecondScore)
            {
                scores.SecondScore = score;
            }
        }

        private static float MatchTutorialTemplateFamily(System.Collections.Generic.List<Vector2> points, TutorialDrawShape shape, bool reverse, int sampleCount)
        {
            var best = float.PositiveInfinity;
            switch (shape)
            {
                case TutorialDrawShape.Circle:
                    best = Mathf.Min(best, MatchTutorialTemplate(points, CreateTutorialEllipseTemplate(1f, 1f, reverse, sampleCount)));
                    best = Mathf.Min(best, MatchTutorialTemplate(points, CreateTutorialEllipseTemplate(1f, 0.78f, reverse, sampleCount)));
                    best = Mathf.Min(best, MatchTutorialTemplate(points, CreateTutorialEllipseTemplate(0.78f, 1f, reverse, sampleCount)));
                    break;
                case TutorialDrawShape.Rectangle:
                    best = Mathf.Min(best, MatchTutorialTemplate(points, CreateTutorialRectangleTemplate(1f, 1f, reverse, sampleCount)));
                    best = Mathf.Min(best, MatchTutorialTemplate(points, CreateTutorialRectangleTemplate(1f, 0.64f, reverse, sampleCount)));
                    best = Mathf.Min(best, MatchTutorialTemplate(points, CreateTutorialRectangleTemplate(0.64f, 1f, reverse, sampleCount)));
                    best = Mathf.Min(best, MatchTutorialTemplate(points, CreateTutorialRectangleTemplate(1f, 0.42f, reverse, sampleCount)));
                    best = Mathf.Min(best, MatchTutorialTemplate(points, CreateTutorialRectangleTemplate(0.42f, 1f, reverse, sampleCount)));
                    break;
                case TutorialDrawShape.Triangle:
                    best = Mathf.Min(best, MatchTutorialTemplate(points, CreateTutorialTriangleTemplate(new Vector2(0f, 0.62f), new Vector2(-0.58f, -0.48f), new Vector2(0.58f, -0.48f), reverse, sampleCount)));
                    best = Mathf.Min(best, MatchTutorialTemplate(points, CreateTutorialTriangleTemplate(new Vector2(0f, 0.72f), new Vector2(-0.34f, -0.48f), new Vector2(0.34f, -0.48f), reverse, sampleCount)));
                    best = Mathf.Min(best, MatchTutorialTemplate(points, CreateTutorialTriangleTemplate(new Vector2(0f, 0.42f), new Vector2(-0.68f, -0.42f), new Vector2(0.68f, -0.42f), reverse, sampleCount)));
                    best = Mathf.Min(best, MatchTutorialTemplate(points, CreateTutorialTriangleTemplate(new Vector2(-0.56f, 0.52f), new Vector2(-0.56f, -0.50f), new Vector2(0.62f, -0.50f), reverse, sampleCount)));
                    best = Mathf.Min(best, MatchTutorialTemplate(points, CreateTutorialTriangleTemplate(new Vector2(0.56f, 0.52f), new Vector2(-0.62f, -0.50f), new Vector2(0.56f, -0.50f), reverse, sampleCount)));
                    break;
            }
            return best;
        }

        private static float MatchTutorialTemplate(System.Collections.Generic.List<Vector2> points, System.Collections.Generic.List<Vector2> template)
        {
            var best = float.PositiveInfinity;
            for (var angle = 0; angle < 180; angle += 15)
            {
                var rotated = RotateTutorialTemplatePoints(points, angle * Mathf.Deg2Rad);
                for (var shift = 0; shift < rotated.Count; shift += 4)
                {
                    var total = 0f;
                    for (var i = 0; i < rotated.Count; i++)
                    {
                        total += Vector2.Distance(rotated[(i + shift) % rotated.Count], template[i]);
                    }
                    best = Mathf.Min(best, total / rotated.Count);
                }
            }
            return best;
        }

        private static System.Collections.Generic.List<Vector2> CreateTutorialEllipseTemplate(float width, float height, bool reverse, int sampleCount)
        {
            var points = new System.Collections.Generic.List<Vector2>(sampleCount);
            for (var i = 0; i < sampleCount; i++)
            {
                var t = (float)i / sampleCount;
                if (reverse)
                {
                    t = 1f - t;
                }
                var angle = t * Mathf.PI * 2f;
                points.Add(new Vector2(Mathf.Cos(angle) * width * 0.5f, Mathf.Sin(angle) * height * 0.5f));
            }
            return NormalizeTutorialTemplatePoints(points);
        }

        private static System.Collections.Generic.List<Vector2> CreateTutorialRectangleTemplate(float width, float height, bool reverse, int sampleCount)
        {
            var halfW = width * 0.5f;
            var halfH = height * 0.5f;
            var corners = new System.Collections.Generic.List<Vector2>
            {
                new Vector2(-halfW, halfH),
                new Vector2(halfW, halfH),
                new Vector2(halfW, -halfH),
                new Vector2(-halfW, -halfH),
                new Vector2(-halfW, halfH),
            };
            if (reverse)
            {
                corners.Reverse();
            }
            return NormalizeTutorialTemplatePoints(ResampleTutorialDrawPoints(corners, sampleCount));
        }

        private static System.Collections.Generic.List<Vector2> CreateTutorialTriangleTemplate(Vector2 a, Vector2 b, Vector2 c, bool reverse, int sampleCount)
        {
            var corners = new System.Collections.Generic.List<Vector2> { a, b, c, a };
            if (reverse)
            {
                corners = new System.Collections.Generic.List<Vector2> { a, c, b, a };
            }
            return NormalizeTutorialTemplatePoints(ResampleTutorialDrawPoints(corners, sampleCount));
        }

        private static System.Collections.Generic.List<Vector2> ResampleTutorialDrawPoints(System.Collections.Generic.List<Vector2> source, int sampleCount)
        {
            var result = new System.Collections.Generic.List<Vector2>(sampleCount);
            if (source == null || source.Count == 0)
            {
                return result;
            }
            if (source.Count == 1)
            {
                for (var i = 0; i < sampleCount; i++)
                {
                    result.Add(source[0]);
                }
                return result;
            }

            var totalLength = 0f;
            for (var i = 1; i < source.Count; i++)
            {
                totalLength += Vector2.Distance(source[i - 1], source[i]);
            }
            if (totalLength < 0.0001f)
            {
                for (var i = 0; i < sampleCount; i++)
                {
                    result.Add(source[0]);
                }
                return result;
            }

            var interval = totalLength / sampleCount;
            var segmentIndex = 1;
            var distanceBeforeSegment = 0f;
            var nextDistance = 0f;
            for (var sample = 0; sample < sampleCount; sample++)
            {
                while (segmentIndex < source.Count)
                {
                    var segmentLength = Vector2.Distance(source[segmentIndex - 1], source[segmentIndex]);
                    if (distanceBeforeSegment + segmentLength >= nextDistance)
                    {
                        var t = segmentLength < 0.0001f ? 0f : (nextDistance - distanceBeforeSegment) / segmentLength;
                        result.Add(Vector2.Lerp(source[segmentIndex - 1], source[segmentIndex], Mathf.Clamp01(t)));
                        break;
                    }
                    distanceBeforeSegment += segmentLength;
                    segmentIndex++;
                }
                if (result.Count <= sample)
                {
                    result.Add(source[source.Count - 1]);
                }
                nextDistance += interval;
            }
            return result;
        }

        private static System.Collections.Generic.List<Vector2> NormalizeTutorialTemplatePoints(System.Collections.Generic.List<Vector2> source)
        {
            var result = new System.Collections.Generic.List<Vector2>(source.Count);
            if (source.Count == 0)
            {
                return result;
            }

            var minX = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;
            var minY = float.PositiveInfinity;
            var maxY = float.NegativeInfinity;
            for (var i = 0; i < source.Count; i++)
            {
                minX = Mathf.Min(minX, source[i].x);
                maxX = Mathf.Max(maxX, source[i].x);
                minY = Mathf.Min(minY, source[i].y);
                maxY = Mathf.Max(maxY, source[i].y);
            }

            var center = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            var scale = Mathf.Max(maxX - minX, maxY - minY, 0.001f);
            for (var i = 0; i < source.Count; i++)
            {
                result.Add((source[i] - center) / scale);
            }
            return result;
        }

        private static System.Collections.Generic.List<Vector2> RotateTutorialTemplatePoints(System.Collections.Generic.List<Vector2> source, float radians)
        {
            var result = new System.Collections.Generic.List<Vector2>(source.Count);
            var cos = Mathf.Cos(radians);
            var sin = Mathf.Sin(radians);
            for (var i = 0; i < source.Count; i++)
            {
                var point = source[i];
                result.Add(new Vector2(point.x * cos - point.y * sin, point.x * sin + point.y * cos));
            }
            return result;
        }

        private static bool ApplyTutorialRectangleResult(System.Collections.Generic.List<Vector2> rectangle, System.Collections.Generic.List<Vector2> points, Vector2 center2, out TutorialDrawShapeResult result)
        {
            var axis = LongestPolygonEdgeDirection(rectangle);
            var projection = ProjectTutorialDrawBounds(points, axis, center2);
            var yaw = Mathf.Atan2(-axis.y, axis.x) * Mathf.Rad2Deg;
            result = new TutorialDrawShapeResult
            {
                Shape = TutorialDrawShape.Rectangle,
                Center = new Vector3(center2.x, 0.7f, center2.y),
                Scale = new Vector3(Mathf.Clamp(projection.x, 0.42f, 1.55f), 0.48f, Mathf.Clamp(projection.y, 0.32f, 1.25f)),
                Rotation = Quaternion.Euler(0f, yaw, 0f),
                Radius = 0.36f,
                Height = 0.7f,
            };
            return true;
        }

        private static bool ApplyTutorialCircleResult(float averageRadius, Vector2 center2, out TutorialDrawShapeResult result)
        {
            var diameter = Mathf.Clamp(averageRadius * 2f, 0.45f, 1.45f);
            result = new TutorialDrawShapeResult
            {
                Shape = TutorialDrawShape.Circle,
                Center = new Vector3(center2.x, 0.7f, center2.y),
                Scale = Vector3.one * diameter,
                Rotation = Quaternion.identity,
                Radius = diameter * 0.5f,
                Height = 0.7f,
            };
            return true;
        }

        private System.Collections.Generic.List<Vector2> BuildTutorialDraw2DPoints()
        {
            var points = new System.Collections.Generic.List<Vector2>();
            for (var i = 0; i < tutorialDrawWorldPoints.Count; i++)
            {
                var world = tutorialDrawWorldPoints[i];
                var point = new Vector2(world.x, world.z);
                if (points.Count == 0 || Vector2.Distance(points[points.Count - 1], point) >= 0.045f)
                {
                    points.Add(point);
                }
            }
            return points;
        }

        private static bool TryFindTutorialDrawPolygon(System.Collections.Generic.List<Vector2> points, float diagonal, int desiredVertices, out System.Collections.Generic.List<Vector2> polygon)
        {
            polygon = null;
            var factors = new[] { 0.20f, 0.16f, 0.12f, 0.09f, 0.07f };
            for (var i = 0; i < factors.Length; i++)
            {
                var candidate = SimplifyTutorialDrawPolygon(points, diagonal * factors[i]);
                if (candidate.Count == desiredVertices)
                {
                    polygon = candidate;
                    return true;
                }
            }
            return false;
        }

        private static System.Collections.Generic.List<Vector2> SimplifyTutorialDrawPolygon(System.Collections.Generic.List<Vector2> points, float epsilon)
        {
            var open = new System.Collections.Generic.List<Vector2>(points);
            if (open.Count > 2 && Vector2.Distance(open[0], open[open.Count - 1]) < epsilon * 2.5f)
            {
                open.RemoveAt(open.Count - 1);
            }

            var simplified = RdpSimplify(open, epsilon);
            RemoveWeakPolygonVertices(simplified, epsilon * 1.65f);
            return simplified;
        }

        private static System.Collections.Generic.List<Vector2> RdpSimplify(System.Collections.Generic.List<Vector2> points, float epsilon)
        {
            if (points.Count <= 2)
            {
                return new System.Collections.Generic.List<Vector2>(points);
            }

            var index = 0;
            var maxDistance = 0f;
            for (var i = 1; i < points.Count - 1; i++)
            {
                var distance = DistancePointToLine(points[i], points[0], points[points.Count - 1]);
                if (distance > maxDistance)
                {
                    index = i;
                    maxDistance = distance;
                }
            }

            if (maxDistance <= epsilon)
            {
                return new System.Collections.Generic.List<Vector2> { points[0], points[points.Count - 1] };
            }

            var left = RdpSimplify(points.GetRange(0, index + 1), epsilon);
            var right = RdpSimplify(points.GetRange(index, points.Count - index), epsilon);
            left.RemoveAt(left.Count - 1);
            left.AddRange(right);
            return left;
        }

        private static void RemoveWeakPolygonVertices(System.Collections.Generic.List<Vector2> polygon, float minEdge)
        {
            for (var guard = 0; guard < 8 && polygon.Count > 3; guard++)
            {
                var removed = false;
                for (var i = polygon.Count - 1; i >= 0; i--)
                {
                    var previous = polygon[(i - 1 + polygon.Count) % polygon.Count];
                    var current = polygon[i];
                    var next = polygon[(i + 1) % polygon.Count];
                    var shortEdge = Vector2.Distance(previous, current) < minEdge || Vector2.Distance(current, next) < minEdge;
                    var angle = Vector2.Angle((previous - current).normalized, (next - current).normalized);
                    if (shortEdge || angle > 158f)
                    {
                        polygon.RemoveAt(i);
                        removed = true;
                    }
                }
                if (!removed)
                {
                    break;
                }
            }
        }

        private static bool IsTriangleLike(System.Collections.Generic.List<Vector2> polygon, float diagonal)
        {
            if (polygon.Count != 3)
            {
                return false;
            }

            var area = Mathf.Abs(Cross2D(polygon[1] - polygon[0], polygon[2] - polygon[0])) * 0.5f;
            if (area < diagonal * diagonal * 0.055f)
            {
                return false;
            }

            for (var i = 0; i < 3; i++)
            {
                if (Vector2.Distance(polygon[i], polygon[(i + 1) % 3]) < diagonal * 0.18f)
                {
                    return false;
                }
            }
            return true;
        }

        private static Vector2 TriangleBaseAndHeight(System.Collections.Generic.List<Vector2> triangle)
        {
            var baseLength = 0f;
            var altitude = 0f;
            for (var i = 0; i < 3; i++)
            {
                var a = triangle[i];
                var b = triangle[(i + 1) % 3];
                var c = triangle[(i + 2) % 3];
                var edge = Vector2.Distance(a, b);
                if (edge > baseLength)
                {
                    baseLength = edge;
                    altitude = DistancePointToLine(c, a, b);
                }
            }
            return new Vector2(baseLength, altitude);
        }

        private static bool IsRectangleLike(System.Collections.Generic.List<Vector2> polygon, float diagonal)
        {
            if (polygon.Count != 4)
            {
                return false;
            }

            var edges = new float[4];
            for (var i = 0; i < 4; i++)
            {
                edges[i] = Vector2.Distance(polygon[i], polygon[(i + 1) % 4]);
                if (edges[i] < diagonal * 0.13f)
                {
                    return false;
                }
            }

            for (var i = 0; i < 4; i++)
            {
                var previous = (polygon[(i - 1 + 4) % 4] - polygon[i]).normalized;
                var next = (polygon[(i + 1) % 4] - polygon[i]).normalized;
                var angle = Vector2.Angle(previous, next);
                if (angle < 48f || angle > 132f)
                {
                    return false;
                }
            }

            var oppositeA = Mathf.Min(edges[0], edges[2]) / Mathf.Max(edges[0], edges[2]);
            var oppositeB = Mathf.Min(edges[1], edges[3]) / Mathf.Max(edges[1], edges[3]);
            return oppositeA > 0.42f && oppositeB > 0.42f;
        }

        private static float RectangleCornerError(System.Collections.Generic.List<Vector2> polygon)
        {
            if (polygon == null || polygon.Count != 4)
            {
                return float.PositiveInfinity;
            }

            var total = 0f;
            for (var i = 0; i < 4; i++)
            {
                var previous = (polygon[(i - 1 + 4) % 4] - polygon[i]).normalized;
                var next = (polygon[(i + 1) % 4] - polygon[i]).normalized;
                var angle = Vector2.Angle(previous, next);
                total += Mathf.Abs(angle - 90f) / 90f;
            }
            return total / 4f;
        }

        private static float PolygonFitError(System.Collections.Generic.List<Vector2> points, System.Collections.Generic.List<Vector2> polygon)
        {
            if (polygon == null || polygon.Count < 3 || points.Count == 0)
            {
                return float.PositiveInfinity;
            }

            var total = 0f;
            for (var i = 0; i < points.Count; i++)
            {
                var best = float.PositiveInfinity;
                for (var j = 0; j < polygon.Count; j++)
                {
                    var a = polygon[j];
                    var b = polygon[(j + 1) % polygon.Count];
                    best = Mathf.Min(best, DistancePointToLine(points[i], a, b));
                }
                total += best;
            }
            return total / points.Count;
        }

        private static float PolygonMaxFitError(System.Collections.Generic.List<Vector2> points, System.Collections.Generic.List<Vector2> polygon)
        {
            if (polygon == null || polygon.Count < 3 || points.Count == 0)
            {
                return float.PositiveInfinity;
            }

            var max = 0f;
            for (var i = 0; i < points.Count; i++)
            {
                var best = float.PositiveInfinity;
                for (var j = 0; j < polygon.Count; j++)
                {
                    var a = polygon[j];
                    var b = polygon[(j + 1) % polygon.Count];
                    best = Mathf.Min(best, DistancePointToLine(points[i], a, b));
                }
                max = Mathf.Max(max, best);
            }
            return max;
        }

        private static float PolygonPerimeter(System.Collections.Generic.List<Vector2> polygon)
        {
            if (polygon == null || polygon.Count < 2)
            {
                return 0f;
            }

            var perimeter = 0f;
            for (var i = 0; i < polygon.Count; i++)
            {
                perimeter += Vector2.Distance(polygon[i], polygon[(i + 1) % polygon.Count]);
            }
            return perimeter;
        }

        private static Vector2 LongestPolygonEdgeDirection(System.Collections.Generic.List<Vector2> polygon)
        {
            var best = Vector2.right;
            var bestLength = 0f;
            for (var i = 0; i < polygon.Count; i++)
            {
                var edge = polygon[(i + 1) % polygon.Count] - polygon[i];
                if (edge.sqrMagnitude > bestLength)
                {
                    bestLength = edge.sqrMagnitude;
                    best = edge.normalized;
                }
            }
            return best.sqrMagnitude < 0.01f ? Vector2.right : best;
        }

        private static Vector2 ProjectTutorialDrawBounds(System.Collections.Generic.List<Vector2> points, Vector2 axis, Vector2 center)
        {
            var side = new Vector2(-axis.y, axis.x);
            var minA = float.PositiveInfinity;
            var maxA = float.NegativeInfinity;
            var minB = float.PositiveInfinity;
            var maxB = float.NegativeInfinity;
            for (var i = 0; i < points.Count; i++)
            {
                var offset = points[i] - center;
                var a = Vector2.Dot(offset, axis);
                var b = Vector2.Dot(offset, side);
                minA = Mathf.Min(minA, a);
                maxA = Mathf.Max(maxA, a);
                minB = Mathf.Min(minB, b);
                maxB = Mathf.Max(maxB, b);
            }
            return new Vector2(maxA - minA, maxB - minB);
        }

        private static float DistancePointToLine(Vector2 point, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            if (ab.sqrMagnitude < 0.0001f)
            {
                return Vector2.Distance(point, a);
            }

            var t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(point, a + ab * t);
        }

        private static Quaternion YawOnly(Quaternion rotation)
        {
            return Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);
        }

        private GameObject CreateTutorialTriangularPyramid(string name, TutorialDrawShapeResult result)
        {
            var mesh = new Mesh { name = name + " mesh" };
            var baseCenter = (result.TriangleA + result.TriangleB + result.TriangleC) / 3f;
            var height = Mathf.Clamp(result.Height, 0.35f, 1.35f);
            var top = new Vector3(baseCenter.x, height * 0.5f, baseCenter.y);
            var a = new Vector3(result.TriangleA.x, -height * 0.5f, result.TriangleA.y);
            var b = new Vector3(result.TriangleB.x, -height * 0.5f, result.TriangleB.y);
            var c = new Vector3(result.TriangleC.x, -height * 0.5f, result.TriangleC.y);
            var meshCenter = new Vector3(baseCenter.x, 0f, baseCenter.y);
            var vertices = new System.Collections.Generic.List<Vector3>();
            var triangles = new System.Collections.Generic.List<int>();
            AddHardFace(vertices, triangles, top, a, b, ((top + a + b) / 3f) - meshCenter);
            AddHardFace(vertices, triangles, top, b, c, ((top + b + c) / 3f) - meshCenter);
            AddHardFace(vertices, triangles, top, c, a, ((top + c + a) / 3f) - meshCenter);
            AddHardFace(vertices, triangles, a, c, b, Vector3.down);
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var pyramid = new GameObject(name);
            pyramid.AddComponent<MeshFilter>().sharedMesh = mesh;
            pyramid.AddComponent<MeshRenderer>();
            var collider = pyramid.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.convex = true;
            return pyramid;
        }

        private static void AddHardFace(System.Collections.Generic.List<Vector3> vertices, System.Collections.Generic.List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
        {
            var start = vertices.Count;
            var normal = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(normal, outward) < 0f)
            {
                vertices.Add(a);
                vertices.Add(c);
                vertices.Add(b);
            }
            else
            {
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
            }
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
        }

        private void UpdateTutorialDrawErase(GestureFrame frame)
        {
            if (!tutorialDrawErased)
            {
                EnsureTutorialErasePracticeObject();
            }
            var selected = IsIndexCrossEraseGesture(frame, out var crossPoint);
            var target = selected
                ? FindNearestTutorialDrawnObjectByScreenPoint(crossPoint, mode == GameMode.Level3 ? 0.22f : 0.16f, mode == GameMode.Level0 || mode == GameMode.Level3 ? 1.8f : 1.05f)
                : null;
            if (target != null)
            {
                if (tutorialEraseTarget != target)
                {
                    ClearTutorialEraseSelection(true);
                    tutorialEraseTarget = target;
                    tutorialEraseRenderer = tutorialEraseTarget.GetComponent<Renderer>();
                    tutorialEraseIdleMaterial = boxIdle;
                    StartTutorialDrawHold();
                }
                if (tutorialEraseRenderer != null)
                {
                    tutorialEraseRenderer.sharedMaterial = boxHeldMaterial;
                }
                if (TutorialDrawHoldProgress() >= 1f)
                {
                    var erasedObject = tutorialEraseTarget;
                    var erasedLockBlock = mode == GameMode.Level3 && erasedObject == level3LockBlock;
                    tutorialDrawnObjects.Remove(tutorialEraseTarget);
                    DestroyUnityObject(tutorialEraseTarget);
                    ClearTutorialEraseSelection(false);
                    tutorialDrawErased = true;
                    if (mode == GameMode.Level0)
                    {
                        tutorialStageSucceeded = true;
                    }
                    else if (erasedLockBlock)
                    {
                        TriggerLevel3SlideBridgeRelease();
                    }
                    if (mode == GameMode.Level3)
                    {
                        tutorialDrawErased = false;
                        level3EraseRequiresGestureReset = true;
                    }
                    tutorialDrawMessage = erasedLockBlock ? "Lock block erased. The sliding bridge is released." : "Object erased.";
                    tutorialDrawMessageUntil = Time.time + 1.8f;
                    ResetTutorialDrawHold();
                }
            }
            else
            {
                ClearTutorialEraseSelection(true);
            }
        }

        private GameObject FindNearestTutorialDrawnObject(Vector3 point)
        {
            return FindNearestTutorialDrawnObject(point, 1.05f);
        }

        private GameObject FindNearestTutorialDrawnObject(Vector3 point, float maxDistance)
        {
            GameObject nearest = null;
            var nearestDistance = maxDistance;
            for (var i = tutorialDrawnObjects.Count - 1; i >= 0; i--)
            {
                var candidate = tutorialDrawnObjects[i];
                if (candidate == null)
                {
                    tutorialDrawnObjects.RemoveAt(i);
                    continue;
                }
                if (!CanSelectTutorialDrawnObjectForErase(candidate))
                {
                    continue;
                }

                var distance = TutorialDrawnObjectSelectionDistance(point, candidate);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = candidate;
                }
            }
            return nearest;
        }

        private GameObject FindNearestTutorialDrawnObjectByScreenPoint(Vector2 normalizedPoint, float maxScreenDistance, float maxWorldDistance)
        {
            if (mainCamera == null)
            {
                return FindNearestTutorialDrawnObject(ScreenToWorldPlane(normalizedPoint.x, normalizedPoint.y, 0.7f), maxWorldDistance);
            }

            GameObject nearest = null;
            var viewportPoint = HandPointToViewportPoint(normalizedPoint);
            var nearestScreenDistance = maxScreenDistance;
            for (var i = tutorialDrawnObjects.Count - 1; i >= 0; i--)
            {
                var candidate = tutorialDrawnObjects[i];
                if (candidate == null)
                {
                    tutorialDrawnObjects.RemoveAt(i);
                    continue;
                }
                if (!CanSelectTutorialDrawnObjectForErase(candidate))
                {
                    continue;
                }
                if (!TryGetTutorialDrawnObjectBounds(candidate, out var bounds))
                {
                    continue;
                }

                var screenDistance = TutorialDrawnObjectScreenDistance(viewportPoint, bounds);
                if (screenDistance > nearestScreenDistance)
                {
                    continue;
                }

                var worldProbe = ScreenToWorldPlane(normalizedPoint.x, normalizedPoint.y, bounds.center.y);
                var worldDistance = TutorialDrawnObjectSelectionDistance(worldProbe, candidate);
                if (worldDistance > maxWorldDistance)
                {
                    continue;
                }

                nearestScreenDistance = screenDistance;
                nearest = candidate;
            }
            return nearest;
        }

        private bool CanSelectTutorialDrawnObjectForErase(GameObject candidate)
        {
            return candidate != null && (candidate != level3LockBlock || CanEraseLevel3LockBlock());
        }

        private bool TryGetTutorialDrawnObjectBounds(GameObject candidate, out Bounds bounds)
        {
            var collider = candidate.GetComponent<Collider>();
            if (collider != null)
            {
                bounds = collider.bounds;
                return true;
            }

            var renderer = candidate.GetComponent<Renderer>();
            if (renderer != null)
            {
                bounds = renderer.bounds;
                return true;
            }

            bounds = new Bounds(candidate.transform.position, Vector3.one * 0.35f);
            return candidate != null;
        }

        private static Vector2 HandPointToViewportPoint(Vector2 normalizedPoint)
        {
            return new Vector2(Mathf.Clamp01(normalizedPoint.x), Mathf.Clamp01(1f - normalizedPoint.y));
        }

        private float TutorialDrawnObjectScreenDistance(Vector2 viewportPoint, Bounds bounds)
        {
            var best = float.PositiveInfinity;
            if (TryGetProjectedViewportBounds(bounds, out var projectedMin, out var projectedMax))
            {
                const float padding = 0.015f;
                var minX = projectedMin.x - padding;
                var maxX = projectedMax.x + padding;
                var minY = projectedMin.y - padding;
                var maxY = projectedMax.y + padding;
                var dx = viewportPoint.x < minX ? minX - viewportPoint.x : viewportPoint.x > maxX ? viewportPoint.x - maxX : 0f;
                var dy = viewportPoint.y < minY ? minY - viewportPoint.y : viewportPoint.y > maxY ? viewportPoint.y - maxY : 0f;
                best = Mathf.Min(best, new Vector2(dx, dy).magnitude);
            }

            var center = bounds.center;
            best = Mathf.Min(best, ViewportDistance(viewportPoint, center));
            best = Mathf.Min(best, ViewportDistance(viewportPoint, new Vector3(center.x, bounds.min.y, center.z)));
            best = Mathf.Min(best, ViewportDistance(viewportPoint, new Vector3(center.x, bounds.max.y, center.z)));

            for (var x = 0; x < 2; x++)
            {
                for (var y = 0; y < 2; y++)
                {
                    for (var z = 0; z < 2; z++)
                    {
                        var corner = new Vector3(x == 0 ? bounds.min.x : bounds.max.x, y == 0 ? bounds.min.y : bounds.max.y, z == 0 ? bounds.min.z : bounds.max.z);
                        best = Mathf.Min(best, ViewportDistance(viewportPoint, corner));
                    }
                }
            }

            return best;
        }

        private bool TryGetProjectedViewportBounds(Bounds bounds, out Vector2 projectedMin, out Vector2 projectedMax)
        {
            projectedMin = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            projectedMax = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            var hasVisiblePoint = false;
            for (var x = 0; x < 2; x++)
            {
                for (var y = 0; y < 2; y++)
                {
                    for (var z = 0; z < 2; z++)
                    {
                        var corner = new Vector3(x == 0 ? bounds.min.x : bounds.max.x, y == 0 ? bounds.min.y : bounds.max.y, z == 0 ? bounds.min.z : bounds.max.z);
                        var viewport = mainCamera.WorldToViewportPoint(corner);
                        if (viewport.z <= 0f)
                        {
                            continue;
                        }
                        hasVisiblePoint = true;
                        projectedMin = Vector2.Min(projectedMin, new Vector2(viewport.x, viewport.y));
                        projectedMax = Vector2.Max(projectedMax, new Vector2(viewport.x, viewport.y));
                    }
                }
            }
            return hasVisiblePoint;
        }

        private float ViewportDistance(Vector2 viewportPoint, Vector3 worldPoint)
        {
            var viewport = mainCamera.WorldToViewportPoint(worldPoint);
            if (viewport.z <= 0f)
            {
                return float.PositiveInfinity;
            }
            return Vector2.Distance(viewportPoint, new Vector2(viewport.x, viewport.y));
        }

        private static float TutorialDrawnObjectSelectionDistance(Vector3 point, GameObject candidate)
        {
            var collider = candidate.GetComponent<Collider>();
            if (collider != null)
            {
                return BoundsSelectionDistance(point, collider.bounds, collider);
            }

            var renderer = candidate.GetComponent<Renderer>();
            if (renderer != null)
            {
                return BoundsSelectionDistance(point, renderer.bounds, null);
            }

            return DistanceXZ(point, candidate.transform.position);
        }

        private static float BoundsSelectionDistance(Vector3 point, Bounds bounds, Collider collider)
        {
            var best = float.PositiveInfinity;
            var heights = new[] { bounds.min.y, bounds.center.y, bounds.max.y };
            for (var i = 0; i < heights.Length; i++)
            {
                var probe = new Vector3(point.x, heights[i], point.z);
                var closest = collider != null ? collider.ClosestPoint(probe) : bounds.ClosestPoint(probe);
                best = Mathf.Min(best, Vector3.Distance(probe, closest));
            }
            var xzInside = point.x >= bounds.min.x && point.x <= bounds.max.x && point.z >= bounds.min.z && point.z <= bounds.max.z;
            return xzInside ? 0f : best;
        }

        private void ResetTutorialDrawnObjectMaterials()
        {
            for (var i = tutorialDrawnObjects.Count - 1; i >= 0; i--)
            {
                var obj = tutorialDrawnObjects[i];
                if (obj == null)
                {
                    tutorialDrawnObjects.RemoveAt(i);
                    continue;
                }
                if (obj == tutorialEraseTarget || (obj == tutorialDrawHeldObject && tutorialDrawObjectHeld) || obj == tutorialDrawRotatingObject)
                {
                    continue;
                }
                SetTutorialDrawnObjectMaterial(obj, boxIdle);
            }
        }

        private static void SetTutorialDrawnObjectMaterial(GameObject obj, Material material)
        {
            if (obj == null || material == null)
            {
                return;
            }

            var renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        private void EnsureTutorialErasePracticeObject()
        {
            tutorialDrawnObjects.RemoveAll(obj => obj == null);
            if (tutorialDrawnObjects.Count > 0)
            {
                return;
            }
            if (mode == GameMode.Level3)
            {
                return;
            }

            CreateTutorialDrawnObject(PrimitiveType.Cube, new Vector3(0f, 0.7f, 0f), Vector3.one * 0.75f);
        }

        private void ClearTutorialDrawnObjects()
        {
            ClearTutorialEraseSelection(true);
            for (var i = tutorialDrawnObjects.Count - 1; i >= 0; i--)
            {
                if (tutorialDrawnObjects[i] != null)
                {
                    DestroyUnityObject(tutorialDrawnObjects[i]);
                }
            }
            tutorialDrawnObjects.Clear();
            tutorialDrawHeldObject = null;
            tutorialDrawObjectHeld = false;
            tutorialDrawRotatingObject = null;
        }

        private void ResetTutorialDrawingState(bool clearMessage)
        {
            tutorialDrawState = TutorialDrawState.WaitingStart;
            tutorialDrawHoldStart = -1f;
            tutorialDrawSawFingerSeparation = false;
            tutorialDrawScreenPoints.Clear();
            tutorialDrawWorldPoints.Clear();
            ClearTutorialEraseSelection(true);
            if (clearMessage)
            {
                tutorialDrawMessage = "";
                tutorialDrawMessageUntil = -1f;
            }
        }

        private void StartTutorialDrawHold()
        {
            tutorialDrawHoldStart = Time.time;
        }

        private void ResetTutorialDrawHold()
        {
            tutorialDrawHoldStart = -1f;
        }

        private float TutorialDrawHoldProgress()
        {
            return tutorialDrawHoldStart < 0f ? 0f : Mathf.Clamp01((Time.time - tutorialDrawHoldStart) / TutorialDrawConfirmSeconds);
        }

        private string TutorialDrawStatusText()
        {
            var showingErasePrompt = mode == GameMode.Level0 && tutorialStage == TutorialStage.DrawErase;
            showingErasePrompt |= mode == GameMode.Level3 && (!CanDrawCreateObject() || tutorialEraseTarget != null);
            if (showingErasePrompt)
            {
                return tutorialEraseTarget != null
                    ? "Keep both index fingers crossed over the highlighted object to erase it."
                    : "Cross both index fingers over a drawn object to select it.";
            }

            return tutorialDrawState switch
            {
                TutorialDrawState.WaitingStart => "Touch both index fingertips together to prepare drawing.",
                TutorialDrawState.StartConfirm => "Keep fingertips together to start drawing.",
                TutorialDrawState.Drawing => "Draw with the right index fingertip. Left hand movement will not finish the drawing.",
                TutorialDrawState.FinishConfirm => "Keep fingertips together to finish and create the object.",
                TutorialDrawState.CancelConfirm => "Keep the right fist to cancel this drawing.",
                _ => "",
            };
        }

        private void RestoreTutorialEraseTargetMaterial()
        {
            ClearTutorialEraseSelection(true);
        }

        private void ClearTutorialEraseSelection(bool restoreMaterial)
        {
            var hadEraseSelection = tutorialEraseTarget != null || tutorialEraseRenderer != null || tutorialEraseIdleMaterial != null;
            if (tutorialEraseRenderer != null)
            {
                if (restoreMaterial)
                {
                    tutorialEraseRenderer.sharedMaterial = tutorialEraseIdleMaterial != null ? tutorialEraseIdleMaterial : boxIdle;
                }
            }
            tutorialEraseTarget = null;
            tutorialEraseRenderer = null;
            tutorialEraseIdleMaterial = null;
            if (hadEraseSelection || tutorialDrawState == TutorialDrawState.WaitingStart)
            {
                ResetTutorialDrawHold();
            }
        }

        private void UpdateTutorialMirrorRotate(GestureHandFrame a, GestureHandFrame b)
        {
            if (tutorialMirrorProp == null)
            {
                return;
            }

            var angle = Mathf.Atan2(b.pinchY - a.pinchY, b.pinchX - a.pinchX) * Mathf.Rad2Deg;
            if (!tutorialMirrorHeld)
            {
                tutorialMirrorHeld = true;
                tutorialMirrorStartAngle = angle;
                tutorialMirrorStartYaw = NormalizeYaw(tutorialMirrorProp.eulerAngles.y);
            }

            var delta = Mathf.DeltaAngle(tutorialMirrorStartAngle, angle);
            var yaw = Mathf.Clamp(tutorialMirrorStartYaw + delta, -60f, 60f);
            tutorialMirrorProp.rotation = Quaternion.Euler(0f, yaw, 0f);
            var targetPosition = new Vector3(1.22f, 0.42f, 0.52f);
            var aligned = Mathf.Abs(Mathf.DeltaAngle(yaw, Level4MirrorTargetYaw)) < 10f;
            var reflectedEnd = aligned ? targetPosition : tutorialMirrorProp.position + Quaternion.Euler(0f, yaw, 0f) * Vector3.right * 1.70f;
            if (tutorialMirrorReflectedBeam != null)
            {
                PositionLevel4Beam(tutorialMirrorReflectedBeam, tutorialMirrorProp.position, reflectedEnd);
                SetLevel4BeamMaterials(tutorialMirrorReflectedBeam, aligned);
            }
            if (tutorialMirrorRenderer != null)
            {
                tutorialMirrorRenderer.sharedMaterial = boxHeldMaterial;
            }

            if (aligned)
            {
                tutorialMirrorRotated = true;
                if (tutorialMirrorRenderer != null)
                {
                    tutorialMirrorRenderer.sharedMaterial = tealGlow;
                }
            }
        }

        private void UpdateTutorialMagnetPolarity(GestureHandFrame hand)
        {
            if (!TryGetThumbOnlyDirectionFromCurrentFrame(hand, out var direction))
            {
                ResetTutorialMagnetFlipGesture();
                return;
            }

            if (direction == tutorialMagnetPreviewPolarity)
            {
                ResetTutorialMagnetFlipGesture();
                return;
            }

            if (tutorialPendingMagnetDirection != direction)
            {
                tutorialPendingMagnetDirection = direction;
                tutorialPendingMagnetDirectionStart = Time.time;
                return;
            }

            if (Time.time - tutorialPendingMagnetDirectionStart < MagnetThumbDirectionHoldSeconds)
            {
                return;
            }

            tutorialMagnetPreviewPolarity = direction;
            tutorialMagnetReversalCount++;
            tutorialMagnetPolarityChanged = tutorialMagnetReversalCount >= 2;
            ResetTutorialMagnetFlipGesture();
            UpdateTutorialMagnetVisuals(tutorialMagnetPreviewPolarity);
        }

        private void ResetTutorialMagnetFlipGesture()
        {
            tutorialPendingMagnetDirection = 0;
            tutorialPendingMagnetDirectionStart = -1f;
        }

        private void UpdateTutorialMagnetVisuals(int polarity)
        {
            if (tutorialMagnetRenderer != null)
            {
                tutorialMagnetRenderer.sharedMaterial = level2TrimMaterial;
            }
            if (tutorialMagnetDisk != null)
            {
                tutorialMagnetDisk.localRotation = Quaternion.Euler(0f, polarity < 0 ? 180f : 0f, 0f);
            }
        }

        private static bool TryGetThumbOnlyDirection(GestureHandFrame hand, out int direction)
        {
            direction = 0;
            if (hand.score < 0.45f || hand.landmarks == null || hand.landmarks.Length < 21 || hand.palmSpan <= 0f)
            {
                return false;
            }

            var fourFingersFolded =
                !hand.indexExtended && FingerFoldedIntoFist(hand, 8, 6, 5) &&
                !hand.middleExtended && FingerFoldedIntoFist(hand, 12, 10, 9) &&
                !hand.ringExtended && FingerFoldedIntoFist(hand, 16, 14, 13) &&
                !hand.pinkyExtended && FingerFoldedIntoFist(hand, 20, 18, 17);
            var thumbTip = hand.landmarks[4];
            var thumbIp = hand.landmarks[3];
            var wrist = hand.landmarks[0];
            var thumbExtended = hand.thumbExtended || LandmarkDistance(thumbTip, wrist) > LandmarkDistance(thumbIp, wrist) * 1.08f;
            if (!thumbExtended || !fourFingersFolded)
            {
                return false;
            }

            var thumbMcp = hand.landmarks[2];
            var dx = thumbTip.x - thumbMcp.x;
            var dy = thumbTip.y - thumbMcp.y;
            if (Mathf.Abs(dx) < MagnetThumbDirectionDeadZone || Mathf.Abs(dx) < Mathf.Abs(dy) * 0.65f)
            {
                return false;
            }

            direction = dx > 0f ? 1 : -1;
            return true;
        }

        private static bool FingerFoldedIntoFist(GestureHandFrame hand, int tipIndex, int pipIndex, int mcpIndex)
        {
            var wrist = hand.landmarks[0];
            var tip = hand.landmarks[tipIndex];
            var pip = hand.landmarks[pipIndex];
            var mcp = hand.landmarks[mcpIndex];
            var span = Mathf.Max(hand.palmSpan, 0.0001f);
            var tipToWrist = LandmarkDistance(tip, wrist) / span;
            var pipToWrist = LandmarkDistance(pip, wrist) / span;
            var tipToMcp = LandmarkDistance(tip, mcp) / span;
            return tipToWrist < pipToWrist * 1.10f || tipToMcp < 0.88f;
        }

        private bool TryGetThumbOnlyDirectionFromCurrentFrame(GestureHandFrame fallbackHand, out int direction)
        {
            var frame = receiver != null && receiver.HasFreshFrame ? receiver.Latest : GestureFrame.Neutral;
            var bestScore = -1f;
            direction = 0;
            if (frame.hands != null)
            {
                foreach (var hand in frame.hands)
                {
                    if (hand.score <= bestScore || !TryGetThumbOnlyDirection(hand, out var candidate))
                    {
                        continue;
                    }

                    bestScore = hand.score;
                    direction = candidate;
                }
            }

            return bestScore >= 0f || TryGetThumbOnlyDirection(fallbackHand, out direction);
        }

        private void UpdateTutorialBridgePull(GestureHandFrame a, GestureHandFrame b)
        {
            if (tutorialBridgeLeft == null || tutorialBridgeRight == null)
            {
                return;
            }

            var distance = Vector2.Distance(new Vector2(a.pinchX, a.pinchY), new Vector2(b.pinchX, b.pinchY));
            if (tutorialBridgeStartDistance <= 0f)
            {
                tutorialBridgeStartDistance = Mathf.Max(distance, 0.001f);
            }

            var t = Mathf.Clamp01(1f - distance / tutorialBridgeStartDistance);
            tutorialBridgeLeft.localPosition = Vector3.Lerp(new Vector3(-0.55f, 0.22f, -1.05f), new Vector3(-0.16f, 0.22f, -1.05f), t);
            tutorialBridgeRight.localPosition = Vector3.Lerp(new Vector3(0.55f, 0.22f, -1.05f), new Vector3(0.16f, 0.22f, -1.05f), t);
            if (tutorialBridgeLeftRenderer != null)
            {
                tutorialBridgeLeftRenderer.sharedMaterial = boxHeldMaterial;
            }
            if (tutorialBridgeRightRenderer != null)
            {
                tutorialBridgeRightRenderer.sharedMaterial = boxHeldMaterial;
            }
            if (distance < tutorialBridgeStartDistance * 0.72f)
            {
                tutorialBridgePulled = true;
                tutorialBridgeLeft.localPosition = new Vector3(-0.16f, 0.22f, -1.05f);
                tutorialBridgeRight.localPosition = new Vector3(0.16f, 0.22f, -1.05f);
            }
        }

        private void UpdateTutorialPalmActivation(GestureHandFrame hand)
        {
            if (tutorialSealRenderer == null)
            {
                return;
            }

            var target = ScreenToWorldPlane(hand.indexX, hand.indexY, 0.16f);
            var nearSeal = DistanceXZ(target, new Vector3(0f, 0f, 1.05f)) < 0.82f;
            if (hand.openPalm && hand.score >= 0.45f && nearSeal)
            {
                tutorialPalmStart = tutorialPalmStart < 0f ? Time.time : tutorialPalmStart;
                tutorialSealRenderer.sharedMaterial = boxHeldMaterial;
                if (Time.time - tutorialPalmStart >= 0.7f)
                {
                    tutorialPalmActivated = true;
                }
            }
            else if (!tutorialPalmActivated)
            {
                tutorialPalmStart = -1f;
                tutorialSealRenderer.sharedMaterial = amberGlow;
            }

            if (tutorialPalmActivated)
            {
                tutorialSealRenderer.sharedMaterial = tealGlow;
            }
        }

        private void UpdateTutorialStage(GestureFrame frame)
        {
            switch (tutorialStage)
            {
                case TutorialStage.FindHands:
                    tutorialStageSucceeded |= HasLeftAndRightHands(frame);
                    break;
                case TutorialStage.OneHandDrag:
                    tutorialStageSucceeded |= tutorialObjectMoved;
                    break;
                case TutorialStage.TwoHandRotate:
                    tutorialStageSucceeded |= tutorialObjectRotated;
                    break;
                case TutorialStage.BridgePull:
                    tutorialStageSucceeded |= tutorialBridgePulled;
                    break;
                case TutorialStage.PalmActivate:
                    tutorialStageSucceeded |= tutorialPalmActivated;
                    break;
                case TutorialStage.MapControl:
                    tutorialStageSucceeded |= tutorialMapAdjusted;
                    break;
                case TutorialStage.AirflowDirection:
                    tutorialStageSucceeded |= tutorialAirflowDirected;
                    break;
                case TutorialStage.DrawCreate:
                    tutorialStageSucceeded |= tutorialDrawCreated;
                    break;
                case TutorialStage.DrawErase:
                    tutorialStageSucceeded |= tutorialDrawErased;
                    break;
                case TutorialStage.MirrorRotate:
                    tutorialStageSucceeded |= tutorialMirrorRotated;
                    break;
                case TutorialStage.MagnetPolarity:
                    tutorialStageSucceeded |= tutorialMagnetPolarityChanged;
                    break;
                case TutorialStage.Complete:
                    labCompleted = true;
                    tutorialStageSucceeded = true;
                    break;
            }
        }

        private bool TutorialStageSucceeded()
        {
            return tutorialStageSucceeded || tutorialStage == TutorialStage.Complete;
        }

        private void AdvanceTutorialStage()
        {
            var nextStage = tutorialStage;
            switch (tutorialStage)
            {
                case TutorialStage.FindHands:
                    nextStage = TutorialStage.OneHandDrag;
                    break;
                case TutorialStage.OneHandDrag:
                    nextStage = TutorialStage.TwoHandRotate;
                    break;
                case TutorialStage.TwoHandRotate:
                    nextStage = TutorialStage.BridgePull;
                    break;
                case TutorialStage.BridgePull:
                    nextStage = TutorialStage.PalmActivate;
                    break;
                case TutorialStage.PalmActivate:
                    nextStage = TutorialStage.MapControl;
                    break;
                case TutorialStage.MapControl:
                    nextStage = TutorialStage.AirflowDirection;
                    break;
                case TutorialStage.AirflowDirection:
                    nextStage = TutorialStage.DrawCreate;
                    break;
                case TutorialStage.DrawCreate:
                    nextStage = TutorialStage.DrawErase;
                    break;
                case TutorialStage.DrawErase:
                    nextStage = TutorialStage.MirrorRotate;
                    break;
                case TutorialStage.MirrorRotate:
                    nextStage = TutorialStage.MagnetPolarity;
                    break;
                case TutorialStage.MagnetPolarity:
                    StartLevel(GameMode.Level1);
                    return;
            }

            if (nextStage != tutorialStage)
            {
                SelectTutorialStage(nextStage);
            }
            else
            {
                tutorialStageSucceeded = tutorialStage == TutorialStage.Complete;
                labHeld = false;
                twoHandStartDistance = 0f;
                twoFingerMapStartDistance = 0f;
                tutorialAirflowPreviewDirection = 0;
            }
            if (tutorialStage == TutorialStage.Complete)
            {
                labCompleted = true;
            }
        }

        private string TutorialTitle()
        {
            return tutorialStage switch
            {
                TutorialStage.FindHands => "1/11 Move your hands freely.",
                TutorialStage.OneHandDrag => "2/11 Pinch an object with one hand and drag it.",
                TutorialStage.TwoHandRotate => "3/11 Pinch both sides and rotate the object.",
                TutorialStage.BridgePull => "4/11 Join a bridge with both hands.",
                TutorialStage.PalmActivate => "5/11 Open your palm over the glowing seal.",
                TutorialStage.MapControl => "6/11 Join index+middle on both hands.",
                TutorialStage.AirflowDirection => "7/11 Point the airflow with one hand.",
                TutorialStage.DrawCreate => "8/11 Draw a new object.",
                TutorialStage.DrawErase => "9/11 Erase a drawn object.",
                TutorialStage.MirrorRotate => "10/11 Rotate the side mirror.",
                TutorialStage.MagnetPolarity => "11/11 Flip magnetic poles.",
                TutorialStage.Complete => "Tutorial complete.",
                _ => "",
            };
        }

        private string TutorialDetail()
        {
            return tutorialStage switch
            {
                TutorialStage.FindHands => "Move both hands freely and watch how the game recognizes them on screen.",
                TutorialStage.OneHandDrag => "Touch thumb and index finger, grab the object, then move it across the practice slab.",
                TutorialStage.TwoHandRotate => "Pinch the object from both sides, then turn your hands like rotating a real block.",
                TutorialStage.BridgePull => "Pinch both bridge halves, then move your hands closer together until they lock.",
                TutorialStage.PalmActivate => "Open one hand and hold it over the glowing seal until it lights up.",
                TutorialStage.MapControl => "On each hand, keep index and middle fingertips close, with ring and pinky folded, then move both hands to adjust the map.",
                TutorialStage.AirflowDirection => "Use one hand: extend your thumb, keep index and middle together, fold ring and pinky, then point left or right.",
                TutorialStage.DrawCreate => "Touch both index fingertips for 2 seconds to start. Draw a circle, rectangle, or triangle with the right index fingertip, then touch both index fingertips again for 2 seconds to create.",
                TutorialStage.DrawErase => "Cross both index fingers over a drawn object, keep it highlighted, and hold for 2 seconds to erase it.",
                TutorialStage.MirrorRotate => "Pinch with both hands and rotate them until the mirror points at the glowing receiver.",
                TutorialStage.MagnetPolarity => "Make a fist with only the thumb extended. Point it right, then left, to reverse the bar magnet twice.",
                TutorialStage.Complete => "Hold over Next: Level 1 when ready.",
                _ => "",
            };
        }

        private string HandStatusText()
        {
            var frame = receiver != null && receiver.HasFreshFrame ? receiver.Latest : GestureFrame.Neutral;
            var left = HasHandedness(frame, "Left") ? "Left: yes" : "Left: waiting";
            var right = HasHandedness(frame, "Right") ? "Right: yes" : "Right: waiting";
            if (tutorialStage == TutorialStage.AirflowDirection || tutorialStage == TutorialStage.Complete)
            {
                var airflow = tutorialAirflowPreviewDirection > 0 ? "Airflow: RIGHT" : (tutorialAirflowPreviewDirection < 0 ? "Airflow: LEFT" : "Airflow: waiting");
                return $"{left}    {right}    Hands: {frame.handCount}    {airflow}";
            }
            if (tutorialStage == TutorialStage.MagnetPolarity)
            {
                var polarity = tutorialMagnetPreviewPolarity > 0 ? "Blue: RIGHT" : "Blue: LEFT";
                return $"{left}    {right}    Hands: {frame.handCount}    {polarity}    Reversals: {tutorialMagnetReversalCount}/2";
            }
            return $"{left}    {right}    Hands: {frame.handCount}";
        }

        private void BuildLevel1()
        {
            levelRoot = new GameObject("Level01 First Path").transform;
            var roadRotation = Quaternion.Euler(0f, 0f, Level1RoadAngleDegrees);
            level1Stage = Level1Stage.ClearBlock;
            bridgeLocked = false;
            rotateGateLocked = false;
            sealActivated = false;
            rotateGateHeld = false;
            level1RotateRequiresPinchReset = false;
            level1BridgeStartDistance = 0f;
            sealHoldStart = -1f;
            level1SuccessUntil = -1f;

            CreateBox("trial void shadow plinth", new Vector3(0f, -0.62f, 0f), new Vector3(10.9f, 0.42f, 5.2f), darkStone, levelRoot, Quaternion.identity, false);
            CreateBox("trial dungeon base", new Vector3(0f, -0.28f, 0f), new Vector3(9.9f, 0.34f, 3.9f), level2WallMaterial, levelRoot, Quaternion.identity, false);
            CreateBox("road segment start", new Vector3(-3.25f, RoadY(-3.25f), 0f), new Vector3(2.45f, 0.28f, 1.55f), level2FloorMaterial, levelRoot, roadRotation, true);
            CreateBox("road segment bridge approach", new Vector3(-0.33f, RoadY(-0.33f), 0f), new Vector3(1.56f, 0.28f, 1.55f), level2FloorMaterial, levelRoot, roadRotation, true);
            CreateBox("road segment finish", new Vector3(3.25f, RoadY(3.25f), 0f), new Vector3(2.30f, 0.28f, 1.55f), level2FloorMaterial, levelRoot, roadRotation, true);
            CreateBox("road start brass centerline", new Vector3(-3.25f, RoadY(-3.25f) + 0.155f, 0f), new Vector3(2.16f, 0.018f, 0.055f), level2TrimMaterial, levelRoot, roadRotation, false);
            CreateBox("road approach brass centerline", new Vector3(-0.33f, RoadY(-0.33f) + 0.155f, 0f), new Vector3(1.25f, 0.018f, 0.055f), level2TrimMaterial, levelRoot, roadRotation, false);
            CreateBox("road finish brass centerline", new Vector3(3.25f, RoadY(3.25f) + 0.155f, 0f), new Vector3(1.96f, 0.018f, 0.055f), level2TrimMaterial, levelRoot, roadRotation, false);

            obstacleBox = CreateBox("Pinch Movable Obstacle Box", new Vector3(-2.85f, RoadY(-2.85f) + 0.37f, 0f), new Vector3(0.72f, 0.72f, 0.98f), level2TrimMaterial, levelRoot, roadRotation, true).AddComponent<Rigidbody>();
            obstacleBox.isKinematic = true;
            obstacleBox.interpolation = RigidbodyInterpolation.Interpolate;
            obstacleBox.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            obstacleRenderer = obstacleBox.GetComponent<Renderer>();
            var slot = CreateBox("block open side slot", new Vector3(-2.85f, RoadY(-2.85f) + 0.09f, 1.18f), new Vector3(1.05f, 0.045f, 0.82f), level2RuneMaterial, levelRoot, roadRotation, false);
            blockSlotRenderer = slot.GetComponent<Renderer>();

            startGate = CreateGate("start release gate", -3.55f);
            bridgeGate = CreateGate("bridge release gate", -2.05f);
            rotateGateStop = CreateGate("rotating bridge release gate", 0.35f);
            goalGate = CreateGate("goal seal gate", 2.55f);

            var leftBridge = CreateBox("left sliding bridge half", new Vector3(-1.4f, RoadY(-1.4f) + 0.02f, -0.92f), new Vector3(1.3f, 0.18f, 0.56f), level2TrimMaterial, levelRoot, roadRotation, true);
            var rightBridge = CreateBox("right sliding bridge half", new Vector3(-1.4f, RoadY(-1.4f) + 0.02f, 0.92f), new Vector3(1.3f, 0.18f, 0.56f), level2TrimMaterial, levelRoot, roadRotation, true);
            bridgeLeft = leftBridge.transform;
            bridgeRight = rightBridge.transform;
            bridgeLeftRenderer = leftBridge.GetComponent<Renderer>();
            bridgeRightRenderer = rightBridge.GetComponent<Renderer>();

            var rotateGateObject = CreateBox("simple rotating plank bridge", new Vector3(1.20f, RoadY(1.20f) + 0.095f, 0f), new Vector3(2.12f, 0.08f, 0.46f), level2TrimMaterial, levelRoot, roadRotation * Quaternion.Euler(0f, Level1RotateBridgeStartYaw, 0f), true);
            rotateGate = rotateGateObject.transform;
            rotateGateRenderer = rotateGateObject.GetComponent<Renderer>();

            var seal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            seal.name = "final palm seal";
            seal.transform.SetParent(levelRoot, false);
            seal.transform.position = new Vector3(3.35f, RoadY(3.35f) + 0.14f, 1.22f);
            seal.transform.localScale = new Vector3(0.58f, 0.05f, 0.58f);
            sealRenderer = seal.GetComponent<Renderer>();
            sealRenderer.sharedMaterial = amberGlow;
            CreateTorus("final seal ring", seal.transform.position + new Vector3(0f, 0.06f, 0f), 0.53f, 0.04f, tealGlow, levelRoot);

            var ballObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballObject.name = "Golden Physics Ball";
            ballObject.transform.SetParent(levelRoot, false);
            ballObject.transform.position = new Vector3(-4.32f, RoadY(-4.32f) + 0.46f, 0f);
            ballObject.transform.localScale = Vector3.one * 0.46f;
            ballObject.GetComponent<Renderer>().sharedMaterial = ballMaterial;
            var body = ballObject.AddComponent<Rigidbody>();
            body.mass = 1.1f;
            body.linearDamping = 0.08f;
            body.angularDamping = 0.03f;
            levelBall = ballObject.AddComponent<BallController>();

            var goal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            goal.name = "Goal Trigger - Sacred Altar";
            goal.transform.SetParent(levelRoot, false);
            goal.transform.position = new Vector3(4.1f, RoadY(4.1f) + 0.2f, 0f);
            goal.transform.localScale = new Vector3(0.7f, 0.11f, 0.7f);
            goal.GetComponent<Renderer>().sharedMaterial = level2PortalCoreMaterial;
            DestroyUnityObject(goal.GetComponent<Collider>());
            levelBall.Configure(goal.transform);
            CreateLevel2GoalArt(goal.transform.position);
        }

        private void UpdateLevel1(GestureHandFrame hand)
        {
            if (obstacleBox == null)
            {
                return;
            }

            var target = MapPinchToLevel1DragPlane(hand.pinchX, hand.pinchY);
            var isPinch = IsPinching(hand);
            var twoHandsPinching = TryGetTwoPinchingHands(out var a, out var b);
            UpdateLevel1Block(target, isPinch);
            UpdateLevel1Bridge(a, b, twoHandsPinching);
            UpdateLevel1RotatingGate(a, b, twoHandsPinching);
            UpdateLevel1Seal(hand);
        }

        private void UpdateLevel1Block(Vector3 target, bool isPinch)
        {
            if (level1Stage != Level1Stage.ClearBlock)
            {
                boxHeld = false;
                if (obstacleRenderer != null)
                {
                    obstacleRenderer.sharedMaterial = boxHeldMaterial;
                }
                return;
            }

            var close = DistanceXZ(target, obstacleBox.position) < (isPinch ? 1.65f : 0.90f);
            obstacleRenderer.sharedMaterial = level2TrimMaterial;
            if (!boxHeld && close)
            {
                obstacleRenderer.sharedMaterial = boxHover;
            }
            if (!boxHeld && isPinch && close)
            {
                boxHeld = true;
                var snapPos = target;
                snapPos.y = RoadY(snapPos.x) + 0.37f;
                obstacleBox.MovePosition(snapPos);
                level1BoxGrabOffset = Vector3.zero;
            }
            if (boxHeld && !isPinch)
            {
                boxHeld = false;
            }
            if (boxHeld)
            {
                obstacleRenderer.sharedMaterial = boxHeldMaterial;
                target += level1BoxGrabOffset;
                target.x = Mathf.Clamp(target.x, -3.45f, -2.15f);
                target.z = Mathf.Clamp(target.z, -0.70f, 1.46f);
                target.y = RoadY(target.x) + 0.37f;
                obstacleBox.MovePosition(target);
                obstacleBox.linearVelocity = Vector3.zero;
                obstacleBox.angularVelocity = Vector3.zero;
            }

            if (level1Stage == Level1Stage.ClearBlock && DistanceXZ(obstacleBox.position, new Vector3(-2.85f, 0f, 1.18f)) < 0.48f)
            {
                obstacleRenderer.sharedMaterial = boxHeldMaterial;
                blockSlotRenderer.sharedMaterial = tealGlow;
                OpenGate(startGate);
                AdvanceLevel1(Level1Stage.JoinBridge);
            }
        }

        private void UpdateLevel1Bridge(GestureHandFrame a, GestureHandFrame b, bool twoHandsPinching)
        {
            if (bridgeLocked || level1Stage != Level1Stage.JoinBridge || !twoHandsPinching || bridgeLeft == null || bridgeRight == null)
            {
                if (!bridgeLocked && level1Stage == Level1Stage.JoinBridge)
                {
                    level1BridgeStartDistance = 0f;
                }
                return;
            }

            var distance = Vector2.Distance(new Vector2(a.pinchX, a.pinchY), new Vector2(b.pinchX, b.pinchY));
            if (level1BridgeStartDistance <= 0f)
            {
                level1BridgeStartDistance = Mathf.Max(distance, 0.001f);
            }

            var t = Mathf.Clamp01(1f - distance / level1BridgeStartDistance);
            var y = RoadY(-1.4f) + 0.02f;
            bridgeLeft.position = Vector3.Lerp(new Vector3(-1.4f, y, -0.92f), new Vector3(-1.4f, y, -0.28f), t);
            bridgeRight.position = Vector3.Lerp(new Vector3(-1.4f, y, 0.92f), new Vector3(-1.4f, y, 0.28f), t);
            bridgeLeftRenderer.sharedMaterial = boxHeldMaterial;
            bridgeRightRenderer.sharedMaterial = boxHeldMaterial;
            if (distance < level1BridgeStartDistance * 0.72f)
            {
                bridgeLocked = true;
                bridgeLeft.position = new Vector3(-1.4f, y, -0.28f);
                bridgeRight.position = new Vector3(-1.4f, y, 0.28f);
                bridgeLeftRenderer.sharedMaterial = tealGlow;
                bridgeRightRenderer.sharedMaterial = tealGlow;
                OpenGate(bridgeGate);
                level1RotateRequiresPinchReset = true;
                rotateGateHeld = false;
                AdvanceLevel1(Level1Stage.RotateGate);
            }
        }

        private void UpdateLevel1RotatingGate(GestureHandFrame a, GestureHandFrame b, bool twoHandsPinching)
        {
            if (rotateGateLocked || level1Stage != Level1Stage.RotateGate || rotateGate == null)
            {
                return;
            }

            if (level1RotateRequiresPinchReset)
            {
                rotateGateHeld = false;
                if (!twoHandsPinching)
                {
                    level1RotateRequiresPinchReset = false;
                }
                else
                {
                    if (rotateGateRenderer != null)
                    {
                        rotateGateRenderer.sharedMaterial = boxHover;
                    }
                    return;
                }
            }

            if (twoHandsPinching)
            {
                var angle = Mathf.Atan2(b.pinchY - a.pinchY, b.pinchX - a.pinchX);
                if (!rotateGateHeld)
                {
                    rotateGateHeld = true;
                    level1RotateStartAngle = angle;
                }
                var deltaDegrees = Mathf.DeltaAngle(level1RotateStartAngle * Mathf.Rad2Deg, angle * Mathf.Rad2Deg);
                var yaw = Mathf.Clamp(Level1RotateBridgeStartYaw - Mathf.Abs(deltaDegrees), 0f, Level1RotateBridgeStartYaw);
                rotateGate.rotation = Quaternion.Euler(0f, 0f, Level1RoadAngleDegrees) * Quaternion.Euler(0f, yaw, 0f);
                rotateGateRenderer.sharedMaterial = boxHeldMaterial;
                if (yaw <= 12f)
                {
                    rotateGateLocked = true;
                    rotateGate.rotation = Quaternion.Euler(0f, 0f, Level1RoadAngleDegrees);
                    rotateGateRenderer.sharedMaterial = tealGlow;
                    OpenGate(rotateGateStop);
                    AdvanceLevel1(Level1Stage.ActivateSeal);
                }
            }
            else
            {
                rotateGateHeld = false;
                if (!rotateGateLocked)
                {
                    rotateGateRenderer.sharedMaterial = boxHover;
                }
            }
        }

        private void UpdateLevel1Seal(GestureHandFrame hand)
        {
            if (sealActivated || level1Stage != Level1Stage.ActivateSeal)
            {
                return;
            }

            var target = ScreenToWorldPlane(hand.indexX, hand.indexY, RoadY(3.35f) + 0.14f);
            var nearSeal = DistanceXZ(target, new Vector3(3.35f, 0f, 1.22f)) < 0.8f;
            if (hand.openPalm && hand.score >= 0.45f && nearSeal)
            {
                sealHoldStart = sealHoldStart < 0f ? Time.time : sealHoldStart;
                if (sealRenderer != null)
                {
                    sealRenderer.sharedMaterial = boxHeldMaterial;
                }
                if (Time.time - sealHoldStart >= 0.85f)
                {
                    sealActivated = true;
                    if (sealRenderer != null)
                    {
                        sealRenderer.sharedMaterial = tealGlow;
                    }
                    OpenGate(goalGate);
                    AdvanceLevel1(Level1Stage.RunToGoal);
                }
            }
            else
            {
                sealHoldStart = -1f;
                if (sealRenderer != null)
                {
                    sealRenderer.sharedMaterial = amberGlow;
                }
            }
        }

        private GameObject CreateGate(string name, float x)
        {
            var gateRoot = new GameObject(name);
            gateRoot.transform.SetParent(levelRoot, false);
            gateRoot.transform.SetPositionAndRotation(new Vector3(x, RoadY(x) + 0.66f, 0f), Quaternion.Euler(0f, 0f, Level1RoadAngleDegrees));
            var collider = gateRoot.AddComponent<BoxCollider>();
            collider.center = Vector3.zero;
            collider.size = new Vector3(0.18f, 1.12f, 1.82f);

            for (var i = 0; i < 6; i++)
            {
                var z = Mathf.Lerp(-0.68f, 0.68f, i / 5f);
                CreateBox($"{name} dungeon bar {i}", gateRoot.transform.TransformPoint(new Vector3(0f, 0.02f, z)), new Vector3(0.10f, 0.84f, 0.045f), level2WallMaterial, gateRoot.transform, gateRoot.transform.rotation, false);
            }
            CreateBox(name + " cyan lock core", gateRoot.transform.TransformPoint(new Vector3(0f, 0.08f, 0f)), new Vector3(0.13f, 0.22f, 0.22f), level2PortalCoreMaterial, gateRoot.transform, gateRoot.transform.rotation, false);
            CreateBox(name + " brass lintel", gateRoot.transform.TransformPoint(new Vector3(0f, 0.36f, 0f)), new Vector3(0.16f, 0.08f, 1.82f), level2TrimMaterial, gateRoot.transform, gateRoot.transform.rotation, false);
            CreateBox(name + " brass lower rail", gateRoot.transform.TransformPoint(new Vector3(0f, -0.28f, 0f)), new Vector3(0.16f, 0.08f, 1.82f), level2TrimMaterial, gateRoot.transform, gateRoot.transform.rotation, false);
            return gateRoot;
        }

        private void OpenGate(GameObject gate)
        {
            if (gate != null)
            {
                gate.SetActive(false);
            }
        }

        private void AdvanceLevel1(Level1Stage nextStage)
        {
            if (level1Stage == nextStage)
            {
                return;
            }

            level1Stage = nextStage;
            level1SuccessUntil = Time.time + 1.2f;
        }

        private string Level1ObjectiveText()
        {
            return level1Stage switch
            {
                Level1Stage.ClearBlock => "Move the block into the glowing side slot to release the ball.",
                Level1Stage.JoinBridge => "Pinch both bridge halves and pull your hands together to join the bridge.",
                Level1Stage.RotateGate => "Pinch both sides of the floating bridge and rotate it 90 degrees to connect the floors.",
                Level1Stage.ActivateSeal => "Open your palm over the glowing seal to unlock the final gate.",
                Level1Stage.RunToGoal => "The path is open. Guide the ball safely to the altar.",
                _ => "",
            };
        }

        private Vector3 MapPinchToRoad(float normalizedX, float normalizedY)
        {
            var x = Mathf.Lerp(-4.2f, 4.15f, Mathf.Clamp01(normalizedX));
            var z = Mathf.Lerp(1.55f, -1.55f, Mathf.Clamp01(normalizedY));
            x = Mathf.Clamp(x, -4.2f, 4.15f);
            z = Mathf.Clamp(z, -1.55f, 1.55f);
            return new Vector3(x, RoadY(x) + 0.37f, z);
        }

        private Vector3 MapPinchToLevel1DragPlane(float normalizedX, float normalizedY)
        {
            var point = ScreenToWorldPlane(normalizedX, normalizedY, RoadY(-2.85f) + 0.37f);
            point.x = Mathf.Clamp(point.x, -3.65f, -2.00f);
            point.z = Mathf.Clamp(point.z, -0.85f, 1.58f);
            point.y = RoadY(point.x) + 0.37f;
            return point;
        }

        private bool IsPinching(GestureHandFrame hand)
        {
            return hand.score >= 0.35f && hand.pinchDistance < pinchThreshold;
        }

        // -------------------- Level3: Creation & Erasure --------------------
        private void BuildLevel3()
        {
            levelRoot = new GameObject("Level03 Creation Erasure").transform;
            level3Stage = Level3Stage.CreateBridgeObject;
            level3CubePlaced = false;
            level3SpherePlaced = false;
            level3BridgePlaced = false;
            level3SlideBridgeReleased = false;
            level3EraseRequiresGestureReset = false;
            level3SlideBridgeReleaseStart = -1f;
            level3HintMessage = "Create one cube and one sphere, then place each on the matching platform.";
            level1SuccessUntil = -1f;
            tutorialDrawCreated = false;
            tutorialDrawErased = false;
            tutorialDrawInvalid = false;
            ResetTutorialDrawingState(true);
            tutorialDrawnObjects.Clear();
            tutorialDrawnRoot = new GameObject("level3 created objects").transform;
            tutorialDrawnRoot.SetParent(levelRoot, false);
            ConfigureLevel2CameraAndLights();

            var roadRotation = Level3RoadRotation();
            CreateBox("level3 void shadow plinth", new Vector3(0f, -0.72f, 0f), new Vector3(11.4f, 0.42f, 5.1f), darkStone, levelRoot, Quaternion.identity, false);
            CreateLevel3RoadBox("level3 start floor", -3.86f, 0f, new Vector3(2.25f, 0.22f, 1.78f), level2FloorMaterial, true);
            CreateLevel3RoadBox("level3 middle floor", -0.42f, 0f, new Vector3(2.05f, 0.22f, 1.78f), level2FloorMaterial, true);
            CreateLevel3RoadBox("level3 finish floor", 3.25f, 0f, new Vector3(3.15f, 0.22f, 1.78f), level2FloorMaterial, true);
            CreateLevel3RoadBox("level3 start centerline", -3.86f, 0f, new Vector3(1.75f, 0.018f, 0.055f), level2TrimMaterial, false, 0.13f);
            CreateLevel3RoadBox("level3 middle centerline", -0.42f, 0f, new Vector3(1.15f, 0.018f, 0.055f), level2TrimMaterial, false, 0.13f);
            CreateLevel3RoadBox("level3 finish centerline", 3.25f, 0f, new Vector3(2.45f, 0.018f, 0.055f), level2TrimMaterial, false, 0.13f);

            level3BridgePatch = CreateLevel3RoadBox("level3 creation bridge patch", -2.12f, 0f, new Vector3(1.92f, 0.16f, 1.62f), level2TrimMaterial, true, 0.02f);
            level3BridgePatchRenderer = level3BridgePatch.GetComponent<Renderer>();
            level3BridgePatch.SetActive(false);

            var cubePlate = CreateLevel3RoadBox("level3 cube platform", -2.12f, 1.30f, new Vector3(1.32f, 0.06f, 0.98f), level2RuneMaterial, false, 0.18f);
            level3CubePlate = cubePlate.transform;
            level3CubePlateRenderer = cubePlate.GetComponent<Renderer>();
            CreateBox("level3 cube platform icon", level3CubePlate.position + new Vector3(0f, 0.13f, 0f), new Vector3(0.24f, 0.24f, 0.24f), level2TrimMaterial, levelRoot, roadRotation, false);

            var spherePlate = CreateLevel3RoadBox("level3 sphere platform", -2.12f, -1.30f, new Vector3(1.32f, 0.06f, 0.98f), level2RuneMaterial, false, 0.18f);
            level3SpherePlate = spherePlate.transform;
            level3SpherePlateRenderer = spherePlate.GetComponent<Renderer>();
            var sphereIcon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphereIcon.name = "level3 sphere platform icon";
            sphereIcon.transform.SetParent(levelRoot, false);
            sphereIcon.transform.SetPositionAndRotation(level3SpherePlate.position + new Vector3(0f, 0.15f, 0f), roadRotation);
            sphereIcon.transform.localScale = Vector3.one * 0.28f;
            sphereIcon.GetComponent<Renderer>().sharedMaterial = level2TrimMaterial;
            DestroyUnityObject(sphereIcon.GetComponent<Collider>());

            startGate = CreateLevel2Gate("level3 creation gate", new Vector3(-2.82f, Level3RoadY(-2.82f) + 0.68f, 0f), 1.74f);
            bridgeGate = CreateLevel2Gate("level3 slide gate", new Vector3(0.78f, Level3RoadY(0.78f) + 0.68f, 0f), 1.74f);
            BuildLevel3SlideBridgeDevice(1.10f);
            BuildLevel3Decorations();

            var ballObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballObject.name = "Level3 Golden Physics Ball";
            ballObject.transform.SetParent(levelRoot, false);
            ballObject.transform.position = new Vector3(-4.56f, Level3RoadY(-4.56f) + 0.48f, 0f);
            ballObject.transform.localScale = Vector3.one * 0.46f;
            ballObject.GetComponent<Renderer>().sharedMaterial = ballMaterial;
            var body = ballObject.AddComponent<Rigidbody>();
            body.mass = 1.1f;
            body.linearDamping = 0.08f;
            body.angularDamping = 0.03f;
            levelBallBody = body;
            levelBallRenderer = ballObject.GetComponent<Renderer>();
            levelBall = ballObject.AddComponent<BallController>();

            var goal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            goal.name = "Level3 Goal Trigger - Sacred Altar";
            goal.transform.SetParent(levelRoot, false);
            goal.transform.SetPositionAndRotation(new Vector3(4.45f, Level3RoadY(4.45f) + 0.22f, 0f), roadRotation);
            goal.transform.localScale = new Vector3(0.7f, 0.11f, 0.7f);
            goal.GetComponent<Renderer>().sharedMaterial = level2PortalCoreMaterial;
            DestroyUnityObject(goal.GetComponent<Collider>());
            levelBall.Configure(goal.transform);
            CreateLevel2GoalArt(goal.transform.position);
            ApplyLevel3SceneTransformOverrides();
        }

        private void BuildLevel3SlideBridgeDevice(float x)
        {
            var bridgeY = Level3SlideBridgeCenterY(Level3SlideBridgeCenterX);
            var bridgeScale = new Vector3(Level3SlideBridgeWidth, Level3SlidePlatformThickness, 1.78f);
            CreateBox("level3 sliding bridge parked slot", new Vector3(1.111f, -0.205f, 1.85f), new Vector3(1.26f, 0.035f, 3f), level2WallMaterial, levelRoot, Level3RoadRotation(), false);
            CreateBox("level3 sliding bridge left rail", new Vector3(0.541f, -0.135f, 1.863f), new Vector3(0.08f, 0.10f, 3f), level2TrimMaterial, levelRoot, Level3RoadRotation(), false);
            CreateBox("level3 sliding bridge right rail", new Vector3(1.741f, -0.195f, 1.866f), new Vector3(0.08f, 0.10f, 3f), level2TrimMaterial, levelRoot, Level3RoadRotation(), false);

            var platform = CreateBox("level3 side parked sliding bridge", new Vector3(Level3SlideBridgeCenterX, bridgeY, Level3SlideBridgeParkZ), bridgeScale, level2TrimMaterial, levelRoot, Level3RoadRotation(), true);
            level3SlideBridge = platform.transform;
            level3SlideBridgeRenderer = platform.GetComponent<Renderer>();

            var blockScale = new Vector3(0.96f, 0.72f, 0.5f);
            level3LockBlock = CreateBox("level3 sliding bridge lock block", new Vector3(1.13f, 0.17f, 1.18f), blockScale, boxIdle, levelRoot, Level3RoadRotation(), true);
            level3LockBlockRenderer = level3LockBlock.GetComponent<Renderer>();
            level3LockBlockHalo = null;
            tutorialDrawnObjects.Add(level3LockBlock);
        }

        private void BuildLevel3Decorations()
        {
            var roadRotation = Level3RoadRotation();
            CreateLevel3Railing("level3 start railing", -3.86f, 2.05f);
            CreateLevel3Railing("level3 middle railing", -0.42f, 1.72f);
            CreateLevel3Railing("level3 finish railing", 3.25f, 2.85f);

            var slideChevronZ = new[] { 2.22f, 1.82f, 1.42f, 1.02f, 0.62f };
            for (var i = 0; i < slideChevronZ.Length; i++)
            {
                var z = slideChevronZ[i];
                var y = Level3SlideBridgeCenterY(Level3SlideBridgeCenterX) + 0.12f;
                CreateBox($"level3 slide path chevron upper {i}", new Vector3(Level3SlideBridgeCenterX - 0.16f, y, z), new Vector3(0.36f, 0.018f, 0.045f), level2PortalCoreMaterial, levelRoot, roadRotation * Quaternion.Euler(0f, 28f, 0f), false);
                CreateBox($"level3 slide path chevron lower {i}", new Vector3(Level3SlideBridgeCenterX + 0.16f, y, z), new Vector3(0.36f, 0.018f, 0.045f), level2PortalCoreMaterial, levelRoot, roadRotation * Quaternion.Euler(0f, -28f, 0f), false);
            }

            CreateLevel3RuneCornerMarkers("level3 cube platform rune", level3CubePlate.position, 0.56f, 0.40f);
            CreateLevel3RuneCornerMarkers("level3 sphere platform rune", level3SpherePlate.position, 0.56f, 0.40f);
        }

        private void ApplyLevel3SceneTransformOverrides()
        {
            if (levelRoot == null)
            {
                return;
            }

            var transforms = levelRoot.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < Level3SceneTransformOverrides.Length; i++)
            {
                var item = Level3SceneTransformOverrides[i];
                for (var j = 0; j < transforms.Length; j++)
                {
                    var target = transforms[j];
                    if (target.name != item.Name)
                    {
                        continue;
                    }

                    target.localPosition = item.LocalPosition;
                    target.localRotation = item.LocalRotation;
                    target.localScale = item.LocalScale;
                    break;
                }
            }
        }

        private void CreateLevel3Railing(string prefix, float centerX, float length)
        {
            var sides = new[] { -1.03f, 1.03f };
            for (var side = 0; side < sides.Length; side++)
            {
                var z = sides[side];
                CreateLevel3RoadBox($"{prefix} side {side} upper rail", centerX, z, new Vector3(length, 0.055f, 0.06f), level2TrimMaterial, false, 0.43f);
                CreateLevel3RoadBox($"{prefix} side {side} lower rail", centerX, z, new Vector3(length, 0.045f, 0.05f), level2WallMaterial, false, 0.30f);
                for (var i = 0; i < 4; i++)
                {
                    var t = i / 3f;
                    var x = centerX - length * 0.5f + length * t;
                    CreateLevel3RoadBox($"{prefix} side {side} support {i}", x, z, new Vector3(0.055f, 0.28f, 0.055f), level2TrimMaterial, false, 0.31f);
                }
            }
        }

        private void CreateLevel3RuneCornerMarkers(string prefix, Vector3 center, float halfX, float halfZ)
        {
            var offsets = new[]
            {
                new Vector3(-halfX, 0.08f, -halfZ),
                new Vector3(-halfX, 0.08f, halfZ),
                new Vector3(halfX, 0.08f, -halfZ),
                new Vector3(halfX, 0.08f, halfZ),
            };
            for (var i = 0; i < offsets.Length; i++)
            {
                CreateBox($"{prefix} corner glow {i}", center + offsets[i], new Vector3(0.13f, 0.028f, 0.13f), tealGlow, levelRoot, Level3RoadRotation(), false);
            }
        }

        private GameObject CreateLevel3RoadBox(string name, float x, float z, Vector3 scale, Material material, bool keepCollider, float yOffset = 0f)
        {
            return CreateBox(name, new Vector3(x, Level3RoadY(x) + yOffset, z), scale, material, levelRoot, Level3RoadRotation(), keepCollider);
        }

        private static float Level3RoadY(float x)
        {
            return Level3RoadBaseY + (x + 4.5f) * Level3RoadSlope;
        }

        private static Quaternion Level3RoadRotation()
        {
            return Quaternion.Euler(0f, 0f, Level3RoadAngleDegrees);
        }

        private static float Level3SlideBridgeCenterY(float x)
        {
            return Level3RoadY(x) + (Level3RoadThickness - Level3SlidePlatformThickness) * 0.5f * Mathf.Cos(Level3RoadAngleDegrees * Mathf.Deg2Rad);
        }

        private void UpdateLevel3(GestureHandFrame hand)
        {
            if (levelBall == null)
            {
                return;
            }

            var frame = receiver != null && receiver.HasFreshFrame ? receiver.Latest : GestureFrame.Neutral;
            var target = ScreenToWorldPlane(hand.pinchX, hand.pinchY, 0.7f);
            var isPinch = IsPinching(hand);
            var twoHandsPinching = TryGetTwoPinchingHands(out var a, out var b);
            var level3EraseGesture = IsIndexCrossEraseGesture(frame, out _);

            if (!level3EraseGesture)
            {
                level3EraseRequiresGestureReset = false;
                ClearTutorialEraseSelection(true);
            }
            if (!TutorialDrawInputActive() && CanLevel3EraseDrawnObject() && !level3EraseRequiresGestureReset && level3EraseGesture)
            {
                UpdateTutorialDrawErase(frame);
                if (tutorialEraseTarget != null || level3EraseRequiresGestureReset)
                {
                    return;
                }
            }

            if (CanDrawCreateObject())
            {
                UpdateTutorialDrawing(frame);
            }
            if (TutorialDrawInputActive())
            {
                return;
            }
            if (CanManipulateDrawnObjects())
            {
                UpdateTutorialDrawnObjectManipulation(hand, target, isPinch, twoHandsPinching, a, b);
            }

            UpdateLevel3BridgePlacement();
        }

        private void UpdateLevel3BridgePlacement()
        {
            if (level3BridgePlaced || level3CubePlate == null || level3SpherePlate == null)
            {
                return;
            }

            level3CubePlaced = false;
            level3SpherePlaced = false;
            for (var i = tutorialDrawnObjects.Count - 1; i >= 0; i--)
            {
                var obj = tutorialDrawnObjects[i];
                if (obj == null)
                {
                    tutorialDrawnObjects.RemoveAt(i);
                    continue;
                }
                if (obj == level3LockBlock)
                {
                    continue;
                }

                var marker = obj.GetComponent<TutorialDrawnShapeMarker>();
                if (marker == null)
                {
                    continue;
                }

                if (marker.Shape == TutorialDrawShape.Rectangle && DistanceXZ(obj.transform.position, level3CubePlate.position) < 0.86f)
                {
                    level3CubePlaced = true;
                }
                if (marker.Shape == TutorialDrawShape.Circle && DistanceXZ(obj.transform.position, level3SpherePlate.position) < 0.86f)
                {
                    level3SpherePlaced = true;
                }
            }

            if (level3CubePlateRenderer != null)
            {
                level3CubePlateRenderer.sharedMaterial = level3CubePlaced ? tealGlow : level2RuneMaterial;
            }
            if (level3SpherePlateRenderer != null)
            {
                level3SpherePlateRenderer.sharedMaterial = level3SpherePlaced ? tealGlow : level2RuneMaterial;
            }

            if (level3CubePlaced && level3SpherePlaced)
            {
                level3BridgePlaced = true;
                if (level3BridgePatch != null)
                {
                    level3BridgePatch.SetActive(true);
                }
                if (level3BridgePatchRenderer != null)
                {
                    level3BridgePatchRenderer.sharedMaterial = tealGlow;
                }
                OpenGate(startGate);
                AdvanceLevel3(Level3Stage.EraseLockBlock);
                level3HintMessage = "Bridge is open. Erase the lock block to slide the side bridge into the gap.";
            }
        }

        private void TriggerLevel3SlideBridgeRelease()
        {
            if (level3SlideBridgeReleased)
            {
                return;
            }

            level3SlideBridgeReleased = true;
            level3LockBlock = null;
            level3LockBlockRenderer = null;
            if (level3LockBlockHalo != null)
            {
                DestroyUnityObject(level3LockBlockHalo);
                level3LockBlockHalo = null;
            }
            level3SlideBridgeReleaseStart = Time.time;
            if (level3SlideBridgeRenderer != null)
            {
                level3SlideBridgeRenderer.sharedMaterial = tealGlow;
            }
            OpenGate(bridgeGate);
            level3HintMessage = "The side bridge is sliding into the road gap.";
        }

        private void UpdateLevel3SlideBridgeAnimation()
        {
            if (!level3SlideBridgeReleased || level3SlideBridgeReleaseStart < 0f)
            {
                return;
            }

            var t = Mathf.Clamp01((Time.time - level3SlideBridgeReleaseStart) / 0.85f);
            var smooth = Mathf.SmoothStep(0f, 1f, t);
            if (level3SlideBridge != null)
            {
                var y = Level3SlideBridgeCenterY(level3SlideBridge.position.x);
                var z = Mathf.Lerp(Level3SlideBridgeParkZ, Level3SlideBridgeFinalZ, smooth);
                level3SlideBridge.position = new Vector3(level3SlideBridge.position.x, y, z);
            }
            if (t >= 1f && level3Stage != Level3Stage.RunToGoal)
            {
                AdvanceLevel3(Level3Stage.RunToGoal);
                level3HintMessage = "The sliding bridge is locked in place. The ball can roll to the altar.";
            }
        }

        private void AdvanceLevel3(Level3Stage nextStage)
        {
            if (level3Stage == nextStage)
            {
                return;
            }

            level3Stage = nextStage;
            level1SuccessUntil = Time.time + 1.2f;
        }

        private string Level3ObjectiveText()
        {
            return level3Stage switch
            {
                Level3Stage.CreateBridgeObject => "Create a cube and a sphere. Put each one on the matching marked platform.",
                Level3Stage.PlaceCreatedObject => "Move or rotate the cube to the cube mark and the sphere to the sphere mark.",
                Level3Stage.EraseLockBlock => "Cross both index fingers over the lock block and hold for 2 seconds.",
                Level3Stage.RunToGoal => "The sliding bridge completed the road. Let the ball roll to the altar.",
                _ => "",
            };
        }

        // -------------------- Level2: Portals & Airflow --------------------
        private void BuildLevel2()
        {
            levelRoot = new GameObject("Level02 Portal Airflow").transform;
            Debug.Log("[Level2] BuildLevel2 start");
            level1Stage = Level1Stage.ClearBlock; // ClearBlock=Place key, JoinBridge=Airflow, RotateGate=Finish
            portalAActive = false;
            portalBActive = false;
            level1SuccessUntil = -1f;
            level2Teleporting = false;
            pendingAirDirection = 0;
            pendingAirDirectionStart = -1f;
            level2HintMessage = "";
            ConfigureLevel2CameraAndLights();
            
            // Portal A: ball spawn point (left side)
            // Portal B: after gate (right side, where ball teleports to)
            portalAPosition = new Vector3(-3.5f, 0.15f, 0f);
            portalBPosition = new Vector3(0.5f, 0.15f, 0f);

            BuildLevel2DungeonArt();
            var floorCollider = CreateBox("level2 gameplay floor collider", new Vector3(0.55f, 0.02f, 0f), new Vector3(9.9f, 0.045f, 2.25f), level2FloorMaterial, levelRoot, Quaternion.identity, true);
            var floorRenderer = floorCollider.GetComponent<Renderer>();
            if (floorRenderer != null) floorRenderer.enabled = false;

            // single rune for key placement (activates teleport)
            var rune = CreateBox("rune", new Vector3(-2.0f, 0.08f, 1.22f), new Vector3(0.92f, 0.08f, 0.92f), level2RuneMaterial, levelRoot, Quaternion.identity, true);
            runeLeft = rune.transform;
            var runeColliderRenderer = rune.GetComponent<Renderer>();
            if (runeColliderRenderer != null) runeColliderRenderer.enabled = false;
            runeLeftRenderer = CreateLevel2RuneArt(rune.transform.position, amberGlow);
            runeRight = null;
            runeRightRenderer = null;
            runeLeftArrow = null;
            runeRightArrow = null;

            // draggable geometric key
            var key = new GameObject("Portal Key");
            key.name = "Portal Key";
            key.transform.SetParent(levelRoot, false);
            key.transform.position = new Vector3(-3.85f, 0.32f, 1.12f);
            key.transform.rotation = Quaternion.Euler(0f, 24f, 0f);
            var keyCollider = key.AddComponent<BoxCollider>();
            keyCollider.center = new Vector3(0.18f, 0.06f, 0f);
            keyCollider.size = new Vector3(1.08f, 0.35f, 0.42f);
            CreatePortalKeyVisual(key.transform);
            portalKeyRenderers = key.GetComponentsInChildren<Renderer>();
            portalKeyIdleMaterials = new Material[portalKeyRenderers.Length];
            for (var i = 0; i < portalKeyRenderers.Length; i++)
            {
                portalKeyIdleMaterials[i] = portalKeyRenderers[i].sharedMaterial;
            }
            portalKeyRenderer = portalKeyRenderers.Length > 0 ? portalKeyRenderers[0] : null;
            RestorePortalKeyMaterial();
            portalKeyBody = key.AddComponent<Rigidbody>();
            portalKeyBody.isKinematic = true;
            portalKeyBody.interpolation = RigidbodyInterpolation.Interpolate;
            portalKeyBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            portalKey = key;

            // portals visual markers
            portalARenderer = CreateLevel2Portal("portal A", portalAPosition, tealGlow, out portalAParticles);
            portalBRenderer = CreateLevel2Portal("portal B", portalBPosition, amberGlow, out portalBParticles);

            // single air belt (default direction: RIGHT, pushes ball from portal B toward goal)
            airBelts = new Transform[1];
            airBeltDirection = new int[1];
            airBeltRenderers = new Renderer[1];
            airBeltTriggers = new AirBeltTrigger[1];
            airBeltArrowRenderers = new Renderer[1];
            airBeltArrowTransforms = new Transform[1];
            airBeltStreaks = new Transform[6];
            airBeltStreakRenderers = new Renderer[6];
            airBeltMistQuads = new Transform[4];
            airBeltMistRenderers = new Renderer[4];
            
            var beltX = 1.25f; // Matches the full central wind gallery floor.
            var beltY = 0.18f;
            // Use keepCollider=true to preserve the collider, then set it as trigger
            var belt = CreateBox("air belt trigger", new Vector3(beltX, beltY, 0f), new Vector3(4.95f, 0.36f, 1.65f), level2WindMaterial, levelRoot, Quaternion.identity, true);
            airBelts[0] = belt.transform;
            airBeltRenderers[0] = belt.GetComponent<Renderer>();
            airBeltDirection[0] = 0; // Default: OFF (no wind)

            var col = belt.GetComponent<Collider>();
            col.isTrigger = true;

            var trigger = belt.AddComponent<AirBeltTrigger>();
            trigger.beltIndex = 0;
            trigger.direction = 0; // Default: OFF
            trigger.force = AirBeltForce;
            trigger.maxWindSpeed = 1.95f;
            trigger.rampSeconds = 1.45f;
            airBeltTriggers[0] = trigger;

            var beltVisualRoot = new GameObject("air belt visual root");
            beltVisualRoot.transform.SetParent(levelRoot, false);
            beltVisualRoot.transform.position = belt.transform.position;

            var arrow = CreateBox("air arrow glow", beltVisualRoot.transform.position + new Vector3(0f, 0.21f, -0.70f), new Vector3(0.34f, 0.018f, 0.055f), level2PortalCoreMaterial, beltVisualRoot.transform, Quaternion.identity, false);
            airBeltArrowRenderers[0] = arrow.GetComponent<Renderer>();
            airBeltArrowTransforms[0] = arrow.transform;

            for (var i = 0; i < airBeltMistQuads.Length; i++)
            {
                var xOffset = Mathf.Lerp(-1.68f, 1.68f, i / (float)(airBeltMistQuads.Length - 1));
                var zOffset = i % 2 == 0 ? -0.28f : 0.28f;
                var mist = CreateAirflowMist($"air flow mist sheet {i + 1}", beltVisualRoot.transform, new Vector3(xOffset, 0.285f, zOffset), 2.0f, 0.68f);
                airBeltMistQuads[i] = mist.transform;
                airBeltMistRenderers[i] = mist.GetComponent<Renderer>();
            }

            for (var i = 0; i < airBeltStreaks.Length; i++)
            {
                var xOffset = Mathf.Lerp(-2.05f, 2.05f, i / (float)(airBeltStreaks.Length - 1));
                var zOffset = Mathf.Lerp(-0.58f, 0.58f, i / (float)(airBeltStreaks.Length - 1));
                var streak = CreateAirflowRibbon($"air flow texture ribbon {i + 1}", beltVisualRoot.transform, new Vector3(xOffset, 0.33f, zOffset), 1.42f, 0.30f);
                airBeltStreaks[i] = streak.transform;
                airBeltStreakRenderers[i] = streak.GetComponent<Renderer>();
            }
            airBeltParticles = new[]
            {
                CreateAirflowParticles("airflow cyan mist", beltVisualRoot.transform),
            };

            if (airBeltRenderers[0] != null)
            {
                airBeltRenderers[0].enabled = false;
            }
            if (airBeltArrowRenderers[0] != null)
            {
                airBeltArrowRenderers[0].sharedMaterial = boxIdle;
            }
            UpdateAirBeltVisuals();

            // single gate between portal A and portal B
            startGate = null;
            bridgeGate = CreateLevel2Gate("level2 rune gate", new Vector3(-1.5f, 0.68f, 0f), 2.55f);
            // gate between air belt and goal (opens when airflow direction is set to RIGHT)
            rotateGateStop = CreateLevel2Gate("level2 wind gate", new Vector3(3.75f, 0.68f, 0f), 2.55f);
            goalGate = null;

            // ball spawns at portal A
            var ballObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballObject.name = "Golden Physics Ball";
            ballObject.transform.SetParent(levelRoot, false);
            ballObject.transform.position = portalAPosition + new Vector3(0f, 0.2f, 0f);
            ballObject.transform.localScale = Vector3.one * 0.46f;
            levelBallBaseScale = ballObject.transform.localScale;
            levelBallRenderer = ballObject.GetComponent<Renderer>();
            levelBallRuntimeMaterial = new Material(ballMaterial) { name = "Golden physics ball runtime" };
            ConfigureTransparentMaterial(levelBallRuntimeMaterial);
            levelBallRenderer.sharedMaterial = levelBallRuntimeMaterial;
            var body = ballObject.AddComponent<Rigidbody>();
            body.mass = 1.1f;
            body.linearDamping = 0.02f;
            body.angularDamping = 0.01f;
            levelBall = ballObject.AddComponent<BallController>();
            levelBallBody = body;

            // goal at right side
            var goal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            goal.name = "Goal Trigger";
            goal.transform.SetParent(levelRoot, false);
            goal.transform.position = new Vector3(4.85f, 0.2f, 0f);
            goal.transform.localScale = new Vector3(0.7f, 0.11f, 0.7f);
            goal.GetComponent<Renderer>().sharedMaterial = level2PortalCoreMaterial;
            DestroyUnityObject(goal.GetComponent<Collider>());
            levelBall.Configure(goal.transform);
            CreateLevel2GoalArt(goal.transform.position);
        }

        private void ConfigureLevel2CameraAndLights()
        {
            if (mainCamera != null)
            {
                mainCamera.orthographicSize = 3.9f;
                mainCamera.transform.SetPositionAndRotation(new Vector3(0.45f, 7.1f, -5.8f), Quaternion.Euler(55f, 0f, 0f));
                mainCamera.backgroundColor = new Color(0.010f, 0.012f, 0.015f);
            }

            var accent = new GameObject("Level2 Portal Accent Light");
            accent.transform.SetParent(levelRoot, false);
            accent.transform.position = new Vector3(0.6f, 2.8f, -1.4f);
            var light = accent.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.23f, 0.95f, 1f);
            light.intensity = 1.05f;
            light.range = 5.2f;
        }

        private void BuildLevel2DungeonArt()
        {
            CreateBox("level2 void plinth", new Vector3(0.45f, -0.50f, 0f), new Vector3(11.2f, 0.42f, 5.0f), darkStone, levelRoot, Quaternion.identity, false);

            CreateBox("level2 left portal chamber floor", new Vector3(-3.0f, -0.015f, 0f), new Vector3(2.55f, 0.13f, 2.48f), level2FloorMaterial, levelRoot, Quaternion.identity, false);
            CreateBox("level2 central wind gallery floor", new Vector3(1.25f, -0.015f, 0f), new Vector3(4.95f, 0.13f, 1.72f), level2FloorMaterial, levelRoot, Quaternion.identity, false);
            CreateBox("level2 right altar chamber floor", new Vector3(4.55f, -0.015f, 0f), new Vector3(1.65f, 0.13f, 2.15f), level2FloorMaterial, levelRoot, Quaternion.identity, false);

            CreateCleanChamberFrame(-3.0f, 2.55f, 2.48f);
            CreateCleanChamberFrame(1.25f, 4.95f, 1.72f);
            CreateCleanChamberFrame(4.55f, 1.65f, 2.15f);

            CreateBox("level2 wind channel low glow", new Vector3(1.25f, 0.055f, 0f), new Vector3(4.65f, 0.006f, 0.045f), level2WallMaterial, levelRoot, Quaternion.identity, false);
            CreateLevel2WindFloorHints();
        }

        private void CreateLevel2WindFloorHints()
        {
            CreateLevel2WindGrille("level2 wind intake grille", new Vector3(-0.95f, 0.118f, 0f));
            CreateLevel2WindGrille("level2 wind output grille", new Vector3(3.38f, 0.118f, 0f));
            CreateLevel2WindChevron(new Vector3(0.12f, 0.142f, 0f), 0.50f);
            CreateLevel2WindChevron(new Vector3(1.25f, 0.142f, 0f), 0.50f);
            CreateLevel2WindChevron(new Vector3(2.38f, 0.142f, 0f), 0.50f);
        }

        private void CreateLevel2WindGrille(string name, Vector3 position)
        {
            CreateBox(name + " base", position, new Vector3(0.52f, 0.018f, 0.84f), darkStone, levelRoot, Quaternion.identity, false);
            for (var i = 0; i < 5; i++)
            {
                var z = Mathf.Lerp(-0.32f, 0.32f, i / 4f);
                CreateBox($"{name} cyan slit {i}", position + new Vector3(0f, 0.022f, z), new Vector3(0.42f, 0.012f, 0.030f), level2PortalCoreMaterial, levelRoot, Quaternion.identity, false);
            }
        }

        private void CreateLevel2WindChevron(Vector3 position, float length)
        {
            CreateBox("level2 floor wind chevron upper", position + new Vector3(0f, 0f, 0.11f), new Vector3(length, 0.014f, 0.045f), level2PortalCoreMaterial, levelRoot, Quaternion.Euler(0f, 28f, 0f), false);
            CreateBox("level2 floor wind chevron lower", position + new Vector3(0f, 0f, -0.11f), new Vector3(length, 0.014f, 0.045f), level2PortalCoreMaterial, levelRoot, Quaternion.Euler(0f, -28f, 0f), false);
        }

        private void CreateCleanChamberFrame(float centerX, float width, float depth)
        {
            var frontZ = -depth * 0.5f;
            var rearZ = depth * 0.5f;
            CreateBox($"level2 chamber front trim {centerX:0.0}", new Vector3(centerX, 0.105f, frontZ), new Vector3(width, 0.055f, 0.055f), level2TrimMaterial, levelRoot, Quaternion.identity, false);
            CreateBox($"level2 chamber rear trim {centerX:0.0}", new Vector3(centerX, 0.105f, rearZ), new Vector3(width, 0.055f, 0.055f), level2TrimMaterial, levelRoot, Quaternion.identity, false);
            CreateBox($"level2 chamber left trim {centerX:0.0}", new Vector3(centerX - width * 0.5f, 0.105f, 0f), new Vector3(0.055f, 0.055f, depth), level2TrimMaterial, levelRoot, Quaternion.identity, false);
            CreateBox($"level2 chamber right trim {centerX:0.0}", new Vector3(centerX + width * 0.5f, 0.105f, 0f), new Vector3(0.055f, 0.055f, depth), level2TrimMaterial, levelRoot, Quaternion.identity, false);
        }

        private GameObject InstantiateDungeonModel(string resourceName, Vector3 position, Vector3 scale, Quaternion rotation, Material overrideMaterial)
        {
            var prefab = Resources.Load<GameObject>($"KenneyDungeon/{resourceName}");
            if (prefab == null)
            {
                Debug.LogWarning($"Missing Kenney dungeon model in Resources: {resourceName}");
                return null;
            }

            var instance = Instantiate(prefab, position, rotation, levelRoot);
            instance.name = $"Kenney {resourceName}";
            instance.transform.localScale = scale;
            DestroyImportedColliders(instance.transform);
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = true;
                if (overrideMaterial != null)
                {
                    renderer.sharedMaterial = overrideMaterial;
                }
            }
            return instance;
        }

        private static void DestroyImportedColliders(Transform root)
        {
            foreach (var collider in root.GetComponentsInChildren<Collider>())
            {
                DestroyUnityObject(collider);
            }
        }

        private void CreateLevel2Pillar(Vector3 position)
        {
            var baseObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            baseObject.name = "level2 carved pillar";
            baseObject.transform.SetParent(levelRoot, false);
            baseObject.transform.position = position;
            baseObject.transform.localScale = new Vector3(0.18f, 0.34f, 0.18f);
            baseObject.GetComponent<Renderer>().sharedMaterial = level2WallMaterial;
            DestroyUnityObject(baseObject.GetComponent<Collider>());

            var cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cap.name = "level2 pillar amber cap";
            cap.transform.SetParent(levelRoot, false);
            cap.transform.position = position + new Vector3(0f, 0.43f, 0f);
            cap.transform.localScale = Vector3.one * 0.20f;
            cap.GetComponent<Renderer>().sharedMaterial = amberGlow;
            DestroyUnityObject(cap.GetComponent<Collider>());
        }

        private Renderer CreateLevel2Portal(string name, Vector3 position, Material idleMaterial, out ParticleSystem particles)
        {
            var root = new GameObject(name);
            root.transform.SetParent(levelRoot, false);
            root.transform.position = position;

            var core = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            core.name = name + " core";
            core.transform.SetParent(root.transform, false);
            core.transform.localPosition = Vector3.zero;
            core.transform.localScale = new Vector3(0.54f, 0.035f, 0.54f);
            core.GetComponent<Renderer>().sharedMaterial = idleMaterial;
            DestroyUnityObject(core.GetComponent<Collider>());

            var ring = CreateTorus(name + " energy ring", position + new Vector3(0f, 0.11f, 0f), 0.55f, 0.045f, level2PortalCoreMaterial, root.transform);
            ring.transform.localPosition = new Vector3(0f, 0.11f, 0f);
            ring.transform.localRotation = Quaternion.identity;

            var inner = CreateTorus(name + " inner ripple", position + new Vector3(0f, 0.16f, 0f), 0.34f, 0.026f, idleMaterial, root.transform);
            inner.transform.localPosition = new Vector3(0f, 0.16f, 0f);
            inner.transform.localRotation = Quaternion.identity;

            var swirl = GameObject.CreatePrimitive(PrimitiveType.Quad);
            swirl.name = name + " Kenney particle swirl";
            swirl.transform.SetParent(root.transform, false);
            swirl.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            swirl.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            swirl.transform.localScale = Vector3.one * 0.92f;
            swirl.GetComponent<Renderer>().sharedMaterial = level2PortalTwirlMaterial != null ? level2PortalTwirlMaterial : level2WindMaterial;
            DestroyUnityObject(swirl.GetComponent<Collider>());

            particles = null;
            return core.GetComponent<Renderer>();
        }

        private Renderer CreateLevel2RuneArt(Vector3 position, Material material)
        {
            var root = new GameObject("level2 key socket art");
            root.transform.SetParent(levelRoot, false);
            root.transform.position = position;

            var baseBlock = CreateBox("level2 key socket stone block", position + new Vector3(0f, 0.065f, 0f), new Vector3(0.92f, 0.18f, 0.66f), level2WallMaterial, root.transform, Quaternion.identity, false);
            baseBlock.transform.localPosition = new Vector3(0f, 0.065f, 0f);

            var topPlate = CreateBox("level2 key socket brass plate", position + new Vector3(0f, 0.168f, 0f), new Vector3(0.82f, 0.026f, 0.48f), level2TrimMaterial, root.transform, Quaternion.identity, false);
            topPlate.transform.localPosition = new Vector3(0f, 0.168f, 0f);

            var slotRotation = Quaternion.Euler(0f, 24f, 0f);
            var slot = CreateBox("level2 key socket dark slot", position + new Vector3(0.12f, 0.188f, 0f), new Vector3(0.76f, 0.024f, 0.105f), darkStone, root.transform, slotRotation, false);
            slot.transform.localPosition = new Vector3(0.12f, 0.188f, 0f);

            var leftRail = CreateBox("level2 key socket left brass rail", position + new Vector3(0.12f, 0.215f, 0.095f), new Vector3(0.72f, 0.045f, 0.035f), level2TrimMaterial, root.transform, slotRotation, false);
            leftRail.transform.localPosition = new Vector3(0.12f, 0.215f, 0.095f);
            var rightRail = CreateBox("level2 key socket right brass rail", position + new Vector3(0.12f, 0.215f, -0.095f), new Vector3(0.72f, 0.045f, 0.035f), level2TrimMaterial, root.transform, slotRotation, false);
            rightRail.transform.localPosition = new Vector3(0.12f, 0.215f, -0.095f);

            var lockCore = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            lockCore.name = "level2 key socket glow core";
            lockCore.transform.SetParent(root.transform, false);
            lockCore.transform.localPosition = new Vector3(-0.26f, 0.225f, 0f);
            lockCore.transform.localScale = new Vector3(0.16f, 0.018f, 0.16f);
            var renderer = lockCore.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            DestroyUnityObject(lockCore.GetComponent<Collider>());
            return renderer;
        }

        private ParticleSystem CreatePortalParticles(string name, Transform parent, Color color)
        {
            var particleObject = new GameObject(name);
            particleObject.transform.SetParent(parent, false);
            particleObject.transform.localPosition = new Vector3(0f, 0.16f, 0f);
            var ps = particleObject.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.42f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.12f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(color.r, color.g, color.b, 0.72f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 42f;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.52f;
            var renderer = particleObject.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = level2WindMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            return ps;
        }

        private GameObject CreateAirflowRibbon(string name, Transform parent, Vector3 localPosition, float length, float width)
        {
            var ribbon = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ribbon.name = name;
            ribbon.transform.SetParent(parent, false);
            ribbon.transform.localPosition = localPosition;
            ribbon.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ribbon.transform.localScale = new Vector3(length, width, 1f);
            var renderer = ribbon.GetComponent<Renderer>();
            renderer.sharedMaterial = level2WindRibbonMaterial != null ? level2WindRibbonMaterial : level2WindMaterial;
            DestroyUnityObject(ribbon.GetComponent<Collider>());
            return ribbon;
        }

        private GameObject CreateAirflowMist(string name, Transform parent, Vector3 localPosition, float length, float width)
        {
            var mist = GameObject.CreatePrimitive(PrimitiveType.Quad);
            mist.name = name;
            mist.transform.SetParent(parent, false);
            mist.transform.localPosition = localPosition;
            mist.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            mist.transform.localScale = new Vector3(length, width, 1f);
            mist.GetComponent<Renderer>().sharedMaterial = level2WindMistMaterial != null ? level2WindMistMaterial : level2WindMaterial;
            DestroyUnityObject(mist.GetComponent<Collider>());
            return mist;
        }

        private ParticleSystem CreateAirflowParticles(string name, Transform parent)
        {
            var particleObject = new GameObject(name);
            particleObject.transform.SetParent(parent, false);
            particleObject.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            var ps = particleObject.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.85f, 1.35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.16f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.14f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.42f, 0.96f, 1f, 0.52f));
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = ps.emission;
            emission.rateOverTime = 16f;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(2.0f, 0.12f, 1.25f);
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(0.2f);
            var renderer = particleObject.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = level2WindMistMaterial != null ? level2WindMistMaterial : level2WindMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            return ps;
        }

        private GameObject CreateLevel2Gate(string name, Vector3 position, float width)
        {
            var gateRoot = new GameObject(name);
            gateRoot.transform.SetParent(levelRoot, false);
            gateRoot.transform.position = position;
            var collider = gateRoot.AddComponent<BoxCollider>();
            collider.center = Vector3.zero;
            collider.size = new Vector3(0.18f, 1.18f, width);

            for (var i = 0; i < 7; i++)
            {
                var z = Mathf.Lerp(-width * 0.40f, width * 0.40f, i / 6f);
                CreateBox($"{name} metal bar {i}", position + new Vector3(0f, 0.02f, z), new Vector3(0.10f, 0.86f, 0.045f), level2WallMaterial, gateRoot.transform, Quaternion.identity, false);
            }
            CreateBox(name + " cyan lock core", position + new Vector3(0f, 0.08f, 0f), new Vector3(0.13f, 0.22f, 0.22f), level2PortalCoreMaterial, gateRoot.transform, Quaternion.identity, false);
            CreateBox(name + " glow lintel", position + new Vector3(0f, 0.36f, 0f), new Vector3(0.16f, 0.08f, width), level2TrimMaterial, gateRoot.transform, Quaternion.identity, false);
            CreateBox(name + " lower rail", position + new Vector3(0f, -0.28f, 0f), new Vector3(0.16f, 0.08f, width), level2TrimMaterial, gateRoot.transform, Quaternion.identity, false);
            return gateRoot;
        }

        private void CreateLevel2GoalArt(Vector3 position)
        {
            InstantiateDungeonModel("template-detail", position + new Vector3(0f, -0.13f, 0f), new Vector3(0.88f, 0.3f, 0.88f), Quaternion.identity, level2TrimMaterial);
            CreateTorus("level2 goal outer halo", position + new Vector3(0f, 0.13f, 0f), 0.66f, 0.055f, level2PortalCoreMaterial, levelRoot);
            CreateTorus("level2 goal inner halo", position + new Vector3(0f, 0.23f, 0f), 0.40f, 0.030f, tealGlow, levelRoot);
        }

        private void UpdateLevel2(GestureHandFrame hand)
        {
            if (portalKey == null || levelBall == null)
            {
                return;
            }

            var frame = receiver != null && receiver.HasFreshFrame ? receiver.Latest : GestureFrame.Neutral;
            var target = MapPinchToRoad(hand.pinchX, hand.pinchY);
            var isPinch = IsPinching(hand);
            var canOperateKey = level1Stage == Level1Stage.ClearBlock && !portalAActive && !level2Teleporting;

            RestorePortalKeyMaterial();
            if (!canOperateKey)
            {
                keyHeld = false;
                keyHoverStart = -1f;
                lastPinchState = false;
                lastKeyInRange = false;
            }

            if (canOperateKey)
            {
                var grabRadius = isPinch ? PortalKeyGrabPinchRadius : PortalKeyGrabIdleRadius;
                var close = DistanceXZ(target, portalKey.transform.position) < grabRadius;
                lastKeyInRange = close;
                lastPinchState = isPinch;
                level2HintMessage = "";

                if (!keyHeld && close && !isPinch)
                {
                    if (keyHoverStart < 0f) keyHoverStart = Time.time;
                    if (Time.time - keyHoverStart >= KeyDwellSeconds)
                    {
                        SetPortalKeyMaterial(boxHover);
                        level2HintMessage = "Pinch to grab the key";
                    }
                }
                else
                {
                    keyHoverStart = -1f;
                }

                var magnetRadius = 2.0f;
                if (!keyHeld && isPinch && (close || DistanceXZ(target, portalKey.transform.position) < magnetRadius))
                {
                    keyHeld = true;
                    var snapPos = target;
                    snapPos.y = 0.32f;
                    portalKeyBody.MovePosition(snapPos);
                    keyGrabOffset = portalKey.transform.position - target;
                    portalKeyBody.isKinematic = true;
                    Debug.Log("[Level2] Key grabbed by player.");
                }

                if (keyHeld && !isPinch)
                {
                    keyHeld = false;
                    TryActivateLevel2RuneFromKeyPosition();
                }
                if (keyHeld)
                {
                    SetPortalKeyMaterial(boxHeldMaterial);
                    var pos = target + keyGrabOffset;
                    pos.y = 0.32f;
                    portalKeyBody.MovePosition(pos);
                    level2HintMessage = "Place the key onto the rune to activate teleport.";
                }

                if (!keyHeld && !isPinch)
                {
                    TryActivateLevel2RuneFromKeyPosition();
                }
            }

            // airflow gesture: change belt direction only after the portal transfer unlocks the wind gallery.
            var airflowUnlocked = level1Stage == Level1Stage.JoinBridge || level1Stage == Level1Stage.RunToGoal;
            if (airflowUnlocked && TryGetAirflowDirectionCandidate(frame, out var candidateDir) && candidateDir != 0)
            {
                if (pendingAirDirection != candidateDir)
                {
                    pendingAirDirection = candidateDir;
                    pendingAirDirectionStart = Time.time;
                    SetAirBeltDirection(0);
                }
                else if (Time.time - pendingAirDirectionStart >= AirflowDirectionHoldSeconds)
                {
                    SetAirBeltDirection(candidateDir);
                }
            }
            else
            {
                pendingAirDirection = 0;
                pendingAirDirectionStart = -1f;
                SetAirBeltDirection(0);
            }

        }

        private void UpdateLevel2Autonomous()
        {
            if (portalKey == null || levelBall == null)
            {
                return;
            }

            UpdateAirBeltVisuals();
            var frame = receiver != null && receiver.HasFreshFrame ? receiver.Latest : GestureFrame.Neutral;
            var airflowUnlocked = level1Stage == Level1Stage.JoinBridge || level1Stage == Level1Stage.RunToGoal;
            if (airflowUnlocked && (!TryGetAirflowDirectionCandidate(frame, out var currentAirflowDirection) || currentAirflowDirection == 0))
            {
                SetAirBeltDirection(0);
            }
            if (level2Teleporting)
            {
                UpdateLevel2Teleport();
            }

            if (level1Stage == Level1Stage.ClearBlock && portalAActive && !level2Teleporting)
            {
                if (Time.time - level2LastTeleport > 0.5f)
                {
                    BeginLevel2Teleport();
                }
            }

            if (level1Stage == Level1Stage.JoinBridge || level1Stage == Level1Stage.RunToGoal)
            {
                var direction = airBeltDirection != null && airBeltDirection.Length > 0 ? airBeltDirection[0] : 0;
                if (direction == 1)
                {
                    level2HintMessage = "Airflow: RIGHT. The wind will gradually carry the ball to the altar.";
                    if (rotateGateStop != null)
                    {
                        OpenGate(rotateGateStop);
                        AdvanceLevel1(Level1Stage.RunToGoal);
                    }
                }
                else if (direction == -1)
                {
                    level2HintMessage = "Airflow: LEFT. Turn your gesture the other way to reach the goal.";
                }
                else
                {
                    level2HintMessage = "Hold the airflow gesture to create wind.";
                }
            }
        }

        private void ReleaseLevel2KeyIfTrackingLost()
        {
            if (!keyHeld)
            {
                return;
            }

            keyHeld = false;
            RestorePortalKeyMaterial();
            TryActivateLevel2RuneFromKeyPosition();
            keyHoverStart = -1f;
            lastPinchState = false;
            lastKeyInRange = false;
        }

        private bool TryActivateLevel2RuneFromKeyPosition()
        {
            if (portalAActive || portalKey == null || runeLeft == null)
            {
                return false;
            }

            if (DistanceXZ(portalKey.transform.position, runeLeft.position) >= 0.65f)
            {
                return false;
            }

            portalAActive = true;
            Debug.Log("[Level2] Rune activated - teleport enabled");
            LockPortalKeyIntoSocket();
            if (runeLeftRenderer != null) runeLeftRenderer.sharedMaterial = tealGlow;
            if (portalARenderer != null) portalARenderer.sharedMaterial = tealGlow;
            if (portalBRenderer != null) portalBRenderer.sharedMaterial = tealGlow;
            level2HintMessage = "Teleport activated. Watch the ball cross the gate.";
            return true;
        }

        private void LockPortalKeyIntoSocket()
        {
            if (portalKey == null || runeLeft == null)
            {
                return;
            }

            keyHeld = false;
            keyGrabOffset = Vector3.zero;
            var lockedPosition = runeLeft.position + new Vector3(0.12f, 0.265f, 0f);
            var lockedRotation = Quaternion.Euler(0f, 24f, 0f);
            if (portalKeyBody != null)
            {
                portalKeyBody.isKinematic = true;
                portalKeyBody.linearVelocity = Vector3.zero;
                portalKeyBody.angularVelocity = Vector3.zero;
                portalKeyBody.MovePosition(lockedPosition);
                portalKeyBody.MoveRotation(lockedRotation);
            }
            portalKey.transform.SetPositionAndRotation(lockedPosition, lockedRotation);
            RestorePortalKeyMaterial();
        }

        private void CreatePortalKeyVisual(Transform keyTransform)
        {
            var importedKey = Resources.Load<GameObject>("OpenGameArt/LowPolyKey/key");
            if (importedKey != null)
            {
                var model = Instantiate(importedKey, keyTransform);
                model.name = "OpenGameArt low-poly key model";
                model.transform.localPosition = new Vector3(0.18f, 0.08f, 0f);
                model.transform.localRotation = Quaternion.Euler(0f, 90f, 90f);
                model.transform.localScale = Vector3.one;
                DestroyImportedColliders(model.transform);
                FitImportedVisual(model.transform, 0.92f);

                foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                {
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                    renderer.sharedMaterial = brass;
                }

                var gem = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                gem.name = "portal key inset gem";
                gem.transform.SetParent(keyTransform, false);
                gem.transform.localPosition = new Vector3(-0.30f, 0.16f, 0f);
                gem.transform.localScale = Vector3.one * 0.12f;
                gem.GetComponent<Renderer>().sharedMaterial = level2PortalCoreMaterial;
                DestroyUnityObject(gem.GetComponent<Collider>());
                return;
            }

            var ring = CreateTorus("portal key round bow", keyTransform.position, 0.24f, 0.035f, brass, keyTransform);
            ring.transform.localPosition = new Vector3(-0.30f, 0.08f, 0f);
            ring.transform.localRotation = Quaternion.identity;

            CreateKeyPrimitive("portal key gem", PrimitiveType.Sphere, keyTransform, new Vector3(-0.30f, 0.08f, 0f), Vector3.one * 0.18f, Quaternion.identity, level2PortalCoreMaterial);
            CreateKeyPrimitive("portal key rounded shaft", PrimitiveType.Cylinder, keyTransform, new Vector3(0.12f, 0.08f, 0f), new Vector3(0.055f, 0.42f, 0.055f), Quaternion.Euler(0f, 0f, 90f), brass);
            CreateKeyPrimitive("portal key upper ward", PrimitiveType.Cylinder, keyTransform, new Vector3(0.48f, 0.08f, 0.13f), new Vector3(0.050f, 0.14f, 0.050f), Quaternion.Euler(90f, 0f, 0f), level2TrimMaterial);
            CreateKeyPrimitive("portal key lower ward", PrimitiveType.Cylinder, keyTransform, new Vector3(0.62f, 0.08f, -0.10f), new Vector3(0.050f, 0.13f, 0.050f), Quaternion.Euler(90f, 0f, 0f), level2TrimMaterial);
            CreateKeyPrimitive("portal key luminous bit", PrimitiveType.Sphere, keyTransform, new Vector3(0.76f, 0.08f, 0f), new Vector3(0.15f, 0.11f, 0.11f), Quaternion.identity, level2PortalCoreMaterial);
        }

        private static void FitImportedVisual(Transform root, float targetLargestAxis)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return;
            }

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            var largestAxis = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (largestAxis <= 0.0001f)
            {
                return;
            }

            root.localScale *= targetLargestAxis / largestAxis;
        }

        private void CreateKeyPrimitive(string name, PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 localScale, Quaternion localRotation, Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = localRotation;
            part.transform.localScale = localScale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            DestroyUnityObject(part.GetComponent<Collider>());
        }

        private void SetPortalKeyMaterial(Material material)
        {
            if (portalKeyRenderers == null)
            {
                if (portalKeyRenderer != null) portalKeyRenderer.sharedMaterial = material;
                return;
            }

            foreach (var renderer in portalKeyRenderers)
            {
                if (renderer != null) renderer.sharedMaterial = material;
            }
        }

        private void RestorePortalKeyMaterial()
        {
            if (portalKeyRenderers == null || portalKeyIdleMaterials == null)
            {
                if (portalKeyRenderer != null) portalKeyRenderer.sharedMaterial = brass;
                return;
            }

            for (var i = 0; i < portalKeyRenderers.Length; i++)
            {
                if (portalKeyRenderers[i] != null && i < portalKeyIdleMaterials.Length)
                {
                    portalKeyRenderers[i].sharedMaterial = portalKeyIdleMaterials[i];
                }
            }
        }

        private void BeginLevel2Teleport()
        {
            if (levelBall == null)
            {
                return;
            }

            level2Teleporting = true;
            level2TeleportStart = Time.time;
            level2TeleportStartPosition = portalAPosition + new Vector3(0f, 0.2f, 0f);
            level2TeleportEndPosition = portalBPosition + new Vector3(0f, 0.2f, 0f);
            level2LastTeleport = Time.time;
            level2HintMessage = "Portal transfer in progress...";

            levelBall.gameObject.SetActive(true);
            levelBall.transform.localScale = levelBallBaseScale;
            if (levelBallBody != null)
            {
                levelBallBody.linearVelocity = Vector3.zero;
                levelBallBody.angularVelocity = Vector3.zero;
                levelBallBody.isKinematic = true;
            }
            SetBallAlpha(1f);
        }

        private void UpdateLevel2Teleport()
        {
            if (levelBall == null)
            {
                level2Teleporting = false;
                return;
            }

            const float duration = 1.55f;
            var progress = Mathf.Clamp01((Time.time - level2TeleportStart) / duration);
            var rise = new Vector3(0f, 0.78f, 0f);

            if (progress < 0.38f)
            {
                var t = Mathf.SmoothStep(0f, 1f, progress / 0.38f);
                levelBall.transform.position = Vector3.Lerp(level2TeleportStartPosition, level2TeleportStartPosition + rise, t);
                levelBall.transform.localScale = levelBallBaseScale * Mathf.Lerp(1f, 0.58f, t);
                SetBallAlpha(1f - t);
            }
            else if (progress < 0.55f)
            {
                levelBall.transform.position = level2TeleportEndPosition + rise;
                levelBall.transform.localScale = levelBallBaseScale * 0.58f;
                SetBallAlpha(0f);
            }
            else if (progress < 1f)
            {
                var t = Mathf.SmoothStep(0f, 1f, (progress - 0.55f) / 0.45f);
                levelBall.transform.position = Vector3.Lerp(level2TeleportEndPosition + rise, level2TeleportEndPosition, t);
                levelBall.transform.localScale = levelBallBaseScale * Mathf.Lerp(0.58f, 1f, t);
                SetBallAlpha(t);
            }
            else
            {
                levelBall.transform.position = level2TeleportEndPosition;
                levelBall.transform.localScale = levelBallBaseScale;
                SetBallAlpha(1f);
                if (levelBallBody != null)
                {
                    levelBallBody.isKinematic = false;
                    levelBallBody.linearVelocity = Vector3.zero;
                    levelBallBody.angularVelocity = Vector3.zero;
                    levelBallBody.WakeUp();
                }
                OpenGate(bridgeGate);
                AdvanceLevel1(Level1Stage.JoinBridge);
                level2Teleporting = false;
                level2HintMessage = "Use the airflow gesture to set wind direction.";
            }
        }

        private void SetBallAlpha(float alpha)
        {
            if (levelBallRuntimeMaterial == null || levelBallRenderer == null)
            {
                return;
            }

            var color = levelBallRuntimeMaterial.color;
            color.a = Mathf.Clamp01(alpha);
            levelBallRuntimeMaterial.color = color;
            levelBallRenderer.sharedMaterial = levelBallRuntimeMaterial;
        }

        private static void ConfigureTransparentMaterial(Material material)
        {
            material.SetFloat("_Mode", 3f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        private void SetAirBeltDirection(int direction)
        {
            if (airBeltDirection == null || airBeltDirection.Length == 0)
            {
                return;
            }

            airBeltDirection[0] = direction;
            if (airBeltTriggers != null && airBeltTriggers.Length > 0 && airBeltTriggers[0] != null)
            {
                airBeltTriggers[0].direction = direction;
            }
            if (airBeltRenderers != null && airBeltRenderers.Length > 0 && airBeltRenderers[0] != null)
            {
                airBeltRenderers[0].sharedMaterial = direction == 1 ? tealGlow : (direction == -1 ? amberGlow : boxIdle);
            }
            if (airBeltArrowRenderers != null && airBeltArrowRenderers.Length > 0 && airBeltArrowRenderers[0] != null)
            {
                airBeltArrowRenderers[0].sharedMaterial = direction == 1 ? tealGlow : (direction == -1 ? amberGlow : boxIdle);
            }
            if (airBeltArrowTransforms != null && airBeltArrowTransforms.Length > 0 && airBeltArrowTransforms[0] != null)
            {
                airBeltArrowTransforms[0].localPosition = new Vector3(direction < 0 ? -0.18f : 0.18f, 0.28f, -0.70f);
            }
            UpdateAirBeltVisuals();
        }

        private void UpdateAirBeltVisuals()
        {
            if (airBelts == null || airBelts.Length == 0 || airBelts[0] == null)
            {
                return;
            }

            var direction = airBeltDirection != null && airBeltDirection.Length > 0 ? airBeltDirection[0] : 0;
            var active = direction != 0;
            var material = direction == -1 ? amberGlow : (direction == 1 ? tealGlow : boxIdle);
            var windColor = direction == -1 ? new Color(1f, 0.73f, 0.22f, active ? 0.82f : 0.20f) : new Color(0.38f, 0.95f, 1f, active ? 0.82f : 0.20f);
            TintParticleMaterial(level2WindRibbonMaterial, windColor);
            TintParticleMaterial(level2WindMistMaterial, new Color(windColor.r, windColor.g, windColor.b, active ? 0.42f : 0.12f));
            if (airBeltRenderers != null && airBeltRenderers.Length > 0 && airBeltRenderers[0] != null)
            {
                airBeltRenderers[0].enabled = false;
                airBeltRenderers[0].sharedMaterial = material;
            }
            if (airBeltArrowRenderers != null && airBeltArrowRenderers.Length > 0 && airBeltArrowRenderers[0] != null)
            {
                airBeltArrowRenderers[0].enabled = active;
                airBeltArrowRenderers[0].sharedMaterial = material;
            }

            if (airBeltStreaks == null)
            {
                UpdateAirflowParticleVisuals(direction, active, material.color);
                return;
            }

            if (airBeltMistQuads != null)
            {
                for (var i = 0; i < airBeltMistQuads.Length; i++)
                {
                    var mist = airBeltMistQuads[i];
                    if (mist == null)
                    {
                        continue;
                    }

                    var baseZ = i % 2 == 0 ? -0.28f : 0.28f;
                    var cycle = Mathf.Repeat(Time.time * (active ? 0.30f : 0.08f) + i * 0.19f, 1f);
                    var x = active ? Mathf.Lerp(-1.92f, 1.92f, direction > 0 ? cycle : 1f - cycle) : Mathf.Lerp(-1.68f, 1.68f, i / (float)Mathf.Max(airBeltMistQuads.Length - 1, 1));
                    mist.localPosition = new Vector3(x, 0.285f, baseZ);
                    mist.localScale = new Vector3(active ? 2.0f : 1.32f, active ? 0.74f : 0.44f, 1f);
                    if (airBeltMistRenderers != null && i < airBeltMistRenderers.Length && airBeltMistRenderers[i] != null)
                    {
                        airBeltMistRenderers[i].enabled = active;
                        airBeltMistRenderers[i].sharedMaterial = level2WindMistMaterial != null ? level2WindMistMaterial : level2WindMaterial;
                    }
                }
            }

            for (var i = 0; i < airBeltStreaks.Length; i++)
            {
                var streak = airBeltStreaks[i];
                if (streak == null)
                {
                    continue;
                }

                var baseZ = i % 2 == 0 ? -0.42f : 0.42f;
                var cycle = Mathf.Repeat(Time.time * (active ? 0.72f : 0.18f) + i * 0.23f, 1f);
                var x = active ? Mathf.Lerp(-2.18f, 2.18f, direction > 0 ? cycle : 1f - cycle) : Mathf.Lerp(-2.02f, 2.02f, i / (float)Mathf.Max(airBeltStreaks.Length - 1, 1));
                streak.localPosition = new Vector3(x, 0.31f, baseZ);
                streak.localScale = new Vector3(active ? 1.42f : 0.86f, active ? 0.30f : 0.18f, 1f);
                if (airBeltStreakRenderers != null && i < airBeltStreakRenderers.Length && airBeltStreakRenderers[i] != null)
                {
                    airBeltStreakRenderers[i].enabled = active;
                    airBeltStreakRenderers[i].sharedMaterial = level2WindRibbonMaterial != null ? level2WindRibbonMaterial : level2WindMaterial;
                }
            }
            UpdateAirflowParticleVisuals(direction, active, material.color);
        }

        private static void TintParticleMaterial(Material material, Color color)
        {
            if (material == null)
            {
                return;
            }

            material.color = color;
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_TintColor")) material.SetColor("_TintColor", color);
        }

        private void UpdateAirflowParticleVisuals(int direction, bool active, Color color)
        {
            if (airBeltParticles == null)
            {
                return;
            }

            foreach (var particleSystem in airBeltParticles)
            {
                if (particleSystem == null)
                {
                    continue;
                }

                var main = particleSystem.main;
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(color.r, color.g, color.b, active ? 0.68f : 0.24f));
                main.startSpeed = new ParticleSystem.MinMaxCurve(active ? 0.20f : 0.05f, active ? 0.48f : 0.14f);
                var emission = particleSystem.emission;
                emission.rateOverTime = active ? 52f : 12f;
                var velocity = particleSystem.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.Local;
                velocity.x = new ParticleSystem.MinMaxCurve(active ? direction * 1.7f : 0.18f);
                if (!particleSystem.isPlaying)
                {
                    particleSystem.Play();
                }
            }
        }

        private static bool IsAirflowGesture(GestureHandFrame hand)
        {
            if (hand.landmarks == null || hand.landmarks.Length < 21)
            {
                return false;
            }

            if (hand.openPalm || hand.palmSpan <= 0f || !hand.thumbExtended || !IndexMiddleTogether(hand))
            {
                return false;
            }

            var ringCurled = FingerCurled(hand, 16, 14, 13);
            var pinkyCurled = FingerCurled(hand, 20, 18, 17);
            return ringCurled && pinkyCurled && !hand.ringExtended && !hand.pinkyExtended;
        }

        private static bool FingerCurled(GestureHandFrame hand, int tipIndex, int pipIndex, int mcpIndex)
        {
            var wrist = hand.landmarks[0];
            var tip = hand.landmarks[tipIndex];
            var pip = hand.landmarks[pipIndex];
            var mcp = hand.landmarks[mcpIndex];
            var span = Mathf.Max(hand.palmSpan, 0.0001f);
            var tipToWrist = LandmarkDistance(tip, wrist) / span;
            var pipToWrist = LandmarkDistance(pip, wrist) / span;
            var mcpToWrist = LandmarkDistance(mcp, wrist) / span;

            var foldedTowardPalm = tipToWrist < pipToWrist * 1.08f || tipToWrist < mcpToWrist * 1.28f;
            var notPointingUp = tip.y > pip.y - span * 0.04f;
            return foldedTowardPalm || notPointingUp;
        }

        private static float LandmarkDistance(GestureLandmark a, GestureLandmark b)
        {
            var dx = a.x - b.x;
            var dy = a.y - b.y;
            var dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private bool TryGetAirflowHand(GestureFrame frame, out GestureHandFrame hand)
        {
            hand = default;
            if (frame.hands == null || frame.hands.Length == 0)
            {
                return false;
            }
            foreach (var h in frame.hands)
            {
                if (h.score >= 0.45f && IsAirflowGesture(h))
                {
                    hand = h; return true;
                }
            }
            return false;
        }

        private bool TryGetAirflowDirectionCandidate(GestureFrame frame, out int direction)
        {
            direction = 0;
            if (!TryGetAirflowHand(frame, out var hand))
            {
                return false;
            }

            var dirX = hand.landmarks != null && hand.landmarks.Length > 8 ? hand.landmarks[8].x - hand.landmarks[0].x : hand.indexX - hand.pinchX;
            direction = Mathf.Abs(dirX) < AirflowDirectionDeadZone ? 0 : (dirX > 0f ? 1 : -1);
            return true;
        }

        // -------------------- End Level2 methods --------------------

        // -------------------- Level4: Mirror & Magnet --------------------
        private void BuildLevel4()
        {
            levelRoot = new GameObject("Level04 Mirror Magnet").transform;
            level4Stage = Level4Stage.LightGuide;
            level4MagnetPolarity = -1;
            level4MirrorHeld = false;
            level4LightSolved = false;
            level4BackstopRaised = false;
            ResetLevel4MagnetFlipGesture();
            level1SuccessUntil = -1f;
            level4NorthMaterial = NewMaterial("Level4 soft north red", new Color(0.82f, 0.28f, 0.25f), 0.44f, 0.18f);
            level4SouthMaterial = NewMaterial("Level4 soft south blue", new Color(0.25f, 0.44f, 0.86f), 0.44f, 0.18f);
            ConfigureLevel4CameraAndLights();

            var roadRotation = Quaternion.Euler(0f, 0f, Level4RoadAngleDegrees);
            CreateBox("level4 deep shadow under slope", new Vector3(0.70f, -0.74f, 0f), new Vector3(12.6f, 0.48f, 5.1f), darkStone, levelRoot, Quaternion.identity, false);
            CreateLevel4RoadSegment("level4 mirror approach slope", -2.95f, 3.20f, 2.10f, roadRotation);
            CreateLevel4RoadSegment("level4 light gate slope", -0.35f, 2.10f, 1.86f, roadRotation);
            CreateLevel4RoadSegment("level4 full magnetic support slope", 3.38f, 5.45f, 1.72f, roadRotation);
            CreateLevel4SideShadow("level4 lower slope shadow", new Vector3(0.70f, Level4RoadY(0.70f) - 0.24f, -1.06f), new Vector3(10.8f, 0.22f, 0.18f), roadRotation);
            CreateLevel4SideShadow("level4 upper slope shadow", new Vector3(0.70f, Level4RoadY(0.70f) - 0.18f, 1.08f), new Vector3(10.8f, 0.16f, 0.13f), roadRotation);

            var source = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            source.name = "level4 light source";
            source.transform.SetParent(levelRoot, false);
            source.transform.position = new Vector3(-4.34f, Level4RoadY(-4.34f) + 0.24f, 1.03f);
            source.transform.localScale = new Vector3(0.26f, 0.08f, 0.26f);
            source.GetComponent<Renderer>().sharedMaterial = amberGlow;
            DestroyUnityObject(source.GetComponent<Collider>());
            CreateTorus("level4 light source halo", source.transform.position + new Vector3(0f, 0.09f, 0f), 0.30f, 0.026f, level2RuneMaterial, levelRoot);

            var mirrorPosition = new Vector3(-2.58f, Level4RoadY(-2.58f) + 0.62f, 1.03f);
            var mirrorObject = CreateBox("level4 rotatable side mirror", mirrorPosition, new Vector3(0.14f, 0.70f, 0.62f), level2TrimMaterial, levelRoot, Quaternion.Euler(0f, -18f, 0f), false);
            level4Mirror = mirrorObject.transform;
            level4MirrorRenderer = mirrorObject.GetComponent<Renderer>();
            CreateTorus("level4 mirror pivot ring", level4Mirror.position + new Vector3(0f, -0.31f, 0f), 0.36f, 0.024f, tealGlow, levelRoot);
            level4MirrorStartYaw = level4Mirror.eulerAngles.y;

            level4LightGate = CreateLevel2Gate("level4 light energy gate", new Vector3(Level4LightGateX, Level4RoadY(Level4LightGateX) + 0.66f, 0f), 1.92f);
            level4DoorRenderer = level4LightGate.transform.Find("level4 light energy gate cyan lock core")?.GetComponent<Renderer>();
            CreateLevel4LightReceiver(new Vector3(Level4LightGateX, Level4RoadY(Level4LightGateX) + 0.34f, 1.03f));

            level4BeamSegments = new Transform[2];
            level4BeamSegments[0] = CreateLevel4Beam("level4 source-to-mirror beam", source.transform.position + new Vector3(0f, 0.22f, 0f), level4Mirror.position, amberGlow).transform;
            level4BeamSegments[1] = CreateLevel4Beam("level4 reflected beam", level4Mirror.position, level4Mirror.position + Quaternion.Euler(0f, -18f, 0f) * Vector3.right * 2.10f, amberGlow).transform;

            BuildLevel4MagnetRail();
            CreateBox("level4 magnet activation line", new Vector3(Level4MagnetTriggerX, Level4RoadY(Level4MagnetTriggerX) + 0.135f, 0f), new Vector3(0.055f, 0.032f, 1.56f), level2RuneMaterial, levelRoot, roadRotation, false);
            level4MagnetBackstop = CreateLevel2Gate("level4 magnetic entry backstop", new Vector3(Level4LightGateX, Level4RoadY(Level4LightGateX) + 0.66f, 0f), 1.92f);
            level4MagnetBackstop.SetActive(false);
            level4MagnetGate = CreateLevel2Gate("level4 magnetic lock gate", new Vector3(Level4MagnetGateX, Level4RoadY(Level4MagnetGateX) + 0.66f, 0f), 1.62f);

            var ballObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballObject.name = "Silver Magnetic Ball";
            ballObject.transform.SetParent(levelRoot, false);
            ballObject.transform.position = new Vector3(-4.35f, Level4RoadY(-4.35f) + 0.34f, 0f);
            ballObject.transform.localScale = Vector3.one * 0.46f;
            ballObject.GetComponent<Renderer>().sharedMaterial = NewMaterial("Silver magnetic ball", new Color(0.70f, 0.78f, 0.76f), 0.75f, 0.06f);
            var body = ballObject.AddComponent<Rigidbody>();
            body.mass = 1.0f;
            body.linearDamping = 0.34f;
            body.angularDamping = 0.16f;
            levelBall = ballObject.AddComponent<BallController>();
            levelBallBody = body;

            var goal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            goal.name = "Level4 Goal Trigger";
            goal.transform.SetParent(levelRoot, false);
            goal.transform.position = new Vector3(Level4GoalX, Level4RoadY(Level4GoalX) + 0.2f, 0f);
            goal.transform.localScale = new Vector3(0.7f, 0.11f, 0.7f);
            goal.GetComponent<Renderer>().sharedMaterial = level2PortalCoreMaterial;
            DestroyUnityObject(goal.GetComponent<Collider>());
            levelBall.Configure(goal.transform);
            CreateLevel2GoalArt(goal.transform.position);
        }

        private void ConfigureLevel4CameraAndLights()
        {
            if (mainCamera != null)
            {
                mainCamera.orthographicSize = 4.05f;
                mainCamera.transform.SetPositionAndRotation(new Vector3(0.50f, 7.45f, -5.9f), Quaternion.Euler(55f, 0f, 0f));
                mainCamera.backgroundColor = new Color(0.012f, 0.014f, 0.014f);
            }

            var accent = new GameObject("Level4 Mirror Accent Light");
            accent.transform.SetParent(levelRoot, false);
            accent.transform.position = new Vector3(-1.6f, 2.8f, -1.1f);
            var light = accent.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.72f, 0.28f);
            light.intensity = 1.1f;
            light.range = 5.0f;
        }

        private void BuildLevel4MagnetRail()
        {
            var roadRotation = Quaternion.Euler(0f, 0f, Level4RoadAngleDegrees);
            for (var i = 0; i < 8; i++)
            {
                var x = Mathf.Lerp(-0.22f, 5.05f, i / 7f);
                CreateBox($"level4 brass magnetic rail {i}", new Vector3(x, Level4RoadY(x) + 0.15f, -0.34f), new Vector3(0.52f, 0.035f, 0.055f), level2TrimMaterial, levelRoot, roadRotation, false);
                CreateBox($"level4 brass magnetic rail pair {i}", new Vector3(x, Level4RoadY(x) + 0.15f, 0.34f), new Vector3(0.52f, 0.035f, 0.055f), level2TrimMaterial, levelRoot, roadRotation, false);
            }

            var magnetPosition = new Vector3(Level4MagnetX, Level4RoadY(Level4MagnetX) + 0.18f, 0f);
            var stand = CreateBox("level4 bar magnet stand", magnetPosition, new Vector3(1.84f, 0.14f, 0.66f), level2TrimMaterial, levelRoot, roadRotation, false);
            level4MagnetRenderer = stand.GetComponent<Renderer>();

            level4MagnetTurntable = new GameObject("level4 rotating bar magnet").transform;
            level4MagnetTurntable.SetParent(levelRoot, false);
            level4MagnetTurntable.position = magnetPosition + new Vector3(0f, 0.20f, 0f);
            level4MagnetTurntable.rotation = roadRotation;

            var northPole = CreateBox("level4 bar magnet north red", level4MagnetTurntable.position, new Vector3(0.86f, 0.18f, 0.38f), level4NorthMaterial, level4MagnetTurntable, Quaternion.identity, false);
            var southPole = CreateBox("level4 bar magnet south blue", level4MagnetTurntable.position, new Vector3(0.86f, 0.18f, 0.38f), level4SouthMaterial, level4MagnetTurntable, Quaternion.identity, false);
            northPole.transform.localPosition = new Vector3(-0.43f, 0f, 0f);
            northPole.transform.localRotation = Quaternion.identity;
            southPole.transform.localPosition = new Vector3(0.43f, 0f, 0f);
            southPole.transform.localRotation = Quaternion.identity;
            CreateLevel4MagnetLabel("N", northPole.transform.position + new Vector3(0f, 0.11f, 0f), level4NorthMaterial, level4MagnetTurntable);
            CreateLevel4MagnetLabel("S", southPole.transform.position + new Vector3(0f, 0.11f, 0f), level4SouthMaterial, level4MagnetTurntable);
            level4MagnetNeedle = null;
            level4MagnetNeedleRenderer = null;
        }

        private void CreateLevel4RoadSegment(string name, float centerX, float width, float depth, Quaternion roadRotation)
        {
            CreateBox(name, new Vector3(centerX, Level4RoadY(centerX), 0f), new Vector3(width, 0.18f, depth), level2FloorMaterial, levelRoot, roadRotation, true);
            CreateBox(name + " front brass edge", new Vector3(centerX, Level4RoadY(centerX) + 0.115f, -depth * 0.50f), new Vector3(width, 0.05f, 0.06f), level2TrimMaterial, levelRoot, roadRotation, false);
            CreateBox(name + " rear brass edge", new Vector3(centerX, Level4RoadY(centerX) + 0.115f, depth * 0.50f), new Vector3(width, 0.05f, 0.06f), level2TrimMaterial, levelRoot, roadRotation, false);
            CreateBox(name + " downhill dark thickness", new Vector3(centerX, Level4RoadY(centerX) - 0.18f, -depth * 0.50f - 0.04f), new Vector3(width, 0.25f, 0.10f), cliffStone, levelRoot, roadRotation, false);
        }

        private void CreateLevel4SideShadow(string name, Vector3 position, Vector3 scale, Quaternion rotation)
        {
            CreateBox(name, position, scale, cliffStone, levelRoot, rotation, false);
        }

        private void CreateLevel4LightReceiver(Vector3 position)
        {
            var root = new GameObject("level4 light receiver target");
            root.transform.SetParent(levelRoot, false);
            root.transform.position = position;
            CreateBox("level4 receiver brass plate", position, new Vector3(0.56f, 0.05f, 0.32f), level2TrimMaterial, root.transform, Quaternion.identity, false).transform.localPosition = Vector3.zero;
            CreateTorus("level4 receiver cyan bullseye", position + new Vector3(0f, 0.07f, 0f), 0.25f, 0.020f, tealGlow, root.transform);
            CreateTorus("level4 receiver inner bullseye", position + new Vector3(0f, 0.10f, 0f), 0.12f, 0.016f, amberGlow, root.transform);
        }

        private void CreateLevel4MagnetLabel(string text, Vector3 position, Material material, Transform parent)
        {
            var label = new GameObject("level4 magnet label " + text);
            label.transform.SetParent(parent, true);
            label.transform.position = position;
            label.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            label.transform.localScale = Vector3.one * 0.16f;
            var mesh = label.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontSize = 44;
            mesh.color = material != null ? material.color : Color.white;
        }

        private GameObject CreateLevel4Beam(string name, Vector3 start, Vector3 end, Material material)
        {
            var root = new GameObject(name);
            root.transform.SetParent(levelRoot, false);

            var halo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            halo.name = name + " soft glow";
            halo.transform.SetParent(root.transform, false);
            DestroyUnityObject(halo.GetComponent<Collider>());

            var core = GameObject.CreatePrimitive(PrimitiveType.Quad);
            core.name = name + " bright core";
            core.transform.SetParent(root.transform, false);
            DestroyUnityObject(core.GetComponent<Collider>());

            SetLevel4BeamMaterials(root.transform, material == tealGlow);
            PositionLevel4Beam(root.transform, start, end);
            return root;
        }

        private void PositionLevel4Beam(Transform beam, Vector3 start, Vector3 end)
        {
            if (beam == null)
            {
                return;
            }

            var delta = end - start;
            var length = new Vector2(delta.x, delta.z).magnitude;
            beam.position = (start + end) * 0.5f;
            beam.rotation = Quaternion.Euler(0f, Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg + 90f, 0f);
            beam.localScale = Vector3.one;
            var halo = beam.Find(beam.name + " soft glow");
            if (halo != null)
            {
                halo.localPosition = Vector3.zero;
                halo.localRotation = Quaternion.identity;
                halo.localScale = new Vector3(length, 0.34f, 1f);
            }
            var core = beam.Find(beam.name + " bright core");
            if (core != null)
            {
                core.localPosition = new Vector3(0f, 0.002f, 0f);
                core.localRotation = Quaternion.identity;
                core.localScale = new Vector3(length, 0.11f, 1f);
            }
        }

        private void SetLevel4BeamMaterials(Transform beam, bool aligned)
        {
            if (beam == null)
            {
                return;
            }

            var halo = beam.Find(beam.name + " soft glow")?.GetComponent<Renderer>();
            if (halo != null)
            {
                halo.sharedMaterial = aligned ? level4BeamHaloTealMaterial : level4BeamHaloAmberMaterial;
            }
            var core = beam.Find(beam.name + " bright core")?.GetComponent<Renderer>();
            if (core != null)
            {
                core.sharedMaterial = aligned ? level4BeamTealMaterial : level4BeamAmberMaterial;
            }
        }

        private void UpdateLevel4(GestureHandFrame hand)
        {
            if (levelBall == null)
            {
                return;
            }

            if (level4Stage == Level4Stage.LightGuide && TryGetTwoPinchingHands(out var a, out var b))
            {
                var angle = Mathf.Atan2(b.pinchY - a.pinchY, b.pinchX - a.pinchX) * Mathf.Rad2Deg;
                if (!level4MirrorHeld)
                {
                    level4MirrorHeld = true;
                    level4RotateStartAngle = angle;
                    level4MirrorStartYaw = NormalizeYaw(level4Mirror.eulerAngles.y);
                }

                var delta = Mathf.DeltaAngle(level4RotateStartAngle, angle);
                var yaw = Mathf.Clamp(level4MirrorStartYaw + delta, -68f, 68f);
                level4Mirror.rotation = Quaternion.Euler(0f, yaw, 0f);
                if (level4MirrorRenderer != null) level4MirrorRenderer.sharedMaterial = boxHeldMaterial;
            }
            else
            {
                level4MirrorHeld = false;
                if (!level4LightSolved && level4MirrorRenderer != null) level4MirrorRenderer.sharedMaterial = level2TrimMaterial;
            }

            if (level4Stage == Level4Stage.MagneticTurntable || level4Stage == Level4Stage.RunToGoal)
            {
                UpdateLevel4MagnetFlipGesture(hand);
            }
            else
            {
                ResetLevel4MagnetFlipGesture();
            }
        }

        private void UpdateLevel4MagnetFlipGesture(GestureHandFrame hand)
        {
            if (!TryGetThumbOnlyDirectionFromCurrentFrame(hand, out var direction))
            {
                ResetLevel4MagnetFlipGesture();
                return;
            }

            if (direction == level4MagnetPolarity)
            {
                ResetLevel4MagnetFlipGesture();
                return;
            }

            if (level4PendingMagnetDirection != direction)
            {
                level4PendingMagnetDirection = direction;
                level4PendingMagnetDirectionStart = Time.time;
                return;
            }

            if (Time.time - level4PendingMagnetDirectionStart < MagnetThumbDirectionHoldSeconds)
            {
                return;
            }

            SetLevel4MagnetPolarity(direction);
            ResetLevel4MagnetFlipGesture();
        }

        private void ResetLevel4MagnetFlipGesture()
        {
            level4PendingMagnetDirection = 0;
            level4PendingMagnetDirectionStart = -1f;
        }

        private void UpdateLevel4Autonomous()
        {
            if (levelBall == null || levelBallBody == null)
            {
                return;
            }

            UpdateLevel4Light();
            UpdateLevel4MagnetVisuals();
            ClampLevel4BallSpeed();

            if (level4LightSolved && level4Stage == Level4Stage.LightGuide)
            {
                levelBallBody.AddForce(Vector3.right * 0.20f, ForceMode.Acceleration);
                if (levelBall.transform.position.x > Level4MagnetTriggerX)
                {
                    level4Stage = Level4Stage.MagneticTurntable;
                    level1SuccessUntil = Time.time + 1.2f;
                    level4BackstopRaised = true;
                    if (level4MagnetBackstop != null)
                    {
                        level4MagnetBackstop.SetActive(true);
                    }
                    SetLevel4MagnetPolarity(-1);
                }
            }

            if (level4Stage == Level4Stage.MagneticTurntable || level4Stage == Level4Stage.RunToGoal)
            {
                if (!level4BackstopRaised && levelBall.transform.position.x > Level4MagnetTriggerX)
                {
                    level4BackstopRaised = true;
                    if (level4MagnetBackstop != null)
                    {
                        level4MagnetBackstop.SetActive(true);
                    }
                }

                ApplyLevel4MagnetForce();
                if (level4Stage == Level4Stage.MagneticTurntable && level4MagnetPolarity > 0)
                {
                    OpenGate(level4MagnetGate);
                }
                if (levelBall.transform.position.x > Level4MagnetGateX + 0.35f && level4Stage == Level4Stage.MagneticTurntable)
                {
                    level4Stage = Level4Stage.RunToGoal;
                    level1SuccessUntil = Time.time + 1.2f;
                }
            }
        }

        private void ClampLevel4BallSpeed()
        {
            if (levelBallBody == null)
            {
                return;
            }

            if (levelBallBody.linearVelocity.magnitude > 2.15f)
            {
                levelBallBody.linearVelocity = levelBallBody.linearVelocity.normalized * 2.15f;
            }
        }

        private void UpdateLevel4Light()
        {
            if (level4Mirror == null || level4BeamSegments == null || level4BeamSegments.Length < 2)
            {
                return;
            }

            var mirrorPosition = level4Mirror.position + new Vector3(0f, -0.08f, 0f);
            var sourcePosition = new Vector3(-4.34f, Level4RoadY(-4.34f) + 0.46f, 1.03f);
            var targetGatePosition = new Vector3(Level4LightGateX, Level4RoadY(Level4LightGateX) + 0.56f, 1.03f);
            PositionLevel4Beam(level4BeamSegments[0], sourcePosition, mirrorPosition);

            var yaw = NormalizeYaw(level4Mirror.eulerAngles.y);
            var aligned = Mathf.Abs(Mathf.DeltaAngle(yaw, Level4MirrorTargetYaw)) <= 7.5f;
            var reflectedEnd = aligned ? targetGatePosition : mirrorPosition + Quaternion.Euler(0f, yaw, 0f) * Vector3.right * 2.10f;
            PositionLevel4Beam(level4BeamSegments[1], mirrorPosition, reflectedEnd);

            SetLevel4BeamMaterials(level4BeamSegments[1], aligned);
            if (level4DoorRenderer != null) level4DoorRenderer.sharedMaterial = aligned ? tealGlow : level2PortalCoreMaterial;

            var wasSolved = level4LightSolved;
            level4LightSolved = aligned;

            if (level4Stage == Level4Stage.LightGuide && level4LightGate != null)
            {
                level4LightGate.SetActive(!aligned);
            }
            else if (level4LightGate != null)
            {
                level4LightGate.SetActive(false);
            }

            if (aligned && !wasSolved)
            {
                if (level4MirrorRenderer != null) level4MirrorRenderer.sharedMaterial = tealGlow;
                level1SuccessUntil = Time.time + 1.2f;
            }
        }

        private void SetLevel4MagnetPolarity(int polarity)
        {
            level4MagnetPolarity = Mathf.Clamp(polarity, -1, 1);
            UpdateLevel4MagnetVisuals();
        }

        private void UpdateLevel4MagnetVisuals()
        {
            if (level4MagnetRenderer != null) level4MagnetRenderer.sharedMaterial = level2TrimMaterial;
            if (level4MagnetTurntable != null)
            {
                var targetYaw = level4MagnetPolarity < 0 ? 180f : 0f;
                level4MagnetTurntable.rotation = Quaternion.Euler(0f, 0f, Level4RoadAngleDegrees) * Quaternion.Euler(0f, targetYaw, 0f);
            }
        }

        private void ApplyLevel4MagnetForce()
        {
            if (level4MagnetPolarity == 0 || levelBallBody == null)
            {
                return;
            }

            var ballPosition = levelBallBody.position;
            if (ballPosition.x < Level4LightGateX - 0.28f || ballPosition.x > Level4GoalX + 0.60f)
            {
                return;
            }

            var direction = level4MagnetPolarity > 0 ? Vector3.right : Vector3.left;
            var distanceFromCoil = Mathf.Abs(ballPosition.x - Level4MagnetX);
            var falloff = Mathf.Clamp01(1f - distanceFromCoil / (Level4MagnetRadius + 0.75f));
            var baseStrength = level4MagnetPolarity > 0 ? 0.82f : 0.45f;
            levelBallBody.AddForce(direction * (Level4MagnetForce * (baseStrength + falloff)), ForceMode.Acceleration);
            if (level4MagnetPolarity > 0 && levelBallBody.linearVelocity.x < 0.22f)
            {
                var velocity = levelBallBody.linearVelocity;
                velocity.x = 0.22f;
                levelBallBody.linearVelocity = velocity;
            }
            if (levelBallBody.linearVelocity.magnitude > 2.15f)
            {
                levelBallBody.linearVelocity = levelBallBody.linearVelocity.normalized * 2.15f;
            }
        }

        private static float NormalizeYaw(float yaw)
        {
            return Mathf.DeltaAngle(0f, yaw);
        }

        private static float Level4RoadY(float x)
        {
            return Level4RoadCenterY + x * Mathf.Sin(Level4RoadAngleDegrees * Mathf.Deg2Rad);
        }

        // -------------------- End Level4 methods --------------------

        private static bool HasLeftAndRightHands(GestureFrame frame)
        {
            return HasHandedness(frame, "Left") && HasHandedness(frame, "Right");
        }

        private static bool HasHandedness(GestureFrame frame, string handedness)
        {
            if (frame.hands == null)
            {
                return false;
            }

            foreach (var hand in frame.hands)
            {
                if (hand.score >= 0.35f && string.Equals(hand.handedness, handedness, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool TryGetHandBySide(GestureFrame frame, string handedness, out GestureHandFrame hand)
        {
            hand = default;
            if (frame.hands == null)
            {
                return false;
            }

            foreach (var candidate in frame.hands)
            {
                if (candidate.score >= 0.35f && string.Equals(candidate.handedness, handedness, System.StringComparison.OrdinalIgnoreCase))
                {
                    hand = candidate;
                    return true;
                }
            }
            return false;
        }

        private static bool IndexTipsTogether(GestureHandFrame left, GestureHandFrame right)
        {
            if (left.landmarks == null || right.landmarks == null || left.landmarks.Length < 9 || right.landmarks.Length < 9)
            {
                return false;
            }

            var averageSpan = Mathf.Max((left.palmSpan + right.palmSpan) * 0.5f, 0.0001f);
            var distance = LandmarkDistance(left.landmarks[8], right.landmarks[8]) / averageSpan;
            return distance < 0.58f;
        }

        private static bool IsFist(GestureHandFrame hand)
        {
            if (hand.score < 0.35f || hand.openPalm || hand.landmarks == null || hand.landmarks.Length < 21)
            {
                return false;
            }

            var folded = 0;
            if (!hand.indexExtended || FingerCurled(hand, 8, 6, 5)) folded++;
            if (!hand.middleExtended || FingerCurled(hand, 12, 10, 9)) folded++;
            if (!hand.ringExtended || FingerCurled(hand, 16, 14, 13)) folded++;
            if (!hand.pinkyExtended || FingerCurled(hand, 20, 18, 17)) folded++;
            return folded >= 4 && !hand.indexExtended && !hand.middleExtended;
        }

        private static bool IsIndexCrossEraseGesture(GestureFrame frame, out Vector2 crossPoint)
        {
            crossPoint = Vector2.zero;
            if (!TryGetHandBySide(frame, "Left", out var left) || !TryGetHandBySide(frame, "Right", out var right))
            {
                return false;
            }

            if (left.landmarks == null || right.landmarks == null || left.landmarks.Length < 9 || right.landmarks.Length < 9)
            {
                return false;
            }

            if (!left.indexExtended || !right.indexExtended || left.openPalm || right.openPalm)
            {
                return false;
            }

            var leftMcp = new Vector2(left.landmarks[5].x, left.landmarks[5].y);
            var leftTip = new Vector2(left.landmarks[8].x, left.landmarks[8].y);
            var rightMcp = new Vector2(right.landmarks[5].x, right.landmarks[5].y);
            var rightTip = new Vector2(right.landmarks[8].x, right.landmarks[8].y);
            var angle = Vector2.Angle(leftTip - leftMcp, rightTip - rightMcp);
            if (angle < 35f || angle > 145f)
            {
                return false;
            }

            var tipsClose = Vector2.Distance(leftTip, rightTip) < 0.16f;
            var segmentsCross = TryGetSegmentsIntersection(leftMcp, leftTip, rightMcp, rightTip, out var intersection);
            if (!tipsClose && !segmentsCross)
            {
                return false;
            }

            crossPoint = segmentsCross ? intersection : (leftTip + rightTip) * 0.5f;
            return true;
        }

        private static bool SegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            return TryGetSegmentsIntersection(a, b, c, d, out _);
        }

        private static bool TryGetSegmentsIntersection(Vector2 a, Vector2 b, Vector2 c, Vector2 d, out Vector2 intersection)
        {
            var r = b - a;
            var s = d - c;
            var denominator = Cross2D(r, s);
            if (Mathf.Abs(denominator) < 0.0001f)
            {
                intersection = Vector2.zero;
                return false;
            }

            var u = Cross2D(c - a, r) / denominator;
            var t = Cross2D(c - a, s) / denominator;
            if (t < 0f || t > 1f || u < 0f || u > 1f)
            {
                intersection = Vector2.zero;
                return false;
            }

            intersection = a + r * t;
            return true;
        }

        private static float Cross2D(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        private bool TryGetTwoFingerMapHands(GestureFrame frame, out GestureHandFrame a, out GestureHandFrame b)
        {
            a = default;
            b = default;
            if (frame.hands == null || frame.hands.Length < 2)
            {
                return false;
            }

            var found = 0;
            foreach (var hand in frame.hands)
            {
                if (hand.score < 0.35f || !IndexMiddleTogether(hand))
                {
                    continue;
                }

                if (found == 0)
                {
                    a = hand;
                }
                else
                {
                    b = hand;
                    return true;
                }
                found++;
            }
            return false;
        }

        private static bool IndexMiddleTogether(GestureHandFrame hand)
        {
            if (hand.landmarks == null || hand.landmarks.Length < 21 || hand.palmSpan <= 0f)
            {
                return false;
            }

            var indexTip = hand.landmarks[8];
            var middleTip = hand.landmarks[12];
            var dx = indexTip.x - middleTip.x;
            var dy = indexTip.y - middleTip.y;
            var dz = indexTip.z - middleTip.z;
            var distance = Mathf.Sqrt(dx * dx + dy * dy + dz * dz) / Mathf.Max(hand.palmSpan, 0.0001f);
            return distance < 0.55f && hand.indexExtended && hand.middleExtended;
        }

        private static Vector2 FingerMidpoint(GestureHandFrame hand)
        {
            if (hand.landmarks == null || hand.landmarks.Length < 13)
            {
                return new Vector2(hand.indexX, hand.indexY);
            }

            return new Vector2(
                (hand.landmarks[8].x + hand.landmarks[12].x) * 0.5f,
                (hand.landmarks[8].y + hand.landmarks[12].y) * 0.5f);
        }

        private bool TryGetPrimaryHand(out GestureHandFrame hand)
        {
            var frame = receiver != null && receiver.HasFreshFrame ? receiver.Latest : GestureFrame.Neutral;
            if (frame.hands != null && frame.hands.Length > 0)
            {
                hand = frame.hands[0];
                var bestScore = PrimaryHandScore(hand);
                for (var i = 1; i < frame.hands.Length; i++)
                {
                    var candidateScore = PrimaryHandScore(frame.hands[i]);
                    if (candidateScore > bestScore)
                    {
                        hand = frame.hands[i];
                        bestScore = candidateScore;
                    }
                }
                return true;
            }

            if (frame.confidence > 0f)
            {
                hand = new GestureHandFrame
                {
                    id = "primary",
                    handedness = "Unknown",
                    score = frame.confidence,
                    pinchX = frame.pinchX,
                    pinchY = frame.pinchY,
                    indexX = frame.indexX,
                    indexY = frame.indexY,
                    pinchDistance = frame.pinchDistance,
                    palmSpan = frame.palmSpan,
                    palmRoll = frame.palmRoll,
                    palmPitch = frame.palmPitch,
                    palmYaw = frame.palmYaw,
                    pinch = frame.pinch,
                    openPalm = frame.openPalm,
                };
                return true;
            }

            hand = default;
            return false;
        }

        private float PrimaryHandScore(GestureHandFrame hand)
        {
            var score = hand.score;
            if (IsPinching(hand))
            {
                score += 0.45f;
            }
            if (hand.indexExtended)
            {
                score += 0.25f;
            }
            if (!hand.middleExtended && !hand.ringExtended && !hand.pinkyExtended)
            {
                score += 0.10f;
            }
            return score;
        }

        private bool TryGetTwoPinchingHands(out GestureHandFrame a, out GestureHandFrame b)
        {
            var frame = receiver != null && receiver.HasFreshFrame ? receiver.Latest : GestureFrame.Neutral;
            a = default;
            b = default;
            if (frame.hands == null || frame.hands.Length < 2)
            {
                return false;
            }

            a = frame.hands[0];
            b = frame.hands[1];
            return IsPinching(a) && IsPinching(b);
        }

        private List<UiPointer> GetUiPointers()
        {
            var pointers = new List<UiPointer>(2);
            var frame = receiver != null && receiver.HasFreshFrame ? receiver.Latest : GestureFrame.Neutral;
            if (frame.hands != null && frame.hands.Length > 0)
            {
                for (var i = 0; i < frame.hands.Length; i++)
                {
                    var hand = frame.hands[i];
                    if (hand.score < 0.25f || !hand.indexExtended)
                    {
                        continue;
                    }

                    var id = !string.IsNullOrEmpty(hand.id) ? hand.id : (!string.IsNullOrEmpty(hand.handedness) ? hand.handedness : $"hand-{i}");
                    pointers.Add(new UiPointer(id, new Vector2(hand.indexX * Screen.width, hand.indexY * Screen.height), IsPinching(hand)));
                }
            }
            else if (TryGetPrimaryHand(out var hand))
            {
                pointers.Add(new UiPointer("primary", new Vector2(hand.indexX * Screen.width, hand.indexY * Screen.height), IsPinching(hand)));
            }
            return pointers;
        }

        private void DrawCursor()
        {
            var pointers = GetUiPointers();
            foreach (var pointer in pointers)
            {
                var pos = pointer.Position;
                var rect = new Rect(pos.x - 14f, pos.y - 14f, 28f, 28f);
                GUI.color = pointer.Pinching ? Color.cyan : Color.white;
                GUI.Box(rect, pointer.Pinching ? "P" : "");
            }
            GUI.color = Color.white;
        }

        private void DrawHoverButton(string key, string label, Rect rect, float dwellSeconds, System.Action action, int fontSize = 0)
        {
            DrawButtonCore(key, label, rect, dwellSeconds, action, false, fontSize);
        }

        private void DrawUtilityButton(string key, string label, Rect rect, float dwellSeconds, System.Action action)
        {
            DrawButtonCore(key, label, rect, dwellSeconds, action, true, 0);
        }

        private void DrawButtonCore(string key, string label, Rect rect, float dwellSeconds, System.Action action, bool allowMouseClick, int fontSize)
        {
            var pointers = GetUiPointers();
            var activeHoverIds = new List<string>(pointers.Count);
            var progress = 0f;
            foreach (var pointer in pointers)
            {
                var hoverId = $"{key}|{pointer.Id}";
                if (rect.Contains(pointer.Position))
                {
                    activeHoverIds.Add(hoverId);
                    if (!hoverStartsByPointer.ContainsKey(hoverId))
                    {
                        hoverStartsByPointer[hoverId] = Time.time;
                    }
                    progress = Mathf.Max(progress, Mathf.Clamp01((Time.time - hoverStartsByPointer[hoverId]) / dwellSeconds));
                }
            }

            var prefix = key + "|";
            var staleHoverIds = new List<string>();
            foreach (var entry in hoverStartsByPointer)
            {
                if (entry.Key.StartsWith(prefix) && !activeHoverIds.Contains(entry.Key))
                {
                    staleHoverIds.Add(entry.Key);
                }
            }
            foreach (var hoverId in staleHoverIds)
            {
                hoverStartsByPointer.Remove(hoverId);
            }

            var style = fontSize > 0
                ? new GUIStyle(GUI.skin.button)
                {
                    fontSize = fontSize,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                }
                : GUI.skin.button;
            var clicked = allowMouseClick ? GUI.Button(rect, label, style) : false;
            if (!allowMouseClick)
            {
                GUI.Box(rect, label, style);
            }
            var bar = new Rect(rect.x, rect.yMax - 6f, rect.width * progress, 6f);
            GUI.color = Color.cyan;
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = Color.white;
            if (clicked || progress >= 1f)
            {
                RemoveHoverStartsForKey(key);
                action();
            }
        }

        private void RemoveHoverStartsForKey(string key)
        {
            var prefix = key + "|";
            var removeIds = new List<string>();
            foreach (var entry in hoverStartsByPointer)
            {
                if (entry.Key.StartsWith(prefix))
                {
                    removeIds.Add(entry.Key);
                }
            }
            foreach (var id in removeIds)
            {
                hoverStartsByPointer.Remove(id);
            }
        }

        private static void DrawPanel(Rect rect)
        {
            GUI.Box(rect, "");
        }

        private void DrawHandSkeletonOverlay()
        {
            var frame = receiver != null && receiver.HasFreshFrame ? receiver.Latest : GestureFrame.Neutral;
            if (frame.hands == null || frame.hands.Length == 0)
            {
                skeletonScreenCache.Clear();
                var message = launchedBridge
                    ? "Waiting for camera hand tracking..."
                    : bridgeStatus;
                GUI.Label(new Rect(Screen.width / 2f - 170f, Screen.height - 48f, 340f, 24f), message);
                return;
            }

            foreach (var hand in frame.hands)
            {
                var hasRawDisplayLandmarks = hand.displayLandmarks != null && hand.displayLandmarks.Length >= 21;
                var skeletonLandmarks = hasRawDisplayLandmarks ? hand.displayLandmarks : hand.landmarks;
                if (skeletonLandmarks == null || skeletonLandmarks.Length < 21)
                {
                    continue;
                }
                var screenPoints = hasRawDisplayLandmarks
                    ? LandmarksToScreenPoints(skeletonLandmarks)
                    : GuardedLandmarksToScreenPoints(hand, skeletonLandmarks);

                var baseColor = string.Equals(hand.handedness, "Left", System.StringComparison.OrdinalIgnoreCase)
                    ? new Color(0.20f, 0.74f, 1f, 0.96f)
                    : new Color(1f, 0.72f, 0.20f, 0.96f);
                var pinchColor = IsPinching(hand) ? new Color(0.12f, 1f, 0.85f, 1f) : baseColor;
                for (var i = 0; i < HandConnectionPairs.Length; i += 2)
                {
                    var a = screenPoints[HandConnectionPairs[i]];
                    var b = screenPoints[HandConnectionPairs[i + 1]];
                    DrawLine(a, b, pinchColor, 3f);
                }

                for (var i = 0; i < screenPoints.Length; i++)
                {
                    var point = screenPoints[i];
                    var radius = i == 4 || i == 8 ? 5f : 3.5f;
                    GUI.color = i == 4 || i == 8 ? Color.cyan : baseColor;
                    GUI.DrawTexture(new Rect(point.x - radius, point.y - radius, radius * 2f, radius * 2f), Texture2D.whiteTexture);
                }
                var label = $"{hand.handedness} {hand.score:0.00}";
                var labelPoint = screenPoints[0];
                GUI.color = baseColor;
                GUI.Label(new Rect(labelPoint.x + 8f, labelPoint.y - 10f, 110f, 22f), label);
                GUI.color = Color.white;
            }
        }

        private static Vector2[] LandmarksToScreenPoints(GestureLandmark[] landmarks)
        {
            var points = new Vector2[landmarks.Length];
            for (var i = 0; i < landmarks.Length; i++)
            {
                points[i] = LandmarkToScreen(landmarks[i]);
            }
            return points;
        }

        private Vector2[] GuardedLandmarksToScreenPoints(GestureHandFrame hand, GestureLandmark[] landmarks)
        {
            var key = !string.IsNullOrEmpty(hand.id) ? hand.id : (!string.IsNullOrEmpty(hand.handedness) ? hand.handedness : "primary");
            if (!skeletonScreenCache.TryGetValue(key, out var cached) || cached.Length != landmarks.Length)
            {
                cached = new Vector2[landmarks.Length];
                for (var i = 0; i < landmarks.Length; i++)
                {
                    cached[i] = LandmarkToScreen(landmarks[i]);
                }
                skeletonScreenCache[key] = cached;
                return cached;
            }

            var currentPoints = new Vector2[landmarks.Length];
            var currentCentroid = Vector2.zero;
            var cachedCentroid = Vector2.zero;
            for (var i = 0; i < landmarks.Length; i++)
            {
                currentPoints[i] = LandmarkToScreen(landmarks[i]);
                currentCentroid += currentPoints[i];
                cachedCentroid += cached[i];
            }

            currentCentroid /= landmarks.Length;
            cachedCentroid /= landmarks.Length;
            var centroidDelta = currentCentroid - cachedCentroid;
            var centroidDistance = centroidDelta.magnitude;
            var jumpGuardPixels = Mathf.Max(180f, Mathf.Min(Screen.width, Screen.height) * 0.16f);
            var guardedCentroid = currentCentroid;
            if (centroidDistance > jumpGuardPixels)
            {
                guardedCentroid = cachedCentroid + centroidDelta.normalized * jumpGuardPixels;
            }

            var centroidOffset = guardedCentroid - currentCentroid;
            for (var i = 0; i < currentPoints.Length; i++)
            {
                cached[i] = currentPoints[i] + centroidOffset;
            }
            return cached;
        }

        private static Vector2 LandmarkToScreen(GestureLandmark landmark)
        {
            return new Vector2(landmark.x * Screen.width, landmark.y * Screen.height);
        }

        private void DrawLine(Vector2 start, Vector2 end, Color color, float width)
        {
            if (Event.current != null && Event.current.type != EventType.Repaint)
            {
                return;
            }

            lineTexture ??= Texture2D.whiteTexture;
            var delta = end - start;
            var angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            var oldColor = GUI.color;
            var oldMatrix = GUI.matrix;
            GUI.color = color;
            GUIUtility.RotateAroundPivot(angle, start);
            GUI.DrawTexture(new Rect(start.x, start.y - width * 0.5f, delta.magnitude, width), lineTexture);
            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
        }

        private static void DrawProgressBar(float progress, Rect rect)
        {
            GUI.Box(rect, "");
            GUI.color = Color.cyan;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * progress, rect.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void StartGestureBridgeIfNeeded()
        {
            if (Application.isBatchMode)
            {
                bridgeStatus = "Camera bridge disabled in batch build.";
                return;
            }

            usesExternalBridge = HasCommandLineFlag("--gesture-bridge-external");
            if (usesExternalBridge)
            {
                launchedBridge = true;
                bridgeStatus = "Camera bridge started by launcher; waiting for hand...";
                return;
            }

            if (launchedBridge)
            {
                return;
            }

            launchedBridge = true;
            var bridgeDirectory = FindBridgeDirectory();
            if (string.IsNullOrEmpty(bridgeDirectory))
            {
                bridgeStatus = "Camera bridge not found. Use Play-HandOfGod.bat.";
                Debug.LogWarning(bridgeStatus);
                return;
            }

            StartHiddenGestureBridge(bridgeDirectory);
        }

        private void StartHiddenGestureBridge(string bridgeDirectory)
        {
            if (bridgeProcess != null && !bridgeProcess.HasExited)
            {
                bridgeStatus = "Camera bridge is already running.";
                return;
            }

            var scriptPath = Path.Combine(bridgeDirectory, "mediapipe_udp_sender.py");
            var python = ResolveGesturePython(bridgeDirectory);
            var logPath = Path.Combine(bridgeDirectory, "gesture-bridge-runtime.log");
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = python,
                    Arguments = $"\"{scriptPath}\" --no-preview",
                    WorkingDirectory = bridgeDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                };
                launchedBridge = true;
                bridgeStatus = "Starting camera...";
                bridgeProcess = Process.Start(startInfo);
                if (bridgeProcess != null)
                {
                    bridgeProcess.OutputDataReceived += (_, args) => AppendBridgeLog(logPath, args.Data);
                    bridgeProcess.ErrorDataReceived += (_, args) => AppendBridgeLog(logPath, args.Data);
                    bridgeProcess.BeginOutputReadLine();
                    bridgeProcess.BeginErrorReadLine();
                }
                bridgeStatus = "Camera bridge launched; waiting for hand...";
            }
            catch (System.Exception exception)
            {
                bridgeStatus = "Failed to launch camera bridge.";
                Debug.LogWarning($"Failed to launch gesture bridge: {exception.Message}");
            }
        }

        private void StartVisibleGestureBridge()
        {
            if (bridgeProcess != null && !bridgeProcess.HasExited)
            {
                bridgeStatus = "Camera bridge is already running.";
                return;
            }

            var bridgeDirectory = FindBridgeDirectory();
            if (string.IsNullOrEmpty(bridgeDirectory))
            {
                bridgeStatus = "Camera bridge not found. Start with Play-HandOfGod.bat.";
                return;
            }

            var scriptPath = Path.Combine(bridgeDirectory, "mediapipe_udp_sender.py");
            var python = ResolveGesturePython(bridgeDirectory);
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = python,
                    Arguments = $"\"{scriptPath}\" --no-preview",
                    WorkingDirectory = bridgeDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                };
                var logPath = Path.Combine(bridgeDirectory, "gesture-bridge-runtime.log");
                launchedBridge = true;
                bridgeStatus = "Starting camera...";
                bridgeProcess = Process.Start(startInfo);
                if (bridgeProcess != null)
                {
                    bridgeProcess.OutputDataReceived += (_, args) => AppendBridgeLog(logPath, args.Data);
                    bridgeProcess.ErrorDataReceived += (_, args) => AppendBridgeLog(logPath, args.Data);
                    bridgeProcess.BeginOutputReadLine();
                    bridgeProcess.BeginErrorReadLine();
                }
                usesExternalBridge = false;
                bridgeStatus = "Camera bridge started; waiting for image and hand...";
            }
            catch (System.Exception exception)
            {
                bridgeStatus = "Failed to start camera bridge.";
                Debug.LogWarning($"Failed to start gesture bridge: {exception.Message}");
            }
        }

        private static string FindBridgeDirectory()
        {
            var cliDirectory = GetCommandLineValue("--gesture-bridge-dir");
            if (!string.IsNullOrEmpty(cliDirectory) && File.Exists(Path.Combine(cliDirectory, "mediapipe_udp_sender.py")))
            {
                return cliDirectory;
            }

            var candidates = new System.Collections.Generic.List<string>
            {
                Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "gesture_bridge")),
                Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "..", "gesture_bridge")),
                Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "gesture_bridge")),
                Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "..", "unity", "gesture_bridge")),
                Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "..", "..", "unity", "gesture_bridge")),
            };

            var cursor = new DirectoryInfo(Application.dataPath);
            for (var i = 0; i < 8 && cursor != null; i++)
            {
                candidates.Add(Path.Combine(cursor.FullName, "gesture_bridge"));
                candidates.Add(Path.Combine(cursor.FullName, "unity", "gesture_bridge"));
                cursor = cursor.Parent;
            }

            foreach (var candidate in candidates)
            {
                if (File.Exists(Path.Combine(candidate, "mediapipe_udp_sender.py")))
                {
                    return candidate;
                }
            }
            return "";
        }

        private static string ResolveGesturePython(string bridgeDirectory)
        {
            var cliPython = GetCommandLineValue("--gesture-python");
            if (!string.IsNullOrEmpty(cliPython) && File.Exists(cliPython))
            {
                return cliPython;
            }

            var asciiRuntimePython = @"E:\Unity\HandOfGodGestureBridge\.venv\Scripts\python.exe";
            if (File.Exists(asciiRuntimePython))
            {
                return asciiRuntimePython;
            }

            var projectVenvPython = Path.Combine(bridgeDirectory, ".venv", "Scripts", "python.exe");
            return File.Exists(projectVenvPython) ? projectVenvPython : "python";
        }

        private static bool HasCommandLineFlag(string flag)
        {
            foreach (var argument in System.Environment.GetCommandLineArgs())
            {
                if (string.Equals(argument, flag, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static string GetCommandLineValue(string key)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], key, System.StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }
            return "";
        }

        private static void AppendBridgeLog(string logPath, string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return;
            }

            try
            {
                File.AppendAllText(logPath, $"[{System.DateTime.Now:HH:mm:ss}] {line}{System.Environment.NewLine}");
            }
            catch (System.Exception)
            {
                // Logging must not break gameplay.
            }
        }

        private void QuitGame()
        {
            if (Application.isEditor)
            {
                Debug.Log("Quit requested.");
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#endif
                return;
            }
            Application.Quit();
        }

        private void OnApplicationQuit()
        {
            StopBridgeProcess();
        }

        private void OnDisable()
        {
            StopBridgeProcess();
        }

        private void OnDestroy()
        {
            StopBridgeProcess();
        }

        private void StopBridgeProcess()
        {
            if (bridgeProcess == null)
            {
                return;
            }

            try
            {
                if (!bridgeProcess.HasExited)
                {
                    if (Application.isEditor)
                    {
                        StopBridgeProcessAsync(bridgeProcess);
                        bridgeProcess = null;
                        launchedBridge = false;
                        usesExternalBridge = false;
                        return;
                    }
                    KillProcessTree(bridgeProcess.Id);
                    if (!bridgeProcess.WaitForExit(1200))
                    {
                        bridgeProcess.Kill();
                        bridgeProcess.WaitForExit(500);
                    }
                }
            }
            catch (System.Exception)
            {
                // Process may already be gone during application shutdown.
            }
            finally
            {
                bridgeProcess.Dispose();
                bridgeProcess = null;
                launchedBridge = false;
            }
        }

        private void StopBridgeProcessAsync(Process process)
        {
            if (process == null || stoppingBridge)
            {
                return;
            }

            stoppingBridge = true;
            var processId = process.Id;
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        KillProcessTree(processId);
                    }
                    process.Dispose();
                }
                catch (System.Exception)
                {
                    // Editor shutdown should never be blocked by camera bridge cleanup.
                }
                finally
                {
                    stoppingBridge = false;
                }
            });
        }

        private static void KillProcessTree(int processId)
        {
            if (processId <= 0)
            {
                return;
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "taskkill",
                    Arguments = $"/PID {processId} /T /F",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                };
                using var killer = Process.Start(startInfo);
                killer?.WaitForExit(1000);
            }
            catch (System.Exception)
            {
                // Fall back to Process.Kill in the caller.
            }
        }

        private Vector3 ScreenToWorldPlane(float normalizedX, float normalizedY, float y)
        {
            var ray = mainCamera.ScreenPointToRay(new Vector3(normalizedX * Screen.width, (1f - normalizedY) * Screen.height, 0f));
            var plane = new Plane(Vector3.up, new Vector3(0f, y, 0f));
            return plane.Raycast(ray, out var distance) ? ray.GetPoint(distance) : new Vector3(0f, y, 0f);
        }

        private void ClearLevel()
        {
            ResetLevel2RuntimeReferences();
            if (levelRoot != null)
            {
                DestroyUnityObject(levelRoot.gameObject);
            }
            levelRoot = null;
            labObject = null;
            labBody = null;
            labRenderer = null;
            tutorialBridgeLeft = null;
            tutorialBridgeRight = null;
            tutorialBridgeRoot = null;
            tutorialSealRoot = null;
            tutorialAirflowRoot = null;
            tutorialMirrorRoot = null;
            tutorialMirrorProp = null;
            tutorialMirrorReflectedBeam = null;
            tutorialMagnetRoot = null;
            tutorialMagnetDisk = null;
            tutorialBridgeLeftRenderer = null;
            tutorialBridgeRightRenderer = null;
            tutorialSealRenderer = null;
            tutorialAirflowPadRenderer = null;
            tutorialMirrorRenderer = null;
            tutorialMagnetRenderer = null;
            tutorialAirflowArrow = null;
            tutorialStageSucceeded = false;
            tutorialObjectMoved = false;
            tutorialObjectRotated = false;
            tutorialBridgePulled = false;
            tutorialPalmActivated = false;
            tutorialMapAdjusted = false;
            tutorialAirflowDirected = false;
            tutorialDrawCreated = false;
            tutorialDrawErased = false;
            tutorialDrawInvalid = false;
            tutorialMirrorRotated = false;
            tutorialMagnetPolarityChanged = false;
            tutorialAirflowPreviewDirection = 0;
            tutorialMagnetPreviewPolarity = 0;
            tutorialMagnetReversalCount = 0;
            ResetTutorialMagnetFlipGesture();
            tutorialDrawState = TutorialDrawState.WaitingStart;
            tutorialDrawHoldStart = -1f;
            tutorialDrawSawFingerSeparation = false;
            tutorialDrawMessage = "";
            tutorialDrawMessageUntil = -1f;
            tutorialDrawScreenPoints.Clear();
            tutorialDrawWorldPoints.Clear();
            tutorialDrawnObjects.Clear();
            tutorialDrawHeldObject = null;
            tutorialDrawGrabOffset = Vector3.zero;
            tutorialDrawObjectHeld = false;
            tutorialDrawRotatingObject = null;
            tutorialDrawRotateStartDistance = 0f;
            tutorialDrawRotateStartAngle = 0f;
            tutorialDrawRotateStartRotation = Quaternion.identity;
            tutorialDrawnRoot = null;
            tutorialEraseTarget = null;
            tutorialEraseRenderer = null;
            tutorialEraseIdleMaterial = null;
            obstacleBox = null;
            obstacleRenderer = null;
            blockSlotRenderer = null;
            bridgeLeft = null;
            bridgeRight = null;
            bridgeLeftRenderer = null;
            bridgeRightRenderer = null;
            rotateGate = null;
            rotateGateRenderer = null;
            sealRenderer = null;
            levelBall = null;
            labHeld = false;
            boxHeld = false;
            bridgeLocked = false;
            rotateGateLocked = false;
            sealActivated = false;
            rotateGateHeld = false;
            level1RotateRequiresPinchReset = false;
            startGate = null;
            bridgeGate = null;
            rotateGateStop = null;
            goalGate = null;
            level1BridgeStartDistance = 0f;
            sealHoldStart = -1f;
            level1SuccessUntil = -1f;
            level1BoxGrabOffset = Vector3.zero;
            twoHandStartDistance = 0f;
            level3Stage = Level3Stage.CreateBridgeObject;
            level3CubePlate = null;
            level3SpherePlate = null;
            level3CubePlateRenderer = null;
            level3SpherePlateRenderer = null;
            level3BridgePatch = null;
            level3BridgePatchRenderer = null;
            level3LockBlock = null;
            level3LockBlockHalo = null;
            level3LockBlockRenderer = null;
            level3SlideBridge = null;
            level3SlideBridgeRenderer = null;
            level3SlideBridgeCore = null;
            level3SlideBridgeReleaseStart = -1f;
            level3CubePlaced = false;
            level3SpherePlaced = false;
            level3BridgePlaced = false;
            level3SlideBridgeReleased = false;
            level3EraseRequiresGestureReset = false;
            level3HintMessage = "";
        }

        private void ResetLevel2RuntimeReferences()
        {
            if (airBeltTriggers != null)
            {
                foreach (var trigger in airBeltTriggers)
                {
                    if (trigger != null)
                    {
                        trigger.ResetWindState();
                        trigger.direction = 0;
                    }
                }
            }

            if (airBeltParticles != null)
            {
                foreach (var particleSystem in airBeltParticles)
                {
                    if (particleSystem != null)
                    {
                        particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    }
                }
            }

            portalKey = null;
            portalKeyBody = null;
            portalKeyRenderer = null;
            portalKeyRenderers = null;
            portalKeyIdleMaterials = null;
            runeLeft = null;
            runeRight = null;
            runeLeftRenderer = null;
            runeRightRenderer = null;
            runeLeftArrow = null;
            runeRightArrow = null;
            portalAActive = false;
            portalBActive = false;
            portalARenderer = null;
            portalBRenderer = null;
            airBelts = null;
            airBeltDirection = null;
            airBeltRenderers = null;
            airBeltTriggers = null;
            airBeltArrowTransforms = null;
            airBeltArrowRenderers = null;
            airBeltStreaks = null;
            airBeltStreakRenderers = null;
            airBeltMistQuads = null;
            airBeltMistRenderers = null;
            airBeltParticles = null;
            portalAParticles = null;
            portalBParticles = null;
            levelBallBody = null;
            levelBallRenderer = null;
            levelBallRuntimeMaterial = null;
            levelBallBaseScale = Vector3.one;
            keyHeld = false;
            keyGrabOffset = Vector3.zero;
            level2LastTeleport = -10f;
            level2Teleporting = false;
            level2TeleportStart = 0f;
            level2TeleportStartPosition = Vector3.zero;
            level2TeleportEndPosition = Vector3.zero;
            pendingAirDirection = 0;
            pendingAirDirectionStart = -1f;
            level2HintMessage = "";
            lastPinchState = false;
            lastKeyInRange = false;
            keyHoverStart = -1f;
            level4Mirror = null;
            level4MirrorRenderer = null;
            level4LightGate = null;
            level4DoorRenderer = null;
            level4BeamSegments = null;
            level4MagnetTurntable = null;
            level4MagnetRenderer = null;
            level4MagnetNeedle = null;
            level4MagnetNeedleRenderer = null;
            level4MagnetGate = null;
            level4MagnetBackstop = null;
            level4MagnetPolarity = 0;
            level4RotateStartAngle = 0f;
            level4MirrorStartYaw = 0f;
            level4MirrorHeld = false;
            level4LightSolved = false;
            level4BackstopRaised = false;
            ResetLevel4MagnetFlipGesture();
        }

        private static void DestroyNamed(string objectName)
        {
            var existing = GameObject.Find(objectName);
            if (existing != null)
            {
                DestroyUnityObject(existing);
            }
        }

        private void BuildMaterials()
        {
            stone = NewMaterial("Basalt green stone", new Color(0.34f, 0.43f, 0.39f), 0.38f, 0f);
            paleStone = NewMaterial("Worn top stone", new Color(0.52f, 0.60f, 0.55f), 0.3f, 0f);
            darkStone = NewMaterial("Obsidian void table", new Color(0.028f, 0.043f, 0.039f), 0.55f, 0.01f);
            cliffStone = NewMaterial("Temple cliff side", new Color(0.20f, 0.27f, 0.24f), 0.25f, 0f);
            brass = NewMaterial("Aged brass inlay", new Color(0.74f, 0.55f, 0.22f), 0.48f, 0.02f);
            tealGlow = NewMaterial("Teal sacred glow", new Color(0.13f, 0.82f, 0.72f), 0.35f, 0.62f);
            amberGlow = NewMaterial("Amber brazier glow", new Color(1f, 0.35f, 0.08f), 0.2f, 0.85f);
            ballMaterial = NewMaterial("Golden physics ball", new Color(1f, 0.67f, 0.06f), 0.65f, 0.1f);
            boxIdle = NewMaterial("Movable cedar box", new Color(0.58f, 0.34f, 0.18f), 0.28f, 0f);
            boxHover = NewMaterial("Movable box hover", new Color(0.88f, 0.55f, 0.24f), 0.35f, 0.15f);
            boxHeldMaterial = NewMaterial("Movable box held glow", new Color(0.18f, 0.95f, 0.78f), 0.35f, 0.65f);
            level2FloorMaterial = NewMaterial("Level2 carved slate floor", new Color(0.21f, 0.25f, 0.27f), 0.42f, 0.02f);
            level2WallMaterial = NewMaterial("Level2 blue gray dungeon wall", new Color(0.30f, 0.36f, 0.38f), 0.34f, 0.01f);
            level2TrimMaterial = NewMaterial("Level2 worn brass trim", new Color(0.82f, 0.62f, 0.30f), 0.58f, 0.08f);
            level2PortalCoreMaterial = NewMaterial("Level2 cyan portal core", new Color(0.10f, 0.96f, 1f), 0.18f, 0.95f);
            level2WindMaterial = NewParticleMaterial("Level2 translucent wind", new Color(0.38f, 0.95f, 1f, 0.58f));
            level2WindRibbonMaterial = NewParticleMaterial("Kenney wind trace material", new Color(0.38f, 0.95f, 1f, 0.78f), Resources.Load<Texture2D>("KenneyParticles/wind_trace"));
            level2WindMistMaterial = NewParticleMaterial("Kenney wind mist material", new Color(0.38f, 0.95f, 1f, 0.32f), Resources.Load<Texture2D>("KenneyParticles/soft_smoke"));
            level2PortalTwirlMaterial = NewParticleMaterial("Kenney portal twirl material", new Color(0.10f, 0.96f, 1f, 0.68f), Resources.Load<Texture2D>("KenneyParticles/portal_twirl"));
            level2RuneMaterial = NewMaterial("Level2 active rune gold", new Color(1f, 0.73f, 0.20f), 0.36f, 0.65f);
            level4BeamAmberMaterial = NewParticleMaterial("Level4 amber light beam core", new Color(1f, 0.58f, 0.12f, 0.82f), Resources.Load<Texture2D>("KenneyParticles/wind_trace"));
            level4BeamTealMaterial = NewParticleMaterial("Level4 teal light beam core", new Color(0.18f, 1f, 0.92f, 0.86f), Resources.Load<Texture2D>("KenneyParticles/wind_trace"));
            level4BeamHaloAmberMaterial = NewParticleMaterial("Level4 amber light beam halo", new Color(1f, 0.42f, 0.10f, 0.28f), Resources.Load<Texture2D>("KenneyParticles/soft_smoke"));
            level4BeamHaloTealMaterial = NewParticleMaterial("Level4 teal light beam halo", new Color(0.16f, 1f, 0.90f, 0.32f), Resources.Load<Texture2D>("KenneyParticles/soft_smoke"));
        }

        private static Material NewMaterial(string name, Color color, float smoothness, float emission)
        {
            var material = new Material(Shader.Find("Standard")) { name = name, color = color };
            material.SetFloat("_Glossiness", smoothness);
            if (emission > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * emission);
            }
            return material;
        }

        private static Material NewParticleMaterial(string name, Color color)
        {
            return NewParticleMaterial(name, color, null);
        }

        private static Material NewParticleMaterial(string name, Color color, Texture texture)
        {
            var shader = texture != null ? Shader.Find("Legacy Shaders/Particles/Alpha Blended") : Shader.Find("Particles/Standard Unlit");
            shader ??= texture != null ? Shader.Find("Unlit/Transparent") : null;
            shader ??= Shader.Find("Legacy Shaders/Particles/Additive");
            shader ??= Shader.Find("Standard");

            var material = new Material(shader) { name = name, color = color };
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_TintColor")) material.SetColor("_TintColor", color);
            if (texture != null)
            {
                material.mainTexture = texture;
                if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            return material;
        }

        private void BuildCameraAndLights()
        {
            var cameraObject = new GameObject("Main Camera");
            mainCamera = cameraObject.AddComponent<Camera>();
            mainCamera.tag = "MainCamera";
            mainCamera.transform.SetPositionAndRotation(new Vector3(0f, 7.9f, -5.2f), Quaternion.Euler(58f, 0f, 0f));
            mainCamera.orthographic = true;
            mainCamera.orthographicSize = 4.35f;
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = new Color(0.012f, 0.017f, 0.016f);
            BuildCameraBackgroundPlane(cameraObject.transform);

            var sun = new GameObject("Key Light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.12f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(46f, -34f, 22f);

            var fill = new GameObject("Temple Fill Light").AddComponent<Light>();
            fill.type = LightType.Point;
            fill.intensity = 1.4f;
            fill.range = 9f;
            fill.transform.position = new Vector3(-1.2f, 4.4f, -1.6f);
        }

        private void BuildCameraBackgroundPlane(Transform cameraTransform)
        {
            var plane = GameObject.CreatePrimitive(PrimitiveType.Quad);
            plane.name = "Embedded Camera Feed Background";
            plane.transform.SetParent(cameraTransform, false);
            plane.transform.localPosition = new Vector3(0f, 0f, 30f);
            plane.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            DestroyUnityObject(plane.GetComponent<Collider>());

            cameraBackgroundMaterial = new Material(Shader.Find("Unlit/Texture"))
            {
                name = "Embedded Camera Feed Material",
            };
            plane.GetComponent<Renderer>().sharedMaterial = cameraBackgroundMaterial;
            cameraBackgroundPlane = plane.transform;
            UpdateCameraBackground();
        }

        private void UpdateCameraBackground()
        {
            if (cameraBackgroundPlane == null || mainCamera == null)
            {
                return;
            }

            var height = mainCamera.orthographicSize * 2f;
            var width = height * Mathf.Max(Screen.width / (float)Mathf.Max(Screen.height, 1), 0.01f);
            cameraBackgroundPlane.localScale = new Vector3(width, height, 1f);

            if (cameraBackgroundMaterial != null && cameraFrames != null && cameraFrames.HasFreshFrame && cameraFrames.Texture != null)
            {
                cameraBackgroundMaterial.mainTexture = cameraFrames.Texture;
                cameraBackgroundMaterial.color = Color.white;
            }
        }

        private GameObject CreateBox(string name, Vector3 position, Vector3 scale, Material material, Transform parent, Quaternion rotation, bool keepCollider)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.position = position;
            box.transform.rotation = rotation;
            box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = material;
            if (!keepCollider)
            {
                DestroyUnityObject(box.GetComponent<Collider>());
            }
            return box;
        }

        private GameObject CreateTorus(string name, Vector3 position, float majorRadius, float minorRadius, Material material, Transform parent)
        {
            var mesh = new Mesh { name = name + " mesh" };
            const int majorSegments = 64;
            const int minorSegments = 10;
            var vertices = new Vector3[majorSegments * minorSegments];
            var triangles = new int[majorSegments * minorSegments * 6];
            for (var i = 0; i < majorSegments; i++)
            {
                var u = i / (float)majorSegments * Mathf.PI * 2f;
                for (var j = 0; j < minorSegments; j++)
                {
                    var v = j / (float)minorSegments * Mathf.PI * 2f;
                    var radius = majorRadius + minorRadius * Mathf.Cos(v);
                    vertices[i * minorSegments + j] = new Vector3(radius * Mathf.Cos(u), minorRadius * Mathf.Sin(v), radius * Mathf.Sin(u));
                }
            }
            var tri = 0;
            for (var i = 0; i < majorSegments; i++)
            {
                for (var j = 0; j < minorSegments; j++)
                {
                    var a = i * minorSegments + j;
                    var b = ((i + 1) % majorSegments) * minorSegments + j;
                    var c = ((i + 1) % majorSegments) * minorSegments + (j + 1) % minorSegments;
                    var d = i * minorSegments + (j + 1) % minorSegments;
                    triangles[tri++] = a; triangles[tri++] = b; triangles[tri++] = c;
                    triangles[tri++] = a; triangles[tri++] = c; triangles[tri++] = d;
                }
            }
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            var torus = new GameObject(name);
            torus.transform.SetParent(parent, false);
            torus.transform.position = position;
            torus.AddComponent<MeshFilter>().sharedMesh = mesh;
            torus.AddComponent<MeshRenderer>().sharedMaterial = material;
            return torus;
        }

        private static float RoadY(float x)
        {
            return Level1RoadCenterY + x * Mathf.Sin(Level1RoadAngleDegrees * Mathf.Deg2Rad);
        }

        private static float DistanceXZ(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private static void DestroyUnityObject(Object target)
        {
            if (target == null)
            {
                return;
            }
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
