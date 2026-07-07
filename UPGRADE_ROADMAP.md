# TeknoParrotUI Upgrade & Modernization Roadmap

**Last Updated**: July 7, 2026  
**Status**: Phases 1–3 COMPLETE, Phase 4 near-complete (branch `net8-migration`)

> **Progress**:
> - ✅ **Phase 1 done** — All projects on .NET 8 (SDK-style, PackageReference, CefSharp.Wpf.NETCore). Costura/Fody removed; `publish.ps1` replaces it.
> - ✅ **Phase 2 done** — All packages updated (CefSharp 149, MaterialDesign 5.3.2, YamlDotNet 18, RawInput.Sharp 0.1.3). Vulnerability audit clean.
> - ✅ **Phase 3 done** — Auto dark mode, library search perf/UX, tabbed settings, session toast, publish script, CI workflow.
> - 🔄 **Phase 4 near-complete** — `TeknoParrotUi.Common` is UI-framework-free on plain net8.0 (Linux verified). **TeknoParrotUi.Avalonia** covers the full daily workflow: library (icons/genres/search), add/remove games, game scanner, per-game settings, input binding (XInput/DirectInput/RawInput incl. keyboard + lightgun cursor), file verification, component updater, mods browser, TPO lobbies, browser-based OAuth login (CEF-free, cross-platform), app settings with hotkey capture.
>
> **Remaining for full WPF retirement**:
> 1. **Native launch validation** — the launch pipeline (JVS, pipes, input, per-game fixes, process management) is now extracted into `TeknoParrotUi.Common.GameLaunch` and available in the Avalonia library as **"Native launcher (experimental)"** (default off; classic CLI remains the default). It needs per-emulator-family gameplay testing before becoming the default. Dolphin/Play/RPCS3/cxbxr/pcsx2x6/SegaTools types still route through the classic exe.
> 2. **teknoparrot.com deployment** — the OAuth loopback-redirect change (committed in the TeknoParrotDotCom repo) must be deployed for browser login to work.
> 3. Setup wizard / first-run experience (scanner covers bulk setup).

---

## 📊 Executive Summary

This document outlines a comprehensive upgrade strategy for TeknoParrotUI to:
1. **Modernize** the .NET runtime and dependencies
2. **Improve** UI/UX with modern design patterns
3. **Enable cross-platform** support (Windows, Linux, macOS)

**Recommended Timeline**: 2-3 months (phased approach)

---

## 🔴 Current State Analysis

### .NET Runtime
- **Framework**: .NET Framework 4.6.2 (Released 2016, End-of-Life)
- **Impact**: No security updates, performance limitations, legacy only

### Projects
| Project | Framework | Platform | Target |
|---------|-----------|----------|--------|
| TeknoParrotUi | .NET Framework 4.6.2 | x86 only | WPF |
| TeknoParrotUi.Common | .NET Framework 4.6.1 | Any CPU | Library |
| ParrotPatcher | .NET Framework 4.6.2 | AnyCPU | WinForms |

### UI Framework
- **WPF** (Windows Presentation Foundation)
- **Theme**: Material Design 5.2.1 ✓ (modern)
- **Status**: Functional but Windows-only

### Key Dependencies
| Package | Version | Status | Notes |
|---------|---------|--------|-------|
| CefSharp.Wpf | 136.1.40 | ✓ Current | Good, embeds Chromium |
| MaterialDesignThemes | 5.2.1 | ✓ Modern | Excellent theming |
| Newtonsoft.Json | 13.0.3 | ✓ Current | JSON serialization |
| SharpDX | 4.2.0 | ⚠️ Stable | Input/DirectX access |
| System.* polyfills | 4.3.0 | ❌ Legacy | Needed for .NET 4.6, obsolete on .NET 8 |
| NETStandard.Library | 2.0.3 | ❌ Legacy | Compatibility layer, unneeded on .NET 8 |

### Language Features
- **C# Version**: 9.0 (enabled, good)
- **Modern Syntax**: Supported but not fully utilized

---

## 🎯 Phase 1: Modernize to .NET 8 (CRITICAL)

