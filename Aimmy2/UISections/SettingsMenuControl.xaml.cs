using Aimmy2.AILogic;
using Dictionary = Aimmy2.Class.Dictionary;
using Aimmy2.Class;
using Aimmy2.MouseMovementLibraries.GHubSupport;
using Aimmy2.UILibrary;
using MouseMovementLibraries.ddxoftSupport;
using MouseMovementLibraries.MakcuSupport;
using MouseMovementLibraries.RazerSupport;
using Other;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using UILibrary;
using Visuality;
using LogLevel = Other.LogManager.LogLevel;

namespace Aimmy2.Controls
{
    public partial class SettingsMenuControl : UserControl
    {
        private MainWindow? _mainWindow;
        private bool _isInitialized;

        // Local minimize state management
        private readonly Dictionary<string, bool> _localMinimizeState = new()
        {
            { "Settings Menu", false },
            { "X/Y Percentage Adjustment", false },
            { "Theme Settings", false },
            { "Display Settings", false }
        };

        // Keep local refs for Makcu controls
        private ADropdown? _makcuPortDropdown;
        private APButton? _makcuRefreshButton;

        // === Needed by MainWindow ===
        public ScrollViewer SettingsMenuScrollViewer => SettingsMenu;

        public SettingsMenuControl()
        {
            InitializeComponent();
        }

        public void Initialize(MainWindow mainWindow)
        {
            if (_isInitialized) return;

            _mainWindow = mainWindow;
            _isInitialized = true;

            LoadMinimizeStatesFromGlobal();
            LoadSettingsConfig();
            LoadXYPercentageMenu();
            LoadThemeMenu();
            LoadDisplaySelectMenu();
            ApplyMinimizeStates();

            DisplayManager.DisplayChanged += OnDisplayChanged;
        }

        // ---------------- Dispose (called by MainWindow on shutdown) ----------------
        public void Dispose()
        {
            DisplayManager.DisplayChanged -= OnDisplayChanged;
            _mainWindow?.uiManager.DisplaySelector?.Dispose();
            SaveMinimizeStatesToGlobal();
        }

        // ---------------- Minimize helpers ----------------
        private void LoadMinimizeStatesFromGlobal()
        {
            foreach (var key in _localMinimizeState.Keys.ToList())
            {
                if (Dictionary.minimizeState.ContainsKey(key))
                    _localMinimizeState[key] = Dictionary.minimizeState[key];
            }
        }

        private void SaveMinimizeStatesToGlobal()
        {
            foreach (var kvp in _localMinimizeState)
                Dictionary.minimizeState[kvp.Key] = kvp.Value;
        }

        private void ApplyMinimizeStates()
        {
            ApplyPanelState("Settings Menu", SettingsConfig);
            ApplyPanelState("X/Y Percentage Adjustment", XYPercentageEnablerMenu);
            ApplyPanelState("Theme Settings", ThemeMenu);
            ApplyPanelState("Display Settings", DisplaySelectMenu);
        }

        private void ApplyPanelState(string stateName, StackPanel panel)
        {
            if (_localMinimizeState.TryGetValue(stateName, out bool isMinimized))
                SetPanelVisibility(panel, !isMinimized);
        }

        private void SetPanelVisibility(StackPanel panel, bool isVisible)
        {
            foreach (UIElement child in panel.Children)
            {
                bool keepVisible = child is ATitle || child is ASpacer || child is ARectangleBottom;
                child.Visibility = keepVisible ? Visibility.Visible : (isVisible ? Visibility.Visible : Visibility.Collapsed);
            }
        }

        private void TogglePanel(string stateName, StackPanel panel)
        {
            if (!_localMinimizeState.ContainsKey(stateName)) return;

            _localMinimizeState[stateName] = !_localMinimizeState[stateName];
            SetPanelVisibility(panel, !_localMinimizeState[stateName]);
            SaveMinimizeStatesToGlobal();
        }

