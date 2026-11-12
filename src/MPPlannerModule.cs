using AI.Customers.CustomerEntries;
using BigAmbitions.DayNightCycle;
using BigAmbitions.Items;
using Buildings.Schedule;
using Entities;
using Entities.Employee.JobDemands;
using Entities.Employee.JobDemands.Requirements;
using Helpers;
using Localizor;
using TMPro;
using UI.Smartphone;
using UnityEngine;

namespace BigAmbitionsMP
{
    /// <summary>
    /// MODULE-PLANNER-1 (Going Public Server 1.0.12, module 4 of the server's own features; replaces the BAScheduler mod
    /// with own code): Shift+F9 opens the "Planner" for the local player's own businesses (not headquarters, warehouses,
    /// other players' shops, shared or merged shops).
    ///   Schedules - every business is checked with the game's own data and rules: seat-hours without staff while open,
    ///     shifts while closed, double bookings, more than 12 h a day (the auto-fill's own limit), shifts on workstations
    ///     that no longer exist, employees without a shift, and every schedule wish the game itself reports as unmet
    ///     (JobDemand.Fulfilled). A rough staff estimate per skill compares the seat-hours with what the staff can give
    ///     (their weekly-hours and days wishes). "Auto-fill" runs the GAME's own schedule auto-fill (its OR-Tools solver,
    ///     ScheduleAutoFillerHelper.AutoFillSchedule) - with the game's own rule that an HR manager must have a shift -
    ///     one business at a time, after a backup that "Undo" restores.
    ///   Opening hours - a 7x24 grid of the business type's customer demand (BusinessType day x hour multipliers, the
    ///     numbers CustomerEntriesCalculator uses), a suggestion above a threshold, hours toggled by click, "Apply hours"
    ///     with the same backup.
    /// Multiplayer: these are the owner's own registrations, and BusinessSync's heartbeat already carries their opening
    /// hours and shifts to everyone; the duty stand-ins (BAMP_DUTY_*) in a day are never touched. Backups live in memory
    /// for the session.
    /// </summary>
    internal static class ShopPlanner
    {
        internal const int MAX_DAY_HOURS = 12;   // ScheduleAutoFiller.AddEmployeeMaxHoursPerDayConstraint

        internal sealed class Biz
        {
            public BuildingRegistration Reg = null!;
            public string Key = "", Name = "";
            public int Stations, OpenHours, Employees;
            public readonly List<string> Problems = new List<string>();
            public readonly List<string> Need = new List<string>();
        }

        private sealed class DayBackup
        {
            public DayOfWeekOrdered Day;
            public bool IsOpen;
            public List<OpeningHourSlot> Slots = new List<OpeningHourSlot>();
            public List<WorkShift> Shifts = new List<WorkShift>();
        }

        internal static readonly List<Biz> List = new List<Biz>();
        internal static int Version;   // bumped whenever the list or a status changes - the window rebuilds on it
        internal static string Status = "";
        internal static bool StatusBad;
        private static readonly Dictionary<string, List<DayBackup>> _backups = new Dictionary<string, List<DayBackup>>();
        private static readonly List<string> _queue = new List<string>();
        private static string? _fillingKey;
        private static ScheduleAutoFiller? _filler;
        private static float _fillStarted;

        internal static bool IsFilling(string key) => _fillingKey == key || _queue.Contains(key);
        internal static bool HasBackup(string key) => _backups.ContainsKey(key);

        internal static bool IsDuty(string? employeeId)
            => !string.IsNullOrEmpty(employeeId) && employeeId!.StartsWith(MPRegisterSync.SyntheticDutyEmployeeIdPrefix, StringComparison.Ordinal);

        /// <summary>The local player's own, plannable businesses.</summary>
        internal static void Scan()
        {
            List.Clear();
            var gi = SaveGameManager.Current;
            if (gi?.BuildingRegistrations != null)
            {
                foreach (var reg in gi.BuildingRegistrations)
                {
                    try
                    {
                        if (reg == null || !reg.RentedByPlayer || string.IsNullOrWhiteSpace(reg.BusinessName)) continue;
                        string t = reg.businessTypeName ?? "";
                        if (t.Length == 0 || t == "ba:businesstype_empty" || t == "ba:businesstype_headquarters" || t == "ba:businesstype_warehouse") continue;
                        if (GameStatePatcher.IsForeignPlayerBusiness(reg)) continue;
                        string key = GameStateReader.AddressKey(reg);
                        if (MergerFlip.IsFlipped(key)) continue;
                        if (reg.scheduleDays == null || reg.scheduleDays.Count == 0) continue;
                        var b = new Biz { Reg = reg, Key = key, Name = reg.BusinessName };
                        Check(b);
                        List.Add(b);
                    }
                    catch (Exception ex) { Plugin.Logger.LogWarning($"[Planner] scan '{reg?.BusinessName}': {ex.Message}"); }
                }
            }
            List.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            Version++;
        }

        internal static Biz? Find(string key) => List.Find(b => b.Key == key);

        internal static List<ScheduleDay> Days(BuildingRegistration reg)
        {
            var days = reg.scheduleDays.Where(d => d != null).ToList();
            days.Sort((a, b) => ((int)a.day).CompareTo((int)b.day));
            return days;
        }

        internal static string DayName(DayOfWeekOrdered d)
        {
            try { string s = d.GetLocalizeKey().GetLocalization(); if (!string.IsNullOrEmpty(s)) return s; } catch { }
            return d.ToString();
        }

        private static List<ItemInstance> Stations(BuildingRegistration reg)
        {
            var list = new List<ItemInstance>();
            try { reg.GetAssignableItems(list); } catch { }
            return list;
        }

