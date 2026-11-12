using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

namespace BigAmbitionsMP
{
    /// <summary>GO-LIVERY-1 (user 2026-11-10, "Ja" to the proposal "Andere Spielende sehen deine Wagen in deiner
    /// Beklebung"): every player's service van livery reaches the others. Each machine paints liveries itself
    /// (goingonline/core/Livery.cs); what travels is the livery's description (motif, colour, name, slogan, contact, offers)
    /// and the business logo as a small JPG - sent when it changes and after each (re)connect, kept by the host and passed on
    /// to everybody and to every joiner (MessageType.VanLivery). Another player's van on this machine (a ghost of their
    /// vehicle, "BAMP_" + its id) is painted with its owner's livery; while that is not known (an older client, the module
    /// switched off there) it stays plain.
    /// CAR-LIVERY-1 (user 2026-11-10, each vehicle its own livery after the purchase): the same message carries every
    /// vehicle's own livery (vehicle id -> CarLivery.Pack) and the logos those show (business -> JPG), so the other players'
    /// cars wear theirs here (CarWraps, and Going Online's van through Livery.PerVehicle).</summary>
    internal static class VanLiveries
    {
        static readonly ConcurrentDictionary<string, VanLiveryPayload> _known = new ConcurrentDictionary<string, VanLiveryPayload>();
        static readonly ConcurrentDictionary<string, (string pack, byte[]? logo)> _decoded = new ConcurrentDictionary<string, (string, byte[]?)>();
        static readonly ConcurrentDictionary<string, Dictionary<string, string>> _cars = new ConcurrentDictionary<string, Dictionary<string, string>>();
        static readonly ConcurrentDictionary<string, Dictionary<string, byte[]>> _logos = new ConcurrentDictionary<string, Dictionary<string, byte[]>>();
        static readonly ConcurrentDictionary<string, Dictionary<string, string>> _plates = new ConcurrentDictionary<string, Dictionary<string, string>>();   // GO-PLATE-1
        static string _sentPack = "\u0001", _sentCars = "\u0001";
        static int _sentLogo = -1;
        static float _next, _sentAt;
        static bool _hooked, _wasOnline;
        static int _logged, _carLogged;

        /// <summary>Rises when another player's vehicle liveries arrive (their cars look again).</summary>
        internal static int CarsVersion;

        /// <summary>Once: Going Online asks here who owns a van and how that player's livery looks.</summary>
        static void Hook()
        {
            if (_hooked) return;
            _hooked = true;
            GoingOnline.Livery.OwnerOf = OwnerOf;
            GoingOnline.Livery.RemoteOf = id => _decoded.TryGetValue(id, out var v) ? v : ((string, byte[]?)?)null;
            GoingOnline.Livery.PlayerName = () => MPConfig.PlayerId;   // GO-PLATE-1: "GO1-<name>"
            GoingOnline.Livery.RemotePlate = RemotePlate;
        }

        /// <summary>GO-PLATE-1: another player's van's plate as they sent it (null: not known).</summary>
        static string? RemotePlate(GameObject car)
        {
            if (car == null) return null;
            string vid = "", owner = "";
            if (VehicleManager.GhostOf(car, out var gv, out var go)) { vid = gv; owner = go; }
            else
            {
                var vc = car.GetComponent<VehicleController>();
                string id = vc != null && vc.vehicleInstance != null ? vc.vehicleInstance.id ?? "" : "";
                if (!id.StartsWith("BAMP_")) return null;
                vid = id.Substring(5);
                owner = VehicleManager.OwnerIdFor(vid) ?? "";
            }
            return owner.Length > 0 && _plates.TryGetValue(owner, out var map) && map != null && map.TryGetValue(vid, out var t) ? t : null;
        }

        /// <summary>The owner of another player's vehicle on this machine (a ghost of VehicleManager; one that may be driven
        /// still carries "BAMP_" + the vehicle id), "?" when the ghost's owner is not known; null for the player's own
        /// vehicles and the city's traffic.</summary>
        static string? OwnerOf(GameObject car)
        {
            if (car == null) return null;
            string ghost = VehicleManager.OwnerOfGhost(car);
            if (ghost.Length > 0) return ghost;
            var vc = car.GetComponent<VehicleController>();
            string id = vc != null && vc.vehicleInstance != null ? vc.vehicleInstance.id ?? "" : "";
            if (!id.StartsWith("BAMP_")) return null;
            string owner = VehicleManager.OwnerIdFor(id.Substring(5));
            return string.IsNullOrEmpty(owner) ? "?" : owner;
        }

