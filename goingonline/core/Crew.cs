using BigAmbitions.DayNightCycle;
using BigAmbitions.Characters.Skills;
using UnityEngine;

namespace GoingOnline
{
    /// <summary>GAME-TYPES-1: the staff are the game's own employees. The city's recruitment agencies offer the provider's
    /// jobs (GameTypes: cable technician, switching staff, network master, field sales, technical support; the game's own
    /// customer service), the player hires them there, gives them a workplace (Going Online's workplaces from the office
    /// furniture stores) and a shift in BizMan, and the game pays them. Every game hour the provider's list (IspState.Techs)
    /// follows the employees assigned to its buildings: who has which job, how skilled, on duty or not. What the game does
    /// not know stays the provider's own: how friendly someone is with customers (their customer-service skill, else a
    /// value of their own), the customer-care training, the complaints. Staff hired in the app before 0.6 stay until they
    /// are let go. In the developer's dry run nothing of this runs.</summary>
    internal static class Crew
    {
        static HashSet<string> OurKeys(IspState s)
        {
            var keys = new HashSet<string>(Places.AllKeys(s));   // GO-HUBS-1: the headend, the hubs and the customer centers
            if (s.HeadendKey.Length > 0) keys.Add(s.HeadendKey);
            if (s.CenterKey.Length > 0) keys.Add(s.CenterKey);
            // GO-STAFF-2 (user 2026-11-10: "Techniker sind eingestellt und Kundendienstler, aber zählt nicht unter
            // Personal"): every building the player rents with the cable business or a customer center - also one the
            // app does not know as a hub yet
            try
            {
                var g = SaveGameManager.Current;
                if (g?.BuildingRegistrations != null)
                    foreach (var r in g.BuildingRegistrations)
                        if (r != null && r.RentedByPlayer && GameTypes.IsOurs(r.businessTypeName)) keys.Add(City.KeyOf(r.Address));
            }
            catch { }
            return keys;
        }

        public const int NameLimit = 30;

        /// <summary>GO-RENAME-1 (user 2026-11-12: "Mitarbeiter Namen ändern wenn sie eingestellt sind nur bei going online
        /// mod"): a hired person of the provider gets a new name. It is the game's own name (EmployeeInstance.characterData.name,
        /// saved with the game), so BizMan and MyEmployees show it too, and Sync keeps it. Only people the provider's list holds
        /// - nobody else's employees. Empty or unchanged = nothing happens.</summary>
        public static void Rename(Tech t, string name)
        {
            name = (name ?? "").Trim();
            if (name.Length > NameLimit) name = name.Substring(0, NameLimit).Trim();
            if (name.Length == 0 || name == t.Name || t.Emp.Length == 0) return;
            try
            {
                var list = SaveGameManager.Current?.EmployeeInstances;
                var emp = list?.Find(e => e != null && e.id == t.Emp);
                if (emp?.characterData == null) { Go.Warn("rename: the employee is no longer in the game (GO-RENAME-1)."); return; }
                string old = emp.characterData.name ?? "";
                emp.characterData.name = name;
                t.Name = name;
                Go.Log($"staff: '{old}' is now called '{name}' (GO-RENAME-1).");
            }
            catch (System.Exception ex) { Go.Warn("rename: " + ex.Message + " (GO-RENAME-1)."); }
        }

        static string _diag = "";
        static int _diagLogs;

        /// <summary>GO-HQ-1: the player's headquarters - network masters may work there too.</summary>
        static HashSet<string> HqKeys()
        {
            var keys = new HashSet<string>();
            var g = SaveGameManager.Current;
            if (g?.BuildingRegistrations == null) return keys;
            foreach (var r in g.BuildingRegistrations)
                if (r != null && r.RentedByPlayer && r.businessTypeName == GameTypes.Hq) keys.Add(City.KeyOf(r.Address));
            return keys;
        }