        private static bool IsCleaning(ItemInstance s)
        {
            try { var sk = s.ItemCached?.suitableSkills; return sk != null && sk.Contains("ba:skill_cleaning"); } catch { return false; }
        }

        private static List<EmployeeInstance> Staff(BuildingRegistration reg)
        {
            try
            {
                return EmployeeHelper.GetEmployeeInstances(new EmployeeInstancesQueryInfo
                {
                    withAssignedAddress = reg.Address,
                    withoutSkills = new[] { "ba:skill_deliverydriver" },
                });
            }
            catch { return new List<EmployeeInstance>(); }
        }

        private static string Person(EmployeeInstance? e, string? id)
            => e?.characterData?.name is string n && n.Length > 0 ? n : (id ?? "?");

        private static string Loc2(string key)
        {
            // a missing key comes back as the (lower-cased) key itself
            try { string s = key.GetLocalization(); if (!string.IsNullOrEmpty(s) && !string.Equals(s, key, StringComparison.OrdinalIgnoreCase)) return s; } catch { }
            int c = key.LastIndexOf('_');
            return c >= 0 && c < key.Length - 1 ? key.Substring(c + 1) : key;
        }

        /// <summary>The schedule check and the staff estimate of one business, from the game's own data.</summary>
        internal static void Check(Biz b)
        {
            b.Problems.Clear(); b.Need.Clear();
            var reg = b.Reg;
            var days = Days(reg);
            var stations = Stations(reg);
            var staff = Staff(reg);
            var byId = new Dictionary<string, EmployeeInstance>();
            foreach (var e in staff) if (e != null && !string.IsNullOrEmpty(e.id)) byId[e.id] = e;
            var stationIds = new HashSet<string>(stations.Select(s => s.id));
            var seats = stations.Where(s => !IsCleaning(s)).ToList();
            b.Stations = stations.Count;
            b.Employees = staff.Count;
            b.OpenHours = days.Sum(d => d.isOpen ? d.GetHoursOpen : 0);

            var withShift = new HashSet<string>();
            foreach (var d in days)
            {
                string dn = DayName(d.day);
                var shifts = d.workShifts?.Where(w => w != null).ToList() ?? new List<WorkShift>();
                // seat-hours without staff while open
                int uncovered = 0;
                if (d.isOpen)
                    for (int h = 0; h < 24; h++)
                    {
                        if (!d.IsOpenAtHour(h)) continue;
                        foreach (var s in seats)
                            if (!shifts.Exists(w => w.itemInstanceId == s.id && !string.IsNullOrEmpty(w.employeeId) && w.IsHourInShift(h))) uncovered++;
                    }
                if (uncovered > 0) b.Problems.Add(Loc.T("gps_plan_p_uncovered", "{0}: {1} seat-hour(s) without staff while open.", dn, uncovered));

                var hoursByEmp = new Dictionary<string, int>();
                var busy = new Dictionary<string, HashSet<int>>();
                var doubled = new HashSet<string>();
                foreach (var w in shifts)
                {
                    if (string.IsNullOrEmpty(w.employeeId) || IsDuty(w.employeeId)) continue;
                    withShift.Add(w.employeeId);
                    byId.TryGetValue(w.employeeId, out var e);
                    string who = Person(e, w.employeeId);
                    if (!stationIds.Contains(w.itemInstanceId ?? ""))
                        b.Problems.Add(Loc.T("gps_plan_p_ghost", "{0}: {1} has a shift on a workstation that no longer exists.", dn, who));
                    bool closed = false;
                    for (int h = w.startingHour; h < w.endingHour; h++) if (!d.IsOpenAtHour(h)) closed = true;
                    if (closed)
                        b.Problems.Add(Loc.T("gps_plan_p_closed", "{0}: {1} works {2}:00-{3}:00 while the business is closed.", dn, who, w.startingHour, w.endingHour));
                    hoursByEmp[w.employeeId] = (hoursByEmp.TryGetValue(w.employeeId, out var hh) ? hh : 0) + Math.Max(0, w.endingHour - w.startingHour);
                    if (!busy.TryGetValue(w.employeeId, out var set)) busy[w.employeeId] = set = new HashSet<int>();
                    for (int h = w.startingHour; h < w.endingHour; h++)
                        if (!set.Add(h) && doubled.Add(w.employeeId))
                            b.Problems.Add(Loc.T("gps_plan_p_double", "{0}: {1} is booked twice at {2}:00.", dn, who, h));
                }
                foreach (var kv in hoursByEmp)
                    if (kv.Value > MAX_DAY_HOURS)
                        b.Problems.Add(Loc.T("gps_plan_p_daily", "{0}: {1} works {2} h (at most {3} h a day).", dn, Person(byId.TryGetValue(kv.Key, out var e2) ? e2 : null, kv.Key), kv.Value, MAX_DAY_HOURS));
            }

            foreach (var e in staff)
            {
                if (e == null) continue;
                string who = Person(e, e.id);
                if (!withShift.Contains(e.id)) { b.Problems.Add(Loc.T("gps_plan_p_idle", "{0} has no shift.", who)); continue; }
                if (e.demands == null) continue;
                foreach (var dn in e.demands)
                {
                    try
                    {
                        if (string.IsNullOrEmpty(dn) || !JobDemandHelper.ScheduleDemands.Contains(dn)) continue;
                        var jd = JobDemandHelper.GetByName(dn);
                        if (jd != null && !jd.Fulfilled(e, reg.scheduleDays))
                            b.Problems.Add(Loc.T("gps_plan_p_demand", "{0}: wish not met - {1}.", who, Loc2(dn)));
                    }
                    catch { }
                }
            }

            Estimate(b, days, seats, staff);
        }

