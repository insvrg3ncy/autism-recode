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
    private readonly SliderControl _rSlider;
    private readonly SliderControl _gSlider;
    private readonly SliderControl _bSlider;
    private readonly SliderControl _aSlider;

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
        MinHeight = 150;
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
            MinWidth = 60,
            MinHeight = 30,
            VerticalAlignment = VAlignment.Center
        };
        _colorPreview.PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = new Color(1f, 1f, 1f, 1f),
            BorderThickness = new Thickness(1),
            BorderColor = new Color(0.5f, 0.5f, 0.5f, 1f)
        };

        header.AddChild(_label);
        header.AddChild(_colorPreview);

        _rSlider = new SliderControl("R", 0f, 1f, 1f) { Format = "F3" };
        _gSlider = new SliderControl("G", 0f, 1f, 1f) { Format = "F3" };
        _bSlider = new SliderControl("B", 0f, 1f, 1f) { Format = "F3" };
        _aSlider = new SliderControl("A", 0f, 1f, 1f) { Format = "F3" };

        _rSlider.ValueChanged += v => { _color.X = v; UpdatePreview(); ColorChanged?.Invoke(_color); };
        _gSlider.ValueChanged += v => { _color.Y = v; UpdatePreview(); ColorChanged?.Invoke(_color); };
        _bSlider.ValueChanged += v => { _color.Z = v; UpdatePreview(); ColorChanged?.Invoke(_color); };
        _aSlider.ValueChanged += v => { _color.W = v; UpdatePreview(); ColorChanged?.Invoke(_color); };

        container.AddChild(header);
        container.AddChild(_rSlider);
        container.AddChild(_gSlider);
        container.AddChild(_bSlider);
        container.AddChild(_aSlider);

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


