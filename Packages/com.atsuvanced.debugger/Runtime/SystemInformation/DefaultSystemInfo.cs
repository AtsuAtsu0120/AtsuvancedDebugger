using System;
using System.Globalization;
using UnityEngine;

namespace Atsuvanced.Debugger
{
    /// <summary>Populates the built-in System Info categories.</summary>
    /// <summary>The built-in system information (application, device, display, graphics ...).</summary>
    public static class DefaultSystemInfo
    {
        /// <summary>Adds the built-in entries to <paramref name="r" /> (done by <c>SystemInfoFeature</c>).</summary>
        public static void Populate(SystemInfoRegistry r)
        {
            const string app = "Application";
            r.Add(InfoEntry.Create("Product", Application.productName), app);
            r.Add(InfoEntry.Create("Version", Application.version), app);
            r.Add(InfoEntry.Create("Identifier", Application.identifier), app);
            r.Add(InfoEntry.Create("Unity Version", Application.unityVersion), app);
            r.Add(InfoEntry.Create("Platform", Application.platform), app);
            r.Add(InfoEntry.Create("Is Debug Build", Debug.isDebugBuild), app);
            // Dynamic values build their text (or box) only when the value changes (the tab polls them every second).
            r.Add(InfoEntry.Create("Target Frame Rate", Cached(() => Application.targetFrameRate, v => v)), app);
            r.Add(InfoEntry.Create("Time Since Startup",
                Cached(() => (long)(Time.realtimeSinceStartup * 10f),
                    v => (v / 10.0).ToString("F1", CultureInfo.InvariantCulture) + " s")), app);

            const string device = "Device";
            r.Add(InfoEntry.Create("Model", SystemInfo.deviceModel), device);
            r.Add(InfoEntry.Create("Type", SystemInfo.deviceType), device);
            r.Add(InfoEntry.Create("Name", SystemInfo.deviceName, true), device);
            r.Add(InfoEntry.Create("Unique Identifier", SystemInfo.deviceUniqueIdentifier, true), device);
            r.Add(InfoEntry.Create("Operating System", SystemInfo.operatingSystem), device);
            r.Add(InfoEntry.Create("Processor", SystemInfo.processorType), device);
            r.Add(InfoEntry.Create("Processor Count", SystemInfo.processorCount), device);
            r.Add(InfoEntry.Create("System Memory", SystemInfo.systemMemorySize + " MB"), device);
            r.Add(
                InfoEntry.Create("Battery",
                    Cached(() => (int)SystemInfo.batteryStatus * 1000 + Mathf.RoundToInt(SystemInfo.batteryLevel * 100f),
                        v => (BatteryStatus)(v / 1000) + " " + (v % 1000).ToString(CultureInfo.InvariantCulture) + "%")),
                device);
            r.Add(InfoEntry.Create("Language",
                Cached(() => (int)Application.systemLanguage, v => ((SystemLanguage)v).ToString())), device);

            const string graphics = "Graphics";
            r.Add(InfoEntry.Create("Device Name", SystemInfo.graphicsDeviceName), graphics);
            r.Add(InfoEntry.Create("Device Type", SystemInfo.graphicsDeviceType), graphics);
            r.Add(InfoEntry.Create("Device Version", SystemInfo.graphicsDeviceVersion), graphics);
            r.Add(InfoEntry.Create("Memory", SystemInfo.graphicsMemorySize + " MB"), graphics);
            r.Add(InfoEntry.Create("Shader Level", SystemInfo.graphicsShaderLevel), graphics);
            r.Add(InfoEntry.Create("Max Texture Size", SystemInfo.maxTextureSize), graphics);
            // QualitySettings.names allocates a new array on every call.
            r.Add(InfoEntry.Create("Quality Level",
                Cached(QualitySettings.GetQualityLevel, i => QualitySettings.names[i])), graphics);

            const string screen = "Screen";
            r.Add(InfoEntry.Create("Resolution",
                Cached(() => (long)Screen.width << 32 | (uint)Screen.height,
                    v => (v >> 32).ToString(CultureInfo.InvariantCulture) + " x " +
                         (v & 0xffffffff).ToString(CultureInfo.InvariantCulture))), screen);
            r.Add(InfoEntry.Create("DPI", Cached(() => Screen.dpi, v => v)), screen);
            r.Add(InfoEntry.Create("Safe Area", Cached(() => Screen.safeArea, v => v)), screen);
            r.Add(InfoEntry.Create("Orientation",
                Cached(() => (int)Screen.orientation, v => ((ScreenOrientation)v).ToString())), screen);
            r.Add(InfoEntry.Create("Full Screen Mode",
                Cached(() => (int)Screen.fullScreenMode, v => ((FullScreenMode)v).ToString())), screen);
            r.Add(
                InfoEntry.Create("Refresh Rate",
                    Cached(() => Math.Round(Screen.currentResolution.refreshRateRatio.value * 100.0),
                        v => (v / 100.0).ToString("F2", CultureInfo.InvariantCulture) + " Hz")), screen);
        }

        /// <summary>
        ///     A getter that reads <paramref name="read" /> every time but builds the shown value (string / boxed value)
        ///     only when it changes, so polling an unchanged value allocates nothing.
        /// </summary>
        private static Func<object> Cached<T>(Func<T> read, Func<T, object> make) where T : struct, IEquatable<T>
        {
            var has = false;
            T last = default;
            object shown = null;
            return () =>
            {
                var v = read();
                if (!has || !v.Equals(last))
                {
                    has = true;
                    last = v;
                    shown = make(v);
                }

                return shown;
            };
        }
    }
}