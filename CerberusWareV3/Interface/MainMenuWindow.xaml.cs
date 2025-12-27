using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using CerberusWareV3.Configuration;
using CerberusWareV3.Interface.Controls;
using CerberusWareV3.Localization;
using Robust.Shared.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Timing;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace CerberusWareV3.Interface;

public sealed class MainMenuWindow : DefaultWindow
{
    [Dependency] private readonly IEntitySystemManager _sysMan = default!;
    private IUserInterfaceManager? _uiManager;
    
    private int _currentTab = 0;
    private int _currentSubTab = 0;
    private readonly List<TabInfo> _tabs = new();
    private readonly Dictionary<int, float> _tabAlpha = new();
    private readonly float _tabSwitchSpeed = 5f;
    
    private readonly ComponentManager _componentManager = new();
    private PlayerTrackerSystem? _playerTracker;
    private EntityPreviewSystem? _entityPreview;
    private SpammerSystem? _spammerSystem;
    
    private readonly Dictionary<string, ConnectStatus> _logs = new();
    private readonly Dictionary<NetUserId, PlayerInfoWindow> _playerWindows = new();
    private readonly Dictionary<string, SettingsPopupWindow> _settingsPopups = new();
    private readonly HashSet<NetUserId> _renderedPreviews = new();
    
    private Control _notificationContainer = null!;
    private BoxContainer _mainTabsContainer = null!;
    private BoxContainer _subTabsContainer = null!;
    private BoxContainer _contentContainer = null!;

    private string _playerSearch = "";
    private int _selectedSort = 0;
    private readonly string[] _sortOptions = { "Online", "Offline", "Name Ascending", "Name Desc", "Char Ascending", "Char Descending" };
    
    private static readonly Color ChildBgColor = new Color(100, 129, 246, 255);
    private static readonly Color ChildBgColor2 = new Color(23, 24, 28, 255);
    private static readonly Color WindowBgColor = new Color(18, 19, 23, 255);
    private static readonly Color ButtonColor = new Color(35, 36, 40, 255);
    private static readonly Color ButtonHoverColor = new Color(35, 36, 40, 255);
    private static readonly Color ButtonActiveColor = new Color(23, 24, 28, 255);
    private static readonly Color TextColor = new Color(207, 207, 209, 255);

    public MainMenuWindow()
    {
        // Create window programmatically instead of using XAML
        Title = "CerberusWare V3";
        MinSize = new Vector2(880, 570);
        SetSize = new Vector2(880, 570);
        Resizable = false;
        MouseFilter = Control.MouseFilterMode.Stop;
        
        IoCManager.InjectDependencies(this);
        
        // Create UI programmatically
        CreateUI();
        
        _playerTracker = _sysMan.GetEntitySystem<PlayerTrackerSystem>();
        _entityPreview = _sysMan.GetEntitySystem<EntityPreviewSystem>();
        _spammerSystem = _sysMan.GetEntitySystem<SpammerSystem>();
        
        if (_playerTracker != null)
        {
            _playerTracker.OnPlayerJoined += OnPlayerJoined;
            _playerTracker.OnPlayerLeft += OnPlayerLeft;
        }
        
        _uiManager = IoCManager.Resolve<IUserInterfaceManager>();
        
        InitializeTabs();
        SetupEventHandlers();
        UpdateContent();
        
        // Initialize notification manager
        NotificationManager.Initialize(_notificationContainer);
        
        // Initially hide if config says so
        if (!CerberusConfig.Settings.ShowMenu)
        {
            Visible = false;
        }
    }

    private void OnPlayerJoined(ICommonSession session)
    {
        var message = LocalizationManager.GetString("Players_Logs_Join", new object[] { session.Name });
        var timestamp = $"[{DateTime.Now:HH:mm:ss}] {message}";
        _logs[timestamp] = ConnectStatus.Join;
        
        if (_currentTab == 2 && _currentSubTab == 1)
        {
            RenderLogsTab();
        }
    }

    private void OnPlayerLeft(ICommonSession session)
    {
        var message = LocalizationManager.GetString("Players_Logs_Leave", new object[] { session.Name });
        var timestamp = $"[{DateTime.Now:HH:mm:ss}] {message}";
        _logs[timestamp] = ConnectStatus.Leave;
        
        if (_playerWindows.TryGetValue(session.UserId, out var window))
        {
            window.Close();
            _playerWindows.Remove(session.UserId);
        }
        _renderedPreviews.Remove(session.UserId);
        
        if (_currentTab == 2 && _currentSubTab == 1)
        {
            RenderLogsTab();
        }
    }

    private void OpenPlayerWindow(PlayerData playerData)
    {
        if (_uiManager == null)
            return;

        if (_playerWindows.TryGetValue(playerData.Session.UserId, out var existingWindow))
        {
            if (existingWindow.IsOpen)
            {
                existingWindow.MoveToFront();
                return;
            }
        }

        var window = _uiManager.CreateWindow<PlayerInfoWindow>();
        window.SetPlayerData(playerData);
        window.OpenCentered();
        _playerWindows[playerData.Session.UserId] = window;
        
        window.OnClose += () =>
        {
            _playerWindows.Remove(playerData.Session.UserId);
        };
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        
        // Handle visibility based on config
        Visible = CerberusConfig.Settings.ShowMenu && IsOpen;
        
        // Update notifications
        NotificationManager.Update(args);
        
        // Update settings popups
        UpdateSettingsPopups();
    }

    private void UpdateSettingsPopups()
    {
        // Close popups that are no longer needed
        var popupsToClose = _settingsPopups.Where(kvp => !kvp.Value.IsOpen).ToList();
        foreach (var kvp in popupsToClose)
        {
            _settingsPopups.Remove(kvp.Key);
        }
    }

    private void OpenSettingsPopup(string uniqueId, Action<string> renderContent)
    {
        if (_uiManager == null)
            return;

        if (_settingsPopups.TryGetValue(uniqueId, out var existingPopup))
        {
            if (existingPopup.IsOpen)
            {
                existingPopup.MoveToFront();
                existingPopup.UpdateContent();
                return;
            }
        }

        var popup = _uiManager.CreateWindow<SettingsPopupWindow>();
        popup.UniqueId = uniqueId;
        popup.RenderContent = renderContent;
        popup.UpdateContent();
        popup.OpenCentered();
        _settingsPopups[uniqueId] = popup;

        popup.OnClose += () =>
        {
            _settingsPopups.Remove(uniqueId);
        };
    }

    private void CreateUI()
    {
        var mainPanel = new PanelContainer
        {
            MouseFilter = Control.MouseFilterMode.Pass
        };
        mainPanel.PanelOverride = new StyleBoxFlat { BackgroundColor = new Color(18, 11, 30) };
        
        var mainContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Margin = new Thickness(0),
            MouseFilter = Control.MouseFilterMode.Pass
        };
        
        // Left sidebar
        var leftSidebar = new PanelContainer
        {
            MinWidth = 180,
            MaxWidth = 180,
            MouseFilter = Control.MouseFilterMode.Pass
        };
        leftSidebar.PanelOverride = new StyleBoxFlat { BackgroundColor = new Color(26, 15, 26) };
        
