using System;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;

namespace Sts2AllowUserPause;

public partial class DeathDecisionPopup : Control, IScreenContext
{
    private const string VerticalPopupWithIconScenePath = "res://userTscn/vertical_popup_with_icon.tscn";
    private const string ConfirmDeathCustomIconPath = "res://userTscn/Icon/YbtnIcon.png";
    private const string ReturnToFloorStartCustomIconPath = "res://userTscn/Icon/NbtnIcon.png";
    private const float ButtonIconGap = 6f;
    private static readonly string ConfirmDeathFallbackIconPath = ImageHelper.GetImagePath("ui/emote/skull.png");
    private static readonly string ReturnToFloorStartFallbackIconPath = ImageHelper.GetImagePath("ui/main_menu/submenu_load.png");

    private readonly Action _confirmAction;
    private readonly Action _returnToFloorStartAction;
    private readonly Action? _closedAction;

    private NVerticalPopup? _verticalPopup;
    private TextureRect? _yesButtonIcon;
    private TextureRect? _noButtonIcon;

    public Control? DefaultFocusedControl => _verticalPopup?.YesButton;

    private DeathDecisionPopup(Action confirmAction, Action returnToFloorStartAction, Action? closedAction)
    {
        _confirmAction = confirmAction;
        _returnToFloorStartAction = returnToFloorStartAction;
        _closedAction = closedAction;
        ProcessMode = ProcessModeEnum.WhenPaused;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    public static DeathDecisionPopup Create(Action confirmAction, Action returnToFloorStartAction, Action? closedAction = null)
    {
        return new DeathDecisionPopup(confirmAction, returnToFloorStartAction, closedAction);
    }

    public override void _Ready()
    {
        NVerticalPopup popupVisual = ResourceLoader.Load<PackedScene>(VerticalPopupWithIconScenePath).Instantiate<NVerticalPopup>(PackedScene.GenEditState.Disabled);
        popupVisual.ProcessMode = ProcessModeEnum.Inherit;
        this.AddChildSafely(popupVisual);

        _verticalPopup = popupVisual;
        _yesButtonIcon = popupVisual.GetNodeOrNull<TextureRect>("YesButtonIcon");
        _noButtonIcon = popupVisual.GetNodeOrNull<TextureRect>("NoButtonIcon");

        _verticalPopup.SetText(
            "Died?",
            "重新在本层开始 or 确认死亡回菜单"
        );
        popupVisual.GetNodeOrNull<CanvasItem>("Description")?.Show();
        _verticalPopup.InitYesButton(new MegaCrit.Sts2.Core.Localization.LocString("main_menu_ui", "GENERIC_POPUP.confirm"), OnConfirmPressed);
        _verticalPopup.InitNoButton(new MegaCrit.Sts2.Core.Localization.LocString("main_menu_ui", "GENERIC_POPUP.cancel"), OnReturnToFloorStartPressed);
        _verticalPopup.YesButton.SetText("不挣扎了");
        _verticalPopup.NoButton.SetText("本层SL");

        _verticalPopup.YesButton.FocusMode = FocusModeEnum.All;
        _verticalPopup.NoButton.FocusMode = FocusModeEnum.All;

        ConfigureButtonIcon(
            _yesButtonIcon,
            _verticalPopup.YesButton,
            LoadIconTexture(ConfirmDeathCustomIconPath) ?? PreloadManager.Cache.GetTexture2D(ConfirmDeathFallbackIconPath));
        ConfigureButtonIcon(
            _noButtonIcon,
            _verticalPopup.NoButton,
            LoadIconTexture(ReturnToFloorStartCustomIconPath) ?? PreloadManager.Cache.GetTexture2D(ReturnToFloorStartFallbackIconPath));

        RegisterBlockingHotkeys();
        Callable.From(() => _verticalPopup.YesButton.GrabFocus()).CallDeferred();
    }

    public override void _ExitTree()
    {
        UnregisterBlockingHotkeys();
        _verticalPopup?.DisconnectSignals();
        _verticalPopup?.DisconnectHotkeys();
        _closedAction?.Invoke();
    }

    public void ClosePopup()
    {
        if (NModalContainer.Instance?.OpenModal == this)
        {
            NModalContainer.Instance.Clear();
            return;
        }

        this.QueueFreeSafely();
    }

    private void RegisterBlockingHotkeys()
    {
        NHotkeyManager? hotkeyManager = NHotkeyManager.Instance;
        if (hotkeyManager == null)
        {
            return;
        }

        hotkeyManager.AddBlockingScreen(this);
        hotkeyManager.PushHotkeyPressedBinding(MegaInput.select, TriggerConfirmFromHotkey);
        hotkeyManager.PushHotkeyPressedBinding(MegaInput.cancel, TriggerReturnToFloorStartFromHotkey);
    }

    private void UnregisterBlockingHotkeys()
    {
        NHotkeyManager? hotkeyManager = NHotkeyManager.Instance;
        if (hotkeyManager == null)
        {
            return;
        }

        hotkeyManager.RemoveHotkeyPressedBinding(MegaInput.select, TriggerConfirmFromHotkey);
        hotkeyManager.RemoveHotkeyPressedBinding(MegaInput.cancel, TriggerReturnToFloorStartFromHotkey);
        hotkeyManager.RemoveBlockingScreen(this);
    }

    private void TriggerConfirmFromHotkey()
    {
        _verticalPopup?.YesButton?.ForceClick();
    }

    private void TriggerReturnToFloorStartFromHotkey()
    {
        _verticalPopup?.NoButton?.ForceClick();
    }

    private void OnConfirmPressed(NButton _)
    {
        Callable.From(_confirmAction).CallDeferred();
    }

    private void OnReturnToFloorStartPressed(NButton _)
    {
        Callable.From(_returnToFloorStartAction).CallDeferred();
    }

    private static void ConfigureButtonIcon(TextureRect? iconNode, Control? button, Texture2D? texture)
    {
        if (iconNode == null)
        {
            return;
        }

        iconNode.Texture = texture;
        iconNode.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        iconNode.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        iconNode.MouseFilter = MouseFilterEnum.Ignore;
        UpdateButtonIconVisibility(iconNode, button);

        if (button != null)
        {
            UpdateButtonIconLayout(iconNode, button);
            button.Connect(CanvasItem.SignalName.VisibilityChanged, Callable.From(() => UpdateButtonIconVisibility(iconNode, button)));
            button.Connect(Control.SignalName.Resized, Callable.From(() => UpdateButtonIconLayout(iconNode, button)));
            Callable.From(() => UpdateButtonIconLayout(iconNode, button)).CallDeferred();
        }
    }

    private static void UpdateButtonIconVisibility(TextureRect iconNode, CanvasItem? button)
    {
        iconNode.Visible = iconNode.Texture != null && (button?.Visible ?? true);
    }

    private static void UpdateButtonIconLayout(Control iconNode, Control button)
    {
        Vector2 iconSize = iconNode.Size;
        Vector2 buttonSize = button.Size;
        iconNode.Position = new Vector2(
            button.Position.X + (buttonSize.X - iconSize.X) * 0.5f,
            button.Position.Y - iconSize.Y - ButtonIconGap);
    }

    private static Texture2D? LoadIconTexture(string resourcePath)
    {
        byte[] bytes = Godot.FileAccess.GetFileAsBytes(resourcePath);
        if (bytes.Length == 0)
        {
            GD.PushWarning($"[Sts2AllowUserPause] Missing icon texture: {resourcePath}");
            return null;
        }

        Image image = new();
        Error error = image.LoadPngFromBuffer(bytes);
        if (error != Error.Ok)
        {
            GD.PushWarning($"[Sts2AllowUserPause] Failed to decode icon texture: {resourcePath} ({error})");
            return null;
        }

        return ImageTexture.CreateFromImage(image);
    }
}
