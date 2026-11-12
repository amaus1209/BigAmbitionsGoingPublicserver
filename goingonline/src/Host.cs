using System.Linq;
using UnityEngine;

namespace GoingOnline.Standalone
{
    /// <summary>The stand-alone mod's side of the provider: logging through the mod loader, the full menu opened the way
    /// the phone's apps open it, the settings from the game's mod options.</summary>
    internal sealed class Host : IGoHost
    {
        static bool? _serverMod;
        static float _checkedAt = -100f;

        public string ModRoot => Entry.Root;
        public bool Enabled => !ServerModPresent();
        public void Log(string message) { try { if (Entry.Logger != null) Entry.Logger.Info(message); else Debug.Log(message); } catch { } }
        public void Warn(string message) { try { if (Entry.Logger != null) Entry.Logger.Warn(message); else Debug.LogWarning(message); } catch { } }

        public bool OpenFullMenu()
        {
            try
            {
                var uis = InstanceBehavior<UI.UIs>.Instance;
                if (uis == null || uis.smartphoneUI == null) return false;
                uis.smartphoneUI.OpenApp(AppName.Persona);   // what the phone's own icons do; our page takes over right after
                return true;
            }
            catch (System.Exception ex) { Warn("[GoingOnline] open the menu: " + ex.Message); return false; }
        }

        public void HideOtherPages() { }

        public int Percent(string id, int fallback) => Settings.Get(id, fallback);

        /// <summary>Going Public Server is loaded: it carries the provider itself, this mod steps back. Checked every few
        /// seconds until the answer is yes (mods load one after the other).</summary>
        public static bool ServerModPresent()
        {
            if (_serverMod == true) return true;
            if (Time.unscaledTime - _checkedAt < 5f && _serverMod.HasValue) return _serverMod.Value;
            _checkedAt = Time.unscaledTime;
            try { _serverMod = System.AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "BigAmbitionsMP"); }
            catch { _serverMod = false; }
            if (_serverMod == true) Debug.Log("[GoingOnline] Going Public Server is loaded and carries the provider - this mod stays idle.");
            return _serverMod.Value;
        }
    }
}
