using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.TopBar;

namespace Sts2AllowUserPause.Patches;

[HarmonyPatch(typeof(NTopBarPauseButton), "OnRelease")]
public static class NTopBarPauseButtonDeathGracePatch
{
    [HarmonyPrefix]
    public static bool OnReleasePrefix()
    {
        return !DeckPauseController.ShouldBlockPauseMenuDuringDeathGrace();
    }
}