        // ---------------- Menu Section Loaders ----------------
        private void LoadSettingsConfig()
        {
            var uiManager = _mainWindow!.uiManager;
            var builder = new SectionBuilder(this, SettingsConfig);

            builder
                .AddTitle("Settings Menu", true, t =>
                {
                    uiManager.AT_SettingsMenu = t;
                    t.Minimize.Click += (s, e) => TogglePanel("Settings Menu", SettingsConfig);
                })
                .AddToggle("Collect Data While Playing", t => uiManager.T_CollectDataWhilePlaying = t)
                .AddToggle("Auto Label Data", t => uiManager.T_AutoLabelData = t)
                .AddDropdown("Mouse Movement Method", d =>
                {
                    uiManager.D_MouseMovementMethod = d;
                    d.DropdownBox.SelectedIndex = -1;

                    _mainWindow.AddDropdownItem(d, "Mouse Event");
                    _mainWindow.AddDropdownItem(d, "SendInput");
                    uiManager.DDI_LGHUB = _mainWindow.AddDropdownItem(d, "LG HUB");
                    uiManager.DDI_RazerSynapse = _mainWindow.AddDropdownItem(d, "Razer Synapse (Require Razer Peripheral)");
                    uiManager.DDI_ddxoft = _mainWindow.AddDropdownItem(d, "ddxoft Virtual Input Driver");
                    uiManager.DDI_MAKCU = _mainWindow.AddDropdownItem(d, "MAKCU");

                    // LG Hub
                    uiManager.DDI_LGHUB.Selected += async (s, e) =>
                    {
                        if (!new LGHubMain().Load())
                            await ResetToMouseEvent();
                    };

                    // Razer
                    uiManager.DDI_RazerSynapse.Selected += async (s, e) =>
                    {
                        MakcuMain.Unload();
                        if (!await RZMouse.Load())
                            await ResetToMouseEvent();
                    };

                    // ddxoft
                    uiManager.DDI_ddxoft.Selected += async (s, e) =>
                    {
                        MakcuMain.Unload();
                        if (!await DdxoftMain.Load())
                            await ResetToMouseEvent();
                    };

                    // MAKCU
                    uiManager.DDI_MAKCU.Selected += async (s, e) =>
                    {
                        ShowMakcuControls();
                        RefreshMakcuPorts();

                        if (_makcuPortDropdown!.DropdownBox.Items.Count == 0)
                        {
                            // Auto-detect fallback
                            if (!await MakcuMain.Load())
                                await ResetToMouseEvent();
                        }
                        else
                        {
                            _makcuPortDropdown.DropdownBox.SelectedIndex = 0;
                        }
                    };

                    uiManager.DDI_MAKCU.Unselected += (s, e) =>
                    {
                        MakcuMain.DisposeInstance();
                        HideMakcuControls();
                    };
                })
                .AddDropdown("Makcu COM Port", d =>
                {
                    _makcuPortDropdown = d;
                    d.Visibility = Visibility.Collapsed;
                    d.DropdownBox.SelectionChanged += async (s, e) =>
                    {
                        if (d.DropdownBox.SelectedItem is ComboBoxItem item)
                        {
                            var port = item.Content?.ToString();
                            if (!string.IsNullOrEmpty(port))
                            {
                                MakcuMain.Unload();
                                if (!await MakcuMain.Load(port, 4000000))
                                    await ResetToMouseEvent();
                            }
                        }
                    };
                })
                .AddButton("Refresh Makcu Ports", b =>
                {
                    _makcuRefreshButton = b;
                    b.Visibility = Visibility.Collapsed;
                    b.Reader.Click += (s, e) => RefreshMakcuPorts();
                })
                .AddDropdown("Screen Capture Method", d =>
                {
                    uiManager.D_ScreenCaptureMethod = d;
                    d.DropdownBox.SelectedIndex = -1;
                    _mainWindow.AddDropdownItem(d, "DirectX");
                    _mainWindow.AddDropdownItem(d, "GDI+");
                })
                .AddDropdown("Image Size", d =>
                {
                    uiManager.D_ImageSize = d;
                    _mainWindow.AddDropdownItem(d, "640");
                    _mainWindow.AddDropdownItem(d, "512");
                    _mainWindow.AddDropdownItem(d, "416");
                    _mainWindow.AddDropdownItem(d, "320");
                    _mainWindow.AddDropdownItem(d, "256");
                    _mainWindow.AddDropdownItem(d, "160");

                    var currentSize = Dictionary.dropdownState["Image Size"];
                    for (int i = 0; i < d.DropdownBox.Items.Count; i++)
                    {
                        if ((d.DropdownBox.Items[i] as ComboBoxItem)?.Content?.ToString() == currentSize)
                        {
                            d.DropdownBox.SelectedIndex = i;
                            break;
                        }
                    }

                    d.DropdownBox.SelectionChanged += (s, e) =>
                    {
                        if (d.DropdownBox.SelectedItem is ComboBoxItem item)
                        {
                            var newSize = item.Content?.ToString();
                            if (!string.IsNullOrEmpty(newSize))
                                Dictionary.dropdownState["Image Size"] = newSize;
                        }
                    };
                })
                .AddSlider("AI Minimum Confidence", "% Confidence", 1, 1, 1, 100, s =>
                {
                    uiManager.S_AIMinimumConfidence = s;
                })
                .AddToggle("Mouse Background Effect", t => uiManager.T_MouseBackgroundEffect = t)
                .AddToggle("UI TopMost", t => uiManager.T_UITopMost = t)
                .AddToggle("Debug Mode", t => uiManager.T_DebugMode = t)
                .AddToggle("StreamGuard", t => uiManager.T_StreamGuard = t)
                .AddButton("Save Config", b =>
                {
                    uiManager.B_SaveConfig = b;
                    b.Reader.Click += (s, e) => new ConfigSaver().ShowDialog();
                })
                .AddSeparator();
        }

