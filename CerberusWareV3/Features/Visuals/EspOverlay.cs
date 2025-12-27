using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using CerberusWareV3.Configuration;
using Content.Shared.CombatMode;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Player;


[CompilerGenerated]
public sealed class EspOverlay : Overlay
{
	public EspOverlay()
	{
		IoCManager.InjectDependencies<EspOverlay>(this);
		base.ZIndex = new int?(200);
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
			if (!CerberusConfig.Esp.Enabled)
				return;

			this.UpdateValidSessions();
			
			if (this._validSessions.Count == 0)
				return;

			Font font;
			Font font2;
			try
			{
				font = new VectorFont(this._resourceCache.GetResource<FontResource>(CerberusConfig.Esp.MainFontPath, true), CerberusConfig.Esp.MainFontSize);
				font2 = new VectorFont(this._resourceCache.GetResource<FontResource>(CerberusConfig.Esp.OtherFontPath, true), CerberusConfig.Esp.OtherFontSize);
			}
			catch
			{
				// Если не удалось загрузить шрифты, используем дефолтный
				var defaultFont = this._resourceCache.GetResource<FontResource>("/Fonts/Boxfont-round/Boxfont Round.ttf", true);
				font = new VectorFont(defaultFont, 12);
				font2 = font;
			}

			if (this._friendSystem == null)
			{
				this._friendSystem = this._systemManager.GetEntitySystem<FriendSystem>();
			}
			if (this._prioritySystem == null)
			{
				this._prioritySystem = this._systemManager.GetEntitySystem<PrioritySystem>();
			}
			if (this._contrabandDetector == null)
			{
				this._contrabandDetector = this._systemManager.GetEntitySystem<ContrabandDetector>();
			}
			if (this._entityLookup == null)
			{
				this._entityLookup = this._systemManager.GetEntitySystem<EntityLookupSystem>();
			}
			if (this._antagDetector == null)
			{
				this._antagDetector = this._systemManager.GetEntitySystem<AntagDetector>();
			}
			if (this._noSlipSystem == null)
			{
				this._noSlipSystem = this._systemManager.GetEntitySystem<NoSlipSystem>();
			}

			if (this._entityLookup == null || this._eyeManager == null || this._entityManager == null)
				return;

			foreach (ICommonSession commonSession in this._validSessions)
			{
				EntityUid? attachedEntity = commonSession.AttachedEntity;
				if (attachedEntity == null)
				{
					continue;
				}

				EntityUid valueOrDefault = attachedEntity.GetValueOrDefault();
				if (!this._entityManager.EntityExists(valueOrDefault))
				{
					continue;
				}

				TransformComponent? transformComponent;
				if (!this._entityManager.TryGetComponent<TransformComponent>(valueOrDefault, out transformComponent))
				{
					continue;
				}

				if (transformComponent.MapID != this._eyeManager.CurrentMap)
				{
					continue;
				}

				MetaDataComponent? component;
				if (!this._entityManager.TryGetComponent<MetaDataComponent>(valueOrDefault, out component))
				{
					continue;
				}

				Box2 worldAABB;
				try
				{
					worldAABB = this._entityLookup.GetWorldAABB(valueOrDefault, transformComponent);
				}
				catch
				{
					continue;
				}

				if (args.ViewportControl == null)
					continue;

				Box2 worldAABB_copy = args.WorldAABB; 
				Vector2 center = worldAABB.Center;
				
				if (!worldAABB.Intersects(ref worldAABB_copy))
				{
					continue;
				}

				Vector2 screenPos;
				try
				{
					screenPos = args.ViewportControl.WorldToScreen(center) + new Vector2(1f, 7f);
				}
				catch
				{
					continue;
				}

				Vector2 vector3 = screenPos;
				Vector2 vector4 = new Vector2(0f, (float)CerberusConfig.Esp.FontInterval);
				string name = commonSession.Name;
				ICommonSession localSession = this._playerManager.LocalSession;
				bool flag5 = name == ((localSession != null) ? localSession.Name : null);
				if (flag5)
				{
					continue;
				}
				bool showName = CerberusConfig.Esp.ShowName;
				if (showName)
				{
					args.ScreenHandle.DrawString(font, vector3, component.EntityName, (commonSession.Status == (SessionStatus)4) ? Color.White : new Color(ref CerberusConfig.Esp.NameColor));
					vector3 += vector4;
				}
				bool showCKey = CerberusConfig.Esp.ShowCKey;
				if (showCKey)
				{
					args.ScreenHandle.DrawString(font, vector3, commonSession.Name, (commonSession.Status == (SessionStatus)4) ? Color.White : new Color(ref CerberusConfig.Esp.CKeyColor));
					vector3 += vector4;
				}
				if (CerberusConfig.Esp.ShowAntag && this._antagDetector != null)
				{
					try
					{
						if (this._antagDetector.IsAgent(valueOrDefault))
						{
							args.ScreenHandle.DrawString(font, vector3, LocalizationManager.GetString("ESP_Agent"), new Color(ref CerberusConfig.Esp.AntagColor));
							vector3 += vector4;
						}
						if (this._antagDetector.IsHeretic(valueOrDefault))
						{
							args.ScreenHandle.DrawString(font, vector3, LocalizationManager.GetString("ESP_Heretic"), new Color(ref CerberusConfig.Esp.AntagColor));
							vector3 += vector4;
						}
						if (this._antagDetector.IsVampire(valueOrDefault))
						{
							args.ScreenHandle.DrawString(font, vector3, LocalizationManager.GetString("ESP_Vampire"), new Color(ref CerberusConfig.Esp.AntagColor));
							vector3 += vector4;
						}
						if (this._antagDetector.IsFleshCultist(valueOrDefault))
						{
							args.ScreenHandle.DrawString(font, vector3, LocalizationManager.GetString("ESP_FleshCult"), new Color(ref CerberusConfig.Esp.AntagColor));
							vector3 += vector4;
						}
						if (this._antagDetector.IsZeroZombie(valueOrDefault))
						{
							args.ScreenHandle.DrawString(font, vector3, LocalizationManager.GetString("ESP_ZeroZombie"), new Color(ref CerberusConfig.Esp.AntagColor));
							vector3 += vector4;
						}
						if (this._antagDetector.IsChangeling(valueOrDefault))
						{
							args.ScreenHandle.DrawString(font, vector3, LocalizationManager.GetString("ESP_Changeling"), new Color(ref CerberusConfig.Esp.AntagColor));
							vector3 += vector4;
						}
						if (this._antagDetector.IsCosmicCult(valueOrDefault))
						{
							args.ScreenHandle.DrawString(font, vector3, LocalizationManager.GetString("ESP_CosmicCult"), new Color(ref CerberusConfig.Esp.AntagColor));
							vector3 += vector4;
						}
						if (this._antagDetector.IsDevil(valueOrDefault))
						{
							args.ScreenHandle.DrawString(font, vector3, LocalizationManager.GetString("ESP_Devil"), new Color(ref CerberusConfig.Esp.AntagColor));
							vector3 += vector4;
						}
						if (this._antagDetector.IsBlob(valueOrDefault))
						{
							args.ScreenHandle.DrawString(font, vector3, LocalizationManager.GetString("ESP_Blob"), new Color(ref CerberusConfig.Esp.AntagColor));
							vector3 += vector4;
						}
						if (this._antagDetector.IsThief(valueOrDefault))
						{
							args.ScreenHandle.DrawString(font, vector3, LocalizationManager.GetString("ESP_Thief"), new Color(ref CerberusConfig.Esp.AntagColor));
							vector3 += vector4;
						}
					}
					catch { }
				}
				if (CerberusConfig.Esp.ShowFriend && this._friendSystem != null)
				{
					try
					{
						if (this._friendSystem.IsFriend(valueOrDefault))
						{
							args.ScreenHandle.DrawString(font, vector3, LocalizationManager.GetString("ESP_Friend"), new Color(ref CerberusConfig.Esp.FriendColor));
							vector3 += vector4;
						}
					}
					catch { }
				}
				if (CerberusConfig.Esp.ShowPriority && this._prioritySystem != null)
				{
					try
					{
						if (this._prioritySystem.IsPriority(valueOrDefault))
						{
							args.ScreenHandle.DrawString(font, vector3, LocalizationManager.GetString("ESP_Priority"), new Color(ref CerberusConfig.Esp.PriorityColor));
							vector3 += vector4;
						}
					}
					catch { }
				}
				if (CerberusConfig.Esp.ShowCombatMode)
				{
					CombatModeComponent combatModeComponent;
					if (this._entityManager.TryGetComponent<CombatModeComponent>(valueOrDefault, out combatModeComponent))
					{
						try
						{
							if (combatModeComponent.IsInCombatMode)
							{
								args.ScreenHandle.DrawString(font, vector3, LocalizationManager.GetString("ESP_CombatMode"), new Color(ref CerberusConfig.Esp.CombatModeColor));
								vector3 += vector4;
							}
						}
						catch { }
					}
				}
				if (CerberusConfig.Esp.ShowContraband && this._contrabandDetector != null)
				{
					try
					{
						if (this._contrabandDetector.HasContraband(valueOrDefault))
						{
							args.ScreenHandle.DrawString(font2, vector3, LocalizationManager.GetString("ESP_Contraband"), new Color(ref CerberusConfig.Esp.ContrabandColor));
							vector3 += vector4;
						}
					}
					catch { }
				}
				if (CerberusConfig.Esp.ShowImplants && this._contrabandDetector != null)
				{
					try
					{
						if (this._contrabandDetector.HasImplants(valueOrDefault))
						{
							args.ScreenHandle.DrawString(font2, vector3, LocalizationManager.GetString("ESP_Implants"), new Color(ref CerberusConfig.Esp.ImplantsColor));
							vector3 += vector4;
						}
					}
					catch { }
				}
				if (CerberusConfig.Esp.ShowWeapon && this._contrabandDetector != null)
				{
					try
					{
						if (this._contrabandDetector.HasWeapons(valueOrDefault))
						{
							args.ScreenHandle.DrawString(font2, vector3, LocalizationManager.GetString("ESP_Weapon"), new Color(ref CerberusConfig.Esp.WeaponColor));
							vector3 += vector4;
						}
					}
					catch { }
				}
				if (CerberusConfig.Esp.ShowNoSlip && this._noSlipSystem != null)
				{
					try
					{
						if (!this._noSlipSystem.CanSlip(valueOrDefault))
						{
							args.ScreenHandle.DrawString(font2, vector3, LocalizationManager.GetString("ESP_NoSlip"), new Color(ref CerberusConfig.Esp.NoSlipColor));
							vector3 += vector4;
						}
					}
					catch { }
				}
				continue;
			}
		}
		catch (System.TypeLoadException)
		{
			// Типы не могут быть загружены, пропускаем отрисовку
		}
		catch (System.NullReferenceException)
		{
			// Некоторые компоненты могут быть null, пропускаем отрисовку
		}
		catch (System.Exception)
		{
			// Игнорируем другие ошибки при отрисовке
		}
	}
	private void UpdateValidSessions()
	{
		List<ICommonSession> list = this._playerManager.Sessions.ToList<ICommonSession>();
		foreach (ICommonSession commonSession in this._validSessions.ToList<ICommonSession>())
		{
			bool flag = !list.Contains(commonSession);
			if (flag)
			{
				// Удаляем сессию из списка, если её больше нет
				this._validSessions.Remove(commonSession);
			}
		}
		foreach (ICommonSession commonSession2 in list)
		{
			bool flag2 = !this._validSessions.Contains(commonSession2);
			if (flag2)
			{
				this._validSessions.Add(commonSession2);
			}
		}
	}
	
	[Robust.Shared.IoC.Dependency] private readonly IEntityManager _entityManager = null;
	
	[Robust.Shared.IoC.Dependency] private readonly IEyeManager _eyeManager = null;
	
	[Robust.Shared.IoC.Dependency] private readonly IPlayerManager _playerManager = null;
	
	[Robust.Shared.IoC.Dependency] private readonly IResourceCache _resourceCache = null;
	
	[Robust.Shared.IoC.Dependency] private readonly IEntitySystemManager _systemManager = null;
	
	private FriendSystem _friendSystem;
	
	private PrioritySystem _prioritySystem;
	
	private ContrabandDetector _contrabandDetector;
	
	private EntityLookupSystem _entityLookup;
	
	private AntagDetector _antagDetector;
	
	private NoSlipSystem _noSlipSystem;
	private readonly List<ICommonSession> _validSessions = new List<ICommonSession>();
}
