using System;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace CerberusWareV3.Interface.Controls;

public class ToggleWithSettingsControl : Control
{
    private bool _value;
    private float _animationProgress;
    private readonly Label _label;
    private readonly ToggleControl _toggle;
    private readonly Button _settingsButton;
    private readonly PanelContainer _settingsButtonPanel;

    public bool Value
    {
        get => _value;
        set
        {
            if (_value != value)
            {
                _value = value;
                _toggle.Value = value;
                ValueChanged?.Invoke(value);
            }
        }
    }

    public string LabelText
    {
        get => _label.Text;
        set => _label.Text = value;
    }

    public string UniquePopupId { get; set; } = "";

    public event Action<bool>? ValueChanged;
    public event Action<string>? SettingsClicked;

    public ToggleWithSettingsControl(string label = "", string uniquePopupId = "")
    {
        MinHeight = 50;
        HorizontalExpand = true;
        UniquePopupId = uniquePopupId;

        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            VerticalExpand = true,
            Margin = new Thickness(10, 0)
        };

        _label = new Label
        {
            Text = label,
            VerticalAlignment = VAlignment.Center,
            HorizontalExpand = true
        };

        _settingsButtonPanel = new PanelContainer
        {
            MinWidth = 35,
            MinHeight = 30,
            VerticalAlignment = VAlignment.Center,
            Margin = new Thickness(5, 0, 0, 0)
        };
        _settingsButtonPanel.PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = new Color(50, 50, 55),
            BorderThickness = new Thickness(0)
        };

        _settingsButton = new Button
        {
            Text = "⚙",
            HorizontalExpand = true,
            VerticalExpand = true
        };
        _settingsButton.OnPressed += _ =>
        {
            SettingsClicked?.Invoke(UniquePopupId);
        };

        _settingsButtonPanel.AddChild(_settingsButton);

        _toggle = new ToggleControl("");
        _toggle.ValueChanged += v =>
        {
            _value = v;
            ValueChanged?.Invoke(v);
        };

        container.AddChild(_label);
        container.AddChild(_settingsButtonPanel);
        container.AddChild(_toggle);

        AddChild(container);
    }
}

