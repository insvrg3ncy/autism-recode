using System;
using CerberusWareV3.Interface;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Input;
using Robust.Shared.Maths;
using DependencyAttribute = Robust.Shared.IoC.DependencyAttribute;

namespace CerberusWareV3.Interface.Controls;

public class KeyBindInputControl : Control
{
    [Dependency] private readonly IEntitySystemManager _sysMan = default!;
    
    private Keyboard.Key _keyBind;
    private bool _isWaitingForKey;
    private readonly Label _label;
    private readonly Button _keyButton;
    private readonly PanelContainer _buttonPanel;
    private HotkeyInputSystem? _hotkeySystem;

    public Keyboard.Key KeyBind
    {
        get => _keyBind;
        set
        {
            if (_keyBind != value)
            {
                _keyBind = value;
                _isWaitingForKey = false;
                _hotkeySystem?.UnregisterWaitingControl(this);
                UpdateButtonText();
                KeyBindChanged?.Invoke(_keyBind);
            }
        }
    }

    public event Action<Keyboard.Key>? KeyBindChanged;

    public string LabelText
    {
        get => _label.Text;
        set => _label.Text = value;
    }

    public KeyBindInputControl(string label = "")
    {
        IoCManager.InjectDependencies(this);
        _hotkeySystem = _sysMan.GetEntitySystem<HotkeyInputSystem>();
        
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

        _buttonPanel = new PanelContainer
        {
            MinWidth = 110,
            MinHeight = 35,
            VerticalAlignment = VAlignment.Center
        };

        _keyButton = new Button
        {
            Text = "None",
            HorizontalExpand = true,
            VerticalExpand = true
        };
        _keyButton.OnPressed += OnButtonPressed;

        _buttonPanel.AddChild(_keyButton);
        container.AddChild(_label);
        container.AddChild(_buttonPanel);

        AddChild(container);

        OnKeyBindDown += OnKeyDown;
        OnKeyBindUp += OnKeyUp;
    }


    private void OnButtonPressed(BaseButton.ButtonEventArgs args)
    {
        _isWaitingForKey = !_isWaitingForKey;
        
        if (_isWaitingForKey)
        {
            _hotkeySystem?.RegisterWaitingControl(this);
        }
        else
        {
            _hotkeySystem?.UnregisterWaitingControl(this);
        }
        
        UpdateButtonText();
    }

    private void OnKeyDown(GUIBoundKeyEventArgs args)
    {
        if (!_isWaitingForKey)
            return;

        if (args.Function == EngineKeyFunctions.UIClick)
        {
            // Mouse buttons will be handled by HotkeyInputSystem
            return;
        }

        // Handle Escape to cancel
        if (args.Function == EngineKeyFunctions.CloseModals)
        {
            _isWaitingForKey = false;
            UpdateButtonText();
            args.Handle();
        }
    }

    private void OnKeyUp(GUIBoundKeyEventArgs args)
    {
        if (!_isWaitingForKey)
            return;

        if (args.Function == EngineKeyFunctions.CloseModals)
        {
            _isWaitingForKey = false;
            UpdateButtonText();
            args.Handle();
        }
    }

    private void UpdateButtonText()
    {
        if (_isWaitingForKey)
        {
            _keyButton.Text = "Press Key...";
            if (_buttonPanel.PanelOverride is StyleBoxFlat flat)
            {
                flat.BackgroundColor = new Color(237, 128, 97); // BindWaitColor
            }
        }
        else
        {
            _keyButton.Text = _keyBind == Keyboard.Key.Unknown ? "[None]" : _keyBind.ToString();
            if (_buttonPanel.PanelOverride is StyleBoxFlat flat)
            {
                flat.BackgroundColor = new Color(50, 50, 50);
            }
        }
    }

}

