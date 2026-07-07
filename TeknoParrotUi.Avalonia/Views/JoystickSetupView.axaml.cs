using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using TeknoParrotUi.Avalonia.Services;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Avalonia.Views;

public partial class JoystickSetupView : UserControl
{
    private GameProfile? _profile;
    private InputApi _api = InputApi.DirectInput;
    private readonly InputCaptureService _capture = new();
    private Button? _armedButton;
    private JoystickButtons? _armedBinding;

    public event Action? BackRequested;
    public event Action<string>? Saved;

    public JoystickSetupView()
    {
        InitializeComponent();
        _capture.BindingCaptured += captured => Dispatcher.UIThread.Post(() => OnCaptured(captured));
        Unloaded += (_, _) => _capture.Stop();
    }

    public void LoadProfile(GameProfile profile)
    {
        _profile = profile;
        _armedButton = null;
        _armedBinding = null;

        var apiString = profile.ConfigValues.FirstOrDefault(c => c.FieldName == "Input API")?.FieldValue;
        _api = apiString != null && Enum.TryParse<InputApi>(apiString, out var parsed) ? parsed : InputApi.DirectInput;

        Header.Text = $"{profile.GameNameInternal ?? profile.ProfileName} — Controls";
        ApiText.Text = _api is InputApi.RawInput or InputApi.RawInputTrackball
            ? $"Input API: {_api} — keyboard/mouse capture is not available here yet, use the Windows UI for this game."
            : $"Input API: {_api} — click a binding, then press the button or move the axis on your controller.";

        RowsPanel.Children.Clear();
        foreach (var button in profile.JoystickButtons.Where(IsVisibleForApi))
            RowsPanel.Children.Add(BuildRow(button));

        _capture.Stop();
        if (_api is InputApi.XInput or InputApi.DirectInput or InputApi.MergedInput)
            _capture.Start(_api);
    }

    private bool IsVisibleForApi(JoystickButtons b) => _api switch
    {
        InputApi.XInput => !b.HideWithXInput,
        InputApi.DirectInput => !b.HideWithDirectInput,
        InputApi.RawInput => !b.HideWithRawInput,
        InputApi.RawInputTrackball => !b.HideWithRawInputTrackball,
        _ => true
    };

    private string CurrentBindName(JoystickButtons b) => _api switch
    {
        InputApi.XInput => b.BindNameXi ?? b.BindName ?? "",
        InputApi.DirectInput => b.BindNameDi ?? b.BindName ?? "",
        InputApi.RawInput or InputApi.RawInputTrackball => b.BindNameRi ?? b.BindName ?? "",
        _ => b.BindName ?? ""
    };

    private Control BuildRow(JoystickButtons binding)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("240,*,Auto"), Margin = new global::Avalonia.Thickness(0, 2, 0, 2) };

        var label = new TextBlock { Text = binding.ButtonName, VerticalAlignment = VerticalAlignment.Center, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap };
        if (!string.IsNullOrWhiteSpace(binding.Hint))
            ToolTip.SetTip(label, binding.Hint);

        var bindButton = new Button
        {
            Content = string.IsNullOrWhiteSpace(CurrentBindName(binding)) ? "(not bound)" : CurrentBindName(binding),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsEnabled = _api is InputApi.XInput or InputApi.DirectInput or InputApi.MergedInput
        };
        bindButton.Click += (_, _) => Arm(bindButton, binding);

        var clearButton = new Button { Content = "✕", Margin = new global::Avalonia.Thickness(6, 0, 0, 0) };
        ToolTip.SetTip(clearButton, "Clear binding");
        clearButton.Click += (_, _) =>
        {
            ClearBinding(binding);
            bindButton.Content = "(not bound)";
        };

        Grid.SetColumn(label, 0);
        Grid.SetColumn(bindButton, 1);
        Grid.SetColumn(clearButton, 2);
        grid.Children.Add(label);
        grid.Children.Add(bindButton);
        grid.Children.Add(clearButton);
        return grid;
    }

    private void Arm(Button button, JoystickButtons binding)
    {
        if (_armedButton != null)
            _armedButton.Content = ArmedOriginalText ?? "(not bound)";

        _armedButton = button;
        _armedBinding = binding;
        ArmedOriginalText = button.Content as string;
        button.Content = "Press a button / move an axis...";
    }

    private string? ArmedOriginalText;

    private void OnCaptured(CapturedBinding captured)
    {
        if (_armedButton == null || _armedBinding == null)
            return;

        switch (_api)
        {
            case InputApi.XInput when captured.XInput != null:
                _armedBinding.XInputButton = captured.XInput;
                _armedBinding.BindNameXi = captured.DisplayName;
                _armedBinding.BindName = captured.DisplayName;
                break;
            case InputApi.DirectInput when captured.DirectInput != null:
                _armedBinding.DirectInputButton = captured.DirectInput;
                _armedBinding.BindNameDi = captured.DisplayName;
                _armedBinding.BindName = captured.DisplayName;
                break;
            case InputApi.MergedInput:
                if (captured.XInput != null) { _armedBinding.XInputButton = captured.XInput; _armedBinding.BindNameXi = captured.DisplayName; }
                if (captured.DirectInput != null) { _armedBinding.DirectInputButton = captured.DirectInput; _armedBinding.BindNameDi = captured.DisplayName; }
                _armedBinding.BindName = captured.DisplayName;
                break;
            default:
                return;
        }

        _armedButton.Content = captured.DisplayName;
        _armedButton = null;
        _armedBinding = null;
        ArmedOriginalText = null;
    }

    private void ClearBinding(JoystickButtons binding)
    {
        switch (_api)
        {
            case InputApi.XInput:
                binding.XInputButton = null;
                binding.BindNameXi = null;
                break;
            case InputApi.DirectInput:
                binding.DirectInputButton = null;
                binding.BindNameDi = null;
                break;
            default:
                binding.XInputButton = null;
                binding.DirectInputButton = null;
                binding.BindNameXi = null;
                binding.BindNameDi = null;
                break;
        }
        binding.BindName = null;
    }

    private void BtnBack_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        _capture.Stop();
        BackRequested?.Invoke();
    }

    private void BtnSave_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_profile == null) return;
        System.IO.Directory.CreateDirectory("UserProfiles");
        JoystickHelper.SerializeGameProfile(_profile);
        Saved?.Invoke(_profile.GameNameInternal ?? _profile.ProfileName ?? "profile");
        _capture.Stop();
        BackRequested?.Invoke();
    }
}
