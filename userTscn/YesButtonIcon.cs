using Godot;

namespace Sts2AllowUserPause.UserTscn;

public partial class YesButtonIcon : TextureRect
{
    [Export]
    public Texture2D? DefaultIcon { get; set; }

    private Control? _button;

    public override void _Ready()
    {
        ExpandMode = ExpandModeEnum.IgnoreSize;
        StretchMode = StretchModeEnum.KeepAspectCentered;
        MouseFilter = MouseFilterEnum.Ignore;

        _button = GetParent().GetNodeOrNull<Control>("YesButton");
        if (Texture == null)
        {
            Texture = DefaultIcon;
        }

        if (_button != null)
        {
            _button.Connect(CanvasItem.SignalName.VisibilityChanged, Callable.From(RefreshVisibility));
        }

        RefreshVisibility();
    }

    public void SetIcon(Texture2D? icon)
    {
        Texture = icon ?? DefaultIcon;
        RefreshVisibility();
    }

    private void RefreshVisibility()
    {
        Visible = Texture != null && (_button?.Visible ?? true);
    }
}
