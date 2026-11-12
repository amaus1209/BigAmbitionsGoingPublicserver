using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Steamworks;

namespace BigAmbitionsMP
{
    /// <summary>
    /// DEDICATED-HOST-1 (client side): when a host that requires identical mods turns this player away, the refusal carries
    /// the host's Workshop mod ids ("|ws=id,id,..." after the names, see MPContentFingerprint.AppendWorkshopIds). With the
    /// player's explicit opt-in (config key AutoDownloadHostMods = "true") this subscribes to those items and waits for Steam
    /// to download them. The game loads mods at start, so the player then restarts Big Ambitions, enables them under Mods
    /// and joins again - a mod cannot be hot-loaded into a running game by this code.
    ///
    /// SAFETY: a Workshop mod is code that runs on the player's machine, so a host must never be able to install one
    /// silently. This is OFF unless the player turned it on, capped at <see cref="MaxItems"/> items per refusal, and it only
    /// ever subscribes - it never enables or runs anything. Only the ids the host's own refusal named are touched.
    /// </summary>
    public static class ModAutoDownload
    {
        private const int MaxItems = 12;
        private const int WaitSeconds = 300;
        private static int _running;

        /// <summary>The Workshop ids from a "BAMP:mods:..." refusal tag ("ws=" field), empty when there are none.</summary>
        public static List<ulong> ParseIds(string tag)
        {
            var ids = new List<ulong>();
            try
            {
                if (string.IsNullOrEmpty(tag)) return ids;
                foreach (var part in tag.Split('|'))
                {
                    if (!part.StartsWith("ws=", StringComparison.Ordinal)) continue;
                    foreach (var s in part.Substring(3).Split(','))
                        if (ulong.TryParse(s.Trim(), out ulong id) && id != 0 && !ids.Contains(id) && ids.Count < MaxItems)
                            ids.Add(id);
                }
            }
            catch { }
            return ids;
        }

        /// <summary>Called by the client when the host refuses over mods. Returns a sentence to append to the "couldn't join"
        /// text ("" when the tag names no Workshop mods).</summary>
        public static string OnRefusalTag(string tag)
        {
            var ids = ParseIds(tag);
            if (ids.Count == 0) return "";
            if (!MPConfig.AutoDownloadHostMods)
                return $" The host uses {ids.Count} Workshop mod(s). To download them automatically, set \"AutoDownloadHostMods\" to \"true\" in {System.IO.Path.GetFileName(MPConfig.ConfigPath)} and join again.";
            Start(ids);
            return " Downloading the host's Workshop mods now. When Steam is done, restart Big Ambitions, enable them under Mods and join again.";
        }

        private static void Start(List<ulong> ids)
        {
            if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
            {
                Plugin.Logger.LogInfo("[ModDownload] a download is already running.");
                return;
            }
            Task.Run(() => RunAsync(ids));
        }

        private static async Task RunAsync(List<ulong> ids)
        {
            try
            {
                if (!SteamClient.IsValid)
                {
                    Plugin.Logger.LogWarning("[ModDownload] Steam is not available - nothing downloaded.");
                    return;
                }
                var wanted = new List<Steamworks.Ugc.Item>();
                foreach (ulong id in ids)
                {
                    try
                    {
                        var found = await Steamworks.Ugc.Item.GetAsync(new Steamworks.Data.PublishedFileId { Value = id });
                        if (!found.HasValue) { Plugin.Logger.LogWarning($"[ModDownload] Workshop item {id} was not found."); continue; }
                        var item = found.Value;
                        if (item.IsInstalled && !item.NeedsUpdate)
                        {
                            Plugin.Logger.LogInfo($"[ModDownload] '{item.Title}' ({id}) is already installed.");
                            continue;
                        }
                        bool subscribed = await item.Subscribe();
                        item.Download(true);
                        wanted.Add(item);
                        Plugin.Logger.LogInfo($"[ModDownload] '{item.Title}' ({id}): subscribed={subscribed}, download started.");
                    }
                    catch (Exception ex) { Plugin.Logger.LogWarning($"[ModDownload] item {id}: {ex.Message}"); }
                }
                if (wanted.Count == 0)
                {
                    Notify("The host's Workshop mods are already installed. Restart Big Ambitions, enable them under Mods and join again.");
                    return;
                }
                for (int waited = 0; waited < WaitSeconds; waited += 2)
                {
                    bool done = true;
                    foreach (var item in wanted)
                        if (!item.IsInstalled || item.IsDownloading || item.IsDownloadPending) { done = false; break; }
                    if (done)
                    {
                        Plugin.Logger.LogInfo($"[ModDownload] {wanted.Count} mod(s) downloaded.");
                        Notify($"{wanted.Count} mod(s) downloaded. Restart Big Ambitions, enable them under Mods and join again.");
                        return;
                    }
                    await Task.Delay(2000);
                }
                Plugin.Logger.LogWarning("[ModDownload] timed out waiting for Steam - the download may still finish in the background.");
                Notify("Steam is still downloading the host's mods. Wait for it to finish, then restart Big Ambitions and join again.");
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[ModDownload] {ex.Message}"); }
            finally { Interlocked.Exchange(ref _running, 0); }
        }

        private static void Notify(string text)
        {
            try { GameStatePatcher.EnqueueOnMainThread(() => { try { PassengerHud.Toast(text, 30f); } catch { } }); }
            catch { }
        }
    }
}