        /// <summary>Rough staff estimate per skill: the seat-hours a week of the staffed workstations against the most the
        /// matching staff can give (weekly-hours wish, days wish x 12 h). A lower bound - the hours of the day do not
        /// enter it - so the auto-fill decides the real week.</summary>
        private static void Estimate(Biz b, List<ScheduleDay> days, List<ItemInstance> seats, List<EmployeeInstance> staff)
        {
            int openDays = days.Count(d => d.isOpen && d.GetHoursOpen > 0);
            if (seats.Count == 0 || openDays == 0) return;
            var groups = new List<(string[] skills, int need, int cap)>();
            foreach (var s in seats)
            {
                string[] sk = s.ItemCached?.suitableSkills ?? Array.Empty<string>();
                if (sk.Length == 0) continue;
                int i = groups.FindIndex(g => g.skills.SequenceEqual(sk));
                if (i < 0) { groups.Add((sk, 0, 0)); i = groups.Count - 1; }
                var g0 = groups[i]; groups[i] = (g0.skills, g0.need + b.OpenHours, g0.cap);
            }
            foreach (var e in staff)
            {
                if (e == null) continue;
                string prim = "";
                try { prim = e.GetPrimarySkill() ?? ""; } catch { }
                int i = groups.FindIndex(g => g.skills.Contains(prim));
                if (i < 0) i = groups.FindIndex(g => e.characterData?.skills?.Exists(x => x != null && g.skills.Contains(x.name)) == true);
                if (i < 0) continue;
                int maxH = 40, maxDays = 7;
                foreach (var dn in e.demands ?? new List<string>())
                {
                    try
                    {
                        var jd = JobDemandHelper.GetByName(dn);
                        if (jd is HoursWorkingPerWeek hw) maxH = hw.MaxHours;
                        else if (jd is DaysWorkingPerWeek dw) maxDays = dw.daysWorkingPerWeek;
                    }
                    catch { }
                }
                int give = Math.Min(maxH, Math.Min(maxDays, openDays) * MAX_DAY_HOURS);
                var g0 = groups[i]; groups[i] = (g0.skills, g0.need, g0.cap + give);
            }
            foreach (var g in groups)
            {
                string skill = Loc2(g.skills[0]);
                string line = Loc.T("gps_plan_need_line", "{0}: {1} h a week needed, your staff can give up to {2} h.", skill, g.need, g.cap);
                if (g.need > g.cap) line += " " + Loc.T("gps_plan_need_hire", "Hire about {0} more.", (g.need - g.cap + 39) / 40);
                else if (g.cap - g.need >= 40) line += " " + Loc.T("gps_plan_need_spare", "About {0} could be spare.", (g.cap - g.need) / 40);
                b.Need.Add(line);
            }
        }

        // ── auto-fill (the game's own), one business at a time ──

        /// <summary>The game's own rule for its auto-fill button (ScheduleDaySelectionController.UpdateHrManagerAvailability).</summary>
        internal static bool HasHrManager()
        {
            try
            {
                return EmployeeHelper.GetEmployeeInstances(new EmployeeInstancesQueryInfo
                {
                    withSkills = new[] { "ba:skill_hrmanager" },
                    excludeBeingReplaced = true,
                    isAssignedToAnyWorkShift = true,
                }).Count > 0;
            }
            catch { return false; }
        }

        internal static void QueueFill(IEnumerable<Biz> list)
        {
            if (!HasHrManager())
            {
                SetStatus(Loc.T("gps_plan_needs_hr", "Auto-fill needs an HR manager with a shift at your headquarters, as in the game's own schedule."), true);
                return;
            }
            int n = 0;
            foreach (var b in list)
            {
                if (IsFilling(b.Key)) continue;
                if (b.Employees == 0) { SetStatus(Loc.T("gps_plan_no_staff", "{0} has no employees to plan.", b.Name), true); continue; }
                _queue.Add(b.Key); n++;
            }
            if (n > 0) SetStatus(Loc.T("gps_plan_filling", "The game fills {0} schedule(s), one after another...", n + (_fillingKey != null ? 1 : 0)), false);
            Version++;
        }

        private static object? _game;

        /// <summary>Main thread, every frame in the world: starts the next queued fill once the running one is done. Another
        /// save (or none) drops the list, the queue and every backup - they belong to the save they were made in.</summary>
        internal static void Tick()
        {
            var gi = SaveGameManager.Current;
            if (!ReferenceEquals(gi, _game))
            {
                _game = gi;
                List.Clear(); _queue.Clear(); _backups.Clear(); _fillingKey = null; _filler = null; Status = "";
                Version++;
            }
            if (_fillingKey != null)
            {
                bool running = false;
                try
                {
                    var fillers = InstanceBehavior<UI.UIs>.Instance?.fullMenu?.schedule?._activeAutoFillers;
                    if (fillers != null)
                        foreach (var f in fillers)
                            if (f != null && GameStateReader.AddressKey(f.Registration) == _fillingKey) { running = true; _filler ??= f; }
                }
                catch { }
                if (running || Time.unscaledTime - _fillStarted < 1f) return;
                var b = Find(_fillingKey);
                string name = b?.Name ?? _fillingKey;
                bool partial = _filler != null && !_filler.isOptimal;
                Plugin.Logger.LogInfo($"[Planner] auto-fill of '{name}' finished (optimal={!partial}).");
                if (b != null) { try { Check(b); } catch { } }
                string msg = partial
                    ? Loc.T("gps_plan_filled_partial", "{0}: schedule filled, but not every hour or wish could be covered. Undo restores the previous one.", name)
                    : Loc.T("gps_plan_filled", "{0}: schedule filled. Undo restores the previous one.", name);
                SetStatus(msg, partial);
                Notice(partial ? UI.Notification.NotificationType.Warning : UI.Notification.NotificationType.Success, msg);
                _fillingKey = null; _filler = null;
                Version++;
            }
            if (_fillingKey != null || _queue.Count == 0) return;
            string key = _queue[0];
            _queue.RemoveAt(0);
            var biz = Find(key);
            if (biz == null || biz.Reg == null) { Version++; return; }
            try
            {
                Backup(biz);
                _fillingKey = key; _filler = null; _fillStarted = Time.unscaledTime;
                biz.Reg.AutoFillSchedule(null, null, warnIfUnassigned: false, fast: false, inhibitSuccessNotification: true);
                Plugin.Logger.LogInfo($"[Planner] auto-fill of '{biz.Name}' started (game's own solver).");
            }
            catch (Exception ex)
            {
                _fillingKey = null;
                SetStatus(Loc.T("gps_plan_failed", "{0}: that did not work ({1}).", biz.Name, ex.Message), true);
                Plugin.Logger.LogWarning($"[Planner] auto-fill '{biz.Name}': {ex}");
            }
            Version++;
        }

