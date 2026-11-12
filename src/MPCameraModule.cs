using System.Globalization;
using CameraControllers;
using Cinemachine;
using Helpers;
using TMPro;
using UI;
using UI.Load;
using UI.MiniMenu;
using UI.Smartphone;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BigAmbitionsMP
{
    /// <summary>
    /// MODULE-CAMERA-1 (Going Public Server 1.0.12, module 3 of the server's own features; replaces the Immersion mod
    /// with own code): an RPG-style third-person camera on foot and in the car. F6 switches it on and off, F8 opens its
    /// settings. Only this machine's view changes - nothing goes to the server.
    ///
    /// The game's own Cinemachine cameras (GameManager.pedestrianCamera etc., switched by Helpers.CameraHelper) keep doing
    /// the work. While the module is on it retunes, every frame, the values the game's designers set in the scene, and it
    /// puts the originals back when it is switched off. No Harmony patch: each value is a public field the game reads
    /// every frame.
    ///   on foot (PedestrianCam on pedestrianCamera): `offset` is the viewing angle (game ~47°), `minMaxDistance` /
    ///     `distance` the zoom (game 15-35 m). Turning with a right-drag sideways and the wheel zoom are the game's own;
    ///     the module adds the angle on a right-drag up/down, a closer zoom with a step that grows with the distance,
    ///     and lifts the composer's aim from the player's feet (Follow = LookAt = "Player") to the chest;
    ///   in the car (vehicleCamera / vehicleCameraReverse: a Transposer locked to the car's heading at 0/21.95/-22.3,
    ///     no zoom, no turning): the follow offset becomes distance + angle + a turn around the car from a right-drag,
    ///     the wheel zooms, and the turn swings back behind the car once it drives on;
    ///   both: field of view and view distance (the lens' far clip).
    /// Inside buildings (indoorCamera, indoorVehicleCamera*) the game's view stays unless "Also inside buildings" is on.
    /// The game's own fading of buildings between the camera and the player keeps working for the lower camera.
    /// </summary>
    internal static class RpgCamera
    {
        internal const float FOOT_MIN = 2.5f, FOOT_MAX = 40f, CAR_MIN = 5f, CAR_MAX = 45f;
        internal const float FOOT_PITCH_MIN = 5f, FOOT_PITCH_MAX = 75f, CAR_PITCH_MIN = 3f, CAR_PITCH_MAX = 60f;
        private const float D_FOOT_PITCH = 20f, D_FOOT_DIST = 8f, D_CAR_PITCH = 18f, D_CAR_DIST = 15f, D_HEIGHT = 1.5f, D_FOV = 55f;

        // settings - "CameraSettings" in the mod config, "k=v;..." (Serialize/Load)
        internal static bool On, Recenter = true, Indoors;
        internal static float FootPitch = D_FOOT_PITCH, FootDist = D_FOOT_DIST, CarPitch = D_CAR_PITCH, CarDist = D_CAR_DIST;
        internal static float Height = D_HEIGHT, Shoulder, Sens = 1f, Fov = D_FOV, View = 1f;
        internal static int DeadZone = 3;
        private static bool _hintShown, _loaded, _dirty, _footDistSet, _managed;
        private static int _rulesVersion = -1;

        /// <summary>SERVER-SETTINGS-1: in a server's world the camera's settings are the server's (AMP) - the same for every
        /// player. The F8 window only shows them; zoom, tilt and turn still work but are not kept.</summary>
        internal static bool Managed => _managed;
        private static float _dirtyAt, _worldSince = -1f;

        /// <summary>One of the game's cameras and the values the scene gave it.</summary>
        private sealed class Rec
        {
            public CinemachineVirtualCamera Vc = null!;
            public PedestrianCam? Ped;
            public CinemachineTransposer? Tr;
            public CinemachineComposer? Co;
            public bool Indoor, Reverse, Applied;
            public Vector3 POffset, TrOffset, CoOffset;
            public Vector2 PMinMax;
            public float PChange, PSens, CoScreenX, Fov, Far;
        }
        private static GameManager? _gm;
        private static readonly List<Rec> _recs = new List<Rec>();

        // input state
        private static bool _rmb, _drag, _wasCar, _hasLast;
        private static Vector2 _rmbStart, _rmbLast;
        private static float _orbit, _carDistCur = D_CAR_DIST, _lastTurn;
        private static Vector3 _lastPos;
        private static int _errors;

        /// <summary>Main thread, every frame from MPCanvasUI.Update.</summary>
        internal static void Tick()
        {
            if (HeadlessHost.Active) return;
            try
            {
                if (!_loaded) Load();
                bool managed = ServerRules.FromServer;
                if (managed != _managed || (managed && _rulesVersion != ServerRules.Version))
                {
                    _managed = managed; _rulesVersion = ServerRules.Version;
                    if (managed) FromServer(); else Load();   // back to this player's own settings
                }
                SaveIfDirty();
                var gm = InstanceBehavior<GameManager>.Instance;
                bool inWorld = gm != null && SaveGameManager.Current != null && PlayerHelper.PlayerController != null
                               && !GameManager.isCitySceneBeingUnloaded && !LoadScene.isLoading;
                if (!inWorld) { _worldSince = -1f; _rmb = _drag = _wasCar = _hasLast = false; return; }
                if (!ReferenceEquals(gm, _gm)) Capture(gm!);   // a new city scene: new cameras with the game's values
                if (_worldSince < 0f) _worldSince = Time.unscaledTime;
                bool allowed = ServerRules.Module("ModuleCamera");   // SERVER-RULES-1: the server admin may switch it off
                if (allowed && !_managed) ShowHintOnce();
                if (ModKeys.Camera.Down() && !MPChat.SuppressGameInput && !GameManager.HasInputSelected())
                {
                    if (allowed && _managed) ServerRules.ServerSet(Loc.T("gps_cam_title", "RPG camera"));
                    else if (allowed) SetOn(!On);
                    else ServerRules.ModuleOff(Loc.T("gps_cam_title", "RPG camera"));
                }
                if (!On || !allowed) { foreach (var r in _recs) Restore(r); return; }

                CinemachineVirtualCameraBase? cur = null;
                try { cur = CameraHelper.GetCurrentCamera(); } catch { }
                Rec? live = null;
                foreach (var r in _recs) if (r.Vc != null && r.Vc == cur && (!r.Indoor || Indoors)) live = r;
                bool carLive = live?.Tr != null, footLive = live?.Ped != null;
                if (carLive && !_wasCar) { _orbit = 0f; _hasLast = false; }   // every drive starts behind the car
                _wasCar = carLive;
                HandleMouse(footLive, carLive);
                float dt = Time.unscaledDeltaTime;
                _carDistCur = Mathf.Lerp(_carDistCur, CarDist, 1f - Mathf.Exp(-6f * dt));
                if (carLive)
                {
                    float speed = TargetSpeed(live!.Vc.Follow);
                    if (Recenter && !_drag && Time.unscaledTime - _lastTurn > 1.5f && speed > 2f)
                        _orbit = Mathf.MoveTowardsAngle(_orbit, 0f, 90f * dt);
                }
                foreach (var r in _recs)
                {
                    if (r.Vc == null) continue;
                    if (r.Indoor && !Indoors) Restore(r); else Apply(r, r == live);
                }
                _footDistSet = false;
            }
            catch (Exception ex) { if (_errors++ < 5) Plugin.Logger.LogWarning($"[Camera] tick: {ex.Message}"); }
        }

        private static void Capture(GameManager gm)
        {
            _recs.Clear();
            _gm = gm;
            Add(gm.pedestrianCamera, false, false);
            Add(gm.indoorCamera, true, false);
            Add(gm.vehicleCamera, false, false);
            Add(gm.vehicleCameraReverse, false, true);
            Add(gm.indoorVehicleCamera, true, false);
            Add(gm.indoorVehicleCameraReverse, true, true);
            Plugin.Logger.LogInfo($"[Camera] {_recs.Count} game camera(s) found; RPG camera {(On ? "on" : "off")}.");
        }

        private static void Add(CinemachineVirtualCameraBase? b, bool indoor, bool reverse)
        {
            if (!(b is CinemachineVirtualCamera vc) || vc == null) return;
            var r = new Rec
            {
                Vc = vc, Indoor = indoor, Reverse = reverse,
                Ped = vc.GetComponent<PedestrianCam>(),
                Tr = vc.GetCinemachineComponent<CinemachineTransposer>(),
                Co = vc.GetCinemachineComponent<CinemachineComposer>(),
                Fov = vc.m_Lens.FieldOfView, Far = vc.m_Lens.FarClipPlane,
            };
            if (r.Ped == null && r.Tr == null) return;
            if (r.Ped != null) { r.POffset = r.Ped.offset; r.PMinMax = r.Ped.minMaxDistance; r.PChange = r.Ped.distanceChangeSpeed; r.PSens = r.Ped.mouseSensitivity; }
            if (r.Tr != null) r.TrOffset = r.Tr.m_FollowOffset;
            if (r.Co != null) { r.CoOffset = r.Co.m_TrackedObjectOffset; r.CoScreenX = r.Co.m_ScreenX; }
            _recs.Add(r);
        }

        private static void Apply(Rec r, bool live)
        {
            var vc = r.Vc;
            vc.m_Lens.FieldOfView = Fov;
            vc.m_Lens.FarClipPlane = r.Far * View;
            if (r.Ped != null)
            {
                var p = r.Ped;
                p.minMaxDistance = new Vector2(FOOT_MIN, r.Indoor ? Mathf.Max(r.PMinMax.y, 20f) : FOOT_MAX);
                if (!r.Indoor)
                {
                    // the game's wheel input runs for this camera even while another one is live (driving, indoors):
                    // only the live camera's zoom counts
                    if (!r.Applied || !live || _footDistSet) p.distance = FootDist;
                    else if (Mathf.Abs(p.distance - FootDist) > 0.01f) { FootDist = Mathf.Clamp(p.distance, FOOT_MIN, FOOT_MAX); Dirty(); }
                }
                p.distance = Mathf.Clamp(p.distance, p.minMaxDistance.x, p.minMaxDistance.y);
                p.distanceChangeSpeed = Mathf.Max(0.3f, p.distance * 0.1f);   // the zoom step grows with the distance
                p.mouseSensitivity = r.PSens * Sens;
                // never lower than 0.8 m above the feet
                float pitch = Mathf.Max(FootPitch, Mathf.Asin(Mathf.Min(1f, 0.8f / Mathf.Max(0.8f, p.distance))) * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                p.offset = new Vector3(0f, Mathf.Sin(pitch), -Mathf.Cos(pitch));
                if (r.Co != null)
                {
                    r.Co.m_TrackedObjectOffset = r.CoOffset + new Vector3(0f, Height, 0f);
                    r.Co.m_ScreenX = Mathf.Clamp(r.CoScreenX - Shoulder * 0.15f, 0.2f, 0.8f);
                }
            }
            else if (r.Tr != null)
            {
                float d = r.Indoor ? Mathf.Min(_carDistCur, r.TrOffset.magnitude) : _carDistCur;
                float h = Height * 0.7f;
                // never lower than 1.2 m above the road
                float pitch = Mathf.Max(CarPitch, Mathf.Asin(Mathf.Clamp01(Mathf.Max(0f, 1.2f - h) / d)) * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                var off = new Vector3(0f, h + Mathf.Sin(pitch) * d, -Mathf.Cos(pitch) * d);
                r.Tr.m_FollowOffset = Quaternion.Euler(0f, _orbit + (r.Reverse ? 180f : 0f), 0f) * off;
                if (r.Co != null) r.Co.m_TrackedObjectOffset = r.CoOffset + new Vector3(0f, h, 0f);
            }
            r.Applied = true;
        }

        private static void Restore(Rec r)
        {
            if (!r.Applied || r.Vc == null) return;
            r.Applied = false;
            r.Vc.m_Lens.FieldOfView = r.Fov;
            r.Vc.m_Lens.FarClipPlane = r.Far;
            if (r.Ped != null)
            {
                var p = r.Ped;
                p.offset = r.POffset; p.minMaxDistance = r.PMinMax; p.distanceChangeSpeed = r.PChange; p.mouseSensitivity = r.PSens;
                p.distance = Mathf.Clamp(p.distance, r.PMinMax.x, r.PMinMax.y);
            }
            if (r.Tr != null) r.Tr.m_FollowOffset = r.TrOffset;
            if (r.Co != null) { r.Co.m_TrackedObjectOffset = r.CoOffset; r.Co.m_ScreenX = r.CoScreenX; }
        }

        /// <summary>Right-drag (after the dead zone): up/down tilts, in the car sideways turns around it (the game turns
        /// the camera on foot itself). The wheel zooms in the car. The same blocks as PedestrianCam.UpdateInput.</summary>
        private static void HandleMouse(bool footLive, bool carLive)
        {
            bool blocked = FullMenu.IsOpen || CityMap.IsOpen || MiniMenu.IsOpen || BuildingPreview.isPreviewing
                           || GameManager.ShouldBlockKeyboardShortcuts() || MPChat.SuppressGameInput;
            Vector2 mp = Input.mousePosition;
            if (Input.GetMouseButtonDown(1)) { _rmb = !blocked && !OverUi(); _drag = false; _rmbStart = _rmbLast = mp; }
            if (!Input.GetMouseButton(1))
            {
                if (_drag) _lastTurn = Time.unscaledTime;
                _rmb = _drag = false;
            }
            else
            {
                if (_rmb && !blocked && (footLive || carLive))
                {
                    if (!_drag && (mp - _rmbStart).magnitude >= DeadZone) _drag = true;
                    if (_drag)
                    {
                        var d = mp - _rmbLast;
                        float k = 0.15f * Sens;
                        if (footLive) FootPitch = Mathf.Clamp(FootPitch - d.y * k, FOOT_PITCH_MIN, FOOT_PITCH_MAX);
                        else
                        {
                            CarPitch = Mathf.Clamp(CarPitch - d.y * k, CAR_PITCH_MIN, CAR_PITCH_MAX);
                            float turn = -d.x * k;   // the game's own sense of turning on foot (PedestrianCam.UpdateInput)
                            if (PedestrianCam.invertRotation) turn = -turn;
                            _orbit = Mathf.Repeat(_orbit + turn + 180f, 360f) - 180f;
                            _lastTurn = Time.unscaledTime;
                        }
                        if (Mathf.Abs(d.y) > 0f) Dirty();
                    }
                }
                _rmbLast = mp;   // also while blocked: no jump when the block ends mid-drag
            }
            if (carLive && !blocked && !PedestrianCam.blockCameraZoom && !OverUi())
            {
                float w = Input.mouseScrollDelta.y;
                if (Mathf.Abs(w) > 0.001f) { CarDist = Mathf.Clamp(CarDist * (1f - w * 0.08f), CAR_MIN, CAR_MAX); Dirty(); }
            }
        }

        private static bool OverUi()
        {
            try { return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(); } catch { return false; }
        }

        /// <summary>How fast the car camera's target moves (m/s of game time) - the turn swings back only while driving.</summary>
        private static float TargetSpeed(Transform? t)
        {
            if (t == null) { _hasLast = false; return 0f; }
            var pos = t.position;
            float dt = Time.deltaTime, v = 0f;
            if (_hasLast && dt > 0f) v = (pos - _lastPos).magnitude / dt;
            _lastPos = pos; _hasLast = true;
            return v;
        }

        internal static void SetOn(bool on)
        {
            On = on;
            Dirty();
            Notice(on ? ModKeys.Fix(Loc.T("gps_cam_on", "RPG camera on. The right mouse button turns, the mouse wheel zooms, F8 opens the settings."))
                      : Loc.T("gps_cam_off", "RPG camera off: the game's own camera is back."));
            Plugin.Logger.LogInfo($"[Camera] RPG camera {(on ? "on" : "off")}.");
        }

        private static void ShowHintOnce()
        {
            if (_hintShown || _worldSince < 0f || Time.unscaledTime - _worldSince < 15f) return;
            _hintShown = true;
            Dirty();
            if (!On) Notice(ModKeys.Fix(Loc.T("gps_cam_hint", "New: F6 switches on the RPG camera (third person, also in the car). F8 opens its settings.")));
        }

        private static void Notice(string text)
        {
            try { UI.Notification.Notifications.Show(UI.Notification.NotificationType.Info, text); } catch { }
        }

        // ── the settings window's rows ──
        // 0 angle on foot · 1 distance on foot · 2 angle in the car · 3 distance in the car · 4 target height
        // 5 shoulder offset · 6 mouse sensitivity · 7 field of view · 8 view distance · 9 right-click dead zone
        internal static readonly (string key, string en)[] Rows =
        {
            ("gps_cam_foot_pitch", "Angle on foot"), ("gps_cam_foot_dist", "Distance on foot"),
            ("gps_cam_car_pitch", "Angle in the car"), ("gps_cam_car_dist", "Distance in the car"),
            ("gps_cam_height", "Target height"), ("gps_cam_shoulder", "Shoulder offset"),
            ("gps_cam_sens", "Mouse sensitivity"), ("gps_cam_fov", "Field of view"),
            ("gps_cam_view", "View distance"), ("gps_cam_deadzone", "Right-click dead zone"),
        };

        internal static string Value(int i) => i switch
        {
            0 => $"{FootPitch:0}°",
            1 => $"{FootDist:0.0} m",
            2 => $"{CarPitch:0}°",
            3 => $"{CarDist:0.0} m",
            4 => $"{Height:0.0} m",
            5 => Shoulder.ToString("+0.0;-0.0;0.0"),
            6 => $"{Sens:0.0}×",
            7 => $"{Fov:0}°",
            8 => $"{View:0.00}×",
            _ => $"{DeadZone} px",
        };

        internal static void Step(int i, int dir)
        {
            switch (i)
            {
                case 0: FootPitch = Mathf.Clamp(Mathf.Round(FootPitch / 5f) * 5f + 5f * dir, FOOT_PITCH_MIN, FOOT_PITCH_MAX); break;
                case 1: FootDist = Mathf.Clamp(Mathf.Round(FootDist) + dir, FOOT_MIN, FOOT_MAX); _footDistSet = true; break;
                case 2: CarPitch = Mathf.Clamp(Mathf.Round(CarPitch / 5f) * 5f + 5f * dir, CAR_PITCH_MIN, CAR_PITCH_MAX); break;
                case 3: CarDist = Mathf.Clamp(Mathf.Round(CarDist) + 2f * dir, CAR_MIN, CAR_MAX); break;
                case 4: Height = Mathf.Clamp(Height + 0.1f * dir, 0f, 3f); break;
                case 5: Shoulder = Mathf.Clamp(Shoulder + 0.1f * dir, -1f, 1f); break;
                case 6: Sens = Mathf.Clamp(Sens + 0.1f * dir, 0.2f, 3f); break;
                case 7: Fov = Mathf.Clamp(Fov + 5f * dir, 30f, 90f); break;
                case 8: View = Mathf.Clamp(View + 0.25f * dir, 0.5f, 3f); break;
                default: DeadZone = Mathf.Clamp(DeadZone + dir, 0, 30); break;
            }
            Dirty();
        }

        internal static void Defaults()
        {
            FootPitch = D_FOOT_PITCH; FootDist = D_FOOT_DIST; CarPitch = D_CAR_PITCH; CarDist = D_CAR_DIST;
            Height = D_HEIGHT; Shoulder = 0f; Sens = 1f; Fov = D_FOV; View = 1f; DeadZone = 3;
            Recenter = true; Indoors = false; _footDistSet = true;
            Dirty();
        }

        internal static void Dirty() { if (_managed) return; _dirty = true; _dirtyAt = Time.unscaledTime; }

        /// <summary>SERVER-SETTINGS-1: the server's camera (AMP's "RPG camera" section) for this session; nothing is saved.</summary>
        private static void FromServer()
        {
            On = ServerRules.Module("ModuleCamera");
            FootPitch = ServerRules.Value("CamFootPitch", D_FOOT_PITCH, FOOT_PITCH_MIN, FOOT_PITCH_MAX);
            FootDist = ServerRules.Value("CamFootDist", D_FOOT_DIST, FOOT_MIN, FOOT_MAX);
            CarPitch = ServerRules.Value("CamCarPitch", D_CAR_PITCH, CAR_PITCH_MIN, CAR_PITCH_MAX);
            CarDist = ServerRules.Value("CamCarDist", D_CAR_DIST, CAR_MIN, CAR_MAX);
            Height = ServerRules.Value("CamHeight", D_HEIGHT, 0f, 3f);
            Shoulder = ServerRules.Value("CamShoulder", 0f, -1f, 1f);
            Sens = ServerRules.Value("CamSensitivity", 1f, 0.2f, 3f);
            Fov = ServerRules.Value("CamFov", D_FOV, 30f, 90f);
            View = ServerRules.Value("CamViewDistance", 1f, 0.5f, 3f);
            DeadZone = Mathf.RoundToInt(ServerRules.Value("CamDeadZone", 3f, 0f, 30f));
            Recenter = ServerRules.Flag("CamRecenter");
            Indoors = ServerRules.Flag("CamIndoors");
            _carDistCur = CarDist; _footDistSet = true; _dirty = false;
            Plugin.Logger.LogInfo($"[Camera] settings from the server: on={On}, foot {FootPitch:0}° / {FootDist:0.0} m, car {CarPitch:0}° / {CarDist:0.0} m, fov {Fov:0}.");
        }

        // ── persistence: written 2 s after the last change ──
        private static void SaveIfDirty()
        {
            if (!_dirty || Time.unscaledTime - _dirtyAt < 2f) return;
            _dirty = false;
            MPConfig.SetCameraSettings(string.Format(CultureInfo.InvariantCulture,
                "on={0};fp={1:0.#};fd={2:0.##};cp={3:0.#};cd={4:0.#};th={5:0.##};sh={6:0.##};se={7:0.##};fov={8:0};vd={9:0.##};rc={10};in={11};dz={12};hint={13}",
                On ? 1 : 0, FootPitch, FootDist, CarPitch, CarDist, Height, Shoulder, Sens, Fov, View,
                Recenter ? 1 : 0, Indoors ? 1 : 0, DeadZone, _hintShown ? 1 : 0));
        }

        private static void Load()
        {
            _loaded = true;
            // start from the defaults: values a server set earlier in this session must not stay
            On = false; FootPitch = D_FOOT_PITCH; FootDist = D_FOOT_DIST; CarPitch = D_CAR_PITCH; CarDist = D_CAR_DIST;
            Height = D_HEIGHT; Shoulder = 0f; Sens = 1f; Fov = D_FOV; View = 1f; DeadZone = 3; Recenter = true; Indoors = false;
            _footDistSet = true;
            foreach (var part in MPConfig.CameraSettings.Split(';'))
            {
                int eq = part.IndexOf('=');
                if (eq <= 0) continue;
                string k = part.Substring(0, eq).Trim(), v = part.Substring(eq + 1).Trim();
                float F(float def) => float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : def;
                switch (k)
                {
                    case "on": On = v == "1"; break;
                    case "fp": FootPitch = Mathf.Clamp(F(D_FOOT_PITCH), FOOT_PITCH_MIN, FOOT_PITCH_MAX); break;
                    case "fd": FootDist = Mathf.Clamp(F(D_FOOT_DIST), FOOT_MIN, FOOT_MAX); break;
                    case "cp": CarPitch = Mathf.Clamp(F(D_CAR_PITCH), CAR_PITCH_MIN, CAR_PITCH_MAX); break;
                    case "cd": CarDist = Mathf.Clamp(F(D_CAR_DIST), CAR_MIN, CAR_MAX); break;
                    case "th": Height = Mathf.Clamp(F(D_HEIGHT), 0f, 3f); break;
                    case "sh": Shoulder = Mathf.Clamp(F(0f), -1f, 1f); break;
                    case "se": Sens = Mathf.Clamp(F(1f), 0.2f, 3f); break;
                    case "fov": Fov = Mathf.Clamp(F(D_FOV), 30f, 90f); break;
                    case "vd": View = Mathf.Clamp(F(1f), 0.5f, 3f); break;
                    case "rc": Recenter = v != "0"; break;
                    case "in": Indoors = v == "1"; break;
                    case "dz": DeadZone = Mathf.Clamp(Mathf.RoundToInt(F(3f)), 0, 30); break;
                    case "hint": _hintShown = v == "1"; break;
                }
            }
            _carDistCur = CarDist;
        }
    }

    public partial class MPCanvasUI
    {
        private GameObject? _camRoot;
        private bool _camOpen;
        private readonly List<(RectTransform rt, int act, int idx)> _camHits = new List<(RectTransform, int, int)>();

        /// <summary>Main thread, every frame from Update: the camera itself, and F8 toggles its settings window in the world;
        /// while open the window owns the mouse.</summary>
        private void TickCameraModule()
        {
            RpgCamera.Tick();
            try
            {
                bool inWorld = !HeadlessHost.Active && SaveGameManager.Current != null && PlayerHelper.PlayerController != null && !GameManager.isCitySceneBeingUnloaded;
                if (!inWorld) { if (_camOpen) CamClose(); return; }
                if (!_camOpen)
                {
                    if (ModKeys.CameraSettings.Down() && !MPChat.SuppressGameInput && !GameManager.HasInputSelected())
                    {
                        if (ServerRules.Module("ModuleCamera")) CamOpen();   // SERVER-RULES-1
                        else ServerRules.ModuleOff(Loc.T("gps_cam_title", "RPG camera"));
                    }
                    return;
                }
                if (Input.GetKeyDown(KeyCode.Escape) || ModKeys.CameraSettings.Down()) { CamClose(); ConsumeEscape(); return; }
                if (!Input.GetMouseButtonDown(0)) return;
                var mp = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
                foreach (var (rt, act, idx) in _camHits)
                    if (rt != null && rt.gameObject.activeInHierarchy && RectHit(rt, mp)) { CamAct(act, idx); return; }
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Camera] window: {ex.Message}"); }
        }

        private void CamOpen()
        {
            _camOpen = true;
            CamRebuild();
            Plugin.Logger.LogInfo("[Camera] settings window opened.");
        }

        private void CamClose()
        {
            _camOpen = false;
            ShieldPressUntilRelease();   // NAV-WALK-CLICK-1
            _camHits.Clear();
            if (_camRoot != null) { try { Destroy(_camRoot); } catch { } _camRoot = null; }
        }

        // acts: 1 on/off · 2 row minus (idx) · 3 row plus (idx) · 4 swing back · 5 inside buildings · 6 defaults · 7 close
        private void CamAct(int act, int idx)
        {
            switch (act)
            {
                case 1: RpgCamera.SetOn(!RpgCamera.On); break;
                case 2: RpgCamera.Step(idx, -1); break;
                case 3: RpgCamera.Step(idx, +1); break;
                case 4: RpgCamera.Recenter = !RpgCamera.Recenter; RpgCamera.Dirty(); break;
                case 5: RpgCamera.Indoors = !RpgCamera.Indoors; RpgCamera.Dirty(); break;
                case 6: RpgCamera.Defaults(); break;
                case 7: CamClose(); return;
            }
            CamRebuild();
        }

        // ── the window (lobby kit, mock-up units) ──
        private void CamRebuild()
        {
            if (_canvasGO == null) return;
            if (_camRoot != null) { try { Destroy(_camRoot); } catch { } }
            _camHits.Clear();
            _camRoot = LMakeRoot("BAMP_CameraSettings");
            _camRoot.transform.SetAsLastSibling();
            const float W = 640f, LX = 26f, VX = 356f, VW = 110f, BX = W - 26f - 104f, ROW = 38f;
            float H = 60f + 46f + 26f + RpgCamera.Rows.Length * ROW + 2f * ROW + 74f;
            var card = LMakeCard(_camRoot.transform, "Card", Loc.T("gps_cam_title", "RPG camera"), W, H);
            card.sub.text = ModKeys.CameraSettings.Display();   // MOD-KEYS-1
            var ct = card.go.transform;
            string on = Loc.T("gps_cam_state_on", "On"), off = Loc.T("gps_cam_state_off", "Off");

            float y = 60f;
            LText(ct, ModKeys.Fix(Loc.T("gps_cam_switch", "RPG camera (F6)")), 15f, C_WHITE, LX, y, 300f, 36f, TextAlignmentOptions.Left, true);
            bool ro = RpgCamera.Managed;   // SERVER-SETTINGS-1: on a server the window only shows the admin's settings
            var sw = LButton(ct, "Switch", RpgCamera.On ? on : off, BX, y, 104f, 36f, ro ? 4 : RpgCamera.On ? 1 : 0, 12f); if (!ro) _camHits.Add((sw.rt, 1, 0));
            y += 46f;
            LText(ct, Loc.T("gps_cam_controls", "Right mouse button: turn and tilt · Mouse wheel: zoom"), 12f, L_MUTED, LX, y, W - 52f, 20f, TextAlignmentOptions.Left);
            y += 26f;

            for (int i = 0; i < RpgCamera.Rows.Length; i++, y += ROW)
            {
                LText(ct, Loc.T(RpgCamera.Rows[i].key, RpgCamera.Rows[i].en), 13f, C_WHITE, LX, y, VX - LX - 10f, 32f, TextAlignmentOptions.Left);
                LText(ct, RpgCamera.Value(i), 13f, L_MUTED, VX, y, VW, 32f, TextAlignmentOptions.Center);
                if (ro) continue;
                var mb = LButton(ct, "Minus" + i, "-", BX, y, 48f, 32f, 0, 13f); _camHits.Add((mb.rt, 2, i));
                var pb = LButton(ct, "Plus" + i, "+", BX + 56f, y, 48f, 32f, 0, 13f); _camHits.Add((pb.rt, 3, i));
            }

            LText(ct, Loc.T("gps_cam_recenter", "Swing back behind the car"), 13f, C_WHITE, LX, y, BX - LX - 10f, 32f, TextAlignmentOptions.Left);
            var rc = LButton(ct, "Recenter", RpgCamera.Recenter ? on : off, BX, y, 104f, 32f, ro ? 4 : RpgCamera.Recenter ? 1 : 0, 12f); if (!ro) _camHits.Add((rc.rt, 4, 0));
            y += ROW;
            LText(ct, Loc.T("gps_cam_indoors", "Also inside buildings"), 13f, C_WHITE, LX, y, BX - LX - 10f, 32f, TextAlignmentOptions.Left);
            var ib = LButton(ct, "Indoors", RpgCamera.Indoors ? on : off, BX, y, 104f, 32f, ro ? 4 : RpgCamera.Indoors ? 1 : 0, 12f); if (!ro) _camHits.Add((ib.rt, 5, 0));

            if (ro)
                LText(ct, Loc.T("gps_cam_server_note", "On this server the admin sets the camera in AMP. Zoom, tilt and turn work as usual but are not kept."),
                    12f, L_NOTICE, LX, H - 60f, W - 52f - 170f, 48f, TextAlignmentOptions.TopLeft, false, false, 0f, true);
            else { var df = LButton(ct, "Defaults", Loc.T("gps_cam_defaults", "Defaults"), LX, H - 54f, 150f, 40f, 0, 13f); _camHits.Add((df.rt, 6, 0)); }
            var cl = LButton(ct, "Close", Loc.T("gps_ui_close", "Close"), W - 26f - 150f, H - 54f, 150f, 40f, 0, 13f); _camHits.Add((cl.rt, 7, 0));
        }
    }
}
