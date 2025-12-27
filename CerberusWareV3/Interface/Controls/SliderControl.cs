using System;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Timing;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Maths;

namespace CerberusWareV3.Interface.Controls;

public class SliderControl : Control
{
    private float _value;
    private float _min;
    private float _max;
    private readonly Label _label;
    private readonly Label _valueLabel;
    private readonly PanelContainer _sliderTrack;
    private readonly PanelContainer _sliderFill;
    private readonly PanelContainer _sliderHandle;
    private bool _isDragging;

    public float Value
    {
        get => _value;
        set
        {
            var clamped = Math.Clamp(value, _min, _max);
            if (Math.Abs(_value - clamped) > 0.001f)
            {
                _value = clamped;
                UpdateDisplay();
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
            Value = Math.Clamp(Value, _min, _max);
        }
    }

    public float Max
    {
        get => _max;
        set
        {
            _max = value;
            Value = Math.Clamp(Value, _min, _max);
        }
    }

    public string LabelText
    {
        get => _label.Text;
        set => _label.Text = value;
    }

    public string Format { get; set; } = "F2";

    public event Action<float>? ValueChanged;

    public SliderControl(string label = "", float min = 0f, float max = 100f, float value = 0f)
    {
        MinHeight = 50;
        HorizontalExpand = true;

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

        header.AddChild(_label);
        header.AddChild(_valueLabel);

        _sliderTrack = new PanelContainer
        {
            MinHeight = 6,
            HorizontalExpand = true,
            Margin = new Thickness(0, 5, 0, 0)
        };
        _sliderTrack.PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = new Color(50, 50, 50),
            BorderThickness = new Thickness(0)
        };

        _sliderFill = new PanelContainer
        {
            MinHeight = 6,
            HorizontalAlignment = HAlignment.Left
        };
        _sliderFill.PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = new Color(0, 150, 255),
            BorderThickness = new Thickness(0)
        };

        _sliderHandle = new PanelContainer
        {
            MinWidth = 16,
            MinHeight = 16,
            HorizontalAlignment = HAlignment.Left,
            VerticalAlignment = VAlignment.Center
        };
        _sliderHandle.PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = new Color(200, 200, 200),
            BorderThickness = new Thickness(1),
            BorderColor = new Color(100, 100, 100)
        };

        var sliderContainer = new Control
        {
            MinHeight = 20,
            HorizontalExpand = true
        };
        sliderContainer.AddChild(_sliderTrack);
        sliderContainer.AddChild(_sliderFill);
        sliderContainer.AddChild(_sliderHandle);

        container.AddChild(header);
        container.AddChild(sliderContainer);

        AddChild(container);

        OnKeyBindDown += OnKeyDown;
        OnKeyBindUp += OnKeyUp;

        UpdateDisplay();
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        if (_isDragging)
        {
            UpdateValueFromPosition(args.RelativePosition.X);
        }
    }

    private void OnKeyDown(GUIBoundKeyEventArgs args)
    {
        if (args.Function == EngineKeyFunctions.UIClick && _sliderTrack.PixelRect.Contains(new Robust.Shared.Maths.Vector2i((int)args.RelativePosition.X, (int)args.RelativePosition.Y)))
        {
            _isDragging = true;
            UpdateValueFromPosition(args.RelativePosition.X);
            args.Handle();
        }
    }

    private void OnKeyUp(GUIBoundKeyEventArgs args)
    {
        if (args.Function == EngineKeyFunctions.UIClick)
        {
            _isDragging = false;
        }
    }

    private void UpdateValueFromPosition(float x)
    {
        var trackRect = _sliderTrack.PixelRect;
        if (trackRect.Width <= 0) return;
        var normalized = Math.Clamp((x - trackRect.Left) / trackRect.Width, 0f, 1f);
        Value = _min + normalized * (_max - _min);
    }

    private void UpdateDisplay()
    {
        _valueLabel.Text = _value.ToString(Format);
        
        var normalized = (_value - _min) / (_max - _min);
        var trackWidth = _sliderTrack.Size.X;
        
        _sliderFill.MinWidth = trackWidth * normalized;
        LayoutContainer.SetPosition(_sliderHandle, new System.Numerics.Vector2(trackWidth * normalized - 8, 2));
    }

    protected override void Resized()
    {
        base.Resized();
        UpdateDisplay();
    }
}


