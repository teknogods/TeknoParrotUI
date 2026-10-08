using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TeknoParrotUi.Common;
using TeknoParrotUi.Helpers;
using R = TeknoParrotUi.Properties.Resources;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Senjou no Kizuna's avatar editor (Library: PILOT AVATAR): the account's avatar, voice and greeting for each side, which
    /// the Kizuna server gives the account's pilots at their next card login. The parts and the website's names come from
    /// TeknoParrot.com (KizunaAvatarApi); the preview and the greetings from the user's own game files (KizunaAvatarGameData).
    /// The website checks every save; a side without an avatar shows the game's default in the game.
    /// </summary>
    public partial class KizunaAvatarEditor : Window
    {
        private readonly GameProfile _profile;
        private readonly Func<Task<string>> _token;
        private readonly KizunaAvatar[] _working = new KizunaAvatar[2];
        private readonly KizunaAvatar[] _baseline = new KizunaAvatar[2];
        private readonly List<ComboBox> _partBoxes = new List<ComboBox>();
        private readonly Dictionary<int, int?> _bodies = new Dictionary<int, int?>();
        private KizunaAvatarCatalogInfo _catalog;
        private KizunaAvatarAccount _account;
        private KizunaAvatarGameData _gameData;
        private SoundPlayer _player;
        private int _side;
        private bool _filling = true;
        private bool _busy;

        public KizunaAvatarEditor(GameProfile profile, Func<Task<string>> token)
        {
            _profile = profile;
            _token = token;
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            statusText.Text = R.KizunaAvatarLoading;
            var gamePath = _profile?.GamePath;
            string gameError = null;
            var gameData = Task.Run(() => KizunaAvatarGameData.Load(gamePath, out gameError));

            var token = await _token();
            var (catalogOutcome, catalog) = await KizunaAvatarApi.GetCatalogAsync(token);
            var accountOutcome = catalogOutcome;
            KizunaAvatarAccount account = null;
            if (catalogOutcome == KizunaAvatarApi.Outcome.Ok)
                (accountOutcome, account) = await KizunaAvatarApi.GetAccountAsync(token);
            _gameData = await gameData;
            if (_gameData == null)
                ShowPreviewMessage(string.Format(R.KizunaAvatarNoGameFiles, gameError));

            if (accountOutcome != KizunaAvatarApi.Outcome.Ok || catalog == null || account == null)
            {
                statusText.Text = accountOutcome == KizunaAvatarApi.Outcome.Unauthorized ? R.KizunaAvatarLoginNeeded : R.KizunaAvatarUnavailable;
                return;
            }

            _catalog = catalog;
            _account = account;
            for (var side = 0; side < 2; side++)
            {
                _working[side] = account.Side(side)?.Clone() ?? catalog.Default(side, false) ?? new KizunaAvatar();
                _baseline[side] = _working[side].Clone();
            }

            BuildFields();
            fields.IsEnabled = true;
            defaultButton.IsEnabled = true;
            saveButton.IsEnabled = true;
            statusText.Text = string.Empty;
            ShowSide(0);
        }

        // ---- fields ----------------------------------------------------------------------------------------------

        private void BuildFields()
        {
            bodyBox.Items.Add(Choice(R.KizunaAvatarMale, 1));
            bodyBox.Items.Add(Choice(R.KizunaAvatarFemale, 2));
            for (var voice = 0; voice < _catalog.Voices; voice++)
                voiceBox.Items.Add(Choice(string.Format(voice < _catalog.MaleVoices ? R.KizunaAvatarVoiceMale : R.KizunaAvatarVoiceFemale, voice + 1), voice));
            for (var greeting = 0; greeting < _catalog.Greetings; greeting++)
                greetingBox.Items.Add(Choice(string.Format(R.KizunaAvatarGreetingNumber, greeting + 1), greeting));

            foreach (var category in _catalog.Categories.OrderBy(category => category.Slot))
            {
                var row = partsGrid.RowDefinitions.Count;
                partsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var label = new TextBlock
                {
                    Text = R.ResourceManager.GetString("KizunaAvatarPart" + category.Name, R.Culture) ?? category.Name,
                    FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetRow(label, row);
                partsGrid.Children.Add(label);

                var box = new ComboBox { Margin = new Thickness(0, 4, 0, 4), Tag = category.Slot };
                if (!category.Required)
                    box.Items.Add(Choice(R.KizunaAvatarNone, 0));
                foreach (var item in category.Items)
                {
                    _bodies[item.Id] = item.Body;
                    box.Items.Add(Choice(string.IsNullOrEmpty(item.Name) ? item.Id.ToString() : $"{item.Id} – {item.Name}", item.Id));
                }

                box.SelectionChanged += Field_Changed;
                Grid.SetRow(box, row);
                Grid.SetColumn(box, 2);
                partsGrid.Children.Add(box);
                _partBoxes.Add(box);
            }
        }

        /// <summary>
        /// Each part lists the items made for the chosen body and those for both, unless all are asked for. The chosen item
        /// always stays listed: the game takes any part on any body.
        /// </summary>
        private void FilterParts()
        {
            var body = _working[_side]?.Female == true ? 2 : 1;
            var all = allBodiesBox.IsChecked == true;
            foreach (var box in _partBoxes)
            {
                foreach (var item in box.Items.OfType<ComboBoxItem>())
                {
                    var made = _bodies.TryGetValue((int)item.Tag, out var b) ? b : null;
                    item.Visibility = all || made == null || made == body || item.IsSelected ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        private static ComboBoxItem Choice(string text, int value) => new ComboBoxItem { Content = text, Tag = value };

        private static void Select(ComboBox box, int value) =>
            box.SelectedItem = box.Items.OfType<ComboBoxItem>().FirstOrDefault(item => (int)item.Tag == value);

        private static int Selected(ComboBox box, int fallback) => box.SelectedItem is ComboBoxItem item ? (int)item.Tag : fallback;

        private void ShowSide(int side)
        {
            _side = side;
            if (_catalog == null)
                return;
            var avatar = _working[side];
            _filling = true;
            Select(bodyBox, avatar.Gender);
            Select(voiceBox, avatar.Voice);
            Select(greetingBox, avatar.Greeting);
            foreach (var box in _partBoxes)
            {
                var slot = (int)box.Tag;
                Select(box, slot < avatar.Items.Length ? avatar.Items[slot] : 0);
            }

            _filling = false;
            FilterParts();
            Refresh();
        }

        private void Field_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_filling || _catalog == null)
                return;
            var avatar = _working[_side];
            avatar.Gender = Selected(bodyBox, avatar.Gender);
            avatar.Voice = Selected(voiceBox, avatar.Voice);
            avatar.Greeting = Selected(greetingBox, avatar.Greeting);
            foreach (var box in _partBoxes)
            {
                var slot = (int)box.Tag;
                if (slot < avatar.Items.Length)
                    avatar.Items[slot] = Selected(box, avatar.Items[slot]);
            }

            FilterParts();
            Refresh();
        }

        /// <summary>The preview, the side's status and the buttons for the current state.</summary>
        private void Refresh()
        {
            var avatar = _working[_side];
            if (_gameData != null)
                preview.Source = _gameData.Compose(avatar.Female, avatar.Items);

            var saved = _account?.Side(_side);
            var status = saved == null ? R.KizunaAvatarUsingDefault : R.KizunaAvatarUsingCustom;
            sideStatus.Text = Dirty(_side) ? status + " " + R.KizunaAvatarUnsaved : status;
            removeButton.IsEnabled = !_busy && saved != null;
            saveButton.IsEnabled = !_busy && _catalog != null;
            defaultButton.IsEnabled = !_busy && _catalog != null;
            fields.IsEnabled = !_busy && _catalog != null;
        }

        private bool Dirty(int side) => _working[side] != null && !_working[side].SameAs(_baseline[side]);

        private void ShowPreviewMessage(string message)
        {
            previewMessage.Text = message;
            previewMessage.Visibility = Visibility.Visible;
        }

        // ---- actions ---------------------------------------------------------------------------------------------

        private void Side_Checked(object sender, RoutedEventArgs e)
        {
            if (IsLoaded)
                ShowSide(sender == zeonTab ? 1 : 0);
        }

        private void AllBodies_Changed(object sender, RoutedEventArgs e)
        {
            if (_catalog != null)
                FilterParts();
        }

        /// <summary>The game's own parts and voice for this side and the body chosen in the form.</summary>
        private void LoadDefault_Click(object sender, RoutedEventArgs e)
        {
            var avatar = _catalog?.Default(_side, _working[_side].Female);
            if (avatar == null)
                return;
            _working[_side] = avatar;
            ShowSide(_side);
        }

        private async void Save_Click(object sender, RoutedEventArgs e) => await SaveAsync(_working[_side].Clone());

        /// <summary>Back to the game's default: the side's avatar is removed from the account.</summary>
        private async void Remove_Click(object sender, RoutedEventArgs e) => await SaveAsync(null);

        private async Task SaveAsync(KizunaAvatar avatar)
        {
            var side = _side;
            _busy = true;
            Refresh();
            statusText.Text = R.KizunaAvatarSaving;
            try
            {
                var (outcome, account) = await KizunaAvatarApi.SaveAsync(await _token(), side, avatar);
                if (outcome != KizunaAvatarApi.Outcome.Ok || account == null)
                {
                    statusText.Text = outcome == KizunaAvatarApi.Outcome.Unauthorized ? R.KizunaAvatarLoginNeeded : R.KizunaAvatarUnavailable;
                    return;
                }

                switch (account.Status)
                {
                    case "saved":
                    case "unchanged":
                    case "removed":
                        _account = account;
                        if (avatar == null)
                            _working[side] = _catalog.Default(side, _working[side].Female) ?? _working[side];
                        _baseline[side] = _working[side].Clone();
                        statusText.Text = avatar == null ? R.KizunaAvatarRemoved : R.KizunaAvatarSaved;
                        break;
                    default:
                        statusText.Text = R.KizunaAvatarInvalid;
                        break;
                }
            }
            finally
            {
                _busy = false;
                if (side == _side)
                    ShowSide(side);
                else
                    Refresh();
            }
        }

        private void PlayGreeting_Click(object sender, RoutedEventArgs e)
        {
            var avatar = _working[_side];
            var wav = avatar == null ? null : _gameData?.GreetingWav(avatar.Greeting, avatar.Voice);
            if (wav == null)
            {
                statusText.Text = R.KizunaAvatarNoSound;
                return;
            }

            _player?.Stop();
            _player = new SoundPlayer(new MemoryStream(wav));
            _player.Play();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if ((Dirty(0) || Dirty(1)) && !MessageBoxHelper.WarningYesNo(R.KizunaAvatarDiscardChanges))
            {
                e.Cancel = true;
                return;
            }

            _player?.Stop();
        }
    }
}
