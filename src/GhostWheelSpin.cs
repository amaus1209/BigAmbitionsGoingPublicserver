using System.Collections.Generic;
using GleyTrafficSystem;
using UnityEngine;

namespace BigAmbitionsMP
{
    /// <summary>WHEEL-SPIN-1 (user 2026-10-31: "the wheels of the cars driving past don't turn"): a traffic ghost is a
    /// stripped clone of the traffic prefab that the mod moves itself. Wheels are turned only by the traffic system's own
    /// job (Gley TrafficManager -> UpdateWheelJob) for its own pool cars, so a ghost's wheels stood still. This turns them
    /// from the ghost's own movement the way that job does: localRotation = Euler(roll, steer, 0) - the roll from the
    /// distance driven over the wheel's circumference, the front wheels' steer from the turn rate (bicycle model over the
    /// distance between the axles, capped at the car's max steer). Purely visual.</summary>
    internal sealed class GhostWheelSpin : MonoBehaviour
    {
        private Transform[] _wheels = System.Array.Empty<Transform>();
        private bool[] _front = System.Array.Empty<bool>();
        private float _radius = 0.35f, _wheelBase = 2.6f, _maxSteer = 30f;
        private float _roll, _steer, _lastYaw;
        private Vector3 _lastPos;
        private bool _has;

        /// <summary>Reads the wheels from the clone's VehicleComponent - BEFORE the strip removes it. False = nothing to turn.</summary>
        internal static bool Attach(GameObject clone)
        {
            try
            {
                var vc = clone.GetComponentInChildren<VehicleComponent>(true);
                if (vc == null || vc.allWheels == null || vc.allWheels.Length == 0) return false;
                var wheels = new List<Transform>();
                var front = new List<bool>();
                float r = 0f;
                foreach (var w in vc.allWheels)
                {
                    if (w == null || w.wheelGraphics == null) continue;
                    wheels.Add(w.wheelGraphics);
                    front.Add(w.wheelPosition == Wheel.WheelPosition.Front);
                    if (r <= 0f && w.wheelRadius > 0.05f) r = w.wheelRadius;
                }
                if (wheels.Count == 0) return false;
                var spin = clone.AddComponent<GhostWheelSpin>();
                spin._wheels = wheels.ToArray();
                spin._front = front.ToArray();
                if (r > 0f) spin._radius = r;
                if (vc.wheelDistance > 0.5f) spin._wheelBase = vc.wheelDistance;
                if (vc.maxSteer > 1f) spin._maxSteer = vc.maxSteer;
                return true;
            }
            catch { return false; }
        }

        private void LateUpdate()
        {
            var t = transform;
            Vector3 pos = t.position;
            float yaw = t.eulerAngles.y, dt = Time.deltaTime;
            if (!_has) { _has = true; _lastPos = pos; _lastYaw = yaw; return; }
            Vector3 d = pos - _lastPos;
            float dYaw = Mathf.DeltaAngle(_lastYaw, yaw);
            _lastPos = pos;
            _lastYaw = yaw;
            if (d.sqrMagnitude > 100f) return;   // a snap (respawn, rewind): no spin
            float along = Vector3.Dot(d, t.forward);
            _roll = Mathf.Repeat(_roll + along / (2f * Mathf.PI * _radius) * 360f, 360f);
            float steer = 0f;
            if (dt > 0f)
            {
                float v = along / dt;                    // m/s, negative when reversing
                float w = dYaw * Mathf.Deg2Rad / dt;     // rad/s
                if (Mathf.Abs(v) > 0.5f) steer = Mathf.Clamp(Mathf.Atan(w * _wheelBase / v) * Mathf.Rad2Deg, -_maxSteer, _maxSteer);
            }
            _steer = Mathf.Lerp(_steer, steer, Mathf.Min(1f, dt * 6f));
            for (int i = 0; i < _wheels.Length; i++)
            {
                var wt = _wheels[i];
                if (wt != null) wt.localRotation = Quaternion.Euler(_roll, _front[i] ? _steer : 0f, 0f);
            }
        }
    }
}