        // ── backup / undo ──

        private static void Backup(Biz b)
        {
            var list = new List<DayBackup>();
            foreach (var d in b.Reg.scheduleDays)
            {
                if (d == null) continue;
                list.Add(new DayBackup
                {
                    Day = d.day, IsOpen = d.isOpen,
                    Slots = d.openingHourSlots?.Where(s => s != null).Select(s => s.Clone()).ToList() ?? new List<OpeningHourSlot>(),
                    Shifts = d.workShifts?.Where(w => w != null && !IsDuty(w.employeeId)).Select(w => w.Clone()).ToList() ?? new List<WorkShift>(),
                });
            }
            _backups[b.Key] = list;
        }

        internal static void Undo(Biz b)
        {
            if (IsFilling(b.Key)) { SetStatus(Loc.T("gps_plan_busy_wait", "Wait until the running auto-fill is finished."), true); return; }
            if (!_backups.TryGetValue(b.Key, out var list)) { SetStatus(Loc.T("gps_plan_nothing_undo", "Nothing to undo for {0}.", b.Name), true); return; }
            var reg = b.Reg;
            var staff = Staff(reg);
            var ids = new HashSet<string>(staff.Where(e => e != null).Select(e => e.id));
            var stationIds = new HashSet<string>(Stations(reg).Select(s => s.id));
            int restored = 0, dropped = 0;
            foreach (var d in reg.scheduleDays)
            {
                if (d == null) continue;
                var bk = list.Find(x => x.Day == d.day);
                if (bk == null) continue;
                d.RemoveAllWorkShiftsThatMatchPredicate(w => !IsDuty(w.employeeId));   // the game's own removal does the employee bookkeeping
                d.isOpen = bk.IsOpen;
                d.openingHourSlots = bk.Slots.Select(s => s.Clone()).ToList();
                foreach (var w in bk.Shifts)
                {
                    bool empOk = string.IsNullOrEmpty(w.employeeId) || ids.Contains(w.employeeId);
                    if (!empOk || !stationIds.Contains(w.itemInstanceId ?? "")) { dropped++; continue; }
                    d.AddWorkShift(w.Clone()); restored++;
                }
            }
            AfterChange(reg, staff);
            _backups.Remove(b.Key);
            Check(b);
            SetStatus(Loc.T("gps_plan_undone", "{0}: previous schedule and opening hours restored.", b.Name)
                      + (dropped > 0 ? " " + Loc.T("gps_plan_undone_dropped", "{0} shift(s) left out (employee or workstation gone).", dropped) : ""), false);
            Plugin.Logger.LogInfo($"[Planner] undo '{b.Name}': {restored} shift(s) restored, {dropped} dropped.");
            Version++;
        }

        /// <summary>The bookkeeping the game does after its own schedule changes (ScheduleAutoFillerHelper.OnCompleted,
        /// BizManSchedule.OnDisable).</summary>
        private static void AfterChange(BuildingRegistration reg, List<EmployeeInstance> staff)
        {
            foreach (var e in staff)
            {
                if (e == null) continue;
                try { e.UpdateWeeklyHoursAndDays(); e.UpdateAssignedWorkStationItems(); } catch { }
            }
            try { reg.UpdateSecurityLevel(); } catch { }
            try { SaveGameManager.MarkChange(); } catch { }
            try { CustomerEntriesHelper.UpdateCustomerEntriesForPlayerBusiness(reg, TimeHelper.GetDayOfWeek()); } catch { }
            try { UI.Tasks.TasksUI.UpdateTasksFromBusiness(reg); } catch { }
            try { UI.Smartphone.Apps.BizMan.Schedule.ScheduleHelper.OnWorkShiftChanged?.Invoke(); } catch { }
            try { GameEvent.Invoke("ba:gameevent_changedbuildingregistration"); } catch { }
        }

        // ── opening hours ──

        /// <summary>Relative customer demand [day row, hour] of the business type, 0..1 of the busiest hour; null = no data.</summary>
        internal static float[,]? Demand(BuildingRegistration reg, List<ScheduleDay> days)
        {
            BusinessType? bt = null;
            try { bt = BusinessTypeHelper.GetData(reg); } catch { }
            if (bt?.dayFactorMultipliers == null || bt.hourlyFactorMultipliers == null || bt.hourlyFactorMultipliers.Count == 0) return null;
            var m = new float[days.Count, 24];
            float max = 0f;
            for (int r = 0; r < days.Count; r++)
            {
                float dm = bt.dayFactorMultipliers.Find(x => x != null && x.dayOfWeekOrdered == days[r].day)?.multiplier ?? 0f;
                for (int h = 0; h < 24; h++)
                {
                    float hm = 0f;
                    foreach (var f in bt.hourlyFactorMultipliers)
                        if (f != null && h >= f.startingHour && h <= f.endingHour - 1) { hm = f.multiplier; break; }
                    m[r, h] = Mathf.Max(0f, dm * hm);
                    max = Mathf.Max(max, m[r, h]);
                }
            }
            if (max <= 0f) return null;
            for (int r = 0; r < days.Count; r++) for (int h = 0; h < 24; h++) m[r, h] /= max;
            return m;
        }

