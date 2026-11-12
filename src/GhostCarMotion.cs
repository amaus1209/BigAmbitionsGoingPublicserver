using System.Collections.Generic;
using UnityEngine;

namespace BigAmbitionsMP
{
    /// <summary>PASSENGER-VIEW-1 (user 2026-11-01: "the passenger's wheels don't turn", "he doesn't see the steering",
    /// "the passenger doesn't hear the engine"): another player's car is a ghost on this machine - the prefab with its
    /// rigidbodies frozen and (unless a key was granted) its controllers stripped, moved only by the synced pose - so
    /// nothing of a moving car happens on it. This gives it back from the ghost's own movement, the way WHEEL-SPIN-1 does
    /// for the traffic: the wheels roll over their circumference, the front wheels steer from the turn rate (bicycle model
    /// over the wheelbase, capped at the car's max steer), and the engine runs - the car's own NWH engine clip through
    /// its mixer group, pitch and volume from the speed - while its driver sits in it (fleet "Driving") or it moves.
    /// The rotations are about the CAR's axes in each wheel's parent, so a mirrored wheel mesh rolls the right way.
    /// A granted ghost the local player drives himself is NWH's again (dynamic rigidbody): this stands back.</summary>
    internal sealed class GhostCarMotion : MonoBehaviour
    {
        private sealed class Wheel
        {
            public Transform T = null!;
            public Quaternion Base;
            public Vector3 Axle, Up;
            public bool Front;
        }

        private Wheel[] _wheels = System.Array.Empty<Wheel>();
        private float _radius = 0.35f, _wheelBase = 2.6f, _maxSteer = 30f;
        private float _roll, _steer, _lastYaw, _speed;
        private Vector3 _lastPos;
        private bool _has;
        private NWH.VehiclePhysics2.VehicleController? _nwh;   // stripped (Unity-null) on a plain ghost, kept on a granted one
        private AudioClip? _clip;
        private UnityEngine.Audio.AudioMixerGroup? _mixer;
        private AudioSource? _engine;
        private float _pitchOffset = 0.2f, _pitchRange = 2f, _baseVolume = 0.5f, _volumeRange = 0.1f, _master = 1f, _spatial = 0.9f;
        private float _movedAt = -100f, _gain;

        /// <summary>The owner sits in it (VehicleManager: fleet e.Driving).</summary>
        internal bool Driven;

        /// <summary>Called on the clone BEFORE the strip removes its controllers (NWH's wheels and sounds are read here).</summary>
        internal static void Attach(GameObject go)
        {
            try
            {
                if (go == null || go.GetComponent<GhostCarMotion>() != null) return;
                var nwh = go.GetComponentInChildren<NWH.VehiclePhysics2.VehicleController>(true);
                if (nwh == null) return;
                var m = go.AddComponent<GhostCarMotion>();
                var car = go.transform;
                var list = new List<Wheel>();
                float r = 0f;
                foreach (var wc in nwh.powertrain.wheels)
                {
                    var api = wc?.wheelUAPI;
                    var vis = api != null ? api.WheelVisual : null;
                    if (vis == null) continue;
                    var t = vis.transform;
                    var parent = t.parent != null ? t.parent : car;
                    list.Add(new Wheel
                    {
                        T = t,
                        Base = t.localRotation,
                        Axle = parent.InverseTransformDirection(car.right).normalized,
                        Up = parent.InverseTransformDirection(car.up).normalized,
                        Front = car.InverseTransformPoint(t.position).z > 0f,
                    });
                    if (r <= 0f && api!.Radius > 0.05f) r = api.Radius;
                }
                m._wheels = list.ToArray();
                if (r > 0f) m._radius = r;
                try { m._wheelBase = Mathf.Clamp(nwh.steering.CalculateWheelbase(), 1.5f, 6f); } catch { }
                try { m._maxSteer = Mathf.Clamp(nwh.steering.maximumSteerAngle, 10f, 50f); } catch { }
                try
                {
                    var sm = nwh.soundManager;
                    var er = sm?.engineRunningComponent;
                    if (sm != null && er != null && er.Clip != null)
                    {
                        m._clip = er.Clip;
                        m._mixer = sm.engineMixerGroup;
                        m._pitchOffset = er.pitchOffset; m._pitchRange = er.pitchRange;
                        m._baseVolume = er.baseVolume; m._volumeRange = er.volumeRange;
                        m._master = sm.masterVolume;
                        m._spatial = Mathf.Max(0.8f, sm.spatialBlend);
                    }
                }
                catch { }
                m._nwh = nwh;
            }
            catch (System.Exception ex) { Plugin.Logger.LogWarning($"[GhostCar] {ex.Message}"); }
        }

