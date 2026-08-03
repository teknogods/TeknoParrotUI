using System;
using System.Collections.Generic;

namespace TeknoParrotUi.Common
{
    /// <summary>
    /// Resolves the official TeknoParrot high-score page for a profile.
    /// Kept in the shared project so every UI uses the same profile mappings.
    /// </summary>
    public static class HighScoreUrlResolver
    {
        private static readonly IReadOnlyDictionary<string, string> ExternalGames =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["IDTA"] = "IDACS3",
                ["IDTAS5"] = "IDACS5",
                ["WMMT6RR"] = "WMMT6RR"
            };

        private static readonly IReadOnlyDictionary<string, string> Games =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["BattleGear4Tuned"] = "BattleGear4Tuned",
                ["CruisnBlast"] = "CruisnBlast",
                ["Daytona3"] = "Daytona3",
                ["Daytona3NSE"] = "Daytona3NSE",
                ["DeadHeat"] = "DeadHeat",
                ["DeadHeatRiders"] = "DeadHeatRiders",
                ["DirtyDrivin"] = "DirtyDrivin",
                ["FarCryParadiseLost"] = "FarCryParadiseLost",
                ["GaelcoChampionshipTuningRace"] = "GaelcoChampionshipTuningRace",
                ["H2Overdrive"] = "H2Overdrive",
                ["HOTD4"] = "HOTD4",
                ["HOTDSD"] = "HOTDSD",
                ["GoldenTeeLive2006"] = "gt06",
                ["GoldenTeeLive2007"] = "gt07",
                ["GoldenTeeLive2008"] = "gt08",
                ["GoldenTeeLive2009"] = "gt09",
                ["GoldenTeeLive2010"] = "gt10",
                ["GoldenTeeLive2011"] = "gt11",
                ["GoldenTeeLive2012"] = "gt12",
                ["GoldenTeeLive2013"] = "gt13",
                ["GoldenTeeLive2014"] = "gt14",
                ["GoldenTeeLive2015"] = "gt15",
                ["GoldenTeeLive2016"] = "gt16",
                ["GoldenTeeLive2017"] = "gt17",
                ["GoldenTeeLive2018"] = "gt18",
                ["GoldenTeeLive2019"] = "gt19",
                ["ID6"] = "ID6",
                ["ID7"] = "ID7",
                ["ID8"] = "ID8",
                ["or2spdlx"] = "or2spdlx",
                ["PowerPuttLive2012"] = "ppl12",
                ["PowerPuttLive2013"] = "ppl13",
                ["RastanSaga"] = "RastanSaga",
                ["SilverStrikeBowlingLive"] = "silverstrikelive",
                ["SR3"] = "SR3",
                ["SRC"] = "SRC",
                ["Taiko"] = "Taiko",
                ["TC5"] = "TC5",
                ["TargetTerrorGold"] = "TTG",
                ["WMMT3"] = "WMMT3",
                ["WMMT3DXP"] = "WMMT3DXPlus",
                ["WMMT5"] = "WMMT5",
                ["WMMT5DX"] = "WMMT5DX",
                ["WMMT5DXPlus"] = "WMMT5DXPlus",
                ["WMMT6"] = "WMMT6",
                ["WMMT6R"] = "WMMT6R"
            };

        private static readonly IReadOnlyDictionary<string, string> WebsiteLanguages =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en-US"] = "en",
                ["en"] = "en",
                ["fi-FI"] = "fi",
                ["ar-SA"] = "sa",
                ["de-DE"] = "de",
                ["es-ES"] = "es",
                ["fr-FR"] = "fr",
                ["he-IL"] = "il",
                ["it-IT"] = "it",
                ["ja-JP"] = "jp",
                ["ko-KR"] = "kr",
                ["nl-NL"] = "nl",
                ["pl-PL"] = "pl",
                ["pt-BR"] = "pt",
                ["pt-PT"] = "pt",
                ["ru-RU"] = "ru",
                ["zh-CN"] = "cn",
                ["zh-TW"] = "cn"
            };

        public static Uri Resolve(string profileName, string language)
        {
            if (string.IsNullOrWhiteSpace(profileName))
                return null;

            var websiteLanguage = ResolveWebsiteLanguage(language);
            if (ExternalGames.TryGetValue(profileName, out var externalIdentifier))
            {
                return new Uri(
                    $"https://teknoparrot.com/{websiteLanguage}/Highscore/GameSpecificExternal/{externalIdentifier}");
            }

            return Games.TryGetValue(profileName, out var gameIdentifier)
                ? new Uri(
                    $"https://teknoparrot.com/{websiteLanguage}/Highscore/GameSpecific/{gameIdentifier}")
                : null;
        }

        private static string ResolveWebsiteLanguage(string language)
        {
            if (!string.IsNullOrWhiteSpace(language) &&
                WebsiteLanguages.TryGetValue(language, out var exact))
            {
                return exact;
            }

            if (!string.IsNullOrWhiteSpace(language))
            {
                var separator = language.IndexOf('-');
                var languagePart = separator >= 0 ? language[..separator] : language;
                foreach (var entry in WebsiteLanguages)
                {
                    var entrySeparator = entry.Key.IndexOf('-');
                    var entryLanguage = entrySeparator >= 0
                        ? entry.Key[..entrySeparator]
                        : entry.Key;
                    if (string.Equals(languagePart, entryLanguage, StringComparison.OrdinalIgnoreCase))
                        return entry.Value;
                }
            }

            return "en";
        }
    }
}
