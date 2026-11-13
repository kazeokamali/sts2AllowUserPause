using System;
using Godot;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;

namespace Sts2AllowUserPause;

public partial class DeathDecisionPopup : Control, IScreenContext
{
    private const string PopupTexturePath = "res://images/atlases/ui_atlas.sprites/popup_vertical.tres";
    private const string ConfirmButtonTexturePath = "res://images/atlases/ui_atlas.sprites/popup_confirm_button.tres";
    private const string ConfirmButtonOutlineTexturePath = "res://images/atlases/compressed_atlas.sprites/popup_confirm_button_outline.tres";
    private const string CancelButtonTexturePath = "res://images/atlases/ui_atlas.sprites/popup_cancel_button.tres";
    private const string CancelButtonOutlineTexturePath = "res://images/atlases/compressed_atlas.sprites/popup_cancel_button_outline.tres";
    private const string HsvShaderPath = "res://shaders/hsv.gdshader";
    private const string HeaderFontPath = "res://themes/kreon_bold_glyph_space_two.tres";
    private const string BodyFontPath = "res://themes/kreon_regular_glyph_space_one.tres";
    private const string BodyBoldFontPath = "res://themes/kreon_bold_glyph_space_one.tres";
    private const string ConfirmDeathIconPath = "res://userTscn/Icon/YbtnIcon.png";
    private const string ReturnToFloorStartIconPath = "res://userTscn/Icon/NbtnIcon.png";
    private const string TitleFallback = "Died?";
    private const string BodyFallback = "Restart from the beginning of this floor, or confirm death and return to the main menu?";
    private const string ConfirmDeathFallback = "Accept death";
    private const string ReturnToFloorStartFallback = "Restart floor";

    private static readonly LocString PopupTitleLoc = new("main_menu_ui", "STS2_ALLOW_USER_PAUSE.DEATH_DECISION_POPUP.title");
    private static readonly LocString PopupBodyLoc = new("main_menu_ui", "STS2_ALLOW_USER_PAUSE.DEATH_DECISION_POPUP.body");
    private static readonly LocString ConfirmDeathButtonTextLoc = new("main_menu_ui", "STS2_ALLOW_USER_PAUSE.DEATH_DECISION_POPUP.confirm_death");
    private static readonly LocString ReturnToFloorStartButtonTextLoc = new("main_menu_ui", "STS2_ALLOW_USER_PAUSE.DEATH_DECISION_POPUP.return_to_floor_start");

    private readonly Action _confirmDeathAction;
    private readonly Action _returnToFloorStartAction;
    private readonly Action? _closedAction;

    private Label? _titleLabel;
    private RichTextLabel? _bodyLabel;
    private NativePopupButton? _confirmButton;
    private NativePopupButton? _returnButton;
    private bool _hotkeysRegistered;
    private bool _isClosing;

    public Control? DefaultFocusedControl => _confirmButton;

