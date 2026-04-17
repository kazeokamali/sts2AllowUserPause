using System;
using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace Sts2AllowUserPause;

[ModInitializer(nameof(OnModLoaded))]
public static class MainFile
{
    internal const string ModId = "sts2AllowUserPause";
    private const string HarmonyId = "zed.sts2allowuserpause";

    private static bool _initialized;
    private static Harmony? _harmony;

    public static void OnModLoaded()
    {
        if (_initialized)
        {
            Log.Info("[Sts2AllowUserPause] Initialize skipped (already initialized).");
            return;
        }

        _initialized = true;
        Log.Info("[Sts2AllowUserPause] Bootstrap start.");
        Log.Info($"[Sts2AllowUserPause] Runtime: {RuntimeInformation.FrameworkDescription}, CLR={Environment.Version}");
        Assembly harmonyAssembly = typeof(Harmony).Assembly;
        Log.Info($"[Sts2AllowUserPause] Harmony assembly: {harmonyAssembly.Location} v{harmonyAssembly.GetName().Version}");

        _harmony ??= new Harmony(HarmonyId);
        _harmony.PatchAll();

        Log.Info("[Sts2AllowUserPause] Mod initialized.");
    }
}