        private void LoadXYPercentageMenu()
        {
            var uiManager = _mainWindow!.uiManager;
            var builder = new SectionBuilder(this, XYPercentageEnablerMenu);

            builder
                .AddTitle("X/Y Percentage Adjustment", true, t =>
                {
                    uiManager.AT_XYPercentageAdjustmentEnabler = t;
                    t.Minimize.Click += (s, e) => TogglePanel("X/Y Percentage Adjustment", XYPercentageEnablerMenu);
                })
                .AddToggle("X Axis Percentage Adjustment", t => uiManager.T_XAxisPercentageAdjustment = t)
                .AddToggle("Y Axis Percentage Adjustment", t => uiManager.T_YAxisPercentageAdjustment = t)
                .AddSeparator();
        }

        private void LoadThemeMenu()
        {
            var uiManager = _mainWindow!.uiManager;
            var builder = new SectionBuilder(this, ThemeMenu);

            builder
                .AddTitle("Theme Settings", true, t =>
                {
                    uiManager.AT_ThemeColorWheel = t;
                    t.Minimize.Click += (s, e) => TogglePanel("Theme Settings", ThemeMenu);
                })
                .AddSeparator();

            uiManager.ThemeColorWheel = new AColorWheel();

            var insertIndex = ThemeMenu.Children.Count - 2;
            ThemeMenu.Children.Insert(insertIndex, uiManager.ThemeColorWheel);
        }

        private void LoadDisplaySelectMenu()
        {
            var uiManager = _mainWindow!.uiManager;
            var builder = new SectionBuilder(this, DisplaySelectMenu);

            builder
                .AddTitle("Display Settings", true, t =>
                {
                    uiManager.AT_DisplaySelector = t;
                    t.Minimize.Click += (s, e) => TogglePanel("Display Settings", DisplaySelectMenu);
                })
                .AddSeparator();

            uiManager.DisplaySelector = new ADisplaySelector();
            uiManager.DisplaySelector.RefreshDisplays();

            var insertIndex = DisplaySelectMenu.Children.Count - 2;
            DisplaySelectMenu.Children.Insert(insertIndex, uiManager.DisplaySelector);

            var refreshButton = new APButton("Refresh Displays");
            refreshButton.Reader.Click += (s, e) =>
            {
                try
                {
                    DisplayManager.RefreshDisplays();
                    uiManager.DisplaySelector.RefreshDisplays();
                    LogManager.Log(LogLevel.Info, "Display list refreshed successfully.", true);
                }
                catch (Exception ex)
                {
                    LogManager.Log(LogLevel.Error, $"Error refreshing displays: {ex.Message}", true);
                }
            };
            DisplaySelectMenu.Children.Insert(insertIndex + 1, refreshButton);
        }