        _mainTabsContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(5),
            MouseFilter = Control.MouseFilterMode.Pass
        };
        
        // Logo area
        var logoPanel = new PanelContainer
        {
            Margin = new Thickness(0, 0, 0, 10),
            MinHeight = 70
        };
        logoPanel.PanelOverride = new StyleBoxFlat { BackgroundColor = new Color(57, 42, 100) };
        var logoLabel = new Label
        {
            Text = "t.me/RobusterHome",
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            FontColorOverride = Color.White
        };
        logoPanel.AddChild(logoLabel);
        _mainTabsContainer.AddChild(logoPanel);
        
        // Tab buttons will be added in SetupEventHandlers
        _mainTabsContainer.AddChild(new Control { VerticalExpand = true });
        
        // Footer
        var versionLabel = new Label
        {
            Name = "VersionLabel",
            Text = "Version",
            HorizontalAlignment = HAlignment.Center,
            FontColorOverride = new Color(207, 207, 209),
            Margin = new Thickness(0, 0, 0, 5)
        };
        _mainTabsContainer.AddChild(versionLabel);
        
        var antiCheatLabel = new Label
        {
            Name = "AntiCheatLabel",
            Text = "",
            HorizontalAlignment = HAlignment.Center,
            FontColorOverride = Color.Red,
            Visible = false
        };
        _mainTabsContainer.AddChild(antiCheatLabel);
        
        leftSidebar.AddChild(_mainTabsContainer);
        mainContainer.AddChild(leftSidebar);
        
        // Main content area
        var contentArea = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            MouseFilter = Control.MouseFilterMode.Pass
        };
        
        // Sub-tabs header
        var subTabsHeader = new PanelContainer
        {
            MinHeight = 70,
            MaxHeight = 70,
            MouseFilter = Control.MouseFilterMode.Pass
        };
        subTabsHeader.PanelOverride = new StyleBoxFlat { BackgroundColor = new Color(23, 17, 23) };
        
        _subTabsContainer = new BoxContainer
        {
            Name = "SubTabsContainer",
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Margin = new Thickness(5),
            VerticalAlignment = VAlignment.Center,
            MouseFilter = Control.MouseFilterMode.Pass
        };
        subTabsHeader.AddChild(_subTabsContainer);
        contentArea.AddChild(subTabsHeader);
        
        // Content area
        var contentPanel = new PanelContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            Margin = new Thickness(10),
            MouseFilter = Control.MouseFilterMode.Pass
        };
        contentPanel.PanelOverride = new StyleBoxFlat { BackgroundColor = new Color(23, 17, 23) };
        
        var scrollContainer = new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            HScrollEnabled = false,
            MouseFilter = Control.MouseFilterMode.Pass
        };
        
        _contentContainer = new BoxContainer
        {
            Name = "ContentContainer",
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(10),
            MouseFilter = Control.MouseFilterMode.Pass
        };
        scrollContainer.AddChild(_contentContainer);
        contentPanel.AddChild(scrollContainer);
        contentArea.AddChild(contentPanel);
        
        mainContainer.AddChild(contentArea);
        mainPanel.AddChild(mainContainer);
        
        // Notification container - use LayoutContainer to position it on top without blocking interactions
        var notificationLayout = new LayoutContainer
        {
            MouseFilter = Control.MouseFilterMode.Ignore,
            HorizontalExpand = true,
            VerticalExpand = true
        };
        
        _notificationContainer = new Control
        {
            Name = "NotificationContainer",
            MouseFilter = Control.MouseFilterMode.Ignore,
            HorizontalExpand = true,
            VerticalExpand = true
        };
        notificationLayout.AddChild(_notificationContainer);
        LayoutContainer.SetAnchorAndMarginPreset(_notificationContainer, LayoutContainer.LayoutPreset.Wide);
        
        mainPanel.AddChild(notificationLayout);
        
        AddChild(mainPanel);
    }
    
    private void InitializeTabs()
    {
        _tabs.Add(new TabInfo("AimBot", new List<string> 
        { 
            LocalizationManager.GetString("Main_Gun"),
            LocalizationManager.GetString("Main_Melee")
        }, () => RenderAimBotTab()));
        
        _tabs.Add(new TabInfo("Visuals", new List<string>
        {
            LocalizationManager.GetString("Main_Esp"),
            LocalizationManager.GetString("Main_Eye"),
            LocalizationManager.GetString("Main_Fun")
        }, () => RenderVisualsTab()));
        
        _tabs.Add(new TabInfo("Players", new List<string>
        {
            LocalizationManager.GetString("Main_Players"),
            LocalizationManager.GetString("Main_Logs")
        }, () => RenderPlayersTab()));
        
        _tabs.Add(new TabInfo("Misc", new List<string>
        {
            LocalizationManager.GetString("Main_Misc"),
            LocalizationManager.GetString("Main_Spammer")
        }, () => RenderMiscTab()));
        
        _tabs.Add(new TabInfo("Settings", new List<string>
        {
            LocalizationManager.GetString("Main_Settings"),
            LocalizationManager.GetString("Main_Configs")
        }, () => RenderSettingsTab()));
    }

    private void SetupEventHandlers()
    {
        // Create tab buttons
        var aimBotTabButton = new Button { Text = "AimBot", Margin = new Thickness(0, 2) };
        var visualsTabButton = new Button { Text = "Visuals", Margin = new Thickness(0, 2) };
        var playersTabButton = new Button { Text = "Players", Margin = new Thickness(0, 2) };
        var miscTabButton = new Button { Text = "Misc", Margin = new Thickness(0, 2) };
        var settingsTabButton = new Button { Text = "Settings", Margin = new Thickness(0, 2) };
        
        aimBotTabButton.OnPressed += _ => SwitchTab(0);
        visualsTabButton.OnPressed += _ => SwitchTab(1);
        playersTabButton.OnPressed += _ => SwitchTab(2);
        miscTabButton.OnPressed += _ => SwitchTab(3);
        settingsTabButton.OnPressed += _ => SwitchTab(4);
        
        _mainTabsContainer.AddChild(aimBotTabButton);
        _mainTabsContainer.AddChild(visualsTabButton);
        _mainTabsContainer.AddChild(playersTabButton);
        _mainTabsContainer.AddChild(miscTabButton);
        _mainTabsContainer.AddChild(settingsTabButton);
        
        // Update version and anti-cheat labels
        Label? versionLabel = null;
        Label? antiCheatLabel = null;
        
        foreach (var child in _mainTabsContainer.Children)
        {
            if (child is Label label)
            {
                if (label.Name == "VersionLabel")
                    versionLabel = label;
                if (label.Name == "AntiCheatLabel")
                    antiCheatLabel = label;
            }
        }
        
        if (versionLabel != null)
        {
            versionLabel.Text = CerberusConfig.NoSavedConfig.Version;
        }
        
        if (CerberusConfig.NoSavedConfig.HasAntiCheat && antiCheatLabel != null)
        {
            antiCheatLabel.Text = "AntiCheat";
            antiCheatLabel.Visible = true;
        }
    }

    private void SwitchTab(int tabIndex)
    {
        _currentTab = tabIndex;
        _currentSubTab = 0;
        UpdateContent();
    }

    private void UpdateContent()
    {
        UpdateSubTabs();
        RenderCurrentTab();
    }

    private void UpdateSubTabs()
    {
        if (_subTabsContainer == null)
            return;
            
        _subTabsContainer.RemoveAllChildren();
        
        var currentTab = _tabs[_currentTab];
        if (currentTab.SubTabs == null || currentTab.SubTabs.Count == 0)
            return;
        
        float buttonWidth = 700f / currentTab.SubTabs.Count;
        
        for (int i = 0; i < currentTab.SubTabs.Count; i++)
        {
            var button = new Button
            {
                Text = currentTab.SubTabs[i],
                MinWidth = buttonWidth,
                MinHeight = 70
            };
            
            int subTabIndex = i;
            bool isSelected = _currentSubTab == i;
            
            button.StyleBoxOverride = new StyleBoxFlat
            {
                BackgroundColor = isSelected ? ButtonActiveColor : ButtonColor,
                BorderColor = isSelected ? Color.White : TextColor,
                BorderThickness = new Thickness(0, 0, 0, isSelected ? 2 : 0)
            };
            
            button.OnPressed += _ =>
            {
                _currentSubTab = subTabIndex;
                UpdateContent();
            };
            
            _subTabsContainer.AddChild(button);
        }
    }

    private void RenderCurrentTab()
    {
        if (_contentContainer != null)
            _contentContainer.RemoveAllChildren();
        
        var currentTab = _tabs[_currentTab];
        currentTab.RenderAction?.Invoke();
    }

    private void RenderAimBotTab()
    {
        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            VerticalExpand = true
        };
        
        var leftPanel = CreatePanel(340f, 480f);
        var rightPanel = CreatePanel(340f, 480f);
        
        if (_currentSubTab == 0)
        {
            var rightTop = CreatePanel(340f, 235f);
            var rightBottom = CreatePanel(340f, 235f);
            var rightContainer = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                HorizontalExpand = true,
                VerticalExpand = true
            };
            RenderGunLeft(leftPanel);
            RenderGunRightTop(rightTop);
            RenderGunRightDown(rightBottom);
            rightContainer.AddChild(rightTop);
            rightContainer.AddChild(rightBottom);
            container.AddChild(leftPanel);
            container.AddChild(rightContainer);
        }
        else if (_currentSubTab == 1)
        {
            var rightTop = CreatePanel(340f, 235f);
            var rightBottom = CreatePanel(340f, 235f);
            var rightContainer = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                HorizontalExpand = true,
                VerticalExpand = true
            };
            RenderMeleeLeft(leftPanel);
            RenderMeleeRightTop(rightTop);
            RenderMeleeRightDown(rightBottom);
            rightContainer.AddChild(rightTop);
            rightContainer.AddChild(rightBottom);
            container.AddChild(leftPanel);
            container.AddChild(rightContainer);
        }
        
        if (_contentContainer != null)
            _contentContainer.AddChild(container);
    }

    private void RenderVisualsTab()
    {
        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            VerticalExpand = true
        };
        
        var leftPanel = CreatePanel(340f, 480f);
        var rightPanel = CreatePanel(340f, 480f);
        
        if (_currentSubTab == 0)
        {
            var rightTop = CreatePanel(340f, 235f);
            var rightBottom = CreatePanel(340f, 235f);
            var rightContainer = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                HorizontalExpand = true,
                VerticalExpand = true
            };
            RenderEspLeft(leftPanel);
            RenderEspRightTop(rightTop);
            RenderEspRightDown(rightBottom);
            rightContainer.AddChild(rightTop);
            rightContainer.AddChild(rightBottom);
            container.AddChild(leftPanel);
            container.AddChild(rightContainer);
        }
        else if (_currentSubTab == 1)
        {
            var rightTop = CreatePanel(340f, 235f);
            var rightBottom = CreatePanel(340f, 235f);
            var rightContainer = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                HorizontalExpand = true,
                VerticalExpand = true
            };
            RenderEyeLeft(leftPanel);
            RenderEyeRightTop(rightTop);
            RenderEyeRightDown(rightBottom);
            rightContainer.AddChild(rightTop);
            rightContainer.AddChild(rightBottom);
            container.AddChild(leftPanel);
            container.AddChild(rightContainer);
        }
        else if (_currentSubTab == 2)
        {
            var rightTop = CreatePanel(340f, 235f);
            var rightBottom = CreatePanel(340f, 235f);
            var rightContainer = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                HorizontalExpand = true,
                VerticalExpand = true
            };
            RenderFunLeft(leftPanel);
            RenderFunRightTop(rightTop);
            RenderFunRightDown(rightBottom);
            rightContainer.AddChild(rightTop);
            rightContainer.AddChild(rightBottom);
            container.AddChild(leftPanel);
            container.AddChild(rightContainer);
        }
        
        if (_contentContainer != null)
            _contentContainer.AddChild(container);
    }

    private void RenderPlayersTab()
    {
        if (_currentSubTab == 0)
        {
            RenderGeneralTab();
        }
        else if (_currentSubTab == 1)
        {
            RenderLogsTab();
        }
    }

    private void RenderMiscTab()
    {
        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            VerticalExpand = true
        };
        
        var leftPanel = CreatePanel(340f, 480f);
        var rightPanel = CreatePanel(340f, 480f);
        
        if (_currentSubTab == 0)
        {
            var rightTop = CreatePanel(340f, 235f);
            var rightBottom = CreatePanel(340f, 235f);
            var rightContainer = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                HorizontalExpand = true,
                VerticalExpand = true
            };
            RenderMiscGeneralLeft(leftPanel);
            RenderMiscGeneralRightTop(rightTop);
            RenderMiscGeneralRightDown(rightBottom);
            rightContainer.AddChild(rightTop);
            rightContainer.AddChild(rightBottom);
            container.AddChild(leftPanel);
            container.AddChild(rightContainer);
        }
        else if (_currentSubTab == 1)
        {
            var rightTop = CreatePanel(340f, 235f);
            var rightBottom = CreatePanel(340f, 235f);
            var rightContainer = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                HorizontalExpand = true,
                VerticalExpand = true
            };
            RenderMiscSpammerLeft(leftPanel);
            RenderMiscSpammerRightTop(rightTop);
            RenderMiscSpammerRightDown(rightBottom);
            rightContainer.AddChild(rightTop);
            rightContainer.AddChild(rightBottom);
            container.AddChild(leftPanel);
            container.AddChild(rightContainer);
        }
        
        if (_contentContainer != null)
            _contentContainer.AddChild(container);
    }

    private void RenderSettingsTab()
    {
        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            VerticalExpand = true
        };
        
        var leftPanel = CreatePanel(340f, 480f);
        var rightPanel = CreatePanel(340f, 480f);
        
        if (_currentSubTab == 0)
        {
            var rightTop = CreatePanel(340f, 270f);
            var rightContainer = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                HorizontalExpand = true,
                VerticalExpand = true
            };
            RenderSettingsGeneralLeft(leftPanel);
            RenderSettingsGeneralRight(rightTop);
            rightContainer.AddChild(rightTop);
            container.AddChild(leftPanel);
            container.AddChild(rightContainer);
        }
        else if (_currentSubTab == 1)
        {
            RenderConfigsTab(leftPanel);
            container.AddChild(leftPanel);
        }
        
        if (_contentContainer != null)
            _contentContainer.AddChild(container);
    }

    private PanelContainer CreatePanel(float width, float height)
    {
        var panel = new PanelContainer
        {
            MinWidth = width,
            MinHeight = height
        };
        
        panel.PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = ChildBgColor2
        };
        
        return panel;
    }

    private void RenderGunLeft(Control parent)
    {
        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(10)
        };

        var title = new Label
        {
            Text = LocalizationManager.GetString("AimBot_Gun_General"),
            FontColorOverride = Color.White,
            Margin = new Thickness(0, 0, 0, 10)
        };
        container.AddChild(title);

        var enabledToggle = new ToggleControl(LocalizationManager.GetString("AimBot_Gun_Enabled"));
        enabledToggle.Value = CerberusConfig.GunAimBot.Enabled;
        enabledToggle.ValueChanged += v => CerberusConfig.GunAimBot.Enabled = v;
        container.AddChild(enabledToggle);

        var gunHotkey = new KeyBindInputControl(LocalizationManager.GetString("AimBot_Gun_HotKey"));
        gunHotkey.KeyBind = CerberusConfig.GunAimBot.HotKey;
        gunHotkey.KeyBindChanged += v => CerberusConfig.GunAimBot.HotKey = v;
        container.AddChild(gunHotkey);

        var radiusSlider = new SliderControl(LocalizationManager.GetString("AimBot_Gun_Radius"), 0f, 10f, CerberusConfig.GunAimBot.CircleRadius);
        radiusSlider.ValueChanged += v => CerberusConfig.GunAimBot.CircleRadius = v;
        container.AddChild(radiusSlider);

        var priorityCombo = new ComboControl(LocalizationManager.GetString("AimBot_Gun_Priority"), GetTargetPriorityNames());
        priorityCombo.SelectedIndex = CerberusConfig.GunAimBot.TargetPriority;
        priorityCombo.SelectedIndexChanged += i => CerberusConfig.GunAimBot.TargetPriority = i;
        container.AddChild(priorityCombo);

        var onlyPriorityToggle = new ToggleControl(LocalizationManager.GetString("AimBot_OnlyPriority"));
        onlyPriorityToggle.Value = CerberusConfig.GunAimBot.OnlyPriority;
        onlyPriorityToggle.ValueChanged += v => CerberusConfig.GunAimBot.OnlyPriority = v;
        container.AddChild(onlyPriorityToggle);

        var criticalToggle = new ToggleControl(LocalizationManager.GetString("AimBot_Gun_Critical"));
        criticalToggle.Value = CerberusConfig.GunAimBot.TargetCritical;
        criticalToggle.ValueChanged += v => CerberusConfig.GunAimBot.TargetCritical = v;
        container.AddChild(criticalToggle);

        var minSpreadToggle = new ToggleControl(LocalizationManager.GetString("AimBot_Gun_MinimalSpread"));
        minSpreadToggle.Value = CerberusConfig.GunAimBot.MinSpread;
        minSpreadToggle.ValueChanged += v => CerberusConfig.GunAimBot.MinSpread = v;
        container.AddChild(minSpreadToggle);

        var hitScanToggle = new ToggleControl(LocalizationManager.GetString("AimBot_Gun_HitScan"));
        hitScanToggle.Value = CerberusConfig.GunAimBot.HitScan;
        hitScanToggle.ValueChanged += v => CerberusConfig.GunAimBot.HitScan = v;
        container.AddChild(hitScanToggle);

        var autoPredictToggle = new ToggleControl(LocalizationManager.GetString("AimBot_Gun_AutoPredict"));
        autoPredictToggle.Value = CerberusConfig.GunAimBot.AutoPredict;
        autoPredictToggle.ValueChanged += v => CerberusConfig.GunAimBot.AutoPredict = v;
        container.AddChild(autoPredictToggle);

        var predictToggle = new ToggleControl(LocalizationManager.GetString("AimBot_Gun_Predict"));
        predictToggle.Value = CerberusConfig.GunAimBot.PredictEnabled;
        predictToggle.ValueChanged += v => CerberusConfig.GunAimBot.PredictEnabled = v;
        container.AddChild(predictToggle);

        var predictCorrectionSlider = new SliderControl(LocalizationManager.GetString("AimBot_Gun_PredictCorrection"), 0f, 1000f, CerberusConfig.GunAimBot.PredictCorrection);
        predictCorrectionSlider.ValueChanged += v => CerberusConfig.GunAimBot.PredictCorrection = v;
        container.AddChild(predictCorrectionSlider);

        parent.AddChild(container);
    }

    private void RenderGunRightTop(Control parent)
    {
        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(10)
        };

        var title = new Label
        {
            Text = LocalizationManager.GetString("AimBot_Gun_Visual"),
            FontColorOverride = Color.White,
            Margin = new Thickness(0, 0, 0, 10)
        };
        container.AddChild(title);

        var circleToggle = new ToggleControl(LocalizationManager.GetString("AimBot_Gun_Circle"));
        circleToggle.Value = CerberusConfig.GunAimBot.ShowCircle;
        circleToggle.ValueChanged += v => CerberusConfig.GunAimBot.ShowCircle = v;
        container.AddChild(circleToggle);

        var lineToggle = new ToggleControl(LocalizationManager.GetString("AimBot_Gun_Line"));
        lineToggle.Value = CerberusConfig.GunAimBot.ShowLine;
        lineToggle.ValueChanged += v => CerberusConfig.GunAimBot.ShowLine = v;
        container.AddChild(lineToggle);

        var colorPicker = new ColorPickerControl(LocalizationManager.GetString("AimBot_Gun_Color"));
        colorPicker.Color = CerberusConfig.GunAimBot.Color;
        colorPicker.ColorChanged += v => CerberusConfig.GunAimBot.Color = v;
        container.AddChild(colorPicker);

        parent.AddChild(container);
    }

    private void RenderGunRightDown(Control parent)
    {
        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(10)
        };

        var title = new Label
        {
            Text = LocalizationManager.GetString("AimBot_Gun_Helpers"),
            FontColorOverride = Color.White,
            Margin = new Thickness(0, 0, 0, 10)
        };
        container.AddChild(title);

        var enabledHelperToggle = new ToggleControl(LocalizationManager.GetString("AimBot_Gun_EnabledHelper"));
        enabledHelperToggle.Value = CerberusConfig.GunHelper.Enabled;
        enabledHelperToggle.ValueChanged += v => CerberusConfig.GunHelper.Enabled = v;
        container.AddChild(enabledHelperToggle);

        var showAmmoToggle = new ToggleControl(LocalizationManager.GetString("AimBot_Gun_ShowAmmo"));
        showAmmoToggle.Value = CerberusConfig.GunHelper.ShowAmmo;
        showAmmoToggle.ValueChanged += v => CerberusConfig.GunHelper.ShowAmmo = v;
        container.AddChild(showAmmoToggle);

        var autoBoltToggle = new ToggleControl(LocalizationManager.GetString("AimBot_Gun_AutoBolt"));
        autoBoltToggle.Value = CerberusConfig.GunHelper.AutoBolt;
        autoBoltToggle.ValueChanged += v => CerberusConfig.GunHelper.AutoBolt = v;
        container.AddChild(autoBoltToggle);

        var autoReloadToggle = new ToggleControl(LocalizationManager.GetString("AimBot_Gun_AutoReload"));
        autoReloadToggle.Value = CerberusConfig.GunHelper.AutoReload;
        autoReloadToggle.ValueChanged += v => CerberusConfig.GunHelper.AutoReload = v;
        container.AddChild(autoReloadToggle);

        var autoReloadDelaySlider = new SliderControl(LocalizationManager.GetString("AimBot_Gun_AutoReloadDelay"), 0.01f, 0.5f, CerberusConfig.GunHelper.AutoReloadDelay);
        autoReloadDelaySlider.ValueChanged += v => CerberusConfig.GunHelper.AutoReloadDelay = v;
        container.AddChild(autoReloadDelaySlider);

        parent.AddChild(container);
    }

    private void RenderMeleeLeft(Control parent)
    {
        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(10)
        };

        var title = new Label
        {
            Text = LocalizationManager.GetString("AimBot_Melee_General"),
            FontColorOverride = Color.White,
            Margin = new Thickness(0, 0, 0, 10)
        };
        container.AddChild(title);

        var enabledToggle = new ToggleControl(LocalizationManager.GetString("AimBot_Melee_Enabled"));
        enabledToggle.Value = CerberusConfig.MeleeAimBot.Enabled;
        enabledToggle.ValueChanged += v => CerberusConfig.MeleeAimBot.Enabled = v;
        container.AddChild(enabledToggle);

        var meleeLightHotkey = new KeyBindInputControl(LocalizationManager.GetString("AimBot_Melee_LightHotKey"));
        meleeLightHotkey.KeyBind = CerberusConfig.MeleeAimBot.LightHotKey;
        meleeLightHotkey.KeyBindChanged += v => CerberusConfig.MeleeAimBot.LightHotKey = v;
        container.AddChild(meleeLightHotkey);

        var meleeHeavyHotkey = new KeyBindInputControl(LocalizationManager.GetString("AimBot_Melee_HeavyHotKey"));
        meleeHeavyHotkey.KeyBind = CerberusConfig.MeleeAimBot.HeavyHotKey;
        meleeHeavyHotkey.KeyBindChanged += v => CerberusConfig.MeleeAimBot.HeavyHotKey = v;
        container.AddChild(meleeHeavyHotkey);

        var radiusSlider = new SliderControl(LocalizationManager.GetString("AimBot_Melee_Radius"), 0f, 10f, CerberusConfig.MeleeAimBot.CircleRadius);
        radiusSlider.ValueChanged += v => CerberusConfig.MeleeAimBot.CircleRadius = v;
        container.AddChild(radiusSlider);

        var priorityCombo = new ComboControl(LocalizationManager.GetString("AimBot_Melee_Priority"), GetTargetPriorityNames());
        priorityCombo.SelectedIndex = CerberusConfig.MeleeAimBot.TargetPriority;
        priorityCombo.SelectedIndexChanged += i => CerberusConfig.MeleeAimBot.TargetPriority = i;
        container.AddChild(priorityCombo);

        var onlyPriorityToggle = new ToggleControl(LocalizationManager.GetString("AimBot_OnlyPriority"));
        onlyPriorityToggle.Value = CerberusConfig.MeleeAimBot.OnlyPriority;
        onlyPriorityToggle.ValueChanged += v => CerberusConfig.MeleeAimBot.OnlyPriority = v;
        container.AddChild(onlyPriorityToggle);

        var criticalToggle = new ToggleControl(LocalizationManager.GetString("AimBot_Melee_Critical"));
        criticalToggle.Value = CerberusConfig.MeleeAimBot.TargetCritical;
        criticalToggle.ValueChanged += v => CerberusConfig.MeleeAimBot.TargetCritical = v;
        container.AddChild(criticalToggle);

        var fixNetworkDelayToggle = new ToggleControl(LocalizationManager.GetString("AimBot_Melee_FixNetworkDelay"));
        fixNetworkDelayToggle.Value = CerberusConfig.MeleeAimBot.FixNetworkDelay;
        fixNetworkDelayToggle.ValueChanged += v => 
        {
            CerberusConfig.MeleeAimBot.FixNetworkDelay = v;
            UpdateMeleeFixDelayVisibility();
        };
        container.AddChild(fixNetworkDelayToggle);

        var fixDelaySlider = new SliderControl(LocalizationManager.GetString("AimBot_Melee_FixDelay"), 0.1f, 2f, CerberusConfig.MeleeAimBot.FixDelay);
        fixDelaySlider.ValueChanged += v => CerberusConfig.MeleeAimBot.FixDelay = v;
        fixDelaySlider.Visible = CerberusConfig.MeleeAimBot.FixNetworkDelay;
        _meleeFixDelaySlider = fixDelaySlider;
        container.AddChild(fixDelaySlider);

        var rotateToTargetToggle = new ToggleControl(LocalizationManager.GetString("AimBot_Melee_RotateToTarget"));
        rotateToTargetToggle.Value = CerberusConfig.MeleeHelper.RotateToTarget;
        rotateToTargetToggle.ValueChanged += v => CerberusConfig.MeleeHelper.RotateToTarget = v;
        container.AddChild(rotateToTargetToggle);

        parent.AddChild(container);
    }

    private void RenderMeleeRightTop(Control parent)
    {
        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(10)
        };

        var title = new Label
        {
            Text = LocalizationManager.GetString("AimBot_Melee_Visual"),
            FontColorOverride = Color.White,
            Margin = new Thickness(0, 0, 0, 10)
        };
        container.AddChild(title);

        var circleToggle = new ToggleControl(LocalizationManager.GetString("AimBot_Melee_Circle"));
        circleToggle.Value = CerberusConfig.MeleeAimBot.ShowCircle;
        circleToggle.ValueChanged += v => CerberusConfig.MeleeAimBot.ShowCircle = v;
        container.AddChild(circleToggle);

        var lineToggle = new ToggleControl(LocalizationManager.GetString("AimBot_Melee_Line"));
        lineToggle.Value = CerberusConfig.MeleeAimBot.ShowLine;
        lineToggle.ValueChanged += v => CerberusConfig.MeleeAimBot.ShowLine = v;
        container.AddChild(lineToggle);

        var colorPicker = new ColorPickerControl(LocalizationManager.GetString("AimBot_Melee_Color"));
        colorPicker.Color = CerberusConfig.MeleeAimBot.Color;
        colorPicker.ColorChanged += v => CerberusConfig.MeleeAimBot.Color = v;
        container.AddChild(colorPicker);

        parent.AddChild(container);
    }

    private void RenderMeleeRightDown(Control parent)
    {
        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(10)
        };

        var title = new Label
        {
            Text = LocalizationManager.GetString("AimBot_Melee_Helpers"),
            FontColorOverride = Color.White,
            Margin = new Thickness(0, 0, 0, 10)
        };
        container.AddChild(title);

        var enabledHelperToggle = new ToggleControl(LocalizationManager.GetString("AimBot_Melee_EnabledHelper"));
        enabledHelperToggle.Value = CerberusConfig.MeleeHelper.Enabled;
        enabledHelperToggle.ValueChanged += v => CerberusConfig.MeleeHelper.Enabled = v;
        container.AddChild(enabledHelperToggle);

        var attack360Toggle = new ToggleControl(LocalizationManager.GetString("AimBot_Melee_Attack360"));
        attack360Toggle.Value = CerberusConfig.MeleeHelper.Attack360;
        attack360Toggle.ValueChanged += v => CerberusConfig.MeleeHelper.Attack360 = v;
        container.AddChild(attack360Toggle);

        var autoAttackToggle = new ToggleControl(LocalizationManager.GetString("AimBot_Melee_AutoAttack"));
        autoAttackToggle.Value = CerberusConfig.MeleeHelper.AutoAttack;
        autoAttackToggle.ValueChanged += v => CerberusConfig.MeleeHelper.AutoAttack = v;
        container.AddChild(autoAttackToggle);

        parent.AddChild(container);
    }

    private void RenderEspLeft(Control parent)
    {
        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(10)
        };

        var title = new Label
        {
            Text = LocalizationManager.GetString("Visuals_ESP_General"),
            FontColorOverride = Color.White,
            Margin = new Thickness(0, 0, 0, 10)
        };
        container.AddChild(title);

        var enabledToggle = new ToggleControl(LocalizationManager.GetString("Visuals_ESP_Enabled"));
        enabledToggle.Value = CerberusConfig.Esp.Enabled;
        enabledToggle.ValueChanged += v => CerberusConfig.Esp.Enabled = v;
        container.AddChild(enabledToggle);

        var showNameToggle = new ToggleControl(LocalizationManager.GetString("Visuals_ESP_Name"));
        showNameToggle.Value = CerberusConfig.Esp.ShowName;
        showNameToggle.ValueChanged += v => CerberusConfig.Esp.ShowName = v;
        container.AddChild(showNameToggle);

        var showCKeyToggle = new ToggleControl(LocalizationManager.GetString("Visuals_ESP_CKey"));
        showCKeyToggle.Value = CerberusConfig.Esp.ShowCKey;
        showCKeyToggle.ValueChanged += v => CerberusConfig.Esp.ShowCKey = v;
        container.AddChild(showCKeyToggle);

        var showAntagToggle = new ToggleControl(LocalizationManager.GetString("Visuals_ESP_Antag"));
        showAntagToggle.Value = CerberusConfig.Esp.ShowAntag;
        showAntagToggle.ValueChanged += v => CerberusConfig.Esp.ShowAntag = v;
        container.AddChild(showAntagToggle);

        var showFriendToggle = new ToggleControl(LocalizationManager.GetString("Visuals_ESP_Friend"));
        showFriendToggle.Value = CerberusConfig.Esp.ShowFriend;
        showFriendToggle.ValueChanged += v => CerberusConfig.Esp.ShowFriend = v;
        container.AddChild(showFriendToggle);

        var showPriorityToggle = new ToggleControl(LocalizationManager.GetString("Visuals_ESP_Priority"));
        showPriorityToggle.Value = CerberusConfig.Esp.ShowPriority;
        showPriorityToggle.ValueChanged += v => CerberusConfig.Esp.ShowPriority = v;
        container.AddChild(showPriorityToggle);

        var showCombatModeToggle = new ToggleControl(LocalizationManager.GetString("Visuals_ESP_CombatMode"));
        showCombatModeToggle.Value = CerberusConfig.Esp.ShowCombatMode;
        showCombatModeToggle.ValueChanged += v => CerberusConfig.Esp.ShowCombatMode = v;
        container.AddChild(showCombatModeToggle);

        var showImplantsToggle = new ToggleControl(LocalizationManager.GetString("Visuals_ESP_Implants"));
        showImplantsToggle.Value = CerberusConfig.Esp.ShowImplants;
        showImplantsToggle.ValueChanged += v => CerberusConfig.Esp.ShowImplants = v;
        container.AddChild(showImplantsToggle);

        var showContrabandToggle = new ToggleControl(LocalizationManager.GetString("Visuals_ESP_Contraband"));
        showContrabandToggle.Value = CerberusConfig.Esp.ShowContraband;
        showContrabandToggle.ValueChanged += v => CerberusConfig.Esp.ShowContraband = v;
        container.AddChild(showContrabandToggle);

        var showWeaponToggle = new ToggleControl(LocalizationManager.GetString("ESP_Weapon"));
        showWeaponToggle.Value = CerberusConfig.Esp.ShowWeapon;
        showWeaponToggle.ValueChanged += v => CerberusConfig.Esp.ShowWeapon = v;
        container.AddChild(showWeaponToggle);

        var showNoSlipToggle = new ToggleControl(LocalizationManager.GetString("ESP_NoSlip"));
        showNoSlipToggle.Value = CerberusConfig.Esp.ShowNoSlip;
        showNoSlipToggle.ValueChanged += v => CerberusConfig.Esp.ShowNoSlip = v;
        container.AddChild(showNoSlipToggle);

        parent.AddChild(container);
    }

    private void RenderEyeLeft(Control parent)
    {
        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(10)
        };

        var title = new Label
        {
            Text = LocalizationManager.GetString("Visuals_Eye_General"),
            FontColorOverride = Color.White,
            Margin = new Thickness(0, 0, 0, 10)
        };
        container.AddChild(title);

        var fovToggle = new ToggleControl(LocalizationManager.GetString("Visuals_Eye_FOV"));
        fovToggle.Value = CerberusConfig.Eye.FovEnabled;
        fovToggle.ValueChanged += v => CerberusConfig.Eye.FovEnabled = v;
        container.AddChild(fovToggle);

        var fovHotkey = new KeyBindInputControl(LocalizationManager.GetString("Visuals_Eye_FOV_HotKey"));
        fovHotkey.KeyBind = CerberusConfig.Eye.FovHotKey;
        fovHotkey.KeyBindChanged += v => CerberusConfig.Eye.FovHotKey = v;
        container.AddChild(fovHotkey);

        var fullBrightToggle = new ToggleControl(LocalizationManager.GetString("Visuals_Eye_FullBright"));
        fullBrightToggle.Value = CerberusConfig.Eye.FullBrightEnabled;
        fullBrightToggle.ValueChanged += v => CerberusConfig.Eye.FullBrightEnabled = v;
        container.AddChild(fullBrightToggle);

        var fullBrightHotkey = new KeyBindInputControl(LocalizationManager.GetString("Visuals_Eye_FullBright_HotKey"));
        fullBrightHotkey.KeyBind = CerberusConfig.Eye.FullBrightHotKey;
        fullBrightHotkey.KeyBindChanged += v => CerberusConfig.Eye.FullBrightHotKey = v;
        container.AddChild(fullBrightHotkey);

        var zoomSlider = new SliderControl(LocalizationManager.GetString("Visuals_Eye_Zoom"), 0.5f, 30f, CerberusConfig.Eye.Zoom);
        zoomSlider.ValueChanged += v => CerberusConfig.Eye.Zoom = v;
        container.AddChild(zoomSlider);

        var zoomUpHotkey = new KeyBindInputControl(LocalizationManager.GetString("Visuals_Eye_ZoomUp_HotKey"));
        zoomUpHotkey.KeyBind = CerberusConfig.Eye.ZoomUpHotKey;
        zoomUpHotkey.KeyBindChanged += v => CerberusConfig.Eye.ZoomUpHotKey = v;
        container.AddChild(zoomUpHotkey);

        var zoomDownHotkey = new KeyBindInputControl(LocalizationManager.GetString("Visuals_Eye_ZoomDown_HotKey"));
        zoomDownHotkey.KeyBind = CerberusConfig.Eye.ZoomDownHotKey;
        zoomDownHotkey.KeyBindChanged += v => CerberusConfig.Eye.ZoomDownHotKey = v;
        container.AddChild(zoomDownHotkey);

        var resetButton = new Button
        {
            Text = LocalizationManager.GetString("Visuals_Eye_Reset"),
            MinHeight = 30
        };
        resetButton.OnPressed += _ =>
        {
            CerberusConfig.Eye.Zoom = 1f;
            zoomSlider.Value = 1f;
        };
        container.AddChild(resetButton);

        var storageViewerTitle = new Label
        {
            Text = LocalizationManager.GetString("Visuals_Eye_StorageViewer"),
            FontColorOverride = Color.White,
            Margin = new Thickness(0, 20, 0, 10)
        };
        container.AddChild(storageViewerTitle);

        var storageViewerToggle = new ToggleControl(LocalizationManager.GetString("Visuals_Eye_StorageViewer_Enabled"));
        storageViewerToggle.Value = CerberusConfig.StorageViewer.Enabled;
        storageViewerToggle.ValueChanged += v => CerberusConfig.StorageViewer.Enabled = v;
        container.AddChild(storageViewerToggle);

        var storageViewerHotkey = new KeyBindInputControl(LocalizationManager.GetString("Visuals_Eye_StorageViewer_HotKey"));
        storageViewerHotkey.KeyBind = CerberusConfig.StorageViewer.HotKey;
        storageViewerHotkey.KeyBindChanged += v => CerberusConfig.StorageViewer.HotKey = v;
        container.AddChild(storageViewerHotkey);

        var storageViewerColor = new ColorPickerControl(LocalizationManager.GetString("Visuals_Eye_StorageViewer_Color"));
        storageViewerColor.Color = CerberusConfig.StorageViewer.Color;
        storageViewerColor.ColorChanged += v => CerberusConfig.StorageViewer.Color = v;
        container.AddChild(storageViewerColor);

        parent.AddChild(container);
    }

    private void RenderFunLeft(Control parent)
    {
        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(10)
        };

        var title = new Label
        {
            Text = LocalizationManager.GetString("Visuals_Fun_General"),
            FontColorOverride = Color.White,
            Margin = new Thickness(0, 0, 0, 10)
        };
        container.AddChild(title);

        var enabledToggle = new ToggleControl(LocalizationManager.GetString("Visuals_Fun_Enabled"));
        enabledToggle.Value = CerberusConfig.Fun.Enabled;
        enabledToggle.ValueChanged += v => CerberusConfig.Fun.Enabled = v;
        container.AddChild(enabledToggle);

        var rotationToggle = new ToggleControl(LocalizationManager.GetString("Visuals_Fun_Rotation"));
        rotationToggle.Value = CerberusConfig.Fun.RotationEnabled;
        rotationToggle.ValueChanged += v => CerberusConfig.Fun.RotationEnabled = v;
        container.AddChild(rotationToggle);

        var rotationSpeedSlider = new SliderControl(LocalizationManager.GetString("Visuals_Fun_Speed"), 0f, 360f, CerberusConfig.Fun.RotationSpeed);
        rotationSpeedSlider.ValueChanged += v => CerberusConfig.Fun.RotationSpeed = v;
        container.AddChild(rotationSpeedSlider);

        var jumpToggle = new ToggleControl(LocalizationManager.GetString("Visuals_Fun_Jump"));
        jumpToggle.Value = CerberusConfig.Fun.JumpEnabled;
        jumpToggle.ValueChanged += v => CerberusConfig.Fun.JumpEnabled = v;
        container.AddChild(jumpToggle);

        var shakeToggle = new ToggleControl(LocalizationManager.GetString("Visuals_Fun_Snake"));
        shakeToggle.Value = CerberusConfig.Fun.ShakeEnabled;
        shakeToggle.ValueChanged += v => CerberusConfig.Fun.ShakeEnabled = v;
        container.AddChild(shakeToggle);

        var rainbowToggle = new ToggleControl(LocalizationManager.GetString("Visuals_Fun_Rainbow"));
        rainbowToggle.Value = CerberusConfig.Fun.RainbowEnabled;
        rainbowToggle.ValueChanged += v => CerberusConfig.Fun.RainbowEnabled = v;
        container.AddChild(rainbowToggle);

        var colorPicker = new ColorPickerControl(LocalizationManager.GetString("Visuals_Fun_Color"));
        colorPicker.Color = CerberusConfig.Fun.Color;
        colorPicker.ColorChanged += v => CerberusConfig.Fun.Color = v;
        container.AddChild(colorPicker);

        parent.AddChild(container);
    }

    private void RenderGeneralTab()
    {
        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(10)
        };

        var searchBox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            Margin = new Thickness(0, 0, 0, 10)
        };

        var searchInput = new LineEdit
        {
            PlaceHolder = LocalizationManager.GetString("Players_General_SearchHint"),
            HorizontalExpand = true,
            MinHeight = 30
        };
        searchInput.OnTextChanged += args => _playerSearch = args.Text;
        searchBox.AddChild(searchInput);

        var sortCombo = new ComboControl("", _sortOptions.Select(LocalizationManager.GetString).ToArray());
        sortCombo.SelectedIndex = _selectedSort;
        sortCombo.SelectedIndexChanged += i => 
        {
            _selectedSort = i;
            RenderGeneralTab();
        };
        searchBox.AddChild(sortCombo);

        container.AddChild(searchBox);

        var scrollContainer = new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            HScrollEnabled = false
        };

        var playerList = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true
        };

        if (_playerTracker != null)
        {
            var players = _playerTracker.AllPlayerSessions.Values.AsEnumerable();
            
            if (!string.IsNullOrWhiteSpace(_playerSearch))
            {
                var lowerSearch = _playerSearch.ToLowerInvariant();
                players = players.Where(p => 
                    p.Session.Name.ToLowerInvariant().Contains(lowerSearch) || 
                    p.EntityName.ToLowerInvariant().Contains(lowerSearch));
            }

            var sortedPlayers = _selectedSort switch
            {
                0 => players.OrderBy(p => p.Status == "Offline").ThenBy(p => p.Session.Name, StringComparer.OrdinalIgnoreCase),
                1 => players.OrderByDescending(p => p.Status == "Offline").ThenBy(p => p.Session.Name, StringComparer.OrdinalIgnoreCase),
                2 => players.OrderBy(p => p.Session.Name, StringComparer.OrdinalIgnoreCase),
                3 => players.OrderByDescending(p => p.Session.Name, StringComparer.OrdinalIgnoreCase),
                4 => players.OrderBy(p => p.EntityName == "Unknown").ThenBy(p => p.EntityName, StringComparer.OrdinalIgnoreCase),
                5 => players.OrderBy(p => p.EntityName == "Unknown").ThenByDescending(p => p.EntityName, StringComparer.OrdinalIgnoreCase),
                _ => players.OrderBy(p => p.Status == "Offline").ThenBy(p => p.Session.Name, StringComparer.OrdinalIgnoreCase)
            };

            foreach (var player in sortedPlayers)
            {
                var playerCard = CreatePlayerCard(player);
                playerList.AddChild(playerCard);
            }
        }

        scrollContainer.AddChild(playerList);
        container.AddChild(scrollContainer);
        if (_contentContainer != null)
            _contentContainer.AddChild(container);
    }

    private Control CreatePlayerCard(PlayerData playerData)
    {
        var card = new PanelContainer
        {
            MinHeight = 60,
            HorizontalExpand = true,
            Margin = new Thickness(0, 0, 0, 5)
        };
        card.PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = new Color(28, 29, 32),
            BorderThickness = new Thickness(1),
            BorderColor = new Color(50, 50, 50)
        };

        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            Margin = new Thickness(10)
        };

        var nameLabel = new Label
        {
            Text = playerData.Session.Name,
            MinWidth = 200,
            VerticalAlignment = VAlignment.Center
        };
        container.AddChild(nameLabel);

        var charLabel = new Label
        {
            Text = playerData.EntityName,
            MinWidth = 200,
            VerticalAlignment = VAlignment.Center
        };
        container.AddChild(charLabel);

        var entityLabel = new Label
        {
            Text = playerData.AttachedEntity?.ToString() ?? "None",
            MinWidth = 100,
            VerticalAlignment = VAlignment.Center
        };
        container.AddChild(entityLabel);

        var statusLabel = new Label
        {
            Text = playerData.Status,
            MinWidth = 100,
            VerticalAlignment = VAlignment.Center,
            FontColorOverride = playerData.Status == "Online" ? Color.Green : Color.Red
        };
        container.AddChild(statusLabel);

        var moreButton = new Button
        {
            Text = "More",
            MinWidth = 80,
            MinHeight = 30
        };
        moreButton.OnPressed += _ =>
        {
            OpenPlayerWindow(playerData);
        };
        container.AddChild(moreButton);

        card.AddChild(container);
        return card;
    }

    private void RenderLogsTab()
    {
        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(10)
        };

        var title = new Label
        {
            Text = LocalizationManager.GetString("Players_Logs_Title"),
            FontColorOverride = Color.White,
            Margin = new Thickness(0, 0, 0, 10)
        };
        container.AddChild(title);

        var scrollContainer = new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            HScrollEnabled = false
        };

        var logList = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true
        };

        foreach (var log in _logs)
        {
            var logLabel = new Label
            {
                Text = log.Key,
                HorizontalExpand = true,
                Margin = new Thickness(0, 0, 0, 2)
            };
            logLabel.FontColorOverride = log.Value == ConnectStatus.Join ? Color.Green : Color.Red;
            logList.AddChild(logLabel);
        }

        scrollContainer.AddChild(logList);
        container.AddChild(scrollContainer);
        if (_contentContainer != null)
            _contentContainer.AddChild(container);
    }

    private void RenderMiscGeneralLeft(Control parent)
    {
        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(10)
        };

        var antiSoapToggle = new ToggleControl(LocalizationManager.GetString("Misc_General_AntiSoap"));
        antiSoapToggle.Value = CerberusConfig.Misc.AntiSoapEnabled;
        antiSoapToggle.ValueChanged += v => CerberusConfig.Misc.AntiSoapEnabled = v;
        container.AddChild(antiSoapToggle);

        var antiAfkToggle = new ToggleControl(LocalizationManager.GetString("Misc_General_AntiAFK"));
        antiAfkToggle.Value = CerberusConfig.Misc.AntiAfkEnabled;
        antiAfkToggle.ValueChanged += v => CerberusConfig.Misc.AntiAfkEnabled = v;
        container.AddChild(antiAfkToggle);

        var showExplosionsToggle = new ToggleControl(LocalizationManager.GetString("Misc_General_ShowExplosions"));
        showExplosionsToggle.Value = CerberusConfig.Misc.ShowExplosive;
        showExplosionsToggle.ValueChanged += v => CerberusConfig.Misc.ShowExplosive = v;
        container.AddChild(showExplosionsToggle);

        var showTrajectoryToggle = new ToggleControl(LocalizationManager.GetString("Misc_General_ShowTrajectory"));
        showTrajectoryToggle.Value = CerberusConfig.Misc.ShowTrajectory;
        showTrajectoryToggle.ValueChanged += v => CerberusConfig.Misc.ShowTrajectory = v;
        container.AddChild(showTrajectoryToggle);

        var damageOverlayToggle = new ToggleControl(LocalizationManager.GetString("Misc_General_DamageOverlay"));
        damageOverlayToggle.Value = CerberusConfig.Misc.DamageOverlayEnabled;
        damageOverlayToggle.ValueChanged += v => CerberusConfig.Misc.DamageOverlayEnabled = v;
        container.AddChild(damageOverlayToggle);

        var antiAimToggle = new ToggleControl(LocalizationManager.GetString("Misc_General_AntiAim"));
        antiAimToggle.Value = CerberusConfig.Misc.AntiAimEnabled;
        antiAimToggle.ValueChanged += v => CerberusConfig.Misc.AntiAimEnabled = v;
        container.AddChild(antiAimToggle);

        var speedSlider = new SliderControl(LocalizationManager.GetString("Misc_General_Speed"), 180f, 3600f, CerberusConfig.Misc.AutoRotateSpeed);
        speedSlider.ValueChanged += v => CerberusConfig.Misc.AutoRotateSpeed = v;
        container.AddChild(speedSlider);

        var trashTalkToggle = new ToggleControl(LocalizationManager.GetString("Misc_General_TrashTalk"));
        trashTalkToggle.Value = CerberusConfig.Misc.TrashTalkEnabled;
        trashTalkToggle.ValueChanged += v => CerberusConfig.Misc.TrashTalkEnabled = v;
        container.AddChild(trashTalkToggle);

        var openFolderButton = new Button
        {
            Text = LocalizationManager.GetString("Misc_General_OpenFolder"),
            MinHeight = 30
        };
        openFolderButton.OnPressed += _ =>
        {
            var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CerberusWare");
            Process.Start("explorer", configPath);
        };
        container.AddChild(openFolderButton);

        parent.AddChild(container);
    }

    private void RenderMiscSpammerLeft(Control parent)
    {
        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(10)
        };

        var title = new Label
        {
            Text = LocalizationManager.GetString("Misc_Spammer_Settings"),
            FontColorOverride = Color.White,
            Margin = new Thickness(0, 0, 0, 10)
        };
        container.AddChild(title);

        var protectWordToggle = new ToggleControl(LocalizationManager.GetString("Misc_Spammer_Settings_ProtectWord"));
        protectWordToggle.Value = CerberusConfig.Spammer.ProtectTextEnabled;
        protectWordToggle.ValueChanged += v => CerberusConfig.Spammer.ProtectTextEnabled = v;
        container.AddChild(protectWordToggle);

        var randomLengthToggle = new ToggleControl(LocalizationManager.GetString("Misc_Spammer_Settings_RandomLength"));
        randomLengthToggle.Value = CerberusConfig.Spammer.ProtectRandomLength;
        randomLengthToggle.ValueChanged += v => CerberusConfig.Spammer.ProtectRandomLength = v;
        container.AddChild(randomLengthToggle);

        var lengthSlider = new SliderControl(LocalizationManager.GetString("Misc_Spammer_Settings_Length"), 1, 12, CerberusConfig.Spammer.ProtectLength);
        lengthSlider.ValueChanged += v => CerberusConfig.Spammer.ProtectLength = (int)v;
        lengthSlider.Visible = !CerberusConfig.Spammer.ProtectRandomLength;
        randomLengthToggle.ValueChanged += v => lengthSlider.Visible = !v;
        container.AddChild(lengthSlider);

        ChannelAddToggle(container, "Local", 1);
        ChannelAddToggle(container, "Whisper", 2);
        ChannelAddToggle(container, "Radio", 16);
        ChannelAddToggle(container, "LOOC", 32);
        ChannelAddToggle(container, "OOC", 64);
        ChannelAddToggle(container, "Emotes", 512);
        ChannelAddToggle(container, "Dead", 1024);
        ChannelAddToggle(container, "Admin", 8192);

        parent.AddChild(container);
    }

    private void ChannelAddToggle(Control parent, string label, int channel)
    {
        bool isEnabled = CerberusConfig.Spammer.Channels.Contains(channel);
        var toggle = new ToggleControl(label);
        toggle.Value = isEnabled;
        toggle.ValueChanged += v =>
        {
            if (v)
            {
                if (!CerberusConfig.Spammer.Channels.Contains(channel))
                {
                    CerberusConfig.Spammer.Channels.Add(channel);
                }
            }
            else
            {
                CerberusConfig.Spammer.Channels.Remove(channel);
            }
        };
        parent.AddChild(toggle);
    }

    private void RenderSettingsGeneralLeft(Control parent)
    {
        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(10)
        };

        var title = new Label
        {
            Text = LocalizationManager.GetString("Settings_General"),
            FontColorOverride = Color.White,
            Margin = new Thickness(0, 0, 0, 10)
        };
        container.AddChild(title);

        var showMenuToggle = new ToggleControl(LocalizationManager.GetString("Settings_ShowMenu"));
        showMenuToggle.Value = CerberusConfig.Settings.ShowMenu;
        showMenuToggle.ValueChanged += v => CerberusConfig.Settings.ShowMenu = v;
        container.AddChild(showMenuToggle);

        var showMenuHotkey = new KeyBindInputControl(LocalizationManager.GetString("Settings_ShowMenuHotKey"));
        showMenuHotkey.KeyBind = CerberusConfig.Settings.ShowMenuHotKey;
        showMenuHotkey.KeyBindChanged += v => CerberusConfig.Settings.ShowMenuHotKey = v;
        container.AddChild(showMenuHotkey);

        var uiCustomizableToggle = new ToggleControl(LocalizationManager.GetString("Settings_UICustomizable"));
        uiCustomizableToggle.Value = CerberusConfig.Settings.UiCustomizable;
        uiCustomizableToggle.ValueChanged += v => CerberusConfig.Settings.UiCustomizable = v;
        container.AddChild(uiCustomizableToggle);

        var languageButton = new Button
        {
            Text = CerberusConfig.Settings.CurrentLanguage == Language.Ru 
                ? LocalizationManager.GetString("Settings_General_SwitchToEnglish")
                : LocalizationManager.GetString("Settings_General_SwitchToRussian"),
            MinHeight = 30
        };
        languageButton.OnPressed += _ =>
        {
            LocalizationManager.Switch();
            languageButton.Text = CerberusConfig.Settings.CurrentLanguage == Language.Ru 
                ? LocalizationManager.GetString("Settings_General_SwitchToEnglish")
                : LocalizationManager.GetString("Settings_General_SwitchToRussian");
            UpdateContent();
        };
        container.AddChild(languageButton);

        var unloadButton = new Button
        {
            Text = LocalizationManager.GetString("Settings_General_Unload"),
            MinHeight = 30
        };
        unloadButton.OnPressed += _ => MainController.Instance.PanicUnload();
        container.AddChild(unloadButton);

        parent.AddChild(container);
    }

    private void RenderConfigsTab(Control parent)
    {
        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(10)
        };

        var title = new Label
        {
            Text = LocalizationManager.GetString("Configs_Title"),
            FontColorOverride = Color.White,
            Margin = new Thickness(0, 0, 0, 10)
        };
        container.AddChild(title);

        var configNameInput = new LineEdit
        {
            PlaceHolder = LocalizationManager.GetString("Configs_Name"),
            MinHeight = 30,
            Margin = new Thickness(0, 0, 0, 10)
        };
        container.AddChild(configNameInput);

        var scrollContainer = new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            HScrollEnabled = false,
            Margin = new Thickness(0, 0, 0, 10)
        };

        var configList = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true
        };

        if (Directory.Exists(ConfigManager.configDir))
        {
            var files = Directory.GetFiles(ConfigManager.configDir, "*.json");
            ConfigManager.ConfigFiles = files.Select(Path.GetFileNameWithoutExtension).ToList();
        }
        else
        {
            ConfigManager.ConfigFiles = new List<string>();
        }

        int selectedIndex = ConfigManager.SelectedConfigIndex;
        for (int i = 0; i < ConfigManager.ConfigFiles.Count; i++)
        {
            var configName = ConfigManager.ConfigFiles[i];
            var configButton = new Button
            {
                Text = configName,
                MinHeight = 30,
                Margin = new Thickness(0, 0, 0, 2)
            };
            int index = i;
            configButton.OnPressed += _ =>
            {
                ConfigManager.SelectedConfigIndex = index;
                RenderConfigsTab(parent);
            };
            if (selectedIndex == i)
            {
                configButton.StyleBoxOverride = new StyleBoxFlat
                {
                    BackgroundColor = new Color(0, 100, 200),
                    BorderThickness = new Thickness(1),
                    BorderColor = new Color(0, 150, 255)
                };
            }
            configList.AddChild(configButton);
        }

        scrollContainer.AddChild(configList);
        container.AddChild(scrollContainer);

        var buttonContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true
        };

        var saveButton = new Button
        {
            Text = LocalizationManager.GetString("Configs_Save"),
            MinHeight = 30,
            HorizontalExpand = true,
            Margin = new Thickness(0, 0, 5, 0)
        };
        saveButton.OnPressed += _ =>
        {
            var name = configNameInput.Text;
            if (!string.IsNullOrWhiteSpace(name))
            {
                ConfigManager.SaveConfig(name);
                RenderConfigsTab(parent);
            }
        };
        buttonContainer.AddChild(saveButton);

        if (selectedIndex >= 0 && selectedIndex < ConfigManager.ConfigFiles.Count)
        {
            var loadButton = new Button
            {
                Text = LocalizationManager.GetString("Configs_Load"),
                MinHeight = 30,
                HorizontalExpand = true,
                Margin = new Thickness(0, 0, 5, 0)
            };
            loadButton.OnPressed += _ =>
            {
                var config = ConfigManager.LoadConfig(ConfigManager.ConfigFiles[selectedIndex]);
                if (config != null)
                {
                    ConfigManager.ApplyConfig(config);
                    UpdateContent();
                }
            };
            buttonContainer.AddChild(loadButton);

            var deleteButton = new Button
            {
                Text = LocalizationManager.GetString("Configs_Delete"),
                MinHeight = 30,
                HorizontalExpand = true
            };
            deleteButton.OnPressed += _ =>
            {
                var path = Path.Combine(ConfigManager.configDir, ConfigManager.ConfigFiles[selectedIndex] + ".json");
                if (File.Exists(path))
                {
                    File.Delete(path);
                    ConfigManager.SelectedConfigIndex = -1;
                    RenderConfigsTab(parent);
                }
            };
            buttonContainer.AddChild(deleteButton);
        }

        var openFolderButton = new Button
        {
            Text = LocalizationManager.GetString("Configs_OpenFolder"),
            MinHeight = 30,
            HorizontalExpand = true,
            Margin = new Thickness(0, 10, 0, 0)
        };
        openFolderButton.OnPressed += _ =>
        {
            if (!Directory.Exists(ConfigManager.configDir))
                Directory.CreateDirectory(ConfigManager.configDir);
            Process.Start("explorer", ConfigManager.configDir);
        };
        container.AddChild(buttonContainer);
        container.AddChild(openFolderButton);

        parent.AddChild(container);
    }

    private string[] GetTargetPriorityNames()
    {
        return new[]
        {
            LocalizationManager.GetString("AimBot_TargetPriority_DistanceToPlayer"),
            LocalizationManager.GetString("AimBot_TargetPriority_DistanceToMouse"),
            LocalizationManager.GetString("AimBot_TargetPriority_LowestHealth")
        };
    }

    private void UpdateMeleeFixDelayVisibility()
    {
        if (_meleeFixDelaySlider != null)
        {
            _meleeFixDelaySlider.Visible = CerberusConfig.MeleeAimBot.FixNetworkDelay;
        }
    }

    private SliderControl? _meleeFixDelaySlider;

    private void RenderEspRightTop(Control parent)
    {
        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(10)
        };

        var title = new Label
        {
            Text = LocalizationManager.GetString("Visuals_ESP_Colors"),
            FontColorOverride = Color.White,
            Margin = new Thickness(0, 0, 0, 10)
        };
        container.AddChild(title);

        var nameColor = new ColorPickerControl(LocalizationManager.GetString("Visuals_ESP_Color_Name"));
        nameColor.Color = CerberusConfig.Esp.NameColor;
        nameColor.ColorChanged += v => CerberusConfig.Esp.NameColor = v;
        container.AddChild(nameColor);

        var ckeyColor = new ColorPickerControl(LocalizationManager.GetString("Visuals_ESP_Color_CKey"));
        ckeyColor.Color = CerberusConfig.Esp.CKeyColor;
        ckeyColor.ColorChanged += v => CerberusConfig.Esp.CKeyColor = v;
        container.AddChild(ckeyColor);

        var antagColor = new ColorPickerControl(LocalizationManager.GetString("Visuals_ESP_Color_Antag"));
        antagColor.Color = CerberusConfig.Esp.AntagColor;
        antagColor.ColorChanged += v => CerberusConfig.Esp.AntagColor = v;
        container.AddChild(antagColor);

        var friendColor = new ColorPickerControl(LocalizationManager.GetString("Visuals_ESP_Color_Friend"));
        friendColor.Color = CerberusConfig.Esp.FriendColor;
        friendColor.ColorChanged += v => CerberusConfig.Esp.FriendColor = v;
        container.AddChild(friendColor);

        var priorityColor = new ColorPickerControl(LocalizationManager.GetString("Visuals_ESP_Color_Priority"));
        priorityColor.Color = CerberusConfig.Esp.PriorityColor;
        priorityColor.ColorChanged += v => CerberusConfig.Esp.PriorityColor = v;
        container.AddChild(priorityColor);

        var combatModeColor = new ColorPickerControl(LocalizationManager.GetString("Visuals_ESP_Color_CombatMode"));
        combatModeColor.Color = CerberusConfig.Esp.CombatModeColor;
        combatModeColor.ColorChanged += v => CerberusConfig.Esp.CombatModeColor = v;
        container.AddChild(combatModeColor);

        var implantsColor = new ColorPickerControl(LocalizationManager.GetString("Visuals_ESP_Color_Implants"));
        implantsColor.Color = CerberusConfig.Esp.ImplantsColor;
        implantsColor.ColorChanged += v => CerberusConfig.Esp.ImplantsColor = v;
        container.AddChild(implantsColor);

        var contrabandColor = new ColorPickerControl(LocalizationManager.GetString("Visuals_ESP_Color_Contraband"));
        contrabandColor.Color = CerberusConfig.Esp.ContrabandColor;
        contrabandColor.ColorChanged += v => CerberusConfig.Esp.ContrabandColor = v;
        container.AddChild(contrabandColor);

        var weaponColor = new ColorPickerControl(LocalizationManager.GetString("ESP_Weapon"));
        weaponColor.Color = CerberusConfig.Esp.WeaponColor;
        weaponColor.ColorChanged += v => CerberusConfig.Esp.WeaponColor = v;
        container.AddChild(weaponColor);

        var noSlipColor = new ColorPickerControl(LocalizationManager.GetString("ESP_NoSlip"));
        noSlipColor.Color = CerberusConfig.Esp.NoSlipColor;
        noSlipColor.ColorChanged += v => CerberusConfig.Esp.NoSlipColor = v;
        container.AddChild(noSlipColor);

        parent.AddChild(container);
    }

    private void RenderEspRightDown(Control parent)
    {
        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(10)
        };

        var title = new Label
        {
            Text = LocalizationManager.GetString("Visuals_ESP_Font"),
            FontColorOverride = Color.White,
            Margin = new Thickness(0, 0, 0, 10)
        };
        container.AddChild(title);

        var fontIntervalSlider = new SliderControl(LocalizationManager.GetString("Visuals_ESP_Font_Interval"), 1, 50, CerberusConfig.Esp.FontInterval);
        fontIntervalSlider.ValueChanged += v => CerberusConfig.Esp.FontInterval = (int)v;
        container.AddChild(fontIntervalSlider);

        var fonts = new[]
        {
            "Boxfont Round",
            "NotoSans Regular",
            "NotoSans Bold",
            "NotoSans Italic"
        };

        var mainFontCombo = new ComboControl(LocalizationManager.GetString("Visuals_ESP_Font_MainFont"), fonts);
        mainFontCombo.SelectedIndex = CerberusConfig.Esp.MainFontIndex;
        mainFontCombo.SelectedIndexChanged += i =>
        {
            CerberusConfig.Esp.MainFontIndex = i;
            CerberusConfig.Esp.MainFontPath = i < fonts.Length ? $"/Fonts/{fonts[i]}/{fonts[i]}.ttf" : "/Fonts/Boxfont-round/Boxfont Round.ttf";
        };
        container.AddChild(mainFontCombo);

        var mainFontSizeSlider = new SliderControl(LocalizationManager.GetString("Visuals_ESP_Font_Size"), 6, 30, CerberusConfig.Esp.MainFontSize);
        mainFontSizeSlider.ValueChanged += v => CerberusConfig.Esp.MainFontSize = (int)v;
        container.AddChild(mainFontSizeSlider);

        var otherFontCombo = new ComboControl(LocalizationManager.GetString("Visuals_ESP_Font_OtherFont"), fonts);
        otherFontCombo.SelectedIndex = CerberusConfig.Esp.OtherFontIndex;
        otherFontCombo.SelectedIndexChanged += i =>
        {
            CerberusConfig.Esp.OtherFontIndex = i;
            CerberusConfig.Esp.OtherFontPath = i < fonts.Length ? $"/Fonts/{fonts[i]}/{fonts[i]}.ttf" : "/Fonts/Boxfont-round/Boxfont Round.ttf";
        };
        container.AddChild(otherFontCombo);

        var otherFontSizeSlider = new SliderControl(LocalizationManager.GetString("Visuals_ESP_Font_Size_Other"), 6, 30, CerberusConfig.Esp.OtherFontSize);
        otherFontSizeSlider.ValueChanged += v => CerberusConfig.Esp.OtherFontSize = (int)v;
        container.AddChild(otherFontSizeSlider);

        parent.AddChild(container);
    }

    private void RenderEyeRightTop(Control parent)
    {
        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(10)
        };

        var title = new Label
        {
            Text = LocalizationManager.GetString("Visuals_Eye_HUD"),
            FontColorOverride = Color.White,
            Margin = new Thickness(0, 0, 0, 10)
        };
        container.AddChild(title);

        var showHealthToggle = new ToggleControl(LocalizationManager.GetString("Visuals_Eye_HUD_Health"));
        showHealthToggle.Value = CerberusConfig.Hud.ShowHealth;
        showHealthToggle.ValueChanged += v => CerberusConfig.Hud.ShowHealth = v;
        container.AddChild(showHealthToggle);

        var showStaminaToggle = new ToggleControl(LocalizationManager.GetString("Visuals_Eye_HUD_Stamina"));
        showStaminaToggle.Value = CerberusConfig.Hud.ShowStamina;
        showStaminaToggle.ValueChanged += v => CerberusConfig.Hud.ShowStamina = v;
        container.AddChild(showStaminaToggle);

        var staminaColor = new ColorPickerControl(LocalizationManager.GetString("Visuals_Eye_HUD_Color"));
        staminaColor.Color = CerberusConfig.Hud.StaminaColor;
        staminaColor.ColorChanged += v => CerberusConfig.Hud.StaminaColor = v;
        container.AddChild(staminaColor);

        HudIconToggle(container, LocalizationManager.GetString("Visuals_Eye_HUD_AntagIcons"), ref CerberusConfig.Hud.ShowAntag, "ShowAntagIcons");
        HudIconToggle(container, LocalizationManager.GetString("Visuals_Eye_HUD_JobIcons"), ref CerberusConfig.Hud.ShowJobIcons, "ShowJobIcons");
        HudIconToggle(container, LocalizationManager.GetString("Visuals_Eye_HUD_MindShieldIcons"), ref CerberusConfig.Hud.ShowMindShieldIcons, "ShowMindShieldIcons");
        HudIconToggle(container, LocalizationManager.GetString("Visuals_Eye_HUD_CriminalRecordIcons"), ref CerberusConfig.Hud.ShowCriminalRecordIcons, "ShowCriminalRecordIcons");
        HudIconToggle(container, LocalizationManager.GetString("Visuals_Eye_HUD_SyndicateIcons"), ref CerberusConfig.Hud.ShowSyndicateIcons, "ShowSyndicateIcons");
        HudIconToggle(container, LocalizationManager.GetString("Visuals_Eye_HUD_ChemicalAnalysis"), ref CerberusConfig.Hud.ChemicalAnalysis, "SolutionScanner");
        HudIconToggle(container, LocalizationManager.GetString("Visuals_Eye_HUD_ShowElectrocution"), ref CerberusConfig.Hud.ShowElectrocution, "ShowElectrocutionHUD");

        parent.AddChild(container);
    }

    private void RenderEyeRightDown(Control parent)
    {
        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(10)
        };

        var title = new Label
        {
            Text = LocalizationManager.GetString("Visuals_Eye_Patches"),
            FontColorOverride = Color.White,
            Margin = new Thickness(0, 0, 0, 10)
        };
        container.AddChild(title);

        var noClydeToggle = new ToggleControl(LocalizationManager.GetString("Visuals_Eye_Patches_NoClyde"));
        noClydeToggle.Value = CerberusConfig.Settings.ClydePatch;
        noClydeToggle.ValueChanged += v => CerberusConfig.Settings.ClydePatch = v;
        container.AddChild(noClydeToggle);

        var noSmokeToggle = new ToggleControl(LocalizationManager.GetString("Visuals_Eye_Patches_NoSmoke"));
        noSmokeToggle.Value = CerberusConfig.Settings.SmokePatch;
        noSmokeToggle.ValueChanged += v => CerberusConfig.Settings.SmokePatch = v;
        container.AddChild(noSmokeToggle);

        var noBadOverlaysToggle = new ToggleControl(LocalizationManager.GetString("Visuals_Eye_Patches_NoBadOverlays"));
        noBadOverlaysToggle.Value = CerberusConfig.Settings.OverlaysPatch;
        noBadOverlaysToggle.ValueChanged += v => CerberusConfig.Settings.OverlaysPatch = v;
        container.AddChild(noBadOverlaysToggle);

        var noCameraRecoilToggle = new ToggleControl(LocalizationManager.GetString("Visuals_Eye_Patches_NoCameraRecoil"));
        noCameraRecoilToggle.Value = CerberusConfig.Settings.NoCameraKickPatch;
        noCameraRecoilToggle.ValueChanged += v => CerberusConfig.Settings.NoCameraKickPatch = v;
        container.AddChild(noCameraRecoilToggle);

        parent.AddChild(container);
    }

    private void RenderFunRightTop(Control parent)
    {
        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(10)
        };

        var title = new Label
        {
            Text = LocalizationManager.GetString("Visuals_Fun_Filters"),
            FontColorOverride = Color.White,
            Margin = new Thickness(0, 0, 0, 10)
        };
        container.AddChild(title);

        var affectPlayerToggle = new ToggleControl(LocalizationManager.GetString("Visuals_Fun_Filters_Player"));
        affectPlayerToggle.Value = CerberusConfig.Fun.AffectPlayer;
        affectPlayerToggle.ValueChanged += v => CerberusConfig.Fun.AffectPlayer = v;
        container.AddChild(affectPlayerToggle);

        var affectMobsToggle = new ToggleControl(LocalizationManager.GetString("Visuals_Fun_Filters_Mobs"));
        affectMobsToggle.Value = CerberusConfig.Fun.AffectMobs;
        affectMobsToggle.ValueChanged += v => CerberusConfig.Fun.AffectMobs = v;
        container.AddChild(affectMobsToggle);

        var affectOthersToggle = new ToggleControl(LocalizationManager.GetString("Visuals_Fun_Filters_Others"));
        affectOthersToggle.Value = CerberusConfig.Fun.AffectOthers;
        affectOthersToggle.ValueChanged += v => CerberusConfig.Fun.AffectOthers = v;
        container.AddChild(affectOthersToggle);

        parent.AddChild(container);
    }

    private void RenderFunRightDown(Control parent)
    {
        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(10)
        };

        var title = new Label
        {
            Text = LocalizationManager.GetString("Visuals_Fun_TextureOverlay"),
            FontColorOverride = Color.White,
            Margin = new Thickness(0, 0, 0, 10)
        };
        container.AddChild(title);

        var textureEnabledToggle = new ToggleControl(LocalizationManager.GetString("Visuals_Fun_TextureOverlay_Enabled"));
        textureEnabledToggle.Value = CerberusConfig.Texture.Enabled;
        textureEnabledToggle.ValueChanged += v => CerberusConfig.Texture.Enabled = v;
        container.AddChild(textureEnabledToggle);

        var openFolderButton = new Button
        {
            Text = LocalizationManager.GetString("Visuals_Fun_TextureOverlay_OpenFolder"),
            MinHeight = 30
        };
        openFolderButton.OnPressed += _ =>
        {
            var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CerberusWare");
            Process.Start("explorer", configPath);
        };
        container.AddChild(openFolderButton);

        var sizeSlider = new SliderControl(LocalizationManager.GetString("Visuals_Fun_TextureOverlay_Size"), 0.1f, 5f, CerberusConfig.Texture.Size);
        sizeSlider.ValueChanged += v => CerberusConfig.Texture.Size = v;
        container.AddChild(sizeSlider);

        var invisibleToggle = new ToggleControl(LocalizationManager.GetString("Visuals_Fun_TextureOverlay_Invisible"));
        invisibleToggle.Value = CerberusConfig.Texture.MakeEntitiesInvisible;
        invisibleToggle.ValueChanged += v => CerberusConfig.Texture.MakeEntitiesInvisible = v;
        container.AddChild(invisibleToggle);

        parent.AddChild(container);
    }

    private void RenderMiscGeneralRightTop(Control parent)
    {
        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(10)
        };

        var title = new Label
        {
            Text = LocalizationManager.GetString("Misc_General_Translator"),
            FontColorOverride = Color.White,
            Margin = new Thickness(0, 0, 0, 10)
        };
        container.AddChild(title);

        var translateChatToggle = new ToggleControl(LocalizationManager.GetString("Misc_General_Translator_TranslateChat"));
        translateChatToggle.Value = CerberusConfig.Settings.TranslateChatPatch;
        translateChatToggle.ValueChanged += v => CerberusConfig.Settings.TranslateChatPatch = v;
        container.AddChild(translateChatToggle);

        var languageInput = new LineEdit
        {
            PlaceHolder = LocalizationManager.GetString("Misc_General_Translator_LanguageChat"),
            Text = CerberusConfig.Settings.TranslateChatLang,
            MinHeight = 30,
            Visible = CerberusConfig.Settings.TranslateChatPatch
        };
        languageInput.OnTextChanged += args => CerberusConfig.Settings.TranslateChatLang = args.Text;
        translateChatToggle.ValueChanged += v => languageInput.Visible = v;
        container.AddChild(languageInput);

        var infoLabel = new Label
        {
            Text = CerberusConfig.Settings.CurrentLanguage == Language.En 
                ? "At the moment, translators for aHelp and a self-translator are being developed"
                : "В данный момент разрабатываются переводчики для ahelp и самопереводчик",
            Margin = new Thickness(0, 10, 0, 0)
        };
        container.AddChild(infoLabel);

        parent.AddChild(container);
    }

    private void RenderMiscGeneralRightDown(Control parent)
    {
        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(10)
        };

        var title = new Label
        {
            Text = LocalizationManager.GetString("Misc_General_SearchingItems"),
            FontColorOverride = Color.White,
            Margin = new Thickness(0, 0, 0, 10)
        };
        container.AddChild(title);

        var enabledToggle = new ToggleControl(LocalizationManager.GetString("Misc_General_SearchingItems_Enabled"));
        enabledToggle.Value = CerberusConfig.Misc.ItemSearcherEnabled;
        enabledToggle.ValueChanged += v => CerberusConfig.Misc.ItemSearcherEnabled = v;
        container.AddChild(enabledToggle);

        var showNameToggle = new ToggleControl(LocalizationManager.GetString("Misc_General_SearchingItems_ShowName"));
        showNameToggle.Value = CerberusConfig.Misc.ItemSearcherShowName;
        showNameToggle.ValueChanged += v => CerberusConfig.Misc.ItemSearcherShowName = v;
        container.AddChild(showNameToggle);

        var scrollContainer = new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            HScrollEnabled = false,
            MinHeight = 200
        };

        var itemList = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true
        };

        for (int i = 0; i < CerberusConfig.Misc.ItemSearchEntries.Count; i++)
        {
            var entry = CerberusConfig.Misc.ItemSearchEntries[i];
            var inputWithColor = new InputTextWithColorControl("", entry.ItemName, entry.Color);
            inputWithColor.TextChanged += text =>
            {
                entry.ItemName = text;
                CerberusConfig.Misc.ItemSearchEntries[i] = entry;
            };
            inputWithColor.ColorChanged += color =>
            {
                entry.Color = color;
                CerberusConfig.Misc.ItemSearchEntries[i] = entry;
            };
            int index = i;
            inputWithColor.DeletePressed += () =>
            {
                CerberusConfig.Misc.ItemSearchEntries.RemoveAt(index);
                RenderMiscGeneralRightDown(parent);
            };
            itemList.AddChild(inputWithColor);
        }

        scrollContainer.AddChild(itemList);
        container.AddChild(scrollContainer);

        var addButton = new Button
        {
            Text = LocalizationManager.GetString("Misc_General_SearchingItems_Add"),
            MinHeight = 30
        };
        addButton.OnPressed += _ =>
        {
            CerberusConfig.Misc.ItemSearchEntries.Add(new ItemSearchEntry());
            RenderMiscGeneralRightDown(parent);
        };
        container.AddChild(addButton);

        parent.AddChild(container);
    }

    private void RenderMiscSpammerRightTop(Control parent)
    {
        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(10)
        };

        var title = new Label
        {
            Text = LocalizationManager.GetString("Misc_Spammer_Chat"),
            FontColorOverride = Color.White,
            Margin = new Thickness(0, 0, 0, 10)
        };
        container.AddChild(title);

        var enabledToggle = new ToggleControl(LocalizationManager.GetString("Misc_Spammer_Chat_Enabled"));
        enabledToggle.Value = CerberusConfig.Spammer.ChatEnabled;
        enabledToggle.ValueChanged += v =>
        {
            CerberusConfig.Spammer.ChatEnabled = v;
            if (v && _spammerSystem != null)
            {
                _spammerSystem.StartSpamChat();
            }
        };
        container.AddChild(enabledToggle);

        var delaySlider = new SliderControl(LocalizationManager.GetString("Misc_Spammer_Chat_Delay"), 10, 1000, CerberusConfig.Spammer.ChatDelay);
        delaySlider.ValueChanged += v => CerberusConfig.Spammer.ChatDelay = (int)v;
        container.AddChild(delaySlider);

        var textInput = new LineEdit
        {
            Text = CerberusConfig.Spammer.ChatText,
            PlaceHolder = LocalizationManager.GetString("Misc_Spammer_Chat_Text"),
            MinHeight = 30
        };
        textInput.OnTextChanged += args => CerberusConfig.Spammer.ChatText = args.Text;
        container.AddChild(textInput);

        parent.AddChild(container);
    }

    private void RenderMiscSpammerRightDown(Control parent)
    {
        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(10)
        };

        var title = new Label
        {
            Text = LocalizationManager.GetString("Misc_Spammer_AHelp"),
            FontColorOverride = Color.White,
            Margin = new Thickness(0, 0, 0, 10)
        };
        container.AddChild(title);

        var enabledToggle = new ToggleControl(LocalizationManager.GetString("Misc_Spammer_AHelp_Enabled"));
        enabledToggle.Value = CerberusConfig.Spammer.AHelpEnabled;
        enabledToggle.ValueChanged += v =>
        {
            CerberusConfig.Spammer.AHelpEnabled = v;
            if (v && _spammerSystem != null)
            {
                _spammerSystem.StartSpamAHelp();
            }
        };
        container.AddChild(enabledToggle);

        var delaySlider = new SliderControl(LocalizationManager.GetString("Misc_Spammer_AHelp_Delay"), 10, 1000, CerberusConfig.Spammer.AHelpDelay);
        delaySlider.ValueChanged += v => CerberusConfig.Spammer.AHelpDelay = (int)v;
        container.AddChild(delaySlider);

        var textInput = new LineEdit
        {
            Text = CerberusConfig.Spammer.AHelpText,
            PlaceHolder = LocalizationManager.GetString("Misc_Spammer_AHelp_Text"),
            MinHeight = 30
        };
        textInput.OnTextChanged += args => CerberusConfig.Spammer.AHelpText = args.Text;
        container.AddChild(textInput);

        parent.AddChild(container);
    }

    private void RenderSettingsGeneralRight(Control parent)
    {
        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(10)
        };

        var title = new Label
        {
            Text = LocalizationManager.GetString("Settings_Patches"),
            FontColorOverride = Color.White,
            Margin = new Thickness(0, 0, 0, 10)
        };
        container.AddChild(title);

        var adminPrivilegeToggle = new ToggleControl(LocalizationManager.GetString("Settings_Patches_AdminPrivilege"));
        adminPrivilegeToggle.Value = CerberusConfig.Settings.AdminPatch;
        adminPrivilegeToggle.ValueChanged += v => CerberusConfig.Settings.AdminPatch = v;
        container.AddChild(adminPrivilegeToggle);

        var noDamageFriendToggle = new ToggleControl(LocalizationManager.GetString("Settings_Patches_NoDamageFriend"));
        noDamageFriendToggle.Value = CerberusConfig.Settings.NoDmgFriendPatch;
        noDamageFriendToggle.ValueChanged += v => CerberusConfig.Settings.NoDmgFriendPatch = v;
        container.AddChild(noDamageFriendToggle);

        var noDamageForceSayToggle = new ToggleControl(LocalizationManager.GetString("Settings_Patches_NoDamageForceSay"));
        noDamageForceSayToggle.Value = CerberusConfig.Settings.DamageForcePatch;
        noDamageForceSayToggle.ValueChanged += v => CerberusConfig.Settings.DamageForcePatch = v;
        container.AddChild(noDamageForceSayToggle);

        var antiScreenGrubToggle = new ToggleControl(LocalizationManager.GetString("Settings_Patches_AntiScreenGrub"));
        antiScreenGrubToggle.Value = CerberusConfig.Settings.AntiScreenGrubPatch;
        antiScreenGrubToggle.ValueChanged += v => CerberusConfig.Settings.AntiScreenGrubPatch = v;
        container.AddChild(antiScreenGrubToggle);

        parent.AddChild(container);
    }

    private void HudIconToggle(Control parent, string labelName, ref bool value, string iconName)
    {
        if (!_componentManager.ComponentExists(iconName))
            return;

        var toggle = new ToggleControl(labelName);
        toggle.Value = value;
        Action<bool> handler = v =>
        {
            if (v)
            {
                _componentManager.AddComponent(iconName, null);
            }
            else
            {
                _componentManager.RemoveComponent(iconName, null);
            }
        };
        toggle.ValueChanged += handler;
        parent.AddChild(toggle);
    }

    private class TabInfo
    {
        public string Name { get; }
        public List<string> SubTabs { get; }
        public Action RenderAction { get; }

        public TabInfo(string name, List<string> subTabs, Action renderAction)
        {
            Name = name;
            SubTabs = subTabs;
            RenderAction = renderAction;
        }
    }

    private enum ConnectStatus
    {
        Join,
        Leave
    }
}