        /// <summary>The provider's list follows the game's employees (every game hour; the real game only).</summary>
        public static void Sync(IspState s)
        {
            if (Sim.Test) return;
            try
            {
                var keys = OurKeys(s);
                var hq = HqKeys();
                var seen = new HashSet<string>();
                int withJob = 0, unplaced = 0;
                var away = new SortedDictionary<string, int>();
                // GO-STAFF-3 (user 2026-11-10: "Techniker sind eingestellt und Kundendienstler, aber zählt nicht unter
                // Personal"): the hired people are the save's employee list - the game no longer sets EmployeeInstance.hired
                // ([Obsolete] since build 9427561b; EmployeeHelper.HireCandidate only moves the candidate into that list), and
                // the dictionary also holds the agencies' candidates
                var hiredList = SaveGameManager.Current?.EmployeeInstances;
                if (hiredList == null) return;
                foreach (var emp in hiredList)
                {
                    if (emp == null) continue;
                    var skills = emp.characterData?.skills;
                    if (skills == null) continue;
                    // GO-STAFF-2: who has one of the provider's jobs, and where the game has them - for the log below
                    bool cableJob = false;
                    foreach (var sk in skills) if (sk != null && GameTypes.RoleOf(sk.name) != 255 && sk.name != GameTypes.SkService) { cableJob = true; break; }
                    if (cableJob) withJob++;
                    if (emp.assignedAddress == null) { if (cableJob) unplaced++; continue; }
                    string at = City.KeyOf(emp.assignedAddress);
                    bool ours = keys.Contains(at), atHq = !ours && hq.Contains(at);
                    if (!ours && !atHq)
                    {
                        if (cableJob) away[at] = away.TryGetValue(at, out int n0) ? n0 + 1 : 1;
                        continue;
                    }
                    Skill? job = null;
                    if (atHq) { if (skills.Count > 0 && skills[0] != null && skills[0].name == GameTypes.SkMaster) job = skills[0]; }   // at the headquarters only network masters, by their main job
                    else foreach (var sk in skills) if (sk != null && GameTypes.RoleOf(sk.name) != 255) { job = sk; break; }
                    if (job == null) continue;
                    seen.Add(emp.id);
                    var t = s.Techs.Find(x => x.Emp == emp.id);
                    if (t == null)
                    {
                        t = new Tech { Id = s.NextId++, Emp = emp.id, Manner = Manner(emp, skills) };
                        s.Techs.Add(t);
                        Sim.Event(s, Go.T("go_event_crew_joined", "{0} works for you now ({1}).", emp.characterData!.name ?? "", Act.RoleName(GameTypes.RoleOf(job.name))));
                    }
                    t.Role = GameTypes.RoleOf(job.name);
                    t.Name = emp.characterData!.name ?? "";
                    t.Skill = Mathf.Clamp(Mathf.RoundToInt(job.value), 1, 100);
                    t.Wage = emp.hourlyWage;
                    t.Base = at;   // GO-STAFF-1: the building the person works for
                    // GO-HUBS-1: a network master in a hub looks after its neighbourhood; at the headend or the headquarters the
                    // one the player chose
                    if (t.Role == Cat.RoleMaster && ours && City.ByKey.TryGetValue(at, out var hb) && hb.Hood != Places.HeadHood(s) && Places.IsOffice(s, at)) t.Hood = hb.Hood;
                    if (t.Role == Cat.RoleMaster && t.Hood.Length == 0) t.Hood = FirstHood(s);
                }
                foreach (var t in s.Techs.FindAll(x => x.Emp.Length > 0 && !seen.Contains(x.Emp)))
                {
                    Act.NextShift(s, t);   // frees the tickets the person held
                    s.Techs.Remove(t);
                    Sim.Event(s, Go.T("go_event_crew_left", "{0} no longer works for you.", t.Name));
                }
                // GO-STAFF-2: when what the game has differs from the team, say where they are (a few times a session)
                string diag = $"{withJob}|{seen.Count}|{unplaced}|{string.Join(",", away)}";
                if (diag != _diag && _diagLogs < 8)
                {
                    _diag = diag; _diagLogs++;
                    var where = new List<string>();
                    foreach (var kv in away)
                    {
                        string type = "";
                        try { type = City.Reg(kv.Key)?.businessTypeName ?? ""; } catch { }
                        where.Add($"{kv.Value} at '{kv.Key}' ({(type.Length > 0 ? type : "no business")}{(City.Mine(kv.Key) ? ", yours" : "")})");
                    }
                    Go.Log($"crew: {seen.Count} in the team; {withJob} hired with a cable job (customer service not counted), {unplaced} of them without a workplace building"
                         + (where.Count > 0 ? ", elsewhere: " + string.Join("; ", where) : "") + $"; the provider's buildings: {string.Join(", ", keys)} (GO-STAFF-2).");
                }
            }
            catch (System.Exception ex) { Go.Warn("crew: " + ex.Message); }
        }