        internal static bool[,] CurrentHours(List<ScheduleDay> days)
        {
            var o = new bool[days.Count, 24];
            for (int r = 0; r < days.Count; r++) for (int h = 0; h < 24; h++) o[r, h] = days[r].IsOpenAtHour(h);
            return o;
        }

        internal static void ApplyHours(Biz b, bool[,] plan)
        {
            if (IsFilling(b.Key)) { SetStatus(Loc.T("gps_plan_busy_wait", "Wait until the running auto-fill is finished."), true); return; }
            var days = Days(b.Reg);
            int total = 0; bool same = true;
            for (int r = 0; r < days.Count; r++) for (int h = 0; h < 24; h++) { if (plan[r, h]) total++; if (plan[r, h] != days[r].IsOpenAtHour(h)) same = false; }
            if (total == 0) { SetStatus(Loc.T("gps_plan_hours_empty", "Plan at least one opening hour."), true); return; }
            if (same) { SetStatus(Loc.T("gps_plan_hours_same", "{0}: these are already the opening hours.", b.Name), false); return; }
            Backup(b);
            for (int r = 0; r < days.Count; r++)
            {
                var slots = new List<OpeningHourSlot>();
                for (int h = 0; h < 24; h++)
                {
                    if (!plan[r, h]) continue;
                    int start = h;
                    while (h + 1 < 24 && plan[r, h + 1]) h++;
                    slots.Add(new OpeningHourSlot(start, h + 1));
                }
                days[r].openingHourSlots = slots;
                days[r].isOpen = slots.Count > 0;
            }
            AfterChange(b.Reg, Staff(b.Reg));
            Check(b);
            SetStatus(Loc.T("gps_plan_hours_applied", "{0}: opening hours set ({1} h a week). Auto-fill the schedule for the new hours; Undo restores the old ones.", b.Name, total), false);
            Plugin.Logger.LogInfo($"[Planner] opening hours of '{b.Name}' set: {total} h a week.");
            Version++;
        }

        internal static void SetStatus(string text, bool bad) { Status = text; StatusBad = bad; Version++; }

        private static void Notice(UI.Notification.NotificationType type, string text)
        {
            try { UI.Notification.Notifications.Show(type, text); } catch { }
        }
    }

    public partial class MPCanvasUI
    {
        private const int PL_BIZ_ROWS = 13, PL_LINES = 15;
        private GameObject? _plRoot;
        private bool _plOpen, _plHoursTab;
        private string _plSel = "";
        private int _plBizPage, _plLinePage, _plVersion = -1;
        private int _plThreshold = 40;           // % of the busiest hour
        private string _plDraftKey = "";
        private bool[,]? _plDraft;
        private readonly List<(RectTransform rt, int act, int idx)> _plHits = new List<(RectTransform, int, int)>();

        /// <summary>Main thread, every frame from Update: the fill queue, and Shift+F9 toggles the Planner window in the world
        /// (user ruling 2026-10-30: Shift+F9, not F9).</summary>
        private void TickPlanner()
        {
            if (HeadlessHost.Active) return;
            try
            {
                bool inWorld = SaveGameManager.Current != null && PlayerHelper.PlayerController != null && !GameManager.isCitySceneBeingUnloaded;
                if (!inWorld) { if (_plOpen) PlClose(); return; }
                ShopPlanner.Tick();
                if (!_plOpen)
                {
                    if (PlKey() && !MPChat.SuppressGameInput && !GameManager.HasInputSelected() && !FullMenu.IsOpen)
                    {
                        if (ServerRules.Module("ModulePlanner")) PlOpen();   // SERVER-RULES-1
                        else ServerRules.ModuleOff(Loc.T("gps_plan_title", "Planner"));
                    }
                    return;
                }
                if (Input.GetKeyDown(KeyCode.Escape) || PlKey()) { PlClose(); ConsumeEscape(); return; }
                if (_plVersion != ShopPlanner.Version) PlRebuild();
                if (!Input.GetMouseButtonDown(0)) return;
                var mp = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
                foreach (var (rt, act, idx) in _plHits)
                    if (rt != null && rt.gameObject.activeInHierarchy && RectHit(rt, mp)) { PlAct(act, idx); return; }
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Planner] tick: {ex.Message}"); }
        }

        private static bool PlKey() => ModKeys.Planner.Down();   // MOD-KEYS-1: Shift+F9 unless the player chose another key

        private void PlOpen()
        {
            _plOpen = true;
            ShopPlanner.Status = "";
            ShopPlanner.Scan();
            if (ShopPlanner.Find(_plSel) == null) _plSel = ShopPlanner.List.Count > 0 ? ShopPlanner.List[0].Key : "";
            PlRebuild();
            Plugin.Logger.LogInfo($"[Planner] window opened ({ShopPlanner.List.Count} business(es)).");
        }

        private void PlClose()
        {
            _plOpen = false;
            ShieldPressUntilRelease();   // NAV-WALK-CLICK-1
            _plHits.Clear();
            if (_plRoot != null) { try { Destroy(_plRoot); } catch { } _plRoot = null; }
        }