        internal static void SetDriven(GameObject? go, bool driven)
        {
            try { var m = go != null ? go.GetComponent<GhostCarMotion>() : null; if (m != null) m.Driven = driven; } catch { }
        }

        private void LateUpdate()
        {
            var t = transform;
            // a granted ghost the local player drives: NWH animates and sounds it itself
            if (_nwh != null && _nwh.enabled && _nwh.vehicleRigidbody != null && !_nwh.vehicleRigidbody.isKinematic)
            {
                if (_engine != null && _engine.isPlaying) _engine.Stop();
                _has = false;
                return;
            }
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
                float v = along / dt;
                _speed = Mathf.Lerp(_speed, v, Mathf.Min(1f, dt * 4f));
                float w = dYaw * Mathf.Deg2Rad / dt;
                if (Mathf.Abs(v) > 0.5f) steer = Mathf.Clamp(Mathf.Atan(w * _wheelBase / v) * Mathf.Rad2Deg, -_maxSteer, _maxSteer);
            }
            _steer = Mathf.Lerp(_steer, steer, Mathf.Min(1f, dt * 6f));
            for (int i = 0; i < _wheels.Length; i++)
            {
                var wh = _wheels[i];
                if (wh.T == null) continue;
                wh.T.localRotation = Quaternion.AngleAxis(wh.Front ? _steer : 0f, wh.Up) * Quaternion.AngleAxis(_roll, wh.Axle) * wh.Base;
            }
            Engine(dt);
        }

        private void Engine(float dt)
        {
            if (_clip == null) return;
            if (Mathf.Abs(_speed) > 0.4f) _movedAt = Time.time;
            bool on = Driven || Time.time - _movedAt < 4f;
            _gain = Mathf.MoveTowards(_gain, on ? 1f : 0f, dt * 1.5f);
            if (_engine == null)
            {
                if (_gain <= 0f) return;
                var go = new GameObject("BAMP_GhostEngine");
                go.transform.SetParent(transform, false);
                _engine = go.AddComponent<AudioSource>();
                _engine.clip = _clip;
                _engine.loop = true;
                _engine.playOnAwake = false;
                _engine.spatialBlend = _spatial;
                _engine.dopplerLevel = 0f;
                _engine.rolloffMode = AudioRolloffMode.Linear;
                _engine.minDistance = 3f;
                _engine.maxDistance = 55f;
                if (_mixer != null) _engine.outputAudioMixerGroup = _mixer;
            }
            if (_gain <= 0.01f) { if (_engine.isPlaying) _engine.Stop(); return; }
            if (!_engine.isPlaying) { _engine.time = UnityEngine.Random.Range(0f, Mathf.Max(0f, _clip.length - 0.1f)); _engine.Play(); }
            float rpm = Mathf.Clamp01(0.15f + Mathf.Abs(_speed) / 22f);   // idle about 15 %, the top near 80 km/h
            _engine.pitch = rpm * _pitchRange + _pitchOffset;
            _engine.volume = Mathf.Clamp(_baseVolume + rpm * _volumeRange, 0f, 1f) * _master * _gain;
        }

        private void OnDisable()
        {
            if (_engine != null && _engine.isPlaying) _engine.Stop();
        }
    }
}
