using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace Sts2AllowUserPause.Patches;

[HarmonyPatch]
public static class LossGracePatch
{
    [HarmonyPatch(typeof(CombatManager), nameof(CombatManager.LoseCombat))]
    [HarmonyPostfix]
    public static void LoseCombatPostfix()
    {
        DeckPauseController.BeginDeathGrace();
    }

    [HarmonyPatch(typeof(RunManager), nameof(RunManager.OnEnded))]
    [HarmonyPrefix]
    public static bool OnEndedPrefix(bool isVictory, ref SerializableRun __result)
    {
        SerializableRun? replacement = __result;
        if (!DeckPauseController.TrySuppressRunEnd(isVictory, ref replacement) || replacement == null)
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
        return !DeckPauseController.ShouldSuppressGameOverScreen();
    }
}
