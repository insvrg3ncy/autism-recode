using System;
using System.Runtime.CompilerServices;
using CerberusWareV3.Configuration;
using CerberusWareV3.Interface;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Timing;
using DependencyAttribute = Robust.Shared.IoC.DependencyAttribute;

[CompilerGenerated]
public sealed class UiRegistrationSystem : EntitySystem
{
	[Dependency] private readonly IUserInterfaceManager _uiManager = default!;
	[Dependency] private readonly IEntitySystemManager _sysMan = default!;
	[Dependency] private readonly IInputManager _inputManager = default!;
	[Dependency] private readonly IGameTiming _gameTiming = default!;
	
	private MainMenuWindow? _mainMenuWindow;
	private readonly Dictionary<Keyboard.Key, bool> _lastKeyStates = new();
	private readonly Dictionary<Keyboard.Key, bool> _keyStates = new();

	public override void Initialize()
	{
		// Menu will be opened via hotkey handling
	}

	public override void Shutdown()
	{
		if (_mainMenuWindow != null && _mainMenuWindow.IsOpen)
		{
			_mainMenuWindow.Close();
		}
	}

	public override void Update(float frameTime)
	{
		UpdateKeyStates();
		HandleHotkeys();
	}

	private void UpdateKeyStates()
	{
		// Update all tracked keys
		var keysToCheck = new[]
		{
			CerberusConfig.Settings.ShowMenuHotKey,
			CerberusConfig.Eye.FovHotKey,
			CerberusConfig.Eye.FullBrightHotKey,
			CerberusConfig.Eye.ZoomUpHotKey,
			CerberusConfig.Eye.ZoomDownHotKey,
			CerberusConfig.StorageViewer.HotKey,
			CerberusConfig.GunAimBot.HotKey,
			CerberusConfig.MeleeAimBot.LightHotKey,
			CerberusConfig.MeleeAimBot.HeavyHotKey
		};

		foreach (var key in keysToCheck)
		{
			if (key == Keyboard.Key.Unknown)
				continue;

			var isDown = _inputManager.IsKeyDown(key);
			_keyStates[key] = isDown;
			
			if (!_lastKeyStates.ContainsKey(key))
				_lastKeyStates[key] = false;
		}
	}

	private void HandleHotkeys()
	{
		// Menu toggle
		var menuKey = CerberusConfig.Settings.ShowMenuHotKey;
		if (menuKey != Keyboard.Key.Unknown && IsKeyJustPressed(menuKey))
		{
			ToggleMenu();
		}

		// FOV toggle
		var fovKey = CerberusConfig.Eye.FovHotKey;
		if (fovKey != Keyboard.Key.Unknown && IsKeyJustPressed(fovKey))
		{
			CerberusConfig.Eye.FovEnabled = !CerberusConfig.Eye.FovEnabled;
		}

		// FullBright toggle
		var fullBrightKey = CerberusConfig.Eye.FullBrightHotKey;
		if (fullBrightKey != Keyboard.Key.Unknown && IsKeyJustPressed(fullBrightKey))
		{
			CerberusConfig.Eye.FullBrightEnabled = !CerberusConfig.Eye.FullBrightEnabled;
		}

		// Zoom up
		var zoomUpKey = CerberusConfig.Eye.ZoomUpHotKey;
		if (zoomUpKey != Keyboard.Key.Unknown && IsKeyJustPressed(zoomUpKey))
		{
			CerberusConfig.Eye.Zoom = Math.Min(CerberusConfig.Eye.Zoom + 0.5f, 30f);
		}

		// Zoom down
		var zoomDownKey = CerberusConfig.Eye.ZoomDownHotKey;
		if (zoomDownKey != Keyboard.Key.Unknown && IsKeyJustPressed(zoomDownKey))
		{
			CerberusConfig.Eye.Zoom = Math.Max(CerberusConfig.Eye.Zoom - 0.5f, 0.5f);
		}

		// Storage Viewer toggle
		var storageKey = CerberusConfig.StorageViewer.HotKey;
		if (storageKey != Keyboard.Key.Unknown && IsKeyJustPressed(storageKey))
		{
			CerberusConfig.StorageViewer.Enabled = !CerberusConfig.StorageViewer.Enabled;
		}

		// Update last key states
		foreach (var kvp in _keyStates)
		{
			_lastKeyStates[kvp.Key] = kvp.Value;
		}
	}

	private bool IsKeyJustPressed(Keyboard.Key key)
	{
		if (key == Keyboard.Key.Unknown)
			return false;

		var isDown = _keyStates.GetValueOrDefault(key, false);
		var wasDown = _lastKeyStates.GetValueOrDefault(key, false);
		return isDown && !wasDown;
	}

	public void ToggleMenu()
	{
		if (_mainMenuWindow == null || !_mainMenuWindow.IsOpen)
		{
			_mainMenuWindow = _uiManager.CreateWindow<MainMenuWindow>();
			_mainMenuWindow.OpenCentered();
			CerberusConfig.Settings.ShowMenu = true;
		}
		else
		{
			_mainMenuWindow.Close();
			CerberusConfig.Settings.ShowMenu = false;
		}
	}
}
