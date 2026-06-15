import importlib.util
import math
import pathlib
import types
import unittest


BRIDGE_PATH = pathlib.Path(__file__).resolve().parents[1] / "mediapipe_udp_sender.py"
SPEC = importlib.util.spec_from_file_location("mediapipe_udp_sender", BRIDGE_PATH)
bridge = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(bridge)


def landmark(x, y, z=0.0):
    return types.SimpleNamespace(x=x, y=y, z=z)


def make_hand():
    points = [landmark(0.45, 0.50) for _ in range(21)]
    points[0] = landmark(0.45, 0.70)
    points[1] = landmark(0.40, 0.54)
    points[2] = landmark(0.36, 0.47)
    points[3] = landmark(0.33, 0.42)
    points[4] = landmark(0.30, 0.38)
    points[5] = landmark(0.45, 0.52)
    points[6] = landmark(0.45, 0.42)
    points[7] = landmark(0.45, 0.34)
    points[8] = landmark(0.45, 0.25)
    points[9] = landmark(0.50, 0.52)
    points[10] = landmark(0.50, 0.42)
    points[11] = landmark(0.50, 0.34)
    points[12] = landmark(0.50, 0.25)
    points[13] = landmark(0.53, 0.53)
    points[14] = landmark(0.53, 0.44)
    points[15] = landmark(0.53, 0.37)
    points[16] = landmark(0.53, 0.30)
    points[17] = landmark(0.56, 0.54)
    points[18] = landmark(0.56, 0.46)
    points[19] = landmark(0.56, 0.39)
    points[20] = landmark(0.56, 0.33)
    return points


def point(seq, index):
    item = seq[index]
    if isinstance(item, dict):
        return item["x"], item["y"], item["z"]
    return item.x, item.y, item.z


def segment_length(seq, a, b):
    return math.dist(point(seq, a), point(seq, b))


class ResponsiveFingerDisplayTests(unittest.TestCase):
    def test_lagging_inner_joints_follow_fast_tip_motion(self):
        base = make_hand()
        state = bridge.ResponsiveFingerDisplayHand(base)
        current = make_hand()
        current[8] = landmark(0.45, 0.37)
        current[6] = landmark(0.45, 0.41)
        current[7] = landmark(0.45, 0.335)

        output = state.update(current)

        self.assertGreater(output[6]["y"], current[6].y)
        self.assertGreater(output[7]["y"], current[7].y)
        self.assertAlmostEqual(output[8]["y"], current[8].y)

    def test_joint_response_does_not_stretch_finger_chain(self):
        base = make_hand()
        state = bridge.ResponsiveFingerDisplayHand(base)
        current = make_hand()
        current[8] = landmark(0.45, 0.37)
        current[6] = landmark(0.45, 0.41)
        current[7] = landmark(0.45, 0.335)

        output = state.update(current)
        raw_total = sum(segment_length(current, a, b) for a, b in ((5, 6), (6, 7), (7, 8)))
        output_total = sum(segment_length(output, a, b) for a, b in ((5, 6), (6, 7), (7, 8)))

        self.assertLessEqual(output_total, raw_total * 1.08)

    def test_large_palm_jump_resets_without_extrapolating_old_motion(self):
        base = make_hand()
        state = bridge.ResponsiveFingerDisplayHand(base)
        current = make_hand()
        for item in current:
            item.x += 0.35

        output = state.update(current)

        self.assertAlmostEqual(output[6]["x"], current[6].x)
        self.assertAlmostEqual(output[7]["x"], current[7].x)
        self.assertAlmostEqual(output[8]["x"], current[8].x)

    def test_static_micro_jitter_stays_inside_display_deadband(self):
        base = make_hand()
        state = bridge.ResponsiveFingerDisplayHand(base)
        jittered = make_hand()
        for index, item in enumerate(jittered):
            item.x += 0.00135 if index % 2 == 0 else -0.00135
            item.y += -0.00110 if index % 2 == 0 else 0.00110

        output = state.update(jittered)

        self.assertLess(math.dist(point(output, 8), point(base, 8)), 0.0002)
        self.assertLess(math.dist(point(output, 6), point(base, 6)), 0.0002)

    def test_near_threshold_jitter_moves_only_the_excess_distance(self):
        base = make_hand()
        state = bridge.ResponsiveFingerDisplayHand(base)
        jittered = make_hand()
        jittered[8] = landmark(base[8].x + 0.0032, base[8].y)

        output = state.update(jittered)

        self.assertLess(math.dist(point(output, 8), point(base, 8)), 0.00035)

    def test_interaction_analysis_uses_display_deadband_coordinates(self):
        base = make_hand()
        state = bridge.ResponsiveFingerDisplayHand(base)
        jittered = make_hand()
        jittered[8] = landmark(base[8].x + 0.0015, base[8].y - 0.0012)

        display_output = state.update(jittered)
        interaction_points = bridge.landmark_points_from_json(display_output)
        payload, _ = bridge.analyze_hand(interaction_points, "Right", 1.0, "Right", None, display_output)

        self.assertEqual(payload["landmarks"], display_output)
        self.assertAlmostEqual(payload["indexX"], display_output[8]["x"])
        self.assertAlmostEqual(payload["indexY"], display_output[8]["y"])
        self.assertLess(math.dist((payload["indexX"], payload["indexY"], 0.0), point(base, 8)), 0.0003)

    def test_real_finger_motion_escapes_display_deadband(self):
        base = make_hand()
        state = bridge.ResponsiveFingerDisplayHand(base)
        moved = make_hand()
        moved[8] = landmark(0.45, 0.29)
        moved[7] = landmark(0.45, 0.37)

        output = state.update(moved)

        self.assertGreater(output[8]["y"], base[8].y + 0.025)
        self.assertGreater(output[7]["y"], base[7].y + 0.018)

    def test_slow_real_motion_accumulates_past_display_deadband(self):
        base = make_hand()
        state = bridge.ResponsiveFingerDisplayHand(base)
        output = None

        for step in range(1, 11):
            moved = make_hand()
            moved[8] = landmark(0.45, base[8].y + step * 0.00070)
            moved[7] = landmark(0.45, base[7].y + step * 0.00060)
            output = state.update(moved)

        self.assertIsNotNone(output)
        self.assertGreater(output[8]["y"], base[8].y + 0.002)
        self.assertGreater(output[7]["y"], base[7].y + 0.0015)


if __name__ == "__main__":
    unittest.main()
