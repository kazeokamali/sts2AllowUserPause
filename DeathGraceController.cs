using System;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;

namespace Sts2AllowUserPause;

internal static class DeathGraceController
{
    private sealed record HardPauseSnapshot(
        bool TreeWasPaused,
        Node.ProcessModeEnum HotkeyManagerProcessMode,
        bool CombatWasPaused,
        bool ActionExecutorWasPaused);

    private static HardPauseSnapshot? ActiveHardPause;
    private static bool PendingDeathGrace;
    private static bool FinalizingDeathGrace;
    private static DeathDecisionPopup? ActiveDecisionPopup;
    private static string? CachedFloorStartSaveJson;

    private static bool IsSupportedRun()
    {
        RunManager? runManager = RunManager.Instance;
        return runManager != null && runManager.IsInProgress && runManager.IsSinglePlayerOrFakeMultiplayer;
    }

    private static bool IsDeathGraceActive()
    {
        return IsSupportedRun() && PendingDeathGrace && !FinalizingDeathGrace;
    }

    public static void BeginDeathGrace()
    {
        if (PendingDeathGrace || FinalizingDeathGrace || !IsSupportedRun())
        {
            return;
        }

        CaptureFloorStartSaveSnapshot();
        if (!EnsureHardPauseStarted())
        {
            CachedFloorStartSaveJson = null;
            Log.Warn("[Sts2AllowUserPause] Failed to start the death grace hard pause. Allowing the normal loss flow.");
            return;
        }

        PendingDeathGrace = true;

        // Abandon-run confirmation buttons clear the modal container in the same
        // release callback that ultimately triggers this death-grace flow. Delay
        // popup creation until those UI callbacks finish so the decision popup
        // does not get cleared immediately after being added.
        Callable.From(ShowDecisionPopup).CallDeferred();
        Log.Info("[Sts2AllowUserPause] Death grace window opened.");
    }

    public static bool TryDeferRunEnd(bool isVictory, ref SerializableRun? serializableRun)
    {
        if (!IsSupportedRun() || !PendingDeathGrace || FinalizingDeathGrace || isVictory)
        {
            return false;
        }

        serializableRun = RunManager.Instance.ToSave(null);
        Log.Info("[Sts2AllowUserPause] Delayed RunManager.OnEnded(false) until the hard pause ends.");
        return true;
    }

    public static bool ShouldSuppressGameOverScreen()
    {
        return IsDeathGraceActive();
    }

    public static void PrepareForMainMenuReturn()
    {
        TryRestoreFloorStartSaveBeforeMenuReturn();
        CloseDecisionPopup();
        PendingDeathGrace = false;
        FinalizingDeathGrace = false;
        EndHardPause(finalizePendingDeathGrace: false, resumeGameplay: false);
    }

    public static void ClearState()
    {
        CloseDecisionPopup();
        PendingDeathGrace = false;
        FinalizingDeathGrace = false;
        CachedFloorStartSaveJson = null;
        EndHardPause(finalizePendingDeathGrace: false, resumeGameplay: false);
    }

    private static bool EnsureHardPauseStarted()
    {
        if (ActiveHardPause != null)
        {
            return true;
        }

        NGame? game = NGame.Instance;
        NHotkeyManager? hotkeyManager = NHotkeyManager.Instance;
        SceneTree? tree = game?.GetTree();
        if (game == null || hotkeyManager == null || tree == null)
        {
            return false;
        }

        ActiveHardPause = new HardPauseSnapshot(
            TreeWasPaused: tree.Paused,
            HotkeyManagerProcessMode: hotkeyManager.ProcessMode,
            CombatWasPaused: CombatManager.Instance.IsPaused,
            ActionExecutorWasPaused: RunManager.Instance.ActionExecutor.IsPaused);

        hotkeyManager.ProcessMode = Node.ProcessModeEnum.WhenPaused;
        RunManager.Instance.ActionExecutor.Pause();
        CombatManager.Instance.Pause();
        tree.Paused = true;

        Log.Info("[Sts2AllowUserPause] Hard pause session started.");
        return true;
    }

