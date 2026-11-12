using System;
using System.Collections.Concurrent;
using Helpers;
using UnityEngine;

namespace BigAmbitionsMP
{
    /// <summary>PASSENGER-RADIO-1 (user 2026-11-01: "the passenger doesn't hear the music in the car - whoever drives, his
    /// music has priority for the passengers"): the game's radio is one broadcast per machine (RadioPlayer, muted until
    /// the player gets into a car with a radio - GlobalEvents.onEnterVehicle). A rider sits in a ghost, so that event never
    /// fires and he hears nothing. Now the driver's machine says which station the car's radio plays (on entering, on a
    /// change, and every 20 s for riders who board later); a rider's radio is switched on and tuned to it while he sits in
    /// that car, and back to his own station (muted again, unless he wears headphones) when he gets out - the same steps
    /// the game takes on entering and leaving a car. The song plays from this machine's own broadcast clock, so a song may
    /// stand at another point than the driver's; the driver's own music files (LocalFiles) are not shared.
    /// PASSENGER-RADIO-2 (1.0.16.2, user 2026-11-01: "the driver's station goes over to the client; when he gets out, his
    /// own radio plays again"): getting out brings back the rider's own internet stream too (not only a game station),
    /// and with his "radio on foot" switched on it keeps playing out of the car.</summary>
    internal static class CarRadioSync
    {
        private static readonly ConcurrentDictionary<string, VehicleRadioPayload> _radioOf = new ConcurrentDictionary<string, VehicleRadioPayload>();
        private static string _sentVid = "", _sentUrl = "", _appliedUrl = "";
        private static int _sentStation = -2;
        private static bool _rideStream;
        private static float _sentAt;
        private static bool _riding;
        private static int _applied = -2;
        private static string _ownName = "", _ownUrl = "";   // the rider's own stream before he got in ("" = a game station)

        /// <summary>Any thread: a driver's station for a car.</summary>
        internal static void Receive(VehicleRadioPayload? p)
        {
            if (p == null || string.IsNullOrEmpty(p.VehicleId)) return;
            _radioOf[p.VehicleId] = p;
        }

        /// <summary>Main thread, every frame (MPCanvasUI).</summary>
        internal static void Tick()
        {
            try
            {
                if (!MPClient.InMpGame && !MPServer.IsRunning) { EndRide(); _radioOf.Clear(); return; }
                DriverTick();
                RiderTick();
            }
            catch { }
        }

        private static void DriverTick()
        {
            var car = VehicleHelper.GetCurrentVehicleBase() as CarController;
            if (car == null || !car.controlledByPlayer || !RadioPlayer.VehicleHasRadio(car)) { _sentVid = ""; return; }
            string vid = car.vehicleInstance?.id ?? "";
            if (vid.StartsWith("BAMP_", StringComparison.Ordinal)) vid = vid.Substring(5);   // a granted ghost: the real car's id
            if (vid.Length == 0) return;
            var rp = InstanceBehavior<GameManager>.Instance?.radioPlayer;
            if (rp == null) return;
            int st = (int)rp.GetCurrentStation();
            string url = RadioStreams.CurrentUrl;   // RADIO-STREAMS-1: an internet stream instead
            if (vid == _sentVid && st == _sentStation && url == _sentUrl && Time.unscaledTime - _sentAt < 20f) return;
            _sentVid = vid; _sentStation = st; _sentUrl = url; _sentAt = Time.unscaledTime;
            var p = new VehicleRadioPayload { VehicleId = vid, Station = st, StreamUrl = url, StreamName = RadioStreams.CurrentName };
            if (MPServer.IsRunning) MPServer.BroadcastVehicleRadio(p);
            else if (MPClient.IsConnected) MPClient.SendVehicleRadio(p);
        }

        private static void RiderTick()
        {
            string vid = PassengerRide.SeatedVehicleId;
            if (vid.Length == 0) { EndRide(); return; }
            if (!_radioOf.TryGetValue(vid, out var radio) || radio == null) return;   // no word from its driver (yet)
            int st = radio.Station;
            string url = radio.StreamUrl ?? "";
            if (st < 0 && url.Length == 0) return;
            var rp = InstanceBehavior<GameManager>.Instance?.radioPlayer;
            if (rp == null) return;
            if (!_riding)
            {
                _riding = true;
                _applied = -2;
                _ownName = RadioStreams.CurrentName; _ownUrl = RadioStreams.CurrentUrl;
                if (rp.IsMuted) rp.ToggleMute(isMute: false);
                try { InstanceBehavior<UI.UIs>.Instance.smartphoneUI?.radioControls.EnableUI(); } catch { }
                rp.onRadioToggle.Invoke(true);
                Plugin.Logger.LogInfo($"[Radio] riding in '{vid}': the driver's radio ({(url.Length > 0 ? "stream " + radio.StreamName : ((RadioStation)st).ToString())}).");
            }
            if (url.Length > 0)
            {
                if (url != _appliedUrl && RadioStreams.IsStreamUrl(url))
                {
                    _appliedUrl = url;
                    _applied = -2;
                    _rideStream = true;
                    if (RadioStreams.CurrentUrl != url) RadioStreams.Play(rp, radio.StreamName ?? "", url);   // the same stream plays on
                }
            }
            else if (st != _applied && Enum.IsDefined(typeof(RadioStation), st) && (RadioStation)st != RadioStation.LocalFiles)
            {
                _applied = st;
                _appliedUrl = "";
                _rideStream = false;
                if ((int)rp.GetCurrentStation() != st || RadioStreams.Active) rp.PlayStation((RadioStation)st);
            }
        }

        private static void EndRide()
        {
            if (!_riding) return;
            _riding = false;
            _applied = -2;
            _appliedUrl = "";
            try
            {
                var rp = InstanceBehavior<GameManager>.Instance?.radioPlayer;
                if (rp == null) return;
                var own = (RadioStation)PlayerPrefSettings.RadioStation;
                if (!Enum.IsDefined(typeof(RadioStation), own)) own = RadioStation.Pop;
                if (_ownUrl.Length > 0 && RadioStreams.IsStreamUrl(_ownUrl)) RadioStreams.Resume(rp, _ownName, _ownUrl);   // his own stream again
                else if (_rideStream && RadioStreams.Active) RadioStreams.End(rp, own);
                else if (rp.GetCurrentStation() != own) rp.PlayStation(own);
                _rideStream = false;
                _ownName = _ownUrl = "";
                // the game's rule on foot (muted without headphones) - unless the player's "radio on foot" keeps it playing
                if (!PlayerHelper.IsWearingHeadset() && !PlayerHelper.IsUsingVehicle && RadioStreams.AllowMute(true))
                {
                    rp.ToggleMute(isMute: true);
                    try { InstanceBehavior<UI.UIs>.Instance.smartphoneUI?.radioControls.DisableUI(); } catch { }
                    rp.onRadioToggle.Invoke(false);
                }
                Plugin.Logger.LogInfo("[Radio] ride over: own radio again.");
            }
            catch { }
        }
    }
}
