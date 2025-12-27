using System;
using System.Collections.Generic;
using System.Linq;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace CerberusWareV3.Interface.Controls;

public class ComboControl : Control
{
    private int _selectedIndex;
    private string[] _options = Array.Empty<string>();
    private readonly Label _label;
    private readonly OptionButton _optionButton;

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (value >= 0 && value < _options.Length && _selectedIndex != value)
            {
                _selectedIndex = value;
                _optionButton.SelectId(value);
                SelectedIndexChanged?.Invoke(value);
            }
        }
    }

    public string[] Options
    {
        get => _options;
        set
        {
            _options = value ?? Array.Empty<string>();
            _optionButton.Clear();
            foreach (var option in _options)
            {
                _optionButton.AddItem(option);
            }
            if (_selectedIndex >= _options.Length)
            {
                _selectedIndex = Math.Max(0, _options.Length - 1);
            }
            if (_options.Length > 0)
            {
                _optionButton.SelectId(_selectedIndex);
            }
        }
    }

    public string LabelText
    {
        get => _label.Text;
        set => _label.Text = value;
    }

    public event Action<int>? SelectedIndexChanged;

    public ComboControl(string label = "", string[]? options = null)
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

        _optionButton = new OptionButton
        {
            MinWidth = 200,
            VerticalAlignment = VAlignment.Center
        };
        _optionButton.OnItemSelected += args => 
        {
            _selectedIndex = args.Id;
            SelectedIndexChanged?.Invoke(_selectedIndex);
        };

        container.AddChild(_label);
        container.AddChild(_optionButton);

        AddChild(container);

        if (options != null)
        {
            Options = options;
        }
    }
}


