using System;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Timing;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace CerberusWareV3.Interface.Controls;

public class ToggleControl : Control
{
    private bool _value;
    private float _animationProgress;
    private readonly Label _label;
    private readonly PanelContainer _toggleContainer;
    private readonly PanelContainer _knob;

    public bool Value
    {
        get => _value;
        set
        {
            if (_value != value)
            {
                _value = value;
                ValueChanged?.Invoke(value);
            }
        }
    }

    public string LabelText
    {
        get => _label.Text;
        set => _label.Text = value;
    }

    public event Action<bool>? ValueChanged;

    public ToggleControl(string label = "")
    {
        MinHeight = 50;
        HorizontalExpand = true;

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

        _toggleContainer = new PanelContainer
        {
            MinWidth = 50,
            MinHeight = 23,
            VerticalAlignment = VAlignment.Center
        };
        _toggleContainer.PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = new Color(35, 36, 40),
            BorderThickness = new Thickness(0)
        };

        _knob = new PanelContainer
        {
            MinWidth = 23,
            MinHeight = 23,
            HorizontalAlignment = HAlignment.Left
        };
        _knob.PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = new Color(100, 100, 100),
            BorderThickness = new Thickness(0)
        };

        _toggleContainer.AddChild(_knob);

        container.AddChild(_label);
        container.AddChild(_toggleContainer);

        AddChild(container);

        OnKeyBindDown += OnKeyDown;
    }

    private void OnKeyDown(GUIBoundKeyEventArgs args)
    {
        if (args.Function == EngineKeyFunctions.UIClick)
        {
            Value = !Value;
            args.Handle();
        }
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        float target = Value ? 1f : 0f;
        float speed = 10f * (float)args.DeltaSeconds;
        
        if (_animationProgress < target)
        {
            _animationProgress = Math.Min(_animationProgress + speed, target);
        }
        else if (_animationProgress > target)
        {
            _animationProgress = Math.Max(_animationProgress - speed, target);
        }

                var knobColor = Color.InterpolateBetween(new Color(100, 100, 100), new Color(0, 200, 0), _animationProgress);
                if (_knob.PanelOverride is StyleBoxFlat flat)
                {
                    flat.BackgroundColor = knobColor;
                }

                var knobX = _animationProgress * (_toggleContainer.Size.X - _knob.Size.X);
                LayoutContainer.SetPosition(_knob, new System.Numerics.Vector2(knobX, 0));
    }
}


