using System;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using CerberusWareV3.Configuration;
using Content.Shared.Explosion.Components;
// using Content.Shared.Trigger.Components; // Убрано для предотвращения TypeLoadException
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Containers;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Maths;


public class PlaceholderOverlay : Overlay
{
	public PlaceholderOverlay()
	{
		IoCManager.InjectDependencies<PlaceholderOverlay>(this);
		if (this._font == null)
		{
			this._font = new VectorFont(this._resourceCache.GetResource<FontResource>("/Fonts/Boxfont-round/Boxfont Round.ttf", true), 12);
		}
	}
	public override OverlaySpace Space
	{
		get
		{
			return (OverlaySpace)2;
		}
	}
	protected override void Draw(in OverlayDrawArgs args)
	{
		try
		{
			bool flag = !CerberusConfig.Misc.ShowExplosive;
			if (!flag)
			{
				// Проверяем наличие типа через рефлексию перед использованием
				Type activeTimerTriggerType = Type.GetType("Content.Shared.Trigger.Components.ActiveTimerTriggerComponent, Content.Shared");
				if (activeTimerTriggerType == null)
				{
					// Тип не найден, пропускаем отрисовку
					return;
				}

				if (this._transformSystem == null)
				{
					this._transformSystem = this._entityManager.System<SharedTransformSystem>();
				}
				if (this._containerSystem == null)
				{
					this._containerSystem = this._entityManager.System<SharedContainerSystem>();
				}

				// Используем не-generic метод для безопасной загрузки
				var entityQueryEnumerator = this._entityManager.AllEntityQueryEnumerator(activeTimerTriggerType);
				for (;;)
				{
					bool flag2 = entityQueryEnumerator.MoveNext(out EntityUid entityUid, out IComponent component);
					if (!flag2)
					{
						break;
					}
					Vector2 vector = this._eyeManager.WorldToScreen(this._transformSystem.GetWorldPosition(entityUid));
					Angle worldRotation = this._transformSystem.GetWorldRotation(entityUid);
					SpriteComponent spriteComponent;
					bool flag3 = !this._entityManager.TryGetComponent<SpriteComponent>(entityUid, out spriteComponent) || spriteComponent.Icon == null;
					if (flag3)
					{
						break;
					}
					args.ScreenHandle.DrawString(this._font, vector + new Vector2(-35f, 20f), "Danger", Color.Red);
					bool flag4 = !this._containerSystem.IsEntityInContainer(entityUid, null);
					if (flag4)
					{
						break;
					}
					args.ScreenHandle.DrawEntity(entityUid, vector, new Vector2(3f), new Angle?(worldRotation.GetDir().ToAngle()), this._eyeManager.CurrentEye.Rotation, null, null, null, null);
				}
			}
		}
		catch (System.TypeLoadException)
		{
			// Тип ActiveTimerTriggerComponent не может быть загружен, пропускаем отрисовку
		}
		catch (System.Exception)
		{
			// Игнорируем другие ошибки при отрисовке
		}
	}
	
	[Robust.Shared.IoC.Dependency] private readonly IEntityManager _entityManager = null;
	
	[Robust.Shared.IoC.Dependency] private readonly IEyeManager _eyeManager = null;
	
	[Robust.Shared.IoC.Dependency] private readonly IResourceCache _resourceCache = null;
	private readonly Font _font;
	
	private SharedTransformSystem _transformSystem;
	
	private SharedContainerSystem _containerSystem;
}
