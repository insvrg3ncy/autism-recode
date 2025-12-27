using System;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace CerberusWareV3.Interface.Controls;

public class InputTextWithColorControl : Control
{
    private string _text;
    private Vector4 _color;
    private readonly Label _label;
    private readonly LineEdit _textInput;
    private readonly ColorPickerControl _colorPicker;
    private readonly Button _deleteButton;

    public string Text
    {
        get => _text;
        set
        {
            if (_text != value)
            {
                _text = value;
                _textInput.Text = value;
                TextChanged?.Invoke(_text);
            }
        }
    }

    public Vector4 Color
    {
        get => _color;
        set
        {
            if (_color != value)
            {
                _color = value;
                _colorPicker.Color = value;
                ColorChanged?.Invoke(_color);
            }
        }
    }

    public string LabelText
    {
        get => _label.Text;
        set => _label.Text = value;
    }

    public event Action<string>? TextChanged;
    public event Action<Vector4>? ColorChanged;
    public event Action? DeletePressed;

    public InputTextWithColorControl(string label = "", string text = "", Vector4? color = null)
    {
        MinHeight = 50;
        HorizontalExpand = true;

        _text = text;
        _color = color ?? Vector4.One;

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
            MinWidth = 100
        };

        _textInput = new LineEdit
        {
            Text = text,
            HorizontalExpand = true,
            MinHeight = 30,
            VerticalAlignment = VAlignment.Center
        };
        _textInput.OnTextChanged += args => 
        {
            _text = args.Text;
            TextChanged?.Invoke(_text);
        };

        _colorPicker = new ColorPickerControl("")
        {
            MinWidth = 40,
            MinHeight = 30,
            VerticalAlignment = VAlignment.Center
        };
        _colorPicker.Color = _color;
        _colorPicker.ColorChanged += v =>
        {
            _color = v;
            ColorChanged?.Invoke(_color);
        };

        _deleteButton = new Button
        {
            Text = "-",
            MinWidth = 40,
            MinHeight = 30,
            VerticalAlignment = VAlignment.Center
        };
        _deleteButton.OnPressed += _ => DeletePressed?.Invoke();

        container.AddChild(_label);
        container.AddChild(_textInput);
        container.AddChild(_colorPicker);
        container.AddChild(_deleteButton);

        AddChild(container);
    }
}