### Why .NET 8?
- ✅ **LTS Support** until November 2026 (7+ years)
- ✅ **Performance** improvements (30-50% in many scenarios)
- ✅ **Modern Language Features** (C# 12)
- ✅ **Security** ongoing patches
- ✅ **Cross-platform Ready** (prerequisite for Avalonia)
- ✅ **Cloud-native Features** (AOT, containers)

### Migration Steps

#### 1.1 Convert .csproj to SDK Format (Optional but Recommended)
**Current Format**: Old-style .csproj with package imports  
**New Format**: Simplified SDK-style .csproj

**Benefits**:
- Cleaner configuration
- Automatic package restoration
- Better tooling support
- Easier to maintain

**Before** (TeknoParrotUi.csproj):
```xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="14.0" DefaultTargets="Build" xmlns="...">
  <Import Project="..\packages\..." />
  ...
  <PropertyGroup>
    <TargetFrameworkVersion>v4.6.2</TargetFrameworkVersion>
  </PropertyGroup>
```

**After**:
```xml
<Project Sdk="Microsoft.NET.Sdk.WindowsDesktop">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWpf>true</UseWpf>
    <LangVersion>12</LangVersion>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
```

#### 1.2 Update Target Framework

**TeknoParrotUi.csproj**:
```xml
<TargetFrameworkVersion>v4.6.2</TargetFrameworkVersion>
↓
<TargetFramework>net8.0-windows</TargetFramework>
<UseWpf>true</UseWpf>
```

**TeknoParrotUi.Common.csproj**:
```xml
<TargetFrameworkVersion>v4.6.1</TargetFrameworkVersion>
↓
<TargetFramework>net8.0</TargetFramework>
```

**ParrotPatcher.csproj**:
```xml
<TargetFrameworkVersion>v4.6.2</TargetFrameworkVersion>
↓
<TargetFramework>net8.0-windows</TargetFramework>
<UseWindowsFormsApp>true</UseWindowsFormsApp>
```

#### 1.3 Migrate from packages.config to PackageReference

**Remove obsolete files**:
- Delete `packages.config`
- NuGet will restore from `.csproj` PackageReference entries

**Add to .csproj**:
```xml
<ItemGroup>
  <PackageReference Include="CefSharp.Wpf" Version="136.1.40" />
  <PackageReference Include="MaterialDesignThemes" Version="5.2.1" />
  <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
  <!-- ... etc -->
</ItemGroup>
```

#### 1.4 Update & Prune NuGet Packages

**Packages to Update**:
```
CefSharp.Wpf:                136.1.40 → 136.1.40+ (check latest)
MaterialDesignThemes:        5.2.1 → 5.2.1+ (check latest)
Microsoft.IdentityModel.*:   8.9.0 → 8.9.0+ (security updates)
Newtonsoft.Json:             13.0.3 → 13.0.4+
Microsoft.Xaml.Behaviors:    1.1.77 → 1.1.77+
ControlzEx:                  7.0.1 → 7.0.1+
```

**Packages to REMOVE** (not needed on .NET 8):
```
NETStandard.Library (2.0.3)          - .NET 8 includes all
System.AppContext (4.3.0)            - Built-in
System.Collections (4.3.0)           - Built-in
System.Console (4.3.1)               - Built-in
System.Diagnostics.* (4.3.0)         - Built-in
System.Globalization (4.3.0)         - Built-in
Microsoft.Bcl.* (compatibility)      - Not needed
Microsoft.Build.Framework (15.9.20)  - Optional, check if needed
Resource.Embedder (2.2.0)            - Evaluate if needed
ShowMeTheXAML (1.0.12)               - Debug tool, optional
```

**Packages to Keep**:
```
CefSharp.Wpf, CefSharp.Common, Chromium runtime
MaterialDesignThemes, MaterialDesignColors
Newtonsoft.Json, Newtonsoft.Json.Bson
SharpDX.* (DirectInput, XInput support)
RawInput.Sharp
Fody, Costura.Fody (for assembly embedding)
ControlzEx, Microsoft.Xaml.Behaviors
```

#### 1.5 Handle Breaking Changes

**Binding Redirects**:
- .NET Framework needed `bindingRedirect` for version conflicts
- .NET 8 handles this automatically
- Remove `app.config` redirect sections or convert to `.csproj` properties

**API Changes**:
- Some legacy APIs deprecated (check compiler warnings)
- Most WPF code compatible without changes
- Platform-specific code may need `RuntimeInformation` checks

**Example - Platform Detection**:
```csharp
using System.Runtime.InteropServices;

if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    // Windows-specific code
}
```

#### 1.6 Testing Checklist

- [ ] Project compiles without errors
- [ ] All NuGet packages resolve
- [ ] WPF windows render correctly
- [ ] Bindings work as expected
- [ ] Material Design theming applies
- [ ] CefSharp browser embeds correctly
- [ ] Game launching still works
- [ ] Input/gamepad detection still works
- [ ] Game profiles load/save correctly
- [ ] All unit tests pass

---

## 🟡 Phase 2: NuGet Package Updates

### Update Strategy

**Immediate Updates** (Low Risk):
```
Newtonsoft.Json:             13.0.3 → 13.0.4
Microsoft.IdentityModel.*:   8.9.0 → 8.10.0+ (security patches)
ControlzEx:                  7.0.1 → 7.0.1+ (minor updates)
```

**Evaluate Before Updating** (Medium Risk):
```
CefSharp.Wpf:                Check if 137.x or later available
                             (Chromium updates can cause issues)

MaterialDesignThemes:        Monitor for Material Design 3 support
                             (5.2.1 → 6.x when stabilized)
```

**Keep Current** (Stable):
```
SharpDX:                     4.2.0 (mature, stable)
Fody/Costura.Fody:           6.x (working well)
```

### Dependency Tree Analysis

```
TeknoParrotUi (net8.0-windows)
├── CefSharp.Wpf → CefSharp.Common → Chromium runtime
├── MaterialDesignThemes → MaterialDesignColors
├── Newtonsoft.Json
├── SharpDX.DirectInput, SharpDX.XInput
├── RawInput.Sharp
└── Microsoft.IdentityModel.* (JWT/Auth support)

TeknoParrotUi.Common (net8.0)
├── CefSharp.Common
├── Microsoft.Bcl.AsyncInterfaces
└── Various System.* packages (remove on .NET 8)

ParrotPatcher (net8.0-windows)
└── CefSharp.Wpf (x64 variant)
```

---

## 🟢 Phase 3: UI/UX Modernization

### 3.1 Visual Enhancements

#### Material Design 3 Upgrade (When Available)
- Current: Material Design 5.2.1 (MD2)
- Target: Material Design 3 (when WPF support available)
- Benefits: Modern colors, improved typography, better accessibility

#### Dark Mode Support
```csharp
// Detect system theme
if (SystemParameters.HighContrast)
{
    // Apply high-contrast theme
}

// Listen for theme changes
SystemParameters.StaticPropertyChanged += (s, e) =>
{
    if (e.PropertyName == "HighContrast")
    {
        // Update theme
    }
};
```

#### High-DPI Scaling
```xaml
<!-- Enable DPI-aware rendering -->
<Window ...>
    <WindowChrome.WindowChrome>
        <WindowChrome 
            CaptionHeight="32" 
            UseAeroCaptionButtons="False" />
    </WindowChrome.WindowChrome>
</Window>
```

### 3.2 Component Improvements

**Game List View**:
- [ ] Add search/filter with debouncing
- [ ] Real-time game launch status
- [ ] Drag-and-drop reordering
- [ ] Multi-select operations
- [ ] Game cover art display

**Settings Panel**:
- [ ] Tabbed interface (General, Input, Graphics, Advanced)
- [ ] Collapsible sections
- [ ] Reset to defaults button
- [ ] Profile import/export

**Input Configuration**:
- [ ] Live gamepad visualization
- [ ] Button mapping UI
- [ ] Rumble/haptic feedback preview
- [ ] Calibration wizard

**Status/Feedback**:
- [ ] Toast notifications for game state changes
- [ ] Progress bar for emulator loading
- [ ] Detailed error messages with recovery suggestions

### 3.3 Architecture Improvements

**MVVM Toolkit Integration**:
```csharp
// Replace manual implementation with toolkit
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;

public partial class GameListViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<GameProfile> games;
    
    [RelayCommand]
    private async Task LaunchGame(GameProfile game)
    {
        // Clean implementation
    }
}
```

**Dependency Injection** (Built-in to .NET 8):
```csharp
var services = new ServiceCollection();
services.AddSingleton<IGameProfileService, GameProfileService>();
services.AddSingleton<MainWindow>();

var provider = services.BuildServiceProvider();
var mainWindow = provider.GetRequiredService<MainWindow>();
```

**Reactive Extensions** (Optional):
```csharp
// For event handling and async workflows
using System.Reactive;
using System.Reactive.Linq;

_gameService.GameLaunched
    .Throttle(TimeSpan.FromMilliseconds(500))
    .Subscribe(game => UpdateUI(game));
```

---

## 🌍 Phase 4: Cross-Platform Architecture (Future)

### 4.1 Why Avalonia?

**Framework Comparison**:
| Feature | WPF | Avalonia | .NET MAUI | Uno |
|---------|-----|----------|-----------|-----|
| **Platforms** | Windows only | Win/Linux/Mac | Win/Mac/Mobile | Win/Web/Mobile |
| **XAML** | Yes | Yes (99% compatible) | Yes | Yes |
| **Maturity** | Mature | Production-ready | Stabilizing | Experimental |
| **CefSharp** | ✓ Native | ✓ Supported | ⚠️ Experimental | ⚠️ Limited |
| **Performance** | Excellent | Good | Good | Good |
| **Learning Curve** | Steep | Gentle (from WPF) | Medium | Medium |
| **Community** | Large | Growing | Large (Microsoft) | Smaller |

**Choice Rationale**: Avalonia offers the **smoothest migration path** from WPF with minimal code changes and excellent cross-platform support.

### 4.2 Migration Strategy

#### Phase 4a: Dependency Extraction (0-2 weeks)

**Goal**: Separate platform-agnostic code from WPF

**Create Interface Layer**:
```csharp
// TeknoParrotUi.Abstractions
public interface IGameLauncher
{
    Task LaunchGame(GameProfile profile);
    event EventHandler<GameStateChangedEventArgs> StateChanged;
}

public interface IFileService
{
    Task<string> OpenFileDialog();
    Task<string> SaveFileDialog();
}

public interface IInputService
{
    IObservable<GamepadInput> GamepadInputs { get; }
    void RefreshControllers();
}
```

**Move Core Logic**:
```
Current:
├── TeknoParrotUi (WPF + Logic mixed)
└── TeknoParrotUi.Common (Shared)

After:
├── TeknoParrotUi.Abstractions (Interfaces)
├── TeknoParrotUi.Core (Pure C#, no UI)
├── TeknoParrotUi.Common (Shared)
├── TeknoParrotUi (WPF Implementation)
└── TeknoParrotUi.Avalonia (Future)
```

#### Phase 4b: Create Avalonia Branch (3-4 weeks)

**Step 1: Set up Avalonia project**:
```bash
dotnet new avalonia --name TeknoParrotUi.Avalonia --targetframework net8.0
```

**Step 2: Port XAML** (99% copy-paste):
```xaml
<!-- Before (WPF) -->
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">

<!-- After (Avalonia) -->
<Window xmlns="https://github.com/avaloniaui">
```

**Step 3: Reuse ViewModels** (No changes needed):
```csharp
// Same C# code works on both WPF and Avalonia
public class GameListViewModel : INotifyPropertyChanged { ... }
```

**Step 4: Platform-specific Implementation**:
```csharp
// Services folder structure
Services/
├── IGameLauncher.cs (interface)
├── Windows/
│   └── WindowsGameLauncher.cs
├── Linux/
│   └── LinuxGameLauncher.cs
└── macOS/
    └── MacGameLauncher.cs
```

#### Phase 4c: Handle Platform Differences

**File Paths**:
```csharp
// Instead of: @"C:\Games\TeknoParrot"
// Use:
Path.Combine(Environment.GetFolderPath(
    Environment.SpecialFolder.MyDocuments), 
    "Games", "TeknoParrot");
```

**Process Launching**:
```csharp
public async Task LaunchGame(GameProfile profile)
{
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        await LaunchGameWindows(profile);
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    {
        await LaunchGameLinux(profile);
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
        await LaunchGameMac(profile);
    }
}
```

**Input Handling**:
```csharp
// SharpDX (Windows-only)
#if WINDOWS
using SharpDX.DirectInput;
#endif

// For cross-platform, use:
// - Avalonia.Input for keyboard/mouse
// - SDL2# or vJoy wrappers for gamepad
```

**Registry Access** (Windows-only):
```csharp
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    var key = Registry.CurrentUser.OpenSubKey(@"Software\TeknoParrot");
    // Windows-specific settings
}
else
{
    // Use JSON/YAML config files
    var configPath = Path.Combine(
        Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData),
        ".teknoparrot", "config.json");
}
```

#### Phase 4d: Testing on Multiple Platforms

**Windows**:
- Develop/test environment (primary)
- CefSharp, SharpDX, all libraries work
- Performance baseline

**Linux** (Ubuntu 22.04+ recommended):
- Use WSL2 or VM for development
- Test CefSharp build on Linux x64
- Verify game profile loading
- Test input emulation

**macOS** (if available):
- Test CefSharp on macOS
- Verify path handling
- Test notarization for distribution

### 4.3 Avalonia-Specific Updates

**CefSharp on Avalonia**:
```csharp
// Avalonia doesn't have native WPF CefSharp integration
// Solutions:
// 1. Use native Chromium (CEF) directly via P/Invoke
// 2. Use WebView2 on Windows + Chromium on Linux/Mac
// 3. Switch to lighter embedded browser library
```

**Material Design on Avalonia**:
```
Install: Avalonia.Themes.Fluent
Or: https://github.com/AvaloniaUI/MaterialDesignThemes
```

**Keyboard/Mouse**:
- Avalonia input system ✓ (cross-platform)
- Events same as WPF ✓

**Gamepad Input**:
```csharp
// Platform-specific implementation needed
// Windows: SharpDX.DirectInput
// Linux: evdev via P/Invoke
// macOS: IOKit via P/Invoke
```

---

## 📋 Implementation Roadmap

### **Timeline**: 8-12 weeks (Recommended Phases)

```
Week 1-2:   Phase 1 - .NET 8 Migration (WPF)
            ├─ Convert .csproj files
            ├─ Update target frameworks
            ├─ Migrate to PackageReference
            ├─ Prune obsolete packages
            ├─ Test on Windows
            └─ Commit to main branch

Week 3:     Phase 2 - NuGet Updates
            ├─ Update packages to latest stable
            ├─ Fix any compatibility issues
            ├─ Run full test suite
            └─ Deploy release

Week 4-5:   Phase 3 - UI Modernization
            ├─ Implement dark mode
            ├─ Improve game list UI
            ├─ Add search/filter
            ├─ Update settings layout
            └─ Enhance error messages

Week 6-7:   (Maintenance & Bug Fixes)
            ├─ Gather user feedback
            ├─ Fix reported issues
            ├─ Performance optimization
            └─ Security patches

Week 8-12:  Phase 4 - Avalonia Migration (develop branch)
            ├─ Extract shared abstractions
            ├─ Set up Avalonia project
            ├─ Port XAML/ViewModels
            ├─ Test on Linux (WSL)
            ├─ Test on macOS (if available)
            └─ Prepare for stable release
```

### **Risk Mitigation**

| Risk | Severity | Mitigation |
|------|----------|-----------|
| .NET 8 compatibility | Medium | Extensive testing before release |
| CefSharp on Avalonia | High | Early PoC; evaluate alternatives if needed |
| Breaking changes in dependencies | Low | Test each update in isolation |
| Platform-specific bugs | Medium | Dedicated testing on each platform |
| User adoption of new features | Low | Gradual rollout with documentation |

---

## 🔧 Technical Decisions

### A. Should we convert to SDK-style .csproj?
**Recommendation**: ✅ **YES**
- Modern tooling requires it
- Cleaner configuration
- Easier for contributors
- Required for cross-platform

### B. Update to C# 12?
**Recommendation**: ✅ **YES**
- Leverage: Primary constructors, collection expressions
- Better code readability
- Performance improvements
- No breaking changes

### C. Enable nullable reference types?
**Recommendation**: ✅ **GRADUAL**
- Add `<Nullable>enable</Nullable>` to new projects
- Migrate existing code incrementally
- Reduces null-reference bugs

### D. Use dependency injection?
**Recommendation**: ✅ **YES**
- Better testability
- Cleaner architecture
- Avalonia supports DI natively
- Easier cross-platform implementation

### E. Adopt MVVM Toolkit?
**Recommendation**: ✅ **YES**
- Reduces boilerplate
- Better viewmodel implementation
- Observable collections simplified
- Easier migration to Avalonia

### F. Keep WPF or switch to Avalonia immediately?
**Recommendation**: ⏭️ **Keep WPF → Gradual Migration**
- Maintain stability (WPF proven)
- Develop Avalonia in parallel
- Switch once Avalonia version is production-ready
- Reduces risk of breaking production

---

## 📚 Resources & References

### .NET 8 Migration
- [.NET Framework to .NET](https://learn.microsoft.com/en-us/dotnet/core/porting/)
- [WPF in .NET 8](https://github.com/dotnet/wpf)
- [Breaking Changes in .NET 8](https://learn.microsoft.com/en-us/dotnet/core/compatibility/8.0)

### Avalonia
- [Avalonia Documentation](https://docs.avaloniaui.net/)
- [Avalonia Community Discord](https://discord.gg/tcZVp4b)
- [WPF to Avalonia Migration Guide](https://docs.avaloniaui.net/guides/porting-from-wpf)
- [Avalonia CefSharp Integration](https://github.com/AvaloniaUI/Avalonia.CefSharp.Example)

### NuGet Updates
- [NuGet Package Search](https://www.nuget.org/)
- [Dependabot for GitHub](https://dependabot.com/)
- [NuGet Audit for Security](https://learn.microsoft.com/en-us/nuget/reference/cli-reference/cli-ref-audit)

### Best Practices
- [.NET Best Practices](https://learn.microsoft.com/en-us/dotnet/fundamentals/)
- [MVVM Toolkit Documentation](https://learn.microsoft.com/en-us/windows/communitytoolkit/mvvm/mvvm_introduction)
- [C# 12 Features](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-12)

---

## ✅ Success Criteria

### Phase 1 (.NET 8) — ✅ COMPLETE
- [x] All projects compile on .NET 8
- [x] Game launching works
- [x] Input detection works
- [x] No performance regression
- [ ] Release published

### Phase 2 (Package Updates) — ✅ COMPLETE
- [x] All packages updated to latest stable
- [x] No security vulnerabilities
- [x] Full regression testing passes
- [ ] Release published

### Phase 3 (UI Modernization) — 🔄 IN PROGRESS
- [x] Dark mode working (follow-Windows-theme with live switching)
- [x] Game list search improved (in-memory filtering, hint, clear button)
- [x] Settings panel redesigned (tabbed: General / Appearance / Input / Online & Emulators)
- [x] Toast notification: session ended with play time
- [x] Release packaging (`publish.ps1`, Costura replacement)
- [ ] User feedback positive
- [ ] Release published

### Phase 4 (Cross-Platform)
- [ ] Avalonia version compiles on Windows
- [ ] Compiles on Linux (WSL test)
- [ ] Core functionality works on Linux
- [ ] CefSharp/browser working on Linux
- [ ] Input emulation working
- [ ] Stable release published

---

## 🚀 Next Steps

**Immediate** (Today):
1. ✅ Review this roadmap with team
2. ✅ Decide on timeline/priorities
3. ✅ Create feature branch for Phase 1

**Within 1 Week**:
1. Start .NET 8 migration
2. Set up test environment
3. Document breaking changes found

**Within 2 Weeks**:
1. Complete Phase 1 migration
2. Publish release
3. Gather user feedback

**Ongoing**:
1. Monitor NuGet updates
2. Plan Phase 3 features
3. Research Avalonia PoC

---

## 📞 Contact & Questions

For questions about this roadmap:
- Review the linked documentation
- Check the Avalonia Discord
- File GitHub issues with specific problems
- Consult Microsoft's .NET Migration docs

---

**Document Version**: 1.0  
**Last Updated**: July 7, 2026  
**Status**: For Review & Planning
