using CerberusWareV3.Configuration;
using Content.Shared.Overlays;
using Robust.Client.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

/// <summary>
/// Система для управления компонентом ShowHealthBarsComponent на основе настроек
/// </summary>
public sealed class HealthBarSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    
    private bool _lastShowHealthState = false;
    
    public override void Initialize()
    {
        base.Initialize();
        _lastShowHealthState = CerberusConfig.Hud.ShowHealth;
    }
    
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        
        // Проверяем изменение состояния
        bool currentState = CerberusConfig.Hud.ShowHealth;
        if (currentState != _lastShowHealthState)
        {
            _lastShowHealthState = currentState;
            UpdateHealthBarComponent();
        }
    }
    
    private void UpdateHealthBarComponent()
    {
        var playerEntity = _playerManager.LocalEntity;
        if (playerEntity == null)
            return;
        
        if (CerberusConfig.Hud.ShowHealth)
        {
            // Добавляем компонент, если его нет
            if (!EntityManager.HasComponent<ShowHealthBarsComponent>(playerEntity.Value))
            {
                var component = new ShowHealthBarsComponent
                {
                    NetSyncEnabled = false
                };
                EntityManager.AddComponent(playerEntity.Value, component);
            }
        }
        else
        {
            // Удаляем компонент, если он есть
            if (EntityManager.HasComponent<ShowHealthBarsComponent>(playerEntity.Value))
            {
                EntityManager.RemoveComponent<ShowHealthBarsComponent>(playerEntity.Value);
            }
        }
    }
}

