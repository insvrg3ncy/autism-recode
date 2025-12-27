using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Robust.Shared.ContentPack;
[CompilerGenerated]
public class EntryPoint : GameClient
{
	public override async void PreInit()
	{
		Patcher.PatchAll();
	}
	public override void Init()
	{
		// RenderManager и IntroOverlay больше не используются - все переведено на RobustUI
		// NotificationManager теперь использует RobustUI и регистрируется через UiRegistrationSystem
	}
	public override void Shutdown()
	{
		this._cancellationTokenSource.Cancel();
		// RenderManager больше не используется
	}
	
	private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
}
