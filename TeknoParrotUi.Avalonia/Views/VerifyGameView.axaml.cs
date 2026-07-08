using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Avalonia.Views;

public partial class VerifyGameView : UserControl
{
    private GameProfile? _profile;
    private bool _cancel;

    public event Action? BackRequested;

    public VerifyGameView()
    {
        InitializeComponent();
        Localize();
        Services.Loc.LanguageChanged += Localize;
    }

    private void Localize()
    {
        BtnCancel.Content = Services.Loc.T("Cancel", "Cancel");
        BtnBack.Content = Services.Loc.T("Back", "Back");
    }

    public async void StartVerification(GameProfile profile)
    {
        _profile = profile;
        _cancel = false;
        BtnCancel.IsEnabled = true;
        BtnBack.IsEnabled = false;
        Header.Text = $"{profile.GameNameInternal ?? profile.ProfileName} — Verify Files";
        ResultsText.Text = "";
        Progress.Value = 0;

        var gamePath = Path.GetDirectoryName(profile.GamePath ?? "");
        if (string.IsNullOrEmpty(gamePath))
        {
            StatusText.Text = "Game executable path is not set — configure it in Game Settings first.";
            Finish();
            return;
        }
        if (!File.Exists(Lazydata.ParrotData.DatXmlLocation))
        {
            StatusText.Text = "DAT/XML file not found — set it in Settings → Game Scanner first.";
            Finish();
            return;
        }

        // Find this game's entry in the DAT
        DatXmlParser.DatGame? gameData = null;
        try
        {
            DatXmlParser.ProcessDatFileStreaming(
                Lazydata.ParrotData.DatXmlLocation,
                _ => { },
                game =>
                {
                    if (gameData == null && game.Name == profile.ProfileName?.Replace(".xml", ""))
                        gameData = game;
                });
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Failed to parse DAT file: {ex.Message}";
            Finish();
            return;
        }

        if (gameData == null || gameData.Roms == null || gameData.Roms.Count == 0)
        {
            StatusText.Text = "This game was not found in the DAT file.";
            Finish();
            return;
        }

        StatusText.Text = "Verifying files...";
        int valid = 0, invalid = 0, done = 0;
        int total = gameData.Roms.Count;
        TotalText.Text = $"Total: {total}";
        var results = new StringBuilder();

        foreach (var rom in gameData.Roms)
        {
            if (_cancel)
            {
                StatusText.Text = "Verification cancelled.";
                Finish();
                return;
            }

            var filePath = Path.Combine(gamePath, rom.Name.Replace('/', Path.DirectorySeparatorChar));
            var md5 = await Task.Run(() => CalculateMd5(filePath));
            bool ok = md5 != null && string.Equals(md5, rom.Md5, StringComparison.OrdinalIgnoreCase);

            if (ok) valid++;
            else
            {
                invalid++;
                results.AppendLine(md5 == null ? $"MISSING  {rom.Name}" : $"INVALID  {rom.Name}");
            }
            done++;

            ValidText.Text = $"Valid: {valid}";
            InvalidText.Text = $"Invalid: {invalid}";
            Progress.Value = (double)done / total * 100;
        }

        StatusText.Text = invalid == 0
            ? "Verification complete — all files are valid."
            : $"Verification complete — {invalid} file(s) missing or invalid:";
        ResultsText.Text = results.ToString();
        Finish();
    }

    private static string? CalculateMd5(string filename)
    {
        if (!File.Exists(filename) || filename.EndsWith("teknoparrot.ini", StringComparison.OrdinalIgnoreCase))
            return null;
        using var md5 = MD5.Create();
        using var stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, 81920);
        return Convert.ToHexString(md5.ComputeHash(stream)).ToLowerInvariant();
    }

    private void Finish()
    {
        BtnCancel.IsEnabled = false;
        BtnBack.IsEnabled = true;
    }

    private void BtnCancel_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => _cancel = true;
    private void BtnBack_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => BackRequested?.Invoke();
}
