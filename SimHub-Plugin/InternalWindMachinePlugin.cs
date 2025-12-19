using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using System.Diagnostics;
using System.Reflection;
using System.Linq;
using System.Windows.Documents;
using System.Net;
using System.Threading.Tasks;
using SimHub.Plugins;
using GameReaderCommon;
using Newtonsoft.Json;

namespace InternalWindMachinePlugin
{
    [PluginDescription("Writes wind percentage values from SimHub to .sensor files for the Internal Wind Machine.")]
    [PluginAuthor("The Real Karle")]
    [PluginName("Internal Wind Machine Plugin")]
    public class InternalWindMachinePlugin : IPlugin, IDataPlugin, IWPFSettings
    {
        public InternalWindMachineSettings Settings { get; set; } = new InternalWindMachineSettings();
        public PluginManager PluginManager { get; set; }
        public static readonly string PluginVersion = "2.0.0";

        public System.Drawing.Image Icon => GraphicsHelper.LoadIcon();

        private double _lastCenter, _lastLeft, _lastRight;
        private ProgressBar _pbCenter, _pbLeft, _pbRight;
        private TextBlock _txtCenter, _txtLeft, _txtRight;
        private Border _metL, _metC, _metR; // Meter containers for graying out
        
        private CheckBox _cb3D, _cbL, _cbC, _cbR;
        private CheckBox _cbOverL, _cbOverC, _cbOverR;
        private Slider _slL, _slC, _slR;
        private TextBlock _txtValL, _txtValC, _txtValR;
        private Button _btnUpdate;
        private TextBox _tbDir, _propC, _propL, _propR;
        private CheckBox _cbUpdate, _cbNotify;
        
        private DispatcherTimer _uiTimer;
        private int _resetAllState = 0;
        private int _resetPropsState = 0;

        private string _remoteUrl;

        private void CheckForUpdateAsync()
        {
            if (!Settings.EnableUpdateChecks) return;

            Task.Run(() => {
                try {
                    using (var wc = new WebClient()) {
                        wc.Headers.Add("user-agent", "SimHub-InternalWindMachine-Plugin");
                        string json = wc.DownloadString("https://raw.githubusercontent.com/therealkarle/InternalWindMachine/main/version.json");
                        var data = JsonConvert.DeserializeObject<dynamic>(json);
                        string remoteVersion = (string)data.plugin;
                        _remoteUrl = (string)data.plugin_url;

                        if (remoteVersion != null && remoteVersion != PluginVersion) {
                            Application.Current.Dispatcher.Invoke(() => {
                                if (_btnUpdate != null) {
                                    _btnUpdate.Content = $"INSTALL UPDATE (v{remoteVersion})";
                                    _btnUpdate.Background = Brushes.DarkGreen;
                                    _btnUpdate.Visibility = Visibility.Visible;
                                }

                                // Show Windows Notification
                                if (Settings.EnableUpdateNotifications) {
                                    try {
                                        var tray = new System.Windows.Forms.NotifyIcon() {
                                            Icon = System.Drawing.SystemIcons.Information,
                                            Visible = true,
                                            BalloonTipTitle = "Internal Wind Machine",
                                            BalloonTipText = $"A new update (v{remoteVersion}) is available! Open SimHub settings to install."
                                        };
                                        tray.ShowBalloonTip(5000);
                                        Task.Delay(10000).ContinueWith(_ => { tray.Dispose(); });
                                    } catch { }
                                }
                            });
                        }
                    }
                } catch { }
            });
        }

