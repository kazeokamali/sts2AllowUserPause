using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace Sts2AllowUserPause.Patches;

[HarmonyPatch]
public static class DeathGracePatches
{
    [HarmonyPatch(typeof(CombatManager), nameof(CombatManager.LoseCombat))]
    [HarmonyPostfix]
    public static void LoseCombatPostfix()
    {
        DeathGraceController.BeginDeathGrace();
    }

    [HarmonyPatch(typeof(RunManager), nameof(RunManager.OnEnded))]
    [HarmonyPrefix]
    public static bool RunEndedPrefix(bool isVictory, ref SerializableRun __result)
    {
        SerializableRun? replacement = __result;
        if (!DeathGraceController.TryDeferRunEnd(isVictory, ref replacement) || replacement == null)
        {
            return true;
        }

        __result = replacement;
        return false;
    }

    [HarmonyPatch(typeof(NRun), nameof(NRun.ShowGameOverScreen))]
    [HarmonyPrefix]
    public static bool ShowGameOverScreenPrefix()
    {
        return !DeathGraceController.ShouldSuppressGameOverScreen();
    }

    [HarmonyPatch(typeof(NGame), nameof(NGame.ReturnToMainMenu))]
    [HarmonyPrefix]
    public static void ReturnToMainMenuPrefix()
    {
        DeathGraceController.PrepareForMainMenuReturn();
    }

    [HarmonyPatch(typeof(RunManager), nameof(RunManager.CleanUp))]
    [HarmonyPrefix]
    public static void CleanUpPrefix()
    {
        DeathGraceController.ClearState();
    }
}
