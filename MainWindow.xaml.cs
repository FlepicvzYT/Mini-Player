using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Media.Control;

namespace MiniPlayer
{
    public partial class MainWindow : Window
    {
        private class Theme
        {
            public string Name = "";
            public Brush Back = Brushes.Transparent;
            public Brush Edge = Brushes.Transparent;
            public double EdgeWidth = 0;
            public double Corner = 16;
            public double CoverCorner = 16;
            public double ButtonCorner = 100;
            public double PillCorner = 6;
            public double MenuCorner = 10;
            public double ItemCorner = 6;
            public bool Shadow = false;
            public Brush Gloss = Brushes.Transparent;
            public Brush Title = Brushes.White;
            public Brush Artist = Brushes.Gray;
            public Brush Icon = Brushes.White;
            public Brush PlayIcon = Brushes.White;
            public Brush Close = Brushes.Gray;
            public Brush Pill = Brushes.Transparent;
            public Brush PlayBack = Brushes.Transparent;
            public Brush PlayEdge = Brushes.Transparent;
            public Brush Hover = Brushes.White;
            public Brush CoverBack = Brushes.Transparent;
            public Brush Dim = Brushes.Transparent;
            public Brush MenuBack = Brushes.Black;
            public Brush MenuText = Brushes.White;
            public Brush MenuEdge = Brushes.Gray;
        }

        private record LyricLine(TimeSpan Time, string Text);

        private class LyricsData
        {
            public List<LyricLine> Synced = new List<LyricLine>();
            public List<string> Plain = new List<string>();
            public bool Instrumental = false;
        }

        private static readonly HttpClient http = CreateClient();

        private GlobalSystemMediaTransportControlsSessionManager? manager;
        private GlobalSystemMediaTransportControlsSession? lastSession;
        private readonly DispatcherTimer timer = new DispatcherTimer();
        private readonly DispatcherTimer lyricsTimer = new DispatcherTimer();
        private readonly Dictionary<string, Theme> themes = BuildThemes();
        private readonly string themePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MiniPlayer",
            "theme.txt");
        private readonly List<TextBlock> lineBlocks = new List<TextBlock>();
        private List<LyricLine> syncedLines = new List<LyricLine>();
        private string currentTheme = "";
        private bool switching = false;
        private bool changingView = false;
        private bool lyricsOpen = false;
        private bool isSynced = false;
        private int activeIndex = -1;
        private int lyricsVersion = 0;
        private double plainOffset = 0;
        private DateTime popupClosedAt = DateTime.MinValue;
        private string lastTrack = "";
        private bool hovering = false;
        private bool isPlaying = false;
        private bool? lastPlaying = null;
        private bool refreshing = false;