        /// <summary>MAIN THREAD, from GoingOnlineModule.Tick (not on a dedicated server): the player's own livery to the host
        /// when it changed, at most every 2 s.</summary>
        internal static void Tick()
        {
            Hook();
            bool client = MPClient.IsConnected, host = MPServer.IsRunning;
            if (!client && !host) { _wasOnline = false; return; }
            if (!_wasOnline) { _wasOnline = true; _sentPack = "\u0001"; _sentLogo = -1; _sentCars = "\u0001"; }   // a new session: send it again
            float now = Time.unscaledTime;
            if (now < _next) return;
            _next = now + 2f;
            string pack = "";
            byte[]? logo = null;
            if (ServerRules.Module("ModuleGoingOnline")) pack = GoingOnline.Livery.LocalPack(out logo);
            int logoLen = pack.Length > 0 ? logo?.Length ?? 0 : 0;
            var cars = CarLiveries.Packs();
            var logos = CarLogos(cars);
            var plates = ServerRules.Module("ModuleGoingOnline") ? GoingOnline.Livery.LocalPlates() : new Dictionary<string, string>();   // GO-PLATE-1
            string carSig = Sig(cars, logos) + "#" + string.Join(";", plates.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key + "=" + kv.Value));
            // unchanged: nothing - but every two minutes once more, in case the host dropped one (its rate cap)
            if (pack == _sentPack && logoLen == _sentLogo && carSig == _sentCars && now - _sentAt < 120f) return;
            if (pack.Length == 0 && cars.Count == 0 && _sentPack == "" && _sentCars == carSig) return;   // nothing to say
            _sentPack = pack; _sentLogo = logoLen; _sentCars = carSig; _sentAt = now;
            var p = new VanLiveryPayload
            {
                PlayerId = MPConfig.PlayerId, Spec = pack,
                Logo = logoLen > 0 ? System.Convert.ToBase64String(logo!) : "",
                Cars = cars, Logos = logos,
                Plates = plates.Count > 0 ? plates : null,
            };
            if (host) MPServer.HostVanLivery(p, MPConfig.PlayerId);
            else MPClient.SendVanLivery(p);
            if (_logged++ < 3) Plugin.Logger.LogInfo($"[GoingOnline] van livery sent to the {(host ? "players" : "host")} ({(pack.Length > 0 ? "logo " + logoLen + " bytes" : "none")}; {cars.Count} vehicle livery(ies), {logos.Count} logo(s)) (GO-LIVERY-1, CAR-LIVERY-1).");
        }

