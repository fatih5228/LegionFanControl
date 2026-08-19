using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using IOPath = System.IO.Path;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;
using LibreHardwareMonitor.Hardware;

namespace LegionFanControl
{
    // ---------------- WMI: LENOVO_GAMEZONE_DATA ----------------
    public class FanHardware : IDisposable
    {
        private ManagementObject _instance;

        public FanHardware()
        {
            var mc = new ManagementClass(@"root\wmi:LENOVO_GAMEZONE_DATA");
            var instances = mc.GetInstances();
            if (instances == null || instances.Count == 0)
                throw new Exception("LENOVO_GAMEZONE_DATA bulunamadi");
            foreach (ManagementObject mo in instances) { _instance = mo; break; }
        }

        private uint Invoke(string method, uint? data)
        {
            ManagementBaseObject result;
            if (data.HasValue)
            {
                var inParams = _instance.GetMethodParameters(method);
                inParams["Data"] = data.Value;
                result = _instance.InvokeMethod(method, inParams, null);
            }
            else
            {
                result = _instance.InvokeMethod(method, null, null);
            }
            if (result == null) throw new Exception(method + " cevapsiz");
            var d = result.Properties["Data"];
            return d == null ? 0u : Convert.ToUInt32(d.Value);
        }

        public void SetFanCooling(bool on) { Invoke("SetFanCooling", on ? 1u : 0u); }
        public bool GetFanCoolingStatus() { return Invoke("GetFanCoolingStatus", null) == 1u; }
        public uint GetFan1Speed() { return Invoke("GetFan1Speed", null); }
        public uint GetFan2Speed() { return Invoke("GetFan2Speed", null); }
        public uint GetFanMaxSpeed() { return Invoke("GetFanMaxSpeed", null); }
        public uint GetCPUTemp() { return Invoke("GetCPUTemp", null); }
        public uint GetIRTemp() { return Invoke("GetIRTemp", null); }
        public bool GetWinKeyLock() { return Invoke("GetWinKeyStatus", null) == 1u; }
        public void SetWinKeyLock(bool locked) { Invoke("SetWinKeyStatus", locked ? 1u : 0u); }
        public bool GetTouchpadLock() { return Invoke("GetTPStatus", null) == 1u; }
        public void SetTouchpadLock(bool locked) { Invoke("SetTPStatus", locked ? 1u : 0u); }

        public void Dispose()
        {
            if (_instance != null) { _instance.Dispose(); _instance = null; }
        }
    }

    // ---------------- LibreHardwareMonitor Sicaklik Okuyucu ----------------
    public class LhmMonitor
    {
        private Computer _computer;
        private bool _tried;
        public string CpuNote = "";
        public string GpuNote = "";

        public bool Available { get { return _computer != null; } }

        private void EnsureOpen()
        {
            if (_tried) return;
            _tried = true;
            try
            {
                _computer = new Computer
                {
                    IsCpuEnabled = true,
                    IsGpuEnabled = true
                };
                _computer.Open();
            }
            catch (Exception ex)
            {
                CpuNote = "LHM acilamadi: " + ex.GetType().Name;
                _computer = null;
            }
        }

        public void ReadTemps(out float? cpu, out float? gpu)
        {
            cpu = null; gpu = null;
            EnsureOpen();
            if (_computer == null) return;
            try
            {
                foreach (var hw in _computer.Hardware)
                {
                    hw.Update();
                    ReadSensors(hw, ref cpu, ref gpu);
                    foreach (var sub in hw.SubHardware)
                    {
                        sub.Update();
                        ReadSensors(sub, ref cpu, ref gpu);
                    }
                }
            }
            catch { }
        }

        private void ReadSensors(IHardware hw, ref float? cpu, ref float? gpu)
        {
            bool isCpu = hw.HardwareType == HardwareType.Cpu;
            bool isGpu = hw.HardwareType == HardwareType.GpuNvidia ||
                         hw.HardwareType == HardwareType.GpuAmd ||
                         hw.HardwareType == HardwareType.GpuIntel;
            if (!isCpu && !isGpu) return;
            float? first = null;
            foreach (var s in hw.Sensors)
            {
                if (s.SensorType != SensorType.Temperature || !s.Value.HasValue) continue;
                if (!first.HasValue) first = s.Value;
                string n = s.Name ?? "";
                if (isCpu && (n.IndexOf("Package", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              n.IndexOf("Core Average", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    cpu = s.Value;
                    CpuNote = "CPU: " + n;
                    return;
                }
                if (isGpu && n.IndexOf("Core", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    gpu = s.Value;
                    GpuNote = "GPU: " + n;
                    return;
                }
            }
            if (isCpu && !cpu.HasValue && first.HasValue) cpu = first;
            if (isGpu && !gpu.HasValue && first.HasValue) gpu = first;
        }

        public void Close()
        {
            try { if (_computer != null) _computer.Close(); } catch { }
            _computer = null;
        }
    }

    // ---------------- UI Toggle Switch Bileseni ----------------
    public class ModernSwitch : Border
    {
        private readonly Border _thumb;
        private readonly TextBlock _stateLabel;
        private bool _isChecked;

        public event Action<bool> StateChanged;

        public bool IsChecked
        {
            get { return _isChecked; }
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    UpdateVisual();
                    if (StateChanged != null) StateChanged(_isChecked);
                }
            }
        }

        public ModernSwitch()
        {
            Width = 74;
            Height = 28;
            CornerRadius = new CornerRadius(14);
            Background = MainWindow.SwitchOffBrush;
            BorderBrush = MainWindow.PanelEdgeBrush;
            BorderThickness = new Thickness(1);
            Cursor = Cursors.Hand;

            var grid = new Grid();
            grid.Margin = new Thickness(4, 0, 4, 0);

            _thumb = new Border
            {
                Width = 20,
                Height = 20,
                CornerRadius = new CornerRadius(10),
                Background = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 6,
                    ShadowDepth = 1,
                    Opacity = 0.4
                }
            };

            _stateLabel = new TextBlock
            {
                Text = Lang.T("switch.off"),
                FontSize = 9.5,
                FontWeight = FontWeights.Bold,
                Foreground = MainWindow.TextMutedBrush,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 4, 0)
            };

            grid.Children.Add(_stateLabel);
            grid.Children.Add(_thumb);
            Child = grid;

            MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                if (IsEnabled) IsChecked = !IsChecked;
            };
        }

        public void SetCheckedQuietly(bool val)
        {
            _isChecked = val;
            UpdateVisual();
        }

