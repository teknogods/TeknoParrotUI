using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using TeknoParrotUi.Avalonia.Services;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.Online;

namespace TeknoParrotUi.Avalonia.Views;

public partial class KizunaAvatarView : UserControl
{
    private sealed record Choice(string Name, int Value) { public override string ToString() => Name; }
    private Func<Task<string>> _token = () => Task.FromResult<string>(null!);
    private KizunaAvatarCatalogInfo? _catalog;
    private KizunaAvatarAccount? _account;
    private KizunaAvatarGameData? _gameData;
    private readonly KizunaAvatar?[] _working = new KizunaAvatar?[2], _baseline = new KizunaAvatar?[2];
    private readonly Dictionary<int, ComboBox> _parts = new();
    private bool _filling, _busy;
    private int _side;
    private WriteableBitmap? _bitmap;
    private int _loadVersion;
    public bool HasUnsavedChanges => Dirty(0) || Dirty(1);
    public event Action? BackRequested;
    public KizunaAvatarView()
    {
        InitializeComponent();
        Heading.Text = Loc.T("KizunaAvatarEditorTitle");
        Play.Content = Loc.T("KizunaAvatarPlay");
        AllBodies.Content = Loc.T("KizunaAvatarAllBodies");
        Save.Content = Loc.T("KizunaAvatarSave");
        Defaults.Content = Loc.T("KizunaAvatarLoadDefault");
        Remove.Content = Loc.T("KizunaAvatarRemove");
        Back.Content = Loc.T("Back", "Back");
        DetachedFromVisualTree += (_, _) =>
        { _loadVersion++; AvatarAudio.Stop(); Preview.Source = null; _bitmap?.Dispose(); _bitmap = null; };
        SizeChanged += (_, _) =>
        {
            var narrow = Bounds.Width < 700;
            EditorGrid.ColumnDefinitions = new ColumnDefinitions(narrow ? "*" : "320,*");
            EditorGrid.RowDefinitions = new RowDefinitions(narrow ? "*,Auto" : "*");
            if (Preview.Parent is Control previewPanel)
            { Grid.SetColumn(previewPanel, narrow ? 0 : 1); Grid.SetRow(previewPanel, narrow ? 1 : 0); }
        };
    }
    public async Task LoadAsync(GameProfile profile, Func<Task<string>> token)
    {
        var version = ++_loadVersion;
        _token = token; _catalog = null; _account = null; _busy = true;
        Fields.IsEnabled = Save.IsEnabled = Defaults.IsEnabled = Remove.IsEnabled = false;
        Status.Text = Loc.T("KizunaAvatarLoading");
        string? error = null;
        var data = Task.Run(() => KizunaAvatarGameData.Load(profile.GamePath, out error));
        var access = await token();
        var (result, catalog) = await KizunaAvatarApi.GetCatalogAsync(access);
        KizunaAvatarAccount? account = null;
        if (result == KizunaAvatarApi.Outcome.Ok) (result, account) = await KizunaAvatarApi.GetAccountAsync(access);
        _gameData = await data;
        if (version != _loadVersion) return;
        PreviewStatus.Text = _gameData == null ? string.Format(Loc.T("KizunaAvatarNoGameFiles"), error) : "";
        _busy = false;
        if (result != KizunaAvatarApi.Outcome.Ok || catalog == null || account == null)
        { Status.Text = Loc.T(result == KizunaAvatarApi.Outcome.Unauthorized ? "KizunaAvatarLoginNeeded" : "KizunaAvatarUnavailable"); return; }
        _catalog = catalog; _account = account;
        for (var side = 0; side < 2; side++)
        {
            _working[side] = account.Side(side)?.Clone() ?? catalog.Default(side, false) ?? new KizunaAvatar();
            _baseline[side] = _working[side]!.Clone();
        }
        _filling = true;
        Side.ItemsSource = new[] { "EFSF", "Zeon" };
        Body.ItemsSource = new[] { new Choice(Loc.T("KizunaAvatarMale"), 1), new Choice(Loc.T("KizunaAvatarFemale"), 2) };
        Voice.ItemsSource = Enumerable.Range(0, catalog.Voices).Select(value =>
            new Choice(string.Format(Loc.T(value < catalog.MaleVoices ? "KizunaAvatarVoiceMale" : "KizunaAvatarVoiceFemale"), value + 1), value)).ToList();
        Greeting.ItemsSource = Enumerable.Range(0, catalog.Greetings).Select(value =>
            new Choice(string.Format(Loc.T("KizunaAvatarGreetingNumber"), value + 1), value)).ToList();
        Parts.Children.Clear(); _parts.Clear();
        foreach (var category in catalog.Categories.OrderBy(category => category.Slot))
        {
            Parts.Children.Add(new TextBlock { Text = Loc.T("KizunaAvatarPart" + category.Name, category.Name), TextWrapping = TextWrapping.Wrap });
            var box = new ComboBox { Tag = category.Slot, HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch };
            box.SelectionChanged += FieldChanged;
            _parts[category.Slot] = box; Parts.Children.Add(box);
        }
        Side.SelectedIndex = 0; _filling = false; ShowSide(0);
    }
    private static int Selected(ComboBox box, int fallback) => (box.SelectedItem as Choice)?.Value ?? fallback;
    private static void Select(ComboBox box, int value) => box.SelectedItem = box.Items.OfType<Choice>().FirstOrDefault(item => item.Value == value);
    private void ShowSide(int side)
    {
        if (_catalog == null || _working[side] is not { } avatar) return;
        _side = side; _filling = true;
        Select(Body, avatar.Gender); Select(Voice, avatar.Voice); Select(Greeting, avatar.Greeting);
        foreach (var category in _catalog.Categories)
        {
            var selected = category.Slot < avatar.Items.Length ? avatar.Items[category.Slot] : 0;
            var choices = category.Items.Where(item => AllBodies.IsChecked == true || item.Body == null ||
                item.Body == avatar.Gender || item.Id == selected).Select(item => new Choice(
                    string.IsNullOrEmpty(item.Name) ? item.Id.ToString() : item.Id + " – " + item.Name, item.Id)).ToList();
            if (!category.Required) choices.Insert(0, new Choice(Loc.T("KizunaAvatarNone", "None"), 0));
            _parts[category.Slot].ItemsSource = choices; Select(_parts[category.Slot], selected);
        }
        _filling = false; RefreshPreview();
    }
    private void RefreshPreview()
    {
        if (_working[_side] is not { } avatar) return;
        if (_gameData != null)
        {
            var pixels = _gameData.Compose(avatar.Female, avatar.Items);
            _bitmap ??= new WriteableBitmap(new PixelSize(KizunaAvatarGameData.Width, KizunaAvatarGameData.Height),
                new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
            using (var framebuffer = _bitmap.Lock())
                for (var row = 0; row < KizunaAvatarGameData.Height; row++)
                    Marshal.Copy(pixels, row * KizunaAvatarGameData.Width * 4,
                        IntPtr.Add(framebuffer.Address, row * framebuffer.RowBytes), KizunaAvatarGameData.Width * 4);
            Preview.Source = _bitmap; Preview.InvalidateVisual();
        }
        Status.Text = Loc.T(_account?.Side(_side) == null ? "KizunaAvatarUsingDefault" : "KizunaAvatarUsingCustom") +
            (Dirty(_side) ? " " + Loc.T("KizunaAvatarUnsaved") : "");
        Fields.IsEnabled = Save.IsEnabled = Defaults.IsEnabled = !_busy;
        Remove.IsEnabled = !_busy && _account?.Side(_side) != null;
    }
    private bool Dirty(int side) => _working[side] != null && !_working[side]!.SameAs(_baseline[side]);
    private void SideChanged(object? sender, SelectionChangedEventArgs e) { if (!_filling && Side.SelectedIndex >= 0) ShowSide(Side.SelectedIndex); }
    private void FieldChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_filling || _working[_side] is not { } avatar) return;
        avatar.Gender = Selected(Body, avatar.Gender); avatar.Voice = Selected(Voice, avatar.Voice); avatar.Greeting = Selected(Greeting, avatar.Greeting);
        foreach (var (slot, box) in _parts) if (slot < avatar.Items.Length) avatar.Items[slot] = Selected(box, avatar.Items[slot]);
        if (ReferenceEquals(sender, Body)) ShowSide(_side); else RefreshPreview();
    }
    private void AllBodiesChanged(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) { if (!_filling) ShowSide(_side); }
    private void DefaultClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_catalog?.Default(_side, _working[_side]?.Female == true) is { } defaults) { _working[_side] = defaults; ShowSide(_side); }
    }
    private async void SaveClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => await SaveAsync(_working[_side]?.Clone());
    private async void RemoveClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => await SaveAsync(null);
    private async Task SaveAsync(KizunaAvatar? avatar)
    {
        if (_busy || _catalog == null) return;
        _busy = true; RefreshPreview(); Status.Text = Loc.T("KizunaAvatarSaving");
        var (outcome, account) = await KizunaAvatarApi.SaveAsync(await _token(), _side, avatar);
        _busy = false;
        if (outcome != KizunaAvatarApi.Outcome.Ok || account == null)
        { RefreshPreview(); Status.Text = Loc.T(outcome == KizunaAvatarApi.Outcome.Unauthorized ? "KizunaAvatarLoginNeeded" : "KizunaAvatarUnavailable"); return; }
        if (account.Status is not ("saved" or "unchanged" or "removed"))
        { RefreshPreview(); Status.Text = Loc.T("KizunaAvatarInvalid"); return; }
        _account = account;
        if (avatar == null) _working[_side] = _catalog.Default(_side, _working[_side]?.Female == true) ?? new KizunaAvatar();
        _baseline[_side] = _working[_side]!.Clone();
        ShowSide(_side); Status.Text = Loc.T(avatar == null ? "KizunaAvatarRemoved" : "KizunaAvatarSaved") + " " + Loc.T("KizunaAvatarNextLogin");
    }
    private async void PlayClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var avatar = _working[_side]; var wav = avatar == null ? null : _gameData?.GreetingWav(avatar.Greeting, avatar.Voice);
        if (wav == null) { Status.Text = Loc.T("KizunaAvatarNoSound"); return; }
        try { await AvatarAudio.PlayAsync(wav); } catch (Exception error) { Status.Text = error.Message; }
    }
    private async void BackClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!await ConfirmCloseAsync()) return;
        AvatarAudio.Stop(); BackRequested?.Invoke();
    }
    public async Task<bool> ConfirmCloseAsync() => !HasUnsavedChanges ||
        await Dialogs.ChooseAsync(this, Heading.Text ?? "", Loc.T("KizunaAvatarDiscardChanges"),
            new[] { Loc.T("Yes", "Yes"), Loc.T("Cancel", "Cancel") }) == 0;
}