        static string Sig(Dictionary<string, string> cars, Dictionary<string, string> logos)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var k in cars.Keys.OrderBy(x => x, StringComparer.Ordinal)) sb.Append(k).Append('=').Append(cars[k]).Append(';');
            sb.Append('#');
            foreach (var k in logos.Keys.OrderBy(x => x, StringComparer.Ordinal)) sb.Append(k).Append(':').Append(logos[k].Length).Append(';');
            return sb.ToString();
        }

        static readonly Dictionary<string, string> _jpg = new Dictionary<string, string>();

        /// <summary>The logos the vehicles' liveries show (business -> base64 JPG, made once per logo).</summary>
        static Dictionary<string, string> CarLogos(Dictionary<string, string> cars)
        {
            var d = new Dictionary<string, string>();
            foreach (var pack in cars.Values)
            {
                var l = CarLivery.Unpack(pack);
                if (l == null || l.Biz.Length == 0 || d.ContainsKey(l.Biz) || d.Count >= 8) continue;
                var t = CarLiveries.LogoOf(l.Biz, out var key);
                if (t == null) continue;
                if (!_jpg.TryGetValue(key, out var b64))
                {
                    var bytes = GoingOnline.Livery.Jpg(t);
                    b64 = bytes != null && bytes.Length > 0 ? System.Convert.ToBase64String(bytes) : "";
                    if (_jpg.Count > 32) _jpg.Clear();
                    _jpg[key] = b64;
                }
                if (b64.Length > 0) d[l.Biz] = b64;
            }
            return d;
        }

        /// <summary>MAIN THREAD: the host's word on another player's livery (a change, or the join replay).</summary>
        internal static void Receive(VanLiveryPayload p)
        {
            if (p == null || string.IsNullOrEmpty(p.PlayerId) || p.PlayerId == MPConfig.PlayerId) return;
            Store(p);
        }

        /// <summary>Keeps a player's livery (on the host also for joiners); "" = that player shows none.</summary>
        internal static void Store(VanLiveryPayload p)
        {
            if (p == null || string.IsNullOrEmpty(p.PlayerId)) return;
            _known[p.PlayerId] = p;
            if (p.PlayerId == MPConfig.PlayerId) return;
            if (string.IsNullOrEmpty(p.Spec)) _decoded.TryRemove(p.PlayerId, out _);
            else
            {
                byte[]? logo = null;
                if (!string.IsNullOrEmpty(p.Logo)) { try { logo = System.Convert.FromBase64String(p.Logo); } catch { logo = null; } }
                _decoded[p.PlayerId] = (p.Spec, logo);
            }
            // CAR-LIVERY-1: the vehicles' own liveries and their logos
            _cars[p.PlayerId] = p.Cars != null ? new Dictionary<string, string>(p.Cars) : new Dictionary<string, string>();
            var logos = new Dictionary<string, byte[]>();
            if (p.Logos != null)
                foreach (var kv in p.Logos)
                {
                    try { var b = System.Convert.FromBase64String(kv.Value ?? ""); if (b.Length > 0) logos[kv.Key] = b; } catch { }
                }
            _logos[p.PlayerId] = logos;
            _plates[p.PlayerId] = p.Plates != null ? new Dictionary<string, string>(p.Plates) : new Dictionary<string, string>();   // GO-PLATE-1
            CarsVersion++;
            if (p.Cars != null && p.Cars.Count > 0 && _carLogged++ < 3)
                Plugin.Logger.LogInfo($"[CarLivery] another player's vehicle liveries arrived: {p.Cars.Count} vehicle(s), {logos.Count} logo(s) (CAR-LIVERY-1).");
        }

        static readonly Dictionary<string, (string pack, CarLivery? l)> _unpacked = new Dictionary<string, (string, CarLivery?)>();

        /// <summary>CAR-LIVERY-1: another player's livery for one of their vehicles (null: none, or not known).</summary>
        internal static CarLivery? CarOf(string owner, string vid)
        {
            if (string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(vid)) return null;
            if (!_cars.TryGetValue(owner, out var map) || map == null || !map.TryGetValue(vid, out var pack)) return null;
            string k = owner + "|" + vid;
            lock (_unpacked)
            {
                if (_unpacked.TryGetValue(k, out var have) && have.pack == pack) return have.l;
                var l = CarLivery.Unpack(pack);
                if (_unpacked.Count > 400) _unpacked.Clear();
                _unpacked[k] = (pack, l);
                return l;
            }
        }

        sealed class Tex { public int Hash; public Texture2D? T; }
        static readonly Dictionary<string, Tex> _logoTex = new Dictionary<string, Tex>();

        /// <summary>CAR-LIVERY-1: a logo another player's vehicle shows (from their JPG) and its key.</summary>
        internal static Texture? LogoOf(string owner, string biz, out string key)
        {
            key = "";
            if (string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(biz)) return null;
            if (!_logos.TryGetValue(owner, out var map) || map == null || !map.TryGetValue(biz, out var bytes) || bytes == null) return null;
            int hash = Fnv(bytes);
            string k = owner + "|" + biz;
            if (!_logoTex.TryGetValue(k, out var e)) _logoTex[k] = e = new Tex();
            if (e.Hash != hash || e.T == null)
            {
                if (e.T != null) UnityEngine.Object.Destroy(e.T);
                e.T = null; e.Hash = hash;
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                if (ImageConversion.LoadImage(t, bytes)) e.T = t; else UnityEngine.Object.Destroy(t);
            }
            if (e.T == null) return null;
            key = "p:" + owner + ":" + biz + ":" + hash;
            return e.T;
        }

        static int Fnv(byte[] b)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (var x in b) { h ^= x; h *= 16777619; }
                return (int)(h | 1);
            }
        }

        /// <summary>HOST: every known livery, for a joiner.</summary>
        internal static List<VanLiveryPayload> Snapshot()
        {
            var list = new List<VanLiveryPayload>();
            foreach (var kv in _known)
                if (kv.Value != null && (!string.IsNullOrEmpty(kv.Value.Spec) || (kv.Value.Cars != null && kv.Value.Cars.Count > 0))) list.Add(kv.Value);
            return list;
        }

        internal static void ResetSession()
        {
            _known.Clear();
            _decoded.Clear();
            _cars.Clear();
            _logos.Clear();
            lock (_unpacked) _unpacked.Clear();
            CarsVersion++;
            _sentPack = "\u0001"; _sentLogo = -1; _sentCars = "\u0001";
        }
    }
}
