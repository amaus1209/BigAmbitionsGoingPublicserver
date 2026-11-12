using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Helpers;
using Newtonsoft.Json;
using UnityEngine;

namespace BigAmbitionsMP
{
    /// <summary>CARS-STAY-1 (user 2026-10-31: "a friend's cars vanish when he disconnects - keep them and park them legally
    /// at the roadside"; "a car another player sits in vanishes when he gets out after its owner left - keep it too").
    ///
    /// A dedicated server keeps every player's last fleet. When a player leaves, his cars stay in the world as an ABSENT
    /// fleet the server itself broadcasts (every few seconds, and at once on a change), so everyone - joiners included -
    /// keeps seeing them. A car its owner left in the road (he drove it when he left) or parked illegally is moved into the
    /// nearest free slot of a street parking lane: the slots the game itself hands out to player cars
    /// (ParkingLaneGenerator), never a handicapped one. A friend who still drives one of them finishes the drive; the car
    /// stays where he leaves it, and is parked legally as well after a short while if it stands illegally.
    ///
    /// On the owner's return his fleet is held back from the others until his own client has put the real cars where the
    /// others saw them (VehicleRelocate); then the server's copy is dropped. Kept across server restarts, in one file per
    /// world next to its saves. A ban removes the cars. Main thread, except <see cref="Holds"/>.</summary>
    internal static class AbsentCars
    {
        internal const string FileName = "absent-cars.json";
        private const float BeatSeconds = 3f;          // full absent fleets to everyone
        private const float HoldSeconds = 12f;         // a returning owner's fleet waits at most this long for his relocation
        private const float LeaveParkDelay = 3f;       // the leaver's last packets settle first
        private const float ReleaseParkDelay = 15f;    // a friend who just got out may still be at the car
        private const float ParkRetrySeconds = 10f;
        private const int   MaxParkTries = 6;

        internal sealed class Fleet
        {
            public string Owner { get; set; } = "";
            public long LeftUnix { get; set; }
            public List<VehicleEntry> Cars { get; set; } = new List<VehicleEntry>();
            public List<string> NeedPark { get; set; } = new List<string>();
            [JsonIgnore] public float ParkAt;
            [JsonIgnore] public int ParkTries;
            [JsonIgnore] public float HoldSince = -1f;
            [JsonIgnore] public bool RelocSent;
            [JsonIgnore] public float DriveBeatAt;
        }

        private static readonly Dictionary<string, Dictionary<string, VehicleEntry>> _live = new Dictionary<string, Dictionary<string, VehicleEntry>>();
        private static readonly Dictionary<string, Fleet> _absent = new Dictionary<string, Fleet>();
        private static readonly ConcurrentDictionary<string, byte> _hold = new ConcurrentDictionary<string, byte>();   // read by the network thread
        private static string? _loadedPath;
        private static bool _wipeOnLoad, _dirty, _warned;
        private static float _nextBeatAt, _nextSaveAt;

        internal static bool Enabled => MPConfig.DedicatedHost && MPServer.IsRunning;

        /// <summary>Any thread: this player's fleet is held back while his return is reconciled.</summary>
        internal static bool Holds(string ownerId) => !string.IsNullOrEmpty(ownerId) && _hold.ContainsKey(ownerId);

        // ── the fleets of connected players ──

        /// <summary>A fleet a connected player sent (MPServer.HandleVehicleSync).</summary>
        internal static void NoteFleet(VehicleFleetPayload p)
        {
            if (!Enabled || p == null || string.IsNullOrEmpty(p.OwnerId)) return;
            if (!_live.TryGetValue(p.OwnerId, out var m)) _live[p.OwnerId] = m = new Dictionary<string, VehicleEntry>();
            if (!p.Partial) m.Clear();
            foreach (var e in p.Vehicles) if (e != null && !string.IsNullOrEmpty(e.VehicleId)) m[e.VehicleId] = e;
            if (!p.Partial && _absent.Count > 0) ClaimBack(p);
        }