        // ---------------- Display change handling ----------------
        private void OnDisplayChanged(object? sender, DisplayChangedEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    LogManager.Log(LogLevel.Info,
                        $"AI focus switched to Display {e.DisplayIndex + 1} ({e.Bounds.Width}x{e.Bounds.Height})",
                        true);

                    Dictionary.sliderSettings["SelectedDisplay"] = e.DisplayIndex;
                }
                catch { }
            });
        }


        // ---------------- Makcu Helpers ----------------
        private void ShowMakcuControls()
        {
            if (_makcuPortDropdown != null) _makcuPortDropdown.Visibility = Visibility.Visible;
            if (_makcuRefreshButton != null) _makcuRefreshButton.Visibility = Visibility.Visible;
        }

        private void HideMakcuControls()
        {
            if (_makcuPortDropdown != null) _makcuPortDropdown.Visibility = Visibility.Collapsed;
            if (_makcuRefreshButton != null) _makcuRefreshButton.Visibility = Visibility.Collapsed;
        }

        private void RefreshMakcuPorts()
        {
            if (_makcuPortDropdown == null) return;
            _makcuPortDropdown.DropdownBox.Items.Clear();

            foreach (var port in System.IO.Ports.SerialPort.GetPortNames())
                _mainWindow!.AddDropdownItem(_makcuPortDropdown, port);

            if (_makcuPortDropdown.DropdownBox.Items.Count > 0)
                _makcuPortDropdown.DropdownBox.SelectedIndex = 0;
        }

        private async Task ResetToMouseEvent()
        {
            await Task.Delay(500);
            _mainWindow!.uiManager.D_MouseMovementMethod!.DropdownBox.SelectedIndex = 0;
            HideMakcuControls();
        }

        #region Section Builder

        private class SectionBuilder
        {
            private readonly SettingsMenuControl _parent;
            private readonly StackPanel _panel;

            public SectionBuilder(SettingsMenuControl parent, StackPanel panel)
            {
                _parent = parent;
                _panel = panel;
            }

            public SectionBuilder AddTitle(string title, bool canMinimize, Action<ATitle>? configure = null)
            {
                var titleControl = new ATitle(title, canMinimize);
                configure?.Invoke(titleControl);
                _panel.Children.Add(titleControl);
                return this;
            }

            public SectionBuilder AddToggle(string title, Action<AToggle>? configure = null)
            {
                var toggle = new AToggle(title);
                configure?.Invoke(toggle);
                _panel.Children.Add(toggle);
                return this;
            }

            public SectionBuilder AddSlider(string title, string label, double frequency, double buttonSteps,
                double min, double max, Action<ASlider>? configure = null)
            {
                var slider = new ASlider(title, label, buttonSteps)
                {
                    Slider = { Minimum = min, Maximum = max, TickFrequency = frequency }
                };
                configure?.Invoke(slider);
                _panel.Children.Add(slider);
                return this;
            }

            public SectionBuilder AddDropdown(string title, Action<ADropdown>? configure = null)
            {
                var dropdown = new ADropdown(title, title);
                configure?.Invoke(dropdown);
                _panel.Children.Add(dropdown);
                return this;
            }

            public SectionBuilder AddButton(string title, Action<APButton>? configure = null)
            {
                var button = new APButton(title);
                configure?.Invoke(button);
                _panel.Children.Add(button);
                return this;
            }

            public SectionBuilder AddSeparator()
            {
                _panel.Children.Add(new ARectangleBottom());
                _panel.Children.Add(new ASpacer());
                return this;
            }
        }

        public void UpdateImageSizeDropdown(string newSize)
        {
            if (_mainWindow?.uiManager.D_ImageSize != null)
            {
                var dropdown = _mainWindow.uiManager.D_ImageSize;
                for (int i = 0; i < dropdown.DropdownBox.Items.Count; i++)
                {
                    if ((dropdown.DropdownBox.Items[i] as ComboBoxItem)?.Content?.ToString() == newSize)
                    {
                        dropdown.DropdownBox.SelectedIndex = i;
                        break;
                    }
                }
            }
        }


    }



    #endregion
    namespace Aimmy2.Class
    {
        public static class Dictionary
        {
            public static Dictionary<string, double> sliderSettings = new();
            public static Dictionary<string, bool> toggleState = new();
            public static Dictionary<string, string> dropdownState = new();
            public static Dictionary<string, bool> minimizeState = new();
        }
    }
}
