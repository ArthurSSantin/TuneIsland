using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using TuneIsland.Core;
using TuneIsland.Services;

namespace TuneIsland.Views;

public partial class IslandWindow : Window
{
    private const int CompactHeight = 21;
    private const int CompactWidth = 105;
    private const int IdleWidth = 92;
    private const int IdleHeight = 26;
    private const int ExpandedHeight = 105;
    private const double MaxCornerRadius = 26;
    private const double FloatingMargin = 10;

    private readonly MediaSessionService _media;
    private readonly SolidColorBrush _accent;
    private readonly DropShadowEffect _glow;
    private readonly Border[] _bars;
    private readonly int[] _barDurationsMs = { 420, 560, 360 };

    private readonly DispatcherTimer _enterTimer;
    private readonly DispatcherTimer _leaveTimer;
    private readonly DispatcherTimer _autoExpandTimer;
    private readonly DispatcherTimer _progressTimer;
    private readonly DispatcherTimer _fullscreenTimer;

    private readonly IEasingFunction _expandEase = new QuinticEase { EasingMode = EasingMode.EaseOut };
    private readonly IEasingFunction _collapseEase = new QuarticEase { EasingMode = EasingMode.EaseInOut };

    private AppSettings _settings;
    private MediaSnapshot? _snapshot;
    private string? _trackKey;
    private bool _expanded;
    private bool _hovered;
    private bool _fullscreenHidden;
    private bool _equalizerRunning;
    private double _centerX;

    public IslandWindow(AppSettings settings, MediaSessionService media)
    {
        _settings = settings;
        _media = media;

        _accent = new SolidColorBrush(ToColor(DominantColorExtractor.Fallback));
        Resources["AccentBrush"] = _accent;

        InitializeComponent();

        _glow = new DropShadowEffect
        {
            BlurRadius = 10,
            ShadowDepth = 0,
            Opacity = 0.9,
            Color = ToColor(DominantColorExtractor.Fallback)
        };
        ProgressFill.Effect = _glow;

        _bars = new[] { Bar1, Bar2, Bar3 };

        _autoExpandTimer = new DispatcherTimer();
        _autoExpandTimer.Tick += (_, _) =>
        {
            _autoExpandTimer.Stop();
            if (!_hovered)
            {
                Collapse();
            }
        };

        _enterTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(160) };
        _enterTimer.Tick += (_, _) =>
        {
            _enterTimer.Stop();
            _hovered = true;
            Expand();
        };

        _leaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
        _leaveTimer.Tick += (_, _) =>
        {
            _leaveTimer.Stop();
            _hovered = false;
            if (!_autoExpandTimer.IsEnabled)
            {
                Collapse();
            }
        };

        _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _progressTimer.Tick += (_, _) => UpdateProgress();

        _fullscreenTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _fullscreenTimer.Tick += (_, _) => CheckFullscreen();

        _media.SnapshotChanged += (_, snapshot) =>
            Dispatcher.BeginInvoke(new Action(() => OnSnapshot(snapshot)));

        SourceInitialized += (_, _) => ApplyWindowStyles();
        SizeChanged += (_, _) => ApplyCorners();
        Loaded += (_, _) =>
        {
            ApplyCorners();
            Reposition();
            ApplyFullscreenWatcher();
            UpdateVisibility();
        };
        Closed += (_, _) =>
        {
            _enterTimer.Stop();
            _leaveTimer.Stop();
            _autoExpandTimer.Stop();
            _progressTimer.Stop();
            _fullscreenTimer.Stop();
        };