        private void InstallUpdate()
        {
            if (string.IsNullOrEmpty(_remoteUrl)) return;

            var result = MessageBox.Show(
                "Do you want to download and install the update now?\n\n" +
                "Note: The update will be prepared and activated on the next SimHub restart.",
                "Internal Wind Machine Update", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            Task.Run(() => {
                try {
                    string currentDllPath = Assembly.GetExecutingAssembly().Location;
                    string parentFolder = Path.GetDirectoryName(currentDllPath);
                    string fileName = Path.GetFileName(currentDllPath);
                    string newDllPath = currentDllPath + ".new";
                    string oldDllPath = currentDllPath + ".old";

                    using (var wc = new WebClient()) {
                        wc.Headers.Add("user-agent", "SimHub-InternalWindMachine-Plugin");
                        wc.DownloadFile(_remoteUrl, newDllPath);
                    }

                    if (File.Exists(newDllPath)) {
                        // The Rename Dance
                        if (File.Exists(oldDllPath)) File.Delete(oldDllPath);
                        File.Move(currentDllPath, oldDllPath);
                        File.Move(newDllPath, currentDllPath);

                        Application.Current.Dispatcher.Invoke(() => {
                            MessageBox.Show(
                                "Update successfully prepared!\n\n" +
                                "Please restart SimHub to activate the new version.",
                                "Update Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                            if (_btnUpdate != null) {
                                _btnUpdate.Content = "Update Prepared (Restart SimHub)";
                                _btnUpdate.IsEnabled = false;
                                _btnUpdate.Background = Brushes.Gray;
                            }
                        });
                    }
                }
                catch (Exception ex) {
                    Application.Current.Dispatcher.Invoke(() => {
                        MessageBox.Show($"Update failed: {ex.Message}\n\nMake sure SimHub is running as Administrator.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                }
            });
        }

        private string SettingsPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PluginsData", "InternalWindMachinePlugin.json");

        public void Init(PluginManager pluginManager)
        {
            this.PluginManager = pluginManager;
            LoadSettings();
            UpdateSensorDirectory();
            CheckForUpdateAsync();

            _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _uiTimer.Tick += (s, e) => {
                if (_pbCenter != null) { _pbCenter.Value = _lastCenter; _txtCenter.Text = $"{_lastCenter:0}%"; }
                if (_pbLeft != null) { _pbLeft.Value = _lastLeft; _txtLeft.Text = $"{_lastLeft:0}%"; }
                if (_pbRight != null) { _pbRight.Value = _lastRight; _txtRight.Text = $"{_lastRight:0}%"; }
            };
            _uiTimer.Start();
        }

        private void UpdateSensorDirectory()
        {
            try { string path = GetAbsSensorPath(); if (!Directory.Exists(path)) Directory.CreateDirectory(path); } catch { }
        }

        private string GetAbsSensorPath()
        {
            if (string.IsNullOrEmpty(Settings.SensorDirectory)) return AppDomain.CurrentDomain.BaseDirectory;
            if (Path.IsPathRooted(Settings.SensorDirectory)) return Settings.SensorDirectory;
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Settings.SensorDirectory);
        }

        public void DataUpdate(PluginManager pluginManager, ref GameData data)
        {
            // CENTER FAN
            if (Settings.OverC)
                _lastCenter = WriteValueToFile(Settings.PowerC, "WindPercentageCenter(default).sensor");
            else
                _lastCenter = Settings.EnableCenter || !Settings.Use3DWind ? WritePropertyToFile(Settings.PropCenter, "WindPercentageCenter(default).sensor") : 0;

            // 3D WIND (LEFT/RIGHT)
            if (Settings.Use3DWind)
            {
                // LEFT
                if (Settings.OverL)
                    _lastLeft = WriteValueToFile(Settings.PowerL, "WindPercentageLeft.sensor");
                else
                    _lastLeft = Settings.EnableLeft ? WritePropertyToFile(Settings.PropLeft, "WindPercentageLeft.sensor") : 0;

                // RIGHT
                if (Settings.OverR)
                    _lastRight = WriteValueToFile(Settings.PowerR, "WindPercentageRight.sensor");
                else
                    _lastRight = Settings.EnableRight ? WritePropertyToFile(Settings.PropRight, "WindPercentageRight.sensor") : 0;
            }
            else
            {
                _lastLeft = 0;
                _lastRight = 0;
            }
        }

        private double WritePropertyToFile(string propName, string fileName)
        {
            try
            {
                var val = PluginManager.GetPropertyValue(propName);
                if (val != null) return WriteValueToFile(Convert.ToDouble(val), fileName);
            }
            catch { }
            return 0;
        }

        private double WriteValueToFile(double val, string fileName)
        {
            try
            {
                string filePath = Path.Combine(GetAbsSensorPath(), fileName);
                File.WriteAllText(filePath, val.ToString("0.00"));
                return val;
            }
            catch { }
            return 0;
        }

        public void End(PluginManager pluginManager)
        {
            _uiTimer?.Stop(); ResetSensors(); SaveSettings();
        }

        private void ResetSensors()
        {
            try {
                string path = GetAbsSensorPath();
                if (Directory.Exists(path)) {
                    foreach (var file in Directory.GetFiles(path, "*.sensor")) File.WriteAllText(file, "-1.00");
                }
            } catch { }
        }

        private void ResetSpecificSensor(string fileName)
        {
            try {
                string path = Path.Combine(GetAbsSensorPath(), fileName);
                if (File.Exists(path)) File.WriteAllText(path, "-1.00");
            } catch { }
        }

        private void LoadSettings()
        {
            try {
                if (File.Exists(SettingsPath)) {
                    string json = File.ReadAllText(SettingsPath);
                    Settings = JsonConvert.DeserializeObject<InternalWindMachineSettings>(json) ?? new InternalWindMachineSettings();
                }
            } catch { }
        }

        private void SaveSettings()
        {
            try {
                string dir = Path.GetDirectoryName(SettingsPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(Settings, Formatting.Indented));
            } catch { }
        }

        public Control GetWPFSettingsControl(PluginManager pluginManager)
        {
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)) };
            var mainStack = new StackPanel { Margin = new Thickness(25) };
            scroll.Content = mainStack;

            // HEADER
            var headerBorder = new Border { Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)), CornerRadius = new CornerRadius(5), Padding = new Thickness(15), Margin = new Thickness(0, 0, 0, 20) };
            var headerStack = new StackPanel();
            headerStack.Children.Add(new TextBlock { Text = "INTERNAL WIND MACHINE", FontSize = 28, FontWeight = FontWeights.Bold, Foreground = Brushes.White });
            
