# Isolated news history checks

Build `NewsHistory.csproj` with Visual Studio MSBuild, then run `bin/NewsHistoryTests.exe`.

The project links the current announcement service, window XAML, and window code directly. Test resource strings stand in for the main application's resources so unrelated local application changes cannot block compilation. The window is compiled; the checks exercise the service with fake HTTP responses and do not open a browser or write user settings.

```powershell
& 'C:/Program Files/Microsoft Visual Studio/2022/Professional/MSBuild/Current/Bin/MSBuild.exe' Tests/NewsHistory/NewsHistory.csproj /t:Build /v:minimal /nologo
& Tests/NewsHistory/bin/NewsHistoryTests.exe
```

Validated: 20 checks passed on 2026-10-07. The full application still has existing unrelated Initial D compilation errors; see the website checkout's NEWS_HISTORY.md for the combined validation notes.
