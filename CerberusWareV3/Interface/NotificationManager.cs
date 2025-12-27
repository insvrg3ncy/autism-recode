using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using CerberusWareV3.Configuration;
using Robust.Client.Graphics;
using Robust.Client.Timing;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

[CompilerGenerated]
public static class NotificationManager
{
	private static readonly List<Notification> _notifications = new();
	private static Control? _notificationContainer;
	private static bool _initialized;

	public static void Initialize(Control container)
	{
		_notificationContainer = container;
		_initialized = true;
	}

	public static void ShowNotification(string message, float duration = 5f, float fadeInTime = 0.3f, float fadeOutTime = 0.5f, Vector4? lineColor = null, bool useProgressBar = false)
	{
		bool uiCustomizable = CerberusConfig.Settings.UiCustomizable;
		if (!uiCustomizable && _initialized && _notificationContainer != null)
		{
			bool flag = CerberusConfig.Notifications.MaxNotifications > 0 && _notifications.Count >= CerberusConfig.Notifications.MaxNotifications;
			if (flag)
			{
				_notifications.RemoveAt(0);
			}
			_notifications.Add(new Notification(message, duration, fadeInTime, fadeOutTime, lineColor, useProgressBar));
		}
	}

	private static float _totalTime = 0f;

	public static void Update(FrameEventArgs args)
	{
		if (!_initialized || _notificationContainer == null || !CerberusConfig.Notifications.Enabled)
			return;

		_totalTime += (float)args.DeltaSeconds;

		_notificationContainer.RemoveAllChildren();

		var activeNotifications = new List<Notification>();

		foreach (var notification in _notifications)
		{
			notification.Update((float)args.DeltaSeconds);
			if (notification.IsActive)
			{
				activeNotifications.Add(notification);
			}
		}

		_notifications.RemoveAll(n => !n.IsActive);

		if (activeNotifications.Count == 0)
			return;

		float yPos = _notificationContainer.Size.Y - 15f;
		for (int i = activeNotifications.Count - 1; i >= 0; i--)
		{
			var notification = activeNotifications[i];
			var control = notification.CreateControl();
			control.Measure(Vector2Helpers.Infinity);
			control.Arrange(UIBox2.FromDimensions(Vector2.Zero, control.DesiredSize));
			
			float xPos = _notificationContainer.Size.X - control.Size.X - 15f;
			LayoutContainer.SetPosition(control, new Vector2(xPos, yPos - control.Size.Y));
			_notificationContainer.AddChild(control);
			yPos -= control.Size.Y + 10f;
		}
	}

	private class Notification
	{
		public string Message { get; }
		public float Duration { get; }
		public float FadeInTime { get; }
		public float FadeOutTime { get; }
		public Vector4? LineColor { get; }
		public bool UseProgressBar { get; }
		public float StartTime { get; private set; }
		public float CurrentTime { get; private set; }
		public bool IsActive { get; private set; } = true;
		public float Alpha { get; private set; }
		public float LifeProgress { get; private set; }

		public Notification(string message, float duration, float fadeInTime, float fadeOutTime, Vector4? lineColor, bool useProgressBar)
		{
			Message = message;
			Duration = Math.Max(duration, fadeInTime + fadeOutTime);
			FadeInTime = Math.Min(fadeInTime, Duration / 2f);
			FadeOutTime = Math.Min(fadeOutTime, Duration / 2f);
			LineColor = lineColor ?? new Vector4(1f, 1f, 1f, 1f);
			UseProgressBar = useProgressBar;
			StartTime = 0f;
			CurrentTime = 0f;
			Alpha = 0f;
			LifeProgress = 0f;
		}

		public void Update(float deltaTime)
		{
			CurrentTime += deltaTime;
			LifeProgress = Math.Clamp(CurrentTime / Duration, 0f, 1f);

			if (CurrentTime < FadeInTime)
			{
				Alpha = CurrentTime / FadeInTime;
			}
			else if (CurrentTime < Duration - FadeOutTime)
			{
				Alpha = 1f;
			}
			else
			{
				float fadeOutProgress = (CurrentTime - (Duration - FadeOutTime)) / FadeOutTime;
				Alpha = 1f - fadeOutProgress;
			}

			IsActive = Alpha > 0.01f;
		}

		public Control CreateControl()
		{
			var panel = new PanelContainer
			{
				MinHeight = 30,
				HorizontalExpand = false,
				VerticalExpand = false
			};

			var lineColor = LineColor ?? new Vector4(1f, 1f, 1f, 1f);
			var bgColor = new Color(23, 24, 28, Alpha);
			var borderColor = new Color(lineColor.X, lineColor.Y, lineColor.Z, lineColor.W * Alpha);

			panel.PanelOverride = new StyleBoxFlat
			{
				BackgroundColor = bgColor,
				BorderThickness = new Thickness(0, 0, 0, 2),
				BorderColor = borderColor
			};

			var container = new BoxContainer
			{
				Orientation = BoxContainer.LayoutOrientation.Vertical,
				Margin = new Thickness(10, 5)
			};

			var label = new Label
			{
				Text = Message,
				FontColorOverride = new Color(1f, 1f, 1f, Alpha)
			};
			container.AddChild(label);

			if (UseProgressBar)
			{
				var progressBar = new ProgressBar
				{
					MinHeight = 4,
					HorizontalExpand = true,
					Value = 1f - LifeProgress
				};
				progressBar.ForegroundStyleBoxOverride = new StyleBoxFlat
				{
					BackgroundColor = borderColor
				};
				container.AddChild(progressBar);
			}

			panel.AddChild(container);
			panel.Modulate = new Color(1f, 1f, 1f, Alpha);

			return panel;
		}
	}
}