        static string FirstHood(IspState s)
        {
            foreach (var h in s.Hoods.Keys) return h;
            return "";
        }

        /// <summary>How friendly with customers: the customer-service skill (1..5), else a value of the person's own.</summary>
        static int Manner(Entities.EmployeeInstance emp, List<Skill> skills)
        {
            foreach (var sk in skills)
                if (sk != null && sk.name == "ba:skill_customerservice") return Mathf.Clamp(1 + Mathf.FloorToInt(sk.value / 22f), 1, 5);
            int h = 0;
            foreach (char c in emp.id ?? "") h = h * 31 + c;
            int r = Mathf.Abs(h) % 100;
            return r < 4 ? 1 : r < 14 ? 2 : r < 60 ? 3 : r < 90 ? 4 : 5;
        }

        /// <summary>On duty this hour: a shift at a workplace in one of the provider's buildings today, and not off sick.</summary>
        public static bool OnDuty(IspState s, Tech t, int hour) => Duty(s, t, hour) == 0;

        /// <summary>Why a person of the game's is not at work this hour (0: at work). 3 no shift now or off sick; GO-WORK-2 (as
        /// the game's office businesses run, user 2026-11-07: "simuliere ... die Einrichtung, welche Funktionsweise nötig ist,
        /// bis es funktional läuft"): 4 the shift's workplace is not complete - the game's computer needs a desk and a chair
        /// (its furniture requirements, ItemHelper.HasAnyMissingRequirements, the same check the game's office simulator
        /// makes); 5 the cable office is not set up yet - its requirements in BizMan (CMTS, work computers, toilets, sinks)
        /// are not all met (GO-OFFICE-2; a headend of 0.6 in a warehouse is spared).</summary>
        public static byte Duty(IspState s, Tech t, int hour)
        {
            try
            {
                var emp = Helpers.EmployeeHelper.GetEmployeeById(t.Emp, false);
                if (emp == null || emp.isAbsent || emp.assignedAddress == null) return 3;
                var reg = City.Reg(City.KeyOf(emp.assignedAddress));
                if (reg?.scheduleDays == null) return 3;
                var today = TimeHelper.GetDayOfWeek();
                foreach (var day in reg.scheduleDays)
                {
                    if (day == null || day.day != today || day.workShifts == null) continue;
                    foreach (var ws in day.workShifts)
                    {
                        if (ws == null || ws.employeeId != t.Emp || ws.startingHour > hour || ws.endingHour <= hour) continue;
                        if (reg.itemInstances != null && !string.IsNullOrEmpty(ws.itemInstanceId)
                            && reg.itemInstances.TryGetValue(ws.itemInstanceId, out var station) && station != null
                            && ItemHelper.HasAnyMissingRequirements(station)) return 4;
                        if (GameTypes.IsOurs(reg.businessTypeName) && GameTypes.InOffice(reg) && !Helpers.BusinessHelper.AreAllRequirementsMet(reg)) return 5;   // offices only - a customer center has no such requirements
                        return 0;
                    }
                }
            }
            catch { }
            return 3;
        }

        /// <summary>The provider's staff in the game, per role (for the Staff tab).</summary>
        public static int Count(IspState s, byte role) => s.Techs.Count(t => t.Emp.Length > 0 && t.Role == role);
    }
}
