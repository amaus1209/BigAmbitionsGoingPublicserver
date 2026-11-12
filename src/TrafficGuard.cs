using System;
using Helpers;
using UnityEngine;

namespace BigAmbitionsMP
{
    /// <summary>TRAFFIC-GUARD-1 (user 2026-11-01: "other cars drive into each other"; server logs: 224 contacts between
    /// traffic cars, clustered in the queues along two streets and their crossing, median 2.5 m/s - rear-ends): the
    /// traffic system's own following (its front trigger) brakes too late in a queue now and then. On the dedicated
    /// server every traffic car looks ahead once per physics step; when the time to hit the car in front drops under
    /// 1.2 s, it is braked just enough to stop behind it - never accelerated, and its normal following is left alone.
    /// The clients' ghosts follow the server's cars, so they no longer drive into each other either.</summary>
    internal sealed class TrafficGuard : MonoBehaviour
    {
        private const float TimeToHit = 1.2f;
        private static TrafficGuard? _instance;
        private static int _mask = -1;
        private static int _brakes, _cars;
        private static float _nextLogAt;
        private readonly System.Collections.Generic.HashSet<int> _braked = new System.Collections.Generic.HashSet<int>();

        /// <summary>Main thread, once a second (DedicatedHost.Tick): the guard runs while this machine hosts a world.</summary>
        internal static void EnsureRunning()
        {
            if (_instance != null || !MPServer.IsRunning) return;
            var go = new GameObject("BAMP_TrafficGuard");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<TrafficGuard>();
            Plugin.Logger.LogInfo("[TrafficGuard] traffic rear-end guard on (TRAFFIC-GUARD-1).");
        }

        private void FixedUpdate()
        {
            if (!MPServer.IsRunning || !MPServer.WorldRunning) return;
            try
            {
                if (_mask == -1)
                {
                    int m = 0;
                    foreach (int l in new[] { LayerHelper.AiVehiclesLayerIndex, LayerHelper.VehiclesLayerIndex, LayerHelper.PlayerVehiclesLayerIndex })
                        if (l >= 0 && l < 32) m |= 1 << l;
                    _mask = m;
                }
                var list = GleyTrafficSystem.Manager.GetVehicleList();
                if (list == null) return;
                float dt = Time.fixedDeltaTime;
                for (int i = 0; i < list.Count; i++)
                {
                    var car = list[i];
                    if (car == null || !car.gameObject.activeInHierarchy) continue;
                    var rb = car.rb;
                    if (rb == null || rb.isKinematic) continue;
                    var t = car.transform;
                    Vector3 fwd = t.forward;
                    float v = Vector3.Dot(rb.velocity, fwd);
                    if (v < 1f) continue;
                    Vector3 origin = (car.frontTrigger != null ? car.frontTrigger.position : t.position + fwd * 2f) + Vector3.up * 0.6f;
                    float look = 2f + v * 1.4f;
                    if (!Physics.BoxCast(origin, new Vector3(0.75f, 0.35f, 0.1f), fwd, out var hit, t.rotation, look, _mask, QueryTriggerInteraction.Ignore)) continue;
                    var other = hit.rigidbody;
                    if (other == null || other == rb || (hit.collider != null && hit.collider.transform.IsChildOf(t))) continue;
                    // rear-ends only: a car heading the same way (an oncoming one in a curve, or crossing traffic, is not ours)
                    if (Vector3.Dot(other.transform.forward, fwd) < 0.5f) continue;
                    float vOther = Vector3.Dot(other.velocity, fwd);
                    float closing = v - vOther;
                    if (closing <= 0.3f) continue;
                    float gap = Mathf.Max(hit.distance - 0.4f, 0.2f);
                    if (gap / closing > TimeToHit) continue;
                    // the braking that brings the closing speed to zero within the gap, at least 3 m/s²
                    float a = Mathf.Max(closing * closing / (2f * gap), 3f);
                    rb.velocity -= fwd * Mathf.Min(closing, a * dt);
                    _brakes++;
                    _braked.Add(car.GetInstanceID());
                }
                float now = Time.unscaledTime;
                if (now >= _nextLogAt)
                {
                    if (_brakes > 0)
                        Plugin.Logger.LogInfo($"[TrafficGuard] last 5 min: {_brakes} brake step(s) for {_braked.Count} traffic car(s) closing in on the car ahead.");
                    _nextLogAt = now + 300f;
                    _brakes = 0;
                    _braked.Clear();
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"[TrafficGuard] {ex.Message} - guard off.");
                enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