    private static void EndHardPause(bool finalizePendingDeathGrace, bool resumeGameplay = true)
    {
        HardPauseSnapshot? snapshot = ActiveHardPause;
        if (snapshot == null)
        {
            return;
        }

        ActiveHardPause = null;
        RestoreHardPauseSnapshot(snapshot, resumeGameplay);

        Log.Info("[Sts2AllowUserPause] Hard pause session ended.");

        if (finalizePendingDeathGrace && PendingDeathGrace)
        {
            FinalizePendingDeathGrace();
        }
    }

    private static void RestoreHardPauseSnapshot(HardPauseSnapshot snapshot, bool resumeGameplay)
    {
        NGame? game = NGame.Instance;
        NHotkeyManager? hotkeyManager = NHotkeyManager.Instance;
        SceneTree? tree = game?.GetTree();

        if (hotkeyManager != null)
        {
            hotkeyManager.ProcessMode = snapshot.HotkeyManagerProcessMode;
        }

        if (tree != null)
        {
            tree.Paused = snapshot.TreeWasPaused;
        }

        if (RunManager.Instance.IsInProgress)
        {
            if (!resumeGameplay || snapshot.ActionExecutorWasPaused)
            {
                RunManager.Instance.ActionExecutor.Pause();
            }
            else
            {
                RunManager.Instance.ActionExecutor.Unpause();
            }
        }

        if (!resumeGameplay || snapshot.CombatWasPaused)
        {
            CombatManager.Instance.Pause();
        }
        else
        {
            CombatManager.Instance.Unpause();
        }
    }

    private static void FinalizePendingDeathGrace()
    {
        if (!PendingDeathGrace || FinalizingDeathGrace || !RunManager.Instance.IsInProgress || NRun.Instance == null)
        {
            return;
        }

        PendingDeathGrace = false;
        FinalizingDeathGrace = true;

        try
        {
            SerializableRun serializableRun = RunManager.Instance.OnEnded(isVictory: false);
            NRun.Instance.ShowGameOverScreen(serializableRun);
            Log.Info("[Sts2AllowUserPause] Finalized the delayed loss flow.");
        }
        finally
        {
            CachedFloorStartSaveJson = null;
            FinalizingDeathGrace = false;
        }
    }

    private static void ShowDecisionPopup()
    {
        if (!IsDeathGraceActive())
        {
            return;
        }

        NModalContainer? modalContainer = NModalContainer.Instance;
        if (ActiveDecisionPopup != null || modalContainer == null)
        {
            return;
        }

        // Abandon-run confirmation popups queue-free themselves without resetting
        // the modal container state. Clear any leftover modal/backstop first so
        // the death decision popup can always take over the screen.
        modalContainer.Clear();
        NRun.Instance?.GlobalUi.CapstoneContainer.DisableBackstopInstantly();

        DeathDecisionPopup popup = DeathDecisionPopup.Create(ConfirmDeath, ReturnToFloorStart, ClearDecisionPopupReference);
        ActiveDecisionPopup = popup;
        modalContainer.Add(popup);
        Log.Info("[Sts2AllowUserPause] Death decision popup displayed.");
    }

    private static void CloseDecisionPopup()
    {
        if (ActiveDecisionPopup == null)
        {
            return;
        }

        if (GodotObject.IsInstanceValid(ActiveDecisionPopup))
        {
            ActiveDecisionPopup.ClosePopup();
        }

        ActiveDecisionPopup = null;
    }

    private static void ClearDecisionPopupReference()
    {
        ActiveDecisionPopup = null;
    }

    private static void ConfirmDeath()
    {
        EndHardPause(finalizePendingDeathGrace: true, resumeGameplay: false);
    }