            var subHeader = new TextBlock { FontSize = 12, Foreground = Brushes.Gray };
            subHeader.Inlines.Add(new Run("by "));
            var hl = new Hyperlink(new Run("The Real Karle")) { NavigateUri = new Uri("https://linktr.ee/therealkarle"), Foreground = Brushes.Cyan, TextDecorations = null };
            hl.RequestNavigate += (s, e) => { try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri)); } catch { } e.Handled = true; };
            subHeader.Inlines.Add(hl);
            subHeader.Inlines.Add(new Run($" | v{PluginVersion}"));
            headerStack.Children.Add(subHeader);

            _btnUpdate = new Button { 
                Content = "Checking for Updates...", 
                Background = Brushes.SlateGray, 
                Foreground = Brushes.White,
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(0, 10, 0, 0),
                Visibility = Visibility.Collapsed,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            _btnUpdate.Click += (s, e) => {
                InstallUpdate();
            };
            headerStack.Children.Add(_btnUpdate);

            headerBorder.Child = headerStack;
            mainStack.Children.Add(headerBorder);

            // MONITOR
            mainStack.Children.Add(CreateSectionHeader("LIVE STATUS"));
            var monitorRow = new UniformGrid { Columns = 3, Margin = new Thickness(0, 0, 0, 25) };
            _metL = CreateMeter("LEFT", out _pbLeft, out _txtLeft);
            _metC = CreateMeter("CENTER", out _pbCenter, out _txtCenter);
            _metR = CreateMeter("RIGHT", out _pbRight, out _txtRight);
            monitorRow.Children.Add(_metL);
            monitorRow.Children.Add(_metC);
            monitorRow.Children.Add(_metR);
            mainStack.Children.Add(monitorRow);

            // POWER OVERRIDES TAB
            var powerExp = new Expander { Header = "Fan Power Overrides (Manual Control)", Foreground = Brushes.LimeGreen, Margin = new Thickness(0, 0, 0, 20) };
            var powerStack = new StackPanel { Margin = new Thickness(15, 10, 0, 10) };
            
            powerStack.Children.Add(CreateOverrideControl("Left Fan", Settings.OverL, Settings.PowerL, out _cbOverL, out _slL, out _txtValL, (b) => { Settings.OverL = b; UpdateFanVisuals(); SaveSettings(); }, (v) => { Settings.PowerL = v; SaveSettings(); }));
            powerStack.Children.Add(CreateOverrideControl("Center Fan", Settings.OverC, Settings.PowerC, out _cbOverC, out _slC, out _txtValC, (b) => { Settings.OverC = b; UpdateFanVisuals(); SaveSettings(); }, (v) => { Settings.PowerC = v; SaveSettings(); }));
            powerStack.Children.Add(CreateOverrideControl("Right Fan", Settings.OverR, Settings.PowerR, out _cbOverR, out _slR, out _txtValR, (b) => { Settings.OverR = b; UpdateFanVisuals(); SaveSettings(); }, (v) => { Settings.PowerR = v; SaveSettings(); }));
            
            powerExp.Content = powerStack;
            mainStack.Children.Add(powerExp);

            // GENERAL CONFIG
            mainStack.Children.Add(CreateSectionHeader("GENERAL CONFIGURATION"));
            _cb3D = CreateStyledCheckBox("Enable 3D Wind (Multi-Fan)", Settings.Use3DWind);
            mainStack.Children.Add(_cb3D);

            mainStack.Children.Add(new TextBlock { Text = "Sensor Path:", Foreground = Brushes.White, Margin = new Thickness(0, 5, 0, 5) });
            var pathGrid = new Grid();
            pathGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pathGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _tbDir = CreateStyledTextBox(Settings.SensorDirectory);
            _tbDir.TextChanged += (s, e) => { Settings.SensorDirectory = _tbDir.Text; UpdateSensorDirectory(); SaveSettings(); };
            Grid.SetColumn(_tbDir, 0); pathGrid.Children.Add(_tbDir);
            var btnB = new Button { Content = "Browse...", Padding = new Thickness(10, 0, 10, 0), Height = 30, Margin = new Thickness(5, 5, 0, 10), Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)), Foreground = Brushes.White };
            btnB.Click += (s, e) => {
                using (var fbd = new System.Windows.Forms.FolderBrowserDialog()) {
                    fbd.SelectedPath = GetAbsSensorPath();
                    if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK) _tbDir.Text = fbd.SelectedPath;
                }
            };
            Grid.SetColumn(btnB, 1); pathGrid.Children.Add(btnB);
            mainStack.Children.Add(pathGrid);

            var actionRow = new UniformGrid { Columns = 2, Margin = new Thickness(0, 5, 0, 20) };
            var btnO = new Button { Content = "Open Folder", Padding = new Thickness(0, 8, 0, 8), Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)), Foreground = Brushes.White, BorderThickness = new Thickness(0), Margin = new Thickness(0, 0, 5, 0) };
            btnO.Click += (s, e) => { try { Process.Start("explorer.exe", GetAbsSensorPath()); } catch { } };
            actionRow.Children.Add(btnO);
            var btnR = new Button { Content = "Reset Sensors (-1.00)", Padding = new Thickness(0, 8, 0, 8), Background = new SolidColorBrush(Color.FromRgb(80, 40, 40)), Foreground = Brushes.White, BorderThickness = new Thickness(0), Margin = new Thickness(5, 0, 0, 0) };
            btnR.Click += (s, e) => { ResetSensors(); };
            actionRow.Children.Add(btnR);
            mainStack.Children.Add(actionRow);

            // ADVANCED EXPANDER
            var advExp = new Expander { Header = "Advanced Settings", Foreground = Brushes.Cyan, Margin = new Thickness(0, 10, 0, 10), IsExpanded = false };
            var advStack = new StackPanel { Margin = new Thickness(15, 10, 0, 10) };
            advStack.Children.Add(new TextBlock { Text = "Fan Overrides:", FontWeight = FontWeights.Bold, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 5) });
            advStack.Children.Add(new TextBlock { Text = "Note: Overrides are frozen unless 3D Wind is enabled.", Foreground = Brushes.Gray, FontSize = 10, Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap });
            var fanGrid = new UniformGrid { Columns = 3, Margin = new Thickness(0, 0, 0, 20) };
            _cbL = CreateStyledCheckBox("Left Fan", Settings.EnableLeft);
            _cbL.Checked += (s, e) => { Settings.EnableLeft = true; UpdateFanVisuals(); SaveSettings(); };
            _cbL.Unchecked += (s, e) => { Settings.EnableLeft = false; ResetSpecificSensor("WindPercentageLeft.sensor"); UpdateFanVisuals(); SaveSettings(); };
            fanGrid.Children.Add(_cbL);
            _cbC = CreateStyledCheckBox("Center Fan", Settings.EnableCenter);
            _cbC.Checked += (s, e) => { Settings.EnableCenter = true; UpdateFanVisuals(); SaveSettings(); };
            _cbC.Unchecked += (s, e) => { Settings.EnableCenter = false; ResetSpecificSensor("WindPercentageCenter(default).sensor"); UpdateFanVisuals(); SaveSettings(); };
            fanGrid.Children.Add(_cbC);
            _cbR = CreateStyledCheckBox("Right Fan", Settings.EnableRight);
            _cbR.Checked += (s, e) => { Settings.EnableRight = true; UpdateFanVisuals(); SaveSettings(); };
            _cbR.Unchecked += (s, e) => { Settings.EnableRight = false; ResetSpecificSensor("WindPercentageRight.sensor"); UpdateFanVisuals(); SaveSettings(); };
            fanGrid.Children.Add(_cbR);
            advStack.Children.Add(fanGrid);

            _cb3D.Checked += (s, e) => { 
                Settings.Use3DWind = true; Settings.EnableLeft = true; Settings.EnableCenter = true; Settings.EnableRight = true;
                _cbL.IsChecked = true; _cbC.IsChecked = true; _cbR.IsChecked = true;
                UpdateFanVisuals(); SaveSettings(); 
            };
            _cb3D.Unchecked += (s, e) => { 
                Settings.Use3DWind = false; ResetSpecificSensor("WindPercentageLeft.sensor"); ResetSpecificSensor("WindPercentageRight.sensor");
                UpdateFanVisuals(); SaveSettings(); 
            };
            
            advStack.Children.Add(new TextBlock { Text = "Update Preferences:", FontWeight = FontWeights.Bold, Foreground = Brushes.White, Margin = new Thickness(0, 10, 0, 5) });
            _cbUpdate = CreateStyledCheckBox("Check for updates on startup", Settings.EnableUpdateChecks);
            _cbUpdate.Checked += (s, e) => { Settings.EnableUpdateChecks = true; SaveSettings(); };
            _cbUpdate.Unchecked += (s, e) => { Settings.EnableUpdateChecks = false; SaveSettings(); };
            advStack.Children.Add(_cbUpdate);

            _cbNotify = CreateStyledCheckBox("Show Windows notifications for updates", Settings.EnableUpdateNotifications);
            _cbNotify.Checked += (s, e) => { Settings.EnableUpdateNotifications = true; SaveSettings(); };
            _cbNotify.Unchecked += (s, e) => { Settings.EnableUpdateNotifications = false; SaveSettings(); };
            _cbNotify.Margin = new Thickness(20, 0, 0, 10);
            advStack.Children.Add(_cbNotify);
            
            UpdateFanVisuals(); // Initial call

            var btnResetAll = new Button { Content = "Reset All Plugin Settings", Padding = new Thickness(10, 5, 10, 5), Background = new SolidColorBrush(Color.FromRgb(100, 30, 30)), Foreground = Brushes.White, Margin = new Thickness(0, 10, 0, 10), HorizontalAlignment = HorizontalAlignment.Left };
            btnResetAll.Click += (s, e) => {
                if (_resetAllState == 0) {
                    btnResetAll.Content = "CONFIRM RESET (Click Again)"; btnResetAll.Background = Brushes.Red; _resetAllState = 1;
                    var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                    t.Tick += (st, et) => { _resetAllState = 0; btnResetAll.Content = "Reset All Plugin Settings"; btnResetAll.Background = new SolidColorBrush(Color.FromRgb(100, 30, 30)); t.Stop(); };
                    t.Start();
                } else {
                    Settings = new InternalWindMachineSettings();
                    SaveSettings(); RefreshUI(); UpdateSensorDirectory();
                    MessageBox.Show("All settings have been reset successfully!");
                    _resetAllState = 0;
                }
            };
            advStack.Children.Add(btnResetAll);

            // EXPERT TAB
            var expExp = new Expander { Header = "Expert Settings (Property Bindings)", Foreground = Brushes.OrangeRed, Margin = new Thickness(0, 15, 0, 10) };
            var expStack = new StackPanel { Margin = new Thickness(15, 10, 0, 10) };
            var expConfirm = new StackPanel();
            var btnU = new Button { Content = "I Know What I'm Doing - Unlock Expert Settings", Background = Brushes.DarkRed, Foreground = Brushes.White, Padding = new Thickness(10) };
            expConfirm.Children.Add(new TextBlock { Text = "WARNING: Changing property bindings can break the wind machine if the names are incorrect.", Foreground = Brushes.OrangeRed, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10), FontWeight = FontWeights.Bold });
            expConfirm.Children.Add(btnU);
            var expContent = new StackPanel { Visibility = Visibility.Collapsed };
            btnU.Click += (s, e) => { expConfirm.Visibility = Visibility.Collapsed; expContent.Visibility = Visibility.Visible; };
            _propC = CreateStyledTextBox(Settings.PropCenter); _propC.TextChanged += (s, e) => { Settings.PropCenter = _propC.Text; SaveSettings(); };
            expContent.Children.Add(new TextBlock { Text = "Center Prop:", Foreground = Brushes.Gray }); expContent.Children.Add(_propC);
            _propL = CreateStyledTextBox(Settings.PropLeft); _propL.TextChanged += (s, e) => { Settings.PropLeft = _propL.Text; SaveSettings(); };
            expContent.Children.Add(new TextBlock { Text = "Left Prop:", Foreground = Brushes.Gray }); expContent.Children.Add(_propL);
            _propR = CreateStyledTextBox(Settings.PropRight); _propR.TextChanged += (s, e) => { Settings.PropRight = _propR.Text; SaveSettings(); };
            expContent.Children.Add(new TextBlock { Text = "Right Prop:", Foreground = Brushes.Gray }); expContent.Children.Add(_propR);
            var btnRP = new Button { Content = "Reset Bindings to Default", Padding = new Thickness(10, 5, 10, 5), Background = Brushes.DarkSlateGray, Foreground = Brushes.White, Margin = new Thickness(0, 10, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
            btnRP.Click += (s, e) => {
                if (_resetPropsState == 0) { btnRP.Content = "CONFIRM (Click Again)"; _resetPropsState = 1; var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) }; t.Tick += (st, et) => { _resetPropsState = 0; btnRP.Content = "Reset Bindings to Default"; t.Stop(); }; t.Start();
                } else { Settings.PropCenter = "ShakeItWindPlugin.OutputCenter"; Settings.PropLeft = "ShakeItWindPlugin.OutputLeft"; Settings.PropRight = "ShakeItWindPlugin.OutputRight"; RefreshUI(); SaveSettings(); _resetPropsState = 0; }
            };
            expContent.Children.Add(btnRP); expStack.Children.Add(expConfirm); expStack.Children.Add(expContent);
            expExp.Content = expStack; advStack.Children.Add(expExp);
            advExp.Content = advStack;
            mainStack.Children.Add(advExp);

            return new UserControl { Content = scroll };
        }

        private void UpdateFanVisuals()
        {
            if (_cb3D == null) return;
            
            bool is3D = Settings.Use3DWind;
            
            // Enable/Disable Overrides UI
            _cbL.IsEnabled = is3D; _cbC.IsEnabled = is3D; _cbR.IsEnabled = is3D;
            _cbL.Opacity = is3D ? 1.0 : 0.4; _cbC.Opacity = is3D ? 1.0 : 0.4; _cbR.Opacity = is3D ? 1.0 : 0.4;

            if (_cbOverL != null) { _cbOverL.IsEnabled = is3D; _cbOverL.Opacity = is3D ? 1.0 : 0.4; }
            if (_cbOverR != null) { _cbOverR.IsEnabled = is3D; _cbOverR.Opacity = is3D ? 1.0 : 0.4; }
            if (_slL != null) { _slL.IsEnabled = is3D && Settings.OverL; _slL.Opacity = is3D && Settings.OverL ? 1.0 : 0.4; }
            if (_slR != null) { _slR.IsEnabled = is3D && Settings.OverR; _slR.Opacity = is3D && Settings.OverR ? 1.0 : 0.4; }
            
            // MASTER VISUALS FOR LIVE STATUS METERS
            // Center active in 1-fan mode OR if override/enable in 3D mode
            bool centerActive = !is3D || Settings.EnableCenter || Settings.OverC;
            bool leftActive = is3D && (Settings.EnableLeft || Settings.OverL);
            bool rightActive = is3D && (Settings.EnableRight || Settings.OverR);

            if (_metC != null) _metC.Opacity = centerActive ? 1.0 : 0.3;
            if (_metL != null) _metL.Opacity = leftActive ? 1.0 : 0.3;
            if (_metR != null) _metR.Opacity = rightActive ? 1.0 : 0.3;
        }

        private FrameworkElement CreateOverrideControl(string label, bool isOver, double val, out CheckBox cb, out Slider sl, out TextBlock txtVal, Action<bool> onCheck, Action<double> onSlide)
        {
            var grid = new Grid { Margin = new Thickness(0, 5, 0, 15) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });

            cb = CreateStyledCheckBox(label, isOver);
            cb.Checked += (s, e) => { onCheck(true); UpdateFanVisuals(); };
            cb.Unchecked += (s, e) => { onCheck(false); UpdateFanVisuals(); };
            Grid.SetColumn(cb, 0); grid.Children.Add(cb);

            var slRef = new Slider { Minimum = 0, Maximum = 100, Value = val, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 10, 0) };
            var txtRef = new TextBlock { Text = $"{val:0}%", Foreground = Brushes.Cyan, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
            slRef.ValueChanged += (s, e) => { txtRef.Text = $"{slRef.Value:0}%"; onSlide(slRef.Value); };
            sl = slRef; txtVal = txtRef;
            Grid.SetColumn(sl, 1); grid.Children.Add(sl);
            Grid.SetColumn(txtRef, 2); grid.Children.Add(txtRef);

            return grid;
        }

        private void RefreshUI()
        {
            try {
                if (_cb3D != null) _cb3D.IsChecked = Settings.Use3DWind;
                if (_cbL != null) _cbL.IsChecked = Settings.EnableLeft;
                if (_cbC != null) _cbC.IsChecked = Settings.EnableCenter;
                if (_cbR != null) _cbR.IsChecked = Settings.EnableRight;
                if (_tbDir != null) _tbDir.Text = Settings.SensorDirectory;
                if (_propC != null) _propC.Text = Settings.PropCenter;
                if (_propL != null) _propL.Text = Settings.PropLeft;
                if (_propR != null) _propR.Text = Settings.PropRight;
                if (_cbOverL != null) _cbOverL.IsChecked = Settings.OverL;
                if (_cbOverC != null) _cbOverC.IsChecked = Settings.OverC;
                if (_cbOverR != null) _cbOverR.IsChecked = Settings.OverR;
                if (_slL != null) _slL.Value = Settings.PowerL;
                if (_slC != null) _slC.Value = Settings.PowerC;
                if (_slR != null) _slR.Value = Settings.PowerR;
                if (_cbUpdate != null) _cbUpdate.IsChecked = Settings.EnableUpdateChecks;
                if (_cbNotify != null) _cbNotify.IsChecked = Settings.EnableUpdateNotifications;
                UpdateFanVisuals();
            } catch { }
        }

        private Border CreateMeter(string label, out ProgressBar pb, out TextBlock txt)
        {
            var container = new Border { Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)), BorderBrush = Brushes.DimGray, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), Padding = new Thickness(10), Margin = new Thickness(5) };
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = label, HorizontalAlignment = HorizontalAlignment.Center, Foreground = Brushes.White, FontSize = 10, Margin = new Thickness(0, 0, 0, 5) });
            pb = new ProgressBar { Height = 10, Minimum = 0, Maximum = 100, Background = Brushes.Black, Foreground = Brushes.Cyan, BorderThickness = new Thickness(0) };
            stack.Children.Add(pb);
            txt = new TextBlock { Text = "0%", HorizontalAlignment = HorizontalAlignment.Center, Foreground = Brushes.Cyan, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 5, 0, 0) };
            stack.Children.Add(txt);
            container.Child = stack;
            return container;
        }

        private TextBlock CreateSectionHeader(string title) { return new TextBlock { Text = title, Foreground = Brushes.Cyan, FontWeight = FontWeights.Bold, FontSize = 14, Margin = new Thickness(0, 15, 0, 10) }; }
        private CheckBox CreateStyledCheckBox(string l, bool ic) { return new CheckBox { Content = l, IsChecked = ic, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 10), VerticalContentAlignment = VerticalAlignment.Center }; }
        private TextBox CreateStyledTextBox(string t) { return new TextBox { Text = t, Background = new SolidColorBrush(Color.FromRgb(50, 50, 50)), FontSize = 11, Foreground = Brushes.White, BorderBrush = Brushes.DimGray, Padding = new Thickness(5), Margin = new Thickness(0, 5, 0, 10) }; }
    }

    public static class GraphicsHelper
    {
        public static System.Drawing.Image LoadIcon() { try { var a = Assembly.GetExecutingAssembly(); string rn = a.GetManifestResourceNames().FirstOrDefault(r => r.EndsWith("sdkmenuicon.png")); if (rn != null) { using (Stream s = a.GetManifestResourceStream(rn)) { if (s != null) return System.Drawing.Image.FromStream(s); } } } catch { } return null; }
    }
}
