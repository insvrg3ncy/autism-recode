using System;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace CerberusWareV3.Interface.Controls;

public class SliderWithSettingsControl : Control
{
    private float _value;
    private float _min;
    private float _max;
    private readonly Label _label;
    private readonly Label _valueLabel;
    private readonly SliderControl _slider;
    private readonly Button _settingsButton;
    private readonly PanelContainer _settingsButtonPanel;

    public float Value
    {
        get => _value;
        set
        {
            var clamped = Math.Clamp(value, _min, _max);
            if (Math.Abs(_value - clamped) > 0.001f)
            {
                _value = clamped;
                _slider.Value = clamped;
                UpdateValueLabel();
                ValueChanged?.Invoke(_value);
            }
        }
    }

    public float Min
    {
        get => _min;
        set
        {
            _min = value;
            _slider.Min = value;
            Value = Math.Clamp(Value, _min, _max);
        }
    }

    public float Max
    {
        get => _max;
        set
        {
            _max = value;
            _slider.Max = value;
            Value = Math.Clamp(Value, _min, _max);
        }
    }

    public string LabelText
    {
        get => _label.Text;
        set => _label.Text = value;
    }

    public string Format { get; set; } = "F2";
    public string UniquePopupId { get; set; } = "";

    public event Action<float>? ValueChanged;
    public event Action<string>? SettingsClicked;

    public SliderWithSettingsControl(string label = "", float min = 0f, float max = 100f, float value = 0f, string uniquePopupId = "")
    {
        MinHeight = 70;
        HorizontalExpand = true;
        UniquePopupId = uniquePopupId;

        _min = min;
        _max = max;
        _value = Math.Clamp(value, min, max);

        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(10, 5)
        };

        var header = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true
        };

        _label = new Label
        {
            Text = label,
            VerticalAlignment = VAlignment.Center,
            HorizontalExpand = true
        };

        _valueLabel = new Label
        {
            Text = _value.ToString(Format),
            VerticalAlignment = VAlignment.Center
        };

        _settingsButtonPanel = new PanelContainer
        {
            MinWidth = 32,
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

        header.AddChild(_label);
        header.AddChild(_valueLabel);
        header.AddChild(_settingsButtonPanel);

        _slider = new SliderControl("", min, max, value);
        _slider.Format = Format;
        _slider.ValueChanged += v =>
        {
            _value = v;
            UpdateValueLabel();
            ValueChanged?.Invoke(v);
        };

        container.AddChild(header);
        container.AddChild(_slider);

        AddChild(container);
    }

    private void UpdateValueLabel()
    {
        _valueLabel.Text = _value.ToString(Format);
    }
}

