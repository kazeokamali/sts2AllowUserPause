using System.IO;
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

internal static class DeckPauseController
{
    private sealed record PauseSessionSnapshot(
        bool TreeWasPaused,
        Node.ProcessModeEnum HotkeyManagerProcessMode,
        bool CombatWasPaused,
        bool ActionExecutorWasPaused);

    private static PauseSessionSnapshot? ActiveSession;
    private static bool PendingDeathGrace;
    private static bool FinalizingDeathGrace;
    private static DeathDecisionPopup? ActiveDeathPopup;
    private static string? CachedFloorStartSaveJson;

    private static bool IsDeathGraceSupportedForCurrentRun()
    {
        RunManager? runManager = RunManager.Instance;
        return runManager != null && runManager.IsInProgress && runManager.IsSinglePlayerOrFakeMultiplayer;
    }

    private static bool IsDeathGraceActive()
    {
        return IsDeathGraceSupportedForCurrentRun() && PendingDeathGrace && !FinalizingDeathGrace;
    }

    public static void BeginDeathGrace()
    {
        if (PendingDeathGrace || FinalizingDeathGrace)
        {
            return;
        }

        if (!IsDeathGraceSupportedForCurrentRun())
        {
            return;
        }

        CaptureFloorStartSaveSnapshot();
        PendingDeathGrace = true;
        if (!EnsureSessionStarted())
        {
            return;
        }

        // Abandon-run confirmation buttons clear the modal container in the same
        // release callback that ultimately triggers this death-grace flow. Delay
        // popup creation until those UI callbacks finish so the decision popup
        // does not get cleared immediately after being added.
        Callable.From(ShowDeathDecisionPopup).CallDeferred();
        Log.Info("[Sts2AllowUserPause] Loss grace window opened.");
    }

    public static bool TrySuppressRunEnd(bool isVictory, ref SerializableRun? serializableRun)
    {
        if (!IsDeathGraceSupportedForCurrentRun() || !PendingDeathGrace || FinalizingDeathGrace || isVictory)
        {
            return false;
        }

        serializableRun = RunManager.Instance.ToSave(null);
        Log.Info("[Sts2AllowUserPause] Delayed RunManager.OnEnded(false) until hard pause ends.");
        return true;
    }

    public static bool ShouldSuppressGameOverScreen()
    {
        return IsDeathGraceActive();
    }

    public static void PrepareForRunExit()
    {
        PersistFloorStartSaveSnapshotIfNeeded();
        CloseDeathDecisionPopup();
        PendingDeathGrace = false;
        FinalizingDeathGrace = false;
        EndSession(finalizePendingDeathGrace: false, resumeGameplay: false);
    }

    public static void Reset()
    {
        CloseDeathDecisionPopup();
        PendingDeathGrace = false;
        FinalizingDeathGrace = false;
        CachedFloorStartSaveJson = null;
        EndSession(finalizePendingDeathGrace: false, resumeGameplay: false);
    }

    public static bool ShouldBlockPauseMenuDuringDeathGrace()
    {
        return IsDeathGraceActive();
    }

    private static bool EnsureSessionStarted()
    {
        if (ActiveSession != null)
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

        ActiveSession = new PauseSessionSnapshot(
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

    private static void EndSession(bool finalizePendingDeathGrace, bool resumeGameplay = true)
    {
        PauseSessionSnapshot? snapshot = ActiveSession;
        if (snapshot == null)
        {
            return;
        }

        ActiveSession = null;
        RestoreSnapshot(snapshot, resumeGameplay);

        Log.Info("[Sts2AllowUserPause] Hard pause session ended.");

        if (finalizePendingDeathGrace && PendingDeathGrace)
        {
            FinalizePendingDeathGrace();
        }
    }

    private static void RestoreSnapshot(PauseSessionSnapshot snapshot, bool resumeGameplay)
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
            Log.Info("[Sts2AllowUserPause] Finalized delayed loss flow.");
        }
        finally
        {
            CachedFloorStartSaveJson = null;
            FinalizingDeathGrace = false;
        }
    }

    private static void ShowDeathDecisionPopup()
    {
        if (!IsDeathGraceActive())
        {
            return;
        }

        NModalContainer? modalContainer = NModalContainer.Instance;
        if (ActiveDeathPopup != null || modalContainer == null)
        {
            return;
        }

        // Abandon-run confirmation popups queue-free themselves without resetting
        // the modal container state. Clear any leftover modal/backstop first so
        // the death decision popup can always take over the screen.
        modalContainer.Clear();
        NRun.Instance?.GlobalUi.CapstoneContainer.DisableBackstopInstantly();

        DeathDecisionPopup popup = DeathDecisionPopup.Create(OnConfirmDeathRequested, OnReturnToFloorStartRequested, ClearDeathDecisionPopupReference);
        ActiveDeathPopup = popup;
        modalContainer.Add(popup);
        Log.Info("[Sts2AllowUserPause] Death decision popup displayed.");
    }

    private static void CloseDeathDecisionPopup()
    {
        if (ActiveDeathPopup == null)
        {
            return;
        }

        if (GodotObject.IsInstanceValid(ActiveDeathPopup))
        {
            ActiveDeathPopup.ClosePopup();
        }

        ActiveDeathPopup = null;
    }

    private static void ClearDeathDecisionPopupReference()
    {
        ActiveDeathPopup = null;
    }

    private static void OnConfirmDeathRequested()
    {
        EndSession(finalizePendingDeathGrace: true, resumeGameplay: false);
    }

    private static void OnReturnToFloorStartRequested()
    {
        TaskHelper.RunSafely(ReturnToFloorStartAsync());
    }

    private static async Task ReturnToFloorStartAsync()
    {
        SerializableRun? serializableRun = LoadFloorStartSaveSnapshot();
        if (serializableRun == null || NGame.Instance == null)
        {
            Log.Error("[Sts2AllowUserPause] Return-to-floor-start requested, but the floor-start snapshot could not be loaded. Falling back to death resolution.");
            EndSession(finalizePendingDeathGrace: true, resumeGameplay: false);
            return;
        }

        RunState runState = RunState.FromSerializable(serializableRun);

        PrepareForRunExit();
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
            Log.Warn("[Sts2AllowUserPause] Failed to capture floor-start save snapshot from current_run.save.");
            return;
        }

        CachedFloorStartSaveJson = JsonSerializationUtility.ToJson(readRunSaveResult.SaveData);
        Log.Info("[Sts2AllowUserPause] Captured floor-start save snapshot for death grace.");
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

            Log.Warn("[Sts2AllowUserPause] Cached floor-start save snapshot was invalid. Falling back to current_run.save.");
        }

        ReadSaveResult<SerializableRun> diskReadResult = SaveManager.Instance.LoadRunSave();
        if (diskReadResult.Success && diskReadResult.SaveData != null)
        {
            return diskReadResult.SaveData;
        }

        return null;
    }

    private static void PersistFloorStartSaveSnapshotIfNeeded()
    {
        if (!PendingDeathGrace || FinalizingDeathGrace || string.IsNullOrWhiteSpace(CachedFloorStartSaveJson))
        {
            return;
        }

        string savePath = SaveManager.Instance.GetProfileScopedPath(Path.Combine(UserDataPathProvider.SavesDir, RunSaveManager.runSaveFileName));
        GodotFileIo fileIo = new(UserDataPathProvider.GetAccountScopedBasePath(null));
        fileIo.WriteFile(savePath, CachedFloorStartSaveJson);
        Log.Info("[Sts2AllowUserPause] Restored current_run.save to the floor-start snapshot before returning to the main menu.");
    }
}
