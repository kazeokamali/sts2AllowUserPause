using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;

namespace Sts2AllowUserPause.Patches;

[HarmonyPatch]
public static class RunTransitionPatch
{
    [HarmonyPatch(typeof(NGame), nameof(NGame.ReturnToMainMenu))]
    [HarmonyPrefix]
    public static void ReturnToMainMenuPrefix()
    {
        DeckPauseController.PrepareForRunExit();
    }

    [HarmonyPatch(typeof(RunManager), nameof(RunManager.CleanUp))]
    [HarmonyPrefix]
    public static void CleanUpPrefix()
    {
        DeckPauseController.Reset();
    }
}