        // acts: 1 select business (idx) · 2/3 business page -/+ · 4 tab schedules · 5 tab hours · 6 check again
        //       7 auto-fill selected · 8 auto-fill all with problems · 9 undo · 10 open in BizMan · 11/12 line page -/+
        //       13 hour cell (idx = row*24+hour) · 14/15 threshold -/+ · 16 suggest · 17 reset to current · 18 apply hours
        //       19 close
        private void PlAct(int act, int idx)
        {
            var b = ShopPlanner.Find(_plSel);
            switch (act)
            {
                case 1: if (idx >= 0 && idx < ShopPlanner.List.Count) { _plSel = ShopPlanner.List[idx].Key; _plLinePage = 0; ShopPlanner.Status = ""; } break;
                case 2: _plBizPage = Math.Max(0, _plBizPage - 1); break;
                case 3: _plBizPage++; break;
                case 4: _plHoursTab = false; break;
                case 5: _plHoursTab = true; break;
                case 6: ShopPlanner.Status = ""; ShopPlanner.Scan(); break;
                case 7: if (b != null) ShopPlanner.QueueFill(new[] { b }); break;
                case 8:
                    var bad = ShopPlanner.List.Where(x => x.Problems.Count > 0 && x.Employees > 0).ToList();
                    if (bad.Count == 0) ShopPlanner.SetStatus(Loc.T("gps_plan_all_ok", "No business with problems to fill."), false);
                    else ShopPlanner.QueueFill(bad);
                    break;
                case 9: if (b != null) { ShopPlanner.Undo(b); _plDraftKey = ""; } break;
                case 10:
                    if (b != null)
                    {
                        PlClose();
                        try { InstanceBehavior<UI.UIs>.Instance.fullMenu.bizMan.Open(b.Reg.Address, "Schedule"); }
                        catch (Exception ex) { Plugin.Logger.LogWarning($"[Planner] open BizMan: {ex.Message}"); }
                        return;
                    }
                    break;
                case 11: _plLinePage = Math.Max(0, _plLinePage - 1); break;
                case 12: _plLinePage++; break;
                case 13: if (_plDraft != null && idx / 24 < _plDraft.GetLength(0)) _plDraft[idx / 24, idx % 24] = !_plDraft[idx / 24, idx % 24]; break;
                case 14: _plThreshold = Math.Max(5, _plThreshold - 5); PlSuggest(b); break;
                case 15: _plThreshold = Math.Min(95, _plThreshold + 5); PlSuggest(b); break;
                case 16: PlSuggest(b); break;
                case 17: _plDraftKey = ""; break;
                case 18: if (b != null && _plDraft != null) { ShopPlanner.ApplyHours(b, _plDraft); _plDraftKey = ""; } break;
                case 19: PlClose(); return;
            }
            PlRebuild();
        }

        private void PlSuggest(ShopPlanner.Biz? b)
        {
            if (b == null) return;
            var days = ShopPlanner.Days(b.Reg);
            var dem = ShopPlanner.Demand(b.Reg, days);
            if (dem == null) return;
            _plDraftKey = b.Key;
            _plDraft = new bool[days.Count, 24];
            for (int r = 0; r < days.Count; r++) for (int h = 0; h < 24; h++) _plDraft[r, h] = dem[r, h] > 0f && dem[r, h] * 100f >= _plThreshold;
        }

        // ── the window (lobby kit, mock-up units) ──
        private void PlRebuild()
        {
            _plVersion = ShopPlanner.Version;
            if (_canvasGO == null) return;
            if (_plRoot != null) { try { Destroy(_plRoot); } catch { } }
            _plHits.Clear();
            _plRoot = LMakeRoot("BAMP_Planner");
            _plRoot.transform.SetAsLastSibling();
            const float W = 1080f, H = 700f, LX = 26f, LW = 330f, RX = 376f, RW = W - RX - 26f;
            var card = LMakeCard(_plRoot.transform, "Card", Loc.T("gps_plan_title", "Planner"), W, H);
            card.sub.text = ModKeys.Planner.Display();   // MOD-KEYS-1
            var ct = card.go.transform;

            // tabs
            var t1 = LButton(ct, "TabSched", Loc.T("gps_plan_tab_schedule", "Schedules"), RX, 56f, 200f, 34f, _plHoursTab ? 0 : 1, 12f); _plHits.Add((t1.rt, 4, 0));
            var t2 = LButton(ct, "TabHours", Loc.T("gps_plan_tab_hours", "Opening hours"), RX + 208f, 56f, 200f, 34f, _plHoursTab ? 1 : 0, 12f); _plHits.Add((t2.rt, 5, 0));

            // business list
            var list = ShopPlanner.List;
            if (list.Count == 0)
                LText(ct, Loc.T("gps_plan_no_business", "You have no businesses to plan yet (shops with workstations; not headquarters or warehouses)."), 13f, L_MUTED, LX, 60f, LW, 80f, TextAlignmentOptions.TopLeft, false, false, 0f, true);
            int pages = Math.Max(1, (list.Count + PL_BIZ_ROWS - 1) / PL_BIZ_ROWS);
            _plBizPage = Mathf.Clamp(_plBizPage, 0, pages - 1);
            for (int r = 0; r < PL_BIZ_ROWS; r++)
            {
                int i = _plBizPage * PL_BIZ_ROWS + r;
                if (i >= list.Count) break;
                var b = list[i];
                string st = ShopPlanner.IsFilling(b.Key) ? Loc.T("gps_plan_status_busy", "filling...")
                          : b.Problems.Count == 0 ? Loc.T("gps_plan_status_ok", "OK")
                          : Loc.T("gps_plan_status_n", "{0} problem(s)", b.Problems.Count);
                var row = PlRow(ct, LX, 60f + r * 34f, LW, 30f, b.Name, st, b.Key == _plSel, b.Problems.Count > 0 && !ShopPlanner.IsFilling(b.Key));
                _plHits.Add((row, 1, i));
            }
            float ly = 60f + PL_BIZ_ROWS * 34f + 4f;
            if (pages > 1) { SgPagerTo(_plHits, ct, LX, ly, LW, _plBizPage, pages, 2, 3); ly += 34f; }
            var ck = LButton(ct, "Check", Loc.T("gps_plan_check", "Check again"), LX, H - 104f, LW, 36f, 0, 12f); _plHits.Add((ck.rt, 6, 0));
            var fa = LButton(ct, "FillAll", Loc.T("gps_plan_autofill_all", "Auto-fill all with problems"), LX, H - 62f, LW, 36f, 0, 12f); _plHits.Add((fa.rt, 8, 0));

            var sel = ShopPlanner.Find(_plSel);
            if (sel != null)
            {
                LText(ct, sel.Name, 16f, C_WHITE, RX, 100f, RW, 26f, TextAlignmentOptions.Left, true);
                LText(ct, Loc.T("gps_plan_facts", "{0} workstation(s), {1} open hours a week, {2} employee(s)", sel.Stations, sel.OpenHours, sel.Employees),
                    12f, L_MUTED, RX, 126f, RW, 20f, TextAlignmentOptions.Left);
                if (_plHoursTab) PlHoursPanel(ct, sel, RX, 154f, RW, H);
                else PlSchedulePanel(ct, sel, RX, 154f, RW, H);
            }

            if (ShopPlanner.Status.Length > 0)
                LText(ct, ShopPlanner.Status, 12f, ShopPlanner.StatusBad ? L_ERR : L_NOTICE, RX, H - 104f, RW - 170f, 40f, TextAlignmentOptions.TopLeft, false, false, 0f, true);
            var cl = LButton(ct, "Close", Loc.T("gps_ui_close", "Close"), W - 26f - 150f, H - 62f, 150f, 36f, 0, 12f); _plHits.Add((cl.rt, 19, 0));
        }

