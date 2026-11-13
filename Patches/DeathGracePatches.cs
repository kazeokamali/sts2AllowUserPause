using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Save;
using MegaCrit.Sts2.Core.Logging;
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

    [HarmonyPatch(typeof(CombatManager), "ProcessPendingLoss")]
    [HarmonyPostfix]
    public static void ProcessPendingLossPostfix()
    {
        DeathGraceController.OpenDecisionPopupAfterPendingLoss();
    }

    [HarmonyPatch(typeof(RunManager), nameof(RunManager.OnEnded))]
    [HarmonyPrefix]
    public static bool RunEndedPrefix(bool isVictory, ref PostRunProgressUpdateResult __result)
    {
        return !DeathGraceController.TryDeferRunEnd(isVictory, ref __result);
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

    [HarmonyPatch(typeof(NGame), nameof(NGame.StartNewSingleplayerRun))]
    [HarmonyPrefix]
    public static void StartNewSingleplayerRunPrefix()
    {
        if (!RunManager.Instance.IsInProgress)
        {
            return;
        }

        Log.Warn("[Sts2AllowUserPause] Clearing stale run state before starting a new singleplayer run.");
        RunManager.Instance.CleanUp();
    }

    [HarmonyPatch(typeof(RunManager), nameof(RunManager.CleanUp))]
    [HarmonyPrefix]
    public static void CleanUpPrefix()
    {
        DeathGraceController.ClearState();
    }
}
