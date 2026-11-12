namespace BigAmbitionsMP
{
    /// <summary>GOING-ONLINE-1 (user 2026-11-06: "a new mod for Big Ambitions: an internet service provider and cable TV
    /// provider ... both" - its own mod and a module of this one): module 8, the provider of goingonline/core, compiled
    /// into this mod. Every player runs a provider of their own on their own machine, saved in their own save (which the
    /// server collects anyway). The admin's switch is AMP "ModuleGoingOnline"; a dedicated server has no player and runs
    /// none. The stand-alone Going Online mod steps back when this mod is loaded.</summary>
    internal static class GoingOnlineModule
    {
        static bool _hosted;
        static float _cityAt, _nextLook;
        static DateTime _selfTestStamp = DateTime.MinValue;
        static float _nextReport;
        static string _lastCompany = "\u0001";

        /// <summary>MAIN THREAD, every frame (MPCanvasUI, next to the Radio page) - single player too.</summary>
        /// <summary>The provider's host (log, mod folder) - from the main menu on, so the game types' registration can log.</summary>
        internal static void EnsureHost()
        {
            if (_hosted) return;
            _hosted = true;
            GoingOnline.Go.Host = new Host();
            GoingOnline.GameUi.InMultiplayerMod = true;
            // GO-PATCHES-1: the provider's patches live in goingonline/core/GamePatches.cs now (the stand-alone mod has them
            // too); Plugin patches them with the server mod's other classes, and the business pack rides along on two of them
            GoingOnline.GamePatches.AlsoOurType = t => BizPack.IsOurs(t);
            GoingOnline.GamePatches.AlsoOfferJobs = ra => BizPack.OfferJobs(ra);
            GoingOnline.GamePatches.Active = true;
            // GO-WHOLESALE-2: lines between players ride on this mod's connection
            GoingOnline.Peers.Offers = GoingOnlineLines.Offers;
            GoingOnline.Peers.SendBill = GoingOnlineLines.SendBill;
            GoingOnline.Peers.Me = () => GoingOnlineLines.InSession ? MPConfig.PlayerId : "";
            // GO-STREET-2: every player's cabinets in the street
            GoingOnline.Street.Remote = GoingOnlineStreet.Nets;
            GoingOnline.Street.Me = () => GoingOnlineLines.InSession ? MPConfig.PlayerId : "";
        }

        internal static void Tick()
        {
            try
            {
                EnsureHost();
                if (MPConfig.DedicatedHost) { SelfTest(); GoingOnlineLines.HostTick(); GoingOnlineStreet.HostTick(); return; }
                GoingOnline.Core.Tick();
                VanLiveries.Tick();   // GO-LIVERY-1: the player's van livery to the others, theirs for the ghosts of their vans
                Report();
                GoingOnlineLines.HostTick();   // GO-WHOLESALE-2: a playing host passes the offers and bills on
                GoingOnlineStreet.ClientTick(); // GO-STREET-2: the own network in the street to the others
                GoingOnlineStreet.HostTick();
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[GoingOnline] tick: {ex.Message}"); }
        }

        /// <summary>The developer's dry run (goingonline/core/SelfTest.cs) on a dedicated server whose mod folder holds
        /// goingonline-selftest.txt: half a minute after the world is up, and again whenever the file changes (its text may
        /// be the number of days). Nothing of it reaches the world or the save.</summary>
        static void SelfTest()
        {
            if (UnityEngine.Time.unscaledTime < _nextLook) return;
            _nextLook = UnityEngine.Time.unscaledTime + 5f;
            string flag = System.IO.Path.Combine(MPConfig.ModRootPath, "goingonline-selftest.txt");
            if (!System.IO.File.Exists(flag)) return;
            if (InstanceBehavior<CityManager>.Instance == null || SaveGameManager.Current == null) { _cityAt = 0f; return; }
            if (_cityAt == 0f) { _cityAt = UnityEngine.Time.unscaledTime; return; }
            if (UnityEngine.Time.unscaledTime - _cityAt < 30f) return;
            var stamp = System.IO.File.GetLastWriteTimeUtc(flag);
            if (stamp == _selfTestStamp) return;
            _selfTestStamp = stamp;
            // the text: the number of days, optionally the start money ("60 55000"; without it 55,000 $)
            int days = 40; float money = -1f;
            try
            {
                var parts = System.IO.File.ReadAllText(flag).Trim().Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0 && int.TryParse(parts[0], out int d)) days = Math.Max(1, Math.Min(400, d));
                if (parts.Length > 1 && float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float l)) money = Math.Max(0f, l);
            }
            catch { }
            GoingOnline.SelfTest.Run(days, money);
        }

        /// <summary>GOING-ONLINE-RANK-1 (user 2026-11-06: "Baue mit ein in der Rangliste, wenn jemand so eine Firma
        /// gründet"): once a minute, and at once when the company changes (founded, gone, another save), the player's
        /// provider goes to the host, which puts it on the ranking page. A host that plays records its own.</summary>
        static void Report()
        {
            bool client = MPClient.IsConnected, host = MPServer.IsRunning;
            if (!client && !host) return;
            var s = ServerRules.Module("ModuleGoingOnline") ? GoingOnline.Core.S : null;
            string company = s?.Company ?? "";
            // GO-WHOLESALE-2: an offer switched on or off, or a new price, goes out at once too
            string key = company + (s != null && s.OfferLines ? "|offer|" + s.OfferPrice.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) : "");
            float now = UnityEngine.Time.unscaledTime;
            if (now < _nextReport && key == _lastCompany) return;
            _nextReport = now + 60f;
            _lastCompany = key;
            var p = new ProviderReportPayload { PlayerId = MPConfig.PlayerId, Company = company };
            if (s != null)
            {
                p.Customers = GoingOnline.Sim.Customers(s);
                int buildings = 0;
                foreach (var site in s.Sites.Values) if (!site.Lost && (site.Coax || site.Fiber)) buildings++;
                p.Buildings = buildings;
                p.Hoods = s.Hoods.Count;
                p.Reputation = (float)Math.Round(s.Reputation);
                p.FoundedDay = (int)(s.FoundedHour / 24);
                if (s.OfferLines)
                {
                    var hoods = GoingOnline.Peers.MyHoods(s);
                    if (hoods.Count > 0)
                    {
                        p.OfferPrice = s.OfferPrice;
                        p.OfferHoods = hoods.ConvertAll(h => new LineOfferHood { Id = h.Id, Name = h.Name, Homes = h.Homes, Out = h.Out, Cong = h.Cong });
                    }
                }
            }
            if (host) MPServer.RecordProvider(MPConfig.PlayerId, p);
            else MPClient.SendProviderReport(p);
        }

        /// <summary>The phone's tile (PHONE-PAGES-1).</summary>
        internal static void OpenFromPhone()
        {
            if (ServerRules.Module("ModuleGoingOnline")) GoingOnline.App.Open(-1);
            else ServerRules.ModuleOff(Loc.T("go_app", "Going Online"));
        }

        /// <summary>A native app was chosen in the full menu (MPPatches' FullMenu.ShowApp postfix).</summary>
        internal static void HidePage()
        {
            try { GoingOnline.App.HidePage(); } catch { }
        }

        sealed class Host : GoingOnline.IGoHost
        {
            public string ModRoot => MPConfig.ModRootPath;
            public bool Enabled => !MPConfig.DedicatedHost && ServerRules.Module("ModuleGoingOnline");
            public void Log(string message) => Plugin.Logger.LogInfo(message);
            public void Warn(string message) => Plugin.Logger.LogWarning(message);
            public bool OpenFullMenu()
            {
                if (MPHubNativePage.OpenMenu()) return true;   // the Business page's way (it is there in a session)
                try
                {
                    var uis = InstanceBehavior<global::UI.UIs>.Instance;   // single player: what the phone's own icons do
                    if (uis == null || uis.smartphoneUI == null) return false;
                    uis.smartphoneUI.OpenApp(global::AppName.Persona);
                    return true;
                }
                catch { return false; }
            }
            public void HideOtherPages() { MPHubNativePage.HidePage(); RadioPhonePage.HidePage(); }
            public int Percent(string id, int fallback) => fallback;
        }
    }
}