        /// <summary>The same cars listed by another player id (their owner came back under a new name): no absent copy of
        /// them any more - the full list without them makes their old ghosts go, the new owner's packets bring them back.</summary>
        private static void ClaimBack(VehicleFleetPayload p)
        {
            var ids = new HashSet<string>(p.Vehicles.Where(v => v != null).Select(v => v.VehicleId));
            foreach (var f in _absent.Values.ToList())
            {
                if (f.Owner == p.OwnerId) continue;
                int n = f.Cars.RemoveAll(c => ids.Contains(c.VehicleId));
                if (n == 0) continue;
                f.NeedPark.RemoveAll(ids.Contains);
                Plugin.Logger.LogInfo($"[AbsentCars] {n} vehicle(s) of absent '{f.Owner}' are listed by '{p.OwnerId}' now - their copies go.");
                _dirty = true;
                Broadcast(f, null);
                if (f.Cars.Count == 0) Drop(f.Owner);
            }
        }

        /// <summary>A player left the world (MPServer, main thread). keep = false on a ban: his cars go.</summary>
        internal static void OwnerLeft(string owner, bool keep)
        {
            if (!Enabled || string.IsNullOrEmpty(owner)) return;
            try
            {
                _live.TryGetValue(owner, out var live);
                _live.Remove(owner);
                if (!keep) { Forget(owner, "banned"); return; }
                EnsureLoaded();
                if (live == null || live.Count == 0)
                {
                    // left again before his return was reconciled: the kept copy stays as it was
                    if (_absent.TryGetValue(owner, out var old)) { old.HoldSince = -1f; old.RelocSent = false; }
                    return;
                }
                var f = new Fleet { Owner = owner, LeftUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
                foreach (var e in live.Values)
                {
                    if (e == null || e.Service) continue;   // a private driver's car was never his
                    bool drove = e.Driving;
                    var c = Copy(e);
                    c.Driving = false;
                    f.Cars.Add(c);
                    if (NeedsParking(c, drove) && !MPServer.IsCarDriven(c.VehicleId)) f.NeedPark.Add(c.VehicleId);
                }
                if (f.Cars.Count == 0) return;
                f.ParkAt = Time.unscaledTime + LeaveParkDelay;
                _absent[owner] = f;
                _hold[owner] = 0;
                _dirty = true;
                Save();
                Broadcast(f, null);
                Plugin.Logger.LogInfo($"[AbsentCars] '{owner}' left: their {f.Cars.Count} vehicle(s) stay in the world"
                    + $"{(f.NeedPark.Count > 0 ? $", {f.NeedPark.Count} to be parked at the roadside" : "")} (CARS-STAY-1).");
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[AbsentCars] leave of '{owner}': {ex.Message}"); }
        }

        private static bool NeedsParking(VehicleEntry c, bool drove)
            => string.IsNullOrEmpty(c.Bldg) && !c.Dormant && !VehicleManager.IsHandCartType(c.TypeName ?? "")
               && (drove || c.Park == (int)ParkingState.Illegal);

        /// <summary>A friend's drive stream of one of an absent owner's cars (MPServer.HandleVehicleDrive): the copy follows
        /// him; when he lets go, it stays there - and is parked legally after a while if it stands illegally.</summary>
        internal static void NoteDrive(VehicleDrivePayload p)
        {
            if (!Enabled || p == null || !_absent.TryGetValue(p.OwnerId ?? "", out var f)) return;
            var c = f.Cars.FirstOrDefault(x => x.VehicleId == p.VehicleId);
            if (c == null) return;
            float now = Time.unscaledTime;
            if (!p.Released)
            {
                c.X = p.X; c.Y = p.Y; c.Z = p.Z; c.Qx = p.Qx; c.Qy = p.Qy; c.Qz = p.Qz; c.Qw = p.Qw;
                c.Fuel = p.Fuel; c.Damage = p.Damage; c.Bldg = p.Bldg ?? "";
                f.NeedPark.Remove(c.VehicleId);
                _dirty = true;
                if (now >= f.DriveBeatAt) { f.DriveBeatAt = now + NetRate.MoveInterval; Broadcast(f, c.VehicleId); }   // the others see it move
                return;
            }
            if (p.ParkState >= 0) c.Park = p.ParkState;
            if (string.IsNullOrEmpty(c.Bldg) && !VehicleManager.IsHandCartType(c.TypeName ?? "") && c.Park != (int)ParkingState.Legal)
            {
                if (!f.NeedPark.Contains(c.VehicleId)) f.NeedPark.Add(c.VehicleId);
                f.ParkAt = now + ReleaseParkDelay;
                f.ParkTries = 0;
            }
            _dirty = true;
            Broadcast(f, null);   // the driver's own copy is rebuilt at the drive's end - give it back at once
            Plugin.Logger.LogInfo($"[AbsentCars] '{p.DriverId}' left '{c.VehicleId}' of absent '{f.Owner}' at {c.X:F0},{c.Z:F0}"
                + $"{(c.Bldg.Length > 0 ? " in '" + c.Bldg + "'" : "")}, parking {(ParkingState)c.Park}.");
        }

        // ── the owner's return ──

        /// <summary>A fleet of a returning owner, held back from the others (MPServer.HandleVehicleSync): once his cars stand
        /// where the world saw them, his fleet is shown again and the server's copy goes. Waits at most HoldSeconds.</summary>
        internal static void HeldFleet(VehicleFleetPayload p)
        {
            try
            {
                if (p == null) return;
                if (!_absent.TryGetValue(p.OwnerId, out var f)) { _hold.TryRemove(p.OwnerId, out _); return; }
                if (f.HoldSince < 0f)
                {
                    f.HoldSince = Time.unscaledTime;
                    Plugin.Logger.LogInfo($"[AbsentCars] '{f.Owner}' is back - their {f.Cars.Count} vehicle(s) are compared with where they stand now.");
                }
                if (p.Partial) return;   // the full list decides
                var moves = new List<VehicleEntry>();
                foreach (var e in p.Vehicles)
                {
                    if (e == null || e.Driving || e.Service) continue;   // the one he sits in stays with him
                    var c = f.Cars.FirstOrDefault(x => x.VehicleId == e.VehicleId);
                    if (c == null) continue;
                    if (Vector3.Distance(new Vector3(e.X, e.Y, e.Z), new Vector3(c.X, c.Y, c.Z)) > 2.5f || (e.Bldg ?? "") != (c.Bldg ?? "")) moves.Add(c);
                }
                if (moves.Count == 0) { EndReturn(f, p, "they stand where the others saw them"); return; }
                if (f.RelocSent) return;
                f.RelocSent = true;
                MPServer.SendToPlayer(f.Owner, MessageEnvelope.Create(MessageType.VehicleRelocate, "host", new VehicleRelocatePayload { Cars = moves }));
                Plugin.Logger.LogInfo($"[AbsentCars] '{f.Owner}': {moves.Count} of their vehicle(s) were moved while they were away - their client puts them there (VehicleRelocate).");
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[AbsentCars] return of '{p?.OwnerId}': {ex.Message}"); }
        }

        private static void EndReturn(Fleet f, VehicleFleetPayload? p, string why)
        {
            Drop(f.Owner);
            Plugin.Logger.LogInfo($"[AbsentCars] '{f.Owner}' has their vehicles back ({why}).");
            if (p == null) return;
            NoteFleet(p);
            VehicleManager.ApplyVehicleFleet(p);   // the host's copy
            MPServer.BroadcastAny(MessageEnvelope.Create(MessageType.VehicleSync, f.Owner, p));   // his own client skips its own fleet
        }

        /// <summary>Banned (or otherwise gone for good): every copy of his cars goes.</summary>
        internal static void Forget(string owner, string why)
        {
            if (string.IsNullOrEmpty(owner) || !_absent.TryGetValue(owner, out var f)) return;
            f.Cars.Clear();
            f.NeedPark.Clear();
            Broadcast(f, null);   // an empty full list
            Drop(owner);
            Plugin.Logger.LogInfo($"[AbsentCars] '{owner}': their kept vehicles are gone ({why}).");
        }

        /// <summary>A new world: nothing of the old one's absent players carries in.</summary>
        internal static void ForgetWorld(string why)
        {
            int n = _absent.Count;
            _absent.Clear();
            _hold.Clear();
            _live.Clear();
            _loadedPath = null;
            _dirty = false;
            _wipeOnLoad = true;
            if (n > 0) Plugin.Logger.LogInfo($"[AbsentCars] {why}: {n} absent player(s)' vehicles dropped.");
        }

        private static void Drop(string owner)
        {
            _absent.Remove(owner);
            _hold.TryRemove(owner, out _);
            _dirty = true;
            Save();
        }

        // ── clock ──

        /// <summary>Main thread, once a second (DedicatedHost.Tick).</summary>
        internal static void Tick()
        {
            if (!Enabled) return;
            try
            {
                if (!MPServer.WorldRunning) return;
                EnsureLoaded();
                float now = Time.unscaledTime;
                foreach (var f in _absent.Values.ToList())
                {
                    if (f.HoldSince >= 0f && now - f.HoldSince > HoldSeconds) { EndReturn(f, null, "his client did not move them in time - his own positions count"); continue; }
                    if (f.NeedPark.Count > 0 && now >= f.ParkAt) TryPark(f);
                }
                if (now >= _nextBeatAt)
                {
                    _nextBeatAt = now + BeatSeconds;
                    foreach (var f in _absent.Values) Broadcast(f, null);
                }
                if (_dirty && now >= _nextSaveAt) Save();
            }
            catch (Exception ex)
            {
                if (!_warned) { _warned = true; Plugin.Logger.LogWarning($"[AbsentCars] tick: {ex.Message}"); }
            }
        }

        private static void Broadcast(Fleet f, string? onlyVid)
        {
            var p = new VehicleFleetPayload { OwnerId = f.Owner, Absent = true, Partial = onlyVid != null, T = Time.unscaledTime };
            foreach (var c in f.Cars) if (onlyVid == null || c.VehicleId == onlyVid) p.Vehicles.Add(c);
            if (onlyVid != null && p.Vehicles.Count == 0) return;
            try { VehicleManager.ApplyVehicleFleet(p); } catch { }   // the host's copy: the lanes see it, the traffic brakes for it
            MPServer.BroadcastAny(MessageEnvelope.Create(MessageType.VehicleSync, "host", p));
        }

        // ── parking at the roadside ──

        private static void TryPark(Fleet f)
        {
            f.ParkTries++;
            int moved = 0;
            var others = TakenSpots();
            foreach (var vid in f.NeedPark.ToList())
            {
                var c = f.Cars.FirstOrDefault(x => x.VehicleId == vid);
                if (c == null || !string.IsNullOrEmpty(c.Bldg)) { f.NeedPark.Remove(vid); continue; }
                if (MPServer.IsCarDriven(vid)) continue;   // a friend drives it right now: parked after he lets go
                var ghost = VehicleManager.GhostGo(vid);
                var near = new Vector3(c.X, c.Y, c.Z);
                if (!ParkingSpots.TryFind(near, ParkingSpots.CarLength(ghost), ghost, spot => Taken(spot, vid, others), out var pos, out var rot)) continue;
                c.X = pos.x; c.Y = pos.y; c.Z = pos.z;
                c.Qx = rot.x; c.Qy = rot.y; c.Qz = rot.z; c.Qw = rot.w;
                c.Park = (int)ParkingState.Legal;
                f.NeedPark.Remove(vid);
                others.Add(new KeyValuePair<string, Vector3>(vid, pos));
                moved++;
                Plugin.Logger.LogInfo($"[AbsentCars] {c.TypeName} '{vid}' of '{f.Owner}' parked at the roadside, {Vector3.Distance(near, pos):F0} m from where it stood ({pos.x:F0},{pos.z:F0}).");
            }
            if (moved > 0) { _dirty = true; Broadcast(f, null); }
            if (f.NeedPark.Count == 0) return;
            if (f.ParkTries >= MaxParkTries)
            {
                Plugin.Logger.LogInfo($"[AbsentCars] '{f.Owner}': no free parking slot near {f.NeedPark.Count} vehicle(s) after {f.ParkTries} tries - they stay where they are.");
                f.NeedPark.Clear();
                _dirty = true;
                return;
            }
            f.ParkAt = Time.unscaledTime + ParkRetrySeconds;
        }

        /// <summary>Where every other car stands: the spawned ghosts and every kept absent car.</summary>
        private static List<KeyValuePair<string, Vector3>> TakenSpots()
        {
            var l = VehicleManager.GhostPositions();
            foreach (var f in _absent.Values)
                foreach (var c in f.Cars) l.Add(new KeyValuePair<string, Vector3>(c.VehicleId, new Vector3(c.X, c.Y, c.Z)));
            return l;
        }

        private static bool Taken(Vector3 spot, string selfVid, List<KeyValuePair<string, Vector3>> others)
        {
            foreach (var kv in others) if (kv.Key != selfVid && (kv.Value - spot).sqrMagnitude < 16f) return true;
            return false;
        }

        private static VehicleEntry Copy(VehicleEntry e) => new VehicleEntry
        {
            VehicleId = e.VehicleId, TypeName = e.TypeName, ColorName = e.ColorName, Driving = e.Driving,
            Fuel = e.Fuel, Damage = e.Damage,
            X = e.X, Y = e.Y, Z = e.Z, Qx = e.Qx, Qy = e.Qy, Qz = e.Qz, Qw = e.Qw,
            Cargo = e.Cargo, CargoNested = e.CargoNested ?? new List<VehicleCargoNested>(), CarriedItems = e.CarriedItems,
            Bldg = e.Bldg, Dormant = e.Dormant, Service = e.Service, Park = e.Park,
        };

        // ── one file per world ──

        private static string? FilePath()
        {
            try
            {
                string session = MPSaveCoordinator.StripAutoSuffix(MPSaveCoordinator.ActiveSessionName);
                return string.IsNullOrEmpty(session) ? null : Path.Combine(MPSaveManager.MpSessionFolder(session), FileName);
            }
            catch { return null; }
        }

        private static void EnsureLoaded()
        {
            string? path = FilePath();
            if (path == null || path == _loadedPath) return;
            if (_loadedPath != null && _dirty) Save();   // the previous world's absent players stay with it
            _absent.Clear();
            _hold.Clear();
            _loadedPath = path;
            if (_wipeOnLoad)
            {
                _wipeOnLoad = false;
                try { if (File.Exists(path)) File.Delete(path); } catch { }
                return;
            }
            try
            {
                if (!File.Exists(path)) return;
                var list = JsonConvert.DeserializeObject<List<Fleet>>(File.ReadAllText(path)) ?? new List<Fleet>();
                float now = Time.unscaledTime;
                foreach (var f in list)
                {
                    if (f == null || string.IsNullOrEmpty(f.Owner) || f.Cars == null || f.Cars.Count == 0) continue;
                    f.NeedPark ??= new List<string>();
                    f.ParkAt = now + 5f;
                    _absent[f.Owner] = f;
                    _hold[f.Owner] = 0;
                }
                if (_absent.Count > 0)
                    Plugin.Logger.LogInfo($"[AbsentCars] {_absent.Count} absent player(s) with {_absent.Values.Sum(x => x.Cars.Count)} vehicle(s) kept from the last run.");
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[AbsentCars] load '{path}': {ex.Message}"); }
        }

        private static void Save()
        {
            _nextSaveAt = Time.unscaledTime + 2f;
            string? path = _loadedPath ?? FilePath();
            if (path == null) return;
            try
            {
                _dirty = false;
                if (_absent.Count == 0)
                {
                    if (File.Exists(path)) File.Delete(path);
                    return;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonConvert.SerializeObject(_absent.Values.ToList(), Formatting.Indented));
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
            catch (Exception ex)
            {
                _dirty = true;
                Plugin.Logger.LogWarning($"[AbsentCars] save: {ex.Message}");
            }
        }
    }

    /// <summary>CARS-STAY-1 / AUTO-DRIVE-1: the nearest free player slot of a street parking lane - shared by the server's
    /// roadside parking of absent players' cars and the self-driving car's parking at its destination.</summary>
    internal static class ParkingSpots
    {
        private static readonly float[] SearchRadii = { 25f, 60f, 120f };
        private static readonly Collider[] _laneHits = new Collider[96];
        private static readonly Collider[] _boxHits = new Collider[16];

        /// <summary>The nearest free player slot of a street parking lane around <paramref name="near"/> (the game's own
        /// slot poses, ParkingLaneGenerator.GetPlayerSpotPose; free = the slot's box holds nothing on the game's own
        /// free-spot layers, the car's own ghost aside). Handicapped lanes and delivery lanes are left out.</summary>
        internal static bool TryFind(Vector3 near, float length, GameObject? self, Func<Vector3, bool>? taken, out Vector3 pos, out Quaternion rot)
        {
            pos = default;
            rot = Quaternion.identity;
            Physics.SyncTransforms();
            foreach (float radius in SearchRadii)
            {
                int n = Physics.OverlapSphereNonAlloc(near, radius, _laneHits, LayerHelper.parkingAreaLayerMask, QueryTriggerInteraction.Collide);
                var lanes = new HashSet<ParkingLaneGenerator>();
                for (int i = 0; i < n; i++)
                {
                    var lane = _laneHits[i] != null ? _laneHits[i].GetComponentInParent<ParkingLaneGenerator>() : null;
                    if (lane != null) lanes.Add(lane);
                }
                float best = float.MaxValue;
                bool found = false;
                foreach (var lane in lanes)
                {
                    try
                    {
                        if (lane.isHandicapParking || lane._collider == null || lane.ContainsDeliveryVehicle()) continue;
                        if (lane.sideBySideParkingSpots && length > lane._collider.size.z + 0.5f) continue;
                        if (lane.parkingSpotDistance <= 0.1f) continue;
                        if (lane.IsCurved && lane._segments.Count == 0) lane.SyncCurveSegments();
                        int slots = Mathf.FloorToInt(lane.SlotSpan / lane.parkingSpotDistance);
                        for (int s = 0; s < slots; s++)
                        {
                            lane.GetPlayerSpotPose(s, out var c, out var r);
                            float d = (c - near).sqrMagnitude;
                            if (d >= best || Mathf.Abs(c.y - near.y) > 6f || (taken != null && taken(c))) continue;
                            var half = lane.sideBySideParkingSpots
                                ? new Vector3(lane.parkingSpotDistance * 0.5f, 0.5f, lane._collider.size.z * 0.5f)
                                : new Vector3(lane._collider.size.x * 0.5f, 0.5f, Mathf.Max(lane.parkingSpotDistance * 0.5f, length * 0.5f + 0.3f));
                            if (Occupied(c, half, r, self)) continue;
                            best = d; pos = c; rot = r; found = true;
                        }
                    }
                    catch { }
                }
                if (found) { Ground(ref pos, ref rot); return true; }
            }
            return false;
        }

        private static bool Occupied(Vector3 center, Vector3 half, Quaternion rot, GameObject? self)
        {
            int n = Physics.OverlapBoxNonAlloc(center, half, _boxHits, rot, LayerHelper.freeParkingSpotDetectionMask, QueryTriggerInteraction.Ignore);
            if (n >= _boxHits.Length) return true;
            for (int i = 0; i < n; i++)
            {
                var col = _boxHits[i];
                if (col == null || (self != null && col.transform.IsChildOf(self.transform))) continue;
                return true;
            }
            return false;
        }

        /// <summary>On the ground, tilted like it (VehicleHelper.TeleportVehicleToGround's rule).</summary>
        private static void Ground(ref Vector3 pos, ref Quaternion rot)
        {
            if (Physics.Raycast(pos + Vector3.up * 2f, Vector3.down, out var hit, 4f, LayerHelper.groundLayerMask, QueryTriggerInteraction.Ignore))
            {
                pos = hit.point;
                rot = Quaternion.FromToRotation(Vector3.up, hit.normal) * rot;
            }
        }

        internal static float CarLength(GameObject? ghost)
        {
            try
            {
                if (ghost != null && VehicleHelper.TryGetBodyColliderBounds(ghost.transform, out var b, out _)) return Mathf.Clamp(b.size.z, 3f, 12f);
            }
            catch { }
            return 4.8f;
        }

    }
}
