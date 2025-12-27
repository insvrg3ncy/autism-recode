using System;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace CerberusWareV3.Interface.Controls;

public class ColorPickerControl : Control
{
    private Vector4 _color;
    private readonly Label _label;
    private readonly PanelContainer _colorPreview;
    private readonly Slider _rSlider;
    private readonly Slider _gSlider;
    private readonly Slider _bSlider;
    private readonly Slider _aSlider;
    private readonly Label _rLabel;
    private readonly Label _gLabel;
    private readonly Label _bLabel;
    private readonly Label _aLabel;

    public Vector4 Color
    {
        get => _color;
        set
        {
            if (_color != value)
            {
                _color = value;
                UpdateFromColor();
                ColorChanged?.Invoke(_color);
            }
        }
    }

    public string LabelText
    {
        get => _label.Text;
        set => _label.Text = value;
    }

    public event Action<Vector4>? ColorChanged;

    public ColorPickerControl(string label = "")
    {
        MinHeight = 120;
        HorizontalExpand = true;

        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(10, 5)
        };

        var header = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            Margin = new Thickness(0, 0, 0, 5)
        };

        _label = new Label
        {
            Text = label,
            VerticalAlignment = VAlignment.Center,
            HorizontalExpand = true
        };

        _colorPreview = new PanelContainer
        {
            MinWidth = 80,
            MinHeight = 40,
            VerticalAlignment = VAlignment.Center
        };
        _colorPreview.PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = new Color(1f, 1f, 1f, 1f),
            BorderThickness = new Thickness(2),
            BorderColor = new Color(0.5f, 0.5f, 0.5f, 1f)
        };

        header.AddChild(_label);
        header.AddChild(_colorPreview);

        // R slider
        var rContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            Margin = new Thickness(0, 2)
        };
        _rLabel = new Label
        {
            Text = "R: 1.000",
            MinWidth = 60,
            VerticalAlignment = VAlignment.Center
        };
        _rSlider = new Slider
        {
            MinValue = 0f,
            MaxValue = 1f,
            Value = 1f,
            HorizontalExpand = true
        };
        _rSlider.OnValueChanged += _ =>
        {
            _color.X = _rSlider.Value;
            _rLabel.Text = $"R: {_color.X:F3}";
            UpdatePreview();
            ColorChanged?.Invoke(_color);
        };
        rContainer.AddChild(_rLabel);
        rContainer.AddChild(_rSlider);

        // G slider
        var gContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            Margin = new Thickness(0, 2)
        };
        _gLabel = new Label
        {
            Text = "G: 1.000",
            MinWidth = 60,
            VerticalAlignment = VAlignment.Center
        };
        _gSlider = new Slider
        {
            MinValue = 0f,
            MaxValue = 1f,
            Value = 1f,
            HorizontalExpand = true
        };
        _gSlider.OnValueChanged += _ =>
        {
            _color.Y = _gSlider.Value;
            _gLabel.Text = $"G: {_color.Y:F3}";
            UpdatePreview();
            ColorChanged?.Invoke(_color);
        };
        gContainer.AddChild(_gLabel);
        gContainer.AddChild(_gSlider);

        // B slider
        var bContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            Margin = new Thickness(0, 2)
        };
        _bLabel = new Label
        {
            Text = "B: 1.000",
            MinWidth = 60,
            VerticalAlignment = VAlignment.Center
        };
        _bSlider = new Slider
        {
            MinValue = 0f,
            MaxValue = 1f,
            Value = 1f,
            HorizontalExpand = true
        };
        _bSlider.OnValueChanged += _ =>
        {
            _color.Z = _bSlider.Value;
            _bLabel.Text = $"B: {_color.Z:F3}";
            UpdatePreview();
            ColorChanged?.Invoke(_color);
        };
        bContainer.AddChild(_bLabel);
        bContainer.AddChild(_bSlider);

        // A slider
        var aContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            Margin = new Thickness(0, 2)
        };
        _aLabel = new Label
        {
            Text = "A: 1.000",
            MinWidth = 60,
            VerticalAlignment = VAlignment.Center
        };
        _aSlider = new Slider
        {
            MinValue = 0f,
            MaxValue = 1f,
            Value = 1f,
            HorizontalExpand = true
        };
        _aSlider.OnValueChanged += _ =>
        {
            _color.W = _aSlider.Value;
            _aLabel.Text = $"A: {_color.W:F3}";
            UpdatePreview();
            ColorChanged?.Invoke(_color);
        };
        aContainer.AddChild(_aLabel);
        aContainer.AddChild(_aSlider);

        container.AddChild(header);
        container.AddChild(rContainer);
        container.AddChild(gContainer);
        container.AddChild(bContainer);
        container.AddChild(aContainer);

        AddChild(container);

        _color = new Vector4(1f, 1f, 1f, 1f);
        UpdateFromColor();
    }

    private void UpdateFromColor()
    {
        _rSlider.Value = _color.X;
        _gSlider.Value = _color.Y;
        _bSlider.Value = _color.Z;
        _aSlider.Value = _color.W;
        _rLabel.Text = $"R: {_color.X:F3}";
        _gLabel.Text = $"G: {_color.Y:F3}";
        _bLabel.Text = $"B: {_color.Z:F3}";
        _aLabel.Text = $"A: {_color.W:F3}";
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (_colorPreview.PanelOverride is StyleBoxFlat flat)
        {
            flat.BackgroundColor = new Color(_color.X, _color.Y, _color.Z, _color.W);
        }
    }
}


