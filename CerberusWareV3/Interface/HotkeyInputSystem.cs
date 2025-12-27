using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CerberusWareV3.Interface.Controls;
using Robust.Client.Input;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Input;
using Robust.Shared.Timing;
using DependencyAttribute = Robust.Shared.IoC.DependencyAttribute;

[CompilerGenerated]
public sealed class HotkeyInputSystem : EntitySystem
{
    [Dependency] private readonly IInputManager _inputManager = default!;
    
    private readonly List<KeyBindInputControl> _waitingControls = new();
    private Keyboard.Key? _lastPressedKey;

    public override void Update(float frameTime)
    {
        // Check for any key presses
        foreach (var key in Enum.GetValues<Keyboard.Key>())
        {
            if (key == Keyboard.Key.Unknown)
                continue;

            if (_inputManager.IsKeyDown(key))
            {
                if (_lastPressedKey != key)
                {
                    _lastPressedKey = key;
                    HandleKeyPress(key);
                }
            }
            else if (_lastPressedKey == key)
            {
                _lastPressedKey = null;
            }
        }
    }

    public void RegisterWaitingControl(KeyBindInputControl control)
    {
        if (!_waitingControls.Contains(control))
        {
            _waitingControls.Add(control);
        }
    }

    public void UnregisterWaitingControl(KeyBindInputControl control)
    {
        _waitingControls.Remove(control);
    }

    private void HandleKeyPress(Keyboard.Key key)
    {
        if (_waitingControls.Count == 0)
            return;

        if (key != Keyboard.Key.Unknown)
        {
            // Update all waiting controls
            foreach (var control in _waitingControls.ToArray())
            {
                control.KeyBind = key;
                UnregisterWaitingControl(control);
            }
        }
    }
}