        ApplyCorners();
    }

    /// <summary>Aplica novas configurações sem reiniciar o app.</summary>
    public void ApplySettings(AppSettings settings)
    {
        _settings = settings;
        ApplyCorners();
        ApplyFullscreenWatcher();
        Reposition();
        UpdateVisibility();
    }

    /// <summary>Recalcula posição e tamanho após mudança de monitor, DPI ou resolução.</summary>
    public void Reposition()
    {
        var displays = DisplayService.GetDisplays();
        var index = Math.Clamp(_settings.MonitorIndex, 0, displays.Count - 1);
        var display = displays[index];

        _centerX = (display.Left + display.Width / 2.0) / display.DpiScale;
        Top = display.Top / display.DpiScale +
              (_settings.Style == IslandStyle.Floating ? FloatingMargin : 0);

        LayoutTo(animate: false);
    }

    private void ApplyWindowStyles()
    {
        try
        {
            var handle = new WindowInteropHelper(this).Handle;
            var exStyle = NativeMethods.GetWindowLongPtr(handle, NativeMethods.GWL_EXSTYLE).ToInt64();
            exStyle |= NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE;
            NativeMethods.SetWindowLongPtr(handle, NativeMethods.GWL_EXSTYLE, new IntPtr(exStyle));
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Falha ao aplicar estilos de janela.");
        }
    }

    /// <summary>Raio acompanha a altura atual (metade da altura, até um máximo), então muda suavemente durante a animação.</summary>
    private void ApplyCorners()
    {
        var height = ActualHeight > 0 ? ActualHeight : Height;
        var radius = Math.Min(height / 2.0, MaxCornerRadius);
        Island.CornerRadius = _settings.Style == IslandStyle.Attached
            ? new CornerRadius(0, 0, radius, radius)
            : new CornerRadius(radius);
    }

    private void ApplyFullscreenWatcher()
    {
        if (_settings.HideInFullscreen)
        {
            if (!_fullscreenTimer.IsEnabled)
            {
                _fullscreenTimer.Start();
            }

            CheckFullscreen();
        }
        else
        {
            _fullscreenTimer.Stop();
            _fullscreenHidden = false;
        }
    }

    private void CheckFullscreen()
    {
        var fullscreen = FullscreenDetector.IsFullscreenAppActive();
        if (fullscreen == _fullscreenHidden)
        {
            return;
        }

        _fullscreenHidden = fullscreen;
        UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        var hideForIdle = _snapshot is null && _settings.HideWhenIdle;
        var hideForFullscreen = _fullscreenHidden && _settings.HideInFullscreen;
        var shouldShow = !hideForIdle && !hideForFullscreen;

        if (shouldShow && !IsVisible)
        {
            Show();
        }
        else if (!shouldShow && IsVisible)
        {
            Hide();
        }
    }

    private void OnSnapshot(MediaSnapshot? snapshot)
    {
        _snapshot = snapshot;

        if (snapshot is null)
        {
            _trackKey = null;
            _autoExpandTimer.Stop();
            _progressTimer.Stop();
            SetEqualizer(false);
            Collapse();
            LayoutTo(animate: true, ease: _collapseEase, milliseconds: 260);
            UpdateVisibility();
            return;
        }

        if (snapshot.TrackKey != _trackKey)
        {
            _trackKey = snapshot.TrackKey;
            ApplyArtwork(snapshot.Thumbnail);
            StartAutoExpand();
        }

        TitleText.Text = snapshot.Title;
        ArtistText.Text = snapshot.Artist;
        PlayPauseButton.Content = snapshot.IsPlaying ? "" : "";
        SetEqualizer(snapshot.IsPlaying);
        UpdateProgress();

        if (!_expanded)
        {
            LayoutTo(animate: true, ease: _collapseEase, milliseconds: 260);
        }

        Dispatcher.BeginInvoke(new Action(RefreshMarquee), DispatcherPriority.Loaded);
        UpdateVisibility();
    }

    private void ApplyArtwork(byte[]? thumbnail)
    {
        if (thumbnail is null)
        {
            CompactArt.Source = null;
            LargeArt.Source = null;
            SetAccent(DominantColorExtractor.Fallback);
            return;
        }

        var image = ThumbnailDecoder.CreateImage(thumbnail);
        CompactArt.Source = image;
        LargeArt.Source = image;

        var accent = ThumbnailDecoder.ExtractAccent(thumbnail) ?? DominantColorExtractor.Fallback;
        SetAccent(accent);
    }

    private void SetAccent(RgbColor color)
    {
        var target = ToColor(color);
        _accent.BeginAnimation(SolidColorBrush.ColorProperty,
            new ColorAnimation(target, TimeSpan.FromMilliseconds(450)));
        _glow.Color = target;
    }

    private void SetEqualizer(bool running)
    {
        if (_equalizerRunning == running)
        {
            return;
        }

        _equalizerRunning = running;

        for (var i = 0; i < _bars.Length; i++)
        {
            var scale = (ScaleTransform)_bars[i].RenderTransform;
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);

            if (!running)
            {
                scale.ScaleY = 0.3;
                continue;
            }

            var animation = new DoubleAnimation(0.25, 1.0, TimeSpan.FromMilliseconds(_barDurationsMs[i]))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
                BeginTime = TimeSpan.FromMilliseconds(i * 90)
            };

            scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
        }
    }

    private void UpdateProgress()
    {
        if (_snapshot is null)
        {
            ProgressFill.Width = 0;
            TimeCurrent.Text = string.Empty;
            TimeTotal.Text = string.Empty;
            return;
        }

        var position = _snapshot.EstimatePosition(DateTimeOffset.UtcNow);
        var duration = _snapshot.Duration;

        var ratio = duration > TimeSpan.Zero
            ? Math.Clamp(position.TotalSeconds / duration.TotalSeconds, 0, 1)
            : 0;

        ProgressFill.Width = ProgressHit.ActualWidth * ratio;
        TimeCurrent.Text = TimeFormatter.Format(position);
        TimeTotal.Text = duration > TimeSpan.Zero ? TimeFormatter.Format(duration) : "--:--";
    }

    private void StartAutoExpand()
    {
        _autoExpandTimer.Interval = TimeSpan.FromSeconds(_settings.AutoExpandSeconds);
        _autoExpandTimer.Stop();
        _autoExpandTimer.Start();
        Expand();
    }

    private void Expand()
    {
        if (_expanded || _snapshot is null)
        {
            return;
        }

        _expanded = true;
        ExpandedPanel.Visibility = Visibility.Visible;
        AnimateOpacity(CompactPanel, 0, 120);
        AnimateOpacity(ExpandedPanel, 1, 240);
        ApplyCorners();
        LayoutTo(animate: true, ease: _expandEase, milliseconds: 360);
        _progressTimer.Start();
        UpdateProgress();
        Dispatcher.BeginInvoke(new Action(RefreshMarquee), DispatcherPriority.Loaded);
    }

    private void Collapse()
    {
        if (!_expanded)
        {
            return;
        }

        _expanded = false;
        _progressTimer.Stop();
        StopMarquee();

        AnimateOpacity(ExpandedPanel, 0, 120, () =>
        {
            if (!_expanded)
            {
                ExpandedPanel.Visibility = Visibility.Collapsed;
            }
        });
        AnimateOpacity(CompactPanel, 1, 220);
        ApplyCorners();
        LayoutTo(animate: true, ease: _collapseEase, milliseconds: 260);
    }

    private void LayoutTo(bool animate, IEasingFunction? ease = null, int milliseconds = 300)
    {
        var width = TargetWidth();
        var height = TargetHeight();
        var left = _centerX - width / 2.0;

        if (!animate)
        {
            BeginAnimation(WidthProperty, null);
            BeginAnimation(HeightProperty, null);
            BeginAnimation(LeftProperty, null);
            Width = width;
            Height = height;
            Left = left;
            return;
        }

        // Largura e posição usam a mesma curva e duração, então o centro fica fixo durante a animação.
        var easing = ease ?? _expandEase;
        AnimateTo(WidthProperty, width, milliseconds, easing);
        AnimateTo(HeightProperty, height, milliseconds, easing);
        AnimateTo(LeftProperty, left, milliseconds, easing);
    }

    private double TargetWidth()
    {
        if (_expanded)
        {
            return _settings.Width;
        }

        return _snapshot is null ? IdleWidth : CompactWidth;
    }

    private double TargetHeight()
    {
        if (_expanded)
        {
            return ExpandedHeight;
        }

        return _snapshot is null ? IdleHeight : CompactHeight;
    }

    private void AnimateTo(DependencyProperty property, double to, int milliseconds, IEasingFunction easing)
    {
        var from = (double)GetValue(property);
        var animation = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(milliseconds))
        {
            EasingFunction = easing
        };

        animation.Completed += (_, _) =>
        {
            BeginAnimation(property, null);
            SetValue(property, to);
        };

        BeginAnimation(property, animation);
    }

    private static void AnimateOpacity(UIElement element, double to, int milliseconds, Action? onCompleted = null)
    {
        var animation = new DoubleAnimation(to, TimeSpan.FromMilliseconds(milliseconds))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        if (onCompleted is not null)
        {
            animation.Completed += (_, _) => onCompleted();
        }

        element.BeginAnimation(OpacityProperty, animation);
    }

    private void RefreshMarquee()
    {
        TitleShift.BeginAnimation(TranslateTransform.XProperty, null);
        TitleShift.X = 0;

        if (!_expanded || _snapshot is null)
        {
            return;
        }

        TitleText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var overflow = TitleText.DesiredSize.Width - TitleClip.ActualWidth;
        if (overflow <= 1)
        {
            return;
        }

        var travel = overflow + 24;
        var animation = new DoubleAnimation(0, -travel, TimeSpan.FromSeconds(Math.Max(3, travel / 28.0)))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            BeginTime = TimeSpan.FromSeconds(1.2)
        };

        TitleShift.BeginAnimation(TranslateTransform.XProperty, animation);
    }

    private void StopMarquee() => TitleShift.BeginAnimation(TranslateTransform.XProperty, null);

    private void Island_MouseEnter(object sender, MouseEventArgs e)
    {
        _leaveTimer.Stop();
        _enterTimer.Start();
    }

    private void Island_MouseLeave(object sender, MouseEventArgs e)
    {
        _enterTimer.Stop();
        _leaveTimer.Start();
    }

    private void Island_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_snapshot is null)
        {
            return;
        }

        SystemVolume.Step(e.Delta > 0);
        e.Handled = true;
    }

    private void Progress_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_snapshot is null || _snapshot.Duration <= TimeSpan.Zero || ProgressHit.ActualWidth <= 0)
        {
            return;
        }

        var ratio = Math.Clamp(e.GetPosition(ProgressHit).X / ProgressHit.ActualWidth, 0, 1);
        var target = TimeSpan.FromTicks((long)(_snapshot.Duration.Ticks * ratio));

        // Atualização otimista; o próximo snapshot do SMTC corrige se necessário.
        _snapshot = _snapshot with { Position = target, CapturedAtUtc = DateTimeOffset.UtcNow };
        UpdateProgress();
        _ = _media.SeekAsync(target);
        e.Handled = true;
    }

    private void ProgressHit_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateProgress();

    private void Prev_Click(object sender, RoutedEventArgs e) => _ = _media.PreviousAsync();

    private void PlayPause_Click(object sender, RoutedEventArgs e) => _ = _media.TogglePlayPauseAsync();

    private void Next_Click(object sender, RoutedEventArgs e) => _ = _media.NextAsync();

    private static Color ToColor(RgbColor color) => Color.FromRgb(color.R, color.G, color.B);
}
