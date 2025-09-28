using BepInEx;
using BepInEx.Logging;
using DeadlyElectricity.Configuration;
using HarmonyLib;

namespace DeadlyElectricity
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class DeadlyElectricity : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        private void Awake()
        {
            Config.LoadConfiguration();

            Log = Logger;

            // Plugin startup logic
            LogModMessage(LogLevel.Info, "Loading harmony");
            Harmony harmony = new(PluginInfo.PLUGIN_GUID);

            LogModMessage(LogLevel.Info, "Applying Patches");
            harmony.PatchAll();

            LogModMessage(LogLevel.Info, $"Plugin {PluginInfo.PLUGIN_GUID} {PluginInfo.PLUGIN_VERSION} is loaded!");
        }

        internal static void LogModMessage(LogLevel loglevel, string message)
        {
            switch (loglevel)
            {
                case LogLevel.Debug:
                    if (ConfigurationHelper.GetBool("DebugEnabled"))
                    {
                        Log.LogInfo($"[DEBUG] {message}");
                    }
                    break;
                case LogLevel.Error:
                    Log.LogInfo($"[ERROR] {message}");
                    break;
                case LogLevel.Warning:
                    Log.LogInfo($"[WARNING] {message}");
                    break;
                default:
                    Log.LogInfo($"[INFO] {message}");
                    break;
            }
        }
    }
}