        private void PlSchedulePanel(Transform ct, ShopPlanner.Biz b, float x, float y, float w, float H)
        {
            var lines = new List<(string text, Color c)>();
            lines.Add((Loc.T("gps_plan_problems", "Problems"), C_WHITE));
            if (b.Problems.Count == 0) lines.Add((Loc.T("gps_plan_ok", "No problems found."), L_MUTED));
            foreach (var p in b.Problems) lines.Add(("• " + p, L_NOTICE));
            if (b.Need.Count > 0)
            {
                lines.Add(("", L_MUTED));
                lines.Add((Loc.T("gps_plan_need_title", "Staff estimate (rough, per skill)"), C_WHITE));
                foreach (var n in b.Need) lines.Add(("• " + n, L_MUTED));
            }
            int pages = Math.Max(1, (lines.Count + PL_LINES - 1) / PL_LINES);
            _plLinePage = Mathf.Clamp(_plLinePage, 0, pages - 1);
            for (int r = 0; r < PL_LINES; r++)
            {
                int i = _plLinePage * PL_LINES + r;
                if (i >= lines.Count) break;
                var t = LText(ct, lines[i].text, 12f, lines[i].c, x, y + r * 22f, w, 22f, TextAlignmentOptions.Left, lines[i].c == C_WHITE);
                t.overflowMode = TextOverflowModes.Ellipsis;
            }
            float by = y + PL_LINES * 22f + 6f;
            if (pages > 1) { SgPagerTo(_plHits, ct, x, by, 300f, _plLinePage, pages, 11, 12); }
            by += 40f;
            bool filling = ShopPlanner.IsFilling(b.Key);
            var af = LButton(ct, "Fill", Loc.T("gps_plan_autofill", "Auto-fill"), x, by, 200f, 36f, filling ? 4 : 1, 12f); if (!filling) _plHits.Add((af.rt, 7, 0));
            var un = LButton(ct, "Undo", Loc.T("gps_plan_undo", "Undo"), x + 208f, by, 160f, 36f, ShopPlanner.HasBackup(b.Key) && !filling ? 0 : 4, 12f); if (ShopPlanner.HasBackup(b.Key) && !filling) _plHits.Add((un.rt, 9, 0));
            var bz = LButton(ct, "BizMan", Loc.T("gps_plan_open_bizman", "Open in BizMan"), x + 376f, by, 200f, 36f, 0, 12f); _plHits.Add((bz.rt, 10, 0));
        }

