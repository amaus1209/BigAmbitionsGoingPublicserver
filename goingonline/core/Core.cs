using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

namespace GoingOnline
{
    /// <summary>The provider's life in a city: bound to the loaded save game, advanced every frame, written into the save
    /// right before the game writes it. Call <see cref="Tick"/> once per frame on the main thread.</summary>
    public static class Core
    {
        /// <summary>The provider of the loaded game; null before it is founded (or outside a city).</summary>
        public static IspState? S { get; internal set; }
        public static bool InCity { get; private set; }

        static GameInstance? _game;
        static CityManager? _city;
        static string _character = "";
        static float _cityAt;
        static double _lastMinute = -1;

        public static void Tick()
        {
            GameTypes.Ensure();   // GAME-TYPES-1 (cheap once done; the stand-alone mod has no earlier hook)
            try
            {
                var host = Go.Host;
                if (host == null || !host.Enabled) { if (InCity) Leave(); return; }
                GameInstance? game = null;
                CityManager? city = null;
                try { game = SaveGameManager.Current; city = InstanceBehavior<CityManager>.Instance; } catch { }
                if (game == null || city == null || !City.Ensure()) { if (InCity) Leave(); _cityAt = 0f; return; }
                if (_cityAt == 0f) { _cityAt = Time.unscaledTime; return; }
                if (Time.unscaledTime - _cityAt < 4f) return;   // let the game finish loading the city
                if (!InCity || !ReferenceEquals(city, _city) || (!ReferenceEquals(game, _game) && (game.characterId ?? "") != _character))
                    Enter(game, city);
                else if (!ReferenceEquals(game, _game)) _game = game;   // the same world in a new object (a server's copy): keep the provider

                var s = S;
                if (s != null)
                {
                    Sim.Advance(s);
                    double minute = (double)game.Day * 1440.0 + game.Hour * 60.0 + game.Minute;
                    if (_lastMinute >= 0 && minute > _lastMinute && minute - _lastMinute < 180)
                        Sim.PlayerWork(s, (float)((minute - _lastMinute) / 60.0));
                    _lastMinute = minute;
                }
                App.Tick();
                GameFound.Tick();   // GAME-TYPES-1: a "Cable provider" founded in BizMan founds the provider
                Lobby.Tick();       // GO-LOBBY-1: visitors, the spot and the logo in a customer center the player is in
                Screens.Tick();     // GO-SCREENS-1: the app on the workplaces' monitors
                DeskLinks.Tick();   // GO-DESK-LINK-1: computers and chairs of older template interiors join their desks
                GameUi.SummaryCount();   // GO-SUMMARY-1: the day's summary counts the cable provider
                try { Models.Tick(); } catch { }   // GO-LIGHTS-2: the racks' blinking LEDs
                try { Van.Tick(S); } catch { }     // GO-VAN-3..5: leased vans, trips to the customers, the beacons
                try { VanPins.Tick(S); } catch { } // GO-VAN-12: the leased vans on the city map
                Street.Tick(S);                    // GO-STREET-1: cabinets and road works in the street
            }
            catch (System.Exception ex) { Go.Warn("tick: " + ex); }
        }

        static void Enter(GameInstance game, CityManager city)
        {
            _game = game; _city = city; _character = game.characterId ?? "";
            InCity = true;
            S = SaveCarrier.Read(game);
            if (S != null) Gear.Migrate(S);   // a provider from before 0.6 keeps the equipment it bought in the app
            if (S != null) Places.Migrate(S);   // GO-HUBS-1: a provider from before 0.8 gets grace days for the new rules
            _lastMinute = -1;
            GlobalEvents.onSaveGame -= OnSave;
            GlobalEvents.onSaveGame += OnSave;
            App.Reset();
            Go.Log(S != null ? $"provider '{S.Company}' loaded: {Sim.Customers(S)} customers, {S.Sites.Count} buildings."
                             : "no provider in this game yet.");
        }

        static void Leave()
        {
            Lobby.Clear();
            Screens.Clear();
            GlobalEvents.onSaveGame -= OnSave;
            InCity = false; S = null; _game = null; _city = null; _character = ""; _cityAt = 0f;
            App.Reset();
            City.Forget();
        }

        /// <summary>The game is about to write the save (GlobalEvents.onSaveGame fires inside SaveGameManager.Save, before
        /// it serializes): the provider goes in.</summary>
        static void OnSave()
        {
            try
            {
                var game = SaveGameManager.Current;
                if (game != null && S != null && ReferenceEquals(game, _game)) SaveCarrier.Write(game, S);
            }
            catch (System.Exception ex) { Go.Warn("save: " + ex.Message); }
        }

