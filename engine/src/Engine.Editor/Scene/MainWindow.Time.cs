using System.Globalization;

namespace Engine.Editor;

public partial class MainWindow
{
    private static readonly double[] PlaybackSpeeds = [0.25, 0.5, 1, 2, 4, 8];
    private bool syncingTime;
    private double displayedScale = double.NaN;

    private void InitializeTimeControls()
    {
        PauseButton.Click += (_, _) =>
        {
            if (Viewport.PlayTime is not { } time) return;
            time.IsPaused = !time.IsPaused;
            SyncTimeControls();
            Viewport.Focus();
        };
        StepButton.Click += (_, _) =>
        {
            Viewport.PlayTime?.StepOnce();
            Viewport.Focus();
        };
        SpeedPicker.SelectionChanged += (_, _) =>
        {
            int i = SpeedPicker.SelectedIndex;
            if (!syncingTime && Viewport.PlayTime is { } time && i >= 0 && i < PlaybackSpeeds.Length)
            {
                time.Scale = PlaybackSpeeds[i];
                Viewport.Focus();
            }
        };
    }

    private void SyncTimeControls()
    {
        var time = Viewport.PlayTime;
        TimeControls.IsVisible = PauseButton.IsEnabled = time != null;
        if (time == null) { StepButton.IsEnabled = false; return; }
        var pause = time.IsPaused ? "Resume" : "Pause";
        if (Avalonia.Automation.AutomationProperties.GetName(PauseButton) != pause)
        {
            Avalonia.Automation.AutomationProperties.SetName(PauseButton, pause);
            PauseButton.Content = EditorTokens.Icon(time.IsPaused ? "IconPlay" : "IconPause");
        }
        StepButton.IsEnabled = time.IsPaused;
        if (displayedScale == time.Scale) return;
        displayedScale = time.Scale;
        syncingTime = true;
        try
        {
            // A script may select a speed between the editor presets.
            var speeds = PlaybackSpeeds.Contains(time.Scale) ? PlaybackSpeeds : [..PlaybackSpeeds, time.Scale];
            SpeedPicker.ItemsSource = speeds.Select(speed => speed.ToString("G", CultureInfo.InvariantCulture) + "×").ToArray();
            SpeedPicker.SelectedIndex = Array.IndexOf(speeds, time.Scale);
        }
        finally { syncingTime = false; }
    }
}
