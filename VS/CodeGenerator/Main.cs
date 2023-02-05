using BepInEx;
using System.Diagnostics;
using System.IO;

namespace CodeGenerator
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class Main : BaseUnityPlugin
    {
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "Gorakh";
        public const string PluginName = "CodeGenerator";
        public const string PluginVersion = "1.0.0";

        internal static string ModFolder;

        void Awake()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            Log.Init(Logger);

            ModFolder = Path.GetDirectoryName(Info.Location);
#if DEBUG
            Log.Debug($"{nameof(ModFolder)}={ModFolder}");
#endif

            stopwatch.Stop();
            Log.Info_NoCallerPrefix($"Initialized in {stopwatch.Elapsed.TotalSeconds:F2} seconds");
        }
    }
}