        private void PlHoursPanel(Transform ct, ShopPlanner.Biz b, float x, float y, float w, float H)
        {
            var days = ShopPlanner.Days(b.Reg);
            var dem = ShopPlanner.Demand(b.Reg, days);
            var now = ShopPlanner.CurrentHours(days);
            if (_plDraftKey != b.Key || _plDraft == null || _plDraft.GetLength(0) != days.Count) { _plDraftKey = b.Key; _plDraft = (bool[,])now.Clone(); }
            if (dem == null)
                LText(ct, Loc.T("gps_plan_hours_nodata", "The game has no customer data for this business type. You can still set the hours by clicking the grid."), 12f, L_MUTED, x, y, w, 36f, TextAlignmentOptions.TopLeft, false, false, 0f, true);
            const float DL = 58f, CW = 24f, CH = 26f;
            float gy = y + 44f;
            for (int h = 0; h < 24; h += 3)
                LText(ct, h.ToString(), 10f, L_MUTED, x + DL + h * CW, gy - 18f, CW * 2f, 16f, TextAlignmentOptions.Left);
            var demandCol = new Color(60f / 255f, 142f / 255f, 211f / 255f, 1f);
            for (int r = 0; r < days.Count; r++)
            {
                string dn = ShopPlanner.DayName(days[r].day);
                LText(ct, dn.Length > 3 ? dn.Substring(0, 3) : dn, 11f, C_WHITE, x, gy + r * CH, DL - 6f, CH - 2f, TextAlignmentOptions.Left);
                for (int h = 0; h < 24; h++)
                {
                    float v = dem != null ? dem[r, h] : 0f;
                    var cell = LImg(ct, "H", Color.Lerp(L_ROW, demandCol, v), null, 0f).rectTransform;
                    LPos(cell, x + DL + h * CW, gy + r * CH, CW - 2f, CH - 2f);
                    if (now[r, h]) LPos(LImg(cell, "Now", Color.white, null, 0f).rectTransform, 0f, 0f, CW - 2f, 3f);
                    if (_plDraft![r, h]) LPos(LImg(cell, "Plan", new Color(0.45f, 0.85f, 0.45f, 1f), null, 0f).rectTransform, 0f, CH - 8f, CW - 2f, 6f);
                    _plHits.Add((cell, 13, r * 24 + h));
                }
            }
            float ty = gy + days.Count * CH + 10f;
            LText(ct, Loc.T("gps_plan_hours_legend", "Darker = more customers · white line: open now · green line: planned. Click an hour to add it to the plan or take it out."),
                11f, L_MUTED, x, ty, w, 32f, TextAlignmentOptions.TopLeft, false, false, 0f, true);
            ty += 36f;
            if (dem != null)
            {
                (int hours, int pct) Sum(bool[,] o)
                {
                    float all = 0f, got = 0f; int n = 0;
                    for (int r = 0; r < days.Count; r++) for (int h = 0; h < 24; h++) { all += dem[r, h]; if (o[r, h]) { got += dem[r, h]; n++; } }
                    return (n, all > 0f ? Mathf.RoundToInt(got * 100f / all) : 0);
                }
                var a = Sum(now); var p = Sum(_plDraft!);
                LText(ct, Loc.T("gps_plan_hours_now", "Open now: {0} h a week, {1}% of the weekly customer demand.", a.hours, a.pct), 12f, C_WHITE, x, ty, w, 20f, TextAlignmentOptions.Left);
                LText(ct, Loc.T("gps_plan_hours_planned", "Planned: {0} h a week, {1}% of the weekly customer demand.", p.hours, p.pct), 12f, C_WHITE, x, ty + 22f, w, 20f, TextAlignmentOptions.Left);
                ty += 50f;
                LText(ct, Loc.T("gps_plan_hours_threshold", "Suggest: every hour with at least {0}% of the busiest hour", _plThreshold), 12f, L_MUTED, x, ty, w - 120f, 32f, TextAlignmentOptions.Left);
                var mb = LButton(ct, "ThMinus", "-", x + w - 104f, ty, 48f, 32f, 0, 13f); _plHits.Add((mb.rt, 14, 0));
                var pb = LButton(ct, "ThPlus", "+", x + w - 48f, ty, 48f, 32f, 0, 13f); _plHits.Add((pb.rt, 15, 0));
                ty += 40f;
            }
            float bx = x;
            if (dem != null) { var sg = LButton(ct, "Suggest", Loc.T("gps_plan_hours_suggest", "Suggest"), bx, ty, 180f, 36f, 0, 12f); _plHits.Add((sg.rt, 16, 0)); bx += 188f; }
            var rs = LButton(ct, "Reset", Loc.T("gps_plan_hours_reset", "Reset to current"), bx, ty, 180f, 36f, 0, 12f); _plHits.Add((rs.rt, 17, 0)); bx += 188f;
            bool filling = ShopPlanner.IsFilling(b.Key);
            var ap = LButton(ct, "Apply", Loc.T("gps_plan_hours_apply", "Apply hours"), bx, ty, 180f, 36f, filling ? 4 : 1, 12f); if (!filling) _plHits.Add((ap.rt, 18, 0));
            bx += 188f;
            var un = LButton(ct, "Undo", Loc.T("gps_plan_undo", "Undo"), bx, ty, 110f, 36f, ShopPlanner.HasBackup(b.Key) && !filling ? 0 : 4, 12f); if (ShopPlanner.HasBackup(b.Key) && !filling) _plHits.Add((un.rt, 9, 0));
        }

        private RectTransform PlRow(Transform p, float x, float y, float w, float h, string name, string status, bool on, bool bad)
        {
            var bg = LImg(p, "Row", on ? L_BTN[1] : L_ROW, LRoundSprite(false), 3f);
            LPos(bg.rectTransform, x, y, w, h);
            var t = LText(bg.transform, name, 13f, C_WHITE, 10f, 0f, w - 130f, h, TextAlignmentOptions.Left);
            t.overflowMode = TextOverflowModes.Ellipsis;
            LText(bg.transform, status, 11f, bad ? L_NOTICE : L_MUTED, w - 120f, 0f, 110f, h, TextAlignmentOptions.Right);
            return bg.rectTransform;
        }

        /// <summary>A pager (like the Signs window's) that writes its hits into the given list.</summary>
        private void SgPagerTo(List<(RectTransform rt, int act, int idx)> hits, Transform p, float x, float y, float w, int page, int pages, int actPrev, int actNext)
        {
            var pb = LButton(p, "Prev", "<", x, y, 44f, 28f, page > 0 ? 0 : 4, 12f); if (page > 0) hits.Add((pb.rt, actPrev, 0));
            LText(p, $"{page + 1} / {pages}", 12f, L_MUTED, x + 50f, y, w - 100f, 28f, TextAlignmentOptions.Center);
            var nb = LButton(p, "Next", ">", x + w - 44f, y, 44f, 28f, page < pages - 1 ? 0 : 4, 12f); if (page < pages - 1) hits.Add((nb.rt, actNext, 0));
        }
    }
}
