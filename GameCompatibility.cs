using System;
using System.Reflection;
using MegaCrit.Sts2.Core.Debug;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace Sts2AllowUserPause;

internal static class GameCompatibility
{
    private const string NewRunEndResultType = "MegaCrit.Sts2.Core.Entities.Save.PostRunProgressUpdateResult";

    private static MethodInfo? _runEnded;
    private static MethodInfo? _showGameOverScreen;
    private static MethodInfo? _pushHotkey;
    private static MethodInfo? _removeHotkey;
    private static bool _usesNewApi;

    public static bool UsesNewApi => _usesNewApi;

    public static void Initialize()
    {
        MethodInfo runEnded = typeof(RunManager).GetMethod(nameof(RunManager.OnEnded), new[] { typeof(bool) })
            ?? throw new MissingMethodException(typeof(RunManager).FullName, nameof(RunManager.OnEnded));

        Type resultType = runEnded.ReturnType;
        bool usesNewApi = resultType.FullName == NewRunEndResultType;
        if (!usesNewApi && resultType != typeof(SerializableRun))
        {
            throw new NotSupportedException($"Unexpected RunManager.OnEnded result: {resultType.FullName}");
        }

        MethodInfo showGameOverScreen = typeof(NRun).GetMethod(nameof(NRun.ShowGameOverScreen), new[] { resultType })
            ?? throw new MissingMethodException(typeof(NRun).FullName, nameof(NRun.ShowGameOverScreen));

        Type[] hotkeyParameters = usesNewApi
            ? new[] { typeof(string), typeof(Action), typeof(long) }
            : new[] { typeof(string), typeof(Action) };
        MethodInfo pushHotkey = typeof(NHotkeyManager).GetMethod(nameof(NHotkeyManager.PushHotkeyPressedBinding), hotkeyParameters)
            ?? throw new MissingMethodException(typeof(NHotkeyManager).FullName, nameof(NHotkeyManager.PushHotkeyPressedBinding));
        MethodInfo removeHotkey = typeof(NHotkeyManager).GetMethod(nameof(NHotkeyManager.RemoveHotkeyPressedBinding), new[] { typeof(string), typeof(Action) })
            ?? throw new MissingMethodException(typeof(NHotkeyManager).FullName, nameof(NHotkeyManager.RemoveHotkeyPressedBinding));

        if (removeHotkey.ReturnType != (usesNewApi ? typeof(long) : typeof(void)))
        {
            throw new NotSupportedException($"Unexpected hotkey removal result: {removeHotkey.ReturnType.FullName}");
        }

        _runEnded = runEnded;
        _showGameOverScreen = showGameOverScreen;
        _pushHotkey = pushHotkey;
        _removeHotkey = removeHotkey;
        _usesNewApi = usesNewApi;

        string version = ReleaseInfoManager.Instance.ReleaseInfo?.Version ?? "unknown";
        Log.Info($"[Sts2AllowUserPause] Game version {version}; selected {(usesNewApi ? "0.113.1 beta" : "0.111.1 stable")} API compatibility.");
    }

    public static void FinishDeath()
    {
        object? result = _runEnded!.Invoke(RunManager.Instance, new object[] { false });
        _showGameOverScreen!.Invoke(NRun.Instance, new[] { result });
    }

    public static void PushHotkey(NHotkeyManager manager, string hotkey, Action action)
    {
        object[] args = _usesNewApi ? new object[] { hotkey, action, 0L } : new object[] { hotkey, action };
        _pushHotkey!.Invoke(manager, args);
    }

    public static void RemoveHotkey(NHotkeyManager manager, string hotkey, Action action)
    {
        _removeHotkey!.Invoke(manager, new object[] { hotkey, action });
    }
}
