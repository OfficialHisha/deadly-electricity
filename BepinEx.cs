using BepInEx;
using BepInEx.Logging;
using DeadlyElectricity.Configuration;
using DeadlyElectricity.Switching;
using HarmonyLib;

namespace DeadlyElectricity
{
    [BepInPlugin("DeadlyElectricity", "Deadly Electricity", "1.0.3")]
    public class DeadlyElectricity : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        private void Awake()
        {
            Config.LoadConfiguration();

            Log = Logger;

            // Plugin startup logic
            LogModMessage(LogLevel.Info, "Loading harmony");
            Harmony harmony = new(Info.Metadata.GUID);

            LogModMessage(LogLevel.Info, "Applying Patches");
            harmony.PatchAll();

            // Optional multiplayer sync of the generator switch state (does nothing unless enabled in the config).
            GeneratorSwitchSync.Initialize(Info.Metadata.Version.ToString());

            LogModMessage(LogLevel.Info, $"Plugin {Info.Metadata.GUID} {Info.Metadata.Version} is loaded!");
        }

        private void Update()
        {
            // Sends the generator switch changes queued during the last frame, if any (one integer read otherwise).
            GeneratorSwitchSync.Flush();
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
