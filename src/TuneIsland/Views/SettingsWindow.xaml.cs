using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using TuneIsland.Core;
using TuneIsland.Services;

namespace TuneIsland.Views;

public partial class SettingsWindow : Window
{
    private readonly Action<AppSettings> _onSave;
    private readonly IReadOnlyList<DisplayInfo> _displays;

    public SettingsWindow(AppSettings current, Action<AppSettings> onSave)
    {
        _onSave = onSave;
        _displays = DisplayService.GetDisplays();

        InitializeComponent();

        foreach (var display in _displays)
        {
            MonitorBox.Items.Add(display.Name);
        }

        MonitorBox.SelectedIndex = Math.Clamp(current.MonitorIndex, 0, _displays.Count - 1);
        StyleBox.SelectedIndex = current.Style == IslandStyle.Floating ? 1 : 0;
        WidthSlider.Value = current.Width;
        ExpandSlider.Value = current.AutoExpandSeconds;
        StartupCheck.IsChecked = current.StartWithWindows;
        HideIdleCheck.IsChecked = current.HideWhenIdle;
        HideFullscreenCheck.IsChecked = current.HideInFullscreen;

        UpdateLabels();
    }

    private void WidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdateLabels();

    private void ExpandSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdateLabels();

    private void UpdateLabels()
    {
        WidthLabel.Text = $"Largura: {(int)WidthSlider.Value} px";
        ExpandLabel.Text = $"Auto-expansão ao trocar de faixa: {(int)ExpandSlider.Value} s";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var settings = new AppSettings
        {
            Style = StyleBox.SelectedIndex == 1 ? IslandStyle.Floating : IslandStyle.Attached,
            MonitorIndex = Math.Max(0, MonitorBox.SelectedIndex),
            Width = (int)WidthSlider.Value,
            AutoExpandSeconds = (int)ExpandSlider.Value,
            StartWithWindows = StartupCheck.IsChecked == true,
            HideWhenIdle = HideIdleCheck.IsChecked == true,
            HideInFullscreen = HideFullscreenCheck.IsChecked == true
        };

        settings.Clamp();
        _onSave(settings);
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