        public MainWindow()
        {
            InitializeComponent();
            ApplyTheme(LoadTheme());
            Loaded += async (s, e) =>
            {
                BeginAnimation(OpacityProperty, Anim(1, 450));
                manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                timer.Interval = TimeSpan.FromMilliseconds(500);
                timer.Tick += async (a, b) => await RefreshAsync();
                timer.Start();
                lyricsTimer.Interval = TimeSpan.FromMilliseconds(200);
                lyricsTimer.Tick += (a, b) => UpdateLyricsPosition();
                lyricsTimer.Start();
                await RefreshAsync();
            };
        }

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("MiniPlayer/1.0");
            return client;
        }

        private static SolidColorBrush Solid(string hex)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
        }

        private static LinearGradientBrush Linear(double x1, double y1, double x2, double y2, params (string color, double offset)[] stops)
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(x1, y1),
                EndPoint = new Point(x2, y2)
            };

            foreach (var stop in stops)
            {
                brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(stop.color)!, stop.offset));
            }

            return brush;
        }

        private static RadialGradientBrush Radial(double cx, double cy, double rx, double ry, params (string color, double offset)[] stops)
        {
            var brush = new RadialGradientBrush
            {
                Center = new Point(cx, cy),
                GradientOrigin = new Point(cx, cy),
                RadiusX = rx,
                RadiusY = ry
            };

            foreach (var stop in stops)
            {
                brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(stop.color)!, stop.offset));
            }

            return brush;
        }

        private static Dictionary<string, Theme> BuildThemes()
        {
            var list = new Dictionary<string, Theme>();

            list["sleek"] = new Theme
            {
                Name = "Sleek",
                Back = Solid("#121212"),
                Edge = Brushes.Transparent,
                EdgeWidth = 0,
                Corner = 16,
                Gloss = Brushes.Transparent,
                Title = Solid("#FFFFFF"),
                Artist = Solid("#888888"),
                Icon = Solid("#FFFFFF"),
                PlayIcon = Solid("#FFFFFF"),
                Close = Solid("#888888"),
                Pill = Solid("#22FFFFFF"),
                PlayBack = Solid("#99000000"),
                PlayEdge = Brushes.Transparent,
                Hover = Solid("#FFFFFF"),
                CoverBack = Solid("#2A2A2A"),
                Dim = Solid("#66000000"),
                MenuBack = Solid("#1E1E1E"),
                MenuText = Solid("#FFFFFF"),
                MenuEdge = Solid("#333333")
            };

            list["windows10"] = new Theme
            {
                Name = "Windows 10",
                Back = Solid("#121212"),
                Edge = Solid("#333333"),
                EdgeWidth = 1,
                Corner = 0,
                CoverCorner = 0,
                ButtonCorner = 0,
                PillCorner = 0,
                MenuCorner = 0,
                ItemCorner = 0,
                Gloss = Brushes.Transparent,
                Title = Solid("#FFFFFF"),
                Artist = Solid("#888888"),
                Icon = Solid("#FFFFFF"),
                PlayIcon = Solid("#FFFFFF"),
                Close = Solid("#888888"),
                Pill = Solid("#22FFFFFF"),
                PlayBack = Solid("#B3000000"),
                PlayEdge = Solid("#55FFFFFF"),
                Hover = Solid("#FFFFFF"),
                CoverBack = Solid("#2A2A2A"),
                Dim = Solid("#66000000"),
                MenuBack = Solid("#1E1E1E"),
                MenuText = Solid("#FFFFFF"),
                MenuEdge = Solid("#333333")
            };

            list["windows7"] = new Theme
            {
                Name = "Windows 7",
                Back = Linear(0, 0, 0, 1, ("#D6ECFA", 0), ("#8EC5EC", 0.5), ("#4B9AD6", 1)),
                Edge = Solid("#E6FFFFFF"),
                EdgeWidth = 1,
                Corner = 12,
                PillCorner = 4,
                Gloss = Linear(0, 0, 0, 1, ("#99FFFFFF", 0), ("#33FFFFFF", 0.48), ("#00FFFFFF", 0.5), ("#00FFFFFF", 1)),
                Title = Solid("#0B2A45"),
                Artist = Solid("#2F5F86"),
                Icon = Solid("#0B3A60"),
                PlayIcon = Solid("#FFFFFF"),
                Close = Solid("#1B4D78"),
                Pill = Solid("#66FFFFFF"),
                PlayBack = Linear(0, 0, 0, 1, ("#A8DCFB", 0), ("#4A9ADB", 0.5), ("#1F6DB0", 0.51), ("#3C9AD8", 1)),
                PlayEdge = Solid("#CCFFFFFF"),
                Hover = Solid("#FFFFFF"),
                CoverBack = Solid("#7FB3D6"),
                Dim = Solid("#55103A5E"),
                MenuBack = Solid("#EAF5FD"),
                MenuText = Solid("#0B2A45"),
                MenuEdge = Solid("#7FB3D9")
            };

            list["rosepine"] = new Theme
            {
                Name = "Rosé Pine",
                Back = Solid("#191724"),
                Edge = Solid("#403D52"),
                EdgeWidth = 1,
                Corner = 16,
                Gloss = Brushes.Transparent,
                Title = Solid("#E0DEF4"),
                Artist = Solid("#908CAA"),
                Icon = Solid("#EBBCBA"),
                PlayIcon = Solid("#EB6F92"),
                Close = Solid("#EB6F92"),
                Pill = Solid("#26233A"),
                PlayBack = Solid("#D91F1D2E"),
                PlayEdge = Solid("#EB6F92"),
                Hover = Solid("#EBBCBA"),
                CoverBack = Solid("#1F1D2E"),
                Dim = Solid("#66191724"),
                MenuBack = Solid("#1F1D2E"),
                MenuText = Solid("#E0DEF4"),
                MenuEdge = Solid("#403D52")
            };

            list["rosepinedawn"] = new Theme
            {
                Name = "Rosé Pine Dawn",
                Back = Solid("#FAF4ED"),
                Edge = Solid("#DFDAD9"),
                EdgeWidth = 1,
                Corner = 16,
                Gloss = Brushes.Transparent,
                Title = Solid("#575279"),
                Artist = Solid("#9893A5"),
                Icon = Solid("#D7827E"),
                PlayIcon = Solid("#B4637A"),
                Close = Solid("#D7827E"),
                Pill = Solid("#F2E9E1"),
                PlayBack = Solid("#E6FFFAF3"),
                PlayEdge = Solid("#D7827E"),
                Hover = Solid("#D7827E"),
                CoverBack = Solid("#F2E9E1"),
                Dim = Solid("#55575279"),
                MenuBack = Solid("#FFFAF3"),
                MenuText = Solid("#575279"),
                MenuEdge = Solid("#DFDAD9")
            };

            list["frosty"] = new Theme
            {
                Name = "Frosty",
                Back = Linear(0, 0, 1, 1, ("#80F4FAFF", 0), ("#66DCEBF8", 0.5), ("#7AC9DEF0", 1)),
                Edge = Solid("#B3FFFFFF"),
                EdgeWidth = 1,
                Corner = 20,
                Gloss = Linear(0, 0, 0, 1, ("#4DFFFFFF", 0), ("#00FFFFFF", 0.55)),
                Title = Solid("#10263A"),
                Artist = Solid("#35566E"),
                Icon = Solid("#10263A"),
                PlayIcon = Solid("#10263A"),
                Close = Solid("#35566E"),
                Pill = Solid("#59FFFFFF"),
                PlayBack = Solid("#66FFFFFF"),
                PlayEdge = Solid("#B3FFFFFF"),
                Hover = Solid("#4A86B0"),
                CoverBack = Solid("#40FFFFFF"),
                Dim = Solid("#55EAF4FF"),
                MenuBack = Solid("#F2EAF4FB"),
                MenuText = Solid("#10263A"),
                MenuEdge = Solid("#FFFFFF")
            };

            list["liquidglass"] = new Theme
            {
                Name = "Liquid Glass",
                Back = Linear(0, 0, 1, 1, ("#66FFFFFF", 0), ("#38404058", 0.35), ("#40303048", 0.7), ("#5CFFFFFF", 1)),
                Edge = Linear(0, 0, 1, 1, ("#E6FFFFFF", 0), ("#26FFFFFF", 0.45), ("#26FFFFFF", 0.55), ("#B3FFFFFF", 1)),
                EdgeWidth = 1.5,
                Corner = 30,
                Shadow = true,
                Gloss = Radial(0.2, 0, 0.95, 0.55, ("#66FFFFFF", 0), ("#00FFFFFF", 1)),
                Title = Solid("#FFFFFF"),
                Artist = Solid("#D0D0DC"),
                Icon = Solid("#FFFFFF"),
                PlayIcon = Solid("#FFFFFF"),
                Close = Solid("#E0E0EA"),
                Pill = Solid("#33FFFFFF"),
                PlayBack = Solid("#55FFFFFF"),
                PlayEdge = Solid("#99FFFFFF"),
                Hover = Solid("#FFFFFF"),
                CoverBack = Solid("#33FFFFFF"),
                Dim = Solid("#44000000"),
                MenuBack = Solid("#E61C1C28"),
                MenuText = Solid("#FFFFFF"),
                MenuEdge = Solid("#55FFFFFF")
            };

            list["transparent"] = new Theme
            {
                Name = "Transparent",
                Back = Solid("#01000000"),
                Edge = Brushes.Transparent,
                EdgeWidth = 0,
                Corner = 16,
                Shadow = true,
                Gloss = Brushes.Transparent,
                Title = Solid("#FFFFFF"),
                Artist = Solid("#D9FFFFFF"),
                Icon = Solid("#FFFFFF"),
                PlayIcon = Solid("#FFFFFF"),
                Close = Solid("#E6FFFFFF"),
                Pill = Solid("#40000000"),
                PlayBack = Solid("#66000000"),
                PlayEdge = Solid("#66FFFFFF"),
                Hover = Solid("#FFFFFF"),
                CoverBack = Solid("#22FFFFFF"),
                Dim = Solid("#55000000"),
                MenuBack = Solid("#F01E1E1E"),
                MenuText = Solid("#FFFFFF"),
                MenuEdge = Solid("#44FFFFFF")
            };

            return list;
        }

        private string LoadTheme()
        {
            try
            {
                if (File.Exists(themePath))
                {
                    string saved = File.ReadAllText(themePath).Trim();
                    if (themes.ContainsKey(saved))
                    {
                        return saved;
                    }
                }
            }
            catch
            {
            }

            return "sleek";
        }

        private void SaveTheme(string key)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(themePath)!);
                File.WriteAllText(themePath, key);
            }
            catch
            {
            }
        }

        private void ApplyTheme(string key)
        {
            var t = themes[key];
            currentTheme = key;

            Resources["WindowBrush"] = t.Back;
            Resources["WindowBorderBrush"] = t.Edge;
            Resources["WindowBorderThickness"] = new Thickness(t.EdgeWidth);
            Resources["WindowCorner"] = new CornerRadius(t.Corner);
            Resources["ButtonCorner"] = new CornerRadius(t.ButtonCorner);
            Resources["MenuCorner"] = new CornerRadius(t.MenuCorner);
            Resources["ItemCorner"] = new CornerRadius(t.ItemCorner);
            Resources["GlossBrush"] = t.Gloss;
            Resources["TitleBrush"] = t.Title;
            Resources["ArtistBrush"] = t.Artist;
            Resources["IconBrush"] = t.Icon;
            Resources["PlayIconBrush"] = t.PlayIcon;
            Resources["CloseBrush"] = t.Close;
            Resources["PillBrush"] = t.Pill;
            Resources["PlayBackBrush"] = t.PlayBack;
            Resources["PlayBorderBrush"] = t.PlayEdge;
            Resources["HoverBrush"] = t.Hover;
            Resources["CoverBackBrush"] = t.CoverBack;
            Resources["DimBrush"] = t.Dim;
            Resources["MenuBackBrush"] = t.MenuBack;
            Resources["MenuTextBrush"] = t.MenuText;
            Resources["MenuBorderBrush"] = t.MenuEdge;

            ThemeButton.Resources["ButtonCorner"] = new CornerRadius(t.PillCorner);

            CoverArea.CornerRadius = new CornerRadius(t.CoverCorner);
            CoverClip.RadiusX = t.CoverCorner;
            CoverClip.RadiusY = t.CoverCorner;

            ContentGrid.Effect = t.Shadow
                ? new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 8,
                    ShadowDepth = 0,
                    Opacity = 0.7
                }
                : null;

            ThemeNameText.Text = t.Name;

            foreach (var item in ThemeMenu.Children.OfType<Button>())
            {
                item.FontWeight = (string)item.Tag == key ? FontWeights.Bold : FontWeights.Normal;
            }
        }

        private static LinearGradientBrush WipeBrush(bool reveal)
        {
            var clear = Color.FromArgb(0, 0, 0, 0);
            var solid = Color.FromArgb(255, 0, 0, 0);

            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(1.3, -0.3),
                EndPoint = new Point(-0.3, 1.3)
            };

            brush.GradientStops.Add(new GradientStop(reveal ? solid : clear, 0.0));
            brush.GradientStops.Add(new GradientStop(reveal ? clear : solid, 0.18));

            return brush;
        }

        private static Task WipeAsync(params LinearGradientBrush[] brushes)
        {
            var source = new TaskCompletionSource();
            var span = TimeSpan.FromMilliseconds(650);
            bool first = true;

            foreach (var brush in brushes)
            {
                var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
                var startAnimation = new DoubleAnimation(0.0, 0.82, span) { EasingFunction = ease };
                var endAnimation = new DoubleAnimation(0.18, 1.0, span) { EasingFunction = ease };

                if (first)
                {
                    endAnimation.Completed += (s, e) => source.TrySetResult();
                    first = false;
                }

                brush.GradientStops[0].BeginAnimation(GradientStop.OffsetProperty, startAnimation);
                brush.GradientStops[1].BeginAnimation(GradientStop.OffsetProperty, endAnimation);
            }

            return source.Task;
        }

        private RenderTargetBitmap? Capture()
        {
            double width = Root.ActualWidth;
            double height = Root.ActualHeight;

            if (width < 1 || height < 1)
            {
                return null;
            }

            var dpi = VisualTreeHelper.GetDpi(this);
            var bitmap = new RenderTargetBitmap(
                (int)Math.Ceiling(width * dpi.DpiScaleX),
                (int)Math.Ceiling(height * dpi.DpiScaleY),
                96 * dpi.DpiScaleX,
                96 * dpi.DpiScaleY,
                PixelFormats.Pbgra32);

            bitmap.Render(Root);
            return bitmap;
        }

        private async Task SwitchThemeAsync(string key)
        {
            if (key == currentTheme || switching || changingView)
            {
                return;
            }

            switching = true;

            var snapshot = Capture();
            var hide = WipeBrush(false);
            var reveal = WipeBrush(true);

            if (snapshot != null)
            {
                SnapshotImage.Source = snapshot;
                SnapshotImage.OpacityMask = hide;
                SnapshotImage.Visibility = Visibility.Visible;
                Root.OpacityMask = reveal;
            }

            ApplyTheme(key);
            SaveTheme(key);

            if (snapshot != null)
            {
                await WipeAsync(hide, reveal);
                Root.OpacityMask = null;
                SnapshotImage.OpacityMask = null;
                SnapshotImage.Visibility = Visibility.Collapsed;
                SnapshotImage.Source = null;
            }

            switching = false;
        }

        private async void Lyrics_Click(object sender, RoutedEventArgs e)
        {
            if (changingView || switching)
            {
                return;
            }

            changingView = true;

            bool opening = !lyricsOpen;
            lyricsOpen = opening;

            FrameworkElement incoming = opening ? LyricsLayer : PlayerLayer;
            FrameworkElement outgoing = opening ? PlayerLayer : LyricsLayer;

            if (opening)
            {
                hovering = false;
                ApplyOverlay();
            }

            var reveal = WipeBrush(true);
            var hide = WipeBrush(false);

            incoming.OpacityMask = reveal;
            outgoing.OpacityMask = hide;
            incoming.Visibility = Visibility.Visible;
            incoming.IsHitTestVisible = true;
            outgoing.IsHitTestVisible = false;

            if (opening)
            {
                LyricsLayer.UpdateLayout();
                UpdateLyricsPosition();
                RecenterLyrics(true);
            }

            await WipeAsync(reveal, hide);

            outgoing.Visibility = Visibility.Collapsed;
            outgoing.IsHitTestVisible = true;
            incoming.OpacityMask = null;
            outgoing.OpacityMask = null;

            changingView = false;
        }

        private static string? Str(JsonElement element, string name)
        {
            if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }

            return null;
        }

        private static List<LyricLine> ParseSynced(string lrc)
        {
            var result = new List<LyricLine>();
            var tag = new Regex(@"\[(\d+):(\d+(?:\.\d+)?)\]");

            foreach (var raw in lrc.Split('\n'))
            {
                string line = raw.Trim('\r', ' ');
                var matches = tag.Matches(line);

                if (matches.Count == 0)
                {
                    continue;
                }

                string text = tag.Replace(line, "").Trim();

                foreach (Match match in matches)
                {
                    double minutes = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                    double seconds = double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                    result.Add(new LyricLine(TimeSpan.FromSeconds(minutes * 60 + seconds), text));
                }
            }

            return result.OrderBy(l => l.Time).ToList();
        }

        private static LyricsData? ReadLyrics(JsonElement item)
        {
            var data = new LyricsData();

            if (item.TryGetProperty("instrumental", out var instrumental) && instrumental.ValueKind == JsonValueKind.True)
            {
                data.Instrumental = true;
                return data;
            }

            string? synced = Str(item, "syncedLyrics");

            if (!string.IsNullOrWhiteSpace(synced))
            {
                data.Synced = ParseSynced(synced);

                if (data.Synced.Count > 0)
                {
                    return data;
                }
            }

            string? plain = Str(item, "plainLyrics");

            if (!string.IsNullOrWhiteSpace(plain))
            {
                data.Plain = plain.Replace("\r", "").Split('\n').ToList();
                return data;
            }

            return null;
        }

        private static async Task<LyricsData?> FetchLyricsAsync(string title, string artist, string album, int duration)
        {
            try
            {
                string query = "track_name=" + Uri.EscapeDataString(title) + "&artist_name=" + Uri.EscapeDataString(artist);
                string getUrl = "https://lrclib.net/api/get?" + query;

                if (!string.IsNullOrEmpty(album))
                {
                    getUrl += "&album_name=" + Uri.EscapeDataString(album);
                }

                if (duration > 0)
                {
                    getUrl += "&duration=" + duration;
                }

                using (var response = await http.GetAsync(getUrl))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        string body = await response.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(body);
                        var data = ReadLyrics(doc.RootElement);

                        if (data != null)
                        {
                            return data;
                        }
                    }
                }

                string searchBody = await http.GetStringAsync("https://lrclib.net/api/search?" + query);
                using var searchDoc = JsonDocument.Parse(searchBody);

                JsonElement? best = null;
                double bestDiff = double.MaxValue;

                foreach (var item in searchDoc.RootElement.EnumerateArray())
                {
                    string? synced = Str(item, "syncedLyrics");

                    if (string.IsNullOrWhiteSpace(synced))
                    {
                        continue;
                    }

                    double diff = 0;

                    if (duration > 0 && item.TryGetProperty("duration", out var itemDuration) && itemDuration.ValueKind == JsonValueKind.Number)
                    {
                        diff = Math.Abs(itemDuration.GetDouble() - duration);
                    }

                    if (diff < bestDiff)
                    {
                        bestDiff = diff;
                        best = item;
                    }
                }

                if (best != null)
                {
                    var data = ReadLyrics(best.Value);

                    if (data != null)
                    {
                        return data;
                    }
                }

                foreach (var item in searchDoc.RootElement.EnumerateArray())
                {
                    var data = ReadLyrics(item);

                    if (data != null)
                    {
                        return data;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private async Task LoadLyricsAsync(string title, string artist, string album, int duration)
        {
            int version = ++lyricsVersion;

            ClearLyricLines();
            ShowLyricsStatus("Loading lyrics...");

            var data = await FetchLyricsAsync(title, artist, album, duration);

            if (version != lyricsVersion)
            {
                return;
            }

            if (data == null)
            {
                ShowLyricsStatus("No lyrics found");
                return;
            }

            if (data.Instrumental)
            {
                ShowLyricsStatus("Instrumental");
                return;
            }

            BuildLyricLines(data);
        }

        private void ShowLyricsStatus(string text)
        {
            LyricsStatus.Text = text;
            LyricsStatus.BeginAnimation(OpacityProperty, Anim(1, 300));
            LinesPanel.BeginAnimation(OpacityProperty, Anim(0, 200));
        }

        private void ClearLyricLines()
        {
            lineBlocks.Clear();
            LinesPanel.Children.Clear();
            syncedLines = new List<LyricLine>();
            isSynced = false;
            activeIndex = -1;
            plainOffset = 0;
            LinesShift.BeginAnimation(TranslateTransform.YProperty, null);
            LinesShift.Y = 0;
        }

        private void BuildLyricLines(LyricsData data)
        {
            ClearLyricLines();

            isSynced = data.Synced.Count > 0;
            syncedLines = data.Synced;

            var texts = isSynced
                ? data.Synced.Select(l => l.Text).ToList()
                : data.Plain;

            foreach (var text in texts)
            {
                string shown = string.IsNullOrWhiteSpace(text)
                    ? (isSynced ? "\u266A" : " ")
                    : text;

                var block = new TextBlock
                {
                    Text = shown,
                    FontSize = 15,
                    FontWeight = FontWeights.SemiBold,
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(8, 7, 8, 7),
                    Opacity = isSynced ? 0.28 : 0.9,
                    RenderTransformOrigin = new Point(0.5, 0.5),
                    RenderTransform = new ScaleTransform(1, 1)
                };

                block.SetResourceReference(TextBlock.ForegroundProperty, "TitleBrush");

                lineBlocks.Add(block);
                LinesPanel.Children.Add(block);
            }

            LyricsStatus.BeginAnimation(OpacityProperty, Anim(0, 200));
            LinesPanel.BeginAnimation(OpacityProperty, Anim(1, 500));

            UpdateLyricsPosition();
            RecenterLyrics(true);
        }

        private void UpdateLyricsPosition()
        {
            if (!lyricsOpen || !isSynced || syncedLines.Count == 0 || lastSession == null)
            {
                return;
            }

            TimeSpan position;

            try
            {
                var timeline = lastSession.GetTimelineProperties();
                var status = lastSession.GetPlaybackInfo().PlaybackStatus;
                position = timeline.Position;

                if (status == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                {
                    position += DateTimeOffset.Now - timeline.LastUpdatedTime;
                }
            }
            catch
            {
                return;
            }

            int index = -1;

            for (int i = 0; i < syncedLines.Count; i++)
            {
                if (syncedLines[i].Time <= position)
                {
                    index = i;
                }
                else
                {
                    break;
                }
            }

            SetActiveLine(index);
        }

        private void SetActiveLine(int index)
        {
            if (index == activeIndex)
            {
                return;
            }

            activeIndex = index;

            for (int i = 0; i < lineBlocks.Count; i++)
            {
                var block = lineBlocks[i];
                bool active = i == index;
                int distance = Math.Abs(i - index);
                double opacity = active ? 1.0 : (distance == 1 ? 0.5 : 0.28);
                double scale = active ? 1.08 : 1.0;

                block.BeginAnimation(OpacityProperty, Anim(opacity, 350));

                var transform = (ScaleTransform)block.RenderTransform;
                transform.BeginAnimation(ScaleTransform.ScaleXProperty, Anim(scale, 350));
                transform.BeginAnimation(ScaleTransform.ScaleYProperty, Anim(scale, 350));
            }

            RecenterLyrics(false);
        }

        private void RecenterLyrics(bool instant)
        {
            if (!isSynced || lineBlocks.Count == 0)
            {
                return;
            }

            double viewport = LyricsViewport.ActualHeight;

            if (viewport < 1)
            {
                return;
            }

            int index = Math.Min(Math.Max(activeIndex, 0), lineBlocks.Count - 1);
            var block = lineBlocks[index];
            double center = block.TranslatePoint(new Point(0, block.ActualHeight / 2), LinesPanel).Y;
            double target = viewport / 2 - center;

            if (instant)
            {
                LinesShift.BeginAnimation(TranslateTransform.YProperty, null);
                LinesShift.Y = target;
            }
            else
            {
                LinesShift.BeginAnimation(TranslateTransform.YProperty, Anim(target, 550));
            }
        }

        private void LyricsViewport_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (isSynced || lineBlocks.Count == 0)
            {
                return;
            }

            double viewport = LyricsViewport.ActualHeight;
            double min = Math.Min(0, viewport - LinesPanel.ActualHeight - 20);

            plainOffset = Math.Max(min, Math.Min(0, plainOffset + e.Delta * 0.5));
            LinesShift.BeginAnimation(TranslateTransform.YProperty, Anim(plainOffset, 250));
        }

        private static DoubleAnimation Anim(double to, int ms, bool bounce = false)
        {
            IEasingFunction easing = bounce
                ? new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.7 }
                : new CubicEase { EasingMode = EasingMode.EaseOut };

            return new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms))
            {
                EasingFunction = easing
            };
        }

        private static Task FadeAsync(UIElement element, double to, int ms)
        {
            var source = new TaskCompletionSource();
            var animation = Anim(to, ms);
            animation.Completed += (s, e) => source.TrySetResult();
            element.BeginAnimation(OpacityProperty, animation);
            return source.Task;
        }

        private void AnimateGlyph(TextBlock glyph, ScaleTransform scale, RotateTransform rotate, bool show, double hiddenAngle)
        {
            glyph.BeginAnimation(OpacityProperty, Anim(show ? 1 : 0, 300));
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, Anim(show ? 1 : 0.4, 360, show));
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, Anim(show ? 1 : 0.4, 360, show));
            rotate.BeginAnimation(RotateTransform.AngleProperty, Anim(show ? 0 : hiddenAngle, 360));
        }

        private void ApplyOverlay()
        {
            CoverBlur.BeginAnimation(BlurEffect.RadiusProperty, Anim(hovering ? 24 : 0, 300));
            CoverScale.BeginAnimation(ScaleTransform.ScaleXProperty, Anim(hovering ? 1.08 : 1, 400));
            CoverScale.BeginAnimation(ScaleTransform.ScaleYProperty, Anim(hovering ? 1.08 : 1, 400));
            Dim.BeginAnimation(OpacityProperty, Anim(hovering ? 1 : 0, 300));

            bool showButton = hovering || !isPlaying;
            PlayPauseButton.BeginAnimation(OpacityProperty, Anim(showButton ? 1 : 0, 280));
            ButtonScale.BeginAnimation(ScaleTransform.ScaleXProperty, Anim(showButton ? 1 : 0.6, 380, showButton));
            ButtonScale.BeginAnimation(ScaleTransform.ScaleYProperty, Anim(showButton ? 1 : 0.6, 380, showButton));
        }

        private void SetPlaying(bool playing)
        {
            if (lastPlaying == playing)
            {
                return;
            }

            lastPlaying = playing;
            isPlaying = playing;

            AnimateGlyph(PauseGlyph, PauseScale, PauseRotate, playing, 90);
            AnimateGlyph(PlayGlyph, PlayScale, PlayRotate, !playing, -90);
            ApplyOverlay();
        }

        private async Task ChangeTrackAsync(string title, string artist, BitmapImage? cover)
        {
            await Task.WhenAll(
                FadeAsync(TitleText, 0, 150),
                FadeAsync(ArtistText, 0, 150),
                FadeAsync(CoverImage, 0, 150));

            TitleText.Text = title;
            ArtistText.Text = artist;
            CoverImage.Source = cover;

            await Task.WhenAll(
                FadeAsync(TitleText, 1, 300),
                FadeAsync(ArtistText, 1, 300),
                FadeAsync(CoverImage, 1, 300));
        }

        private GlobalSystemMediaTransportControlsSession? GetSpotifySession()
        {
            if (manager == null)
            {
                return null;
            }

            foreach (var session in manager.GetSessions())
            {
                if (session.SourceAppUserModelId.Contains("Spotify", StringComparison.OrdinalIgnoreCase))
                {
                    return session;
                }
            }

            return null;
        }

        private async Task RefreshAsync()
        {
            if (refreshing)
            {
                return;
            }

            refreshing = true;

            try
            {
                await RefreshCoreAsync();
            }
            finally
            {
                refreshing = false;
            }
        }

        private async Task RefreshCoreAsync()
        {
            var session = GetSpotifySession();
            lastSession = session;

            if (session == null)
            {
                SetPlaying(false);

                if (lastTrack != "")
                {
                    lastTrack = "";
                    lyricsVersion++;
                    ClearLyricLines();
                    ShowLyricsStatus("Open Spotify");
                    await ChangeTrackAsync("Nothing playing", "Open Spotify", null);
                }

                return;
            }

            var playback = session.GetPlaybackInfo();
            SetPlaying(playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing);

            var props = await session.TryGetMediaPropertiesAsync();
            string currentTrack = props.Title + "|" + props.Artist;

            if (currentTrack == lastTrack)
            {
                return;
            }

            lastTrack = currentTrack;

            var timeline = session.GetTimelineProperties();
            int duration = (int)Math.Round((timeline.EndTime - timeline.StartTime).TotalSeconds);
            _ = LoadLyricsAsync(props.Title, props.Artist, props.AlbumTitle, duration);

            BitmapImage? bitmap = null;

            if (props.Thumbnail != null)
            {
                var stream = await props.Thumbnail.OpenReadAsync();
                var netStream = stream.AsStreamForRead();
                var memory = new MemoryStream();
                await netStream.CopyToAsync(memory);
                memory.Position = 0;

                bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = memory;
                bitmap.EndInit();
                bitmap.Freeze();
            }

            await ChangeTrackAsync(props.Title, props.Artist, bitmap);
        }

        private void Cover_MouseEnter(object sender, MouseEventArgs e)
        {
            hovering = true;
            ApplyOverlay();
        }

        private void Cover_MouseLeave(object sender, MouseEventArgs e)
        {
            hovering = false;
            ApplyOverlay();
        }

        private void ThemeButton_Click(object sender, RoutedEventArgs e)
        {
            if ((DateTime.UtcNow - popupClosedAt).TotalMilliseconds < 200)
            {
                return;
            }

            ThemePopup.IsOpen = true;
        }

        private void ThemePopup_Opened(object? sender, EventArgs e)
        {
            ChevronRotate.BeginAnimation(RotateTransform.AngleProperty, Anim(180, 250));
        }

        private void ThemePopup_Closed(object? sender, EventArgs e)
        {
            popupClosedAt = DateTime.UtcNow;
            ChevronRotate.BeginAnimation(RotateTransform.AngleProperty, Anim(0, 250));
        }

        private async void ThemeItem_Click(object sender, RoutedEventArgs e)
        {
            ThemePopup.IsOpen = false;

            if (sender is Button button && button.Tag is string key)
            {
                await SwitchThemeAsync(key);
            }
        }

        private async void PlayPause_Click(object sender, RoutedEventArgs e)
        {
            var session = GetSpotifySession();
            if (session != null)
            {
                await session.TryTogglePlayPauseAsync();
                await Task.Delay(150);
                await RefreshAsync();
            }
        }

        private async void Previous_Click(object sender, RoutedEventArgs e)
        {
            var session = GetSpotifySession();
            if (session != null)
            {
                await session.TrySkipPreviousAsync();
            }
        }

        private async void Next_Click(object sender, RoutedEventArgs e)
        {
            var session = GetSpotifySession();
            if (session != null)
            {
                await session.TrySkipNextAsync();
            }
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void Close_Click(object sender, MouseButtonEventArgs e)
        {
            var animation = Anim(0, 220);
            animation.Completed += (s, a) => Close();
            BeginAnimation(OpacityProperty, animation);
        }
    }
}