    private DeathDecisionPopup(Action confirmDeathAction, Action returnToFloorStartAction, Action? closedAction)
    {
        _confirmDeathAction = confirmDeathAction;
        _returnToFloorStartAction = returnToFloorStartAction;
        _closedAction = closedAction;

        ProcessMode = ProcessModeEnum.WhenPaused;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    public static DeathDecisionPopup Create(Action confirmDeathAction, Action returnToFloorStartAction, Action? closedAction = null)
    {
        return new DeathDecisionPopup(confirmDeathAction, returnToFloorStartAction, closedAction);
    }

    public override void _Ready()
    {
        BuildPopupUi();
        ApplyLocalizedText();
        RegisterBlockingHotkeys();
        LocString.SubscribeToLocaleChange(ApplyLocalizedText);
        Callable.From(() => _confirmButton?.GrabFocus()).CallDeferred();
        Log.Info("[Sts2AllowUserPause] Native-style death decision popup UI initialized.");
    }

    public override void _ExitTree()
    {
        LocString.UnsubscribeToLocaleChange(ApplyLocalizedText);
        UnregisterBlockingHotkeys();
        _confirmButton = null;
        _returnButton = null;
        _closedAction?.Invoke();
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (inputEvent.IsActionPressed(MegaInput.confirm) || inputEvent.IsActionPressed(MegaInput.select))
        {
            TriggerConfirm();
            GetViewport()?.SetInputAsHandled();
            return;
        }

        if (inputEvent.IsActionPressed(MegaInput.cancel) || inputEvent.IsActionPressed(MegaInput.pauseAndBack))
        {
            TriggerReturnToFloorStart();
            GetViewport()?.SetInputAsHandled();
        }
    }

    public void ClosePopup()
    {
        if (NModalContainer.Instance?.OpenModal == this)
        {
            NModalContainer.Instance.Clear();
            return;
        }

        QueueFree();
    }

    private void BuildPopupUi()
    {
        TextureRect popup = new()
        {
            Name = "VerticalPopup",
            Material = CreateHsvMaterial(h: 0.505f, s: 1f, v: 0.75f),
            Texture = LoadTexture(PopupTexturePath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Stop,
        };
        popup.AnchorLeft = 0.5f;
        popup.AnchorTop = 0.5f;
        popup.AnchorRight = 0.5f;
        popup.AnchorBottom = 0.5f;
        popup.OffsetLeft = -261f;
        popup.OffsetTop = -300f;
        popup.OffsetRight = 261f;
        popup.OffsetBottom = 300f;
        AddChild(popup);

        _titleLabel = CreateTitleLabel();
        _bodyLabel = CreateBodyLabel();
        popup.AddChild(_titleLabel);
        popup.AddChild(_bodyLabel);

        _returnButton = NativePopupButton.CreateCancel();
        _confirmButton = NativePopupButton.CreateConfirm();
        _returnButton.Pressed += TriggerReturnToFloorStart;
        _confirmButton.Pressed += TriggerConfirm;
        popup.AddChild(_returnButton);
        popup.AddChild(_confirmButton);

        popup.AddChild(CreateTopIcon(ReturnToFloorStartIconPath, left: 65f, top: 391f));
        popup.AddChild(CreateTopIcon(ConfirmDeathIconPath, left: 407f, top: 391f));
    }

    private static Label CreateTitleLabel()
    {
        Label label = new()
        {
            Name = "Header",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        label.AnchorRight = 1f;
        label.OffsetTop = 56f;
        label.OffsetBottom = 128f;
        label.AddThemeColorOverride("font_color", new Color(0.937255f, 0.784314f, 0.317647f));
        label.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.12549f));
        label.AddThemeColorOverride("font_outline_color", new Color(0.33f, 0.2475f, 0f));
        label.AddThemeConstantOverride("shadow_offset_x", 5);
        label.AddThemeConstantOverride("shadow_offset_y", 4);
        label.AddThemeConstantOverride("outline_size", 12);
        label.AddThemeFontOverride("font", LoadFont(HeaderFontPath));
        label.AddThemeFontSizeOverride("font_size", 32);
        return label;
    }

    private static RichTextLabel CreateBodyLabel()
    {
        RichTextLabel label = new()
        {
            Name = "Description",
            BbcodeEnabled = true,
            ScrollActive = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        label.AnchorRight = 1f;
        label.AnchorBottom = 1f;
        label.OffsetLeft = 50f;
        label.OffsetTop = 115f;
        label.OffsetRight = -50f;
        label.OffsetBottom = -155f;
        label.AddThemeColorOverride("default_color", new Color(1f, 0.964706f, 0.886275f));
        label.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.25098f));
        label.AddThemeConstantOverride("line_separation", -2);
        label.AddThemeConstantOverride("shadow_offset_x", 3);
        label.AddThemeConstantOverride("shadow_offset_y", 2);
        label.AddThemeFontOverride("normal_font", LoadFont(BodyFontPath));
        label.AddThemeFontOverride("bold_font", LoadFont(BodyBoldFontPath));
        label.AddThemeFontOverride("italics_font", LoadFont(BodyFontPath));
        label.AddThemeFontOverride("bold_italics_font", LoadFont(BodyBoldFontPath));
        label.AddThemeFontOverride("mono_font", LoadFont(BodyFontPath));
        label.AddThemeFontSizeOverride("normal_font_size", 26);
        label.AddThemeFontSizeOverride("bold_font_size", 26);
        label.AddThemeFontSizeOverride("italics_font_size", 26);
        label.AddThemeFontSizeOverride("bold_italics_font_size", 26);
        label.AddThemeFontSizeOverride("mono_font_size", 26);
        return label;
    }