        private void UpdateVisual()
        {
            if (_isChecked)
            {
                Background = MainWindow.RedBrush;
                BorderBrush = MainWindow.RedGlowBrush;
                _thumb.HorizontalAlignment = HorizontalAlignment.Right;
                _stateLabel.HorizontalAlignment = HorizontalAlignment.Left;
                _stateLabel.Margin = new Thickness(4, 0, 0, 0);
                _stateLabel.Text = Lang.T("switch.on");
                _stateLabel.Foreground = Brushes.White;
            }
            else
            {
                Background = MainWindow.SwitchOffBrush;
                BorderBrush = MainWindow.PanelEdgeBrush;
                _thumb.HorizontalAlignment = HorizontalAlignment.Left;
                _stateLabel.HorizontalAlignment = HorizontalAlignment.Right;
                _stateLabel.Margin = new Thickness(0, 0, 4, 0);
                _stateLabel.Text = Lang.T("switch.off");
                _stateLabel.Foreground = MainWindow.TextMutedBrush;
            }
        }
    }

    // ---------------- Ana Pencere ----------------
    public class MainWindow : Window
    {
        private uint MaxRpm = 4000;

        // Renk Paleti (Lenovo Legion Dark Gaming Estetigi)
        public static readonly Brush BgBrush = new SolidColorBrush(Color.FromRgb(0x0C, 0x0E, 0x12));
        public static readonly Brush SidebarBrush = new SolidColorBrush(Color.FromRgb(0x12, 0x15, 0x1B));
        public static readonly Brush TitlebarBrush = new SolidColorBrush(Color.FromRgb(0x09, 0x0A, 0x0D));
        public static readonly Brush CardBrush = new SolidColorBrush(Color.FromRgb(0x16, 0x1A, 0x22));
        public static readonly Brush SubCardBrush = new SolidColorBrush(Color.FromRgb(0x10, 0x13, 0x18));
        public static readonly Brush PanelEdgeBrush = new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF));
        public static readonly Brush SelBrush = new SolidColorBrush(Color.FromRgb(0x1F, 0x24, 0x30));
        public static readonly Brush RedBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0x23, 0x1A));
        public static readonly Brush RedGlowBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x38, 0x2E));
        public static readonly Brush RedDarkBrush = new SolidColorBrush(Color.FromRgb(0x8B, 0x12, 0x0D));
        public static readonly Brush SwitchOffBrush = new SolidColorBrush(Color.FromRgb(0x24, 0x29, 0x34));
        public static readonly Brush TrackBrush = new SolidColorBrush(Color.FromRgb(0x10, 0x12, 0x16));
        public static readonly Brush GreenBrush = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
        public static readonly Brush OrangeBrush = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
        public static readonly Brush CyanBrush = new SolidColorBrush(Color.FromRgb(0x06, 0xB6, 0xD4));

        public static readonly Brush TextPrimary = Brushes.White;
        public static readonly Brush TextSecondary = new SolidColorBrush(Color.FromRgb(0xA2, 0xA9, 0xB8));
        public static readonly Brush TextMutedBrush = new SolidColorBrush(Color.FromRgb(0x64, 0x6C, 0x7D));

        private static readonly string BaseDir = IOPath.GetDirectoryName(typeof(MainWindow).Assembly.Location) ?? AppDomain.CurrentDomain.BaseDirectory;

        public static DropShadowEffect SoftShadow(double blur = 14, double depth = 3, double opacity = 0.35)
        {
            return new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = blur,
                ShadowDepth = depth,
                Direction = 270,
                Opacity = opacity,
                RenderingBias = RenderingBias.Performance
            };
        }

        // Fan sayfasi elemanlari
        private Border _heroCardBorder;
        private ModernSwitch _extremeSwitch;
        private TextBlock _heroStatusBadge;
        private TextBlock _heroDescText;
        private ModernSwitch _autoSwitch;
        private Slider _thresholdSlider;
        private TextBlock _thresholdValue;
        private TextBlock _fan1RpmText, _fan2RpmText, _fan1PctText, _fan2PctText;
        private Rectangle _fan1Bar, _fan2Bar;
        private TextBlock _cpuTempVal, _gpuTempVal, _irTempVal;
        private Border _cpuTempBadge, _gpuTempBadge, _irTempBadge;

        // Otomatik Mod Durumu
        private bool _autoMode;
        private int _threshold = 70;
        private bool _loadingSettings;

        // Sayfalar ve Durum
        private Grid[] _pages;
        private Grid[] _menuItems;
        private Rectangle[] _menuStrips;
        private TextBlock[] _menuTexts;
        private TextBlock[] _menuIcons;
        private int _currentPage = -1;
        private TextBlock _statusText;
        private Ellipse _statusDot;

        // Donanim ve Sistem
        private StackPanel _hwContainer;
        private ModernSwitch _winKeySwitch, _tpSwitch, _startupSwitch;

        // Polling ve Donanim
        private DispatcherTimer _timer;
        private bool _polling;
        private bool _realExit;
        private FanHardware _hw;
        private string _hwError;
        private readonly LhmMonitor _lhm = new LhmMonitor();

        private Forms.NotifyIcon _tray;
        private Forms.ToolStripMenuItem _trayToggleItem, _trayShowItem, _trayExitItem;
        private Drawing.Icon _iconOn, _iconOff;

        public MainWindow()
        {
            Title = Lang.T("app.title");
            Width = 840; Height = 670;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = false;
            ResizeMode = ResizeMode.CanResizeWithGrip;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = BgBrush;

            var chrome = new System.Windows.Shell.WindowChrome
            {
                CaptionHeight = 40,
                ResizeBorderThickness = new Thickness(6),
                GlassFrameThickness = new Thickness(1),
                CornerRadius = new CornerRadius(0),
                UseAeroCaptionButtons = false
            };
            System.Windows.Shell.WindowChrome.SetWindowChrome(this, chrome);

            LoadSettings();
            LoadWindowIcon();
            BuildUi();
            SetAutoMode(_autoMode);
            BuildTray();
            TryConnectHardware();
            SelectPage(0);
            LoadHardwareInfo();
            RefreshSysStates();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _timer.Tick += (s, e) => Poll();
            _timer.Start();
            Poll();

            Closing += (s, e) =>
            {
                if (!_realExit)
                {
                    e.Cancel = true;
                    Hide();
                }
            };
            Closed += (s, e) =>
            {
                _timer.Stop();
                _lhm.Close();
                if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
                if (_hw != null) _hw.Dispose();
            };
        }

        private static BitmapImage LoadIconImage(string path, int decodeWidth)
        {
            var bi = new BitmapImage();
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.StreamSource = fs;
                bi.DecodePixelWidth = decodeWidth;
                bi.EndInit();
            }
            bi.Freeze();
            return bi;
        }

        private void LoadWindowIcon()
        {
            try
            {
                string ico = IOPath.Combine(BaseDir, "app.ico");
                if (File.Exists(ico)) Icon = LoadIconImage(ico, 32);
            }
            catch { }
        }

        private void TryConnectHardware()
        {
            try
            {
                _hw = new FanHardware();
                _hwError = null;
                try
                {
                    uint m = _hw.GetFanMaxSpeed();
                    if (m >= 500 && m <= 10000) MaxRpm = m;
                }
                catch { }
            }
            catch (Exception ex)
            {
                _hw = null;
                _hwError = ex.Message;
            }
        }

        private static string SettingsPath { get { return IOPath.Combine(BaseDir, "ayarlar.ini"); } }

        private void LoadSettings()
        {
            _loadingSettings = true;
            _autoMode = false;
            _threshold = 70;
            try
            {
                if (File.Exists(SettingsPath))
                {
                    foreach (var line in File.ReadAllLines(SettingsPath))
                    {
                        int eq = line.IndexOf('=');
                        if (eq <= 0) continue;
                        string key = line.Substring(0, eq).Trim();
                        string val = line.Substring(eq + 1).Trim();
                        if (key == "AutoMode") _autoMode = val == "1";
                        else if (key == "Threshold")
                        {
                            int t;
                            if (int.TryParse(val, out t))
                                _threshold = Math.Max(30, Math.Min(95, t));
                        }
                        else if (key == "Language")
                        {
                            if (val == "tr" || val == "en") Lang.Current = val;
                        }
                    }
                }
            }
            catch { }
            _loadingSettings = false;
        }

        private void SaveSettings()
        {
            if (_loadingSettings) return;
            try
            {
                File.WriteAllText(SettingsPath,
                    "AutoMode=" + (_autoMode ? "1" : "0") + "\n" +
                    "Threshold=" + _threshold + "\n" +
                    "Language=" + Lang.Current + "\n");
            }
            catch { }
        }

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            try
            {
                var helper = new System.Windows.Interop.WindowInteropHelper(this);
                int darkMode = 1;
                DwmSetWindowAttribute(helper.Handle, 20, ref darkMode, sizeof(int));
                DwmSetWindowAttribute(helper.Handle, 19, ref darkMode, sizeof(int));

                int val = 2; // DWMWCP_ROUND (Windows 11 standard rounded corners)
                DwmSetWindowAttribute(helper.Handle, 33, ref val, sizeof(int));
            }
            catch { }
        }

        // ---------------- UI Tasarimi ----------------
        private Border MakeCard(UIElement child, Thickness? margin = null, Thickness? padding = null)
        {
            return new Border
            {
                Background = CardBrush,
                CornerRadius = new CornerRadius(12),
                BorderBrush = PanelEdgeBrush,
                BorderThickness = new Thickness(1),
                Effect = SoftShadow(16, 4, 0.4),
                Padding = padding ?? new Thickness(18, 14, 18, 14),
                Margin = margin ?? new Thickness(0, 0, 0, 10),
                Child = child
            };
        }

        private void BuildUi()
        {
            var shell = new Grid();
            shell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
            shell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            shell.Children.Add(BuildTitleBar());

            var root = new Grid();
            Grid.SetRow(root, 1);
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            root.Children.Add(BuildSidebar());

            var right = new Grid();
            Grid.SetColumn(right, 1);
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var contentHost = new Grid { Margin = new Thickness(20, 12, 20, 6) };
            _pages = new Grid[] { BuildFanPage(), BuildHardwarePage(), BuildSystemPage(), BuildAboutPage() };
            foreach (var p in _pages) contentHost.Children.Add(p);
            right.Children.Add(contentHost);

            // Alt Durum Cubugu (Status Bar)
            var statusBar = new Border
            {
                Background = TitlebarBrush,
                BorderBrush = PanelEdgeBrush,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(16, 7, 16, 7)
            };
            var statusStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            _statusDot = new Ellipse
            {
                Width = 8, Height = 8,
                Fill = GreenBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            _statusText = new TextBlock
            {
                Text = Lang.T("status.ready"),
                Foreground = TextSecondary,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            };
            statusStack.Children.Add(_statusDot);
            statusStack.Children.Add(_statusText);
            statusBar.Child = statusStack;
            Grid.SetRow(statusBar, 1);
            right.Children.Add(statusBar);

            root.Children.Add(right);
            shell.Children.Add(root);

            Content = new Border
            {
                Background = BgBrush,
                SnapsToDevicePixels = true,
                Child = shell
            };
        }

        private UIElement BuildTitleBar()
        {
            var bar = new Grid { Background = TitlebarBrush };
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var left = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(14, 0, 0, 0)
            };

            try
            {
                string ico = IOPath.Combine(BaseDir, "app.ico");
                if (File.Exists(ico))
                {
                    left.Children.Add(new Image
                    {
                        Source = LoadIconImage(ico, 20),
                        Width = 20, Height = 20,
                        VerticalAlignment = VerticalAlignment.Center
                    });
                }
            }
            catch { }

            left.Children.Add(new TextBlock
            {
                Text = "LEGION Y520",
                Foreground = TextPrimary,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 4, 0)
            });

            left.Children.Add(new TextBlock
            {
                Text = Lang.T("titlebar.sub"),
                Foreground = RedBrush,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            });

            bar.Children.Add(left);

            var right = new StackPanel { Orientation = Orientation.Horizontal };
            Grid.SetColumn(right, 1);
            right.Children.Add(MakeTitleButton("—", false));
            right.Children.Add(MakeTitleButton("✕", true));
            bar.Children.Add(right);

            return bar;
        }

        private Border MakeTitleButton(string glyph, bool isClose)
        {
            var txt = new TextBlock
            {
                Text = glyph,
                Foreground = TextSecondary,
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var btn = new Border
            {
                Width = 46,
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Child = txt
            };
            System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(btn, true);
            btn.MouseEnter += (s, e) =>
            {
                btn.Background = isClose ? RedBrush : SelBrush;
                txt.Foreground = TextPrimary;
            };
            btn.MouseLeave += (s, e) =>
            {
                btn.Background = Brushes.Transparent;
                txt.Foreground = TextSecondary;
            };
            btn.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                if (isClose) Hide();
                else WindowState = WindowState.Minimized;
            };
            btn.MouseLeftButtonDown += (s, e) => { e.Handled = true; };
            return btn;
        }

        private UIElement BuildSidebar()
        {
            var outer = new Border
            {
                Background = SidebarBrush,
                BorderBrush = PanelEdgeBrush,
                BorderThickness = new Thickness(0, 0, 1, 0)
            };

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Logo & Header
            var logoCard = new Border
            {
                Margin = new Thickness(14, 16, 14, 14),
                Padding = new Thickness(12, 10, 12, 10),
                Background = SubCardBrush,
                CornerRadius = new CornerRadius(10),
                BorderBrush = PanelEdgeBrush,
                BorderThickness = new Thickness(1)
            };
            var logoStack = new StackPanel();
            var iconRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            try
            {
                string ico = IOPath.Combine(BaseDir, "app.ico");
                if (File.Exists(ico))
                {
                    iconRow.Children.Add(new Image
                    {
                        Source = LoadIconImage(ico, 32),
                        Width = 28, Height = 28,
                        Margin = new Thickness(0, 0, 8, 0)
                    });
                }
            }
            catch { }
            var titleBox = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            titleBox.Children.Add(new TextBlock
            {
                Text = "LEGION",
                Foreground = RedBrush,
                FontSize = 18,
                FontWeight = FontWeights.Black
            });
            titleBox.Children.Add(new TextBlock
            {
                Text = "GAMING ZONE",
                Foreground = TextMutedBrush,
                FontSize = 9,
                FontWeight = FontWeights.Bold
            });
            iconRow.Children.Add(titleBox);
            logoStack.Children.Add(iconRow);
            logoCard.Child = logoStack;
            mainGrid.Children.Add(logoCard);

            // Navigasyon Butonlari
            var menuStack = new StackPanel { Margin = new Thickness(10, 6, 10, 0) };
            Grid.SetRow(menuStack, 1);

            _menuItems = new Grid[4];
            _menuStrips = new Rectangle[4];
            _menuTexts = new TextBlock[4];
            _menuIcons = new TextBlock[4];

            string[] names = { Lang.T("menu.fan"), Lang.T("menu.hw"), Lang.T("menu.sys"), Lang.T("menu.about") };
            string[] icons = { "⚡", "🖥️", "🎮", "ℹ️" };

            for (int i = 0; i < names.Length; i++)
            {
                var item = new Grid
                {
                    Height = 44,
                    Margin = new Thickness(0, 0, 0, 4),
                    Cursor = Cursors.Hand
                };

                var itemBg = new Border
                {
                    CornerRadius = new CornerRadius(8),
                    Background = Brushes.Transparent
                };
                item.Children.Add(itemBg);

                var strip = new Rectangle
                {
                    Width = 4,
                    Height = 22,
                    RadiusX = 2, RadiusY = 2,
                    Fill = RedBrush,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(2, 0, 0, 0),
                    Visibility = Visibility.Collapsed
                };
                item.Children.Add(strip);

                var contentRow = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(16, 0, 0, 0)
                };

                var iconTb = new TextBlock
                {
                    Text = icons[i],
                    FontSize = 14,
                    Foreground = TextSecondary,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 10, 0)
                };
                var nameTb = new TextBlock
                {
                    Text = names[i],
                    Foreground = TextSecondary,
                    FontSize = 13,
                    FontWeight = FontWeights.Medium,
                    VerticalAlignment = VerticalAlignment.Center
                };

                contentRow.Children.Add(iconTb);
                contentRow.Children.Add(nameTb);
                item.Children.Add(contentRow);

                int idx = i;
                item.MouseLeftButtonUp += (s, e) => SelectPage(idx);
                item.MouseEnter += (s, e) =>
                {
                    if (_currentPage != idx) itemBg.Background = new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF));
                };
                item.MouseLeave += (s, e) =>
                {
                    if (_currentPage != idx) itemBg.Background = Brushes.Transparent;
                };

                menuStack.Children.Add(item);
                _menuItems[i] = item;
                _menuStrips[i] = strip;
                _menuTexts[i] = nameTb;
                _menuIcons[i] = iconTb;
            }
            mainGrid.Children.Add(menuStack);

            // Alt Bilgi
            var footer = new Border
            {
                Padding = new Thickness(16, 12, 16, 12),
                BorderBrush = PanelEdgeBrush,
                BorderThickness = new Thickness(0, 1, 0, 0)
            };
            Grid.SetRow(footer, 2);
            var footerStack = new StackPanel();
            footerStack.Children.Add(new TextBlock
            {
                Text = "Y520-15IKBN",
                Foreground = TextSecondary,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold
            });
            footerStack.Children.Add(new TextBlock
            {
                Text = "v2.0 • .NET Framework",
                Foreground = TextMutedBrush,
                FontSize = 9,
                Margin = new Thickness(0, 2, 0, 0)
            });
            footer.Child = footerStack;
            mainGrid.Children.Add(footer);

            outer.Child = mainGrid;
            return outer;
        }

        public int PageCount { get { return _pages.Length; } }

        public void SelectPage(int idx)
        {
            if (idx == _currentPage) return;
            _currentPage = idx;
            for (int i = 0; i < _pages.Length; i++)
            {
                _pages[i].Visibility = i == idx ? Visibility.Visible : Visibility.Collapsed;
                _menuStrips[i].Visibility = i == idx ? Visibility.Visible : Visibility.Collapsed;
                var bg = _menuItems[i].Children[0] as Border;
                if (bg != null)
                {
                    bg.Background = i == idx ? SelBrush : Brushes.Transparent;
                }
                _menuTexts[i].Foreground = i == idx ? TextPrimary : TextSecondary;
                _menuTexts[i].FontWeight = i == idx ? FontWeights.Bold : FontWeights.Medium;
                _menuIcons[i].Foreground = i == idx ? RedGlowBrush : TextSecondary;
            }
        }

        private static void ApplyDarkScrollStyle(ScrollViewer sv)
        {
            string xaml = @"
            <Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                   xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                   TargetType='ScrollBar'>
                <Setter Property='Background' Value='Transparent'/>
                <Setter Property='Width' Value='6'/>
                <Setter Property='Template'>
                    <Setter.Value>
                        <ControlTemplate TargetType='ScrollBar'>
                            <Grid Background='Transparent'>
                                <Track x:Name='PART_Track' IsDirectionReversed='true'>
                                    <Track.Thumb>
                                        <Thumb>
                                            <Thumb.Template>
                                                <ControlTemplate TargetType='Thumb'>
                                                    <Border CornerRadius='3' Background='#333A48' Margin='1,0,1,0'/>
                                                </ControlTemplate>
                                            </Thumb.Template>
                                        </Thumb>
                                    </Track.Thumb>
                                </Track>
                            </Grid>
                        </ControlTemplate>
                    </Setter.Value>
                </Setter>
            </Style>";
            var style = (Style)System.Windows.Markup.XamlReader.Parse(xaml);
            sv.Resources.Add(typeof(ScrollBar), style);
        }

        // ---------------- 1. Sayfa: FAN KONTROL ----------------
        private Grid BuildFanPage()
        {
            var page = new Grid();
            var sp = new StackPanel();

            // HERO CARD: Extreme Cooling
            sp.Children.Add(BuildExtremeCoolingHeroCard());

            // AUTO MODE CARD
            sp.Children.Add(BuildAutoModeCard());

            // DUAL FAN SPEED CARDS
            sp.Children.Add(BuildDualFanDashboard());

            // TEMPERATURE TELEMETRY CARDS
            sp.Children.Add(BuildTelemetryGrid());

            var sv = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = sp
            };
            ApplyDarkScrollStyle(sv);
            page.Children.Add(sv);
            return page;
        }

        private UIElement BuildExtremeCoolingHeroCard()
        {
            var cardGrid = new Grid();
            cardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            cardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var topRow = new Grid();
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Ikon Rozeti
            var iconBadge = new Border
            {
                Width = 46, Height = 46,
                CornerRadius = new CornerRadius(12),
                Background = new LinearGradientBrush(
                    Color.FromRgb(0x4A, 0x0D, 0x0A),
                    Color.FromRgb(0x28, 0x06, 0x05),
                    new Point(0, 0), new Point(1, 1)),
                BorderBrush = RedDarkBrush,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 14, 0),
                Effect = SoftShadow(8, 2, 0.5)
            };
            iconBadge.Child = new TextBlock
            {
                Text = "⚡",
                FontSize = 22,
                Foreground = RedGlowBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            topRow.Children.Add(iconBadge);

            // Metinler
            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(textStack, 1);
            textStack.Children.Add(new TextBlock
            {
                Text = "EXTREME COOLING",
                Foreground = TextPrimary,
                FontSize = 16,
                FontWeight = FontWeights.Bold
            });
            _heroDescText = new TextBlock
            {
                Text = Lang.T("hero.desc.manual"),
                Foreground = TextSecondary,
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 0)
            };
            textStack.Children.Add(_heroDescText);
            topRow.Children.Add(textStack);

            // Switch
            _extremeSwitch = new ModernSwitch();
            _extremeSwitch.StateChanged += (on) =>
            {
                if (!_autoMode) ToggleCooling(on);
            };
            Grid.SetColumn(_extremeSwitch, 2);
            topRow.Children.Add(_extremeSwitch);

            cardGrid.Children.Add(topRow);

            // Alt durum cubugu
            var banner = new Border
            {
                Margin = new Thickness(0, 12, 0, 0),
                Padding = new Thickness(10, 6, 10, 6),
                CornerRadius = new CornerRadius(6),
                Background = SubCardBrush,
                BorderBrush = PanelEdgeBrush,
                BorderThickness = new Thickness(1)
            };
            _heroStatusBadge = new TextBlock
            {
                Text = Lang.T("hero.badge.init"),
                Foreground = TextSecondary,
                FontSize = 11,
                FontWeight = FontWeights.Medium
            };
            banner.Child = _heroStatusBadge;
            Grid.SetRow(banner, 1);
            cardGrid.Children.Add(banner);

            _heroCardBorder = MakeCard(cardGrid, padding: new Thickness(16, 14, 16, 14));
            return _heroCardBorder;
        }

        private UIElement BuildAutoModeCard()
        {
            var box = new StackPanel();

            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var iconBadge = new Border
            {
                Width = 38, Height = 38,
                CornerRadius = new CornerRadius(10),
                Background = new LinearGradientBrush(
                    Color.FromRgb(0x35, 0x20, 0x08),
                    Color.FromRgb(0x1C, 0x12, 0x05),
                    new Point(0, 0), new Point(1, 1)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x78, 0x48, 0x10)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 12, 0)
            };
            iconBadge.Child = new TextBlock
            {
                Text = "🌡",
                FontSize = 18,
                Foreground = OrangeBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            headerGrid.Children.Add(iconBadge);

            var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(titleStack, 1);
            titleStack.Children.Add(new TextBlock
            {
                Text = Lang.T("auto.title"),
                Foreground = TextPrimary,
                FontSize = 14,
                FontWeight = FontWeights.Bold
            });
            titleStack.Children.Add(new TextBlock
            {
                Text = Lang.T("auto.desc"),
                Foreground = TextSecondary,
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 0)
            });
            headerGrid.Children.Add(titleStack);

            _autoSwitch = new ModernSwitch();
            _autoSwitch.StateChanged += (on) => SetAutoMode(on);
            Grid.SetColumn(_autoSwitch, 2);
            headerGrid.Children.Add(_autoSwitch);
            box.Children.Add(headerGrid);

            // Ayırıcı çizgi
            box.Children.Add(new Border
            {
                Height = 1,
                Background = PanelEdgeBrush,
                Margin = new Thickness(0, 12, 0, 12)
            });

            // Slider Alanı
            var sliderContainer = new Grid();
            sliderContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            sliderContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var sliderColumn = new StackPanel();
            var sliderHeader = new Grid();
            sliderHeader.Children.Add(new TextBlock
            {
                Text = Lang.T("auto.slider"),
                Foreground = TextSecondary,
                FontSize = 12,
                FontWeight = FontWeights.Medium
            });
            sliderColumn.Children.Add(sliderHeader);

            _thresholdSlider = new Slider
            {
                Minimum = 50,
                Maximum = 95,
                TickFrequency = 1,
                IsSnapToTickEnabled = true,
                Value = _threshold,
                Height = 28,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 6, 0, 0)
            };
            _thresholdSlider.ValueChanged += (s, e) =>
            {
                _threshold = (int)_thresholdSlider.Value;
                if (_thresholdValue != null) _thresholdValue.Text = _threshold + " °C";
                SaveSettings();
            };
            StyleSlider(_thresholdSlider);
            sliderColumn.Children.Add(_thresholdSlider);

            // Min/Max göstergeleri
            var scaleRow = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            scaleRow.Children.Add(new TextBlock
            {
                Text = "50 °C",
                Foreground = TextMutedBrush,
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Left
            });
            scaleRow.Children.Add(new TextBlock
            {
                Text = Lang.T("auto.recommended"),
                Foreground = TextMutedBrush,
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            scaleRow.Children.Add(new TextBlock
            {
                Text = "95 °C",
                Foreground = TextMutedBrush,
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Right
            });
            sliderColumn.Children.Add(scaleRow);

            sliderContainer.Children.Add(sliderColumn);

            // Büyük Derece Rozeti
            var badgeBorder = new Border
            {
                Background = SubCardBrush,
                CornerRadius = new CornerRadius(10),
                BorderBrush = PanelEdgeBrush,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(16, 8, 16, 8),
                Margin = new Thickness(16, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            _thresholdValue = new TextBlock
            {
                Text = _threshold + " °C",
                Foreground = RedBrush,
                FontSize = 24,
                FontWeight = FontWeights.Black,
                TextAlignment = TextAlignment.Center
            };
            badgeBorder.Child = _thresholdValue;
            Grid.SetColumn(badgeBorder, 1);
            sliderContainer.Children.Add(badgeBorder);

            box.Children.Add(sliderContainer);

            // Histerezis Notu
            box.Children.Add(new TextBlock
            {
                Text = Lang.T("auto.note"),
                Foreground = TextMutedBrush,
                FontSize = 11,
                Margin = new Thickness(0, 10, 0, 0)
            });

            return MakeCard(box);
        }

        private static void StyleSlider(Slider s)
        {
            s.Orientation = Orientation.Horizontal;
            string xaml = @"
            <ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                             xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                             TargetType='Slider'>
                <Grid VerticalAlignment='Center'>
                    <Border Height='6' CornerRadius='3' Background='#151820' BorderBrush='#2A303C' BorderThickness='1' />
                    <Track x:Name='PART_Track'>
                        <Track.Thumb>
                            <Thumb Cursor='Hand'>
                                <Thumb.Template>
                                    <ControlTemplate TargetType='Thumb'>
                                        <Border Width='18' Height='18' CornerRadius='9' Background='#FFFFFF' BorderBrush='#E2231A' BorderThickness='3.5'>
                                            <Border.Effect>
                                                <DropShadowEffect BlurRadius='8' ShadowDepth='2' Color='#000000' Opacity='0.6'/>
                                            </Border.Effect>
                                        </Border>
                                    </ControlTemplate>
                                </Thumb.Template>
                            </Thumb>
                        </Track.Thumb>
                    </Track>
                </Grid>
            </ControlTemplate>";
            s.Template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(xaml);
        }

        private UIElement BuildDualFanDashboard()
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var fan1Card = BuildFanCard("FAN 1 (CPU)", Lang.T("fan1.sub"), out _fan1RpmText, out _fan1PctText, out _fan1Bar);
            var fan2Card = BuildFanCard("FAN 2 (GPU)", Lang.T("fan2.sub"), out _fan2RpmText, out _fan2PctText, out _fan2Bar);

            Grid.SetColumn(fan1Card, 0);
            Grid.SetColumn(fan2Card, 2);

            grid.Children.Add(fan1Card);
            grid.Children.Add(fan2Card);
            return grid;
        }

        private Border BuildFanCard(string title, string subtitle, out TextBlock rpmText, out TextBlock pctText, out Rectangle bar)
        {
            var sp = new StackPanel();

            var topRow = new Grid();
            var titleStack = new StackPanel();
            titleStack.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = TextPrimary,
                FontSize = 13,
                FontWeight = FontWeights.Bold
            });
            titleStack.Children.Add(new TextBlock
            {
                Text = subtitle,
                Foreground = TextMutedBrush,
                FontSize = 10
            });
            topRow.Children.Add(titleStack);

            pctText = new TextBlock
            {
                Text = "0%",
                Foreground = TextSecondary,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            topRow.Children.Add(pctText);
            sp.Children.Add(topRow);

            rpmText = new TextBlock
            {
                Text = "— RPM",
                Foreground = TextPrimary,
                FontSize = 26,
                FontWeight = FontWeights.Black,
                Margin = new Thickness(0, 4, 0, 6)
            };
            sp.Children.Add(rpmText);

            var track = new Border
            {
                Background = TrackBrush,
                BorderBrush = PanelEdgeBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Height = 8,
                SnapsToDevicePixels = true
            };
            var g = new Grid();
            bar = new Rectangle
            {
                Fill = new LinearGradientBrush(
                    Color.FromRgb(0xFF, 0x55, 0x4D),
                    Color.FromRgb(0xE2, 0x23, 0x1A),
                    new Point(0, 0), new Point(1, 0)),
                RadiusX = 4, RadiusY = 4,
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = 0
            };
            g.Children.Add(bar);
            track.Child = g;

            var capturedBar = bar;
            track.SizeChanged += (s, e) => UpdateBarWidth(track, capturedBar);
            sp.Children.Add(track);

            bar.Tag = new BarInfo { Track = track, Rpm = 0 };

            return MakeCard(sp, margin: new Thickness(0), padding: new Thickness(16, 12, 16, 14));
        }

        private class BarInfo { public Border Track; public uint Rpm; }

        private void UpdateBarWidth(Border track, Rectangle bar)
        {
            var info = bar.Tag as BarInfo;
            if (info == null) return;
            double w = track.ActualWidth;
            if (w <= 0) return;
            double ratio = Math.Min(1.0, info.Rpm / (double)MaxRpm);
            bar.Width = w * ratio;
        }

        private void SetBar(Rectangle bar, TextBlock pctText, uint rpm)
        {
            var info = bar.Tag as BarInfo;
            if (info == null) return;
            info.Rpm = rpm;
            UpdateBarWidth(info.Track, bar);
            int pct = (int)Math.Min(100, Math.Round((rpm / (double)MaxRpm) * 100));
            if (pctText != null) pctText.Text = pct + "%";
        }

        private UIElement BuildTelemetryGrid()
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var cpuCard = BuildTelemetryCard(Lang.T("temp.cpu"), Lang.T("temp.cpu.sub"), "⚡", CyanBrush, out _cpuTempVal, out _cpuTempBadge);
            var gpuCard = BuildTelemetryCard(Lang.T("temp.gpu"), "GTX 1050 / Intel", "🎮", GreenBrush, out _gpuTempVal, out _gpuTempBadge);
            var irCard = BuildTelemetryCard(Lang.T("temp.ir"), Lang.T("temp.ir.sub"), "🌡", OrangeBrush, out _irTempVal, out _irTempBadge);

            Grid.SetColumn(cpuCard, 0);
            Grid.SetColumn(gpuCard, 2);
            Grid.SetColumn(irCard, 4);

            grid.Children.Add(cpuCard);
            grid.Children.Add(gpuCard);
            grid.Children.Add(irCard);
            return grid;
        }

        private Border BuildTelemetryCard(string title, string subtitle, string icon, Brush iconColor, out TextBlock valText, out Border badge)
        {
            var sp = new StackPanel();

            var topRow = new Grid();
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var iconBox = new Border
            {
                Width = 24, Height = 24,
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x35, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 8, 0)
            };
            iconBox.Child = new TextBlock
            {
                Text = icon,
                FontSize = 13,
                Foreground = iconColor ?? TextPrimary,
                FontFamily = new FontFamily("Segoe UI Emoji, Segoe UI Symbol, Arial"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            topRow.Children.Add(iconBox);

            var titleTb = new TextBlock
            {
                Text = title,
                Foreground = TextSecondary,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(titleTb, 1);
            topRow.Children.Add(titleTb);
            sp.Children.Add(topRow);

            valText = new TextBlock
            {
                Text = "— °C",
                Foreground = TextPrimary,
                FontSize = 22,
                FontWeight = FontWeights.Black,
                Margin = new Thickness(0, 6, 0, 4)
            };
            sp.Children.Add(valText);

            var subTb = new TextBlock
            {
                Text = subtitle,
                Foreground = TextMutedBrush,
                FontSize = 10
            };
            sp.Children.Add(subTb);

            badge = MakeCard(sp, margin: new Thickness(0), padding: new Thickness(14, 12, 14, 12));
            return badge;
        }

        private void SetTempBadge(TextBlock valText, Border card, float? temp)
        {
            if (!temp.HasValue || temp.Value <= 0)
            {
                valText.Text = "—";
                valText.Foreground = TextSecondary;
                card.BorderBrush = PanelEdgeBrush;
                return;
            }

            int t = (int)Math.Round(temp.Value);
            valText.Text = t + " °C";

            if (t >= 80)
            {
                valText.Foreground = RedGlowBrush;
                card.BorderBrush = new SolidColorBrush(Color.FromArgb(0x60, 0xE2, 0x23, 0x1A));
            }
            else if (t >= 65)
            {
                valText.Foreground = OrangeBrush;
                card.BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0xF5, 0x9E, 0x0B));
            }
            else
            {
                valText.Foreground = CyanBrush;
                card.BorderBrush = PanelEdgeBrush;
            }
        }

        private void SetAutoMode(bool on)
        {
            _autoMode = on;
            _autoSwitch.SetCheckedQuietly(on);

            if (_extremeSwitch != null)
            {
                _extremeSwitch.IsEnabled = !on;
                _extremeSwitch.Opacity = on ? 0.45 : 1.0;
            }

            if (_heroDescText != null)
            {
                _heroDescText.Text = on
                    ? Lang.T("hero.desc.auto")
                    : Lang.T("hero.desc.manual");
                _heroDescText.Foreground = on ? OrangeBrush : TextSecondary;
            }

            SaveSettings();
        }

        // ---------------- 2. Sayfa: DONANIM BİLGİSİ ----------------
        private Grid BuildHardwarePage()
        {
            var page = new Grid();
            var sp = new StackPanel();

            sp.Children.Add(new TextBlock
            {
                Text = Lang.T("hw.title"),
                Foreground = TextPrimary,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(2, 0, 0, 12)
            });

            _hwContainer = new StackPanel();
            _hwContainer.Children.Add(MakeCard(new TextBlock
            {
                Text = Lang.T("hw.scanning"),
                Foreground = TextSecondary,
                FontSize = 13
            }));

            sp.Children.Add(_hwContainer);

            var sv = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = sp
            };
            ApplyDarkScrollStyle(sv);
            page.Children.Add(sv);
            return page;
        }

        private void LoadHardwareInfo()
        {
            Task.Run(() =>
            {
                try
                {
                    string cpu = WmiSingle("Win32_Processor", "Name");
                    var gpus = WmiMulti("Win32_VideoController", "Name");
                    string model = WmiSingle("Win32_ComputerSystem", "Model");
                    string ram = FormatRam();
                    string bios = WmiSingle("Win32_BIOS", "SMBIOSBIOSVersion");

                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        _hwContainer.Children.Clear();

                        _hwContainer.Children.Add(MakeSpecCard(Lang.T("hw.cpu"), cpu, "⚡", Lang.T("hw.cpu.extra")));

                        string gpuInfo = string.Join("\n", gpus.Select((g, i) => (i == 0 ? Lang.T("hw.gpu.ext") : Lang.T("hw.gpu.int")) + g).ToArray());
                        _hwContainer.Children.Add(MakeSpecCard(Lang.T("hw.gpu"), gpuInfo, "🎮", "NVIDIA GeForce & Intel HD Graphics"));

                        _hwContainer.Children.Add(MakeSpecCard(Lang.T("hw.ram"), ram + " DDR4", "💾", Lang.T("hw.ram.extra")));

                        _hwContainer.Children.Add(MakeSpecCard(Lang.T("hw.model"), "Lenovo " + model, "💻", Lang.T("hw.model.extra")));

                        _hwContainer.Children.Add(MakeSpecCard(Lang.T("hw.bios"), Lang.F("hw.bios.value", bios), "⚙️", Lang.T("hw.bios.extra")));
                    }));
                }
                catch (Exception ex)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        _hwContainer.Children.Clear();
                        _hwContainer.Children.Add(MakeCard(new TextBlock
                        {
                            Text = Lang.F("hw.error", ex.Message),
                            Foreground = RedBrush
                        }));
                    }));
                }
            });
        }

        private Border MakeSpecCard(string title, string value, string icon, string extra)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var iconBox = new Border
            {
                Width = 42, Height = 42,
                CornerRadius = new CornerRadius(10),
                Background = new LinearGradientBrush(
                    Color.FromRgb(0x1C, 0x22, 0x2D),
                    Color.FromRgb(0x12, 0x16, 0x1F),
                    new Point(0, 0), new Point(1, 1)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x35, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 14, 0)
            };
            iconBox.Child = new TextBlock
            {
                Text = icon,
                FontSize = 18,
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Segoe UI Emoji, Segoe UI Symbol, Arial"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            grid.Children.Add(iconBox);

            var sp = new StackPanel();
            sp.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = TextSecondary,
                FontSize = 11,
                FontWeight = FontWeights.Bold
            });
            sp.Children.Add(new TextBlock
            {
                Text = value,
                Foreground = TextPrimary,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 2)
            });
            sp.Children.Add(new TextBlock
            {
                Text = extra,
                Foreground = TextMutedBrush,
                FontSize = 10
            });
            Grid.SetColumn(sp, 1);
            grid.Children.Add(sp);

            return MakeCard(grid, padding: new Thickness(16, 12, 16, 12));
        }

        private static string WmiSingle(string cls, string prop)
        {
            using (var s = new ManagementObjectSearcher("SELECT " + prop + " FROM " + cls))
            using (var res = s.Get())
            {
                foreach (ManagementObject mo in res)
                {
                    var v = mo[prop];
                    if (v != null) return v.ToString().Trim();
                }
            }
            return "—";
        }

        private static List<string> WmiMulti(string cls, string prop)
        {
            var list = new List<string>();
            using (var s = new ManagementObjectSearcher("SELECT " + prop + " FROM " + cls))
            using (var res = s.Get())
            {
                foreach (ManagementObject mo in res)
                {
                    var v = mo[prop];
                    if (v != null) list.Add(v.ToString().Trim());
                }
            }
            return list;
        }

        private static string FormatRam()
        {
            try
            {
                using (var s = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
                using (var res = s.Get())
                {
                    foreach (ManagementObject mo in res)
                    {
                        double bytes = Convert.ToDouble(mo["TotalPhysicalMemory"]);
                        return string.Format("{0:0} GB", Math.Round(bytes / (1024.0 * 1024 * 1024)));
                    }
                }
            }
            catch { }
            return "—";
        }

        // ---------------- 3. Sayfa: SİSTEM ARAÇLARI ----------------
        private Grid BuildSystemPage()
        {
            var page = new Grid();
            var sp = new StackPanel();

            sp.Children.Add(new TextBlock
            {
                Text = Lang.T("sys.title"),
                Foreground = TextPrimary,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(2, 0, 0, 12)
            });

            _winKeySwitch = new ModernSwitch();
            _winKeySwitch.StateChanged += (on) => ToggleLock(true, on);
            sp.Children.Add(BuildSystemToolCard(
                Lang.T("sys.winkey.title"),
                Lang.T("sys.winkey.desc"),
                "🔲", _winKeySwitch));

            _tpSwitch = new ModernSwitch();
            _tpSwitch.StateChanged += (on) => ToggleLock(false, on);
            sp.Children.Add(BuildSystemToolCard(
                Lang.T("sys.tp.title"),
                Lang.T("sys.tp.desc"),
                "🖱️", _tpSwitch));

            _startupSwitch = new ModernSwitch();
            _startupSwitch.StateChanged += (on) => ToggleStartup(on);
            sp.Children.Add(BuildSystemToolCard(
                Lang.T("sys.startup.title"),
                Lang.T("sys.startup.desc"),
                "🚀", _startupSwitch));
            _startupSwitch.SetCheckedQuietly(IsStartupEnabled());

            sp.Children.Add(BuildLanguageCard());

            page.Children.Add(sp);
            return page;
        }

        // ---------------- Baslangicta Calistirma (Gorev Zamanlayici) ----------------
        private const string StartupTaskName = "LegionFanControl";

        private static bool IsStartupEnabled()
        {
            try
            {
                var p = Process.Start(new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = "/Query /TN \"" + StartupTaskName + "\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });
                if (p == null) return false;
                p.WaitForExit();
                return p.ExitCode == 0;
            }
            catch { return false; }
        }

        private void ToggleStartup(bool on)
        {
            try
            {
                string exe = typeof(MainWindow).Assembly.Location;
                string args = on
                    ? "/Create /TN \"" + StartupTaskName + "\" /TR \"\\\"" + exe + "\\\"\" /SC ONLOGON /RL HIGHEST /F"
                    : "/Delete /TN \"" + StartupTaskName + "\" /F";
                var p = Process.Start(new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = args,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });
                if (p != null)
                {
                    p.WaitForExit();
                    if (p.ExitCode != 0) throw new Exception("schtasks hata kodu: " + p.ExitCode);
                }
                SetStatus(on ? Lang.T("startup.on") : Lang.T("startup.off"), false);
            }
            catch (Exception ex)
            {
                _startupSwitch.SetCheckedQuietly(IsStartupEnabled());
                SetStatus(Lang.F("startup.fail", ex.Message), true);
            }
        }

        // ---------------- Dil Secimi ----------------
        private Border BuildLanguageCard()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var iconBox = new Border
            {
                Width = 42, Height = 42,
                CornerRadius = new CornerRadius(10),
                Background = new LinearGradientBrush(
                    Color.FromRgb(0x1C, 0x22, 0x2D),
                    Color.FromRgb(0x12, 0x16, 0x1F),
                    new Point(0, 0), new Point(1, 1)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x35, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 14, 0)
            };
            iconBox.Child = new TextBlock
            {
                Text = "🌐",
                FontSize = 18,
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Segoe UI Emoji, Segoe UI Symbol, Arial"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            grid.Children.Add(iconBox);

            var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(new TextBlock
            {
                Text = Lang.T("lang.title"),
                Foreground = TextPrimary,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold
            });
            sp.Children.Add(new TextBlock
            {
                Text = Lang.T("lang.desc"),
                Foreground = TextSecondary,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0)
            });
            Grid.SetColumn(sp, 1);
            grid.Children.Add(sp);

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            btnPanel.Children.Add(MakeLangButton("tr", "Türkçe"));
            btnPanel.Children.Add(MakeLangButton("en", "English"));
            Grid.SetColumn(btnPanel, 2);
            grid.Children.Add(btnPanel);

            return MakeCard(grid, padding: new Thickness(16, 14, 16, 14));
        }

        private Border MakeLangButton(string lang, string label)
        {
            bool active = Lang.Current == lang;
            var txt = new TextBlock
            {
                Text = label,
                Foreground = active ? Brushes.White : TextSecondary,
                FontSize = 12,
                FontWeight = active ? FontWeights.Bold : FontWeights.Medium,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var btn = new Border
            {
                Padding = new Thickness(14, 7, 14, 7),
                Margin = new Thickness(lang == "tr" ? 0 : 8, 0, 0, 0),
                CornerRadius = new CornerRadius(8),
                Background = active ? RedBrush : SubCardBrush,
                BorderBrush = active ? RedGlowBrush : PanelEdgeBrush,
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                Child = txt
            };
            btn.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                ChangeLanguage(lang);
            };
            return btn;
        }

        private void ChangeLanguage(string lang)
        {
            if (Lang.Current == lang) return;
            Lang.Current = lang;
            int page = _currentPage;
            SaveSettings();

            // Tum arayuzu yeni dilde yeniden kur
            BuildUi();
            Title = Lang.T("app.title");
            SetAutoMode(_autoMode);
            _currentPage = -1;
            SelectPage(page < 0 ? 0 : page);
            LoadHardwareInfo();
            RefreshSysStates();
            UpdateTrayLanguage();
            Poll();
        }

        private void UpdateTrayLanguage()
        {
            if (_tray == null) return;
            _tray.Text = Lang.T("tray.tip");
            if (_trayShowItem != null) _trayShowItem.Text = Lang.T("tray.show");
            if (_trayToggleItem != null)
                _trayToggleItem.Text = (_extremeSwitch != null && _extremeSwitch.IsChecked)
                    ? Lang.T("tray.cool.off") : Lang.T("tray.cool.on");
            if (_trayExitItem != null) _trayExitItem.Text = Lang.T("tray.exit");
        }

        // ---------------- 4. Sayfa: HAKKINDA ----------------
        private const string GitHubUrl = "https://github.com/fatih5228/LegionFanControl";

        private Grid BuildAboutPage()
        {
            var page = new Grid();
            var sp = new StackPanel();

            sp.Children.Add(new TextBlock
            {
                Text = Lang.T("about.title"),
                Foreground = TextPrimary,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(2, 0, 0, 12)
            });

            // Uygulama Karti
            var appGrid = new Grid();
            appGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            appGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var iconBadge = new Border
            {
                Width = 56, Height = 56,
                CornerRadius = new CornerRadius(14),
                Background = new LinearGradientBrush(
                    Color.FromRgb(0x4A, 0x0D, 0x0A),
                    Color.FromRgb(0x28, 0x06, 0x05),
                    new Point(0, 0), new Point(1, 1)),
                BorderBrush = RedDarkBrush,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 14, 0),
                Effect = SoftShadow(8, 2, 0.5)
            };
            try
            {
                string ico = IOPath.Combine(BaseDir, "app.ico");
                if (File.Exists(ico))
                {
                    iconBadge.Child = new Image
                    {
                        Source = LoadIconImage(ico, 40),
                        Width = 36, Height = 36,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                }
                else
                {
                    iconBadge.Child = new TextBlock
                    {
                        Text = "⚡",
                        FontSize = 26,
                        Foreground = RedGlowBrush,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                }
            }
            catch { }
            appGrid.Children.Add(iconBadge);

            var appStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            appStack.Children.Add(new TextBlock
            {
                Text = Lang.T("app.title"),
                Foreground = TextPrimary,
                FontSize = 16,
                FontWeight = FontWeights.Bold
            });
            appStack.Children.Add(new TextBlock
            {
                Text = Lang.T("about.version"),
                Foreground = TextSecondary,
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 0)
            });
            appStack.Children.Add(new TextBlock
            {
                Text = Lang.T("about.desc"),
                Foreground = TextMutedBrush,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            });
            Grid.SetColumn(appStack, 1);
            appGrid.Children.Add(appStack);
            sp.Children.Add(MakeCard(appGrid));

            // Gelistirici Karti
            var devStack = new StackPanel();
            devStack.Children.Add(new TextBlock
            {
                Text = Lang.T("about.dev"),
                Foreground = TextSecondary,
                FontSize = 11,
                FontWeight = FontWeights.Bold
            });
            devStack.Children.Add(new TextBlock
            {
                Text = "Fatih (fatih5228)",
                Foreground = TextPrimary,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 4, 0, 0)
            });
            sp.Children.Add(MakeCard(devStack));

            // GitHub Baglanti Karti
            var linkGrid = new Grid();
            linkGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            linkGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var ghBadge = new Border
            {
                Width = 42, Height = 42,
                CornerRadius = new CornerRadius(10),
                Background = new LinearGradientBrush(
                    Color.FromRgb(0x1C, 0x22, 0x2D),
                    Color.FromRgb(0x12, 0x16, 0x1F),
                    new Point(0, 0), new Point(1, 1)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x35, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 14, 0)
            };
            ghBadge.Child = new TextBlock
            {
                Text = "🐙",
                FontSize = 18,
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Segoe UI Emoji, Segoe UI Symbol, Arial"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            linkGrid.Children.Add(ghBadge);

            var linkStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            linkStack.Children.Add(new TextBlock
            {
                Text = Lang.T("about.gh"),
                Foreground = TextSecondary,
                FontSize = 11,
                FontWeight = FontWeights.Bold
            });
            var linkTb = new TextBlock { Margin = new Thickness(0, 4, 0, 0) };
            var link = new System.Windows.Documents.Hyperlink(
                new System.Windows.Documents.Run(GitHubUrl))
            {
                Foreground = CyanBrush,
                FontSize = 13,
                TextDecorations = null,
                Cursor = Cursors.Hand,
                NavigateUri = new Uri(GitHubUrl)
            };
            link.RequestNavigate += (s, e) =>
            {
                try { Process.Start(e.Uri.AbsoluteUri); } catch { }
            };
            linkTb.Inlines.Add(link);
            linkStack.Children.Add(linkTb);
            linkStack.Children.Add(new TextBlock
            {
                Text = Lang.T("about.ghnote"),
                Foreground = TextMutedBrush,
                FontSize = 10,
                Margin = new Thickness(0, 4, 0, 0)
            });
            Grid.SetColumn(linkStack, 1);
            linkGrid.Children.Add(linkStack);
            sp.Children.Add(MakeCard(linkGrid));

            page.Children.Add(sp);
            return page;
        }

        private Border BuildSystemToolCard(string title, string desc, string icon, ModernSwitch sw)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var iconBox = new Border
            {
                Width = 42, Height = 42,
                CornerRadius = new CornerRadius(10),
                Background = new LinearGradientBrush(
                    Color.FromRgb(0x1C, 0x22, 0x2D),
                    Color.FromRgb(0x12, 0x16, 0x1F),
                    new Point(0, 0), new Point(1, 1)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x35, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 14, 0)
            };
            iconBox.Child = new TextBlock
            {
                Text = icon,
                FontSize = 18,
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Segoe UI Emoji, Segoe UI Symbol, Arial"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            grid.Children.Add(iconBox);

            var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = TextPrimary,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold
            });
            sp.Children.Add(new TextBlock
            {
                Text = desc,
                Foreground = TextSecondary,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0)
            });
            Grid.SetColumn(sp, 1);
            grid.Children.Add(sp);

            Grid.SetColumn(sw, 2);
            grid.Children.Add(sw);

            return MakeCard(grid, padding: new Thickness(16, 14, 16, 14));
        }

        private void RefreshSysStates()
        {
            Task.Run(() =>
            {
                bool? winKey = null, tp = null;
                try
                {
                    if (_hw == null) TryConnectHardware();
                    if (_hw != null)
                    {
                        winKey = _hw.GetWinKeyLock();
                        tp = _hw.GetTouchpadLock();
                    }
                }
                catch { }
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (winKey.HasValue && _winKeySwitch != null) _winKeySwitch.SetCheckedQuietly(winKey.Value);
                    if (tp.HasValue && _tpSwitch != null) _tpSwitch.SetCheckedQuietly(tp.Value);
                }));
            });
        }

        private void ToggleLock(bool winKey, bool targetState)
        {
            if (_hw == null)
            {
                TryConnectHardware();
                if (_hw == null) { SetStatus(Lang.F("status.connerror", _hwError), true); return; }
            }
            Task.Run(() =>
            {
                try
                {
                    if (winKey) _hw.SetWinKeyLock(targetState);
                    else _hw.SetTouchpadLock(targetState);

                    bool now = winKey ? _hw.GetWinKeyLock() : _hw.GetTouchpadLock();
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (winKey && _winKeySwitch != null) _winKeySwitch.SetCheckedQuietly(now);
                        if (!winKey && _tpSwitch != null) _tpSwitch.SetCheckedQuietly(now);
                    }));
                }
                catch (Exception ex)
                {
                    Dispatcher.BeginInvoke(new Action(() => SetStatus(Lang.F("status.error", ex.Message), true)));
                }
            });
        }

        // ---------------- Fan Kontrol & Polling ----------------
        private void ToggleCooling(bool on)
        {
            if (_autoMode) { SetStatus(Lang.T("auto.locked"), false); return; }
            if (_hw == null)
            {
                TryConnectHardware();
                if (_hw == null) { SetStatus(Lang.F("status.connerror", _hwError), true); return; }
            }
            Task.Run(() =>
            {
                try
                {
                    _hw.SetFanCooling(on);
                    bool now = _hw.GetFanCoolingStatus();
                    Dispatcher.BeginInvoke(new Action(() => ApplyCoolingState(now)));
                }
                catch (Exception ex)
                {
                    Dispatcher.BeginInvoke(new Action(() => SetStatus(Lang.F("status.error", ex.Message), true)));
                }
            });
        }

        private void ApplyCoolingState(bool on)
        {
            if (_extremeSwitch != null) _extremeSwitch.SetCheckedQuietly(on);

            if (_heroStatusBadge != null)
            {
                _heroStatusBadge.Text = on ? Lang.T("hero.status.on") : Lang.T("hero.status.off");
                _heroStatusBadge.Foreground = on ? RedGlowBrush : TextSecondary;
            }

            if (_heroCardBorder != null)
            {
                _heroCardBorder.BorderBrush = on ? new SolidColorBrush(Color.FromArgb(0x80, 0xE2, 0x23, 0x1A)) : PanelEdgeBrush;
            }

            if (_trayToggleItem != null)
                _trayToggleItem.Text = on ? Lang.T("tray.cool.off") : Lang.T("tray.cool.on");

            if (_tray != null)
            {
                var trayIcon = on ? (_iconOn ?? _iconOff) : (_iconOff ?? _iconOn);
                if (trayIcon != null) _tray.Icon = trayIcon;
            }
        }

        private void Poll()
        {
            if (_polling) return;
            _polling = true;
            Task.Run(() =>
            {
                uint fan1 = 0, fan2 = 0, cpuT = 0, irT = 0;
                bool cooling = false;
                string err = null;
                float? lhmCpu = null, lhmGpu = null;
                string lhmNote = null;

                try
                {
                    if (_hw == null) TryConnectHardware();
                    if (_hw == null)
                    {
                        err = _hwError ?? "Bağlantı yok";
                    }
                    else
                    {
                        fan1 = _hw.GetFan1Speed();
                        fan2 = _hw.GetFan2Speed();
                        cooling = _hw.GetFanCoolingStatus();
                        cpuT = _hw.GetCPUTemp();
                        irT = _hw.GetIRTemp();
                    }

                    _lhm.ReadTemps(out lhmCpu, out lhmGpu);

                    // Otomatik Mod Tetikleme
                    if (_autoMode && _hw != null && err == null)
                    {
                        float? refTemp = lhmCpu.HasValue ? lhmCpu : (irT > 0 ? (float?)irT : (float?)null);
                        if (refTemp.HasValue)
                        {
                            if (!cooling && refTemp.Value >= _threshold)
                            {
                                _hw.SetFanCooling(true);
                                cooling = _hw.GetFanCoolingStatus();
                            }
                            else if (cooling && refTemp.Value <= _threshold - 5)
                            {
                                _hw.SetFanCooling(false);
                                cooling = _hw.GetFanCoolingStatus();
                            }
                        }
                    }

                    var notes = new List<string>();
                    if (_lhm.CpuNote != "") notes.Add(_lhm.CpuNote);
                    if (_lhm.GpuNote != "") notes.Add(_lhm.GpuNote);
                    lhmNote = notes.Count > 0 ? string.Join(" • ", notes.ToArray()) : null;
                }
                catch (Exception ex)
                {
                    err = ex.Message;
                    _hw = null;
                }

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    _polling = false;
                    if (err != null)
                    {
                        SetStatus(Lang.F("status.connerror", err), true);
                        _fan1RpmText.Text = "— RPM";
                        _fan2RpmText.Text = "— RPM";
                        SetTempBadge(_cpuTempVal, _cpuTempBadge, null);
                        SetTempBadge(_gpuTempVal, _gpuTempBadge, null);
                        SetTempBadge(_irTempVal, _irTempBadge, null);
                        return;
                    }

                    ApplyCoolingState(cooling);
                    _fan1RpmText.Text = fan1 + " RPM";
                    _fan2RpmText.Text = fan2 + " RPM";
                    SetBar(_fan1Bar, _fan1PctText, fan1);
                    SetBar(_fan2Bar, _fan2PctText, fan2);

                    float? effCpu = lhmCpu.HasValue ? lhmCpu : (cpuT > 0 ? (float?)cpuT : (float?)null);
                    SetTempBadge(_cpuTempVal, _cpuTempBadge, effCpu);
                    SetTempBadge(_gpuTempVal, _gpuTempBadge, lhmGpu);
                    SetTempBadge(_irTempVal, _irTempBadge, irT > 0 ? (float?)irT : (float?)null);

                    SetStatus(Lang.F("status.lastupdate", DateTime.Now.ToString("HH:mm:ss")) +
                              (lhmNote != null ? "  •  " + lhmNote : ""), false);
                }));
            });
        }

        private void SetStatus(string msg, bool isError)
        {
            _statusText.Text = msg;
            _statusText.Foreground = isError ? RedBrush : TextSecondary;
            _statusDot.Fill = isError ? RedBrush : GreenBrush;
        }

        // ---------------- Sistem Tepsisi ----------------
        private void BuildTray()
        {
            _tray = new Forms.NotifyIcon();
            try
            {
                string icoOn = IOPath.Combine(BaseDir, "app.ico");
                string icoOff = IOPath.Combine(BaseDir, "app_off.ico");
                if (File.Exists(icoOn)) _iconOn = new Drawing.Icon(icoOn, 16, 16);
                if (File.Exists(icoOff)) _iconOff = new Drawing.Icon(icoOff, 16, 16);
            }
            catch { }

            _tray.Icon = _iconOn ?? _iconOff;
            _tray.Text = Lang.T("tray.tip");
            _tray.Visible = true;
            _tray.DoubleClick += (s, e) => ShowWindow();

            var menu = new Forms.ContextMenuStrip();
            _trayShowItem = new Forms.ToolStripMenuItem(Lang.T("tray.show"));
            _trayShowItem.Click += (s, e) => ShowWindow();
            menu.Items.Add(_trayShowItem);
            _trayToggleItem = new Forms.ToolStripMenuItem(Lang.T("tray.cool.on"));
            _trayToggleItem.Click += (s, e) =>
            {
                if (_extremeSwitch != null)
                {
                    ToggleCooling(!_extremeSwitch.IsChecked);
                }
            };
            menu.Items.Add(_trayToggleItem);
            menu.Items.Add(new Forms.ToolStripSeparator());
            _trayExitItem = new Forms.ToolStripMenuItem(Lang.T("tray.exit"));
            _trayExitItem.Click += (s, e) => RealExit();
            menu.Items.Add(_trayExitItem);
            _tray.ContextMenuStrip = menu;
        }

        private void ShowWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
            Topmost = true;
            Topmost = false;
            Focus();
        }

        private void RealExit()
        {
            _realExit = true;
            Close();
        }
    }

    public static class Program
    {
        private static System.Threading.Mutex _singleInstance;

        [STAThread]
        public static void Main(string[] args)
        {
            if (args != null && args.Length > 0 && args[0] == "--shot")
            {
                string outDir = IOPath.GetDirectoryName(typeof(MainWindow).Assembly.Location) ?? AppDomain.CurrentDomain.BaseDirectory;
                var shotApp = new Application();
                var shotWin = new MainWindow();
                shotWin.Show();
                shotWin.UpdateLayout();
                // Pump dispatcher to allow layout and initial loads
                var frame = new DispatcherFrame();
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new DispatcherOperationCallback(f => { ((DispatcherFrame)f).Continue = false; return null; }), frame);
                Dispatcher.PushFrame(frame);

                int w = (int)Math.Max(830, shotWin.ActualWidth);
                int h = (int)Math.Max(660, shotWin.ActualHeight);

                for (int i = 0; i < shotWin.PageCount; i++)
                {
                    shotWin.SelectPage(i);
                    shotWin.UpdateLayout();
                    var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
                    rtb.Render(shotWin);
                    var enc = new PngBitmapEncoder();
                    enc.Frames.Add(BitmapFrame.Create(rtb));
                    using (var fs = new FileStream(IOPath.Combine(outDir, "window_shot" + i + ".png"), FileMode.Create))
                    {
                        enc.Save(fs);
                    }
                }
                shotWin.Close();
                return;
            }

            bool createdNew;
            _singleInstance = new System.Threading.Mutex(true, "LegionFanControl_SingleInstance", out createdNew);
            if (!createdNew) return;

            var app = new Application();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var win = new MainWindow();
            app.MainWindow = win;
            win.Closed += (s, e) => app.Shutdown();
            win.Show();
            app.Run();
        }
    }
}