        /// <summary>GO-DISSOLVE-1: the provider ends (Act.Dissolve has checked that no building of it is left) - out of the game
        /// and out of the save, so the next save no longer carries it; the app shows the founding page again.</summary>
        internal static void Dissolve()
        {
            S = null;
            _lastMinute = -1;
            var game = SaveGameManager.Current;
            if (game != null) SaveCarrier.Remove(game);
            App.Reset();
        }

        /// <summary>Founds the provider (Actions.Found has checked the money and the headend).</summary>
        internal static void Founded(IspState s)
        {
            S = s;
            var game = SaveGameManager.Current;
            if (game != null) SaveCarrier.Write(game, s);
        }
    }

    /// <summary>The provider in the save game: one entry "goingonline:state:" + base64(gzip(JSON)) in
    /// GameInstance.CollapsedCitymapFilterCategories. The game only asks that list whether a map filter group is folded
    /// (Contains by the group's name), so a foreign entry is never read as one of its own; the server's save handling
    /// carries it along with the rest of the save.</summary>
    internal static class SaveCarrier
    {
        const string Prefix = "goingonline:state:";
        static string _unreadable = "";

        public static IspState? Read(GameInstance game)
        {
            _unreadable = "";
            var list = game.CollapsedCitymapFilterCategories;
            if (list == null) return null;
            foreach (var e in list)
            {
                if (e == null || !e.StartsWith(Prefix)) continue;
                try
                {
                    var s = Decode(e.Substring(Prefix.Length));
                    if (s != null) { Fix(s); return s; }
                }
                catch (System.Exception ex) { Go.Warn("the saved provider could not be read: " + ex.Message); }
                _unreadable = e;   // kept as it is: never overwritten by an empty provider
                return null;
            }
            return null;
        }

        /// <summary>GO-DISSOLVE-1: no provider in this save any more.</summary>
        public static void Remove(GameInstance game)
        {
            game.CollapsedCitymapFilterCategories?.RemoveAll(e => e != null && e.StartsWith(Prefix));
            _unreadable = "";
        }

        public static void Write(GameInstance game, IspState s)
        {
            game.CollapsedCitymapFilterCategories ??= new List<string>();
            var list = game.CollapsedCitymapFilterCategories;
            list.RemoveAll(e => e != null && e.StartsWith(Prefix));
            list.Add(Prefix + Encode(s));
            _unreadable = "";
        }

        static string Encode(IspState s)
        {
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(s);
            using (var ms = new MemoryStream())
            {
                using (var gz = new GZipStream(ms, CompressionMode.Compress, true))
                {
                    var bytes = Encoding.UTF8.GetBytes(json);
                    gz.Write(bytes, 0, bytes.Length);
                }
                return System.Convert.ToBase64String(ms.ToArray());
            }
        }

        static IspState? Decode(string payload)
        {
            var raw = System.Convert.FromBase64String(payload);
            using (var ms = new MemoryStream(raw))
            using (var gz = new GZipStream(ms, CompressionMode.Decompress))
            using (var rd = new StreamReader(gz, Encoding.UTF8))
                return Newtonsoft.Json.JsonConvert.DeserializeObject<IspState>(rd.ReadToEnd());
        }

        /// <summary>Fields a save from an older version lacks.</summary>
        static void Fix(IspState s)
        {
            s.Hoods ??= new Dictionary<string, Hood>();
            s.Sites ??= new Dictionary<string, Site>();
            s.Tariffs ??= new List<Tariff>();
            s.Prices ??= new Prices();
            s.Stock ??= new Stock();
            s.PayTv ??= new List<PayTv>();
            s.Techs ??= new List<Tech>();
            s.LeaseIds ??= new List<string>();
            s.VanAway ??= new Dictionary<string, string>();
            s.VanHome ??= new Dictionary<string, float[]>();
            s.Style ??= new VanStyle();
            s.Candidates ??= new List<Candidate>();
            s.Tickets ??= new List<Ticket>();
            s.Books ??= new List<DayBook>();
            s.Today ??= new DayBook();
            s.Events ??= new List<string>();
            s.Flags ??= new List<string>();
            if (s.UpLoad == null || s.UpLoad.Length != 24) s.UpLoad = new float[24];
            foreach (var h in s.Hoods.Values)
            {
                if (h.Util == null || h.Util.Length != 24) h.Util = new float[24];
                if (h.FiberUtil == null || h.FiberUtil.Length != 24) h.FiberUtil = new float[24];
            }
            foreach (var site in s.Sites.Values) site.C ??= new List<Cohort>();
            foreach (var t in s.Tickets) t.Techs ??= new List<int>();
        }

        public static bool HasUnreadable => _unreadable.Length > 0;

        /// <summary>The self-test: the provider through the save's encoding and back (size of the entry, customers after).</summary>
        internal static (int chars, int customers) RoundTrip(IspState s)
        {
            string e = Encode(s);
            var back = Decode(e);
            if (back != null) Fix(back);
            return (e.Length, back == null ? -1 : Sim.Customers(back));
        }
    }
}