    private static TextureRect CreateTopIcon(string resourcePath, float left, float top)
    {
        TextureRect icon = new()
        {
            Texture = LoadPngTexture(resourcePath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 1,
        };
        icon.OffsetLeft = left;
        icon.OffsetTop = top;
        icon.OffsetRight = left + 51f;
        icon.OffsetBottom = top + 51f;
        return icon;
    }

    private void RegisterBlockingHotkeys()
    {
        NHotkeyManager? hotkeyManager = NHotkeyManager.Instance;
        if (hotkeyManager == null || _hotkeysRegistered)
        {
            return;
        }

        hotkeyManager.AddBlockingScreen(this);
        hotkeyManager.PushHotkeyPressedBinding(MegaInput.confirm, TriggerConfirm);
        hotkeyManager.PushHotkeyPressedBinding(MegaInput.select, TriggerConfirm);
        hotkeyManager.PushHotkeyPressedBinding(MegaInput.cancel, TriggerReturnToFloorStart);
        hotkeyManager.PushHotkeyPressedBinding(MegaInput.pauseAndBack, TriggerReturnToFloorStart);
        _hotkeysRegistered = true;
    }

    private void UnregisterBlockingHotkeys()
    {
        NHotkeyManager? hotkeyManager = NHotkeyManager.Instance;
        if (hotkeyManager == null || !_hotkeysRegistered)
        {
            return;
        }

        hotkeyManager.RemoveHotkeyPressedBinding(MegaInput.confirm, TriggerConfirm);
        hotkeyManager.RemoveHotkeyPressedBinding(MegaInput.select, TriggerConfirm);
        hotkeyManager.RemoveHotkeyPressedBinding(MegaInput.cancel, TriggerReturnToFloorStart);
        hotkeyManager.RemoveHotkeyPressedBinding(MegaInput.pauseAndBack, TriggerReturnToFloorStart);
        hotkeyManager.RemoveBlockingScreen(this);
        _hotkeysRegistered = false;
    }

    private void TriggerConfirm()
    {
        if (_isClosing)
        {
            return;
        }

        _isClosing = true;
        ClosePopup();
        Callable.From(_confirmDeathAction).CallDeferred();
    }

    private void TriggerReturnToFloorStart()
    {
        if (_isClosing)
        {
            return;
        }

        _isClosing = true;
        ClosePopup();
        Callable.From(_returnToFloorStartAction).CallDeferred();
    }

    private void ApplyLocalizedText()
    {
        if (_titleLabel == null || _bodyLabel == null || _confirmButton == null || _returnButton == null)
        {
            return;
        }

        _titleLabel.Text = GetLocalizedText(PopupTitleLoc, TitleFallback);
        _bodyLabel.Text = $"[center]{GetLocalizedText(PopupBodyLoc, BodyFallback)}[/center]";
        _confirmButton.SetText(GetLocalizedText(ConfirmDeathButtonTextLoc, ConfirmDeathFallback));
        _returnButton.SetText(GetLocalizedText(ReturnToFloorStartButtonTextLoc, ReturnToFloorStartFallback));
    }

    private static string GetLocalizedText(LocString locString, string fallback)
    {
        try
        {
            return locString.GetFormattedText();
        }
        catch (Exception ex)
        {
            Log.Warn($"[Sts2AllowUserPause] Failed to resolve popup localization. Using fallback text: {ex.Message}");
            return fallback;
        }
    }

    private static Texture2D? LoadTexture(string resourcePath)
    {
        Texture2D? texture = ResourceLoader.Load<Texture2D>(resourcePath);
        if (texture == null)
        {
            Log.Warn($"[Sts2AllowUserPause] Missing STS2 popup texture: {resourcePath}");
        }

        return texture;
    }

    private static Font? LoadFont(string resourcePath)
    {
        Font? font = ResourceLoader.Load<Font>(resourcePath);
        if (font == null)
        {
            Log.Warn($"[Sts2AllowUserPause] Missing STS2 popup font: {resourcePath}");
        }

        return font;
    }

    private static ShaderMaterial? CreateHsvMaterial(float h, float s, float v)
    {
        Shader? shader = ResourceLoader.Load<Shader>(HsvShaderPath);
        if (shader == null)
        {
            Log.Warn($"[Sts2AllowUserPause] Missing STS2 popup shader: {HsvShaderPath}");
            return null;
        }

        ShaderMaterial material = new()
        {
            ResourceLocalToScene = true,
            Shader = shader,
        };
        material.SetShaderParameter("h", h);
        material.SetShaderParameter("s", s);
        material.SetShaderParameter("v", v);
        return material;
    }

    private static Texture2D? LoadPngTexture(string resourcePath)
    {
        byte[] bytes = Godot.FileAccess.GetFileAsBytes(resourcePath);
        if (bytes.Length == 0)
        {
            Log.Warn($"[Sts2AllowUserPause] Missing icon texture: {resourcePath}");
            return null;
        }

        Image image = new();
        Error error = image.LoadPngFromBuffer(bytes);
        if (error != Error.Ok)
        {
            Log.Warn($"[Sts2AllowUserPause] Failed to decode icon texture: {resourcePath} ({error})");
            return null;
        }

        return ImageTexture.CreateFromImage(image);
    }

    private sealed partial class NativePopupButton : Control
    {
        private static readonly Color HalfTransparentWhite = new(1f, 1f, 1f, 0.501961f);
        private static readonly Color GoldOutline = new("f0b400");

        private readonly bool _isYes;
        private readonly Control _visuals;
        private readonly TextureRect _outline;
        private readonly TextureRect _image;
        private readonly Label _label;
        private readonly ShaderMaterial? _hsv;
        private readonly CanvasItemMaterial _outlineMaterial;
        private readonly float _baseS;
        private readonly float _baseV;
        private Tween? _tween;
        private bool _isPressed;
        private bool _isFocused;

        public event Action? Pressed;

        private NativePopupButton(
            bool isYes,
            string outlineTexturePath,
            string imageTexturePath,
            Vector2 visualsOffsetLeftTop,
            Vector2 visualsOffsetRightBottom,
            Vector2 visualsPivotOffset,
            float shaderH,
            float shaderS,
            float shaderV,
            float labelLeft,
            float labelRight,
            Color labelOutlineColor)
        {
            _isYes = isYes;
            FocusMode = FocusModeEnum.All;
            MouseFilter = MouseFilterEnum.Stop;
            CustomMinimumSize = new Vector2(180f, 72f);

            _outlineMaterial = new CanvasItemMaterial();
            _hsv = CreateHsvMaterial(shaderH, shaderS, shaderV);
            _baseS = shaderS;
            _baseV = shaderV;

            _visuals = new Control
            {
                Name = "Visuals",
                CustomMinimumSize = new Vector2(196f, 96f),
                PivotOffset = visualsPivotOffset,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            _visuals.OffsetLeft = visualsOffsetLeftTop.X;
            _visuals.OffsetTop = visualsOffsetLeftTop.Y;
            _visuals.OffsetRight = visualsOffsetRightBottom.X;
            _visuals.OffsetBottom = visualsOffsetRightBottom.Y;
            AddChild(_visuals);

            _outline = new TextureRect
            {
                Name = "Outline",
                Modulate = HalfTransparentWhite,
                SelfModulate = Colors.Black,
                ShowBehindParent = isYes,
                Material = _outlineMaterial,
                Texture = LoadTexture(outlineTexturePath),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspect,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            _outline.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            _visuals.AddChild(_outline);

            _image = new TextureRect
            {
                Name = "Image",
                Material = _hsv,
                Texture = LoadTexture(imageTexturePath),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspect,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            _image.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            _visuals.AddChild(_image);

            _label = new Label
            {
                Name = "Label",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                ClipText = true,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            _label.AnchorRight = 1f;
            _label.AnchorBottom = 1f;
            _label.OffsetLeft = labelLeft;
            _label.OffsetTop = -3f;
            _label.OffsetRight = labelRight;
            _label.OffsetBottom = -3f;
            _label.AddThemeColorOverride("font_color", new Color(1f, 0.964706f, 0.886275f));
            _label.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.12549f));
            _label.AddThemeColorOverride("font_outline_color", labelOutlineColor);
            _label.AddThemeConstantOverride("shadow_offset_x", 5);
            _label.AddThemeConstantOverride("shadow_offset_y", 4);
            _label.AddThemeConstantOverride("outline_size", 16);
            _label.AddThemeFontOverride("font", LoadFont(HeaderFontPath));
            _visuals.AddChild(_label);

            MouseEntered += GrabFocus;
            FocusEntered += OnFocusVisual;
            FocusExited += OnUnfocusVisual;
        }

        public static NativePopupButton CreateCancel()
        {
            NativePopupButton button = new(
                isYes: false,
                outlineTexturePath: CancelButtonOutlineTexturePath,
                imageTexturePath: CancelButtonTexturePath,
                visualsOffsetLeftTop: new Vector2(0f, -8f),
                visualsOffsetRightBottom: new Vector2(196f, 88f),
                visualsPivotOffset: new Vector2(5f, 50f),
                shaderH: 1f,
                shaderS: 0.75f,
                shaderV: 1.2f,
                labelLeft: 15f,
                labelRight: -31f,
                labelOutlineColor: new Color(0.35f, 0.07f, 0f));
            button.AnchorTop = 1f;
            button.AnchorBottom = 1f;
            button.OffsetLeft = 0f;
            button.OffsetTop = -152f;
            button.OffsetRight = 180f;
            button.OffsetBottom = -80f;
            return button;
        }

        public static NativePopupButton CreateConfirm()
        {
            NativePopupButton button = new(
                isYes: true,
                outlineTexturePath: ConfirmButtonOutlineTexturePath,
                imageTexturePath: ConfirmButtonTexturePath,
                visualsOffsetLeftTop: new Vector2(-17f, -8f),
                visualsOffsetRightBottom: new Vector2(179f, 88f),
                visualsPivotOffset: new Vector2(191f, 50f),
                shaderH: 0.75f,
                shaderS: 1.2f,
                shaderV: 1.1f,
                labelLeft: 31f,
                labelRight: -15f,
                labelOutlineColor: new Color(0.121333f, 0.28f, 0f));
            button.AnchorLeft = 1f;
            button.AnchorTop = 1f;
            button.AnchorRight = 1f;
            button.AnchorBottom = 1f;
            button.OffsetLeft = -180f;
            button.OffsetTop = -152f;
            button.OffsetRight = 0f;
            button.OffsetBottom = -80f;
            return button;
        }

        public void SetText(string text)
        {
            _label.Text = text;
            int fontSize = text.Length > 10 ? 23 : 28;
            _label.AddThemeFontSizeOverride("font_size", fontSize);
        }

        public override void _GuiInput(InputEvent inputEvent)
        {
            if (inputEvent is InputEventMouseButton { ButtonIndex: MouseButton.Left } mouseButton)
            {
                if (mouseButton.Pressed)
                {
                    _isPressed = true;
                    GrabFocus();
                    OnPressVisual();
                    AcceptEvent();
                    return;
                }

                if (_isPressed)
                {
                    _isPressed = false;
                    OnReleaseVisual();
                    Pressed?.Invoke();
                    AcceptEvent();
                }
            }
        }

        private void OnFocusVisual()
        {
            if (_isFocused)
            {
                return;
            }

            _isFocused = true;
            _outlineMaterial.BlendMode = CanvasItemMaterial.BlendModeEnum.Add;
            _outline.Modulate = Colors.White;
            _outline.SelfModulate = GoldOutline;
            _tween?.Kill();
            _tween = CreateTween().SetParallel();
            _tween.TweenProperty(_visuals, "scale", Vector2.One * 1.025f, 0.05);
            TweenShaderParameter("s", _baseS + 0.25f, 0.05, Tween.EaseType.Out, Tween.TransitionType.Expo);
            TweenShaderParameter("v", _baseV + 0.25f, 0.05, Tween.EaseType.Out, Tween.TransitionType.Expo);
        }

        private void OnUnfocusVisual()
        {
            _isFocused = false;
            _outlineMaterial.BlendMode = CanvasItemMaterial.BlendModeEnum.Mix;
            _outline.Modulate = HalfTransparentWhite;
            _outline.SelfModulate = Colors.Black;
            _tween?.Kill();
            _tween = CreateTween().SetParallel();
            _tween.TweenProperty(_visuals, "scale", Vector2.One, 0.5).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
            TweenShaderParameter("s", _baseS, 0.5, Tween.EaseType.Out, Tween.TransitionType.Expo);
            TweenShaderParameter("v", _baseV, 0.5, Tween.EaseType.Out, Tween.TransitionType.Expo);
        }

        private void OnPressVisual()
        {
            _tween?.Kill();
            _tween = CreateTween().SetParallel();
            _tween.TweenProperty(_visuals, "scale", Vector2.One * 0.975f, 0.5).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
            TweenShaderParameter("s", _baseS - 0.1f, 0.5, Tween.EaseType.Out, Tween.TransitionType.Expo);
            TweenShaderParameter("v", _baseV - 0.1f, 0.5, Tween.EaseType.Out, Tween.TransitionType.Expo);
        }

        private void OnReleaseVisual()
        {
            _isFocused = false;
            _outlineMaterial.BlendMode = CanvasItemMaterial.BlendModeEnum.Mix;
            _outline.Modulate = HalfTransparentWhite;
            _outline.SelfModulate = Colors.Black;
            _tween?.Kill();
            _tween = CreateTween().SetParallel();
            _tween.TweenProperty(_visuals, "scale", Vector2.One, 0.05);
            TweenShaderParameter("s", _baseS, 0.05, Tween.EaseType.InOut, Tween.TransitionType.Linear);
            TweenShaderParameter("v", _baseV, 0.05, Tween.EaseType.InOut, Tween.TransitionType.Linear);
        }

        private void TweenShaderParameter(string parameterName, float to, double duration, Tween.EaseType ease, Tween.TransitionType transition)
        {
            if (_hsv == null || _tween == null)
            {
                return;
            }

            float from = (float)_hsv.GetShaderParameter(parameterName);
            _tween.TweenMethod(
                    Callable.From<float>(value => _hsv.SetShaderParameter(parameterName, value)),
                    from,
                    to,
                    duration)
                .SetEase(ease)
                .SetTrans(transition);
        }
    }
}