    private static void ReturnToFloorStart()
    {
        TaskHelper.RunSafely(ReturnToFloorStartAsync());
    }

    private static async Task ReturnToFloorStartAsync()
    {
        SerializableRun? serializableRun = LoadFloorStartSaveSnapshot();
        if (serializableRun == null || NGame.Instance == null)
        {
            Log.Error("[Sts2AllowUserPause] Return-to-floor-start requested, but the floor-start snapshot could not be loaded. Falling back to death resolution.");
            EndHardPause(finalizePendingDeathGrace: true, resumeGameplay: false);
            return;
        }

        RunState runState = RunState.FromSerializable(serializableRun);

        PrepareForMainMenuReturn();
        NRun.Instance?.RunMusicController?.StopMusic();

        await NGame.Instance.Transition.FadeOut(0.8f, runState.Players[0].Character.CharacterSelectTransitionPath);
        RunManager.Instance.CleanUp();
        NGame.Instance.ReactionContainer.InitializeNetworking(new NetSingleplayerGameService());
        RunManager.Instance.SetUpSavedSinglePlayer(runState, serializableRun);
        await NGame.Instance.LoadRun(runState, serializableRun.PreFinishedRoom);
        await NGame.Instance.Transition.FadeIn();

        Log.Info("[Sts2AllowUserPause] Returned to the start of the current floor using current_run.save.");
    }

    private static void CaptureFloorStartSaveSnapshot()
    {
        ReadSaveResult<SerializableRun> readRunSaveResult = SaveManager.Instance.LoadRunSave();
        if (!readRunSaveResult.Success || readRunSaveResult.SaveData == null)
        {
            CachedFloorStartSaveJson = null;
            Log.Warn("[Sts2AllowUserPause] Failed to capture the floor-start save snapshot from current_run.save.");
            return;
        }

        CachedFloorStartSaveJson = JsonSerializationUtility.ToJson(readRunSaveResult.SaveData);
        Log.Info("[Sts2AllowUserPause] Captured the floor-start save snapshot for death grace.");
    }

    private static SerializableRun? LoadFloorStartSaveSnapshot()
    {
        if (!string.IsNullOrWhiteSpace(CachedFloorStartSaveJson))
        {
            ReadSaveResult<SerializableRun> cachedReadResult = JsonSerializationUtility.FromJson<SerializableRun>(CachedFloorStartSaveJson);
            if (cachedReadResult.Success && cachedReadResult.SaveData != null)
            {
                return cachedReadResult.SaveData;
            }

            Log.Warn("[Sts2AllowUserPause] The cached floor-start save snapshot was invalid. Falling back to current_run.save.");
        }

        ReadSaveResult<SerializableRun> diskReadResult = SaveManager.Instance.LoadRunSave();
        if (diskReadResult.Success && diskReadResult.SaveData != null)
        {
            return diskReadResult.SaveData;
        }

        return null;
    }

    private static void TryRestoreFloorStartSaveBeforeMenuReturn()
    {
        try
        {
            RestoreFloorStartSaveBeforeMenuReturn();
        }
        catch (Exception ex)
        {
            Log.Error($"[Sts2AllowUserPause] Failed to restore current_run.save before returning to the main menu: {ex}");
        }
    }

    private static void RestoreFloorStartSaveBeforeMenuReturn()
    {
        if (!PendingDeathGrace || FinalizingDeathGrace || string.IsNullOrWhiteSpace(CachedFloorStartSaveJson))
        {
            return;
        }

        string savePath = RunSaveManager.GetRunSavePath(SaveManager.Instance.CurrentProfileId, RunSaveManager.runSaveFileName);
        GodotFileIo fileIo = new(UserDataPathProvider.GetAccountScopedBasePath(null));
        fileIo.WriteFile(savePath, CachedFloorStartSaveJson);
        Log.Info("[Sts2AllowUserPause] Restored current_run.save to the floor-start snapshot before returning to the main menu.");
    }
}
