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

        var enabledToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("AimBot_Gun_Enabled")
        };
        enabledToggle.Pressed = CerberusConfig.GunAimBot.Enabled;
        enabledToggle.OnToggled += args => CerberusConfig.GunAimBot.Enabled = args.Pressed;
        container.AddChild(enabledToggle);

        var gunHotkey = new KeyBindInputControl(LocalizationManager.GetString("AimBot_Gun_HotKey"));
        gunHotkey.KeyBind = CerberusConfig.GunAimBot.HotKey;
        gunHotkey.KeyBindChanged += v => CerberusConfig.GunAimBot.HotKey = v;
        container.AddChild(gunHotkey);

        var radiusSliderLabel = new Label
        {
            Text = LocalizationManager.GetString("AimBot_Gun_Radius")
        };
        container.AddChild(radiusSliderLabel);
        var radiusSlider = new Slider
        {
            MinValue = 0f,
            MaxValue = 10f,
            Value = CerberusConfig.GunAimBot.CircleRadius
        };
        radiusSlider.OnValueChanged += _ => CerberusConfig.GunAimBot.CircleRadius = radiusSlider.Value;
        container.AddChild(radiusSlider);

        var priorityComboLabel = new Label
        {
            Text = LocalizationManager.GetString("AimBot_Gun_Priority")
        };
        container.AddChild(priorityComboLabel);
        var priorityCombo = new OptionButton
        {
            Prefix = LocalizationManager.GetString("AimBot_Gun_Priority") + ": "
        };
        var priorityNames = GetTargetPriorityNames();
        for (int i = 0; i < priorityNames.Length; i++)
        {
            priorityCombo.AddItem(priorityNames[i], i);
        }
        priorityCombo.Select((int)CerberusConfig.GunAimBot.TargetPriority);
        priorityCombo.OnItemSelected += args => CerberusConfig.GunAimBot.TargetPriority = args.Id;
        container.AddChild(priorityCombo);

        var onlyPriorityToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("AimBot_OnlyPriority")
        };
        onlyPriorityToggle.Pressed = CerberusConfig.GunAimBot.OnlyPriority;
        onlyPriorityToggle.OnToggled += args => CerberusConfig.GunAimBot.OnlyPriority = args.Pressed;
        container.AddChild(onlyPriorityToggle);

        var criticalToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("AimBot_Gun_Critical")
        };
        criticalToggle.Pressed = CerberusConfig.GunAimBot.TargetCritical;
        criticalToggle.OnToggled += args => CerberusConfig.GunAimBot.TargetCritical = args.Pressed;
        container.AddChild(criticalToggle);

        var minSpreadToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("AimBot_Gun_MinimalSpread")
        };
        minSpreadToggle.Pressed = CerberusConfig.GunAimBot.MinSpread;
        minSpreadToggle.OnToggled += args => CerberusConfig.GunAimBot.MinSpread = args.Pressed;
        container.AddChild(minSpreadToggle);

        var hitScanToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("AimBot_Gun_HitScan")
        };
        hitScanToggle.Pressed = CerberusConfig.GunAimBot.HitScan;
        hitScanToggle.OnToggled += args => CerberusConfig.GunAimBot.HitScan = args.Pressed;
        container.AddChild(hitScanToggle);

        var autoPredictToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("AimBot_Gun_AutoPredict")
        };
        autoPredictToggle.Pressed = CerberusConfig.GunAimBot.AutoPredict;
        autoPredictToggle.OnToggled += args => CerberusConfig.GunAimBot.AutoPredict = args.Pressed;
        container.AddChild(autoPredictToggle);

        var predictToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("AimBot_Gun_Predict")
        };
        predictToggle.Pressed = CerberusConfig.GunAimBot.PredictEnabled;
        predictToggle.OnToggled += args => CerberusConfig.GunAimBot.PredictEnabled = args.Pressed;
        container.AddChild(predictToggle);

        var predictCorrectionSliderLabel = new Label
        {
            Text = LocalizationManager.GetString("AimBot_Gun_PredictCorrection")
        };
        container.AddChild(predictCorrectionSliderLabel);
        var predictCorrectionSlider = new Slider
        {
            MinValue = 0f,
            MaxValue = 1000f,
            Value = CerberusConfig.GunAimBot.PredictCorrection
        };
        predictCorrectionSlider.OnValueChanged += _ => CerberusConfig.GunAimBot.PredictCorrection = predictCorrectionSlider.Value;

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

        var circleToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("AimBot_Gun_Circle")
        };
        circleToggle.Pressed = CerberusConfig.GunAimBot.ShowCircle;
        circleToggle.OnToggled += args => CerberusConfig.GunAimBot.ShowCircle = args.Pressed;
        container.AddChild(circleToggle);

        var lineToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("AimBot_Gun_Line")
        };
        lineToggle.Pressed = CerberusConfig.GunAimBot.ShowLine;
        lineToggle.OnToggled += args => CerberusConfig.GunAimBot.ShowLine = args.Pressed;
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

        var enabledHelperToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("AimBot_Gun_EnabledHelper")
        };
        enabledHelperToggle.Pressed = CerberusConfig.GunHelper.Enabled;
        enabledHelperToggle.OnToggled += args => CerberusConfig.GunHelper.Enabled = args.Pressed;
        container.AddChild(enabledHelperToggle);

        var showAmmoToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("AimBot_Gun_ShowAmmo")
        };
        showAmmoToggle.Pressed = CerberusConfig.GunHelper.ShowAmmo;
        showAmmoToggle.OnToggled += args => CerberusConfig.GunHelper.ShowAmmo = args.Pressed;
        container.AddChild(showAmmoToggle);

        var autoBoltToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("AimBot_Gun_AutoBolt")
        };
        autoBoltToggle.Pressed = CerberusConfig.GunHelper.AutoBolt;
        autoBoltToggle.OnToggled += args => CerberusConfig.GunHelper.AutoBolt = args.Pressed;
        container.AddChild(autoBoltToggle);

        var autoReloadToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("AimBot_Gun_AutoReload")
        };
        autoReloadToggle.Pressed = CerberusConfig.GunHelper.AutoReload;
        autoReloadToggle.OnToggled += args => CerberusConfig.GunHelper.AutoReload = args.Pressed;
        container.AddChild(autoReloadToggle);

        var autoReloadDelaySliderLabel = new Label
        {
            Text = LocalizationManager.GetString("AimBot_Gun_AutoReloadDelay")
        };
        container.AddChild(autoReloadDelaySliderLabel);
        var autoReloadDelaySlider = new Slider
        {
            MinValue = 0.01f,
            MaxValue = 0.5f,
            Value = CerberusConfig.GunHelper.AutoReloadDelay
        };
        autoReloadDelaySlider.OnValueChanged += _ => CerberusConfig.GunHelper.AutoReloadDelay = autoReloadDelaySlider.Value;

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

        var enabledToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("AimBot_Melee_Enabled")
        };
        enabledToggle.Pressed = CerberusConfig.MeleeAimBot.Enabled;
        enabledToggle.OnToggled += args => CerberusConfig.MeleeAimBot.Enabled = args.Pressed;
        container.AddChild(enabledToggle);

        var meleeLightHotkey = new KeyBindInputControl(LocalizationManager.GetString("AimBot_Melee_LightHotKey"));
        meleeLightHotkey.KeyBind = CerberusConfig.MeleeAimBot.LightHotKey;
        meleeLightHotkey.KeyBindChanged += v => CerberusConfig.MeleeAimBot.LightHotKey = v;
        container.AddChild(meleeLightHotkey);

        var meleeHeavyHotkey = new KeyBindInputControl(LocalizationManager.GetString("AimBot_Melee_HeavyHotKey"));
        meleeHeavyHotkey.KeyBind = CerberusConfig.MeleeAimBot.HeavyHotKey;
        meleeHeavyHotkey.KeyBindChanged += v => CerberusConfig.MeleeAimBot.HeavyHotKey = v;
        container.AddChild(meleeHeavyHotkey);

        var radiusSliderLabel = new Label
        {
            Text = LocalizationManager.GetString("AimBot_Melee_Radius")
        };
        container.AddChild(radiusSliderLabel);
        var radiusSlider = new Slider
        {
            MinValue = 0f,
            MaxValue = 10f,
            Value = CerberusConfig.MeleeAimBot.CircleRadius
        };
        radiusSlider.OnValueChanged += _ => CerberusConfig.MeleeAimBot.CircleRadius = radiusSlider.Value;

        container.AddChild(radiusSlider);

        var priorityCombo = new ComboControl(LocalizationManager.GetString("AimBot_Melee_Priority"), GetTargetPriorityNames());
        priorityCombo.SelectedIndex = (int)CerberusConfig.MeleeAimBot.TargetPriority;
        priorityCombo.SelectedIndexChanged += index => CerberusConfig.MeleeAimBot.TargetPriority = index;
        container.AddChild(priorityCombo);

        var onlyPriorityToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("AimBot_OnlyPriority")
        };
        onlyPriorityToggle.Pressed = CerberusConfig.MeleeAimBot.OnlyPriority;
        onlyPriorityToggle.OnToggled += args => CerberusConfig.MeleeAimBot.OnlyPriority = args.Pressed;
        container.AddChild(onlyPriorityToggle);

        var criticalToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("AimBot_Melee_Critical")
        };
        criticalToggle.Pressed = CerberusConfig.MeleeAimBot.TargetCritical;
        criticalToggle.OnToggled += args => CerberusConfig.MeleeAimBot.TargetCritical = args.Pressed;
        container.AddChild(criticalToggle);

        var fixNetworkDelayToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("AimBot_Melee_FixNetworkDelay")
        };
        fixNetworkDelayToggle.Pressed = CerberusConfig.MeleeAimBot.FixNetworkDelay;
        fixNetworkDelayToggle.OnToggled += args => 
        {
            CerberusConfig.MeleeAimBot.FixNetworkDelay = args.Pressed;
            UpdateMeleeFixDelayVisibility();
        };
        container.AddChild(fixNetworkDelayToggle);

        var fixDelaySliderLabel = new Label
        {
            Text = LocalizationManager.GetString("AimBot_Melee_FixDelay")
        };
        container.AddChild(fixDelaySliderLabel);
        var fixDelaySlider = new SliderControl("", 0.1f, 2f, CerberusConfig.MeleeAimBot.FixDelay);
        fixDelaySlider.ValueChanged += v => CerberusConfig.MeleeAimBot.FixDelay = v;

        fixDelaySlider.Visible = CerberusConfig.MeleeAimBot.FixNetworkDelay;
        _meleeFixDelaySlider = fixDelaySlider;
        container.AddChild(fixDelaySlider);

        var rotateToTargetToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("AimBot_Melee_RotateToTarget")
        };
        rotateToTargetToggle.Pressed = CerberusConfig.MeleeHelper.RotateToTarget;
        rotateToTargetToggle.OnToggled += args => CerberusConfig.MeleeHelper.RotateToTarget = args.Pressed;
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

        var circleToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("AimBot_Melee_Circle")
        };
        circleToggle.Pressed = CerberusConfig.MeleeAimBot.ShowCircle;
        circleToggle.OnToggled += args => CerberusConfig.MeleeAimBot.ShowCircle = args.Pressed;
        container.AddChild(circleToggle);

        var lineToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("AimBot_Melee_Line")
        };
        lineToggle.Pressed = CerberusConfig.MeleeAimBot.ShowLine;
        lineToggle.OnToggled += args => CerberusConfig.MeleeAimBot.ShowLine = args.Pressed;
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

        var enabledHelperToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("AimBot_Melee_EnabledHelper")
        };
        enabledHelperToggle.Pressed = CerberusConfig.MeleeHelper.Enabled;
        enabledHelperToggle.OnToggled += args => CerberusConfig.MeleeHelper.Enabled = args.Pressed;
        container.AddChild(enabledHelperToggle);

        var attack360Toggle = new CheckBox
        {
            Text = LocalizationManager.GetString("AimBot_Melee_Attack360")
        };
        attack360Toggle.Pressed = CerberusConfig.MeleeHelper.Attack360;
        attack360Toggle.OnToggled += args => CerberusConfig.MeleeHelper.Attack360 = args.Pressed;
        container.AddChild(attack360Toggle);

        var autoAttackToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("AimBot_Melee_AutoAttack")
        };
        autoAttackToggle.Pressed = CerberusConfig.MeleeHelper.AutoAttack;
        autoAttackToggle.OnToggled += args => CerberusConfig.MeleeHelper.AutoAttack = args.Pressed;
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

        var enabledToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("Visuals_ESP_Enabled")
        };
        enabledToggle.Pressed = CerberusConfig.Esp.Enabled;
        enabledToggle.OnToggled += args => CerberusConfig.Esp.Enabled = args.Pressed;
        container.AddChild(enabledToggle);

        var showNameToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("Visuals_ESP_Name")
        };
        showNameToggle.Pressed = CerberusConfig.Esp.ShowName;
        showNameToggle.OnToggled += args => CerberusConfig.Esp.ShowName = args.Pressed;
        container.AddChild(showNameToggle);

        var showCKeyToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("Visuals_ESP_CKey")
        };
        showCKeyToggle.Pressed = CerberusConfig.Esp.ShowCKey;
        showCKeyToggle.OnToggled += args => CerberusConfig.Esp.ShowCKey = args.Pressed;
        container.AddChild(showCKeyToggle);

        var showAntagToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("Visuals_ESP_Antag")
        };
        showAntagToggle.Pressed = CerberusConfig.Esp.ShowAntag;
        showAntagToggle.OnToggled += args => CerberusConfig.Esp.ShowAntag = args.Pressed;
        container.AddChild(showAntagToggle);

        var showFriendToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("Visuals_ESP_Friend")
        };
        showFriendToggle.Pressed = CerberusConfig.Esp.ShowFriend;
        showFriendToggle.OnToggled += args => CerberusConfig.Esp.ShowFriend = args.Pressed;
        container.AddChild(showFriendToggle);

        var showPriorityToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("Visuals_ESP_Priority")
        };
        showPriorityToggle.Pressed = CerberusConfig.Esp.ShowPriority;
        showPriorityToggle.OnToggled += args => CerberusConfig.Esp.ShowPriority = args.Pressed;
        container.AddChild(showPriorityToggle);

        var showCombatModeToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("Visuals_ESP_CombatMode")
        };
        showCombatModeToggle.Pressed = CerberusConfig.Esp.ShowCombatMode;
        showCombatModeToggle.OnToggled += args => CerberusConfig.Esp.ShowCombatMode = args.Pressed;
        container.AddChild(showCombatModeToggle);

        var showImplantsToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("Visuals_ESP_Implants")
        };
        showImplantsToggle.Pressed = CerberusConfig.Esp.ShowImplants;
        showImplantsToggle.OnToggled += args => CerberusConfig.Esp.ShowImplants = args.Pressed;
        container.AddChild(showImplantsToggle);

        var showContrabandToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("Visuals_ESP_Contraband")
        };
        showContrabandToggle.Pressed = CerberusConfig.Esp.ShowContraband;
        showContrabandToggle.OnToggled += args => CerberusConfig.Esp.ShowContraband = args.Pressed;
        container.AddChild(showContrabandToggle);

        var showWeaponToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("ESP_Weapon")
        };
        showWeaponToggle.Pressed = CerberusConfig.Esp.ShowWeapon;
        showWeaponToggle.OnToggled += args => CerberusConfig.Esp.ShowWeapon = args.Pressed;
        container.AddChild(showWeaponToggle);

        var showNoSlipToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("ESP_NoSlip")
        };
        showNoSlipToggle.Pressed = CerberusConfig.Esp.ShowNoSlip;
        showNoSlipToggle.OnToggled += args => CerberusConfig.Esp.ShowNoSlip = args.Pressed;
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

        var fovToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("Visuals_Eye_FOV")
        };
        fovToggle.Pressed = CerberusConfig.Eye.FovEnabled;
        fovToggle.OnToggled += args => CerberusConfig.Eye.FovEnabled = args.Pressed;
        container.AddChild(fovToggle);

        var fovHotkey = new KeyBindInputControl(LocalizationManager.GetString("Visuals_Eye_FOV_HotKey"));
        fovHotkey.KeyBind = CerberusConfig.Eye.FovHotKey;
        fovHotkey.KeyBindChanged += v => CerberusConfig.Eye.FovHotKey = v;
        container.AddChild(fovHotkey);

        var fullBrightToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("Visuals_Eye_FullBright")
        };
        fullBrightToggle.Pressed = CerberusConfig.Eye.FullBrightEnabled;
        fullBrightToggle.OnToggled += args => CerberusConfig.Eye.FullBrightEnabled = args.Pressed;
        container.AddChild(fullBrightToggle);

        var fullBrightHotkey = new KeyBindInputControl(LocalizationManager.GetString("Visuals_Eye_FullBright_HotKey"));
        fullBrightHotkey.KeyBind = CerberusConfig.Eye.FullBrightHotKey;
        fullBrightHotkey.KeyBindChanged += v => CerberusConfig.Eye.FullBrightHotKey = v;
        container.AddChild(fullBrightHotkey);

        var zoomSliderLabel = new Label
        {
            Text = LocalizationManager.GetString("Visuals_Eye_Zoom")
        };
        container.AddChild(zoomSliderLabel);
        var zoomSlider = new Slider
        {
            MinValue = 0.5f,
            MaxValue = 30f,
            Value = CerberusConfig.Eye.Zoom
        };
        zoomSlider.OnValueChanged += _ => CerberusConfig.Eye.Zoom = zoomSlider.Value;

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

        var storageViewerToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("Visuals_Eye_StorageViewer_Enabled")
        };
        storageViewerToggle.Pressed = CerberusConfig.StorageViewer.Enabled;
        storageViewerToggle.OnToggled += args => CerberusConfig.StorageViewer.Enabled = args.Pressed;
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

        var enabledToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("Visuals_Fun_Enabled")
        };
        enabledToggle.Pressed = CerberusConfig.Fun.Enabled;
        enabledToggle.OnToggled += args => CerberusConfig.Fun.Enabled = args.Pressed;
        container.AddChild(enabledToggle);

        var rotationToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("Visuals_Fun_Rotation")
        };
        rotationToggle.Pressed = CerberusConfig.Fun.RotationEnabled;
        rotationToggle.OnToggled += args => CerberusConfig.Fun.RotationEnabled = args.Pressed;
        container.AddChild(rotationToggle);

        var rotationSpeedSliderLabel = new Label
        {
            Text = LocalizationManager.GetString("Visuals_Fun_Speed")
        };
        container.AddChild(rotationSpeedSliderLabel);
        var rotationSpeedSlider = new Slider
        {
            MinValue = 0f,
            MaxValue = 360f,
            Value = CerberusConfig.Fun.RotationSpeed
        };
        rotationSpeedSlider.OnValueChanged += _ => CerberusConfig.Fun.RotationSpeed = rotationSpeedSlider.Value;

        container.AddChild(rotationSpeedSlider);

        var jumpToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("Visuals_Fun_Jump")
        };
        jumpToggle.Pressed = CerberusConfig.Fun.JumpEnabled;
        jumpToggle.OnToggled += args => CerberusConfig.Fun.JumpEnabled = args.Pressed;
        container.AddChild(jumpToggle);

        var shakeToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("Visuals_Fun_Snake")
        };
        shakeToggle.Pressed = CerberusConfig.Fun.ShakeEnabled;
        shakeToggle.OnToggled += args => CerberusConfig.Fun.ShakeEnabled = args.Pressed;
        container.AddChild(shakeToggle);

        var rainbowToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("Visuals_Fun_Rainbow")
        };
        rainbowToggle.Pressed = CerberusConfig.Fun.RainbowEnabled;
        rainbowToggle.OnToggled += args => CerberusConfig.Fun.RainbowEnabled = args.Pressed;
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
        sortCombo.SelectedIndexChanged += index => 
        {
            _selectedSort = index;
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

        var antiSoapToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("Misc_General_AntiSoap")
        };
        antiSoapToggle.Pressed = CerberusConfig.Misc.AntiSoapEnabled;
        antiSoapToggle.OnToggled += args => CerberusConfig.Misc.AntiSoapEnabled = args.Pressed;
        container.AddChild(antiSoapToggle);

        var antiAfkToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("Misc_General_AntiAFK")
        };
        antiAfkToggle.Pressed = CerberusConfig.Misc.AntiAfkEnabled;
        antiAfkToggle.OnToggled += args => CerberusConfig.Misc.AntiAfkEnabled = args.Pressed;
        container.AddChild(antiAfkToggle);

        var showExplosionsToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("Misc_General_ShowExplosions")
        };
        showExplosionsToggle.Pressed = CerberusConfig.Misc.ShowExplosive;
        showExplosionsToggle.OnToggled += args => CerberusConfig.Misc.ShowExplosive = args.Pressed;
        container.AddChild(showExplosionsToggle);

        var showTrajectoryToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("Misc_General_ShowTrajectory")
        };
        showTrajectoryToggle.Pressed = CerberusConfig.Misc.ShowTrajectory;
        showTrajectoryToggle.OnToggled += args => CerberusConfig.Misc.ShowTrajectory = args.Pressed;
        container.AddChild(showTrajectoryToggle);

        var damageOverlayToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("Misc_General_DamageOverlay")
        };
        damageOverlayToggle.Pressed = CerberusConfig.Misc.DamageOverlayEnabled;
        damageOverlayToggle.OnToggled += args => CerberusConfig.Misc.DamageOverlayEnabled = args.Pressed;
        container.AddChild(damageOverlayToggle);

        var antiAimToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("Misc_General_AntiAim")
        };
        antiAimToggle.Pressed = CerberusConfig.Misc.AntiAimEnabled;
        antiAimToggle.OnToggled += args => CerberusConfig.Misc.AntiAimEnabled = args.Pressed;
        container.AddChild(antiAimToggle);

        var speedSliderLabel = new Label
        {
            Text = LocalizationManager.GetString("Misc_General_Speed")
        };
        container.AddChild(speedSliderLabel);
        var speedSlider = new Slider
        {
            MinValue = 180f,
            MaxValue = 3600f,
            Value = CerberusConfig.Misc.AutoRotateSpeed
        };
        speedSlider.OnValueChanged += _ => CerberusConfig.Misc.AutoRotateSpeed = speedSlider.Value;

        container.AddChild(speedSlider);

        var trashTalkToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("Misc_General_TrashTalk")
        };
        trashTalkToggle.Pressed = CerberusConfig.Misc.TrashTalkEnabled;
        trashTalkToggle.OnToggled += args => CerberusConfig.Misc.TrashTalkEnabled = args.Pressed;
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

        var protectWordToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("Misc_Spammer_Settings_ProtectWord")
        };
        protectWordToggle.Pressed = CerberusConfig.Spammer.ProtectTextEnabled;
        protectWordToggle.OnToggled += args => CerberusConfig.Spammer.ProtectTextEnabled = args.Pressed;
        container.AddChild(protectWordToggle);

        var randomLengthToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("Misc_Spammer_Settings_RandomLength")
        };
        randomLengthToggle.Pressed = CerberusConfig.Spammer.ProtectRandomLength;
        randomLengthToggle.OnToggled += args => CerberusConfig.Spammer.ProtectRandomLength = args.Pressed;
        container.AddChild(randomLengthToggle);

        var lengthSliderLabel = new Label
        {
            Text = LocalizationManager.GetString("Misc_Spammer_Settings_Length")
        };
        container.AddChild(lengthSliderLabel);
        var lengthSlider = new Slider
        {
            MinValue = 1,
            MaxValue = 12,
            Value = CerberusConfig.Spammer.ProtectLength
        };
        lengthSlider.OnValueChanged += _ => 
        {
            CerberusConfig.Spammer.ProtectLength = (int)lengthSlider.Value;
        };
        lengthSlider.Visible = !CerberusConfig.Spammer.ProtectRandomLength;
        randomLengthToggle.OnToggled += args => lengthSlider.Visible = !args.Pressed;
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
        var toggle = new CheckBox
        {
            Text = label
        };
        toggle.Pressed = isEnabled;
        toggle.OnToggled += args =>
        {
            if (args.Pressed)
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

        var showMenuToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("Settings_ShowMenu")
        };
        showMenuToggle.Pressed = CerberusConfig.Settings.ShowMenu;
        showMenuToggle.OnToggled += args => CerberusConfig.Settings.ShowMenu = args.Pressed;
        container.AddChild(showMenuToggle);

        var showMenuHotkey = new KeyBindInputControl(LocalizationManager.GetString("Settings_ShowMenuHotKey"));
        showMenuHotkey.KeyBind = CerberusConfig.Settings.ShowMenuHotKey;
        showMenuHotkey.KeyBindChanged += v => CerberusConfig.Settings.ShowMenuHotKey = v;
        container.AddChild(showMenuHotkey);

        var uiCustomizableToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("Settings_UICustomizable")
        };
        uiCustomizableToggle.Pressed = CerberusConfig.Settings.UiCustomizable;
        uiCustomizableToggle.OnToggled += args => CerberusConfig.Settings.UiCustomizable = args.Pressed;
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

        var fontIntervalSliderLabel = new Label
        {
            Text = LocalizationManager.GetString("Visuals_ESP_Font_Interval")
        };
        container.AddChild(fontIntervalSliderLabel);
        var fontIntervalSlider = new Slider
        {
            MinValue = 1,
            MaxValue = 50,
            Value = CerberusConfig.Esp.FontInterval
        };
        fontIntervalSlider.OnValueChanged += _ => CerberusConfig.Esp.FontInterval = (int)fontIntervalSlider.Value;
        container.AddChild(fontIntervalSlider);

        var fonts = new[]
        {
            "Boxfont Round",
            "NotoSans Regular",
            "NotoSans Bold",
            "NotoSans Italic"
        };

        var mainFontComboLabel = new Label
        {
            Text = LocalizationManager.GetString("Visuals_ESP_Font_MainFont")
        };
        container.AddChild(mainFontComboLabel);
        var mainFontCombo = new OptionButton
        {
            Prefix = LocalizationManager.GetString("Visuals_ESP_Font_MainFont") + ": "
        };
        var mainFontComboItems = fonts;
        for (int i = 0; i < mainFontComboItems.Length; i++)
        {
            mainFontCombo.AddItem(mainFontComboItems[i], i);
        }
        mainFontCombo.Select((int)CerberusConfig.Esp.MainFontIndex);
        mainFontCombo.OnItemSelected += args =>
        {
            CerberusConfig.Esp.MainFontIndex = args.Id;
            CerberusConfig.Esp.MainFontPath = args.Id < fonts.Length ? $"/Fonts/{fonts[args.Id]}/{fonts[args.Id]}.ttf" : "/Fonts/Boxfont-round/Boxfont Round.ttf";
        };
        container.AddChild(mainFontCombo);

        var mainFontSizeSliderLabel = new Label
        {
            Text = LocalizationManager.GetString("Visuals_ESP_Font_Size")
        };
        container.AddChild(mainFontSizeSliderLabel);
        var mainFontSizeSlider = new Slider
        {
            MinValue = 6,
            MaxValue = 30,
            Value = CerberusConfig.Esp.MainFontSize
        };
        mainFontSizeSlider.OnValueChanged += _ => CerberusConfig.Esp.MainFontSize = (int)mainFontSizeSlider.Value;
        container.AddChild(mainFontSizeSlider);

        var otherFontComboLabel = new Label
        {
            Text = LocalizationManager.GetString("Visuals_ESP_Font_OtherFont")
        };
        container.AddChild(otherFontComboLabel);
        var otherFontCombo = new OptionButton
        {
            Prefix = LocalizationManager.GetString("Visuals_ESP_Font_OtherFont") + ": "
        };
        var otherFontComboItems = fonts;
        for (int i = 0; i < otherFontComboItems.Length; i++)
        {
            otherFontCombo.AddItem(otherFontComboItems[i], i);
        }
        otherFontCombo.Select((int)CerberusConfig.Esp.OtherFontIndex);
        otherFontCombo.OnItemSelected += args =>
        {
            CerberusConfig.Esp.OtherFontIndex = args.Id;
            CerberusConfig.Esp.OtherFontPath = args.Id < fonts.Length ? $"/Fonts/{fonts[args.Id]}/{fonts[args.Id]}.ttf" : "/Fonts/Boxfont-round/Boxfont Round.ttf";
        };
        container.AddChild(otherFontCombo);

        var otherFontSizeSliderLabel = new Label
        {
            Text = LocalizationManager.GetString("Visuals_ESP_Font_Size_Other")
        };
        container.AddChild(otherFontSizeSliderLabel);
        var otherFontSizeSlider = new Slider
        {
            MinValue = 6,
            MaxValue = 30,
            Value = CerberusConfig.Esp.OtherFontSize
        };
        otherFontSizeSlider.OnValueChanged += _ => CerberusConfig.Esp.OtherFontSize = (int)otherFontSizeSlider.Value;
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

        var showHealthToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("Visuals_Eye_HUD_Health")
        };
        showHealthToggle.Pressed = CerberusConfig.Hud.ShowHealth;
        showHealthToggle.OnToggled += args => CerberusConfig.Hud.ShowHealth = args.Pressed;
        container.AddChild(showHealthToggle);

        var showStaminaToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("Visuals_Eye_HUD_Stamina")
        };
        showStaminaToggle.Pressed = CerberusConfig.Hud.ShowStamina;
        showStaminaToggle.OnToggled += args => CerberusConfig.Hud.ShowStamina = args.Pressed;
        container.AddChild(showStaminaToggle);

        var staminaColor = new ColorPickerControl(LocalizationManager.GetString("Visuals_Eye_HUD_Color"));
        staminaColor.Color = CerberusConfig.Hud.StaminaColor;
        staminaColor.ColorChanged += v => CerberusConfig.Hud.StaminaColor = v;
        container.AddChild(staminaColor);

        HudIconToggle(container, LocalizationManager.GetString("Visuals_Eye_HUD_AntagIcons"), () => CerberusConfig.Hud.ShowAntag, v => CerberusConfig.Hud.ShowAntag = v, "ShowAntagIcons");
        HudIconToggle(container, LocalizationManager.GetString("Visuals_Eye_HUD_JobIcons"), () => CerberusConfig.Hud.ShowJobIcons, v => CerberusConfig.Hud.ShowJobIcons = v, "ShowJobIcons");
        HudIconToggle(container, LocalizationManager.GetString("Visuals_Eye_HUD_MindShieldIcons"), () => CerberusConfig.Hud.ShowMindShieldIcons, v => CerberusConfig.Hud.ShowMindShieldIcons = v, "ShowMindShieldIcons");
        HudIconToggle(container, LocalizationManager.GetString("Visuals_Eye_HUD_CriminalRecordIcons"), () => CerberusConfig.Hud.ShowCriminalRecordIcons, v => CerberusConfig.Hud.ShowCriminalRecordIcons = v, "ShowCriminalRecordIcons");
        HudIconToggle(container, LocalizationManager.GetString("Visuals_Eye_HUD_SyndicateIcons"), () => CerberusConfig.Hud.ShowSyndicateIcons, v => CerberusConfig.Hud.ShowSyndicateIcons = v, "ShowSyndicateIcons");
        HudIconToggle(container, LocalizationManager.GetString("Visuals_Eye_HUD_ChemicalAnalysis"), () => CerberusConfig.Hud.ChemicalAnalysis, v => CerberusConfig.Hud.ChemicalAnalysis = v, "SolutionScanner");
        HudIconToggle(container, LocalizationManager.GetString("Visuals_Eye_HUD_ShowElectrocution"), () => CerberusConfig.Hud.ShowElectrocution, v => CerberusConfig.Hud.ShowElectrocution = v, "ShowElectrocutionHUD");

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

        var noClydeToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("Visuals_Eye_Patches_NoClyde")
        };
        noClydeToggle.Pressed = CerberusConfig.Settings.ClydePatch;
        noClydeToggle.OnToggled += args => CerberusConfig.Settings.ClydePatch = args.Pressed;
        container.AddChild(noClydeToggle);

        var noSmokeToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("Visuals_Eye_Patches_NoSmoke")
        };
        noSmokeToggle.Pressed = CerberusConfig.Settings.SmokePatch;
        noSmokeToggle.OnToggled += args => CerberusConfig.Settings.SmokePatch = args.Pressed;
        container.AddChild(noSmokeToggle);

        var noBadOverlaysToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("Visuals_Eye_Patches_NoBadOverlays")
        };
        noBadOverlaysToggle.Pressed = CerberusConfig.Settings.OverlaysPatch;
        noBadOverlaysToggle.OnToggled += args => CerberusConfig.Settings.OverlaysPatch = args.Pressed;
        container.AddChild(noBadOverlaysToggle);

        var noCameraRecoilToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("Visuals_Eye_Patches_NoCameraRecoil")
        };
        noCameraRecoilToggle.Pressed = CerberusConfig.Settings.NoCameraKickPatch;
        noCameraRecoilToggle.OnToggled += args => CerberusConfig.Settings.NoCameraKickPatch = args.Pressed;
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

        var affectPlayerToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("Visuals_Fun_Filters_Player")
        };
        affectPlayerToggle.Pressed = CerberusConfig.Fun.AffectPlayer;
        affectPlayerToggle.OnToggled += args => CerberusConfig.Fun.AffectPlayer = args.Pressed;
        container.AddChild(affectPlayerToggle);

        var affectMobsToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("Visuals_Fun_Filters_Mobs")
        };
        affectMobsToggle.Pressed = CerberusConfig.Fun.AffectMobs;
        affectMobsToggle.OnToggled += args => CerberusConfig.Fun.AffectMobs = args.Pressed;
        container.AddChild(affectMobsToggle);

        var affectOthersToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("Visuals_Fun_Filters_Others")
        };
        affectOthersToggle.Pressed = CerberusConfig.Fun.AffectOthers;
        affectOthersToggle.OnToggled += args => CerberusConfig.Fun.AffectOthers = args.Pressed;
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

        var textureEnabledToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("Visuals_Fun_TextureOverlay_Enabled")
        };
        textureEnabledToggle.Pressed = CerberusConfig.Texture.Enabled;
        textureEnabledToggle.OnToggled += args => CerberusConfig.Texture.Enabled = args.Pressed;
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

        var sizeSliderLabel = new Label
        {
            Text = LocalizationManager.GetString("Visuals_Fun_TextureOverlay_Size")
        };
        container.AddChild(sizeSliderLabel);
        var sizeSlider = new Slider
        {
            MinValue = 0.1f,
            MaxValue = 5f,
            Value = CerberusConfig.Texture.Size
        };
        sizeSlider.OnValueChanged += _ => CerberusConfig.Texture.Size = sizeSlider.Value;

        container.AddChild(sizeSlider);

        var invisibleToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("Visuals_Fun_TextureOverlay_Invisible")
        };
        invisibleToggle.Pressed = CerberusConfig.Texture.MakeEntitiesInvisible;
        invisibleToggle.OnToggled += args => CerberusConfig.Texture.MakeEntitiesInvisible = args.Pressed;
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

        var translateChatToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("Misc_General_Translator_TranslateChat")
        };
        translateChatToggle.Pressed = CerberusConfig.Settings.TranslateChatPatch;
        translateChatToggle.OnToggled += args => CerberusConfig.Settings.TranslateChatPatch = args.Pressed;
        container.AddChild(translateChatToggle);

        var languageInput = new LineEdit
        {
            PlaceHolder = LocalizationManager.GetString("Misc_General_Translator_LanguageChat"),
            Text = CerberusConfig.Settings.TranslateChatLang,
            MinHeight = 30,
            Visible = CerberusConfig.Settings.TranslateChatPatch
        };
        languageInput.OnTextChanged += args => CerberusConfig.Settings.TranslateChatLang = args.Text;
        translateChatToggle.OnToggled += args => languageInput.Visible = args.Pressed;
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

        var enabledToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("Misc_General_SearchingItems_Enabled")
        };
        enabledToggle.Pressed = CerberusConfig.Misc.ItemSearcherEnabled;
        enabledToggle.OnToggled += args => CerberusConfig.Misc.ItemSearcherEnabled = args.Pressed;
        container.AddChild(enabledToggle);

        var showNameToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("Misc_General_SearchingItems_ShowName")
        };
        showNameToggle.Pressed = CerberusConfig.Misc.ItemSearcherShowName;
        showNameToggle.OnToggled += args => CerberusConfig.Misc.ItemSearcherShowName = args.Pressed;
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

        var enabledToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("Misc_Spammer_Chat_Enabled")
        };
        enabledToggle.Pressed = CerberusConfig.Spammer.ChatEnabled;
        enabledToggle.OnToggled += args =>
        {
            CerberusConfig.Spammer.ChatEnabled = args.Pressed;
            if (args.Pressed && _spammerSystem != null)
            {
                _spammerSystem.StartSpamChat();
            }
        };
        container.AddChild(enabledToggle);

        var delaySliderLabel = new Label
        {
            Text = LocalizationManager.GetString("Misc_Spammer_Chat_Delay")
        };
        container.AddChild(delaySliderLabel);
        var delaySlider = new Slider
        {
            MinValue = 10,
            MaxValue = 1000,
            Value = CerberusConfig.Spammer.ChatDelay
        };
        delaySlider.OnValueChanged += _ => CerberusConfig.Spammer.ChatDelay = (int)delaySlider.Value;
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

        var enabledToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("Misc_Spammer_AHelp_Enabled")
        };
        enabledToggle.Pressed = CerberusConfig.Spammer.AHelpEnabled;
        enabledToggle.OnToggled += args =>
        {
            CerberusConfig.Spammer.AHelpEnabled = args.Pressed;
            if (args.Pressed && _spammerSystem != null)
            {
                _spammerSystem.StartSpamAHelp();
            }
        };
        container.AddChild(enabledToggle);

        var delaySliderLabel = new Label
        {
            Text = LocalizationManager.GetString("Misc_Spammer_AHelp_Delay")
        };
        container.AddChild(delaySliderLabel);
        var delaySlider = new Slider
        {
            MinValue = 10,
            MaxValue = 1000,
            Value = CerberusConfig.Spammer.AHelpDelay
        };
        delaySlider.OnValueChanged += _ => CerberusConfig.Spammer.AHelpDelay = (int)delaySlider.Value;
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

        var adminPrivilegeToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("Settings_Patches_AdminPrivilege")
        };
        adminPrivilegeToggle.Pressed = CerberusConfig.Settings.AdminPatch;
        adminPrivilegeToggle.OnToggled += args => CerberusConfig.Settings.AdminPatch = args.Pressed;
        container.AddChild(adminPrivilegeToggle);

        var noDamageFriendToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("Settings_Patches_NoDamageFriend")
        };
        noDamageFriendToggle.Pressed = CerberusConfig.Settings.NoDmgFriendPatch;
        noDamageFriendToggle.OnToggled += args => CerberusConfig.Settings.NoDmgFriendPatch = args.Pressed;
        container.AddChild(noDamageFriendToggle);

        var noDamageForceSayToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("Settings_Patches_NoDamageForceSay")
        };
        noDamageForceSayToggle.Pressed = CerberusConfig.Settings.DamageForcePatch;
        noDamageForceSayToggle.OnToggled += args => CerberusConfig.Settings.DamageForcePatch = args.Pressed;
        container.AddChild(noDamageForceSayToggle);

        var antiScreenGrubToggle = new CheckBox
        {
            Text = LocalizationManager.GetString("Settings_Patches_AntiScreenGrub")
        };
        antiScreenGrubToggle.Pressed = CerberusConfig.Settings.AntiScreenGrubPatch;
        antiScreenGrubToggle.OnToggled += args => CerberusConfig.Settings.AntiScreenGrubPatch = args.Pressed;
        container.AddChild(antiScreenGrubToggle);

        parent.AddChild(container);
    }

    private void HudIconToggle(Control parent, string labelName, Func<bool> getValue, Action<bool> setValue, string iconName)
    {
        if (!_componentManager.ComponentExists(iconName))
            return;

        var toggle = new CheckBox
        {
            Text = labelName
        };
        toggle.Pressed = getValue();
        toggle.OnToggled += args =>
        {
            setValue(args.Pressed);
            if (args.Pressed)
            {
                _componentManager.AddComponent(iconName, null);
            }
            else
            {
                _componentManager.RemoveComponent(iconName, null);
            }
        };
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

